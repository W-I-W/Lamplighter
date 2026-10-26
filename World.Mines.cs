using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

public sealed class Puzzle
{
    public List<Vector2I> Receivers = new(), Doors = new(), Mirrors = new();
    public List<Vector2I> Emitters = new();
    public Poi Poi;
}

public sealed class BeamTrace
{
    public List<Vector2I> Corners = new(); public Vector2I? Hit; public Vector2I EndDir, EndTile;
}

public sealed partial class World
{
    public List<Puzzle> Puzzles = new();
    public List<(Vector2I t, Vector2I d)> Emitters = new();
    public List<Vector2I> Seal1 = new(), Seal2 = new();
    bool[] prot = new bool[Wd * Ht];
    readonly List<(Vector2I p, int r)> taken = new();

    public static readonly Vector2I[] D4 = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };
    public bool Walkable(int x, int y) { var t = Get(x, y); return t == Tl.None || t == Tl.Bridge; }

    // ───────────────────────── лучи света ─────────────────────────
    public static Vector2I Reflect(Tl m, Vector2I d) => m == Tl.Mirror1 ? new Vector2I(-d.Y, -d.X) : new Vector2I(d.Y, d.X);

    public BeamTrace Trace(Vector2I start, Vector2I dir, int maxLen)
    {
        var b = new BeamTrace(); b.Corners.Add(start); var cur = start; var d = dir;
        for (int i = 0; i < maxLen; i++)
        {
            var n = cur + d; var t = Get(n.X, n.Y);
            if (t == Tl.Mirror1 || t == Tl.Mirror2) { b.Corners.Add(n); d = Reflect(t, d); cur = n; continue; }
            if (t == Tl.Receiver || t == Tl.ReceiverOn) { b.Hit = n; b.Corners.Add(n); b.EndTile = n; b.EndDir = d; return b; }
            if (Blocks(t)) break;
            cur = n;
        }
        b.Corners.Add(cur); b.EndTile = cur; b.EndDir = d; return b;
    }

    // ───────────────────────── достижимость ─────────────────────────
    public bool[] Reach(Vector2I start)
    {
        var r = new bool[Wd * Ht]; var q = new Queue<Vector2I>();
        if (!Walkable(start.X, start.Y)) return r;
        r[Key(start.X, start.Y)] = true; q.Enqueue(start);
        while (q.Count > 0)
        {
            var c = q.Dequeue();
            foreach (var d in D4)
            {
                var n = c + d;
                if (!In(n.X, n.Y) || r[Key(n.X, n.Y)] || !Walkable(n.X, n.Y)) continue;
                r[Key(n.X, n.Y)] = true; q.Enqueue(n);
            }
        }
        return r;
    }

    /// <summary>Гарантирует проходимость: если цель недостижима, прорубает кратчайший туннель (не трогая защищённые префабы).</summary>
    public bool MakeReachable(Vector2I start, Vector2I target)
    {
        var reach = Reach(start);
        if (reach[Key(target.X, target.Y)]) return true;
        var prev = new int[Wd * Ht]; Array.Fill(prev, -1);
        var q = new Queue<Vector2I>(); q.Enqueue(target); prev[Key(target.X, target.Y)] = Key(target.X, target.Y);
        int found = -1;
        while (q.Count > 0 && found < 0)
        {
            var c = q.Dequeue();
            if (reach[Key(c.X, c.Y)]) { found = Key(c.X, c.Y); break; }
            foreach (var d in D4)
            {
                var n = c + d; if (!In(n.X, n.Y)) continue;
                int k = Key(n.X, n.Y);
                if (prev[k] != -1 || prot[k]) continue;
                prev[k] = Key(c.X, c.Y); q.Enqueue(n);
            }
        }
        if (found < 0) return false;
        int cur = found, tk = Key(target.X, target.Y);
        while (true)
        {
            int cx = cur % Wd, cy = cur / Wd;
            if (!Walkable(cx, cy)) Obj[cur] = (byte)Tl.None;
            if (cur == tk) break;
            cur = prev[cur];
        }
        return true;
    }

    // ───────────────────────── шахты ─────────────────────────
    void Clear(int x, int y, int rad, byte ground)
    {
        for (int yy = y - rad; yy <= y + rad; yy++)
            for (int xx = x - rad; xx <= x + rad; xx++)
                if (In(xx, yy) && (xx - x) * (xx - x) + (yy - y) * (yy - y) <= rad * rad + 1) { Set(xx, yy, Tl.None); Ground[Key(xx, yy)] = ground; }
    }

    Poi AddPoi(PK k, Vector2I t, int guards = 0, byte variant = 0)
    {
        var p = new Poi { Kind = k, Tile = t, Guards = guards, Variant = variant };
        Pois.Add(p); ByTile[Key(t.X, t.Y)] = p; return p;
    }

    void GenMines(int seed)
    {
        var r = new Random(seed * 53 + 101);
        int w = MX1 - MX0 + 1, h = Ht;
        bool[,] open = null;
        for (int attempt = 0; attempt < 30; attempt++)
        {
            var g = new bool[w, h];
            for (int x = 4; x < w - 4; x++) for (int y = 4; y < h - 4; y++) g[x, y] = r.NextDouble() > .46;
            for (int it = 0; it < 5; it++)
            {
                var n = new bool[w, h];
                for (int x = 1; x < w - 1; x++)
                    for (int y = 1; y < h - 1; y++)
                    {
                        int walls = 0;
                        for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++) if ((dx != 0 || dy != 0) && !g[x + dx, y + dy]) walls++;
                        n[x, y] = walls < 5;
                    }
                g = n;
            }
            for (int x = 0; x <= 14; x++) for (int y = 60; y <= 68; y++) g[x, y] = true;
            for (int x = w - 14; x < w; x++) for (int y = 60; y <= 68; y++) g[x, y] = true;     // подход к восточному проходу
            var comp = new bool[w, h]; var q = new Queue<(int, int)>(); q.Enqueue((8, 64)); comp[8, 64] = true; int cnt = 0;
            while (q.Count > 0)
            {
                var (cx, cy) = q.Dequeue(); cnt++;
                foreach (var d in D4) { int nx = cx + d.X, ny = cy + d.Y; if (nx >= 0 && ny >= 0 && nx < w && ny < h && g[nx, ny] && !comp[nx, ny]) { comp[nx, ny] = true; q.Enqueue((nx, ny)); } }
            }
            open = comp; if (cnt > 4200) break;
        }
        var lake = new FastNoiseLite { Seed = seed + 31, Frequency = .065f, NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex };
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                int gx = MX0 + x, i = Key(gx, y);
                Ground[i] = (byte)(Art.CaveFloor + r.Next(3));
                Obj[i] = (byte)(open[x, y] ? Tl.None : Tl.CaveWall);
                if (open[x, y] && x > 22 && x < w - 18 && lake.GetNoise2D(gx, y) > .28f) Ground[i] = (byte)Art.WaterG;
            }

        // проход из леса (запечатан) и проход в пустоши (запечатан)
        for (int y = 61; y <= 67; y++)
            for (int x = 116; x <= 127; x++) { Set(x, y, Tl.None); Ground[Key(x, y)] = (byte)Art.Dirt; }
        for (int y = 61; y <= 67; y++) { Set(125, y, Tl.Seal); Set(126, y, Tl.Seal); Seal1.Add(new Vector2I(125, y)); Seal1.Add(new Vector2I(126, y)); }
        for (int y = 61; y <= 67; y++)
            for (int x = 242; x <= MX1; x++) { Set(x, y, Tl.None); Ground[Key(x, y)] = (byte)Art.CaveFloor; }
        for (int y = 61; y <= 67; y++) { Set(252, y, Tl.Seal); Set(253, y, Tl.Seal); Seal2.Add(new Vector2I(252, y)); Seal2.Add(new Vector2I(253, y)); }
        taken.Add((new Vector2I(121, 64), 8)); taken.Add((new Vector2I(MX0 + 8, 64), 12)); taken.Add((new Vector2I(248, 64), 10));
        // расстояния от входа
        var dist = new int[Wd * Ht]; Array.Fill(dist, -1);
        var bq = new Queue<Vector2I>(); var ent = new Vector2I(MX0 + 8, 64); bq.Enqueue(ent); dist[Key(ent.X, ent.Y)] = 0; int dmax = 0;
        while (bq.Count > 0)
        {
            var c = bq.Dequeue();
            foreach (var d in D4)
            {
                var n = c + d; if (n.X < MX0 || n.X > 241 || !In(n.X, n.Y) || dist[Key(n.X, n.Y)] >= 0 || Get(n.X, n.Y) != Tl.None) continue;
                dist[Key(n.X, n.Y)] = dist[Key(c.X, c.Y)] + 1; dmax = Math.Max(dmax, dist[Key(n.X, n.Y)]); bq.Enqueue(n);
            }
        }
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
                    for (int x = MX0 + 6; x < 240; x++)
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

        var required = new List<Vector2I>();     // цели, которые обязаны быть достижимы
        // древние фонари шахт
        float[][] bands = { new[] { .25f, .40f }, new[] { .48f, .62f }, new[] { .70f, .84f } };
        for (int k = 0; k < 3; k++)
        {
            var t = Pick(bands[k][0], bands[k][1], 3, 14);
            if (t.X < 0) continue;
            Clear(t.X, t.Y, 3, (byte)Art.CaveFloor);
            Set(t.X, t.Y, Tl.AncOff);
            var an = new Ancient { Tile = t, RadiusTiles = 12, Cost = 6 + k, Region = 1 };
            Ancients.Add(an); required.Add(t + new Vector2I(0, 2));
            VeinsDry(r, an, Tl.SilverOre, 6); VeinsDry(r, an, Tl.CrystalOre, 4);
        }
        // арена Хранителя глубин
        {
            var t = Pick(.93f, 1f, 4, 16);
            if (t.X < 0) t = Pick(.7f, 1f, 3, 10);
            for (int yy = t.Y - 8; yy <= t.Y + 8; yy++)
                for (int xx = t.X - 8; xx <= t.X + 8; xx++)
                {
                    float dd = (xx - t.X) * (xx - t.X) + (yy - t.Y) * (yy - t.Y);
                    if (dd > 64 || !In(xx, yy) || xx < MX0 + 3 || xx > 238 || yy < 3 || yy > Ht - 4) continue;
                    Set(xx, yy, Tl.None); Ground[Key(xx, yy)] = (byte)(dd > 9 && dd < 42 ? Art.WaterG : Art.CaveFloor + r.Next(3));
                }
            Set(t.X, t.Y, Tl.BossOff);
            var an = new Ancient { Tile = t, RadiusTiles = 14, Cost = 0, Boss = true, BossId = 2, Region = 1 };
            Ancients.Add(an); required.Add(t + new Vector2I(0, 3)); taken.Add((t, 10));
            VeinsDry(r, an, Tl.CrystalOre, 5);
        }

        // естественные руды в стенах
        var wallAdj = new List<Vector2I>();
        for (int y = 4; y < Ht - 4; y++)
            for (int x = MX0 + 4; x < 240; x++)
            {
                if (Get(x, y) != Tl.CaveWall) continue;
                foreach (var d in D4) { var n = new Vector2I(x + d.X, y + d.Y); if (In(n.X, n.Y) && dist[Key(n.X, n.Y)] > 30) { wallAdj.Add(new Vector2I(x, y)); break; } }
            }
        for (int i = 0; i < 70 && wallAdj.Count > 0; i++) { var wc = wallAdj[r.Next(wallAdj.Count)]; Set(wc.X, wc.Y, Tl.CrystalOre); }
        for (int i = 0; i < 40 && wallAdj.Count > 0; i++)
        {
            var c = wallAdj[r.Next(wallAdj.Count)];
            foreach (var d in new[] { new Vector2I(0, 0), new Vector2I(1, 0), new Vector2I(0, 1) })
                if (Get(c.X + d.X, c.Y + d.Y) == Tl.CaveWall) Set(c.X + d.X, c.Y + d.Y, Tl.SilverOre);
        }

        // обычные места
        for (int i = 0; i < 5; i++) { var t = Pick(.1f, 1f, 1, 8); if (t.X < 0) continue; Set(t.X, t.Y, Tl.Chest); AddPoi(PK.Chest, t); required.Add(t + new Vector2I(0, 1)); }
        for (int i = 0; i < 3; i++)
        {
            var t = Pick(.3f, .9f, 4, 10); if (t.X < 0) continue;
            Clear(t.X, t.Y, 4, (byte)Art.CaveFloor);
            Set(t.X, t.Y, Tl.Chest); Set(t.X - 3, t.Y - 2, Tl.Tent); Set(t.X + 3, t.Y - 2, Tl.Tent); Set(t.X, t.Y + 3, Tl.Tent);
            AddPoi(PK.Camp, t, 4 + i); required.Add(t + new Vector2I(0, 2));
        }
        { var t = Pick(.2f, .8f, 2, 10); if (t.X >= 0) { Set(t.X, t.Y, Tl.Shrine); AddPoi(PK.Shrine, t); required.Add(t + new Vector2I(0, 1)); } }
        for (int i = 0; i < 2; i++) { var t = Pick(.15f, .9f, 2, 10); if (t.X >= 0) { Set(t.X, t.Y, Tl.Obelisk); AddPoi(PK.Obelisk, t); required.Add(t + new Vector2I(0, 1)); } }

        // префабы
        for (int i = 0; i < 3; i++) Stamp(r, Flip(r, i % 2 == 0 ? PuzzleA() : PuzzleB()), .25f, dist, dmax, required, PK.Puzzle);
        for (int i = 0; i < 2; i++) Stamp(r, Workshop(), .2f, dist, dmax, required, PK.Workshop);
        for (int i = 0; i < 2; i++) Stamp(r, BridgeSiteRows(), .15f, dist, dmax, required, PK.BridgeSite);
        for (int i = 0; i < 3; i++) Stamp(r, Stash(), .15f, dist, dmax, required, PK.Chest);

        // проверка проходимости обязательных целей
        required.Add(new Vector2I(245, 64));
        foreach (var t in required) MakeReachable(ent, t);
        // лес: проход к печати и фонарям
        var sp = Spawn;
        MakeReachable(sp, new Vector2I(120, 64));
        foreach (var a in Ancients) if (a.Region == 0 && a.Tile.X < 128) MakeReachable(sp, a.Tile + new Vector2I(0, a.Boss ? 3 : 2));
    }

    void VeinsDry(Random rng, Ancient an, Tl t, int count)
    {
        int placed = 0, tries = 0;
        while (placed < count && tries++ < 500)
        {
            double a = rng.NextDouble() * Math.PI * 2, d = 4 + rng.NextDouble() * 6;
            int x = an.Tile.X + (int)(Math.Cos(a) * d), y = an.Tile.Y + (int)(Math.Sin(a) * d);
            if (Get(x, y) != Tl.None || Ground[Key(x, y)] == Art.WaterG || an.Veins.Exists(v => v.x == x && v.y == y)) continue;
            an.Veins.Add((x, y, t)); placed++;
        }
    }

    // ───────────────────────── префабы ─────────────────────────
    static char[][] Room(int w, int h)
    {
        var g = new char[h][];
        for (int y = 0; y < h; y++) { g[y] = new char[w]; for (int x = 0; x < w; x++) g[y][x] = (x == 0 || y == 0 || x == w - 1 || y == h - 1) ? '#' : '.'; }
        return g;
    }
    static string[] ToRows(char[][] g) { var r = new string[g.Length]; for (int i = 0; i < g.Length; i++) r[i] = new string(g[i]); return r; }

    public static string[] PuzzleA()
    {
        var g = Room(19, 11);
        g[2][1] = '>'; g[2][4] = '/'; g[6][4] = '/'; g[6][12] = 'R';
        for (int x = 14; x <= 18; x++) { g[4][x] = '#'; g[8][x] = '#'; }
        for (int y = 4; y <= 8; y++) g[y][14] = '#';
        g[6][14] = 'D'; g[5][16] = 'C'; g[7][16] = 'C'; g[10][9] = 's';
        return ToRows(g);
    }
    public static string[] PuzzleB()
    {
        var g = Room(19, 11);
        g[2][1] = '>'; g[2][6] = '/'; g[6][6] = 'R';
        g[8][17] = '<'; g[8][11] = '/'; g[4][11] = 'R';
        for (int x = 13; x <= 18; x++) g[4][x] = '#';
        for (int y = 1; y <= 4; y++) g[y][13] = '#';
        g[4][15] = 'D'; g[2][15] = 'C'; g[10][9] = 's';
        return ToRows(g);
    }
    static string[] Workshop() => new[]
    {
        "###########", "#.........#", "#.M.....C.#", "#.........#", "#.........#", "#.........#", "#####s#####"
    };
    static string[] BridgeSiteRows() => new[]
    {
        "###############", "#....~~~~~....#", "#....~~~~~....#", "s....bbbbb..C.#", "#....~~~~~....#", "#....~~~~~....#", "###############"
    };
    static string[] Stash() => new[] { "#######", "#.C.C.#", "#.....#", "###x###" };

    static string[] Flip(Random r, string[] rows)
    {
        char Map(char c, bool h, bool v)
        {
            if ((h || v) && (c == '/' || c == '\\')) return c == '/' ? '\\' : '/';
            return c;
        }
        bool fh = r.Next(2) == 0, fv = r.Next(2) == 0;
        var o = new string[rows.Length];
        for (int y = 0; y < rows.Length; y++)
        {
            var src = rows[fv ? rows.Length - 1 - y : y].ToCharArray();
            if (fh) Array.Reverse(src);
            for (int x = 0; x < src.Length; x++)
            {
                char c = src[x];
                if (fh && c == '>') c = '<'; else if (fh && c == '<') c = '>';
                if (fv && c == '^') c = 'v'; else if (fv && c == 'v') c = '^';
                if (fh && fv) { /* два отражения: ориентация зеркал сохраняется */ }
                else if (c == '/' || c == '\\') c = (fh || fv) ? (c == '/' ? '\\' : '/') : c;
                src[x] = c;
            }
            o[y] = new string(src);
        }
        return o;
    }

    void Stamp(Random r, string[] rows, float minDistF, int[] dist, int dmax, List<Vector2I> required, PK kind, int x0 = MX0, int x1 = 238, int floorBase = Art.CaveFloor, Tl wallTile = Tl.CaveWall)
    {
        int h = rows.Length, wid = rows[0].Length;
        for (int tries = 0; tries < 600; tries++)
        {
            int ox = r.Next(x0 + 6, x1 - wid), oy = r.Next(5, Ht - 5 - h);
            bool ok = true;
            foreach (var tk in taken) { int m = tk.r + Math.Max(wid, h) / 2 + 2; if (Math.Abs(tk.p.X - (ox + wid / 2)) < m && Math.Abs(tk.p.Y - (oy + h / 2)) < m) { ok = false; break; } }
            if (!ok) continue;
            int openCnt = 0, minD = int.MaxValue;
            for (int y = 0; y < h; y++) for (int x = 0; x < wid; x++) { int d = dist[Key(ox + x, oy + y)]; if (d >= 0) { openCnt++; minD = Math.Min(minD, d); } }
            if (openCnt < wid * h * .12f || minD < minDistF * dmax) continue;
            taken.Add((new Vector2I(ox + wid / 2, oy + h / 2), Math.Max(wid, h) / 2 + 1));
            Puzzle pz = kind == PK.Puzzle ? new Puzzle() : null;
            Vector2I? access = null, bridge = null, machine = null;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < wid; x++)
                {
                    char c = rows[y][x]; int gx = ox + x, gy = oy + y, k = Key(gx, gy);
                    prot[k] = true; Ground[k] = (byte)(c == 'x' || c == 's' ? floorBase : floorBase + r.Next(3));
                    var tp = new Vector2I(gx, gy);
                    switch (c)
                    {
                        case '#': Set(gx, gy, wallTile); break;
                        case '.': Set(gx, gy, Tl.None); break;
                        case 's': Set(gx, gy, Tl.None); access = tp; break;
                        case 'C': Set(gx, gy, Tl.Chest); AddPoi(PK.Chest, tp, 0, 1); break;
                        case 'D': Set(gx, gy, Tl.SecretDoor); pz?.Doors.Add(tp); break;
                        case 'R': Set(gx, gy, Tl.Receiver); pz?.Receivers.Add(tp); break;
                        case '/': Set(gx, gy, Tl.Mirror1); pz?.Mirrors.Add(tp); break;
                        case '\\': Set(gx, gy, Tl.Mirror2); pz?.Mirrors.Add(tp); break;
                        case '>': case '<': case '^': case 'v':
                            Set(gx, gy, Tl.Emitter);
                            var dir = c == '>' ? new Vector2I(1, 0) : c == '<' ? new Vector2I(-1, 0) : c == '^' ? new Vector2I(0, -1) : new Vector2I(0, 1);
                            Emitters.Add((tp, dir)); pz?.Emitters.Add(tp); break;
                        case 'M': Set(gx, gy, Tl.Machine); machine = tp; break;
                        case '~': Set(gx, gy, Tl.Deep); break;
                        case 'b': Set(gx, gy, Tl.BridgeBroken); bridge ??= tp; break;
                        case 'x': Set(gx, gy, Tl.Rubble); access = tp; break;
                    }
                }
            if (kind == PK.Puzzle) { pz.Poi = AddPoi(PK.Puzzle, pz.Receivers[0]); Puzzles.Add(pz); }
            else if (kind == PK.Workshop) AddPoi(PK.Workshop, machine.Value);
            else if (kind == PK.BridgeSite) AddPoi(PK.BridgeSite, bridge.Value);
            if (access.HasValue)
            {
                var a = access.Value; Vector2I outside;
                if (a.Y == oy + h - 1) outside = a + new Vector2I(0, 1); else if (a.Y == oy) outside = a + new Vector2I(0, -1);
                else if (a.X == ox) outside = a + new Vector2I(-1, 0); else outside = a + new Vector2I(1, 0);
                required.Add(outside);
                if (!(kind == PK.Chest) || true) { /* внешний вход прорубается в MakeReachable, внутренняя часть защищена */ }
                // подготовить проходимую клетку снаружи
                if (!prot[Key(outside.X, outside.Y)] && Get(outside.X, outside.Y) != Tl.None) Set(outside.X, outside.Y, Tl.None);
            }
            return;
        }
    }
}
