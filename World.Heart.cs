using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

public sealed class Riddle { public List<Vector2I> Lanterns = new(); public int Real; public List<Vector2I> Gap = new(); public Poi Poi; }

public sealed partial class World
{
    public List<Riddle> Riddles = new();
    public List<Vector2I> FalseLights = new(), Traps = new();
    public Vector2I HeartArena = new(-1, -1);
    public List<Vector2I> ArenaPylons = new();
    public List<List<Vector2I>> HeartGaps = new();

    /// <summary>Сердце Тьмы: четыре парящих острова, разделённые пустотой; мосты света открываются загадками.</summary>
    void GenHeart(int seed)
    {
        var r = new Random(seed * 101 + 419);
        var nz = new FastNoiseLite { Seed = seed + 71, Frequency = .09f, NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex };
        for (int y = 0; y < Ht; y++) for (int x = HX0; x <= HX1; x++) { Obj[Key(x, y)] = (byte)Tl.Void; Ground[Key(x, y)] = (byte)Art.VoidG; }
        // парящие обломки как декор
        for (int i = 0; i < 160; i++) { int x = r.Next(HX0 + 2, HX1 - 1), y = r.Next(3, Ht - 3); if (Get(x, y) == Tl.Void) Set(x, y, Tl.Fragment); }
        int[] cx = { 525, 555, 585, 618 }; int[] rx = { 11, 11, 11, 14 }; int cy = 64;
        for (int k = 0; k < 4; k++)
            for (int y = 10; y < Ht - 10; y++)
                for (int x = cx[k] - rx[k] - 2; x <= cx[k] + rx[k] + 2; x++)
                {
                    float dx = (x - cx[k]) / (float)rx[k], dy = (y - cy) / (k == 3 ? 26f : 22f);
                    if (dx * dx + dy * dy + nz.GetNoise2D(x, y) * .35f < 1f) { Set(x, y, Tl.None); Ground[Key(x, y)] = (byte)(Art.HeartFloor + r.Next(3)); }
                }
        for (int y = 61; y <= 67; y++) for (int x = HX0; x <= cx[0]; x++) { Set(x, y, Tl.None); Ground[Key(x, y)] = (byte)(Art.HeartFloor + r.Next(3)); }
        // проходы между островами (по линии y=64)
        var gaps = new List<List<Vector2I>>();
        for (int x = HX0; x < HX1; x++)
        {
            if (Get(x, 64) != Tl.Void && Get(x, 64) != Tl.Fragment) continue;
            int x0 = x; while (x < HX1 && (Get(x, 64) == Tl.Void || Get(x, 64) == Tl.Fragment)) x++;
            if (x0 > HX0 + 4 && x < HX1 - 2 && Get(x, 64) == Tl.None)
            {
                var g = new List<Vector2I>(); for (int gx = x0; gx < x; gx++) for (int gy = 63; gy <= 65; gy++) g.Add(new Vector2I(gx, gy));
                gaps.Add(g);
            }
        }
        foreach (var g in gaps) foreach (var t in g) Set(t.X, t.Y, Tl.Void);
        HeartGaps = gaps;

        // фиктивное поле расстояний: любая клетка острова считается допустимой для префабов
        var distAll = new int[Wd * Ht]; Array.Fill(distAll, -1);
        for (int y = 0; y < Ht; y++) for (int x = HX0; x <= HX1; x++) if (Get(x, y) == Tl.None) distAll[Key(x, y)] = 100;
        var starts = new[] { new Vector2I(HX0 + 8, 64), new Vector2I(cx[1] - rx[1] + 2, 64), new Vector2I(cx[2] - rx[2] + 2, 64), new Vector2I(cx[3] - rx[3] + 2, 64) };
        var req = new List<Vector2I>(); var kit = new RegionKit(this, r, HX0, HX1, starts[0]);
        void Line(Tl t, Vector2I a) => Set(a.X, a.Y, t);

        // ── мост 1: луч и зеркала на первом острове ──
        if (gaps.Count > 0)
        {
            var e = new Vector2I(cx[0] - 7, cy - 6); var m1 = new Vector2I(cx[0] + 1, cy - 6); var m2 = new Vector2I(cx[0] + 1, cy + 5); var rc = new Vector2I(cx[0] + 8, cy + 5);
            Line(Tl.Emitter, e); Emitters.Add((e, new Vector2I(1, 0))); Line(Tl.Mirror1, m1); Line(Tl.Mirror1, m2); Line(Tl.Receiver, rc);
            for (int x = e.X + 1; x < m1.X; x++) Set(x, e.Y, Tl.None);
            for (int y = m1.Y + 1; y < m2.Y; y++) Set(m1.X, y, Tl.None);
            for (int x = m2.X + 1; x < rc.X; x++) Set(x, m2.Y, Tl.None);
            var pz = new Puzzle { Bridge = true };
            pz.Emitters.Add(e); pz.Mirrors.Add(m1); pz.Mirrors.Add(m2); pz.Receivers.Add(rc); pz.Doors.AddRange(gaps[0]);
            var poi = AddPoi(PK.Puzzle, rc); poi.Tag = "bridge"; pz.Poi = poi; Puzzles.Add(pz);
            req.Add(e + new Vector2I(0, 1)); req.Add(m1 + new Vector2I(0, 0) + new Vector2I(1, 0)); req.Add(rc + new Vector2I(0, 1));
            taken.Add((new Vector2I(cx[0], cy), 12));
        }
        // ── мост 2: два приёмника; один подсвечен постоянным излучателем, второй нужно зажечь своим лучом ──
        if (gaps.Count > 1)
        {
            var r1 = new Vector2I(cx[1], cy - 9); var r2 = new Vector2I(cx[1] + 8, cy + 3); var e2 = new Vector2I(cx[1] + 8, cy + 10);
            Line(Tl.Receiver, r1); Line(Tl.Receiver, r2); Line(Tl.Emitter, e2); Emitters.Add((e2, new Vector2I(0, -1)));
            for (int y = r2.Y + 1; y < e2.Y; y++) Set(r2.X, y, Tl.None);
            var pz = new Puzzle { Bridge = true }; pz.Receivers.Add(r1); pz.Receivers.Add(r2); pz.Emitters.Add(e2); pz.Doors.AddRange(gaps[1]);
            var poi = AddPoi(PK.Puzzle, r1); poi.Tag = "bridge"; pz.Poi = poi; Puzzles.Add(pz);
            req.Add(new Vector2I(cx[1], cy - 6)); req.Add(new Vector2I(cx[1] + 5, cy + 3)); req.Add(e2 + new Vector2I(-1, 0));
            taken.Add((new Vector2I(cx[1], cy), 12));
        }
        // ── мост 3: настоящий среди ложных фонарей ──
        if (gaps.Count > 2)
        {
            var rd = new Riddle { Gap = gaps[2], Real = r.Next(5) };
            for (int i = 0; i < 5; i++) { var p = new Vector2I(cx[2] - 8 + i * 4, cy - 12); Line(Tl.FalseLantern, p); rd.Lanterns.Add(p); req.Add(p + new Vector2I(0, 1)); }
            var poi = AddPoi(PK.Puzzle, rd.Lanterns[0]); poi.Tag = "riddle"; rd.Poi = poi; Riddles.Add(rd);
            taken.Add((new Vector2I(cx[2], cy - 10), 10));
        }
        taken.Add((new Vector2I(cx[3], cy), 14));
        // ── арена финального босса ──
        {
            var a = new Vector2I(cx[3], cy); HeartArena = a; Set(a.X, a.Y, Tl.BossOff);
            foreach (var o in new[] { new Vector2I(-9, 0), new Vector2I(9, 0), new Vector2I(0, -9), new Vector2I(0, 9) }) { var p = a + o; Set(p.X, p.Y, Tl.Pylon); ArenaPylons.Add(p); }
            for (int i = 0; i < 8; i++) { double ang = i * Math.PI / 4 + .39; var p = new Vector2I(a.X + (int)Math.Round(Math.Cos(ang) * 12), a.Y + (int)Math.Round(Math.Sin(ang) * 12)); if (Get(p.X, p.Y) == Tl.None) Set(p.X, p.Y, Tl.Monolith); }
            var an = new Ancient { Tile = a, RadiusTiles = 16, Cost = 0, Boss = true, BossId = 5, Region = 4 };
            Ancients.Add(an); req.Add(a + new Vector2I(0, 3));
        }
        // ── места на островах ──
        taken.AddRange(kit.Taken);
        for (int k = 0; k < 3; k++)
        {
            var kt = new RegionKit(this, r, cx[k] - rx[k] - 2, cx[k] + rx[k] + 2, starts[k]); kt.Taken.AddRange(taken);
            // острова, не связанные мостами, считаем доступными по «бумажному» расстоянию
            for (int y = 0; y < Ht; y++) for (int x = kt.X0; x <= kt.X1; x++) kt.Dist[Key(x, y)] = Get(x, y) == Tl.None ? 50 : -1; kt.DMax = 50;
            for (int i = 0; i < 3; i++) { var t = kt.Pick(.1f, 1f, 1, 7); if (t.X < 0) continue; Set(t.X, t.Y, Tl.Chest); AddPoi(PK.Chest, t); req.Add(t + new Vector2I(0, 1)); }
            if (k >= 1) { var t = kt.Pick(.1f, 1f, 4, 9); if (t.X >= 0) { kt.Floor(t.X, t.Y, 4, Art.HeartFloor); Set(t.X, t.Y, Tl.Chest); Set(t.X - 3, t.Y - 2, Tl.Tent); Set(t.X + 3, t.Y - 2, Tl.Tent); AddPoi(PK.Camp, t, 5 + k); req.Add(t + new Vector2I(0, 2)); } }
            if (k == 0) { var t = kt.Pick(.1f, 1f, 2, 8); if (t.X >= 0) { Set(t.X, t.Y, Tl.Obelisk); AddPoi(PK.Obelisk, t); req.Add(t + new Vector2I(0, 1)); } }
            if (k == 1) { var t = kt.Pick(.1f, 1f, 2, 8); if (t.X >= 0) { Set(t.X, t.Y, Tl.Shrine); AddPoi(PK.Shrine, t); req.Add(t + new Vector2I(0, 1)); } }
            for (int i = 0; i < 6; i++) { var t = kt.Pick(.1f, 1f, 1, 5); if (t.X >= 0) Set(t.X, t.Y, Tl.Monolith); }
            if (k >= 1)
            {
                for (int i = 0; i < 5; i++) { var t = kt.Pick(.1f, 1f, 1, 7); if (t.X >= 0) { Set(t.X, t.Y, Tl.FalseLantern); FalseLights.Add(t); } }
                for (int i = 0; i < 10; i++) { var t = kt.Pick(.1f, 1f, 1, 3); if (t.X >= 0) Traps.Add(t); }
            }
            taken.AddRange(kt.Taken.GetRange(taken.Count, Math.Max(0, kt.Taken.Count - taken.Count)));
        }
        // прототипы строений неизвестного происхождения
        Stamp(r, Workshop(), .1f, distAll, 100, req, PK.Workshop, cx[1] - rx[1], cx[1] + rx[1], Art.HeartFloor, Tl.Monolith);
        Stamp(r, Stash(), .1f, distAll, 100, req, PK.Chest, cx[2] - rx[2], cx[2] + rx[2], Art.HeartFloor, Tl.Monolith);
        // достижимость внутри островов (мосты закрыты)
        for (int i = 0; i < req.Count; i++)
        {
            int k = 0; for (int j = 0; j < 4; j++) if (req[i].X >= cx[j] - rx[j] - 2) k = j;
            if (req[i].X < cx[0]) k = 0;
            MakeReachable(starts[k], req[i]);
        }
        Traps.RemoveAll(t => Get(t.X, t.Y) != Tl.None);
    }

    /// <summary>Страховка генерации: любой древний фонарь (включая боссов) достижим пешком от старта при открытых печатях и мостах.
    /// Прорубает туннель только там, где путь отсутствует, поэтому нормальные миры и старые сохранения не меняются.</summary>
    public int EnsureStoryReachable()
    {
        var saved = new List<(int k, byte v)>();
        void Open(IEnumerable<Vector2I> ts, Tl to) { foreach (var t in ts) { int k = Key(t.X, t.Y); saved.Add((k, Obj[k])); Obj[k] = (byte)to; } }
        Open(Seal1, Tl.None); Open(Seal2, Tl.None); Open(Seal3, Tl.None); Open(Seal4, Tl.None);
        foreach (var g in HeartGaps) Open(g, Tl.LightBridge);
        int fixedN = 0;
        foreach (var a in Ancients)
        {
            var tt = a.Tile + new Vector2I(0, a.Boss ? 3 : 2);
            if (Reach(Spawn)[Key(tt.X, tt.Y)]) continue;
            if (MakeReachable(Spawn, tt)) fixedN++;
        }
        for (int i = saved.Count - 1; i >= 0; i--) Obj[saved[i].k] = saved[i].v;
        return fixedN;
    }
}
