using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

public sealed partial class World
{
    public List<Poi> Pois = new();
    public Dictionary<int, Poi> ByTile = new();

    void GenPois(int seed)
    {
        var r = new Random(seed * 31 + 17);
        int c = N / 2;
        var taken = new List<(Vector2I p, int r)> { (new Vector2I(c, c), 10) };
        foreach (var a in Ancients) taken.Add((a.Tile, 6));

        bool Spot(int rad, int dmin, int dmax, out Vector2I t)
        {
            for (int i = 0; i < 400; i++)
            {
                double ang = r.NextDouble() * Math.PI * 2, d = dmin + r.NextDouble() * (dmax - dmin);
                int x = c + (int)(Math.Cos(ang) * d), y = c + (int)(Math.Sin(ang) * d);
                if (x < rad + 5 || y < rad + 5 || x > N - rad - 6 || y > N - rad - 6) continue;
                bool ok = true;
                foreach (var q in taken)
                {
                    int dd = (q.p.X - x) * (q.p.X - x) + (q.p.Y - y) * (q.p.Y - y), m = q.r + rad + 2;
                    if (dd < m * m) { ok = false; break; }
                }
                if (!ok) continue;
                t = new Vector2I(x, y); taken.Add((t, rad));
                for (int yy = y - rad; yy <= y + rad; yy++)
                    for (int xx = x - rad; xx <= x + rad; xx++)
                        if ((xx - x) * (xx - x) + (yy - y) * (yy - y) <= rad * rad + 1) Set(xx, yy, Tl.None);
                return true;
            }
            t = default; return false;
        }
        Poi Add(PK k, Vector2I t, int guards = 0)
        {
            var p = new Poi { Kind = k, Tile = t, Guards = guards };
            Pois.Add(p); ByTile[Key(t.X, t.Y)] = p; return p;
        }

        for (int i = 0; i < 8; i++) if (Spot(1, 12, 56, out var t)) { Set(t.X, t.Y, Tl.Chest); Add(PK.Chest, t); }
        for (int i = 0; i < 5; i++)
            if (Spot(4, 16, 55, out var t))
            {
                for (int k = 0; k < 12; k++)
                {
                    if (k % 4 == 0) continue;
                    double a = k * Math.PI / 6;
                    Set(t.X + (int)Math.Round(Math.Cos(a) * 3), t.Y + (int)Math.Round(Math.Sin(a) * 3), Tl.Ruin);
                }
                Set(t.X, t.Y, Tl.Chest); Add(PK.Ruins, t);
            }
        for (int i = 0; i < 4; i++)
            if (Spot(3, 14, 50, out var t))
            {
                Set(t.X, t.Y - 1, Tl.House); Set(t.X - 1, t.Y - 1, Tl.Ruin); Set(t.X + 1, t.Y - 1, Tl.Ruin);
                var ct = new Vector2I(t.X, t.Y + 1); Set(ct.X, ct.Y, Tl.Chest); Add(PK.House, ct);
            }
        for (int i = 0; i < 5; i++)
            if (Spot(5, 22, 56, out var t))
            {
                Set(t.X, t.Y, Tl.Chest);
                Set(t.X - 3, t.Y - 2, Tl.Tent); Set(t.X + 3, t.Y - 2, Tl.Tent); Set(t.X, t.Y + 3, Tl.Tent);
                Add(PK.Camp, t, 3 + (i >= 3 ? 1 : 0) + (i == 4 ? 1 : 0));
            }
        for (int i = 0; i < 3; i++) if (Spot(2, 14, 52, out var t)) { Set(t.X, t.Y, Tl.Shrine); Add(PK.Shrine, t); }
        int[][] cr = { new[] { 14, 24 }, new[] { 26, 38 }, new[] { 40, 55 } };
        for (int i = 0; i < 3; i++)
            if (Spot(4, cr[i][0], cr[i][1], out var t))
            {
                Set(t.X, t.Y, Tl.Cage); Set(t.X - 2, t.Y - 1, Tl.Ruin); Set(t.X + 2, t.Y + 1, Tl.Ruin);
                Add(PK.Captive, t, 2 + (i > 0 ? 1 : 0));
            }
        for (int i = 0; i < 3; i++) if (Spot(2, 18, 52, out var t)) { Set(t.X, t.Y, Tl.Obelisk); Add(PK.Obelisk, t); }
        for (int i = 0; i < 4; i++)
            if (Spot(3, 38, 56, out var t))
            {
                int n = 0;
                for (int k = 0; k < 30 && n < 5; k++)
                {
                    int x = t.X + r.Next(-2, 3), y = t.Y + r.Next(-2, 3);
                    if (Get(x, y) == Tl.None) { Set(x, y, Tl.Aether); n++; }
                }
                Add(PK.Grove, t);
            }
    }
}
