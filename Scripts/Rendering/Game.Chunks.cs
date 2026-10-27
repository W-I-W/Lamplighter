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
    sealed class Chunk { public TileMapLayer G, F, O; }
    readonly Chunk[] chunks = new Chunk[ChunksX * ChunksY];
    Node2D groundRoot, objRoot; TileSet chunkSet;
    public int LoadedChunks; public float LastChunkMs;
    float chunkT;
    const int LoadR = 1, KeepR = 2;

    void InitChunks()
    {
        chunkSet = new TileSet { TileSize = new Vector2I(TS, TS) };
        var src = new TileSetAtlasSource { Texture = Art.TileTex, TextureRegionSize = new Vector2I(TS, TS), UseTexturePadding = false };   // без внутренних полей: шейдеры растительности читают атлас по колонкам 16 px
        for (int i = 0; i < Art.TileCount; i++) src.CreateTile(new Vector2I(i, 0));
        srcId = chunkSet.AddSource(src);
        groundRoot = new Node2D(); foliageRoot = new Node2D(); objRoot = new Node2D();
        AddChild(groundRoot); AddChild(foliageRoot); AddChild(objRoot); InitVegetation();
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
            F = new TileMapLayer { TileSet = chunkSet, Position = new Vector2(cx * CS * TS, cy * CS * TS), Material = swayMat },
            O = new TileMapLayer { TileSet = chunkSet, Position = new Vector2(cx * CS * TS, cy * CS * TS), Material = swayMat }
        };
        FillFoliage(c.F, cx, cy);
        for (int y = 0; y < CS; y++)
            for (int x = 0; x < CS; x++)
            {
                int wx = cx * CS + x, wy = cy * CS + y, k = wy * World.Wd + wx;
                c.G.SetCell(new Vector2I(x, y), srcId, new Vector2I(W.Ground[k], 0));
                var t = (Tl)W.Obj[k];
                if (t != Tl.None && t != Tl.SecretCache) c.O.SetCell(new Vector2I(x, y), srcId, new Vector2I(AtlasX(t, LevelAt(wx, wy)), 0));
            }
        groundRoot.AddChild(c.G); foliageRoot.AddChild(c.F); objRoot.AddChild(c.O); chunks[ci] = c; LoadedChunks++;
        LastChunkMs = (float)sw.Elapsed.TotalMilliseconds;
    }

    void UnloadChunk(int ci)
    {
        var c = chunks[ci]; if (c == null) return;
        c.G.QueueFree(); c.F.QueueFree(); c.O.QueueFree(); chunks[ci] = null; LoadedChunks--;
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
