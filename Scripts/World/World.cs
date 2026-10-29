using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

public enum Tl : byte
{
    None, Tree, Stone, Copper, Iron, Lumen, AncOff, AncOn, Furnace, Lantern, Cliff,
    Chest, ChestOpen, Ruin, House, Tent, Shrine, ShrineUsed, Aether, Cage, BossOff, BossOn, Obelisk, ObeliskUsed,
    Warehouse, Sawmill, Mine, Forge,
    CaveWall, CrystalOre, SilverOre, Machine, MachineOn, Mirror1, Mirror2, Receiver, ReceiverOn, Emitter,
    CrystalLantern, CombatLantern, Rubble, BridgeBroken, Bridge, Deep, Seal, SecretDoor, Drill, Collector,
    DeadTree, Obsidian, Ember, AshRuin, Vent,
    TechCache, Spire, Altar, AltarOn, SecretCache, Stall, Hub, Refinery, AmpCrystal, BarrierPost, MeteorOre,
    FrostOre, Pine, IceRuin, IceBlock, Void, Monolith, Tablet, FalseLantern, Pylon, PylonOn, ShadowPillar, Fragment, RiftGate, Statue, Fountain, Banner, Cryo, LightBridge
}

public enum PK { Chest, Ruins, House, Camp, Shrine, Captive, Obelisk, Grove, Workshop, Puzzle, BridgeSite, TechCache, Spire, Altar, Secret, Lore, Tower, Vault, Glade, SparkAltar, RuneObelisk, GiantTree, Survivor }

/// <summary>Место интереса. State: 0 нетронуто, 1 стража вышла, 2 стража побеждена, 3 завершено.</summary>
public sealed class Poi
{
    public PK Kind; public Vector2I Tile; public byte State, Variant; public bool Seen, Revealed; public int Guards; public string Tag;
    public Vector2 Pos => new((Tile.X + .5f) * World.TS, (Tile.Y + .5f) * World.TS);
}

public sealed class Ancient
{
    public Vector2I Tile;
    public bool Lit, Boss; public int Region, BossId;
    public float Anim;          // 0..1 — анимация отступления тьмы
    public int Cost;            // осколков света для зажигания
    public float RadiusTiles, TargetRadius, Pulse;
    public LampKind Kind;       // вид фонаря (этап 8)
    public bool Done;           // условие вида выполнено: стража побеждена или теневой кристалл разрушен
    public Vector2I Crystal = new(-1, -1);   // теневой кристалл заражённого фонаря
    public Poi GuardPoi = new() { Kind = PK.Camp };   // «хозяин» стражей охраняемого фонаря (в список мест не входит)
    public byte Phase;          // 0 — не тронут, 1 — стража вышла
    public Vector2I[] Pylons;   // загадочный фонарь: три светила (запад, север, восток) и порядок их касания
    public int[] Seq; public int SeqPos; public Vector2I Tablet = new(-1, -1);
    public List<(int x, int y, Tl t)> Veins = new();   // скрытые жилы, появляются после зажигания
    public Vector2 Pos => new((Tile.X + .5f) * World.TS, (Tile.Y + .5f) * World.TS);
}

/// <summary>Данные мира: два плоских массива вместо Node на тайл.</summary>
public sealed partial class World
{
    public const int N = 128, TS = 16;          // N — сторона области леса
    /// <summary>Деревья двухклеточные: крона занимает клетку над стволом. Дерево, над которым стоит камень, руда или другой объект, убирается (у верхней кромки мира тоже).</summary>
    void ClearBlockedCrowns()
    {
        for (int y = 0; y < Ht; y++)
            for (int x = 0; x < Wd; x++)
            {
                var t = (Tl)Obj[y * Wd + x]; if (t != Tl.Tree && t != Tl.Pine && t != Tl.DeadTree) continue;
                if (y == 0) { Obj[y * Wd + x] = (byte)Tl.None; continue; }
                var up = (Tl)Obj[(y - 1) * Wd + x]; if (up != Tl.None && up != Tl.Tree && up != Tl.Pine && up != Tl.DeadTree) Obj[y * Wd + x] = (byte)Tl.None;
            }
    }

    public const int Wd = 640, Ht = 128;        // весь мир: лес, шахты, пустоши, монастырь, Сердце Тьмы
    public const int MX0 = 128, MX1 = 255, AX0 = 256, AX1 = 383, IX0 = 384, IX1 = 511, HX0 = 512, HX1 = 639;
    public int Seed;
    public byte[] Obj = new byte[Wd * Ht];
    public byte[] GenObj;
    public byte[] Ground = new byte[Wd * Ht];
    public static int Key(int x, int y) => y * Wd + x;
    public static bool Blocks(Tl t) => t != Tl.None && t != Tl.Bridge && t != Tl.SecretCache && t != Tl.LightBridge;
    public static int RegionOf(int tx) => tx < MX0 ? 0 : tx <= MX1 ? 1 : tx <= AX1 ? 2 : tx <= IX1 ? 3 : 4;
    public List<Ancient> Ancients = new();
    public Vector2I Spawn = new(N / 2, N / 2 + 2);

    public static bool In(int x, int y) => x >= 0 && y >= 0 && x < Wd && y < Ht;
    public Tl Get(int x, int y) => In(x, y) ? (Tl)Obj[y * Wd + x] : Tl.Cliff;
    public void Set(int x, int y, Tl t) { if (In(x, y)) Obj[y * Wd + x] = (byte)t; }

    public static World Generate(int seed, int genV = CurrentGen)
    {
        var w = new World { Seed = seed, GenV = genV };
        var rng = new Random(seed);
        var nz = new FastNoiseLite { Seed = seed, Frequency = 0.05f, NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex };
        var forest = new FastNoiseLite { Seed = seed + 7, Frequency = 0.045f, NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex };
        var edge = new FastNoiseLite { Seed = seed + 13, Frequency = 0.25f };
        int c = N / 2;

        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                int i = y * Wd + x;
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

        // Хранитель: особый древний фонарь на краю карты
        {
            double a = a0 + 5 * 2.4;
            int x = Math.Clamp(c + (int)(Math.Cos(a) * 54), 10, N - 11), y = Math.Clamp(c + (int)(Math.Sin(a) * 54), 10, N - 11);
            for (int yy = y - 5; yy <= y + 5; yy++)
                for (int xx = x - 5; xx <= x + 5; xx++)
                    if ((xx - x) * (xx - x) + (yy - y) * (yy - y) <= 25) w.Set(xx, yy, Tl.None);
            var an = new Ancient { Tile = new Vector2I(x, y), RadiusTiles = 14, Cost = 0, Boss = true, BossId = 1 };
            w.Set(x, y, Tl.BossOff); w.Ancients.Add(an);
            w.Veins(rng, an, Tl.Aether, 4); w.Veins(rng, an, Tl.Lumen, 4);
        }
        w.GenPois(seed);
        w.GenMines(seed);
        w.GenAsh(seed);
        w.GenFinds(seed, 0, 2);
        w.GenIce(seed);
        w.GenHeart(seed);
        w.GenFinds(seed, 3, 4);
        w.GenLore(seed);
        if (genV >= 1) w.GenExplore(seed);
        w.EnsureStoryReachable();
        w.ClearBlockedCrowns();
        w.GenObj = (byte[])w.Obj.Clone();
        w.ScaleLampCosts();
        return w;
    }

    public const float LampCostBase = 1.8f, LampCostPerTile = 1f / 80f;      // цена зажигания = базовая цена фонаря × (1,8 + расстояние от Центрального маяка в клетках / 80)

    /// <summary>Чем глубже фонарь от центра мира, тем он дороже: множитель растёт с расстоянием до Центрального маяка. Стражи и сам маяк бесплатны. Расчёт без случайных чисел, поэтому карта не меняется.</summary>
    void ScaleLampCosts()
    {
        var c = Ancients[0].Tile;
        foreach (var a in Ancients) { if (a.Boss || a.Cost <= 0) continue; float d = new Vector2(a.Tile.X - c.X, a.Tile.Y - c.Y).Length(); a.Cost = Math.Max(a.Cost, Mathf.RoundToInt(a.Cost * (LampCostBase + d * LampCostPerTile))); }
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
