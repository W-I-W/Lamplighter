using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

public enum Tl : byte { None, Tree, Stone, Copper, Iron, Lumen, AncOff, AncOn, Furnace, Lantern, Cliff }

public sealed class Ancient
{
    public Vector2I Tile;
    public bool Lit;
    public float Anim;          // 0..1 — анимация отступления тьмы
    public int Cost;            // осколков света для зажигания
    public float RadiusTiles;
    public List<(int x, int y, Tl t)> Veins = new();   // скрытые жилы, появляются после зажигания
    public Vector2 Pos => new((Tile.X + .5f) * World.TS, (Tile.Y + .5f) * World.TS);
}

/// <summary>Данные мира: два плоских массива вместо Node на тайл.</summary>
public sealed class World
{
    public const int N = 128, TS = 16;
    public int Seed;
    public byte[] Obj = new byte[N * N];
    public byte[] Ground = new byte[N * N];
    public List<Ancient> Ancients = new();
    public Vector2I Spawn = new(N / 2, N / 2 + 2);

    public static bool In(int x, int y) => x >= 0 && y >= 0 && x < N && y < N;
    public Tl Get(int x, int y) => In(x, y) ? (Tl)Obj[y * N + x] : Tl.Cliff;
    public void Set(int x, int y, Tl t) { if (In(x, y)) Obj[y * N + x] = (byte)t; }

    public static World Generate(int seed)
    {
        var w = new World { Seed = seed };
        var rng = new Random(seed);
        var nz = new FastNoiseLite { Seed = seed, Frequency = 0.05f, NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex };
        var forest = new FastNoiseLite { Seed = seed + 7, Frequency = 0.045f, NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex };
        var edge = new FastNoiseLite { Seed = seed + 13, Frequency = 0.25f };
        int c = N / 2;

        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                int i = y * N + x;
                w.Ground[i] = nz.GetNoise2D(x, y) > .3f ? (byte)Art.Dirt : (byte)(rng.NextDouble() < .72 ? 0 : rng.Next(1, 4));
                int d = Math.Min(Math.Min(x, y), Math.Min(N - 1 - x, N - 1 - y));
                if (d < 2 + (int)((edge.GetNoise2D(x, y) + 1f) * 1.2f)) { w.Obj[i] = (byte)Tl.Cliff; continue; }
                double q = rng.NextDouble();
                if (forest.GetNoise2D(x, y) > .22f && q < .5) w.Obj[i] = (byte)Tl.Tree;
                else if (q < .012) w.Obj[i] = (byte)Tl.Tree;
                else if (q < .022) w.Obj[i] = (byte)Tl.Stone;
            }

        for (int k = 0; k < 20; k++) w.Cluster(rng, Tl.Copper, rng.Next(8, N - 8), rng.Next(8, N - 8), rng.Next(3, 6), 2);
        for (int k = 0; k < 16; k++) w.Cluster(rng, Tl.Stone, rng.Next(8, N - 8), rng.Next(8, N - 8), rng.Next(4, 8), 2);

        // стартовая зона: чистая площадка и гарантированные ресурсы рядом
        for (int y = c - 5; y <= c + 5; y++)
            for (int x = c - 5; x <= c + 5; x++)
                if ((x - c) * (x - c) + (y - c) * (y - c) <= 30) w.Set(x, y, Tl.None);
        void Near(Tl t, int n, int cnt)
        {
            for (int k = 0; k < n; k++)
            {
                double a = rng.NextDouble() * Math.PI * 2; double d = 6.5 + rng.NextDouble() * 4;
                w.Cluster(rng, t, c + (int)(Math.Cos(a) * d), c + (int)(Math.Sin(a) * d), cnt, 2, true);
            }
        }
        Near(Tl.Tree, 4, 7); Near(Tl.Stone, 3, 6); Near(Tl.Copper, 2, 4);

        // маяк в центре — уже зажжён
        w.Ancients.Add(new Ancient { Tile = new Vector2I(c, c), Lit = true, Anim = 1, RadiusTiles = 11, Cost = 0 });
        w.Set(c, c, Tl.AncOn);

        // 5 древних фонарей по спирали
        float[] dist = { 19, 29, 38, 46, 53 };
        double a0 = rng.NextDouble() * Math.PI * 2;
        for (int k = 0; k < 5; k++)
        {
            double a = a0 + k * 2.4;
            int x = Math.Clamp(c + (int)(Math.Cos(a) * dist[k]), 9, N - 10), y = Math.Clamp(c + (int)(Math.Sin(a) * dist[k]), 9, N - 10);
            for (int yy = y - 3; yy <= y + 3; yy++)
                for (int xx = x - 3; xx <= x + 3; xx++)
                    if ((xx - x) * (xx - x) + (yy - y) * (yy - y) <= 11) w.Set(xx, yy, Tl.None);
            var an = new Ancient { Tile = new Vector2I(x, y), RadiusTiles = 12, Cost = 2 + k };
            w.Set(x, y, Tl.AncOff);
            w.Ancients.Add(an);
        }
        // скрытые жилы: открываются зажиганием соответствующего фонаря
        for (int k = 1; k <= 5; k++)
        {
            var an = w.Ancients[k];
            if (k <= 4) w.Veins(rng, an, Tl.Iron, 7);
            if (k >= 3) w.Veins(rng, an, Tl.Lumen, 4);
        }
        return w;
    }

    void Cluster(Random rng, Tl t, int cx, int cy, int count, int spread, bool overwrite = false)
    {
        for (int i = 0; i < count; i++)
        {
            int x = cx + rng.Next(-spread, spread + 1), y = cy + rng.Next(-spread, spread + 1);
            if (!In(x, y) || Get(x, y) == Tl.Cliff) continue;
            if (Get(x, y) == Tl.None || overwrite) Set(x, y, t);
        }
    }

    void Veins(Random rng, Ancient an, Tl t, int count)
    {
        int placed = 0, tries = 0;
        while (placed < count && tries++ < 400)
        {
            double a = rng.NextDouble() * Math.PI * 2, d = 4 + rng.NextDouble() * 6;
            int x = an.Tile.X + (int)(Math.Cos(a) * d), y = an.Tile.Y + (int)(Math.Sin(a) * d);
            if (Get(x, y) != Tl.None || an.Veins.Exists(v => v.x == x && v.y == y)) continue;
            an.Veins.Add((x, y, t)); placed++;
        }
    }
}
