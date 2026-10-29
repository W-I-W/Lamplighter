using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Возобновление ресурсов: природные залежи, деревья и камни медленно восстанавливаются вдали от построек — без рутинной «охоты» за последними жилами.</summary>
public partial class Game
{
    float regrowT;

    static float RegrowChance(Tl t) => t switch
    {
        Tl.Tree or Tl.DeadTree or Tl.Pine => .5f,
        Tl.Stone or Tl.Rubble => .4f,
        Tl.Copper or Tl.Iron or Tl.SilverOre or Tl.FrostOre => .25f,
        Tl.CrystalOre or Tl.Obsidian or Tl.Ember => .12f,
        Tl.Aether => .06f,
        _ => 0f
    };

    bool NearSettlement(Vector2 p)
    {
        foreach (var b in Buildings) if (b.Pos.DistanceTo(p) < 6 * TS) return true;
        foreach (var l in Lamps) if (l.Pos.DistanceTo(p) < 3 * TS) return true;
        return W.Ancients[0].Pos.DistanceTo(p) < 7 * TS || TileCenter(W.Spawn.X, W.Spawn.Y).DistanceTo(p) < 5 * TS;
    }

    void UpdateRegrow(float dt)
    {
        if (NetClient) return;                                          // восстановление и появление ресурсов ведёт хост, клетки приходят по сети
        regrowT -= dt; if (regrowT > 0 || W.GenObj == null) return; regrowT = 1.5f;
        var pt = ToTile(PPos);
        for (int i = 0; i < 30; i++)
        {
            int x = pt.X + rng.RandiRange(-48, 48), y = pt.Y + rng.RandiRange(-32, 32);
            if (!World.In(x, y)) continue; int k = World.Key(x, y);
            if (W.Obj[k] != (byte)Tl.None) continue;
            var gen = (Tl)W.GenObj[k]; float ch = RegrowChance(gen) * Tune.T.RegrowMul; if (ch <= 0 || rng.Randf() > ch) continue;
            var c = TileCenter(x, y); if (c.DistanceTo(PPos) < 14 * TS || W.Ground[k] == Art.WaterG || NearSettlement(c)) continue;
            bool occ = false; foreach (var e in Enemies) if (e.Pos.DistanceTo(c) < 10) { occ = true; break; }
            if (occ) continue;
            SetObj(x, y, gen);
        }
    }

    // ───────────────────────── нехватка ресурсов ─────────────────────────
    /// <summary>Минимум природных ресурсов рядом с игроком (в окне 100×72 клеток своей области). Если игроки вырубили и выломали слишком много, новые деревья, камни и руды появляются
    /// сами — вне света и построек, не ближе 12 клеток к игрокам и не в узких проходах, — чтобы любой нужный для крафта ресурс всегда можно было найти. Ведёт хост, клиенты получают клетки по сети.</summary>
    static readonly (Tl t, int min, int gate)[][] ScarceTable =
    {
        new[] { (Tl.Tree, 70, 0), (Tl.Stone, 28, 0), (Tl.Copper, 12, 0), (Tl.Iron, 7, 1), (Tl.Lumen, 3, 2) },                                       // тёмный лес
        new[] { (Tl.Stone, 28, 0), (Tl.Copper, 8, 0), (Tl.Iron, 9, 0), (Tl.SilverOre, 5, 0), (Tl.CrystalOre, 4, 0), (Tl.Lumen, 3, 0) },               // шахты
        new[] { (Tl.Stone, 22, 0), (Tl.Iron, 6, 0), (Tl.Obsidian, 6, 0), (Tl.Ember, 6, 0) },                                                       // пустоши
        new[] { (Tl.Stone, 18, 0), (Tl.SilverOre, 5, 0), (Tl.FrostOre, 6, 0), (Tl.Iron, 5, 0) },                                                   // монастырь
    };
    float scarceT; int scarceIdx;

    static int RegionLo(int reg) => reg == 0 ? 0 : reg == 1 ? World.MX0 : reg == 2 ? World.AX0 : World.IX0;
    static int RegionHi(int reg) => reg == 0 ? World.MX0 - 1 : reg == 1 ? World.MX1 : reg == 2 ? World.AX1 : World.IX1;

    void UpdateScarcity(float dt)
    {
        if (NetClient || W.GenObj == null || RiftOn || TestNoSpawn) return;
        scarceT -= dt; if (scarceT > 0) return; scarceT = 8f;
        var targets = new List<Vector2> { PPos }; if (NetHost) foreach (var r in Remotes()) if (r.Hp > 0) targets.Add(r.Pos);
        var tp = targets[scarceIdx++ % targets.Count]; int reg = RegionAt(tp); if (reg < 0 || reg > 3) return;
        var pt = ToTile(tp); int lo = Math.Max(RegionLo(reg), pt.X - 50), hi = Math.Min(RegionHi(reg), pt.X + 50), y0 = Math.Max(2, pt.Y - 36), y1 = Math.Min(World.Ht - 3, pt.Y + 36);
        var count = new Dictionary<Tl, int>();
        for (int y = y0; y <= y1; y++) for (int x = lo; x <= hi; x++) { var o = (Tl)W.Obj[World.Key(x, y)]; if (o == Tl.Pine || o == Tl.DeadTree) o = Tl.Tree; count[o] = count.GetValueOrDefault(o) + 1; }
        foreach (var (t, min, gate) in ScarceTable[reg])
        {
            if (gate == 1 && LitCount < 1 || gate == 2 && LitCount < 2) continue;
            int need = (int)(min * Tune.T.ScarcityMul) - count.GetValueOrDefault(t); if (need <= 0) continue;
            for (int n = 0, made = 0; n < 60 && made < Math.Min(3, need); n++)
            {
                int x = rng.RandiRange(lo, hi), y = rng.RandiRange(y0, y1); int k = World.Key(x, y);
                if (W.Obj[k] != (byte)Tl.None || W.Ground[k] == Art.WaterG || W.Ground[k] == Art.HotAsh || W.Ground[k] == Art.VoidG) continue;
                var c = TileCenter(x, y); bool far = true; foreach (var q in targets) if (q.DistanceTo(c) < 12 * TS) { far = false; break; }
                if (!far || NearSettlement(c) || IsLit(c)) continue;
                bool open = true; for (int dy = -1; dy <= 1 && open; dy++) for (int dx = -1; dx <= 1; dx++) if (!W.Walkable(x + dx, y + dy)) { open = false; break; }          // не в узком проходе
                if (!open) continue;
                bool near = false; foreach (var p in W.Pois) if (Math.Abs(p.Tile.X - x) < 5 && Math.Abs(p.Tile.Y - y) < 5) { near = true; break; }
                foreach (var a in W.Ancients) if (Math.Abs(a.Tile.X - x) < 8 && Math.Abs(a.Tile.Y - y) < 8) { near = true; break; }
                if (near) continue;
                SetObj(x, y, t); made++;
            }
        }
    }
}
