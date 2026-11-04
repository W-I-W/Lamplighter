using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lamplighter;

/// <summary>Подготовленная арена босса: свободный круг, неломаемая стена в два слоя, от одного до четырёх проходов-ворот и тотем. Ворота в мире открыты (None); на время боя игра ставит на них BossGate.</summary>
public sealed class BossArena
{
    public int BossId, Radius; public Vector2I Center, Totem;
    public List<Vector2I[]> Gates = new();          // клетки каждого прохода: две в глубину, три в ширину
    public List<Vector2I> Entries = new();          // первая клетка снаружи от каждого прохода
    public Vector2 Pos => new((Center.X + .5f) * World.TS, (Center.Y + .5f) * World.TS);
    public bool Contains(Vector2 p, float extra = 0) => p.DistanceTo(Pos) <= (Radius + .5f) * World.TS + extra;
}

public sealed partial class World
{
    public List<BossArena> Arenas = new();
    public BossArena ArenaOf(int bossId) { foreach (var a in Arenas) if (a.BossId == bossId) return a; return null; }

    static readonly int[] ArenaRadius = { 10, 12, 12, 12, 13 };            // по номеру босса: лес (небольшая роща Пожирателя), глубины, пепел, зима, Источник Тьмы
    static readonly int[] ArenaGates = { 2, 2, 3, 3, 4 };
    static readonly HashSet<Tl> ArenaClearable = new() { Tl.Tree, Tl.Pine, Tl.DeadTree, Tl.Stone, Tl.Copper, Tl.Iron, Tl.Lumen, Tl.Aether, Tl.CrystalOre, Tl.SilverOre, Tl.Rubble, Tl.Ruin, Tl.CaveWall, Tl.Cliff,
        Tl.Obsidian, Tl.Ember, Tl.FrostOre, Tl.IceBlock, Tl.IceRuin, Tl.AshRuin, Tl.MeteorOre };

    /// <summary>Арены строятся после всех биомов, но до проверки достижимости: стена защищена от прокладки туннелей, а потерянные ею проходы к местам интереса и фонарям возвращаются.</summary>
    void GenArenas(int seed)
    {
        var r = new Random(seed * 53 + 11);
        var saved = OpenStoryPaths();
        var tracked = new List<(Vector2I want, Vector2I[] near)>();
        var reach = Reach(Spawn);
        foreach (var p in Pois) tracked.Add((p.Tile, D4.Select(d => p.Tile + d).Where(t => In(t.X, t.Y) && reach[Key(t.X, t.Y)]).ToArray()));
        foreach (var a in Ancients) { var t = a.Tile + new Vector2I(0, a.Boss ? 3 : 2); tracked.Add((t, reach[Key(t.X, t.Y)] ? new[] { t } : Array.Empty<Vector2I>())); }
        RestoreStoryPaths(saved);

        foreach (var a in Ancients) if (a.Boss && a.BossId >= 1 && a.BossId <= 5) BuildArena(a, r);

        saved = OpenStoryPaths();
        foreach (var (want, near) in tracked)
        {
            if (near.Length == 0) continue;
            var rch = Reach(Spawn);
            if (near.Any(t => rch[Key(t.X, t.Y)])) continue;
            MakeReachable(Spawn, near[0]);
        }
        foreach (var ar in Arenas) foreach (var e in ar.Entries) if (Walkable(e.X, e.Y) && !Reach(Spawn)[Key(e.X, e.Y)]) MakeReachable(Spawn, e);
        RestoreStoryPaths(saved);
    }

    List<(int k, byte v)> OpenStoryPaths()
    {
        var saved = new List<(int k, byte v)>();
        void Open(IEnumerable<Vector2I> ts, Tl to) { foreach (var t in ts) { int k = Key(t.X, t.Y); saved.Add((k, Obj[k])); Obj[k] = (byte)to; } }
        Open(Seal1, Tl.None); Open(Seal2, Tl.None); Open(Seal3, Tl.None); Open(Seal4, Tl.None);
        foreach (var g in HeartGaps) Open(g, Tl.LightBridge);
        return saved;
    }

    void RestoreStoryPaths(List<(int k, byte v)> saved) { for (int i = saved.Count - 1; i >= 0; i--) Obj[saved[i].k] = saved[i].v; }

    /// <summary>Центр арены: ближайшая к желаемой точке внутри области, где вокруг нет других фонарей (стена арены не должна отрезать их от мира).</summary>
    Vector2I ArenaCenter(Vector2I want, int x0, int x1)
    {
        bool Free(Vector2I c) { foreach (var a in Ancients) if (new Vector2(a.Tile.X - c.X, a.Tile.Y - c.Y).Length() < 19f) return false; return true; }
        Vector2I Clamp(Vector2I c) => new(Math.Clamp(c.X, x0, x1), Math.Clamp(c.Y, 18, Ht - 19));
        for (int rad = 0; rad <= 70; rad += 2)
            for (int k = 0; k < (rad == 0 ? 1 : 24); k++)
            {
                double ang = k * Math.PI * 2 / 24; var c = Clamp(new Vector2I(want.X + (int)Math.Round(Math.Cos(ang) * rad), want.Y + (int)Math.Round(Math.Sin(ang) * rad)));
                if (Free(c)) return c;
            }
        return Clamp(want);
    }

    void BuildArena(Ancient a, Random r)
    {
        int R = ArenaRadius[a.BossId - 1], n = ArenaGates[a.BossId - 1];
        var ar = new BossArena { BossId = a.BossId, Radius = R, Center = a.Tile, Totem = a.Tile + new Vector2I(0, 4) };
        Tl wall = a.Region == 1 ? Tl.CaveWall : Tl.Cliff;
        var gateSet = new HashSet<int>(); var corridor = new HashSet<int>();
        foreach (var d in GateDirs(a.Tile, R, n))
        {
            var p = new Vector2I(-d.Y, d.X); var tiles = new List<Vector2I>();
            for (int k = R + 1; k <= R + 2; k++) for (int o = -1; o <= 1; o++) tiles.Add(a.Tile + d * k + p * o);
            ar.Gates.Add(tiles.ToArray()); foreach (var t in tiles) gateSet.Add(Key(t.X, t.Y));
            for (int k = R + 3; k <= R + 5; k++) for (int o = -1; o <= 1; o++) { var t = a.Tile + d * k + p * o; if (In(t.X, t.Y)) corridor.Add(Key(t.X, t.Y)); }
            ar.Entries.Add(a.Tile + d * (R + 4));
        }
        // места интереса внутри круга, стены и проходов не переживают: убираем вместе с их клеткой
        foreach (var poi in Pois.ToList())
        {
            float dd = new Vector2(poi.Tile.X - a.Tile.X, poi.Tile.Y - a.Tile.Y).Length(); if (dd > R + 3.5f) continue;
            Pois.Remove(poi); ByTile.Remove(Key(poi.Tile.X, poi.Tile.Y)); if (Get(poi.Tile.X, poi.Tile.Y) != Tl.Void) Set(poi.Tile.X, poi.Tile.Y, Tl.None);
        }
        for (int y = a.Tile.Y - R - 5; y <= a.Tile.Y + R + 5; y++)
            for (int x = a.Tile.X - R - 5; x <= a.Tile.X + R + 5; x++)
            {
                if (!In(x, y)) continue; int k = Key(x, y); var cur = Get(x, y);
                float d = new Vector2(x - a.Tile.X, y - a.Tile.Y).Length();
                if (gateSet.Contains(k)) { Set(x, y, Tl.None); continue; }
                if (d <= R + .5f) { if (ArenaClearable.Contains(cur)) Set(x, y, Tl.None); }
                else if (d <= R + 2.5f) { Set(x, y, wall); prot[k] = true; }
                else if (corridor.Contains(k) && ArenaClearable.Contains(cur)) Set(x, y, Tl.None);
            }
        Set(ar.Totem.X, ar.Totem.Y, Tl.BossTotem);
        Arenas.Add(ar);
    }

    /// <summary>Стороны света для проходов: первый смотрит к старту мира, затем боковые и дальний; стороны, за которыми пустота, идут в конце.</summary>
    List<Vector2I> GateDirs(Vector2I c, int R, int n)
    {
        var v = Spawn - c;
        var a1 = Math.Abs(v.X) >= Math.Abs(v.Y) ? new Vector2I(v.X >= 0 ? 1 : -1, 0) : new Vector2I(0, v.Y >= 0 ? 1 : -1);
        var b = new Vector2I(-a1.Y, a1.X);
        if (a1.X != 0 ? v.Y < 0 : v.X < 0) b = -b;               // боковой проход — со стороны старта
        var order = new List<Vector2I> { a1, b, -b, -a1 };
        bool Open(Vector2I d) { var t = c + d * (R + 4); return In(t.X, t.Y) && Get(t.X, t.Y) != Tl.Void && t.X > 4 && t.Y > 4 && t.X < Wd - 5 && t.Y < Ht - 5; }
        var good = order.Where(Open).ToList(); var bad = order.Where(d => !Open(d)).ToList(); good.AddRange(bad);
        return good.Take(n).ToList();
    }
}
