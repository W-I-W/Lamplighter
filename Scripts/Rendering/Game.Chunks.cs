using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Lamplighter;

/// <summary>Стриминг карты: мир хранится массивами (W.Ground/W.Obj), а на экране существуют только чанки 32×32 вокруг камеры.
/// Чанк — пара TileMapLayer (земля и объекты); узлов на клетку нет. Загрузка ограничена бюджетом на кадр, выгрузка — с гистерезисом.</summary>
public partial class Game
{
    const int CS = 32;
    const int ChunksX = World.Wd / CS, ChunksY = World.Ht / CS;
    sealed class Chunk { public TileMapLayer G, F, O, R, T, X1, X2, D; }       // R — деревья под игроком, T — деревья над игроком (полупрозрачные: игрок стоит за ними)
    readonly Chunk[] chunks = new Chunk[ChunksX * ChunksY];
    ColorRect backdrop; Node2D groundRoot, objRoot, treeRoot, topRoot; BreakAreaLayer breakArea; TileSet chunkSet;
    public int LoadedChunks; public float LastChunkMs;
    float chunkT;
    const int LoadR = 1, KeepR = 2;

    void InitChunks()
    {
        chunkSet = new TileSet { TileSize = new Vector2I(TS, TS) };
        var src = new TileSetAtlasSource { Texture = Art.TileTex, TextureRegionSize = new Vector2I(TS, TS), UseTexturePadding = false };   // без внутренних полей: шейдеры растительности читают атлас по колонкам 16 px
        for (int i = 0; i < Art.TileCount; i++)
        {
            if (Art.IsBig(i)) { if (Art.IsBigAnchor(i)) { var bc = new Vector2I(i, 0); src.CreateTile(bc, new Vector2I(2, 2)); int bf = SpriteLibrary.TileFrames[i];
                if (bf > 1) { src.SetTileAnimationColumns(bc, 1); src.SetTileAnimationFramesCount(bc, bf); for (int k = 0; k < bf; k++) src.SetTileAnimationFrameDuration(bc, k, .5f); src.SetTileAnimationMode(bc, TileSetAtlasSource.TileAnimationMode.RandomStartTimes); }       // покадровое качание; у каждого дерева своя фаза
                src.GetTileData(bc, 0).TextureOrigin = new Vector2I(0, 8); } continue; }       // дерево: один тайл 32×32 по центру клетки ствола, сдвинут вверх на 8 px (крона в клетке выше); кадры качания — столбиком в атласе
            var at = new Vector2I(i, 0); src.CreateTile(at); int fr = i < SpriteLibrary.TileFrames.Length ? SpriteLibrary.TileFrames[i] : 1;
            if (fr > 1) { src.SetTileAnimationColumns(at, 1); src.SetTileAnimationFramesCount(at, fr); for (int k = 0; k < fr; k++) src.SetTileAnimationFrameDuration(at, k, .22f); }          // кадры лежат столбиком под тайлом атласа
        }
        foreach (int c in AllTreeCols) for (int v = 1; v < TreeVariants; v++) { src.CreateAlternativeTile(new Vector2I(c, 0), v); { var td = src.GetTileData(new Vector2I(c, 0), v); td.Modulate = TreeTint(c, v); td.TextureOrigin = new Vector2I(0, 8); } }      // разновидности деревьев: оттенок листвы и коры
        srcId = chunkSet.AddSource(src);
        groundRoot = new Node2D(); foliageRoot = new Node2D(); objRoot = new Node2D(); treeRoot = new Node2D(); topRoot = new Node2D();
        blendRoot = new Node2D(); decorRoot = new Node2D(); InitBlend();
        AddChild(groundRoot); AddChild(blendRoot); AddChild(decorRoot); backdrop = new ColorRect { Color = new Color(.35f, .55f, .26f), MouseFilter = Control.MouseFilterEnum.Ignore, ZIndex = -1 }; groundRoot.AddChild(backdrop); AddChild(foliageRoot); AddChild(objRoot); AddChild(treeRoot); breakArea = new BreakAreaLayer { G = this }; AddChild(breakArea); InitVegetation();
    }

    Chunk ChunkAt(int x, int y)
    {
        if (!World.In(x, y)) return null;
        return chunks[(y / CS) * ChunksX + x / CS];
    }

    void LoadChunk(int cx, int cy)
    {
        int ci = cy * ChunksX + cx; if (chunks[ci] != null) return;
        var sw = Stopwatch.StartNew();
        var c = new Chunk
        {
            G = new TileMapLayer { TileSet = chunkSet, Position = new Vector2(cx * CS * TS, cy * CS * TS), Material = waterMat },
            X1 = new TileMapLayer { TileSet = blendSet, Position = new Vector2(cx * CS * TS, cy * CS * TS) },     // переходы между поверхностями (визуальные): слабый материал снизу
            X2 = new TileMapLayer { TileSet = blendSet, Position = new Vector2(cx * CS * TS, cy * CS * TS) },     // …и сильный поверх
            D = new TileMapLayer { TileSet = blendSet, Position = new Vector2(cx * CS * TS, cy * CS * TS) },      // декор: травинки, камешки, пятна
            F = new TileMapLayer { TileSet = chunkSet, Position = new Vector2(cx * CS * TS, cy * CS * TS), Material = swayMat },
            O = new TileMapLayer { TileSet = chunkSet, Position = new Vector2(cx * CS * TS, cy * CS * TS), Material = swayMat },
            R = new TileMapLayer { TileSet = chunkSet, Position = new Vector2(cx * CS * TS, cy * CS * TS), Material = swayMat, YSortEnabled = true },     // деревья целиком под игроком (игрок спереди дерева); нижнее дерево рисуется поверх верхнего
            T = new TileMapLayer { TileSet = chunkSet, Position = new Vector2(cx * CS * TS, cy * CS * TS), Material = swayMat, YSortEnabled = true, Modulate = new Color(1, 1, 1, TreeFadeAlpha) }     // деревья целиком выше игрока, когда он стоит за ними: всё дерево полупрозрачно, чтобы видеть игрока/постройку
        };
        FillFoliage(c.F, cx, cy); FillBlend(c, cx, cy);
        for (int y = 0; y < CS; y++)
            for (int x = 0; x < CS; x++)
            {
                int wx = cx * CS + x, wy = cy * CS + y, k = wy * World.Wd + wx;
                c.G.SetCell(new Vector2I(x, y), srcId, new Vector2I(W.Ground[k], 0));
                var t = (Tl)W.Obj[k];
                if (IsPlantTile(t)) c.R.SetCell(new Vector2I(x, y), srcId, new Vector2I(TreeAtlas(t, wx, wy), 0), TreeAlt(wx, wy));
                else if (t != Tl.None && t != Tl.SecretCache && !HiddenCliff(wx, wy)) c.O.SetCell(new Vector2I(x, y), srcId, new Vector2I(AtlasX(t, LevelAt(wx, wy)), 0));
            }
        groundRoot.AddChild(c.G); blendRoot.AddChild(c.X1); blendRoot.AddChild(c.X2); decorRoot.AddChild(c.D); foliageRoot.AddChild(c.F); objRoot.AddChild(c.O); treeRoot.AddChild(c.R); topRoot.AddChild(c.T); chunks[ci] = c; LoadedChunks++;
        LastChunkMs = (float)sw.Elapsed.TotalMilliseconds;
    }

    void UnloadChunk(int ci)
    {
        var c = chunks[ci]; if (c == null) return;
        c.G.QueueFree(); c.X1.QueueFree(); c.X2.QueueFree(); c.D.QueueFree(); c.F.QueueFree(); c.O.QueueFree(); c.R.QueueFree(); c.T.QueueFree(); chunks[ci] = null; LoadedChunks--;
    }

    /// <summary>Загружает/выгружает чанки вокруг камеры. force — загрузить всё нужное сразу (старт, телепорт).</summary>
    void UpdateChunks(float dt, bool force = false)
    {
        chunkT -= dt; if (chunkT > 0 && !force) return; chunkT = .15f;
        var ct = ToTile(cam != null ? CamCenter : PPos); int cx = Math.Clamp(ct.X / CS, 0, ChunksX - 1), cy = Math.Clamp(ct.Y / CS, 0, ChunksY - 1);
        var pt = ToTile(PPos); int px = Math.Clamp(pt.X / CS, 0, ChunksX - 1), py = Math.Clamp(pt.Y / CS, 0, ChunksY - 1);
        int budget = force ? 99 : 2;
        // чанк игрока и соседние — приоритетно и без бюджета, если их нет
        for (int dy = -LoadR; dy <= LoadR; dy++)
            for (int dx = -LoadR; dx <= LoadR; dx++)
            {
                foreach (var (bx, by) in new[] { (px, py), (cx, cy) })
                {
                    int x = bx + dx, y = by + dy; if (x < 0 || y < 0 || x >= ChunksX || y >= ChunksY) continue;
                    if (chunks[y * ChunksX + x] != null) continue;
                    bool urgent = Math.Abs(dx) + Math.Abs(dy) == 0 || (x == px && y == py);
                    if (urgent || budget > 0) { LoadChunk(x, y); budget--; }
                }
            }
        for (int i = 0; i < chunks.Length; i++)
        {
            if (chunks[i] == null) continue;
            int x = i % ChunksX, y = i / ChunksX;
            if ((Math.Abs(x - cx) > KeepR || Math.Abs(y - cy) > KeepR) && (Math.Abs(x - px) > KeepR || Math.Abs(y - py) > KeepR)) UnloadChunk(i);
        }
    }
}
