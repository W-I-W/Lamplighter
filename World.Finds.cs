using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

public sealed partial class World
{
    static readonly int[][] RegionX = { new[] { 8, 110 }, new[] { MX0 + 8, MX1 - 14 }, new[] { AX0 + 10, AX1 - 6 }, new[] { IX0 + 8, IX1 - 30 }, new[] { HX0 + 2, 538 } };
    Vector2I RegionStart(Vector2I spawn, int reg) => reg == 0 ? spawn : reg == 1 ? new Vector2I(MX0 + 8, 64) : reg == 2 ? new Vector2I(AX0 + 8, 64) : reg == 3 ? new Vector2I(IX0 + 8, IceGateY) : new Vector2I(HX0 + 8, 64);

    /// <summary>Редкие находки: фиксированные награды (технологии, чертежи, реликвии) + шпили, тайники и алтари лучей.</summary>
    void GenFinds(int seed, int rA, int rB)
    {
        var r = new Random(seed * 97 + 5 + (rA > 0 ? 7777 : 0));
        var reaches = new bool[5][]; for (int i = rA; i <= rB; i++) reaches[i] = Reach(RegionStart(Spawn, i));
        var used = new List<Vector2I>();

        List<Vector2I> Cands(int reg, int clear)
        {
            var l = new List<Vector2I>(); var st = RegionStart(Spawn, reg);
            for (int y = 8; y < Ht - 8; y++)
                for (int x = RegionX[reg][0]; x < RegionX[reg][1]; x++)
                {
                    if (!reaches[reg][Key(x, y)] || Ground[Key(x, y)] == Art.WaterG || Ground[Key(x, y)] == Art.HotAsh) continue;
                    if (reg == 0 && (x - Spawn.X) * (x - Spawn.X) + (y - 64) * (y - 64) < 144) continue;
                    if (reg > 0 && reg < 4 && Math.Abs(x - st.X) < 14 && Math.Abs(y - st.Y) < 10) continue;
                    bool ok = true;
                    for (int yy = y - clear; yy <= y + clear && ok; yy++) for (int xx = x - clear; xx <= x + clear; xx++) { var t = Get(xx, yy); if (t != Tl.None || Ground[Key(xx, yy)] == Art.WaterG || Ground[Key(xx, yy)] == Art.HotAsh) { ok = false; break; } }
                    if (ok) l.Add(new Vector2I(x, y));
                }
            return l;
        }
        bool Far(Vector2I t, int d)
        {
            foreach (var p in Pois) if ((p.Tile.X - t.X) * (p.Tile.X - t.X) + (p.Tile.Y - t.Y) * (p.Tile.Y - t.Y) < d * d) return false;
            foreach (var a in Ancients) if ((a.Tile.X - t.X) * (a.Tile.X - t.X) + (a.Tile.Y - t.Y) * (a.Tile.Y - t.Y) < 49) return false;
            foreach (var u in used) if ((u.X - t.X) * (u.X - t.X) + (u.Y - t.Y) * (u.Y - t.Y) < d * d) return false;
            return true;
        }
        Vector2I Pick(List<Vector2I> c, int d)
        {
            for (int i = 0; i < 60 && c.Count > 0; i++) { var t = c[r.Next(c.Count)]; if (Far(t, d)) { used.Add(t); return t; } }
            return new Vector2I(-1, -1);
        }
        Poi Mk(PK k, Vector2I t, Tl tile, string tag, byte variant = 0)
        {
            Set(t.X, t.Y, tile); var p = AddPoi(k, t, 0, variant); p.Tag = tag; return p;
        }

        string[][] tc =
        {
            new[] { "bp:bp_hub;frag:bp_refinery", "bp:bp_mod_multi;frag:bp_sword2" }, new[] { "bp:bp_mod_frost;frag:bp_amp", "bp:bp_barrier;frag:bp_mod_siphon" },
            new[] { "bp:bp_mod_ignite;frag:bp_staff3", "bp:bp_herolamp;frag:bp_mod_shock" }, new[] { "bp:bp_cryo;relic:r_glaciersoul", "bp:bp_amp;item:" + It.Frost + ":3" },
            new[] { "item:" + It.AncCrystal + ":3;item:" + It.Crystal + ":6", "" }
        };
        string[][] secret = { new[] { "relic:r_luckycoin" }, new[] { "relic:r_stormclaw", "frag:bp_bow3" }, new[] { "relic:r_shadowblood", "frag:bp_sword3" }, new[] { "frag:bp_cryo;item:" + It.Frost + ":4", "relic:r_frostshard" }, new[] { "relic:r_voidlens", "" } };
        string[][] rare = { new[] { "relic:r_embersp", "relic:r_minercharm" }, new[] { "relic:r_rimemirror", "frag:bp_staff2" }, new[] { "relic:r_ashcrown", "frag:bp_bow2" }, new[] { "frag:bp_cryo", "item:" + It.Frost + ":5;item:" + It.Silver + ":3" }, new[] { "item:" + It.AncCrystal + ":2", "item:" + It.Core + ":0" } };
        string[] altarTag = { "", "weapon:storm_u", "relic:r_firstlight;item:" + It.AncCrystal + ":2", "relic:r_glaciersoul;item:" + It.AncCrystal + ":2", "item:" + It.AncCrystal + ":4" };
        int[] spires = { 2, 3, 3, 3, 2 };

        for (int reg = rA; reg <= rB; reg++)
        {
            var cl = Cands(reg, 2);
            int ntc = reg == 4 ? 1 : 2;
            for (int i = 0; i < ntc; i++) { var t = Pick(cl, 9); if (t.X >= 0) Mk(PK.TechCache, t, Tl.TechCache, tc[reg][i]); }
            for (int i = 0; i < spires[reg]; i++) { var t = Pick(cl, 8); if (t.X >= 0) Mk(PK.Spire, t, Tl.Spire, null); }
            int ns = reg == 0 || reg == 4 ? 1 : 2;
            for (int i = 0; i < ns; i++) { var t = Pick(cl, 8); if (t.X >= 0) Mk(PK.Secret, t, Tl.SecretCache, secret[reg][i], 6); }
            for (int i = 0; i < 2; i++) { var t = Pick(cl, 8); if (t.X >= 0) { var tg = rare[reg][i]; if (tg.EndsWith(":0")) tg = null; Mk(PK.Chest, t, Tl.Chest, tg, 3); } }
            if (reg > 0)
            {
                var big = Cands(reg, 5); var a = Pick(big, 12);
                if (a.X >= 0)
                {
                    var alt = Mk(PK.Altar, a, Tl.Altar, null);
                    var pz = new Puzzle { Poi = alt };
                    foreach (var o in new[] { new Vector2I(-4, 0), new Vector2I(4, 0), new Vector2I(0, -4) }) { var rt = a + o; Set(rt.X, rt.Y, Tl.Receiver); pz.Receivers.Add(rt); }
                    Puzzles.Add(pz);
                    Mk(PK.Chest, a + new Vector2I(0, 2), Tl.Chest, altarTag[reg], 7);
                }
            }
        }
    }

    /// <summary>Таблички с записками (сюжет): по три в каждом регионе; добавляются в конец списка мест.</summary>
    void GenLore(int seed)
    {
        var r = new Random(seed * 131 + 9);
        int n = 0;
        for (int reg = 0; reg < 5; reg++)
        {
            var reach = reg < 4 ? Reach(RegionStart(Spawn, reg)) : null; var spots = new List<Vector2I>();
            for (int y = 8; y < Ht - 8; y++)
                for (int x = RegionX[reg][0]; x < (reg == 4 ? HX1 - 6 : RegionX[reg][1]); x++)
                {
                    if (reg < 4 && !reach[Key(x, y)]) continue;
                    if (Get(x, y) != Tl.None || Ground[Key(x, y)] == Art.WaterG || Ground[Key(x, y)] == Art.HotAsh || Ground[Key(x, y)] == Art.VoidG) continue;
                    if (Get(x + 1, y) != Tl.None || Get(x, y + 1) != Tl.None || Get(x - 1, y) != Tl.None || Get(x, y - 1) != Tl.None) continue;
                    if (reg == 0 && (x - Spawn.X) * (x - Spawn.X) + (y - 64) * (y - 64) < 100) continue;
                    spots.Add(new Vector2I(x, y));
                }
            var chosen = new List<Vector2I>();
            for (int i = 0; i < 3 && spots.Count > 0; i++)
                for (int tries = 0; tries < 80; tries++)
                {
                    var t = spots[r.Next(spots.Count)]; bool ok = true;
                    foreach (var p in Pois) if ((p.Tile.X - t.X) * (p.Tile.X - t.X) + (p.Tile.Y - t.Y) * (p.Tile.Y - t.Y) < 36) { ok = false; break; }
                    foreach (var c in chosen) if ((c.X - t.X) * (c.X - t.X) + (c.Y - t.Y) * (c.Y - t.Y) < 100) { ok = false; break; }
                    if (!ok) continue;
                    chosen.Add(t); Set(t.X, t.Y, Tl.Tablet); var po = AddPoi(PK.Lore, t, 0, 0); po.Tag = "lore:" + (reg * 3 + i); n++; break;
                }
        }
    }
}
