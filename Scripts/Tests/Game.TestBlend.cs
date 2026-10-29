using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Проверка смешивания поверхностей (LAMP_STAGE=blend): PNG-листы без полупрозрачности, детерминизм по Seed и координатам, независимость от порядка загрузки чанков,
/// отсутствие влияния на мир, работа настроек из Resource + скриншоты переходов.</summary>
public partial class Game
{
    void BuildBlendScript()
    {
        int fails = 0;
        void Check(bool ok, string what) { if (!ok) fails++; GD.Print($"[check] {(ok ? "PASS" : "FAIL")} {what}"); }
        ulong Snap(Chunk c)
        {
            ulong h = 1469598103934665603UL;
            foreach (var l in new[] { c.X1, c.X2, c.D })
                for (int y = 0; y < CS; y++)
                    for (int x = 0; x < CS; x++)
                    {
                        var v = new Vector2I(x, y); int s = l.GetCellSourceId(v); var a = l.GetCellAtlasCoords(v);
                        h = (h ^ (ulong)(s + 2)) * 1099511628211UL; h = (h ^ (ulong)(a.X + 2)) * 1099511628211UL; h = (h ^ (ulong)(a.Y + 2)) * 1099511628211UL;
                    }
            return h;
        }
        ulong WorldHash() { ulong h = 14695981039346656037UL; for (int i = 0; i < W.Ground.Length; i++) { h = (h ^ W.Ground[i]) * 1099511628211UL; h = (h ^ W.Obj[i]) * 1099511628211UL; } return h; }
        int CountBlend(Chunk c) { int n = 0; foreach (var l in new[] { c.X1, c.X2 }) n += l.GetUsedCells().Count; return n; }
        Vector2I FindEdge(int x0, int x1, int fa, int fb)
        {
            for (int y = 8; y < World.Ht - 8; y++)
                for (int x = x0; x < x1; x++)
                    if (TerrainBlend.FamilyOfGround(W.Ground[World.Key(x, y)]) == fa && TerrainBlend.FamilyOfGround(W.Ground[World.Key(x + 1, y)]) == fb && W.Get(x, y) == Tl.None) return new Vector2I(x, y);
            return new Vector2I(-1, -1);
        }
        ulong worldBefore = 0; Vector2I pc = default; ulong snapBefore = 0; var cfgOrig = TerrainBlendConfig.Get();

        S(.4f, () =>
        {
            Menu = Mn.None; noHeal = true; Hp = MaxHp; Invuln = 99f; Enemies.Clear();
            worldBefore = WorldHash();
            Check(cfgOrig != null && cfgOrig.Enabled && cfgOrig.BlendWidth >= 0, "blend: TerrainBlend.tres loaded");
            Check(TerrainBlend.MaskCount == 47, $"blend: 47 canonical 8-neighbour masks (got {TerrainBlend.MaskCount})");
            bool alphaOk = true, drawn = false;
            for (int m = 0; m < TerrainBlend.Sources; m++) for (int row = 0; row < 3; row++) for (int col = 0; col < TerrainBlend.Cols; col++) blendArt.Ensure(m, col, row * 7);
            for (int m = 0; m < TerrainBlend.Sources; m++)
            {
                var im = blendArt.SheetImage(m);
                for (int y = 0; y < im.GetHeight() && alphaOk; y++) for (int x = 0; x < im.GetWidth(); x++) { float al = im.GetPixel(x, y).A; if (al != 0f && al != 1f) { alphaOk = false; break; } if (al == 1f) drawn = true; }
            }
            for (int r = 0; r < TerrainBlend.Sources; r++) for (int c = 0; c < TerrainBlend.DecorCols; c++) blendArt.EnsureDecor(c, r);
            var dec = blendArt.DecorImage;
            for (int y = 0; y < dec.GetHeight() && alphaOk; y++) for (int x = 0; x < dec.GetWidth(); x++) { float al = dec.GetPixel(x, y).A; if (al != 0f && al != 1f) { alphaOk = false; break; } }
            Check(drawn, "blend: transition sheets are drawn by code from the ground tiles (no PNG files needed)");
            Check(alphaOk, "blend: every pixel is fully opaque or fully transparent (no blur, no semi-transparency)");
            // перерисованные тайлы земли должны сразу менять переходы: красим траву и смотрим на лист
            var recol = Art.TileTex.GetImage(); for (int t = 0; t < 4; t++) for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) recol.SetPixel(t * 16 + x, y, new Color(1, 0, 0));
            var art2 = new TerrainBlendArt(recol); art2.Ensure(0, TerrainBlend.MaskColumn(1), 14); var im2 = art2.SheetImage(0); bool red = false;
            for (int y = 14 * 16; y < 15 * 16 && !red; y++) for (int x = 0; x < 16; x++) if (im2.GetPixel(TerrainBlend.MaskColumn(1) * 16 + x, y).A == 1f) { red = im2.GetPixel(TerrainBlend.MaskColumn(1) * 16 + x, y).IsEqualApprox(new Color(1, 0, 0)); break; }
            Check(red, "blend: redrawing a ground tile PNG changes the generated transitions (code-driven)");
            Check(blendOk, "blend: tile sets built from PNG");
            var ts = ProjectSettings.GetSetting("rendering/textures/canvas_textures/default_texture_filter").AsInt32();
            Check(ts == 0, "blend: project default texture filter is Nearest");
            Check(blendSet.TileSize == new Vector2I(16, 16), "blend: tile size 16x16");
            Tp(TileCenter(W.Spawn.X, W.Spawn.Y)); UpdateChunks(0, true);
        });
        S(.5f, () =>
        {
            pc = new Vector2I(W.Spawn.X / CS, W.Spawn.Y / CS);
            var c = chunks[pc.Y * ChunksX + pc.X]; Check(c != null, "blend: player chunk loaded");
            Check(c.X1.TileSet == blendSet && c.D.TileSet == blendSet && c.G.TileSet == chunkSet, "blend: separate layers on their own tile set; ground layer untouched");
            int total = 0, decor = 0; for (int i = 0; i < chunks.Length; i++) if (chunks[i] != null) { total += CountBlend(chunks[i]); decor += chunks[i].D.GetUsedCells().Count; }
            Log($"blend cells={total} decor cells={decor} chunks={LoadedChunks}");
            Check(total > 0, "blend: transition tiles exist around the spawn (grass/dirt)");
            Check(decor > 0, "blend: decor layer is populated");
            // совпадение со слоями чанка на границах чанков
            int bad = 0, checkedN = 0;
            for (int i = 0; i < chunks.Length; i++)
            {
                var ch = chunks[i]; if (ch == null) continue; int cx = i % ChunksX, cy = i / ChunksX;
                for (int y = 0; y < CS; y++) for (int x = 0; x < CS; x++)
                {
                    bool border = x == 0 || y == 0 || x == CS - 1 || y == CS - 1; if (!border) continue;
                    var r = TerrainBlend.Cell(W.Ground, cx * CS + x, cy * CS + y, blendP); checkedN++;
                    bool u = ch.X1.GetCellSourceId(new Vector2I(x, y)) >= 0, o = ch.X2.GetCellSourceId(new Vector2I(x, y)) >= 0;
                    if (u != r.Under.Any || o != r.Over.Any) { bad++; continue; }
                    if (u) { var a = ch.X1.GetCellAtlasCoords(new Vector2I(x, y)); if (a.X != r.Under.Col || a.Y != r.Under.Row) bad++; }
                    if (o) { var a = ch.X2.GetCellAtlasCoords(new Vector2I(x, y)); if (a.X != r.Over.Col || a.Y != r.Over.Row) bad++; }
                }
            }
            Check(bad == 0, $"blend: chunk-border cells equal the pure world-coordinate result ({checkedN} cells, {bad} mismatches)");
            snapBefore = Snap(c);
            // порядок загрузки: выгрузить все чанки и загрузить в обратном порядке
            var hashes = new Dictionary<int, ulong>(); for (int i = 0; i < chunks.Length; i++) if (chunks[i] != null) hashes[i] = Snap(chunks[i]);
            var order = new List<int>(hashes.Keys); foreach (var i in order) UnloadChunk(i);
            for (int k = order.Count - 1; k >= 0; k--) LoadChunk(order[k] % ChunksX, order[k] / ChunksX);
            int diff = 0; foreach (var kv in hashes) if (chunks[kv.Key] == null || Snap(chunks[kv.Key]) != kv.Value) diff++;
            Check(diff == 0, $"blend: reloading chunks in reverse order gives identical layers ({hashes.Count} chunks, {diff} differ)");
            Check(WorldHash() == worldBefore, "blend: ground and objects (collision, resources) unchanged");
            int gm = 0; var cc = chunks[pc.Y * ChunksX + pc.X]; for (int y = 0; y < CS; y++) for (int x = 0; x < CS; x++) if (cc.G.GetCellAtlasCoords(new Vector2I(x, y)).X != W.Ground[(pc.Y * CS + y) * World.Wd + pc.X * CS + x]) gm++;
            Check(gm == 0, "blend: ground layer still shows the real ground tiles");
            // отдельная серия по сидам: Seed определяет результат
            var p1 = blendP; var p2 = blendP; p2.Seed += 9973; int same = 0, n2 = 0;
            for (int y = 40; y < 90; y++) for (int x = 40; x < 90; x++) { var a = TerrainBlend.Cell(W.Ground, x, y, p1); var b = TerrainBlend.Cell(W.Ground, x, y, p2); n2++; if (a.Under.Row == b.Under.Row && a.Under.Col == b.Under.Col && a.Over.Row == b.Over.Row) same++; }
            Check(same < n2, "blend: a different seed salt changes the noise layout");
        });
        // настройки из Resource
        S(.2f, () =>
        {
            int Count() { int n = 0; for (int i = 0; i < chunks.Length; i++) if (chunks[i] != null) n += CountBlend(chunks[i]) + chunks[i].D.GetUsedCells().Count; return n; }
            ulong All() { ulong h = 7; for (int i = 0; i < chunks.Length; i++) if (chunks[i] != null) h = h * 31 + Snap(chunks[i]); return h; }
            ulong baseHash = All();
            var cfg = new TerrainBlendConfig { Enabled = false }; TerrainBlendConfig.Override(cfg); RebuildBlend();
            Check(Count() == 0, "blend config: Enabled=false removes all blend and decor tiles");
            cfg = new TerrainBlendConfig { BlendWidth = 4f, WidthJitter = 0f, DitherIntensity = 1f, Roughness = 1f }; TerrainBlendConfig.Override(cfg); RebuildBlend(); ulong h1 = All();
            cfg = new TerrainBlendConfig { BlendWidth = 0f, WidthJitter = 0f, DitherIntensity = 0f, Roughness = 0f, ScatterDensity = 0f, DecorDensity = 0f }; TerrainBlendConfig.Override(cfg); RebuildBlend(); ulong h2 = All();
            Check(h1 != baseHash && h2 != baseHash && h1 != h2, "blend config: width / dither / roughness change the result");
            cfg = new TerrainBlendConfig { ScatterDensity = 0f, DecorDensity = 0f }; TerrainBlendConfig.Override(cfg); RebuildBlend();
            int deco = 0; for (int i = 0; i < chunks.Length; i++) if (chunks[i] != null) deco += chunks[i].D.GetUsedCells().Count; Check(deco == 0, "blend config: DecorDensity=0 leaves the decor layer empty");
            TerrainBlendConfig.Override(cfgOrig); RebuildBlend(); Check(All() == baseHash, "blend config: restoring the resource restores the identical result");
        });
        // скриншоты
        S(.2f, () => { var e = FindEdge(40, 90, 0, 1); if (e.X < 0) e = FindEdge(2, 120, 1, 0); Log($"grass/dirt edge at {e}"); Tp(TileCenter(e.X, e.Y)); });
        S(.9f, () => Shot("blend_1_forest"));
        S(.2f, () => { var e = FindEdge(0, 120, 1, 0); Tp(TileCenter(e.X + 6, e.Y + 4)); });
        S(.9f, () => Shot("blend_2_forest_b"));
        S(.2f, () => { var e = FindEdge(World.AX0, World.AX1, 3, 4); Log($"ash/scorch edge at {e}"); if (e.X < 0) e = FindEdge(World.AX0, World.AX1, 4, 3); Tp(TileCenter(e.X, e.Y)); });
        S(.9f, () => Shot("blend_3_ash"));
        S(.2f, () => { var e = FindEdge(World.IX0, World.IX1, 6, 5); if (e.X < 0) e = FindEdge(World.IX0, World.IX1, 5, 6); Log($"snow/ice edge at {e}"); Tp(TileCenter(e.X, e.Y)); });
        S(.9f, () => Shot("blend_4_ice"));
        S(.2f, () => { var e = FindEdge(World.MX0, World.MX1, 2, 8); if (e.X < 0) e = FindEdge(World.MX0, World.MX1, 8, 2); Log($"cave/water edge at {e}"); Tp(TileCenter(e.X, e.Y)); });
        S(.9f, () => Shot("blend_5_cave_water"));
        S(.2f, () => { var e = FindEdge(World.HX0, World.HX1, 7, 7); Tp(TileCenter(W.Spawn.X + 8, W.Spawn.Y)); });
        S(.9f, () => Shot("blend_6_spawn"));
        S(.3f, () => { GD.Print($"[check] SUMMARY failures={fails}"); GD.Print("BLEND DONE"); });
    }
}
