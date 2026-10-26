using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

public sealed class Lamp
{
    public int X, Y, Kind, Level = 1, Dir; public float Anim;
    public Vector2 Pos => Game.TileCenter(X, Y);
}

public sealed class BeamPath { public List<Vector2> Pts = new(); public float Dps; public Color C; }

public partial class Game
{
    public List<Lamp> Lamps = new();
    public const int MaxLamps = 40;
    public static readonly string[] LampNames = { "Фонарь", "Боевой фонарь", "Кристальный фонарь" };
    static readonly int[][][] LampCost = { CostLantern, C(It.Wood, 3, It.Silver, 2, It.Iron, 2), C(It.Crystal, 3, It.Silver, 2, It.Copper, 2) };
    static readonly int[][] MirrorCost = C(It.Crystal, 2);
    public static readonly Vector2I[] Dirs = { new(1, 0), new(0, 1), new(-1, 0), new(0, -1) };
    public static int[][] LampCostOf(int kind) => LampCost[kind];
    public static int[][] MirrorCostArr => MirrorCost;

    public float LightBlockT;
    public List<BeamPath> Beams = new();
    public List<Vector2> BeamLights = new();
    public float BeamE = 100; public bool BeamOn, beamLock; float beamSfxT, puzzleT;

    public float LampRad(Lamp l)
    {
        float b = l.Kind == 0 ? 96 : l.Kind == 1 ? 72 : 64;
        float lv = l.Level == 1 ? 1f : l.Level == 2 ? 1.35f : 1.7f;
        float f = b * lv * (Has(Tech.FarLanterns) ? 1.5f : 1f) * (Has(Tech.FireLens) ? 1.3f : 1f);
        f *= (LightBlockT > 0 ? .4f : 1f) * (1f - .55f * SmokeAt(l.Pos));
        return f * Ease(l.Anim);
    }

    // ───────────────────────── строительство фонарей ─────────────────────────
    void BuildLamp(int kind)
    {
        if (kind == 1 && !Has(Tech.CombatLamp)) { Fail("Боевой фонарь нужно изучить [T] — вкладка «Глубины»"); return; }
        if (kind == 2 && !Has(Tech.Optics)) { Fail("Кристальный фонарь нужно изучить [T] — «Кристальная оптика»"); return; }
        var cost = LampCost[kind];
        if (!Can(cost)) { Fail($"{LampNames[kind]}: нужно {CostStr(cost)}"); return; }
        if (Lamps.Count >= MaxLamps) { Fail("Достигнут предел фонарей"); return; }
        var t = PlaceTarget(); if (t == null) return;
        Pay(cost);
        SetObj(t.Value.X, t.Value.Y, kind == 0 ? Tl.Lantern : kind == 1 ? Tl.CombatLantern : Tl.CrystalLantern);
        Lamps.Add(new Lamp { X = t.Value.X, Y = t.Value.Y, Kind = kind, Anim = 0 });
        Burst(TileCenter(t.Value.X, t.Value.Y), kind == 1 ? new Color("#ff7a3a") : kind == 2 ? new Color("#bfe9ff") : new Color("#ffd86b"), 14, 70);
        Sfx.Play("build"); GiveXp(25); Save();
        if (kind == 2) Toast("Кристальный фонарь поставлен: E — повернуть луч. Зеркала [0] перенаправляют его");
    }

    void BuildMirror()
    {
        if (!Has(Tech.Optics)) { Fail("Зеркала нужно изучить [T] — «Кристальная оптика»"); return; }
        if (!Can(MirrorCost)) { Fail($"Зеркало: нужно {CostStr(MirrorCost)}"); return; }
        var t = PlaceTarget(); if (t == null) return;
        Pay(MirrorCost); SetObj(t.Value.X, t.Value.Y, Tl.Mirror1);
        Burst(TileCenter(t.Value.X, t.Value.Y), new Color("#bfe9ff"), 10, 50); Sfx.Play("build", -2); GiveXp(5);
    }

    static int[][] LampUpCost(int toLevel) => toLevel == 2 ? C(It.Copper, 3, It.Shard, 2) : C(It.Silver, 2, It.Crystal, 2);

    void UpgradeLamp(Lamp l)
    {
        if (l.Level >= 3) { Fail("Фонарь уже на максимальном уровне"); return; }
        var cost = LampUpCost(l.Level + 1);
        if (!Can(cost)) { Fail($"Улучшение фонаря: нужно {NeedStr(cost)}"); return; }
        Pay(cost); l.Level++; l.Anim = .5f;
        Burst(l.Pos, new Color("#ffe27a"), 16, 80); Sfx.Play("upgrade"); GiveXp(20);
        Announce($"{LampNames[l.Kind]}: уровень {l.Level}", l.Kind == 2 ? "Луч длиннее и мощнее, свет шире" : l.Kind == 1 ? "Сильнее жжёт врагов, свет шире" : "Радиус света заметно больше", new Color("#ffe27a"), "upgrade");
        Save();
    }

    Lamp LampNear(Vector2 p, float r)
    {
        Lamp best = null; float bd = r;
        foreach (var l in Lamps) { float d = l.Pos.DistanceTo(p); if (d < bd) { bd = d; best = l; } }
        return best;
    }

    void LampIx(Lamp l, Ix ix)
    {
        ix.Lines.Add($"{LampNames[l.Kind]} ур.{l.Level}" + (l.Kind == 2 ? $" · луч {(new[] { "→", "↓", "←", "↑" })[l.Dir]}" : l.Kind == 1 ? " · жжёт врагов рядом" : ""));
        if (l.Kind == 2) { ix.Lines.Add("[E] Повернуть луч (90°)"); ix.E = () => { l.Dir = (l.Dir + 1) % 4; Sfx.Play("tick"); Burst(l.Pos, new Color("#bfe9ff"), 4, 40); }; }
        if (l.Level < 3) { ix.Lines.Add($"[U] Улучшить до ур.{l.Level + 1}: {CostStr(LampUpCost(l.Level + 1))}"); ix.U = () => UpgradeLamp(l); }
        else ix.Lines.Add("Максимальный уровень");
    }

    void UpdateLamps(float dt)
    {
        LightBlockT = Mathf.Max(0, LightBlockT - dt);
        foreach (var l in Lamps)
        {
            if (l.Anim < 1) l.Anim = Mathf.Min(1, l.Anim + dt / 1.2f);
            if (l.Kind == 1 && l.Anim >= 1)
            {
                float rr = 56f * (l.Level == 1 ? 1f : l.Level == 2 ? 1.3f : 1.6f), dps = 5f * l.Level;
                bool any = false;
                foreach (var e in Enemies)
                    if (!e.Hidden && e.Pos.DistanceTo(l.Pos) < rr) { e.Hp -= dps * dt * (e.IsBoss ? .3f : 1f); e.Flash = Mathf.Max(e.Flash, .04f); any = true; if (Has(Tech.FireLens)) e.Burn = 2f; }
                if (rng.Randf() < dt * (any ? 12 : 4)) Parts.Add(new Particle { P = l.Pos + new Vector2(rng.RandfRange(-3, 3), -6), V = new Vector2(rng.RandfRange(-6, 6), -rng.RandfRange(8, 20)), Life = .6f, Max = .6f, C = new Color(1f, .55f, .2f), Size = 1.5f });
            }
        }
    }

    // ───────────────────────── лучи света ─────────────────────────
    void AddBeam(Vector2I src, Vector2I dir, int maxLen, float dps, Color col, HashSet<int> rec, Vector2? startWorld = null)
    {
        var tr = W.Trace(src, dir, maxLen);
        var bp = new BeamPath { Dps = dps, C = col };
        bp.Pts.Add(startWorld ?? (TileCenter(src.X, src.Y) + new Vector2(dir.X, dir.Y) * 7));
        for (int i = 1; i < tr.Corners.Count - 1; i++) bp.Pts.Add(TileCenter(tr.Corners[i].X, tr.Corners[i].Y));
        if (tr.Hit != null) { bp.Pts.Add(TileCenter(tr.Hit.Value.X, tr.Hit.Value.Y)); rec.Add(World.Key(tr.Hit.Value.X, tr.Hit.Value.Y)); }
        else bp.Pts.Add(TileCenter(tr.EndTile.X, tr.EndTile.Y) + new Vector2(tr.EndDir.X, tr.EndDir.Y) * 8);
        Beams.Add(bp);
        int pts = 0;
        for (int i = 0; i + 1 < bp.Pts.Count && pts < 14; i++)
        {
            var a = bp.Pts[i]; var b = bp.Pts[i + 1]; float len = a.DistanceTo(b);
            for (float s = 0; s < len && pts < 14; s += 28f) { BeamLights.Add(a.Lerp(b, s / Mathf.Max(len, 1))); pts++; }
        }
    }

    static float DistToSeg(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a; float l2 = ab.LengthSquared(); if (l2 < .001f) return p.DistanceTo(a);
        float t = Mathf.Clamp((p - a).Dot(ab) / l2, 0, 1); return p.DistanceTo(a + ab * t);
    }

    void UpdateBeams(float dt)
    {
        Beams.Clear(); BeamLights.Clear();
        var rec = new HashSet<int>();
        bool blocked = LightBlockT > 0;
        foreach (var (t, d) in W.Emitters) AddBeam(t, d, 26, 4f, new Color("#ffe27a"), rec);
        if (!blocked)
            foreach (var l in Lamps)
                if (l.Kind == 2 && l.Anim >= 1) AddBeam(new Vector2I(l.X, l.Y), Dirs[l.Dir], 10 + 4 * (l.Level - 1), 8f + 4f * (l.Level - 1), new Color("#bfe9ff"), rec);

        // личный луч героя (награда Хранителя глубин)
        bool want = Has(Tech.Beam) && !blocked && (Input.IsKeyPressed(Key.R) || (testing && beamTest));
        if (BeamE <= 0) beamLock = true; if (BeamE >= 25) beamLock = false;
        BeamOn = want && !beamLock;
        if (BeamOn)
        {
            BeamE = Mathf.Max(0, BeamE - 32f * dt);
            var d = Aim - PPos; var dir = Mathf.Abs(d.X) > Mathf.Abs(d.Y) ? new Vector2I(Math.Sign(d.X == 0 ? 1 : d.X), 0) : new Vector2I(0, Math.Sign(d.Y == 0 ? 1 : d.Y));
            AddBeam(ToTile(PPos), dir, 18, 16f, new Color("#ffffff"), rec, PPos + new Vector2(dir.X, dir.Y) * 6);
            beamSfxT -= dt; if (beamSfxT <= 0) { beamSfxT = .25f; Sfx.Play("tick", -8, 1.8f); }
        }
        else BeamE = Mathf.Min(100, BeamE + (want ? 0 : 22f) * dt);

        // урон врагам
        foreach (var bp in Beams)
            for (int i = 0; i + 1 < bp.Pts.Count; i++)
                foreach (var e in Enemies)
                {
                    if (e.Hidden) continue;
                    if (DistToSeg(e.Pos, bp.Pts[i], bp.Pts[i + 1]) < e.Radius + 3)
                    {
                        e.Hp -= bp.Dps * dt * (e.IsBoss ? .35f : 1f); e.Flash = Mathf.Max(e.Flash, .05f);
                        if (rng.Randf() < dt * 15) Burst(e.Pos, bp.C, 1, 30);
                    }
                }
        puzzleT -= dt;
        if (puzzleT <= 0) { puzzleT = .1f; UpdatePuzzles(rec); }
    }

    bool beamTest;
    public bool beamLockPublic => beamLock;

    void UpdatePuzzles(HashSet<int> rec)
    {
        foreach (var pz in W.Puzzles)
        {
            bool all = true;
            foreach (var r in pz.Receivers)
            {
                bool lit = rec.Contains(World.Key(r.X, r.Y)); all &= lit;
                var cur = W.Get(r.X, r.Y);
                if (lit && cur == Tl.Receiver) { SetObj(r.X, r.Y, Tl.ReceiverOn); Sfx.Play("tick", -4, 1.5f); }
                else if (!lit && cur == Tl.ReceiverOn && pz.Poi.State < 3) SetObj(r.X, r.Y, Tl.Receiver);
            }
            if (all && pz.Poi.State < 3) SolvePuzzle(pz);
        }
    }

    void SolvePuzzle(Puzzle pz)
    {
        pz.Poi.State = 3; pz.Poi.Seen = true;
        foreach (var d in pz.Doors) { SetObj(d.X, d.Y, Tl.None); Burst(TileCenter(d.X, d.Y), new Color("#8ff0ff"), 14, 70); }
        GiveXp(60); Shake = 3; Flash = .25f;
        Announce("Загадка света решена!", "Тайная дверь открылась — внутри хранилище", new Color("#ffe27a"), "tech");
        Save();
    }

    void DrawBeams(EntityLayer c)
    {
        foreach (var (t, d) in W.Emitters)
        {
            var p = TileCenter(t.X, t.Y); var dv = new Vector2(d.X, d.Y); var perp = new Vector2(-dv.Y, dv.X);
            c.DrawColoredPolygon(new[] { p + dv * 8, p + dv * 3 + perp * 3, p + dv * 3 - perp * 3 }, new Color(1f, .9f, .4f, .9f));
        }
        foreach (var bp in Beams)
        {
            float w = bp.C == Colors.White ? 3f : 2f;
            float pulse = .75f + .25f * Mathf.Sin(Time * 18f);
            for (int i = 0; i + 1 < bp.Pts.Count; i++)
            {
                c.DrawLine(bp.Pts[i], bp.Pts[i + 1], new Color(bp.C.R, bp.C.G, bp.C.B, .22f), w * 3.4f);
                c.DrawLine(bp.Pts[i], bp.Pts[i + 1], new Color(bp.C.R, bp.C.G, bp.C.B, .9f * pulse), w);
                c.DrawLine(bp.Pts[i], bp.Pts[i + 1], new Color(1, 1, 1, .8f), 1f);
            }
        }
    }
}
