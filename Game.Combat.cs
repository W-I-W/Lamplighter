using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

public enum EK { Shade, Wraith, Spitter, Swarm, Bomber, Warden, Eel, Leech, Beetle, DepthWarden, Imp, Golem, Spirit, AshWarden }

public sealed class Enemy
{
    public EK Kind; public Vector2 Pos, Kb, DashDir, Face = new(1, 0); public Vector2? Home; public Poi Guard;
    public float Hp, MaxHp, Atk, Flash, Phase, Age, Cd = 1.5f, T1, T2, T3, Dash, Tele, Scale = 1f, Burn;
    public int State; public bool Hidden, Elite, Carrier; public Vector2 Mark; public float Slow, Freeze; public GEvent Ev;
    public bool IsBoss => Kind == EK.Warden || Kind == EK.DepthWarden || Kind == EK.AshWarden;
    public float Radius => Kind switch
    {
        EK.Warden => 14, EK.DepthWarden => 15, EK.AshWarden => 15, EK.Swarm => 3, EK.Bomber => 6, EK.Shade => 6, EK.Beetle => 7, EK.Golem => 9, EK.Leech => 4, _ => 5
    };
}

public partial class Game
{
    public List<Enemy> Enemies = new();
    public List<Proj> Projs = new();
    public List<Beacon> Beacons = new();
    public Enemy Boss;
    public float DashCd, SpinCd, FlareCd, PulseCd, SpinT, DashT;
    Vector2 DashDir, LastMoveDir;
    float spawnT = 4;

    float DangerScale => 1f + .18f * LitCount + .12f * LitIn(1) + .12f * LitIn(2) + (BossDown(1) ? .1f : 0) + (BossDown(2) ? .1f : 0);

    public static string EnemyName(EK k) => k switch
    {
        EK.Shade => "Тень", EK.Wraith => "Призрак", EK.Spitter => "Плевун", EK.Swarm => "Мрак-рой", EK.Bomber => "Подрывник", EK.Warden => "Хранитель Тёмного леса",
        EK.Eel => "Глубинный угорь", EK.Leech => "Светоед", EK.Beetle => "Кристальный жук", EK.DepthWarden => "Хранитель глубин",
        EK.Imp => "Огненный бес", EK.Golem => "Пепельный голем", EK.Spirit => "Дух пепла", _ => "Пепельный страж"
    };

    // ───────────────────────── способности ─────────────────────────
    void TryDash()
    {
        if (!Has(Tech.Dash) || DashCd > 0 || DashT > 0) return;
        var d = LastMoveDir.LengthSquared() > .01f ? LastMoveDir : (Aim - PPos).Normalized();
        if (d.LengthSquared() < .01f) d = new Vector2(Facing, 0);
        DashDir = d; DashT = .18f; DashCd = 1.6f; Invuln = Mathf.Max(Invuln, .3f); Sfx.Play("dash"); RelicOnDash();
    }

    void TrySpin()
    {
        if (!Has(Tech.Spin) || SpinCd > 0) return;
        SpinCd = 3f; SpinT = .25f; Sfx.Play("spin"); Swing = .22f;
        foreach (var e in new List<Enemy>(Enemies))
            if (!e.Hidden && e.Pos.DistanceTo(PPos) < 32 + e.Radius) Hurt(e, PlayerDamage * 1.2f, (e.Pos - PPos).Normalized(), true);
        Burst(PPos, new Color("#fff2b0"), 12, 80);
    }

    void TryFlare()
    {
        if (!Has(Tech.Flare) || FlareCd > 0) return;
        var d = Aim - PPos; if (d.Length() > 110) d = d.Normalized() * 110;
        Beacons.Add(new Beacon { Pos = PPos + d, Dur = 10f, R = 72, Burn = 6f });
        FlareCd = 8f; Sfx.Play("flare"); Burst(PPos + d, new Color("#ffd86b"), 14, 70);
    }

    void TryPulse()
    {
        if (!Has(Tech.Pulse) || PulseCd > 0) return;
        PulseCd = 12f; Sfx.Play("pulse"); Shake = 5; Flash = .3f; LightBlockT = 0; RelicOnPulse();
        Beacons.Add(new Beacon { Pos = PPos, Dur = 1.8f, R = 170, Ring = 1f });
        foreach (var e in new List<Enemy>(Enemies))
            if (!e.Hidden && e.Pos.DistanceTo(PPos) < 170)
            {
                var d = (e.Pos - PPos).Normalized();
                Hurt(e, 8f, d); e.Kb += d * (e.IsBoss ? 20f : 200f);
            }
        for (int i = Projs.Count - 1; i >= 0; i--) if (Projs[i].Pos.DistanceTo(PPos) < 170) Projs.RemoveAt(i);
        Burst(PPos, new Color("#ffe9a0"), 30, 160);
    }

    public float BeaconR(Beacon b)
    {
        if (b.Ring > 0) return b.R * Ease(b.T / .7f) * (1 - Mathf.Pow(b.T / b.Dur, 2));
        return b.R * Mathf.Min(1, b.T / .3f) * Mathf.Min(1, (b.Dur - b.T) / 1f);
    }

    void UpdateBeacons(float dt)
    {
        for (int i = Beacons.Count - 1; i >= 0; i--)
        {
            var b = Beacons[i]; b.T += dt;
            if (b.T >= b.Dur) { Beacons.RemoveAt(i); continue; }
            if (b.Burn > 0)
                foreach (var e in Enemies)
                    if (!e.Hidden && e.Pos.DistanceTo(b.Pos) < BeaconR(b) * .85f) { e.Hp -= b.Burn * dt * (e.IsBoss ? .3f : 1f); e.Flash = Mathf.Max(e.Flash, .04f); }
        }
    }

    // ───────────────────────── спавн ─────────────────────────
    bool LineClear(Vector2 a, Vector2 b)
    {
        float len = a.DistanceTo(b);
        for (float s = 8; s < len; s += 8) { var t = ToTile(a.Lerp(b, s / len)); if (IsWall(W.Get(t.X, t.Y))) return false; }
        return true;
    }

    void UpdateSpawning(float dt)
    {
        if (IsLit(PPos)) return;
        int reg = RegionAt(PPos);
        float nearUnlit = float.MaxValue;
        for (int i = 1; i < W.Ancients.Count; i++) if (!W.Ancients[i].Lit && !W.Ancients[i].Boss && W.Ancients[i].Region == reg) nearUnlit = Mathf.Min(nearUnlit, W.Ancients[i].Pos.DistanceTo(PPos));
        bool guarded = nearUnlit < 9 * TS;
        int free = 0; foreach (var e in Enemies) if (e.Guard == null && !e.IsBoss) free++;
        int baseCap = reg == 0 ? 3 + LitCount * 2 : 4 + LitIn(reg) * 2;
        int cap = Math.Min(baseCap + (guarded ? 3 : 0), 12);
        spawnT -= dt;
        if (spawnT > 0) return;
        spawnT = rng.RandfRange(2.2f, 3.8f) * (guarded ? .55f : 1f);
        if (free >= cap) return;
        for (int tries = 0; tries < 24; tries++)
        {
            float a = rng.Randf() * Mathf.Tau, d = rng.RandfRange(130, 200);
            var p = PPos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
            var t = ToTile(p);
            if (!W.Walkable(t.X, t.Y) || IsLit(p) || RegionAt(p) != reg) continue;
            if (reg != 0 && !LineClear(PPos, p)) continue;
            var k = PickKind(p, reg);
            if (k == EK.Eel && !WaterAt(p)) continue;
            if (k == EK.Swarm) for (int s = 0; s < 4; s++) SpawnEnemy(p + new Vector2(rng.RandfRange(-8, 8), rng.RandfRange(-8, 8)), k);
            else SpawnEnemy(p, k);
            return;
        }
    }

    EK PickKind(Vector2 p, int reg)
    {
        float[] w; EK[] kinds;
        if (reg == 1)
        {
            kinds = new[] { EK.Leech, EK.Beetle, EK.Eel, EK.Spitter, EK.Swarm };
            w = new float[] { 4, 3, WaterNear(p) ? 4 : 0, 1.2f, LitIn(1) >= 1 ? 1.5f : 0 };
        }
        else if (reg == 2)
        {
            kinds = new[] { EK.Imp, EK.Golem, EK.Spirit, EK.Bomber };
            w = new float[] { 4, 2.5f, 3, 1 };
        }
        else
        {
            float distC = p.DistanceTo(TileCenter(World.N / 2, World.N / 2)) / TS;
            kinds = new[] { EK.Shade, EK.Wraith, EK.Spitter, EK.Swarm, EK.Bomber };
            w = new float[] { 5, 3, distC > 22 ? 2.5f : 0, LitCount >= 1 ? 2 : 0, LitCount >= 2 ? 2 : 0 };
        }
        float sum = 0; foreach (var x in w) sum += x;
        float r = rng.Randf() * sum;
        for (int i = 0; i < w.Length; i++) { r -= w[i]; if (r <= 0) return kinds[i]; }
        return kinds[0];
    }

    bool WaterNear(Vector2 p)
    {
        var t = ToTile(p);
        for (int y = -3; y <= 3; y++) for (int x = -3; x <= 3; x++) if (World.In(t.X + x, t.Y + y) && W.Ground[World.Key(t.X + x, t.Y + y)] == Art.WaterG) return true;
        return false;
    }

    static float BaseHp(EK k) => k switch
    {
        EK.Shade => 7f, EK.Wraith => 3.5f, EK.Spitter => 4.5f, EK.Swarm => 1.3f, EK.Bomber => 5f, EK.Warden => 170f,
        EK.Eel => 6f, EK.Leech => 4f, EK.Beetle => 12f, EK.DepthWarden => 260f, EK.Imp => 5f, EK.Golem => 22f, EK.Spirit => 6f, _ => 300f
    };

    public Enemy SpawnEnemy(Vector2 p, EK k, Poi guard = null, float scale = -1)
    {
        if (scale < 0) scale = DangerScale;
        bool boss = k == EK.Warden || k == EK.DepthWarden || k == EK.AshWarden;
        float hp = BaseHp(k) * (boss ? 1f : scale);
        var e = new Enemy { Kind = k, Pos = p, Hp = hp, MaxHp = hp, Phase = rng.Randf() * 6.28f, Scale = scale, Guard = guard, Cd = rng.RandfRange(.8f, 2f) };
        if (guard != null) e.Home = p;
        if (boss) { e.Home = p; e.T1 = 2.5f; e.T3 = 4f; }
        Enemies.Add(e); return e;
    }

    // ───────────────────────── боссы ─────────────────────────
    float bossT;
    void UpdateBossSpawn(float dt)
    {
        if (Boss != null && !Enemies.Contains(Boss)) Boss = null;
        bossT -= dt; if (bossT > 0) return; bossT = .3f;
        foreach (var a in W.Ancients)
        {
            if (!a.Boss || a.Lit || BossDown(a.BossId)) continue;
            EK k = a.BossId == 1 ? EK.Warden : a.BossId == 2 ? EK.DepthWarden : EK.AshWarden;
            bool exists = false; foreach (var e in Enemies) if (e.Kind == k) { exists = true; break; }
            if (exists) continue;
            if (PPos.DistanceTo(a.Pos) < 13 * TS)
            {
                Boss = SpawnEnemy(a.Pos + new Vector2(0, -36), k, null, DangerScale);
                Announce(EnemyName(k) + " пробудился!", a.BossId == 2 ? "Вода и тьма — его стихия. Береги свет!" : a.BossId == 3 ? "Огонь и дым — его оружие" : "Он охраняет особый древний фонарь. Побеждай — и получишь силу света", new Color("#d58cff"), "boss");
                Shake = 4;
            }
        }
        if (Boss == null) foreach (var e in Enemies) if (e.IsBoss) { Boss = e; break; }
    }

    void BossKilled(Enemy e)
    {
        int id = e.Kind == EK.Warden ? 1 : e.Kind == EK.DepthWarden ? 2 : 3;
        BossMask |= 1 << (id - 1); if (Boss == e) Boss = null;
        GiveWeapon(id == 1 ? "sword_u" : id == 2 ? "bow_u" : "staff_u");
        GiveRelic(id == 1 ? "r_dawnprism" : id == 2 ? "r_winterheart" : "r_emberheart");
        Give(It.Core, 1, e.Pos + new Vector2(0, -20));
        Give(It.Shard, 6, e.Pos); Give(It.Relic, 2, e.Pos + new Vector2(0, -8));
        string title, sub;
        if (id == 1)
        {
            TechMask |= 1 << (int)Tech.Pulse; Give(It.Aether, 3, e.Pos + new Vector2(0, -16));
            title = "Хранитель Тёмного леса повержен!"; sub = "Первое ядро света добыто! Световой импульс [F] — улучшите Центральный маяк у центра";
        }
        else if (id == 2)
        {
            TechMask |= 1 << (int)Tech.Beam; Give(It.Crystal, 4, e.Pos + new Vector2(0, -16)); Give(It.Silver, 3, e.Pos + new Vector2(0, -24));
            title = "Хранитель глубин повержен!"; sub = "Второе ядро света и ПРОЖЕКТОР [R]: луч, что отражается зеркалами. Улучшите маяк — откроются пустоши";
        }
        else
        {
            TechMask |= 1 << (int)Tech.Fireproof; Give(It.Ember, 4, e.Pos + new Vector2(0, -16)); Give(It.Obsidian, 3, e.Pos + new Vector2(0, -24));
            title = "Пепельный страж повержен!"; sub = "Третье ядро света и огнестойкость: пепел и дым больше не вредят";
        }
        GiveXp(150 + 50 * id); Flash = .8f; Shake = 8;
        Burst(e.Pos, new Color("#d58cff"), 50, 180);
        Announce(title, sub, new Color("#ffe9a0"), "ancient");
        Save();
    }

    // ───────────────────────── урон ─────────────────────────
    static bool SoulKind(EK k) => k == EK.Shade || k == EK.Wraith || k == EK.Swarm || k == EK.Leech || k == EK.Eel || k == EK.Spirit || k == EK.Spitter;

    public void Hurt(Enemy e, float dmg, Vector2 dir, bool melee = false)
    {
        if (e.Hidden) return;
        float m = 1f;
        if (melee && Has(Tech.SilverEdge) && SoulKind(e.Kind)) m *= 1.5f;
        if (melee && e.Kind == EK.Beetle)
        {
            float cosA = e.Face.Normalized().Dot((PPos - e.Pos).Normalized());
            if (cosA > .35f) { m *= Has(Tech.SilverEdge) ? .6f : .2f; Burst(e.Pos + e.Face * 6, new Color("#e4f4ff"), 4, 60); Sfx.Play("tick", -2, .6f); }
        }
        dmg *= m;
        e.Hp -= dmg; e.Flash = .15f; e.Kb += dir * (e.IsBoss ? 14f : e.Kind == EK.Wraith ? 160f : e.Kind == EK.Golem || e.Kind == EK.Beetle ? 50f : 120f);
        if (e.Kind == EK.Golem && rng.Randf() < .6f) AddZone(e.Pos, 40, 0, 3.5f, 0f, true);
        if (melee && Has(Tech.FlameEdge)) e.Burn = 3f;
        if (melee && Tier >= 4) e.Burn = Mathf.Max(e.Burn, 2f);
        Burst(e.Pos, new Color("#c070ff"), 6, 70);
        Texts.Add(new FText { P = e.Pos + new Vector2(0, -8 - e.Radius), S = ((int)Mathf.Round(dmg)).ToString(), Life = .6f, C = m < 1 ? new Color("#9aa6bf") : new Color("#ffd0d0") });
        Shake = Mathf.Max(Shake, 1.2f);
    }

    public void HurtPlayer(float dmg, Vector2 kb, bool fire = false)
    {
        if (Window == Win.Trade) Window = Win.None;
        if (Invuln > 0) return;
        if (fire && Has(Tech.Fireproof)) return;
        if (fire && Has(Tech.HeatShield)) dmg *= .4f;
        if (fire) dmg *= 1f - N("HeatResist");
        Hp -= dmg; Invuln = .6f; regenDelay = 4f; Shake = 3f;
        pVel += kb * 120f; Sfx.Play("hurt");
        Burst(PPos, fire ? new Color("#ff9a2e") : new Color("#ff5050"), 8, 80);
        Texts.Add(new FText { P = PPos + new Vector2(0, -12), S = "-" + (int)Mathf.Round(dmg), Life = .8f, C = new Color("#ff6060") });
        if (Hp <= 0) Die();
    }

    void Die()
    {
        EventsOnDeath();
        Enemies.Clear(); Projs.Clear(); Boss = null; LightBlockT = 0; LeechT = 0; Zones.Clear();
        int lost = Inv[It.Shard] / 2; Inv[It.Shard] -= lost;
        PPos = new Vector2((W.Spawn.X + .5f) * TS, (W.Spawn.Y + .5f) * TS); Hp = MaxHp; Invuln = 1.5f;
        Toast(lost > 0 ? $"Тьма поглотила вас. Вы очнулись у маяка (потеряно осколков: {lost})" : "Тьма поглотила вас. Вы очнулись у маяка");
        cam.ResetSmoothing();
    }

    void KillEnemy(Enemy e)
    {
        Kills++;
        if (e.Ev != null) EvEnemyKilled(e);
        if (e.IsBoss) { BossKilled(e); return; }
        float xp = e.Kind switch { EK.Shade => 15, EK.Wraith => 12, EK.Spitter => 18, EK.Bomber => 16, EK.Eel => 22, EK.Leech => 14, EK.Beetle => 26, EK.Imp => 24, EK.Golem => 40, EK.Spirit => 26, _ => 3 };
        float chance = e.Kind switch { EK.Shade => .7f, EK.Wraith => .45f, EK.Spitter => .6f, EK.Bomber => .5f, EK.Eel => .6f, EK.Leech => .5f, EK.Beetle => .7f, EK.Imp => .6f, EK.Golem => .9f, EK.Spirit => .6f, _ => .12f };
        GiveXp(xp);
        Burst(e.Pos, new Color("#a060ff"), e.Kind == EK.Swarm ? 5 : 14, 90);
        if (E(Fxk.Blast) > 0 && !inBlast) BlastAt(e.Pos, CurDef != null ? WeaponDmg(CurDef) : PlayerDamage);
        if (N("KillHeal") > 0) Hp = Mathf.Min(MaxHp, Hp + N("KillHeal"));
        if (Has(Tech.Reaper)) { Hp = Mathf.Min(MaxHp, Hp + 3); Texts.Add(new FText { P = PPos + new Vector2(0, -14), S = "+3", Life = .7f, C = new Color("#7ad45a") }); }
        if (rng.Randf() < chance) Give(It.Shard, e.Kind == EK.Golem ? 2 : 1, e.Pos);
        if (e.Kind == EK.Beetle && rng.Randf() < .3f) Give(It.Crystal, 1, e.Pos + new Vector2(0, -8));
        if (e.Kind == EK.Eel && rng.Randf() < .25f) Give(It.SilverOre, 1, e.Pos + new Vector2(0, -8));
        if ((e.Kind == EK.Imp || e.Kind == EK.Golem) && rng.Randf() < .3f) Give(It.Ember, 1, e.Pos + new Vector2(0, -8));
    }

    // ───────────────────────── ИИ врагов ─────────────────────────
    void UpdateEnemies(float dt)
    {
        int attached = 0;
        for (int i = Enemies.Count - 1; i >= 0; i--)
        {
            var e = Enemies[i];
            e.Age += dt; e.Flash = Mathf.Max(0, e.Flash - dt); e.Atk = Mathf.Max(0, e.Atk - dt);
            e.Kb *= Mathf.Max(0, 1 - 9 * dt);
            e.Slow = Mathf.Max(0, e.Slow - dt); e.Freeze = Mathf.Max(0, e.Freeze - dt);
            if (e.Burn > 0) { e.Burn -= dt; e.Hp -= 3f * dt * (e.IsBoss ? .3f : 1f); if (rng.Randf() < dt * 12) Burst(e.Pos, new Color("#ff9a2e"), 1, 25); }
            var to = PPos - e.Pos; float dist = to.Length(); var dir = dist > .01f ? to / dist : Vector2.Zero;
            var perp = new Vector2(-dir.Y, dir.X);
            bool guard = e.Guard != null || e.IsBoss || e.Ev != null;
            if (!guard && dist > 380) { Enemies.RemoveAt(i); continue; }

            bool lit = !e.IsBoss && IsLit(e.Pos);
            if (lit)
            {
                e.Hp -= 5f * dt; e.Flash = .05f;
                if (rng.Randf() < dt * 20) Burst(e.Pos, new Color("#ffe9a0"), 1, 30);
                if (e.Hp <= 0) { Burst(e.Pos, new Color("#ffe9a0"), 10, 60); Enemies.RemoveAt(i); continue; }
            }
            float auraR = PlayerLightR * (Has(Tech.HeroLamp) ? .55f : .6f);
            if ((Has(Tech.Searing) || Has(Tech.HeroLamp) || HeroLampLv >= 2) && dist < auraR && !e.Hidden)
            {
                e.Hp -= (Has(Tech.HeroLamp) ? 4.5f : 2.5f) * dt * (e.IsBoss ? .3f : 1f); e.Flash = Mathf.Max(e.Flash, .04f);
                if (rng.Randf() < dt * 10) Burst(e.Pos, new Color("#ffe27a"), 1, 25);
            }
            float k = lit ? .5f : 1f;
            bool water = WaterAt(e.Pos);
            bool aquatic = e.Kind == EK.Eel || e.Kind == EK.DepthWarden;
            float wm = aquatic ? (water ? 1.4f : .45f) : (water ? .65f : 1f);
            bool aggro = e.IsBoss ? (e.Home.HasValue && PPos.DistanceTo(e.Home.Value) < 20 * TS) : (!guard || dist < 150);
            Vector2 vel = Vector2.Zero;
            e.Hidden = false;

            if (!aggro)
            {
                if (e.Home.HasValue) { var h = e.Home.Value - e.Pos; if (h.Length() > 20) vel = h.Normalized() * 28; }
            }
            else if (e.Freeze > 0) { }
            else switch (e.Kind)
            {
                case EK.Shade:
                    vel = dir * (36f + 2f * LitCount) * k;
                    if (dist < 12 && e.Atk <= 0) { e.Atk = .9f; HurtPlayer(9f * e.Scale, -dir); }
                    break;
                case EK.Wraith:
                    e.Cd -= dt;
                    if (e.Tele > 0) { e.Tele -= dt; if (e.Tele <= 0) { e.Dash = .32f; e.DashDir = dir; } }
                    else if (e.Dash > 0)
                    {
                        e.Dash -= dt; vel = e.DashDir * 170f * k;
                        if (dist < 12 && e.Atk <= 0) { e.Atk = .9f; HurtPlayer(7f * e.Scale, -dir); }
                    }
                    else
                    {
                        vel = (dir + perp * Mathf.Sin(Time * 3f + e.Phase) * .8f).Normalized() * 58f * k;
                        if (e.Cd <= 0 && dist < 100) { e.Tele = .35f; e.Cd = rng.RandfRange(2.2f, 3.2f); }
                    }
                    break;
                case EK.Spitter:
                    e.Cd -= dt;
                    if (e.Tele > 0)
                    {
                        e.Tele -= dt;
                        if (e.Tele <= 0) { Projs.Add(new Proj { Pos = e.Pos, Vel = dir * 100f, Dmg = 7f * e.Scale }); Sfx.Play("shoot", -5); e.Cd = rng.RandfRange(2f, 3f); }
                    }
                    else
                    {
                        if (dist > 95) vel = dir * 42f; else if (dist < 60) vel = -dir * 42f; else vel = perp * Mathf.Sin(Time * 1.5f + e.Phase) * 20f;
                        vel *= k;
                        if (e.Cd <= 0 && dist < 150) e.Tele = .4f;
                    }
                    break;
                case EK.Swarm:
                    vel = (dir + perp * Mathf.Sin(Time * 6f + e.Phase) * .6f).Normalized() * 75f * k;
                    if (dist < 9 && e.Atk <= 0) { e.Atk = .7f; HurtPlayer(3.5f * e.Scale, -dir); }
                    break;
                case EK.Bomber:
                    if (e.State == 0) { vel = dir * 55f * k; if (dist < 26) { e.State = 1; e.T1 = 1f; } }
                    else
                    {
                        e.T1 -= dt; e.Flash = ((int)(Time * 16) % 2 == 0) ? .05f : 0;
                        if (e.T1 <= 0) { Explode(e); Enemies.RemoveAt(i); continue; }
                    }
                    break;
                case EK.Warden: vel = UpdateWarden(e, dt, dir, dist); break;
                case EK.DepthWarden: vel = UpdateDepthWarden(e, dt, dir, dist, water); break;
                case EK.Eel:
                    e.Cd -= dt;
                    if (e.Tele > 0) { e.Tele -= dt; if (e.Tele <= 0) { e.Dash = .28f; e.DashDir = dir; } }
                    else if (e.Dash > 0)
                    {
                        e.Dash -= dt; vel = e.DashDir * 150f * wm;
                        if (dist < 13 && e.Atk <= 0) { e.Atk = .9f; HurtPlayer(8f * e.Scale, -dir); }
                    }
                    else
                    {
                        vel = (dir + perp * Mathf.Sin(Time * 2.2f + e.Phase) * .5f).Normalized() * 58f * wm * k;
                        if (e.Cd <= 0 && dist < 40) { e.Tele = .45f; e.Cd = rng.RandfRange(1.6f, 2.4f); }
                        if (!water) { e.Hp -= 1.5f * dt; }
                    }
                    e.Hidden = water && e.Tele <= 0 && e.Dash <= 0 && dist > 22;
                    break;
                case EK.Leech:
                    if (e.State == 0)
                    {
                        vel = (dir + perp * Mathf.Sin(Time * 5f + e.Phase) * .5f).Normalized() * 60f * k;
                        if (dist < 9) { e.State = 1; e.Mark = new Vector2(rng.RandfRange(-5, 5), rng.RandfRange(-6, 2)); }
                    }
                    else
                    {
                        attached++;
                        e.Pos = PPos + e.Mark; vel = Vector2.Zero; LeechT = .3f;
                        e.T1 -= dt; if (e.T1 <= 0) { e.T1 = 1f; HurtPlayer(2.5f * e.Scale, Vector2.Zero); Invuln = 0; }
                        if (DashT > 0) { e.State = 0; e.Kb += -dir * 200f; e.Cd = 1.5f; }
                    }
                    break;
                case EK.Beetle:
                    {
                        float want = dir.Angle(), cur = e.Face.Angle();
                        float diff = Mathf.Wrap(want - cur, -Mathf.Pi, Mathf.Pi);
                        e.Face = Vector2.FromAngle(cur + Mathf.Clamp(diff, -2.2f * dt, 2.2f * dt));
                        vel = e.Face * 32f * wm * k;
                        if (dist < 14 && e.Atk <= 0) { e.Atk = .9f; HurtPlayer(11f * e.Scale, -dir); }
                    }
                    break;
                default: vel = UpdateAshEnemy(e, dt, dir, dist, perp, k); break;
            }
            if (e.Kind == EK.Leech && e.State == 1) { }
            else { var np = Move(e.Pos, ((e.Slow > 0 ? vel * (e.IsBoss ? .75f : .5f) : vel) * (e.Kind == EK.Eel || e.Kind == EK.DepthWarden || e.Kind == EK.Beetle ? 1f : wm) + e.Kb) * dt, Mathf.Min(5f, e.Radius), e.Kind == EK.Wraith || e.Kind == EK.Warden || e.Kind == EK.DepthWarden || e.Kind == EK.AshWarden || e.Kind == EK.Spirit);
                if (!e.IsBoss && Barriers.Count > 0 && BarrierCross(e.Pos, np)) { e.Kb = Vector2.Zero; } else e.Pos = np; }

            if (!e.IsBoss && e.Kind != EK.Leech)
                foreach (var o in Enemies) if (o != e && !o.IsBoss) { var dd = e.Pos - o.Pos; float l = dd.Length(); if (l > .01f && l < 8) e.Pos += dd / l * (8 - l) * .3f; }

            if (e.Hp <= 0) { var dead = e; Enemies.RemoveAt(i); KillEnemy(dead); }
        }
        if (attached == 0) LeechT = Mathf.Max(0, LeechT - dt);
    }

    void Explode(Enemy e)
    {
        Burst(e.Pos, new Color("#ff9a2e"), 24, 130); Burst(e.Pos, new Color("#ffe27a"), 10, 80);
        Sfx.Play("boom"); Shake = Mathf.Max(Shake, 4);
        if (PPos.DistanceTo(e.Pos) < 38) HurtPlayer(18f * e.Scale, (PPos - e.Pos).Normalized() * 2f, e.Kind != EK.Bomber || RegionAt(e.Pos) == 2);
    }

    Vector2 UpdateWarden(Enemy e, float dt, Vector2 dir, float dist)
    {
        float hpf = e.Hp / e.MaxHp, mul = hpf < .33f ? .65f : 1f;
        Vector2 vel = Vector2.Zero;
        switch (e.State)
        {
            case 0:
                vel = dir * (hpf < .33f ? 48f : 40f); e.T1 -= dt;
                if (dist < 22 && e.Atk <= 0) { e.Atk = 1f; HurtPlayer(12f, -dir); }
                if (hpf < .33f) { e.T3 -= dt; if (e.T3 <= 0) { e.T3 = 4f; RingShot(e.Pos, 8, new Color("#d58cff")); } }
                if (e.T1 <= 0)
                {
                    float r = rng.Randf();
                    if (hpf < .66f && r < .33f) { e.State = 5; e.T2 = .8f; }
                    else if (r < .66f) { e.State = 1; e.T2 = .9f; }
                    else { e.State = 2; e.T2 = .6f; e.DashDir = dir; }
                }
                break;
            case 1:
                e.T2 -= dt;
                if (e.T2 <= 0)
                {
                    Shake = 6; Sfx.Play("boom");
                    for (int i = 0; i < 28; i++) { float a = i * Mathf.Tau / 28; Parts.Add(new Particle { P = e.Pos, V = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 120, Life = .5f, Max = .5f, C = new Color("#d58cff"), Size = 2 }); }
                    if (dist < 64) HurtPlayer(22f, -dir);
                    e.State = 0; e.T1 = 2.2f * mul;
                }
                break;
            case 2:
                e.T2 -= dt; e.DashDir = dir;
                if (e.T2 <= 0) { e.State = 3; e.T2 = .5f; Sfx.Play("dash", 2, .7f); }
                break;
            case 3:
                vel = e.DashDir * 200f; e.T2 -= dt;
                if (dist < 20 && e.Atk <= 0) { e.Atk = 1f; HurtPlayer(18f, -dir); }
                if (e.T2 <= 0) { e.State = 4; e.T2 = .8f; }
                break;
            case 4:
                e.T2 -= dt; if (e.T2 <= 0) { e.State = 0; e.T1 = 1.8f * mul; }
                break;
            case 5:
                e.T2 -= dt;
                if (e.T2 <= 0)
                {
                    for (int i = 0; i < 3; i++) SpawnEnemy(e.Pos + new Vector2(rng.RandfRange(-20, 20), rng.RandfRange(-20, 20)), EK.Swarm, null, 1.2f);
                    Burst(e.Pos, new Color("#a060ff"), 16, 90); e.State = 0; e.T1 = 3f * mul;
                }
                break;
        }
        return vel;
    }

    /// <summary>Хранитель глубин: ныряет, бьёт приливом, призывает существ и гасит свет.</summary>
    Vector2 UpdateDepthWarden(Enemy e, float dt, Vector2 dir, float dist, bool water)
    {
        float hpf = e.Hp / e.MaxHp, mul = hpf < .33f ? .6f : 1f;
        Vector2 vel = Vector2.Zero; float wm = water ? 1.4f : .6f;
        switch (e.State)
        {
            case 0:     // плывёт к игроку
                vel = dir * 34f * wm; e.T1 -= dt;
                if (dist < 24 && e.Atk <= 0) { e.Atk = 1f; HurtPlayer(10f, -dir); }
                if (e.T1 <= 0)
                {
                    float r = rng.Randf();
                    if (hpf < .66f && r < .22f) { e.State = 5; e.T2 = 1f; }
                    else if (hpf < .66f && r < .5f) { e.State = 6; e.T2 = 1.2f; }
                    else if (r < .75f) { e.State = 1; e.T2 = 2.2f * mul; }
                    else { e.State = 4; e.T2 = 1f; e.Mark = PPos; }
                }
                break;
            case 1:     // нырок: невидим и неуязвим, быстро плывёт под игроком
                e.Hidden = true; vel = dir * 95f; e.T2 -= dt;
                if (rng.Randf() < dt * 8) Burst(e.Pos, new Color("#9fd8ff"), 1, 25);
                if (e.T2 <= 0 || dist < 18) { e.State = 2; e.T2 = .35f; }
                break;
            case 2:     // всплытие
                e.T2 -= dt;
                if (e.T2 <= 0)
                {
                    Shake = 6; Sfx.Play("boom");
                    for (int i = 0; i < 26; i++) { float a = i * Mathf.Tau / 26; Parts.Add(new Particle { P = e.Pos, V = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 110, Life = .5f, Max = .5f, C = new Color("#9fd8ff"), Size = 2 }); }
                    if (dist < 46) HurtPlayer(18f, -dir);
                    e.State = 3; e.T2 = 1.1f;
                }
                break;
            case 3:     // оглушён и уязвим
                e.T2 -= dt; if (e.T2 <= 0) { e.State = 0; e.T1 = 1.6f * mul; }
                break;
            case 4:     // прилив по отмеченной точке
                e.T2 -= dt;
                if (e.T2 <= 0)
                {
                    Shake = 5; Sfx.Play("boom", -2, 1.2f);
                    for (int i = 0; i < 20; i++) { float a = i * Mathf.Tau / 20; Parts.Add(new Particle { P = e.Mark, V = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 90, Life = .5f, Max = .5f, C = new Color("#7ac8ff"), Size = 2 }); }
                    if (PPos.DistanceTo(e.Mark) < 52) HurtPlayer(20f, (PPos - e.Mark).Normalized());
                    RingShot(e.Pos, hpf < .5f ? 10 : 6, new Color("#7ac8ff"));
                    e.State = 0; e.T1 = 2f * mul;
                }
                break;
            case 5:     // призыв
                e.T2 -= dt;
                if (e.T2 <= 0)
                {
                    SpawnEnemy(e.Pos + new Vector2(rng.RandfRange(-30, 30), rng.RandfRange(-20, 20)), EK.Eel, null, 1.2f);
                    SpawnEnemy(e.Pos + new Vector2(rng.RandfRange(-30, 30), rng.RandfRange(-20, 20)), EK.Leech, null, 1.2f);
                    if (hpf < .4f) SpawnEnemy(e.Pos + new Vector2(rng.RandfRange(-30, 30), rng.RandfRange(-20, 20)), EK.Leech, null, 1.2f);
                    Burst(e.Pos, new Color("#7ac8ff"), 16, 90); Sfx.Play("boss", -4, 1.3f); e.State = 0; e.T1 = 2.6f * mul;
                }
                break;
            case 6:     // тьма: гасит источники света
                e.T2 -= dt;
                if (e.T2 <= 0)
                {
                    LightBlockT = hpf < .4f ? 10f : 7f; Sfx.Play("boss", 0, .6f); Shake = 4;
                    Announce("Свет погас!", "Хранитель глубин гасит фонари — сигнальная ракета [Q] и импульс [F] ещё горят", new Color("#7ac8ff"), "boss");
                    e.State = 0; e.T1 = 2.4f * mul;
                }
                break;
        }
        return vel;
    }

    void RingShot(Vector2 p, int n, Color c, bool fire = false)
    {
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.Tau / n + Time;
            Projs.Add(new Proj { Pos = p, Vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 80f, Dmg = 8f, C = c, Fire = fire });
        }
        Sfx.Play("shoot", -2, .7f);
    }

    void UpdateProjectiles(float dt)
    {
        for (int i = Projs.Count - 1; i >= 0; i--)
        {
            var p = Projs[i]; p.Life -= dt; p.Pos += p.Vel * dt;
            var t = ToTile(p.Pos);
            if (p.Life <= 0 || World.Blocks(W.Get(t.X, t.Y)) && W.Get(t.X, t.Y) != Tl.Deep) { Burst(p.Pos, p.C, 3, 30); if (p.Zone) AddZone(p.Pos, 22, .3f, 4f, 6f); Projs.RemoveAt(i); continue; }
            if (p.Pos.DistanceTo(PPos) < 6f)
            {
                if (Invuln <= 0) { HurtPlayer(p.Dmg, p.Vel.Normalized() * .8f, p.Fire); if (p.Zone) AddZone(p.Pos, 22, .3f, 4f, 6f); Projs.RemoveAt(i); }
            }
        }
    }

    // ───────────────────────── рисование врагов ─────────────────────────
    void DrawEnemy(EntityLayer c, Enemy e)
    {
        float sc = (e.Elite ? 1.25f : 1f) * e.Kind switch { EK.Warden => 2.6f, EK.DepthWarden => 2.8f, EK.AshWarden => 2.8f, EK.Swarm => .6f, EK.Golem => 1.5f, EK.Beetle => 1.15f, EK.Leech => .8f, _ => 1f };
        if (e.Hidden)
        {
            // под водой видна только рябь
            float ph = (Time * 2 + e.Phase) % 1f;
            c.DrawArc(e.Pos, 4 + ph * 8 * (e.IsBoss ? 2 : 1), 0, Mathf.Tau, 16, new Color(.7f, .9f, 1f, .6f * (1 - ph)), 1f);
            c.DrawArc(e.Pos, 2 + ph * 4, 0, Mathf.Tau, 12, new Color(.7f, .9f, 1f, .5f * (1 - ph)), 1f);
            return;
        }
        Shadow(c, e.Pos, 5 * sc);
        var tex = e.Kind switch
        {
            EK.Wraith => Art.WraithTex, EK.Spitter => Art.SpitterTex, EK.Bomber => Art.BomberTex, EK.Eel => Art.EelTex, EK.Leech => Art.LeechTex,
            EK.Beetle => Art.BeetleTex, EK.DepthWarden => Art.DeepTex, EK.Imp => Art.ImpTex, EK.Golem => Art.GolemTex, EK.Spirit => Art.SpiritTex,
            EK.AshWarden => Art.AshBossTex, _ => Art.ShadeTex
        };
        int f = (int)(Time * 4 + e.Phase) % 2;
        float bob = Mathf.Sin(Time * 4 + e.Phase) * 1.2f - (e.Kind == EK.Wraith || e.Kind == EK.Spirit ? 2 : 0);
        float a = Mathf.Clamp(e.Age * 2f, 0, 1);
        Color mod = e.Flash > 0 ? new Color(1.8f, .6f, .6f, a) : e.Freeze > 0 ? new Color(.6f, .9f, 1.6f, a) : e.Slow > 0 ? new Color(.75f, .95f, 1.25f, a) : e.Elite ? new Color(1.3f, 1.15f, .7f, a) : new Color(1, 1, 1, a);
        if (e.Kind == EK.Warden && e.Flash <= 0) mod = new Color(1.2f, .6f, .9f, a);
        if ((e.Tele > 0 || (e.Kind == EK.Warden && (e.State == 1 || e.State == 2 || e.State == 5)) || (e.Kind == EK.DepthWarden && (e.State == 4 || e.State == 5 || e.State == 6))) && ((int)(Time * 25) % 2 == 0)) mod = new Color(1.6f, 1.6f, 2f, a);
        if (WaterAt(e.Pos) && e.Kind != EK.DepthWarden) mod.A *= .85f;
        int face = e.Kind == EK.Beetle ? (e.Face.X < 0 ? -1 : 1) : (PPos.X < e.Pos.X ? -1 : 1);
        c.DrawSetTransform(e.Pos + new Vector2(0, bob), 0, new Vector2(face * sc, sc));
        c.DrawTextureRectRegion(tex, new Rect2(-8, -10, 16, 16), new Rect2(f * 16, 0, 16, 16), mod);
        c.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        if (e.Kind == EK.Warden)
        {
            if (e.State == 1) { float k = 1 - e.T2 / .9f; c.DrawArc(e.Pos, 64, 0, Mathf.Tau, 40, new Color(.85f, .5f, 1f, .4f + .4f * k), 2f); c.DrawArc(e.Pos, 64 * k, 0, Mathf.Tau, 40, new Color(1f, .8f, 1f, .5f), 1f); }
            if (e.State == 2) c.DrawLine(e.Pos, e.Pos + e.DashDir * 90, new Color(1f, .5f, .8f, .6f), 2f);
        }
        if (e.Kind == EK.DepthWarden)
        {
            if (e.State == 4) { float k = 1 - e.T2 / 1f; c.DrawArc(e.Mark, 52, 0, Mathf.Tau, 40, new Color(.4f, .8f, 1f, .4f + .4f * k), 2f); c.DrawArc(e.Mark, 52 * k, 0, Mathf.Tau, 40, new Color(.8f, .95f, 1f, .5f), 1f); }
            if (e.State == 3) c.DrawArc(e.Pos, 10, 0, Mathf.Tau, 12, new Color(1f, 1f, .5f, .6f), 1f);
        }
        DrawAshTelegraph(c, e);
        if (!e.IsBoss && e.Hp < e.MaxHp)
        {
            var o = e.Pos + new Vector2(-6, -14 * sc);
            c.DrawRect(new Rect2(o, new Vector2(12, 1.5f)), new Color(0, 0, 0, .7f));
            c.DrawRect(new Rect2(o, new Vector2(12 * Mathf.Clamp(e.Hp / e.MaxHp, 0, 1), 1.5f)), new Color("#ff5ec8"));
        }
    }
}
