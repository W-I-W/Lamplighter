using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Отрисовка врагов, боссов и телеграфов атак.</summary>
public partial class Game
{
    void DrawAshTelegraph(EntityLayer c, Enemy e)
    {
        if (e.Kind == EK.Golem && e.State == 1)
        {
            float k = 1 - e.T1 / .85f;
            Gfx.Ring(c, e.Pos, 44, 28, new Color(1f, .6f, .3f, .4f + .4f * k), 1.5f); Gfx.Ring(c, e.Pos, 44 * k, 24, new Color(1f, .9f, .6f, .5f), 1f);
        }
        if (e.Kind == EK.AshWarden)
        {
            if (e.State == 2) { float k = 1 - e.T2 / .9f; Gfx.Ring(c, e.Pos, 72, 40, new Color(1f, .5f, .2f, .4f + .4f * k), 2f); Gfx.Ring(c, e.Pos, 72 * k, 36, new Color(1f, .85f, .5f, .5f), 1f); }
            if (e.State == 5) Gfx.Line(c, e.Pos, e.Pos + e.DashDir * 110, new Color(1f, .5f, .2f, .6f), 2f);
            if (e.State == 3 || e.State == 1 || e.State == 4) Gfx.Ring(c, e.Pos, 20 + 6 * Mathf.Sin(Time * 20), 16, new Color(1f, .6f, .3f, .7f), 1.5f);
        }
        if (e.Kind == EK.Spirit && e.Tele > 0) Gfx.Disc(c, e.Pos + new Vector2(0, -2), 3 + 3 * (1 - e.Tele / .5f), new Color(1f, .5f, .2f, .7f));
    }

    // ───────────────────────── рисование врагов ─────────────────────────
    void DrawEnemy(EntityLayer c, Enemy e)
    {
        var vd = EnemyDef(e.Kind); float sc = (e.Elite ? 1.25f : 1f) * vd.Scale;
        if (e.Hidden)
        {
            // под водой видна только рябь
            float ph = (Time * 2 + e.Phase) % 1f;
            Gfx.Ring(c, e.Pos, 4 + ph * 8 * (e.IsBoss ? 2 : 1), 16, new Color(.7f, .9f, 1f, .6f * (1 - ph)), 1f);
            Gfx.Ring(c, e.Pos, 2 + ph * 4, 12, new Color(.7f, .9f, 1f, .5f * (1 - ph)), 1f);
            return;
        }
        Shadow(c, e.Pos, 5 * sc);
        int f = (int)(Time * vd.Fps + e.Phase) % vd.Frames;
        float bob = Mathf.Sin(Time * 4 + e.Phase) * vd.Bob - vd.Lift;
        float a = Mathf.Clamp(e.Age * 2f, 0, 1);
        Color mod = e.Flash > 0 ? new Color(1.8f, .6f, .6f, a) : e.Freeze > 0 ? new Color(.6f, .9f, 1.6f, a) : e.Slow > 0 ? new Color(.75f, .95f, 1.25f, a) : e.Elite ? new Color(1.3f, 1.15f, .7f, a) : new Color(1, 1, 1, a);
        if (e.Kind == EK.Warden && e.Flash <= 0) mod = new Color(1.2f, .6f, .9f, a);
        if ((e.Tele > 0 || (e.Kind == EK.Warden && (e.State == 1 || e.State == 2 || e.State == 5)) || (e.Kind == EK.DepthWarden && (e.State == 4 || e.State == 5 || e.State == 6))) && ((int)(Time * 25) % 2 == 0)) mod = new Color(1.6f, 1.6f, 2f, a);
        if (WaterAt(e.Pos) && e.Kind != EK.DepthWarden) mod.A *= .85f;
        int face = e.Kind == EK.Beetle ? (e.Face.X < 0 ? -1 : 1) : (PPos.X < e.Pos.X ? -1 : 1);
        c.DrawSetTransform(e.Pos + new Vector2(0, bob), 0, new Vector2(face * sc, sc));
        DrawFrame(c, vd, f, mod);
        c.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        if (e.Kind == EK.Warden)
        {
            if (e.State == 1) { float k = 1 - e.T2 / .9f; Gfx.Ring(c, e.Pos, 64, 40, new Color(.85f, .5f, 1f, .4f + .4f * k), 2f); Gfx.Ring(c, e.Pos, 64 * k, 40, new Color(1f, .8f, 1f, .5f), 1f); }
            if (e.State == 2) Gfx.Line(c, e.Pos, e.Pos + e.DashDir * 90, new Color(1f, .5f, .8f, .6f), 2f);
        }
        if (e.Kind == EK.DepthWarden)
        {
            if (e.State == 4) { float k = 1 - e.T2 / 1f; Gfx.Ring(c, e.Mark, 52, 40, new Color(.4f, .8f, 1f, .4f + .4f * k), 2f); Gfx.Ring(c, e.Mark, 52 * k, 40, new Color(.8f, .95f, 1f, .5f), 1f); }
            if (e.State == 3) Gfx.Ring(c, e.Pos, 10, 12, new Color(1f, 1f, .5f, .6f), 1f);
        }
        DrawAshTelegraph(c, e); DrawIceTelegraph(c, e); DrawHeartTelegraph(c, e);
        if (!e.IsBoss && e.Hp < e.MaxHp)
        {
            var o = e.Pos + new Vector2(-6, -14 * sc);
            Gfx.Rect(c, new Rect2(o, new Vector2(12, 1.5f)), new Color(0, 0, 0, .7f));
            Gfx.Rect(c, new Rect2(o, new Vector2(12 * Mathf.Clamp(e.Hp / e.MaxHp, 0, 1), 1.5f)), new Color("#ff5ec8"));
        }
    }

    // ───────────────────────── рисование ─────────────────────────
    void DrawHeartTelegraph(EntityLayer c, Enemy e)
    {
        if (e.Kind == EK.Mirage && e.Tele > 0) Gfx.Line(c, e.Pos, e.Pos + (e.Mark - e.Pos).Normalized() * 80, new Color(.7f, .4f, 1f, .6f), 1.5f);
        if (e.Kind == EK.Weaver && e.Tele > 0) { Gfx.Ring(c, e.Mark, 30, 24, new Color(.6f, .3f, 1f, .8f), 1.5f); }
        if (e.Kind == EK.Stalker && e.Tele > 0) { Gfx.Ring(c, e.Mark, 12, 16, new Color(.7f, .4f, 1f, .9f), 1.5f); Gfx.Line(c, e.Pos, e.Mark, new Color(.7f, .4f, 1f, .25f), 1f); }
        if (e.Kind == EK.Decoy) { float a = .5f + .3f * Mathf.Sin(Time * 14f); Gfx.Ring(c, e.Pos, 7, 12, N("Reveal") > 0 ? new Color(1f, .4f, .4f, .9f) : new Color(.8f, .6f, 1f, a), 1f); }
        if (e.Kind != EK.Source) return;
        if (e.Shield) { float p = .6f + .4f * Mathf.Sin(Time * 5f); Gfx.Ring(c, e.Pos, 28, 32, new Color(.7f, .4f, 1f, .5f * p), 2.5f); }
        if (e.VulnT > 0) Gfx.Ring(c, e.Pos, 22, 24, new Color(1f, .9f, .4f, .8f), 1.5f);
        float k = e.State == 0 ? 0 : 1 - Mathf.Clamp(e.T2 / (e.State == 5 ? 1.6f : .9f), 0, 1);
        switch (e.State)
        {
            case 1: { float R = (int)(e.Mark.X) == 3 ? 115f : 95f; Gfx.Ring(c, e.Pos, R, 48, new Color(.7f, .4f, 1f, .35f + .4f * k), 1.5f); Gfx.Ring(c, e.Pos, R * (1 - k), 36, new Color(1, 1, 1, .5f), 1f); break; }
            case 2: Gfx.Line(c, e.Pos, e.Pos + e.DashDir * 150, new Color(.7f, .4f, 1f, .3f + .5f * k), 2f); break;
            case 3: case 4: case 6: case 7: Gfx.Ring(c, e.Pos, 14 + 14 * k, 20, new Color(.9f, .6f, 1f, .8f), 1.5f); break;
            case 5: Gfx.Ring(c, e.Pos, 20, 24, new Color(.6f, .2f, 1f, .6f + .3f * Mathf.Sin(Time * 14f)), 2f); break;
        }
    }

    /// <summary>Глобальные элементы Сердца: руны, предупреждения пустоты, огни, лучи светил к боссу.</summary>
    void DrawHeartWorld(EntityLayer c)
    {
        if (RegionAt(PPos) != 4 && VoidPending.Count == 0) return;
        foreach (var t in W.Traps)
        {
            var p = TileCenter(t.X, t.Y); float d = p.DistanceTo(PPos); float rv = N("Reveal") > 0 ? 1.7f : 1f; if (d > PlayerLightR * .9f * rv) continue;
            float a = Mathf.Clamp(1.2f - d / (PlayerLightR * .9f * rv), 0, 1) * (.5f + .3f * Mathf.Sin(Time * 4f + t.X));
            Gfx.Ring(c, p, 6, 12, new Color(.7f, .3f, 1f, a), 1f); Gfx.Line(c, p + new Vector2(-4, 0), p + new Vector2(4, 0), new Color(.7f, .3f, 1f, a), 1f); Gfx.Line(c, p + new Vector2(0, -4), p + new Vector2(0, 4), new Color(.7f, .3f, 1f, a), 1f);
        }
        foreach (var t in W.FalseLights)
            if (falseArm.TryGetValue(World.Key(t.X, t.Y), out float arm)) { var p = TileCenter(t.X, t.Y); float k = 1 - arm / .9f; Gfx.Ring(c, p, 26, 24, new Color(1f, .5f, 1f, .5f + .4f * k), 1.5f); Gfx.Ring(c, p, 26 * (1 - k), 20, new Color(1, 1, 1, .5f), 1f); }
        if (N("Reveal") > 0) foreach (var rd in W.Riddles) if (rd.Poi.State < 3) { var rp = TileCenter(rd.Lanterns[rd.Real].X, rd.Lanterns[rd.Real].Y); if (rp.DistanceTo(PPos) < 260) Gfx.Ring(c, rp, 14 + 2 * Mathf.Sin(Time * 4f), 20, new Color(1f, .9f, .4f, .8f), 1.2f); }
        foreach (var f in ArenaFalse) { var p = TileCenter(f.tile.X, f.tile.Y); if (f.arm < .6f) Gfx.Ring(c, p, 18, 20, new Color(1f, .4f, 1f, .8f), 1.5f); }
        foreach (var b in VoidPending) { var p = TileCenter(b.tile.X, b.tile.Y); float k = 1 - b.t / 1.4f; Gfx.Rect(c, new Rect2(p - new Vector2(8, 8), new Vector2(16, 16)), new Color(.5f, .1f, 1f, .15f + .35f * k)); Gfx.Outline(c, new Rect2(p - new Vector2(8, 8), new Vector2(16, 16)), new Color(.8f, .5f, 1f, .7f), 1f); }
        if (Boss != null && Boss.Kind == EK.Source && Boss.Shield)
            foreach (var p in W.ArenaPylons) if (PylonT.GetValueOrDefault(World.Key(p.X, p.Y)) > 0) { var pc = TileCenter(p.X, p.Y); Gfx.Line(c, pc, Boss.Pos, new Color(1f, .9f, .5f, .25f), 4f); Gfx.Line(c, pc, Boss.Pos, new Color(1f, .95f, .8f, .8f), 1.2f); }
    }

    void DrawIceTelegraph(EntityLayer c, Enemy e)
    {
        if (e.Kind == EK.Sentinel && e.State == 1) { float k = 1 - e.T1 / .9f; Gfx.Ring(c, e.Pos, 44, 28, new Color(.7f, .9f, 1f, .4f + .4f * k), 1.5f); Gfx.Ring(c, e.Pos, 44 * k, 24, new Color(1, 1, 1, .5f), 1f); }
        if (e.Kind == EK.FrostPriest && e.Tele > 0) { Gfx.Ring(c, e.Mark, 22, 20, new Color(.6f, .9f, 1f, .8f), 1.5f); Gfx.Ring(c, e.Pos + new Vector2(0, -6), 4 + 4 * (1 - e.Tele / .5f), 12, new Color(.8f, .95f, 1f, .8f), 1f); }
        if (e.Kind == EK.IceWolf && e.Tele > 0) Gfx.Line(c, e.Pos, e.Pos + dirTo(e) * 70, new Color(.7f, .9f, 1f, .6f), 1.5f);
        if (e.Kind == EK.WinterWarden)
        {
            if (e.Shield) { float p = .6f + .4f * Mathf.Sin(Time * 6f); Gfx.Ring(c, e.Pos, 26, 32, new Color(.75f, .92f, 1f, .5f * p), 2.5f); Gfx.Arc(c, e.Pos, 22, Time, Time + 3.1f, 16, new Color(1, 1, 1, .6f), 1.5f); }
            if (e.State == 3) { Gfx.Line(c, e.Pos, e.Pos + e.DashDir * 130, new Color(.7f, .95f, 1f, .35f + .5f * (1 - e.T2 / .9f)), 2f); }
            if (e.VulnT > 0) Gfx.Ring(c, e.Pos, 20, 24, new Color(1f, .9f, .4f, .7f), 1.5f);
        }
        if (e.Kind == EK.WinterWarden || true)
            foreach (var pb in PendingBlocks) { var cc = TileCenter(pb.tile.X, pb.tile.Y); float k = 1 - pb.t / 1.1f; Gfx.Rect(c, new Rect2(cc - new Vector2(8, 8), new Vector2(16, 16)), new Color(.7f, .95f, 1f, .15f + .3f * k)); Gfx.Outline(c, new Rect2(cc - new Vector2(8, 8), new Vector2(16, 16)), new Color(.8f, 1f, 1f, .7f), 1f); }
    }
}
