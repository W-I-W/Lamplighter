using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Общий набор инструментов генерации региона: расстояния от входа, подбор мест, расчистка площадок.</summary>
public sealed class RegionKit
{
    public readonly World W; public readonly Random R; public readonly int X0, X1; public readonly Vector2I Ent;
    public int[] Dist; public int DMax; public readonly List<(Vector2I p, int r)> Taken = new(); public readonly List<Vector2I> Required = new();
    readonly bool[] prot;

    public RegionKit(World w, Random r, int x0, int x1, Vector2I ent) { W = w; R = r; X0 = x0; X1 = x1; Ent = ent; Dist = new int[World.Wd * World.Ht]; prot = null; Bfs(); }

    public void Bfs()
    {
        Array.Fill(Dist, -1); DMax = 0;
        if (!W.Walkable(Ent.X, Ent.Y)) return;
        var q = new Queue<Vector2I>(); q.Enqueue(Ent); Dist[World.Key(Ent.X, Ent.Y)] = 0;
        while (q.Count > 0)
        {
            var c = q.Dequeue();
            foreach (var d in World.D4)
            {
                var n = c + d; if (n.X < X0 || n.X > X1 || !World.In(n.X, n.Y) || Dist[World.Key(n.X, n.Y)] >= 0 || !W.Walkable(n.X, n.Y)) continue;
                Dist[World.Key(n.X, n.Y)] = Dist[World.Key(c.X, c.Y)] + 1; DMax = Math.Max(DMax, Dist[World.Key(n.X, n.Y)]); q.Enqueue(n);
            }
        }
    }

    public bool Clearance(int x, int y, int rad)
    {
        for (int yy = y - rad; yy <= y + rad; yy++) for (int xx = x - rad; xx <= x + rad; xx++) if (W.Get(xx, yy) != Tl.None) return false;
        return true;
    }

    public Vector2I Pick(float f0, float f1, int clear, int sep)
    {
        for (int relax = 0; relax < 4; relax++)
        {
            var c = new List<Vector2I>();
            for (int y = 6; y < World.Ht - 6; y++)
                for (int x = X0 + 6; x < X1 - 6; x++)
                {
                    int d = Dist[World.Key(x, y)];
                    if (d < (f0 - relax * .08f) * DMax || d > Math.Min(1f, f1 + relax * .08f) * DMax) continue;
                    if (!Clearance(x, y, Math.Max(1, clear - relax / 2))) continue;
                    var g = W.Ground[World.Key(x, y)]; if (g == Art.WaterG || g == Art.HotAsh) continue;
                    bool ok = true; foreach (var tk in Taken) { int dd = (tk.p.X - x) * (tk.p.X - x) + (tk.p.Y - y) * (tk.p.Y - y); int m = tk.r + sep; if (dd < m * m) { ok = false; break; } }
                    if (ok) c.Add(new Vector2I(x, y));
                }
            if (c.Count > 0) { var t = c[R.Next(c.Count)]; Taken.Add((t, clear + 2)); return t; }
        }
        return new Vector2I(-1, -1);
    }

    public void Floor(int x, int y, int rad, int floorBase, int edge = 4)
    {
        for (int yy = y - rad; yy <= y + rad; yy++)
            for (int xx = x - rad; xx <= x + rad; xx++)
                if (World.In(xx, yy) && xx > X0 + edge && xx < X1 - 2 && yy > 2 && yy < World.Ht - 3 && (xx - x) * (xx - x) + (yy - y) * (yy - y) <= rad * rad + 1)
                { W.Set(xx, yy, Tl.None); W.Ground[World.Key(xx, yy)] = (byte)(floorBase + R.Next(2)); }
    }

    public void Veins(Ancient an, Tl t, int count, int avoidGround = -1)
    {
        int placed = 0, tries = 0;
        while (placed < count && tries++ < 500)
        {
            double a = R.NextDouble() * Math.PI * 2, d = 5 + R.NextDouble() * 6;
            int x = an.Tile.X + (int)(Math.Cos(a) * d), y = an.Tile.Y + (int)(Math.Sin(a) * d);
            if (W.Get(x, y) != Tl.None || W.Ground[World.Key(x, y)] == avoidGround || W.Ground[World.Key(x, y)] == Art.WaterG || an.Veins.Exists(v => v.x == x && v.y == y)) continue;
            an.Veins.Add((x, y, t)); placed++;
        }
    }

    public Poi Poi(PK k, Vector2I t, Tl tile, int guards = 0, byte variant = 0, string tag = null)
    {
        W.Set(t.X, t.Y, tile); var p = W.AddPoiPublic(k, t, guards, variant); p.Tag = tag; return p;
    }
}
