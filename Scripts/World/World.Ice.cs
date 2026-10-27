using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

public sealed partial class World
{
    public List<Vector2I> Seal3 = new(), Seal4 = new();
    public Vector2I IceArena = new(-1, -1); public int IceGateY = 64;
    public List<Vector2I> ArenaPrisms = new();
    public List<Vector2I> Vents2 = new();

    public Poi AddPoiPublic(PK k, Vector2I t, int guards, byte variant) => AddPoi(k, t, guards, variant);

    void GenIce(int seed)
    {
        var r = new Random(seed * 89 + 313);
        var nTrees = new FastNoiseLite { Seed = seed + 59, Frequency = .055f, NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex };
        var nLake = new FastNoiseLite { Seed = seed + 61, Frequency = .045f, NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex };
        var nEdge = new FastNoiseLite { Seed = seed + 67, Frequency = .22f };
        for (int y = 0; y < Ht; y++)
            for (int x = IX0; x <= IX1; x++)
            {
                int i = Key(x, y);
                Ground[i] = (byte)(nLake.GetNoise2D(x, y) > .22f ? Art.IceFloor + r.Next(2) : Art.Snow + r.Next(3));
                bool border = y < 2 + (int)((nEdge.GetNoise2D(x, y) + 1) * 1.3f) || y >= Ht - 2 - (int)((nEdge.GetNoise2D(x + 99, y) + 1) * 1.3f) || x >= IX1 - 1 - (int)((nEdge.GetNoise2D(x, y + 77) + 1) * 1.3f) || x < IX0 + 4;
                if (border) { Obj[i] = (byte)Tl.Cliff; continue; }
                double q = r.NextDouble();
                if (nTrees.GetNoise2D(x, y) > .18f && q < .10) Obj[i] = (byte)Tl.Pine;
                else if (q < .004) Obj[i] = (byte)Tl.Stone;
                else Obj[i] = (byte)Tl.None;
            }
        for (int k = 0; k < 10; k++) Cluster(r, Tl.IceRuin, r.Next(IX0 + 10, IX1 - 6), r.Next(8, Ht - 8), r.Next(4, 9), 2);
        // проход из пустошей (запечатан): высота выбирается так, чтобы не задеть уже размещённые места
        foreach (var cand in new[] { 64, 48, 80, 32, 96, 40, 88, 24, 104 })
        {
            bool free = true;
            foreach (var p in Pois) if (p.Tile.X >= 350 && p.Tile.X <= AX1 && Math.Abs(p.Tile.Y - cand) <= 6) free = false;
            foreach (var a in Ancients) if (a.Tile.X >= 350 && a.Tile.X <= AX1 && Math.Abs(a.Tile.Y - cand) <= 8) free = false;
            if (free) { IceGateY = cand; break; }
        }
        int gy = IceGateY;
        for (int y = gy - 3; y <= gy + 3; y++) for (int x = 358; x <= AX1; x++) { Set(x, y, Tl.None); Ground[Key(x, y)] = (byte)Art.AshFloor; }
        for (int y = gy - 3; y <= gy + 3; y++) { Set(366, y, Tl.Seal); Set(367, y, Tl.Seal); Seal3.Add(new Vector2I(366, y)); Seal3.Add(new Vector2I(367, y)); }
        for (int y = gy - 4; y <= gy + 4; y++) for (int x = IX0; x <= IX0 + 18; x++) { Set(x, y, Tl.None); Ground[Key(x, y)] = (byte)Art.Snow; }
        for (int y = 61; y <= 67; y++) for (int x = 484; x <= IX1; x++) { Set(x, y, Tl.None); Ground[Key(x, y)] = (byte)Art.Snow; }
        for (int y = 61; y <= 67; y++) { Set(494, y, Tl.Seal); Set(495, y, Tl.Seal); Seal4.Add(new Vector2I(494, y)); Seal4.Add(new Vector2I(495, y)); }
        var ent = new Vector2I(IX0 + 8, gy);
        var kit = new RegionKit(this, r, IX0, IX1, ent);
        kit.Taken.Add((ent, 14)); kit.Taken.Add((new Vector2I(490, 64), 10));
        MakeReachable(new Vector2I(AX0 + 8, 64), new Vector2I(357, gy));
        var required = kit.Required;

        float[][] bands = { new[] { .25f, .40f }, new[] { .50f, .65f }, new[] { .72f, .86f } };
        for (int k = 0; k < 3; k++)
        {
            var t = kit.Pick(bands[k][0], bands[k][1], 3, 14); if (t.X < 0) continue;
            kit.Floor(t.X, t.Y, 4, Art.Snow); Set(t.X, t.Y, Tl.AncOff);
            var an = new Ancient { Tile = t, RadiusTiles = 12, Cost = 12 + k, Region = 3 };
            Ancients.Add(an); required.Add(t + new Vector2I(0, 2));
            kit.Veins(an, Tl.FrostOre, 5, Art.IceFloor); kit.Veins(an, Tl.SilverOre, 4, Art.IceFloor);
        }
        // арена Хранителя Зимы: ледяное озеро с четырьмя призмами
        {
            var t = kit.Pick(.9f, 1f, 4, 16); if (t.X < 0) t = kit.Pick(.6f, 1f, 3, 10);
            kit.Floor(t.X, t.Y, 10, Art.IceFloor, 6);
            Set(t.X, t.Y, Tl.BossOff);
            foreach (var o in new[] { new Vector2I(-6, 0), new Vector2I(6, 0), new Vector2I(0, -6), new Vector2I(0, 6) }) { var p = t + o; Set(p.X, p.Y, Tl.Receiver); ArenaPrisms.Add(p); }
            var an = new Ancient { Tile = t, RadiusTiles = 14, Cost = 0, Boss = true, BossId = 4, Region = 3 };
            Ancients.Add(an); required.Add(t + new Vector2I(0, 3)); kit.Taken.Add((t, 12)); IceArena = t;
            kit.Veins(an, Tl.FrostOre, 4, Art.IceFloor);
        }
        // природные кристаллы холода
        for (int k = 0; k < 12; k++) { var t = kit.Pick(.2f, 1f, 1, 4); if (t.X >= 0) { Set(t.X, t.Y, Tl.FrostOre); foreach (var d in D4) if (r.Next(2) == 0 && Get(t.X + d.X, t.Y + d.Y) == Tl.None) Set(t.X + d.X, t.Y + d.Y, Tl.FrostOre); } }
        for (int i = 0; i < 5; i++) { var t = kit.Pick(.1f, 1f, 1, 8); if (t.X < 0) continue; Set(t.X, t.Y, Tl.Chest); AddPoi(PK.Chest, t); required.Add(t + new Vector2I(0, 1)); }
        for (int i = 0; i < 3; i++)
        {
            var t = kit.Pick(.3f, .9f, 4, 10); if (t.X < 0) continue;
            kit.Floor(t.X, t.Y, 4, Art.Snow);
            Set(t.X, t.Y, Tl.Chest); Set(t.X - 3, t.Y - 2, Tl.Tent); Set(t.X + 3, t.Y - 2, Tl.Tent); Set(t.X, t.Y + 3, Tl.Tent);
            AddPoi(PK.Camp, t, 4 + i); required.Add(t + new Vector2I(0, 2));
        }
        { var t = kit.Pick(.2f, .8f, 2, 10); if (t.X >= 0) { Set(t.X, t.Y, Tl.Shrine); AddPoi(PK.Shrine, t); required.Add(t + new Vector2I(0, 1)); } }
        for (int i = 0; i < 2; i++) { var t = kit.Pick(.15f, .9f, 2, 10); if (t.X >= 0) { Set(t.X, t.Y, Tl.Obelisk); AddPoi(PK.Obelisk, t); required.Add(t + new Vector2I(0, 1)); } }
        taken.AddRange(kit.Taken);
        for (int i = 0; i < 3; i++) Stamp(r, Flip(r, i % 2 == 0 ? PuzzleA() : PuzzleB()), .2f, kit.Dist, kit.DMax, required, PK.Puzzle, IX0, IX1 - 2, Art.Snow, Tl.IceRuin);
        for (int i = 0; i < 2; i++) Stamp(r, Workshop(), .2f, kit.Dist, kit.DMax, required, PK.Workshop, IX0, IX1 - 2, Art.Snow, Tl.IceRuin);
        for (int i = 0; i < 2; i++) Stamp(r, Stash(), .15f, kit.Dist, kit.DMax, required, PK.Chest, IX0, IX1 - 2, Art.Snow, Tl.IceRuin);
        required.Add(new Vector2I(483, 64));
        foreach (var t in required) MakeReachable(ent, t);
    }
}
