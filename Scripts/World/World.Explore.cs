using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Вид древнего фонаря: обычный требует только искры света, остальные добавляют небольшое действие рядом.</summary>
public enum LampKind : byte { Normal, Guarded, Damaged, Infected, Mystery }

public sealed partial class World
{
    /// <summary>Версия генерации: 0 — миры и сохранения до «живого мира» (ничего нового не добавляется, чтобы не ломать старые миры), 1 — природные области, новые места и виды фонарей, 2 — загадочный фонарь, алтарь искр и скрытые тайники, 3 — обелиск рун, древо-хранитель, озёра и ручьи, места во всех биомах, 4 — выжившие (жители) в местах интереса.</summary>
    public const int CurrentGen = 4;
    public int GenV = CurrentGen;

    /// <summary>Отдельный проход после всей старой генерации и со своим генератором случайных чисел: порядок и результаты прежних проходов не меняются.</summary>
    void GenExplore(int seed)
    {
        var r = new Random(seed * 211 + 59);
        var prot = new bool[Wd * Ht];
        void Guard(Vector2I c, int rad) { for (int y = c.Y - rad; y <= c.Y + rad; y++) for (int x = c.X - rad; x <= c.X + rad; x++) if (In(x, y)) prot[Key(x, y)] = true; }
        Guard(Spawn, 8); foreach (var a in Ancients) Guard(a.Tile, 7); foreach (var p in Pois) Guard(p.Tile, 6);
        ShapeForest(seed, r, prot);
        Roads(r);
        AssignLampKinds(r);
        PlaceDefs(r);
        if (GenV >= 3) Waters(seed, r);
    }

    // ───────────────────────── озёра и ручьи ─────────────────────────
    /// <summary>Пруды и один ручей в лесу: вода проходима (замедляет), поэтому достижимость мест не страдает. Не трогает окрестности мест, фонарей и маяка.</summary>
    void Waters(int seed, Random r)
    {
        var prot = new bool[Wd * Ht];
        void Guard(Vector2I c, int rad) { for (int y = c.Y - rad; y <= c.Y + rad; y++) for (int x = c.X - rad; x <= c.X + rad; x++) if (In(x, y)) prot[Key(x, y)] = true; }
        Guard(Spawn, 10); foreach (var a in Ancients) Guard(a.Tile, 8); foreach (var p in Pois) Guard(p.Tile, 7);
        int placed = 0;
        void Put(int x, int y)
        {
            if (x < 4 || y < 4 || x >= N - 4 || y >= Ht - 4 || prot[Key(x, y)]) return;
            var t = Get(x, y); if (t != Tl.None && t != Tl.Tree && t != Tl.Stone && t != Tl.Pine && t != Tl.DeadTree) return;
            Obj[Key(x, y)] = (byte)Tl.None; Ground[Key(x, y)] = Art.WaterG; placed++;
        }
        int c = N / 2;
        for (int n = 0; n < 3; n++)
            for (int attempt = 0; attempt < 8 && placed < 14 * (n + 1); attempt++)       // пока пруд не набрал хотя бы ~14 клеток, пробуем другое место
            {
                double ang = r.NextDouble() * Math.PI * 2, d = 16 + r.NextDouble() * 34; int cx = c + (int)(Math.Cos(ang) * d), cy = c + (int)(Math.Sin(ang) * d);
                double rx = 3 + r.NextDouble() * 3, ry = 2.5 + r.NextDouble() * 2.5; var wob = new FastNoiseLite { Seed = seed + 300 + n * 10 + attempt, Frequency = .22f };
                for (int y = cy - 7; y <= cy + 7; y++) for (int x = cx - 8; x <= cx + 8; x++)
                { double e = (x - cx) * (x - cx) / (rx * rx) + (y - cy) * (y - cy) / (ry * ry); if (e + wob.GetNoise2D(x, y) * .5 < 1) Put(x, y); }
            }
        // ручей: извилистая линия поперёк леса
        { double px = 6 + r.NextDouble() * 20, py = 8 + r.NextDouble() * (N - 16), dir = r.NextDouble() < .5 ? 0 : Math.PI, ph = r.NextDouble() * 6;
          for (int s = 0; s < 110; s++) { ph += .25; double a = dir + Math.Sin(ph) * .8 + (r.NextDouble() - .5) * .3; px += Math.Cos(a) * .9 + (dir == 0 ? .35 : -.35); py += Math.Sin(a) * .9 + (r.NextDouble() - .5) * .4; Put((int)px, (int)py); if (s % 3 == 0) Put((int)px + 1, (int)py); } }
    }

    // ───────────────────────── природные области ─────────────────────────
    /// <summary>Поляны, густые рощи, каменистые участки и редколесье по шуму вместо равномерной россыпи. Обязательные места и их окрестности не трогаются.</summary>
    void ShapeForest(int seed, Random r, bool[] prot)
    {
        var clearing = new FastNoiseLite { Seed = seed + 101, Frequency = .034f, NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex };
        var dense = new FastNoiseLite { Seed = seed + 103, Frequency = .07f, NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex };
        var rocky = new FastNoiseLite { Seed = seed + 107, Frequency = .055f, NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex };
        for (int y = 4; y < N - 4; y++)
            for (int x = 4; x < N - 4; x++)
            {
                int k = Key(x, y); if (prot[k]) continue;
                var t = (Tl)Obj[k]; if (t == Tl.Cliff || (t != Tl.None && t != Tl.Tree && t != Tl.Stone)) continue;
                float c = clearing.GetNoise2D(x, y), d = dense.GetNoise2D(x, y), ro = rocky.GetNoise2D(x, y); double q = r.NextDouble();
                if (c > .36f) { if (t == Tl.Tree || (t == Tl.Stone && q < .5)) Obj[k] = (byte)Tl.None; }                              // поляна
                else if (ro > .4f) { if (t == Tl.Tree && q < .75) Obj[k] = (byte)Tl.None; else if (t == Tl.None && q < .2) Obj[k] = (byte)Tl.Stone; }   // каменистый участок
                else if (d > .28f) { if (t == Tl.None && q < .32) Obj[k] = (byte)Tl.Tree; }                                           // густая роща
                else if (d < -.3f) { if (t == Tl.Tree && q < .55) Obj[k] = (byte)Tl.None; }                                           // редколесье
            }
    }

    /// <summary>Заброшенные дороги: извилистые полосы утоптанной земли от маяка к первым фонарям. Деревья и камни на них убираются, проход остаётся свободным.</summary>
    void Roads(Random r)
    {
        for (int i = 1; i <= 2 && i < Ancients.Count; i++)
        {
            var a = Ancients[i].Tile; double px = Spawn.X, py = Spawn.Y, ph = r.NextDouble() * 6.28;
            for (int s = 0; s < 400; s++)
            {
                double dx = a.X - px, dy = a.Y - py, len = Math.Sqrt(dx * dx + dy * dy); if (len < 3) break;
                ph += .35; double wob = Math.Sin(ph) * .5;
                px += dx / len + (-dy / len) * wob; py += dy / len + (dx / len) * wob;
                int x = (int)Math.Round(px), y = (int)Math.Round(py); if (!In(x, y)) break;
                for (int oy = 0; oy <= 1; oy++) for (int ox = 0; ox <= 1; ox++)
                {
                    int k = Key(x + ox, y + oy); if (!In(x + ox, y + oy)) continue;
                    var t = (Tl)Obj[k]; if (t == Tl.Tree || t == Tl.Stone) Obj[k] = (byte)Tl.None;
                    if (Obj[k] == (byte)Tl.None && Ground[k] != Art.WaterG) Ground[k] = Art.Dirt;
                }
            }
        }
    }

    // ───────────────────────── виды древних фонарей ─────────────────────────
    /// <summary>Первый фонарь остаётся обычным: игрок сразу чувствует прогресс. Остальные четыре получают разные виды в порядке, зависящем от мира.</summary>
    void AssignLampKinds(Random r)
    {
        var kinds = new List<LampKind> { LampKind.Guarded, LampKind.Damaged, LampKind.Infected, GenV >= 2 ? LampKind.Mystery : LampKind.Normal };
        for (int i = kinds.Count - 1; i > 0; i--) { int j = r.Next(i + 1); (kinds[i], kinds[j]) = (kinds[j], kinds[i]); }
        for (int i = 2, n = 0; i <= 5 && i < Ancients.Count; i++, n++)
        {
            var a = Ancients[i]; if (a.Boss || a.Region != 0) continue; a.Kind = kinds[n % kinds.Count];
            if (a.Kind == LampKind.Mystery && !BuildMystery(a)) a.Kind = LampKind.Normal;
            if (a.Kind == LampKind.Infected) { var t = a.Tile + new Vector2I(2, -1); if (Get(t.X, t.Y) == Tl.None) { Set(t.X, t.Y, Tl.ShadowPillar); a.Crystal = t; } else a.Kind = LampKind.Normal; }
        }
    }

    // ───────────────────────── новые места интереса ─────────────────────────
    void PlaceDefs(Random r)
    {
        int c = N / 2;
        bool Far(Vector2I t, int d)
        {
            foreach (var p in Pois) if ((p.Tile.X - t.X) * (p.Tile.X - t.X) + (p.Tile.Y - t.Y) * (p.Tile.Y - t.Y) < d * d) return false;
            foreach (var a in Ancients) if ((a.Tile.X - t.X) * (a.Tile.X - t.X) + (a.Tile.Y - t.Y) * (a.Tile.Y - t.Y) < 100) return false;
            return true;
        }
        foreach (var def in PoiDefs.All)
        {
            if (GenV < def.MinGen) continue;
            if (def.Region != 0) { PlaceInRegion(def, r, Far); continue; }
            for (int n = 0; n < def.Count; n++)
                for (int tries = 0; tries < 1200; tries++)
                {
                    int dmin = tries < 350 ? def.MinFromSpawn : def.MinFromSpawn - 10;       // если подходящего места нет, ближе к маяку тоже можно
                    double ang = r.NextDouble() * Math.PI * 2, d = dmin + r.NextDouble() * (def.MaxFromSpawn - dmin);
                    int x = c + (int)(Math.Cos(ang) * d), y = c + (int)(Math.Sin(ang) * d), rad = def.Clear + 2;
                    if (x < rad + 6 || y < rad + 6 || x > N - rad - 7 || y > N - rad - 7) continue;
                    var t = new Vector2I(x, y); if (!Far(t, tries < 250 ? def.Spacing : tries < 800 ? def.Spacing - 2 : Math.Max(def.Spacing - 4, 9))) continue;
                    var snap = Snapshot(x, y, rad); int poiN = Pois.Count;
                    var poi = Build(def, t, r);
                    var access = Access(def, t);
                    if (poi != null && Reach(Spawn)[Key(access.X, access.Y)]) break;
                    Restore(snap, x, y, rad); while (Pois.Count > poiN) { var last = Pois[^1]; ByTile.Remove(Key(last.Tile.X, last.Tile.Y)); Pois.RemoveAt(Pois.Count - 1); }
                }
        }
    }

    /// <summary>Клетка у входа в место, до которой игрок обязан иметь возможность дойти.</summary>
    /// <summary>Места в шахтах, пустошах и монастыре: случайная точка внутри биома, на свободной достижимой площадке без воды.</summary>
    void PlaceInRegion(PoiDef def, Random r, Func<Vector2I, int, bool> far)
    {
        int reg = def.Region, x0 = reg == 1 ? MX0 : reg == 2 ? AX0 : IX0, rad = def.Clear + 2; var reach = Reach(RegionStart(Spawn, reg));
        if (!reach[Key(RegionStart(Spawn, reg).X, RegionStart(Spawn, reg).Y)]) foreach (var a in Ancients) if (a.Region == reg && !a.Boss) { var st = a.Tile + new Vector2I(0, 2); if (Walkable(st.X, st.Y)) { reach = Reach(st); break; } }
        for (int n = 0; n < def.Count; n++)
            for (int tries = 0; tries < 1500; tries++)
            {
                int x = r.Next(x0 + rad + 6, x0 + 127 - rad - 6), y = r.Next(rad + 6, Ht - rad - 7); var t = new Vector2I(x, y); if (!reach[Key(x, y)]) continue;
                var acc = Access(def, t); if (!reach[Key(x, y)] || !reach[Key(acc.X, acc.Y)]) continue;
                if (!far(t, tries < 600 ? def.Spacing : Math.Max(def.Spacing - 4, 9))) continue;
                bool ok = true;
                for (int yy = y - rad; yy <= y + rad && ok; yy++) for (int xx = x - rad; xx <= x + rad; xx++)
                { var o = Get(xx, yy); var g = Ground[Key(xx, yy)]; if ((o != Tl.None && o != Tl.Tree && o != Tl.Stone && o != Tl.Pine && o != Tl.DeadTree && o != Tl.CaveWall) || g == Art.WaterG || g == Art.HotAsh || g == Art.VoidG) { ok = false; break; } }
                if (!ok) continue;
                if (Build(def, t, r) != null) break;
            }
    }

    /// <summary>Загадочный фонарь: три светила вокруг (запад, север, восток) и записка с порядком касания. Порядок задан миром, одинаков у всех игроков.</summary>
    bool BuildMystery(Ancient a)
    {
        var offs = new[] { new Vector2I(-3, 0), new Vector2I(0, -3), new Vector2I(3, 0) }; var tab = a.Tile + new Vector2I(-2, 2);
        foreach (var o in offs) { var t = a.Tile + o; var g = Get(t.X, t.Y); if (g != Tl.None && g != Tl.Tree && g != Tl.Stone) return false; }
        var tg = Get(tab.X, tab.Y); if (tg != Tl.None && tg != Tl.Tree && tg != Tl.Stone) return false;
        var r = new Random(Seed * 131 + a.Tile.X * 17 + a.Tile.Y); var seq = new[] { 0, 1, 2 };
        for (int i = 2; i > 0; i--) { int j = r.Next(i + 1); (seq[i], seq[j]) = (seq[j], seq[i]); }
        if (seq[0] == 0 && seq[1] == 1) { seq[1] = 2; seq[2] = 1; }       // порядок «слева направо» слишком очевиден
        a.Pylons = new Vector2I[3]; for (int i = 0; i < 3; i++) { a.Pylons[i] = a.Tile + offs[i]; Set(a.Pylons[i].X, a.Pylons[i].Y, Tl.Pylon); }
        a.Seq = seq; a.Tablet = tab; Set(tab.X, tab.Y, Tl.Tablet); return true;
    }

    /// <summary>Светила обелиска рун: север, восток, юг, запад от обелиска.</summary>
    public static readonly Vector2I[] RuneOffs = { new(0, -3), new(3, 0), new(0, 3), new(-3, 0) };

    static Vector2I Access(PoiDef def, Vector2I t) => (PK)def.Kind == PK.Vault || ((PK)def.Kind == PK.Survivor && def.Variant == 3) ? new Vector2I(t.X, t.Y + 3) : new Vector2I(t.X, t.Y + (def.Kind == (int)PK.Tower || def.Kind == (int)PK.GiantTree ? 2 : 1));

    byte[] Snapshot(int x, int y, int rad)
    {
        int w = rad * 2 + 1; var s = new byte[w * w * 2];
        for (int j = 0; j < w; j++) for (int i = 0; i < w; i++) { int xx = x - rad + i, yy = y - rad + j; if (!In(xx, yy)) continue; s[(j * w + i) * 2] = Obj[Key(xx, yy)]; s[(j * w + i) * 2 + 1] = Ground[Key(xx, yy)]; }
        return s;
    }

    void Restore(byte[] s, int x, int y, int rad)
    {
        int w = rad * 2 + 1;
        for (int j = 0; j < w; j++) for (int i = 0; i < w; i++) { int xx = x - rad + i, yy = y - rad + j; if (!In(xx, yy)) continue; Obj[Key(xx, yy)] = s[(j * w + i) * 2]; Ground[Key(xx, yy)] = s[(j * w + i) * 2 + 1]; }
    }

    Poi Build(PoiDef def, Vector2I t, Random r)
    {
        int x = t.X, y = t.Y, rad = def.Clear;
        for (int yy = y - rad; yy <= y + rad; yy++)
            for (int xx = x - rad; xx <= x + rad; xx++)
            {
                if (!In(xx, yy)) return null;
                var o = Get(xx, yy); if (o == Tl.Cliff) return null;
                if ((xx - x) * (xx - x) + (yy - y) * (yy - y) <= rad * rad + 1) Set(xx, yy, Tl.None);
            }
        string tag = def.Reward == "" ? null : def.Reward;
        switch ((PK)def.Kind)
        {
            case PK.Tower:
            {
                Set(x, y - 3, Tl.Monolith); Set(x, y - 2, Tl.Monolith); Set(x - 1, y - 1, Tl.Ruin); Set(x + 1, y - 1, Tl.Ruin); Set(x - 2, y - 2, Tl.Ruin); Set(x + 2, y - 2, Tl.Ruin);
                var ct = new Vector2I(x, y + 1); Set(ct.X, ct.Y, Tl.Chest);
                var p = AddPoi(PK.Tower, ct, def.Guards, (byte)def.Variant); p.Tag = tag; return p;
            }
            case PK.Camp:
            {
                Set(x, y, Tl.Chest); Set(x - 3, y - 2, Tl.Tent); Set(x + 3, y - 2, Tl.Tent); Set(x - 3, y + 2, Tl.Tent); Set(x + 3, y + 2, Tl.Tent); Set(x, y + 3, Tl.Ruin);
                var p = AddPoi(PK.Camp, t, def.Guards, (byte)def.Variant); p.Tag = tag; return p;
            }
            case PK.Vault:
            {
                for (int yy = y - 2; yy <= y + 2; yy++) for (int xx = x - 2; xx <= x + 2; xx++) if (Math.Abs(xx - x) == 2 || Math.Abs(yy - y) == 2) Set(xx, yy, Tl.Ruin);
                Set(x, y + 2, Tl.Rubble); Set(x, y, Tl.Chest);
                var p = AddPoi(PK.Vault, t, 0, (byte)def.Variant); p.Tag = tag; return p;
            }
            case PK.Secret:
            {
                Set(x, y, Tl.SecretCache); var p = AddPoi(PK.Secret, t, 0, (byte)def.Variant); p.Tag = tag; return p;
            }
            case PK.SparkAltar:
            {
                Set(x, y, Tl.Altar); Set(x - 2, y - 1, Tl.Ruin); Set(x + 2, y - 1, Tl.Ruin); Set(x, y - 2, Tl.Ruin);
                var p = AddPoi(PK.SparkAltar, t, 0, (byte)def.Variant); p.Tag = tag; return p;
            }
            case PK.RuneObelisk:
            {
                Set(x, y, Tl.Obelisk); foreach (var o in RuneOffs) Set(x + o.X, y + o.Y, Tl.Pylon);
                var p = AddPoi(PK.RuneObelisk, t, 0, (byte)def.Variant); p.Tag = tag; return p;
            }
            case PK.Survivor:
            {
                if (def.Variant == 3) { for (int yy = y - 2; yy <= y + 2; yy++) for (int xx = x - 2; xx <= x + 2; xx++) if (Math.Abs(xx - x) == 2 || Math.Abs(yy - y) == 2) Set(xx, yy, Tl.Ruin); Set(x, y + 2, Tl.Rubble); }
                Set(x, y, def.Variant == 1 ? Tl.House : Tl.Tent); var p = AddPoi(PK.Survivor, t, 0, (byte)def.Variant); p.Tag = tag; return p;
            }
            case PK.GiantTree:
            {
                for (int i = -1; i <= 1; i++) Set(x + i, y, Tl.Cliff);
                var ct = new Vector2I(x, y + 1); Set(ct.X, ct.Y, Tl.Shrine);
                var p = AddPoi(PK.GiantTree, ct, 0, (byte)def.Variant); p.Tag = tag; return p;
            }
            case PK.Glade:
            {
                int lum = 0, aeth = 0;
                for (int k = 0; k < 40 && (lum < 3 || aeth < 2); k++)
                {
                    int xx = x + r.Next(-2, 3), yy = y + r.Next(-2, 3); if (Get(xx, yy) != Tl.None || (xx == x && yy == y)) continue;
                    if (lum < 3) { Set(xx, yy, Tl.Lumen); lum++; } else { Set(xx, yy, Tl.Aether); aeth++; }
                }
                return AddPoi(PK.Glade, t, r.Next(2) * def.Guards, (byte)def.Variant);
            }
        }
        return null;
    }
}
