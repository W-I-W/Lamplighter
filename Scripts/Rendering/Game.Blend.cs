using Godot;

namespace Lamplighter;

/// <summary>Визуальное смешивание поверхностей поверх существующих чанков: два слоя переходов (X1 — слабее, X2 — сильнее) и слой декора (D) в каждом чанке.
/// Данные мира (Ground/Obj), коллизии и логика не затрагиваются; тайлы выбирает TerrainBlend по Seed и мировым координатам (с чтением соседних клеток за границей чанка).
/// Настройки — Resources/VisualConfigs/TerrainBlend.tres; графика переходов рисуется кодом (TerrainBlendArt) из PNG тайлов земли.</summary>
public partial class Game
{
    TileSet blendSet; readonly int[] blendSrc = new int[TerrainBlend.Sources]; int decorSrc = -1; Node2D blendRoot, decorRoot; bool blendOk;
    TerrainBlend.Params blendP;

    TerrainBlendArt blendArt;

    /// <summary>Листы переходов и декора рисуются кодом из тайлов земли (Art.TileTex): перерисованные PNG земли сразу дают новые переходы.</summary>
    void InitBlend()
    {
        blendSet = new TileSet { TileSize = new Vector2I(TS, TS) };
        blendOk = Art.TileTex != null;
        if (!blendOk) { GD.PushError("Нет атласа тайлов: переходы поверхностей отключены"); return; }
        blendArt = new TerrainBlendArt(Art.TileTex.GetImage());
        TileSetAtlasSource Atlas(Texture2D t) => new TileSetAtlasSource { Texture = t, TextureRegionSize = new Vector2I(TS, TS), UseTexturePadding = false };
        for (int m = 0; m < TerrainBlend.Sources; m++) blendSrc[m] = blendSet.AddSource(Atlas(blendArt.Sheet(m)));
        decorSrc = blendSet.AddSource(Atlas(blendArt.DecorSheet));
        ReloadBlendConfig();
    }

    /// <summary>Перечитать TerrainBlend.tres (после смены ресурса уже загруженные чанки нужно пересобрать — RebuildBlend).</summary>
    void ReloadBlendConfig() => blendP = TerrainBlend.From(TerrainBlendConfig.Get(), W?.Seed ?? 0);

    void PutBlend(TileMapLayer layer, int x, int y, TerrainBlend.Layer l)
    {
        blendArt.Ensure(l.Mat, l.Col, l.Row);
        var src = (TileSetAtlasSource)blendSet.GetSource(blendSrc[l.Mat]); var at = new Vector2I(l.Col, l.Row);
        if (!src.HasTile(at)) src.CreateTile(at);
        layer.SetCell(new Vector2I(x, y), blendSrc[l.Mat], at);
    }

    void FillBlend(Chunk c, int cx, int cy)
    {
        if (!blendOk) return;
        blendP.Seed = W.Seed + TerrainBlendConfig.Get().SeedSalt;
        var decor = (TileSetAtlasSource)blendSet.GetSource(decorSrc);
        for (int y = 0; y < CS; y++)
            for (int x = 0; x < CS; x++)
            {
                int wx = cx * CS + x, wy = cy * CS + y, k = wy * World.Wd + wx;
                var r = TerrainBlend.Cell(W.Ground, wx, wy, blendP);
                if (r.Under.Any) PutBlend(c.X1, x, y, r.Under);
                if (r.Over.Any) PutBlend(c.X2, x, y, r.Over);
                if (W.Obj[k] != (byte)Tl.None || c.F.GetCellSourceId(new Vector2I(x, y)) >= 0) continue;   // декор — только на свободной земле без кустиков
                var d = TerrainBlend.Decor(W.Ground, wx, wy, blendP);
                if (d.X < 0) continue;
                blendArt.EnsureDecor(d.X, d.Y);
                if (!decor.HasTile(d)) decor.CreateTile(d);
                c.D.SetCell(new Vector2I(x, y), decorSrc, d);
            }
        blendArt.Flush();
    }

    /// <summary>Пересобрать слои переходов во всех загруженных чанках (после смены настроек).</summary>
    public void RebuildBlend(bool rereadTiles = false)
    {
        ReloadBlendConfig();
        if (rereadTiles && blendOk) blendArt.Reset(Art.TileTex.GetImage());
        for (int i = 0; i < chunks.Length; i++)
        {
            var c = chunks[i]; if (c == null) continue;
            c.X1.Clear(); c.X2.Clear(); c.D.Clear(); FillBlend(c, i % ChunksX, i / ChunksX);
        }
    }
}
