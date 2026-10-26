using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Огонь на земле: предупреждение (Warn), затем горение (Dur) с уроном (Dps). Smoke — скрыто под дымом.</summary>
public sealed class FireZone { public Vector2 Pos; public float R, T, Warn, Dur, Dps; public bool Smoke, Friendly; }
public sealed class SmokeCloud { public Vector2 Pos, Vel; public float R, Phase; }

/// <summary>Механики Пепельных пустошей: горячий пепел, дым, огонь, пепельные враги и страж.</summary>
public partial class Game
{
    public float LeechT;
    public List<FireZone> Zones = new();
    public List<SmokeCloud> Clouds = new();
    float hotDot, trailT, impT;
    bool ashInit;

    public float SmokeRes => Has(Tech.Fireproof) ? 0f : Has(Tech.HeatShield) ? .5f : 1f;
    public float LightDim => (LightBlockT > 0 ? .3f : 1f) * (LeechT > 0 ? .6f : 1f) * (1f - .6f * SmokeAt(PPos) * SmokeRes);

    void InitAsh()
    {
        if (ashInit) return; ashInit = true;
        var r = new RandomNumberGenerator(); r.Seed = (ulong)W.Seed + 5;
        for (int i = 0; i < 14; i++)
            Clouds.Add(new SmokeCloud
            {
                Pos = new Vector2(r.RandfRange((World.AX0 + 20) * TS, (World.AX1 - 6) * TS), r.RandfRange(8 * TS, (World.Ht - 8) * TS)),
                Vel = Vector2.FromAngle(r.Randf() * Mathf.Tau) * r.RandfRange(5, 11), R = r.RandfRange(60, 110), Phase = r.Randf() * 6.28f
            });
    }

    /// <summary>Густота дыма в точке (0..1): облака пустошей и дымовые завесы стража.</summary>
    public float SmokeAt(Vector2 p)
    {
        float s = 0;
        foreach (var c in Clouds) { float d = p.DistanceTo(c.Pos); if (d < c.R) s = Mathf.Max(s, (1 - d / c.R) * 1.4f); }
        foreach (var z in Zones) if (z.Smoke && z.T < z.Warn + .8f) { float d = p.DistanceTo(z.Pos); float rr = z.R * 1.6f; if (d < rr) s = Mathf.Max(s, (1 - d / rr) * 1.6f); }
        return Mathf.Clamp(s, 0, 1);
    }

    Vector4[] SmokeUniform()
    {
        var a = new Vector4[16]; int n = 0; var cc = CamCenter;
        foreach (var z in Zones) if (z.Smoke && z.T < z.Warn + .8f && n < 16) a[n++] = new Vector4(z.Pos.X, z.Pos.Y, z.R * 1.6f, .95f);
        foreach (var c in Clouds) if (n < 16 && c.Pos.DistanceTo(cc) < 700) a[n++] = new Vector4(c.Pos.X, c.Pos.Y, c.R, .7f);
        return a;
    }

    public void AddZone(Vector2 p, float r, float warn, float dur, float dps, bool smoke = false, bool friendly = false)
    {
        Zones.Add(new FireZone { Pos = p, R = r, Warn = warn, Dur = dur, Dps = dps, Smoke = smoke, Friendly = friendly });
        if (Zones.Count > 70) Zones.RemoveAt(0);
    }

    static bool HotActive(float t, int tx, int ty, out bool warn)
    {
        float ph = (((tx / 5) * 37 + (ty / 5) * 91) % 7) * .93f;
        float cyc = (t + ph) % 6.5f;
        warn = cyc > 3.4f && cyc <= 4.4f;
        return cyc > 4.4f;
    }

    public void DotDamage(float amt, bool fire)
    {
        if (fire && Has(Tech.Fireproof)) return;
        if (fire && Has(Tech.HeatShield)) amt *= .4f;
        if (fire) amt *= 1f - N("HeatResist");
        Hp -= amt; regenDelay = 3f; hotDot -= amt;
        if (hotDot < -3f) { hotDot = 0; Burst(PPos, new Color("#ff7a2a"), 3, 50); Sfx.Play("hurt", -8, 1.4f); Shake = Mathf.Max(Shake, 1.2f); }
        if (Hp <= 0) Die();
    }

    void ApplyHazards(float dt)
    {
        foreach (var c in Clouds)
        {
            c.Pos += c.Vel * dt; c.Phase += dt * .2f; c.Vel = c.Vel.Rotated(Mathf.Sin(c.Phase) * dt * .3f);
            if (c.Pos.X < (World.AX0 + 6) * TS || c.Pos.X > (World.AX1 - 4) * TS) c.Vel.X = -c.Vel.X;
            if (c.Pos.Y < 6 * TS || c.Pos.Y > (World.Ht - 6) * TS) c.Vel.Y = -c.Vel.Y;
        }
        // горячий пепел
        var pt = ToTile(PPos);
        if (World.In(pt.X, pt.Y) && W.Ground[World.Key(pt.X, pt.Y)] == Art.HotAsh && HotActive(Time, pt.X, pt.Y, out _) && DashT <= 0)
            DotDamage(11f * dt, true);
        // огненные зоны
        for (int i = Zones.Count - 1; i >= 0; i--)
        {
            if (i >= Zones.Count) continue;
            var z = Zones[i]; z.T += dt;
            if (z.T >= z.Warn + z.Dur) { Zones.RemoveAt(i); continue; }
            if (z.T < z.Warn || z.Dps <= 0) continue;
            if (z.Friendly) { foreach (var e in Enemies) if (!e.Hidden && e.Kind != EK.Imp && e.Kind != EK.AshWarden && e.Pos.DistanceTo(z.Pos) < z.R + e.Radius) { e.Hp -= z.Dps * dt; e.Burn = Mathf.Max(e.Burn, 1f); e.Flash = Mathf.Max(e.Flash, .04f); } }
            else if (PPos.DistanceTo(z.Pos) < z.R) DotDamage(z.Dps * dt, true);
            if (rng.Randf() < dt * 14) Parts.Add(new Particle { P = z.Pos + new Vector2(rng.RandfRange(-z.R, z.R) * .7f, rng.RandfRange(-z.R, z.R) * .5f), V = new Vector2(rng.RandfRange(-4, 4), -rng.RandfRange(14, 30)), Life = .6f, Max = .6f, C = rng.Randf() < .5f ? new Color("#ff7a2a") : new Color("#ffd04a"), Size = 1.8f });
        }
        // огненный след рывка (награда Пепельного стража)
        if (DashT > 0 && Has(Tech.Fireproof)) { trailT -= dt; if (trailT <= 0) { trailT = .05f; AddZone(PPos, 10, 0, 2.5f, 12f, false, true); } }
    }

    public IEnumerable<Vector4> ZoneLights()
    {
        int n = 0;
        foreach (var z in Zones)
            if (z.T >= z.Warn && z.T < z.Warn + z.Dur && z.Dps > 0) { yield return new Vector4(z.Pos.X, z.Pos.Y, z.R * 2.2f, 0); if (++n >= 12) yield break; }
    }

    void DrawHazards(EntityLayer c)
    {
        var pt = ToTile(PPos);
        if (RegionAt(PPos) == 2)
            for (int y = pt.Y - 9; y <= pt.Y + 9; y++)
                for (int x = pt.X - 14; x <= pt.X + 14; x++)
                {
                    if (!World.In(x, y) || W.Ground[World.Key(x, y)] != Art.HotAsh) continue;
                    bool act = HotActive(Time, x, y, out bool warn);
                    float a = act ? .5f + .15f * Mathf.Sin(Time * 14f + x) : warn ? .2f + .25f * (.5f + .5f * Mathf.Sin(Time * 18f)) : .07f + .03f * Mathf.Sin(Time * 2f + x * .7f);
                    c.DrawRect(new Rect2(x * TS, y * TS, TS, TS), new Color(1f, act ? .45f : .35f, .1f, a));
                    if (act && rng.Randf() < .04f) Parts.Add(new Particle { P = new Vector2(x * TS + rng.RandfRange(2, 14), y * TS + 10), V = new Vector2(0, -rng.RandfRange(15, 35)), Life = .5f, Max = .5f, C = new Color("#ffb04a"), Size = 1.6f });
                }
        foreach (var z in Zones)
        {
            bool pending = z.T < z.Warn;
            if (pending)
            {
                float k = z.T / Mathf.Max(z.Warn, .01f);
                if (z.Smoke) { if ((int)(Time * 6) % 3 == 0) c.DrawRect(new Rect2(z.Pos + new Vector2(rng.RandfRange(-z.R, z.R) * .6f, rng.RandfRange(-z.R, z.R) * .6f), new Vector2(1.5f, 1.5f)), new Color(1f, .5f, .2f, .5f)); }
                else
                {
                    c.DrawCircle(z.Pos, z.R, new Color(1f, .4f, .1f, .12f + .1f * Mathf.Sin(Time * 16f)));
                    c.DrawArc(z.Pos, z.R, 0, Mathf.Tau, 28, new Color(1f, .6f, .2f, .8f), 1.5f);
                    c.DrawArc(z.Pos, z.R * (1 - k), 0, Mathf.Tau, 24, new Color(1f, .9f, .5f, .6f), 1f);
                }
            }
            else if (z.Dps > 0)
            {
                float fade = Mathf.Min(1f, (z.Warn + z.Dur - z.T) * 2f);
                c.DrawCircle(z.Pos, z.R, z.Friendly ? new Color(1f, .8f, .3f, .3f * fade) : new Color(1f, .3f, .08f, .38f * fade));
                c.DrawCircle(z.Pos, z.R * .6f, new Color(1f, .75f, .2f, .3f * fade));
                c.DrawArc(z.Pos, z.R, 0, Mathf.Tau, 24, new Color(1f, .6f, .2f, .7f * fade), 1.2f);
            }
        }
        // жерла: тлеющие огоньки и выбросы дыма
        foreach (var v in W.Vents)
        {
            var p = TileCenter(v.X, v.Y); if (p.DistanceTo(PPos) > 260) continue;
            if (rng.Randf() < .08f) Parts.Add(new Particle { P = p + new Vector2(rng.RandfRange(-3, 3), -4), V = new Vector2(rng.RandfRange(-6, 6), -rng.RandfRange(14, 30)), Life = 1.1f, Max = 1.1f, C = rng.Randf() < .3f ? new Color("#ff9a2e") : new Color(.35f, .33f, .32f), Size = 2f });
        }
    }

    // ───────────────────────── пепельные враги ─────────────────────────
    Vector2 UpdateAshEnemy(Enemy e, float dt, Vector2 dir, float dist, Vector2 perp, float k)
    {
        Vector2 vel = Vector2.Zero;
        switch (e.Kind)
        {
            case EK.Imp:
                vel = (dir + perp * Mathf.Sin(Time * 7f + e.Phase) * .7f).Normalized() * 74f * k;
                e.T1 -= dt; if (e.T1 <= 0) { e.T1 = .32f; AddZone(e.Pos, 9, 0, 2.4f, 7f); }
                if (dist < 12 && e.Atk <= 0) { e.Atk = .8f; HurtPlayer(7f * e.Scale, -dir, true); }
                break;
            case EK.Golem:
                e.Cd -= dt;
                if (e.State == 0)
                {
                    vel = dir * 24f * k;
                    if (dist < 40 && e.Cd <= 0) { e.State = 1; e.T1 = .85f; }
                }
                else
                {
                    e.T1 -= dt;
                    if (e.T1 <= 0)
                    {
                        e.State = 0; e.Cd = 2.2f; Shake = Mathf.Max(Shake, 4); Sfx.Play("boom", -3, .8f);
                        for (int i = 0; i < 16; i++) { float a = i * Mathf.Tau / 16; Parts.Add(new Particle { P = e.Pos, V = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 90, Life = .4f, Max = .4f, C = new Color(.6f, .58f, .55f), Size = 2 }); }
                        AddZone(e.Pos, 34, 0, 3.5f, 0f, true);
                        if (dist < 44) HurtPlayer(15f * e.Scale, -dir);
                    }
                }
                break;
            case EK.Spirit:
                e.Cd -= dt;
                if (e.Tele > 0)
                {
                    e.Tele -= dt;
                    if (e.Tele <= 0) { Projs.Add(new Proj { Pos = e.Pos, Vel = dir * 88f, Dmg = 7f * e.Scale, C = new Color("#ff7a2a"), Fire = true, Zone = true }); Sfx.Play("shoot", -3, .8f); e.Cd = rng.RandfRange(2.2f, 3.2f); }
                }
                else
                {
                    if (dist > 110) vel = dir * 46f; else if (dist < 70) vel = -dir * 46f; else vel = perp * Mathf.Sin(Time * 1.2f + e.Phase) * 24f;
                    vel *= k;
                    if (e.Cd <= 0 && dist < 170) e.Tele = .5f;
                }
                break;
            case EK.AshWarden: vel = UpdateAshWarden(e, dt, dir, dist); break;
        }
        return vel;
    }

    Vector2 UpdateAshWarden(Enemy e, float dt, Vector2 dir, float dist)
    {
        float hpf = e.Hp / e.MaxHp; bool rage = hpf < .33f; float mul = rage ? .6f : 1f;
        if (rage && e.Mark.X == 0)
        {
            e.Mark = new Vector2(1, 0);
            Announce("Пепельный страж в ярости!", "Огонь держится дольше, он бросается вперёд и оставляет пламя за собой", new Color("#ff7a3a"), "boss"); Shake = 5;
        }
        Vector2 vel = Vector2.Zero;
        switch (e.State)
        {
            case 0:
                vel = dir * (rage ? 46f : 34f); e.T1 -= dt;
                if (dist < 24 && e.Atk <= 0) { e.Atk = 1f; HurtPlayer(12f, -dir, true); }
                if (rage) { trailT -= dt; if (trailT <= 0) { trailT = .25f; AddZone(e.Pos, 12, 0, 3f, 9f); } }
                if (e.T1 <= 0)
                {
                    float r = rng.Randf();
                    if (!rage && hpf > .66f) { e.State = r < .5f ? 1 : 2; e.T2 = r < .5f ? .8f : .9f; }
                    else if (!rage) { if (r < .25f) { e.State = 1; e.T2 = .8f; } else if (r < .6f) { e.State = 3; e.T2 = .7f; } else if (r < .8f) { e.State = 2; e.T2 = .9f; } else { e.State = 4; e.T2 = .8f; } }
                    else { if (r < .3f) { e.State = 5; e.T2 = .5f; e.DashDir = dir; } else if (r < .6f) { e.State = 3; e.T2 = .6f; } else if (r < .8f) { e.State = 1; e.T2 = .6f; } else { e.State = 2; e.T2 = .7f; } }
                }
                break;
            case 1:     // огненные столпы: видимое предупреждение
                e.T2 -= dt;
                if (e.T2 <= 0)
                {
                    float dur = rage ? 8f : 5.5f;
                    AddZone(PPos, 26, 1.1f, dur, 13f);
                    for (int i = 0; i < 3; i++) AddZone(PPos + Vector2.FromAngle(rng.Randf() * Mathf.Tau) * rng.RandfRange(30, 70), 24, 1.1f + i * .2f, dur, 13f);
                    Sfx.Play("flare", -2, .6f); e.State = 0; e.T1 = 2.4f * mul;
                }
                break;
            case 2:     // топот: расходящаяся волна
                e.T2 -= dt;
                if (e.T2 <= 0)
                {
                    Shake = 7; Sfx.Play("boom");
                    for (int i = 0; i < 32; i++) { float a = i * Mathf.Tau / 32; Parts.Add(new Particle { P = e.Pos, V = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 140, Life = .5f, Max = .5f, C = new Color("#ff7a2a"), Size = 2 }); }
                    if (dist < 72) HurtPlayer(20f, -dir, true);
                    RingShot(e.Pos, rage ? 12 : 8, new Color("#ff7a2a"), true);
                    e.State = 0; e.T1 = 2.2f * mul;
                }
                break;
            case 3:     // дымовая завеса: опасные участки скрыты
                e.T2 -= dt;
                if (e.T2 <= 0)
                {
                    float dur = rage ? 8f : 5.5f;
                    for (int i = 0; i < 4; i++) AddZone(PPos + Vector2.FromAngle(i * 1.57f + rng.Randf()) * rng.RandfRange(25, 80), 30, 2.2f + i * .15f, dur, 14f, true);
                    Sfx.Play("pulse", -6, .6f); Burst(e.Pos, new Color(.4f, .38f, .36f), 18, 60);
                    e.State = 0; e.T1 = 2.8f * mul;
                }
                break;
            case 4:     // призыв
                e.T2 -= dt;
                if (e.T2 <= 0)
                {
                    for (int i = 0; i < 2; i++) SpawnEnemy(e.Pos + new Vector2(rng.RandfRange(-24, 24), rng.RandfRange(-24, 24)), EK.Imp, null, 1.2f);
                    Burst(e.Pos, new Color("#ff7a2a"), 16, 90); Sfx.Play("boss", -4, 1.2f); e.State = 0; e.T1 = 3f * mul;
                }
                break;
            case 5:     // рывок с огненным следом
                e.T2 -= dt; e.DashDir = dir;
                if (e.T2 <= 0) { e.State = 6; e.T2 = .6f; Sfx.Play("dash", 2, .6f); }
                break;
            case 6:
                vel = e.DashDir * 190f; e.T2 -= dt; trailT -= dt;
                if (trailT <= 0) { trailT = .08f; AddZone(e.Pos, 13, 0, 4.5f, 10f); }
                if (dist < 22 && e.Atk <= 0) { e.Atk = 1f; HurtPlayer(18f, -dir, true); }
                if (e.T2 <= 0) { e.State = 7; e.T2 = .7f; }
                break;
            case 7:
                e.T2 -= dt; if (e.T2 <= 0) { e.State = 0; e.T1 = 1.6f * mul; }
                break;
        }
        return vel;
    }

    void DrawAshTelegraph(EntityLayer c, Enemy e)
    {
        if (e.Kind == EK.Golem && e.State == 1)
        {
            float k = 1 - e.T1 / .85f;
            c.DrawArc(e.Pos, 44, 0, Mathf.Tau, 28, new Color(1f, .6f, .3f, .4f + .4f * k), 1.5f); c.DrawArc(e.Pos, 44 * k, 0, Mathf.Tau, 24, new Color(1f, .9f, .6f, .5f), 1f);
        }
        if (e.Kind == EK.AshWarden)
        {
            if (e.State == 2) { float k = 1 - e.T2 / .9f; c.DrawArc(e.Pos, 72, 0, Mathf.Tau, 40, new Color(1f, .5f, .2f, .4f + .4f * k), 2f); c.DrawArc(e.Pos, 72 * k, 0, Mathf.Tau, 36, new Color(1f, .85f, .5f, .5f), 1f); }
            if (e.State == 5) c.DrawLine(e.Pos, e.Pos + e.DashDir * 110, new Color(1f, .5f, .2f, .6f), 2f);
            if (e.State == 3 || e.State == 1 || e.State == 4) c.DrawArc(e.Pos, 20 + 6 * Mathf.Sin(Time * 20), 0, Mathf.Tau, 16, new Color(1f, .6f, .3f, .7f), 1.5f);
        }
        if (e.Kind == EK.Spirit && e.Tele > 0) c.DrawCircle(e.Pos + new Vector2(0, -2), 3 + 3 * (1 - e.Tele / .5f), new Color(1f, .5f, .2f, .7f));
    }
}
