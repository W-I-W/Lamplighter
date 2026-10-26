using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Расширения света: светокристаллы-усилители, световые барьеры, улучшение фонаря героя.</summary>
public partial class Game
{
    public List<(Vector2 a, Vector2 b)> Barriers = new();
    public List<Vector2I> Posts = new();
    float lightT;
    static readonly int[][] AmpCost = C(It.Lens, 1, It.Crystal, 1);
    static readonly int[][] BarrierCost = C(It.Lens, 1, It.Gear, 1, It.Silver, 2);
    public static int[][] AmpCostArr => AmpCost;
    public static int[][] BarrierCostArr => BarrierCost;

    void InitLightTech()
    {
        Posts.Clear();
        for (int y = 0; y < World.Ht; y++) for (int x = 0; x < World.Wd; x++) if (W.Get(x, y) == Tl.BarrierPost) Posts.Add(new Vector2I(x, y));
        RecomputeLightTech();
    }

    public void BuildAmp()
    {
        if (!BpDone.Contains("bp_amp")) { Fail("Нужен чертёж «Светокристаллы» (журнал [J], лавка или находки)"); return; }
        if (!Can(AmpCost)) { Fail("Светокристалл: нужно " + NeedStr(AmpCost)); return; }
        var t = PlaceTarget(); if (t == null) return;
        Pay(AmpCost); SetObj(t.Value.X, t.Value.Y, Tl.AmpCrystal); GiveXp(10);
        Burst(TileCenter(t.Value.X, t.Value.Y), new Color("#ffb0ff"), 14, 70); Sfx.Play("upgrade", -2, 1.3f); RecomputeLightTech();
        bool near = false; foreach (var l in Lamps) if (l.Pos.DistanceTo(TileCenter(t.Value.X, t.Value.Y)) < 80) near = true;
        Toast(near ? "Светокристалл усиливает соседние фонари (до трёх на фонарь)" : "Светокристалл работает рядом с фонарями (в радиусе 5 клеток)");
    }

    public void BuildBarrier()
    {
        if (!BpDone.Contains("bp_barrier")) { Fail("Нужен чертёж «Световые барьеры» (журнал [J], лавка или находки)"); return; }
        if (!Can(BarrierCost)) { Fail("Столб-барьер: нужно " + NeedStr(BarrierCost)); return; }
        var t = PlaceTarget(); if (t == null) return;
        Pay(BarrierCost); SetObj(t.Value.X, t.Value.Y, Tl.BarrierPost); Posts.Add(t.Value); GiveXp(10);
        Burst(TileCenter(t.Value.X, t.Value.Y), new Color("#8ff0ff"), 14, 70); Sfx.Play("build", -2, 1.3f); RecomputeLightTech();
        Toast("Два столба на одной линии (до 9 клеток) образуют барьер, задерживающий теней");
    }

    static readonly int[][][] HeroCost = { C(It.Lens, 1, It.Copper, 4), C(It.Lens, 2, It.Silver, 3), C(It.Lens, 3, It.AncCrystal, 1) };
    public static int[][] HeroCostFor(int lv) => HeroCost[lv];

    public void UpgradeHeroLamp()
    {
        if (!BpDone.Contains("bp_herolamp")) { Fail("Нужен чертёж «Фонарь героя» (журнал [J])"); return; }
        if (HeroLampLv >= 3) { Toast("Фонарь героя уже на максимальном уровне"); return; }
        var cost = HeroCost[HeroLampLv];
        if (!Can(cost)) { Fail("Улучшение фонаря: нужно " + NeedStr(cost)); return; }
        Pay(cost); HeroLampLv++; GiveXp(40); Burst(PPos, new Color("#fff2b0"), 24, 110); Sfx.Play("upgrade"); Flash = .25f;
        Announce($"Фонарь героя: уровень {HeroLampLv}", HeroLampLv == 1 ? "Радиус света +12%" : HeroLampLv == 2 ? "Радиус +12%, свет жжёт врагов вокруг" : "Радиус +12%, мощная аура света", new Color("#fff2b0"), "upgrade");
        RecomputeEffects(); Save();
    }

    /// <summary>Раз в секунду: усиление фонарей кристаллами и линии барьеров.</summary>
    void RecomputeLightTech()
    {
        foreach (var l in Lamps)
        {
            int n = 0;
            for (int y = l.Y - 5; y <= l.Y + 5; y++) for (int x = l.X - 5; x <= l.X + 5; x++) if (W.Get(x, y) == Tl.AmpCrystal && TileCenter(x, y).DistanceTo(l.Pos) <= 82) n++;
            l.Amp = Math.Min(3, n);
        }
        Posts.RemoveAll(p => W.Get(p.X, p.Y) != Tl.BarrierPost);
        Barriers.Clear();
        for (int i = 0; i < Posts.Count; i++)
            for (int j = i + 1; j < Posts.Count; j++)
            {
                var a = Posts[i]; var b = Posts[j];
                if (a.X != b.X && a.Y != b.Y) continue;
                int d = Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y); if (d > 9 || d < 2) continue;
                var step = new Vector2I(Math.Sign(b.X - a.X), Math.Sign(b.Y - a.Y)); bool clear = true;
                for (var c = a + step; c != b; c += step) if (World.Blocks(W.Get(c.X, c.Y))) { clear = false; break; }
                if (clear) Barriers.Add((TileCenter(a.X, a.Y), TileCenter(b.X, b.Y)));
            }
    }

    static bool SegIntersect(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
    {
        float d1 = (q2 - q1).Cross(p1 - q1), d2 = (q2 - q1).Cross(p2 - q1), d3 = (p2 - p1).Cross(q1 - p1), d4 = (p2 - p1).Cross(q2 - p1);
        return d1 * d2 < 0 && d3 * d4 < 0;
    }

    public bool BarrierCross(Vector2 a, Vector2 b)
    {
        foreach (var s in Barriers) if (SegIntersect(a, b, s.a, s.b)) return true;
        return false;
    }

    void UpdateLightTech(float dt)
    {
        lightT -= dt; if (lightT > 0) return; lightT = 1f; RecomputeLightTech();
    }

    void DrawBarriers(EntityLayer c)
    {
        foreach (var s in Barriers)
        {
            float k = .6f + .4f * Mathf.Sin(Time * 6f);
            c.DrawLine(s.a, s.b, new Color(.55f, .9f, 1f, .25f * k), 7f);
            c.DrawLine(s.a, s.b, new Color(.7f, .95f, 1f, .8f * k), 1.5f);
            if (rng.Randf() < .1f) Parts.Add(new Particle { P = s.a.Lerp(s.b, rng.Randf()), V = new Vector2(0, -rng.RandfRange(6, 14)), Life = .5f, Max = .5f, C = new Color("#bff9ff"), Size = 1.4f });
        }
    }
}
