using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

public sealed partial class World
{
    public List<Vector2I> Vents = new();

    void GenAsh(int seed)
    {
        var r = new Random(seed * 71 + 211);
        var nScorch = new FastNoiseLite { Seed = seed + 43, Frequency = .035f, NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex };
        var nTrees = new FastNoiseLite { Seed = seed + 47, Frequency = .06f, NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex };
        var nEdge = new FastNoiseLite { Seed = seed + 53, Frequency = .22f };
        for (int y = 0; y < Ht; y++)
            for (int x = AX0; x <= AX1; x++)
            {
                int i = Key(x, y);
                Ground[i] = (byte)(nScorch.GetNoise2D(x, y) > .32f ? Art.Scorch : Art.AshFloor + r.Next(3));
                bool border = y < 2 + (int)((nEdge.GetNoise2D(x, y) + 1) * 1.3f) || y >= Ht - 2 - (int)((nEdge.GetNoise2D(x + 99, y) + 1) * 1.3f)
                              || x >= AX1 - 1 - (int)((nEdge.GetNoise2D(x, y + 77) + 1) * 1.3f) || (x < AX0 + 4);
                if (border) { Obj[i] = (byte)Tl.Cliff; continue; }
                double q = r.NextDouble();
                if (nTrees.GetNoise2D(x, y) > .2f && q < .10) Obj[i] = (byte)Tl.DeadTree;
                else if (q < .004) Obj[i] = (byte)Tl.Stone;
                else Obj[i] = (byte)Tl.None;
            }
        for (int k = 0; k < 12; k++) Cluster(r, Tl.AshRuin, r.Next(AX0 + 10, AX1 - 6), r.Next(8, Ht - 8), r.Next(4, 9), 2);
        // проход со стороны шахт
        for (int y = 60; y <= 68; y++) for (int x = AX0; x <= AX0 + 18; x++) { Set(x, y, Tl.None); Ground[Key(x, y)] = (byte)Art.AshFloor; }
        var ent = new Vector2I(AX0 + 8, 64);

        var dist = new int[Wd * Ht]; Array.Fill(dist, -1);
        int dmax = 0;
        void Bfs()
        {
            Array.Fill(dist, -1); dmax = 0;
            var q = new Queue<Vector2I>(); q.Enqueue(ent); dist[Key(ent.X, ent.Y)] = 0;
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                foreach (var d in D4)
                {
                    var n = c + d; if (n.X < AX0 || n.X > AX1 || !In(n.X, n.Y) || dist[Key(n.X, n.Y)] >= 0 || !Walkable(n.X, n.Y)) continue;
                    dist[Key(n.X, n.Y)] = dist[Key(c.X, c.Y)] + 1; dmax = Math.Max(dmax, dist[Key(n.X, n.Y)]); q.Enqueue(n);
                }
            }
        }
        Bfs();
        taken.Add((ent, 14));
        bool Clearance(int x, int y, int rad)
        {
            for (int yy = y - rad; yy <= y + rad; yy++) for (int xx = x - rad; xx <= x + rad; xx++) if (Get(xx, yy) != Tl.None) return false;
            return true;
        }
        Vector2I Pick(float f0, float f1, int clear, int sep)
        {
            for (int relax = 0; relax < 4; relax++)
            {
                var c = new List<Vector2I>();
                for (int y = 6; y < Ht - 6; y++)
                    for (int x = AX0 + 6; x < AX1 - 6; x++)
                    {
                        int d = dist[Key(x, y)];
                        if (d < (f0 - relax * .08f) * dmax || d > Math.Min(1f, f1 + relax * .08f) * dmax) continue;
                        if (!Clearance(x, y, Math.Max(1, clear - relax / 2))) continue;
                        bool ok = true; foreach (var tk in taken) { int dd = (tk.p.X - x) * (tk.p.X - x) + (tk.p.Y - y) * (tk.p.Y - y); int m = tk.r + sep; if (dd < m * m) { ok = false; break; } }
                        if (ok) c.Add(new Vector2I(x, y));
                    }
                if (c.Count > 0) { var t = c[r.Next(c.Count)]; taken.Add((t, clear + 2)); return t; }
            }
            return new Vector2I(-1, -1);
        }
        void Floor(int x, int y, int rad)
        {
            for (int yy = y - rad; yy <= y + rad; yy++)
                for (int xx = x - rad; xx <= x + rad; xx++)
                    if (In(xx, yy) && xx > AX0 + 4 && xx < AX1 - 2 && yy > 2 && yy < Ht - 3 && (xx - x) * (xx - x) + (yy - y) * (yy - y) <= rad * rad + 1) { Set(xx, yy, Tl.None); Ground[Key(xx, yy)] = (byte)(Art.AshFloor + r.Next(3)); }
        }
        var required = new List<Vector2I>();

        // древние фонари пустошей
        float[][] bands = { new[] { .25f, .40f }, new[] { .50f, .65f }, new[] { .72f, .86f } };
        for (int k = 0; k < 3; k++)
        {
            var t = Pick(bands[k][0], bands[k][1], 3, 14); if (t.X < 0) continue;
            Floor(t.X, t.Y, 4); Set(t.X, t.Y, Tl.AncOff);
            var an = new Ancient { Tile = t, RadiusTiles = 12, Cost = 9 + k, Region = 2 };
            Ancients.Add(an); required.Add(t + new Vector2I(0, 2));
            VeinsAsh(r, an, Tl.Obsidian, 5); VeinsAsh(r, an, Tl.Ember, 5);
        }
        // арена Пепельного стража
        {
            var t = Pick(.9f, 1f, 4, 16); if (t.X < 0) t = Pick(.6f, 1f, 3, 10);
            Floor(t.X, t.Y, 8);
            for (int yy = t.Y - 8; yy <= t.Y + 8; yy++) for (int xx = t.X - 8; xx <= t.X + 8; xx++) if (In(xx, yy) && (xx - t.X) * (xx - t.X) + (yy - t.Y) * (yy - t.Y) <= 64) Ground[Key(xx, yy)] = (byte)Art.Scorch;
            Set(t.X, t.Y, Tl.BossOff);
            for (int k = 0; k < 6; k++) { double a = k * Math.PI / 3 + .3; int vx = t.X + (int)Math.Round(Math.Cos(a) * 7), vy = t.Y + (int)Math.Round(Math.Sin(a) * 7); if (Get(vx, vy) == Tl.None) { Set(vx, vy, Tl.Vent); Vents.Add(new Vector2I(vx, vy)); } }
            var an = new Ancient { Tile = t, RadiusTiles = 14, Cost = 0, Boss = true, BossId = 3, Region = 2 };
            Ancients.Add(an); required.Add(t + new Vector2I(0, 3)); taken.Add((t, 10));
            VeinsAsh(r, an, Tl.Ember, 4); VeinsAsh(r, an, Tl.Obsidian, 4);
        }
        // горячий пепел
        for (int k = 0; k < 28; k++)
        {
            int cx = r.Next(AX0 + 18, AX1 - 6), cy = r.Next(7, Ht - 7), rad = r.Next(2, 5);
            bool ok = true;
            foreach (var tk in taken) { int dd = (tk.p.X - cx) * (tk.p.X - cx) + (tk.p.Y - cy) * (tk.p.Y - cy); int m = tk.r + rad + 2; if (dd < m * m) { ok = false; break; } }
            if (!ok) continue;
            for (int yy = cy - rad; yy <= cy + rad; yy++)
                for (int xx = cx - rad; xx <= cx + rad; xx++)
                    if (In(xx, yy) && Get(xx, yy) != Tl.Cliff && (xx - cx) * (xx - cx) + (yy - cy) * (yy - cy) <= rad * rad + 1) { if (Get(xx, yy) != Tl.None) Set(xx, yy, Tl.None); Ground[Key(xx, yy)] = (byte)Art.HotAsh; }
        }
        // жерла
        for (int k = 0; k < 10; k++)
        {
            var t = Pick(.15f, 1f, 1, 6); if (t.X < 0) continue;
            Set(t.X, t.Y, Tl.Vent); Vents.Add(t); Ground[Key(t.X, t.Y)] = (byte)Art.Scorch;
        }
        // природные руды (обсидиан и угольки)
        for (int k = 0; k < 10; k++) { var t = Pick(.2f, 1f, 1, 4); if (t.X >= 0) { Set(t.X, t.Y, Tl.Obsidian); foreach (var d in D4) if (r.Next(2) == 0 && Get(t.X + d.X, t.Y + d.Y) == Tl.None) Set(t.X + d.X, t.Y + d.Y, Tl.Obsidian); } }
        for (int k = 0; k < 10; k++) { var t = Pick(.2f, 1f, 1, 4); if (t.X >= 0) { Set(t.X, t.Y, Tl.Ember); foreach (var d in D4) if (r.Next(2) == 0 && Get(t.X + d.X, t.Y + d.Y) == Tl.None) Set(t.X + d.X, t.Y + d.Y, Tl.Ember); } }
        // места
        for (int i = 0; i < 5; i++) { var t = Pick(.1f, 1f, 1, 8); if (t.X < 0) continue; Set(t.X, t.Y, Tl.Chest); AddPoi(PK.Chest, t); required.Add(t + new Vector2I(0, 1)); }
        for (int i = 0; i < 3; i++)
        {
            var t = Pick(.3f, .9f, 4, 10); if (t.X < 0) continue;
            Floor(t.X, t.Y, 4);
            Set(t.X, t.Y, Tl.Chest); Set(t.X - 3, t.Y - 2, Tl.Tent); Set(t.X + 3, t.Y - 2, Tl.Tent); Set(t.X, t.Y + 3, Tl.Tent);
            AddPoi(PK.Camp, t, 4 + i); required.Add(t + new Vector2I(0, 2));
        }
        { var t = Pick(.2f, .8f, 2, 10); if (t.X >= 0) { Set(t.X, t.Y, Tl.Shrine); AddPoi(PK.Shrine, t); required.Add(t + new Vector2I(0, 1)); } }
        for (int i = 0; i < 2; i++) { var t = Pick(.15f, .9f, 2, 10); if (t.X >= 0) { Set(t.X, t.Y, Tl.Obelisk); AddPoi(PK.Obelisk, t); required.Add(t + new Vector2I(0, 1)); } }
        for (int i = 0; i < 2; i++) Stamp(r, Flip(r, i % 2 == 0 ? PuzzleA() : PuzzleB()), .2f, dist, dmax, required, PK.Puzzle, AX0, AX1 - 2, Art.AshFloor, Tl.AshRuin);
        Stamp(r, Workshop(), .2f, dist, dmax, required, PK.Workshop, AX0, AX1 - 2, Art.AshFloor, Tl.AshRuin);
        for (int i = 0; i < 2; i++) Stamp(r, Stash(), .15f, dist, dmax, required, PK.Chest, AX0, AX1 - 2, Art.AshFloor, Tl.AshRuin);

        foreach (var t in required) MakeReachable(ent, t);
    }

    void VeinsAsh(Random rng, Ancient an, Tl t, int count)
    {
        int placed = 0, tries = 0;
        while (placed < count && tries++ < 500)
        {
            double a = rng.NextDouble() * Math.PI * 2, d = 5 + rng.NextDouble() * 6;
            int x = an.Tile.X + (int)(Math.Cos(a) * d), y = an.Tile.Y + (int)(Math.Sin(a) * d);
            if (Get(x, y) != Tl.None || Ground[Key(x, y)] == Art.HotAsh || an.Veins.Exists(v => v.x == x && v.y == y)) continue;
            an.Veins.Add((x, y, t)); placed++;
        }
    }
}
