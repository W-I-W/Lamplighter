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
        regrowT -= dt; if (regrowT > 0 || W.GenObj == null) return; regrowT = 1.5f;
        var pt = ToTile(PPos);
        for (int i = 0; i < 30; i++)
        {
            int x = pt.X + rng.RandiRange(-48, 48), y = pt.Y + rng.RandiRange(-32, 32);
            if (!World.In(x, y)) continue; int k = World.Key(x, y);
            if (W.Obj[k] != (byte)Tl.None) continue;
            var gen = (Tl)W.GenObj[k]; float ch = RegrowChance(gen); if (ch <= 0 || rng.Randf() > ch) continue;
            var c = TileCenter(x, y); if (c.DistanceTo(PPos) < 14 * TS || W.Ground[k] == Art.WaterG || NearSettlement(c)) continue;
            bool occ = false; foreach (var e in Enemies) if (e.Pos.DistanceTo(c) < 10) { occ = true; break; }
            if (occ) continue;
            SetObj(x, y, gen);
        }
    }
}
