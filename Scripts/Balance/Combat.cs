using System;
using System.Collections.Generic;
using System.Linq;

namespace Lamplighter.Balance;

public sealed class Foe
{
    public EnemyM Def; public BossM Boss; public string Kind; public float Hp, MaxHp, DmgScale = 1f, Arrive, AtkT, WindT, Burn, Slow, Freeze, Open, ShieldCycle, Speed;
    public bool Elite; public int EMod; public float WardT = 3f; public float[] MoveCd; public int PendingMove = -1; public float PendingDmg, NextT = 2.5f, MeleeT = 1f, Hide;
    public bool Dead; public bool IsBoss => Boss != null; public bool Alive => !Dead && Hp > 0;
    public bool Soul => Def != null && Def.Soul; public bool Ranged => Def != null && Def.Ranged;
}

public struct CombatStats { public float DamageTaken, DamageDealt, Healed, Time; public int Kills, Dodged, Hits; public string KilledBy; public bool Died; public float XpGained; public int Shards; }

/// <summary>Покадровый бой героя с одним или несколькими врагами. Формулы урона, эффектов и реген — по Game.Combat/Weapons/Difficulty; ИИ врагов сведён к циклу «пауза → предупреждение → удар» с таймерами из кода.</summary>
public sealed class CombatSim
{
    public readonly SimPlayer P; readonly Profile pr; readonly Rng r; readonly BalanceModel M; readonly TuneM T;
    public readonly List<Foe> Foes = new(); public CombatStats S; public int Region; public bool Lit;
    float invuln, noDmgT = 99, atkCd, chillT, reactT, puzzleT, vulnT; public float Dt = .05f;
    readonly Action<string, int> onDrop;                         // (предмет, количество) — добыча за убийства

    public CombatSim(SimPlayer p, Profile prof, Rng rng, int region, bool lit = false, Action<string, int> drop = null) { P = p; pr = prof; r = rng; M = p.M; T = M.T; Region = region; Lit = lit; onDrop = drop; }

    public bool AnyContact { get { foreach (var f in Foes) if (f.Alive && f.Arrive <= 0) return true; return false; } }
    public bool AnyAlive { get { foreach (var f in Foes) if (f.Alive) return true; return false; } }
    public int Alive { get { int n = 0; foreach (var f in Foes) if (f.Alive) n++; return n; } }

    // ───────────────────────── создание врагов ─────────────────────────
    public Foe MakeFoe(EnemyM d, float danger, int elitesNow, bool allowElite = true, float distance = -1)
    {
        float hm = T.EnemyHp * T.At(T.BiomeHp, Region), dm = T.EnemyDamage * T.At(T.BiomeDamage, Region);
        var f = new Foe { Def = d, Kind = d.Kind, Hp = d.Hp * danger * hm, DmgScale = danger * dm, Speed = d.Speed * T.EnemySpeed };
        f.MaxHp = f.Hp; if (distance < 0) distance = r.Range(130, 200);
        float travel = d.Ranged ? Math.Max(0, distance - 100) : distance - 20; f.Arrive = travel / Math.Max(10f, f.Speed);
        f.AtkT = r.Range(.3f, 1.2f) + d.Period * .3f;
        if (allowElite && d.Kind != "Swarm" && d.Kind != "Bomber" && Elite(f, elitesNow)) { }
        return f;
    }

    bool Elite(Foe f, int elitesNow)
    {
        if (Region == 0 && P.LitCount < (int)T.EliteMinLit) return false;
        if (elitesNow >= (int)T.EliteMax) return false;
        if (!r.Chance(T.At(T.EliteChance, Region))) return false;
        var pool = f.Def.Ranged ? new[] { 3, 4, 2 } : new[] { 1, 2, 5, 4 };
        f.EMod = pool[r.Int(0, pool.Length - 1)]; f.Elite = true;
        if (f.EMod == 1) { f.MaxHp *= T.EliteHp; f.Hp = f.MaxHp; f.DmgScale *= T.EliteDamage; } else { f.MaxHp *= 1.35f; f.Hp = f.MaxHp; }
        return true;
    }

    public Foe MakeBoss(BossM b)
    {
        // b.Hp в модели — BaseHp; множитель босса применяется здесь (Game.EnemyMuls: BossHp[id−1] × Coop)
        var f = new Foe { Boss = b, Kind = b.Kind, Hp = b.Hp * T.At(T.BossHp, b.Id - 1), DmgScale = T.At(T.BossDamage, b.Id - 1), Arrive = 0 };
        f.MaxHp = f.Hp; f.MoveCd = new float[b.Moves.Length]; f.NextT = 2.5f;                  // e.T1 = 2.5 в SpawnEnemy
        for (int i = 0; i < f.MoveCd.Length; i++) f.MoveCd[i] = b.Moves[i].Cooldown > 0 ? 4f + i : 0f;
        return f;
    }

    // ───────────────────────── удар героя ─────────────────────────
    float DamageMul(Foe e, bool melee, bool light, out bool shielded)
    {
        float m = 1f; shielded = false;
        if (e.Hide > 0) { shielded = true; return 0f; }
        if (e.IsBoss && e.Boss.NeedsLight && e.ShieldCycle > 0 && vulnT <= 0 && e.Hp < e.MaxHp * .66f) { shielded = true; m *= .06f; }
        if (e.Kind == "Sentinel" && !light) m *= .3f;
        if (e.Open > 0) m *= 1.4f;
        if (e.EMod == 3 && e.WardT > 0) m *= .35f;
        if (melee && P.Has(Tech.SilverEdge) && e.Soul) m *= 1f + P.Se(Tech.SilverEdge);
        if (melee && e.Kind == "Beetle" && !r.Chance(pr.Flank)) m *= .2f + .08f * P.Lv(Tech.SilverEdge);
        return m;
    }

    void Hurt(Foe e, float dmg, bool melee, bool light = false, int depth = 0)
    {
        if (!e.Alive) return;
        float m = DamageMul(e, melee, light, out _); dmg *= m; e.Hp -= dmg; S.DamageDealt += dmg;
        // эффекты попадания (Game.Hurt + Game.OnHit)
        if (melee && P.Has(Tech.FlameEdge)) e.Burn = Math.Max(e.Burn, P.Se(Tech.FlameEdge));
        if (melee && P.Has(Tech.FrostEdge)) { e.Slow = Math.Max(e.Slow, P.Se(Tech.FrostEdge)); if (!e.IsBoss && P.Lv(Tech.FrostEdge) >= 3 && r.Chance(.05f * (P.Lv(Tech.FrostEdge) - 2))) e.Freeze = Math.Max(e.Freeze, 1f); }
        if (melee && P.Tier >= 4) e.Burn = Math.Max(e.Burn, 2f);
        float b = P.E(Fxk.Burn); if (b > 0 && r.Chance(.45f + .2f * b)) e.Burn = Math.Max(e.Burn, 2f + b);
        float s = P.E(Fxk.Slow); if (s > 0) e.Slow = Math.Max(e.Slow, 1.6f + s);
        float f = P.E(Fxk.Freeze); if (f > 0 && !e.IsBoss && r.Chance(.18f * f + (s > 0 ? .05f : 0))) e.Freeze = 1.1f;
        float c = P.E(Fxk.Chain);
        if (c > 0 && depth < 1)
        {
            int left = 1 + (int)c;
            foreach (var o in Foes) { if (left <= 0) break; if (o == e || !o.Alive || o.IsBoss || o.Arrive > 0) continue; Hurt(o, dmg / Math.Max(.01f, m) * .45f, false, false, depth + 1); left--; }
        }
        float sp = P.E(Fxk.Siphon); if (sp > 0) { P.FlareCd = Math.Max(0, P.FlareCd - .3f * sp); P.PulseCd = Math.Max(0, P.PulseCd - .3f * sp); P.DashCd = Math.Max(0, P.DashCd - .15f * sp); }
        if (e.Hp <= 0) Kill(e);
    }

    void Kill(Foe e)
    {
        if (e.Dead) return; e.Dead = true; S.Kills++; P.Kills++;
        if (e.IsBoss) return;
        var d = e.Def; float xp = d.Xp * (1f + .3f * Region);
        P.GiveXp(xp); S.XpGained += xp * T.XpMul;
        if (P.E(Fxk.Blast) > 0 && !inBlast) { inBlast = true; float dmg = P.Cur != null ? P.WeaponDmg(P.Cur) : P.PlayerDamage; foreach (var o in Foes) if (o.Alive && o != e && o.Arrive <= 0) Hurt(o, dmg * (.4f + .15f * P.E(Fxk.Blast)), false); inBlast = false; }
        float h0 = P.Hp;
        if (P.N("KillHeal") > 0) P.Hp = Math.Min(P.MaxHp, P.Hp + P.N("KillHeal"));
        if (P.Has(Tech.Reaper)) P.Hp = Math.Min(P.MaxHp, P.Hp + P.ReaperHeal);
        S.Healed += P.Hp - h0;
        if (r.Chance(d.ShardChance)) { int n = d.Kind == "Golem" ? 2 : 1; P.Inv[It.Shard] += n; S.Shards += n; }
        if (d.Kind == "Weaver" || d.Kind == "Stalker") if (r.Chance(.5f)) { P.Inv[It.Shard]++; S.Shards++; }
        if (d.ExtraItem != null && r.Chance(d.ExtraDrop)) P.Inv[ItemId(d.ExtraItem)]++;
        if (e.Elite && e.EMod > 0)
        {
            P.Inv[It.Shard] += 3; S.Shards += 3; P.Inv[It.Coins] += 8 + 4 * Region; P.GiveXp(30);
            if (r.Chance(.18f + .04f * Region)) P.ModInv[r.Int(0, P.ModInv.Length - 1)]++;
            if (e.EMod == 5) Explode(e);
        }
    }
    bool inBlast;

    void Explode(Foe e) { if (r.Chance(.5f)) return; DamagePlayer(18f * e.DmgScale, false, false); }

    public static int ItemId(string n) => n switch { "Crystal" => It.Crystal, "SilverOre" => It.SilverOre, "Ember" => It.Ember, "Frost" => It.Frost, _ => It.Shard };

    /// <summary>Для сверки с настоящей игрой: один удар героя по врагу через тот же конвейер.</summary>
    public void TestHit(Foe e, float dmg, bool melee) => Hurt(e, dmg, melee);

    // ───────────────────────── урон по герою ─────────────────────────
    void DamagePlayer(float dmg, bool fire, bool cold, Foe src = null)
    {
        if (invuln > 0) return;
        if (fire) { if (P.Has(Tech.Fireproof)) return; if (P.Has(Tech.HeatShield)) dmg *= P.HeatTaken; dmg *= 1f - P.N("HeatResist"); }
        dmg *= P.FearMul; P.Hp -= dmg; S.DamageTaken += dmg; S.Hits++; invuln = T.HitInvuln; noDmgT = 0;
        if (cold) chillT = Math.Max(chillT, 2.2f * (P.Has(Tech.FrostArmor) ? P.ColdTaken : 1f));
        if (P.Hp <= 0 && !S.Died) { S.Died = true; S.KilledBy = src?.Kind ?? "env"; }
    }

    /// <summary>Решение героя на предупреждение врага: замечено ли, удалось ли уклониться или рвануть (Game.TryDash: 0,2 с неуязвимости).</summary>
    bool Dodges(float windup, float flight, bool ranged, bool melee)
    {
        float skill = pr.DodgeSkill * (chillT > 0 ? .6f : 1f);
        if (windup <= 0.01f) return r.Chance(skill * .25f);
        float window = windup + (ranged ? flight * .6f : 0f) - .12f;
        float react = r.Normal(pr.ReactMean, pr.ReactSd);
        if (react >= window) return false;
        if (pr.UseDash && P.Has(Tech.Dash) && P.DashCd <= 0 && r.Chance(pr.DashSkill)) { P.DashCd = P.DashCooldown; return true; }
        return r.Chance(skill * (melee ? .85f : 1f));
    }

    /// <summary>Время без врагов: перезарядки и восстановление здоровья.</summary>
    public void Idle(float s)
    {
        float before = noDmgT; noDmgT += s; chillT -= s; P.DashCd = Math.Max(0, P.DashCd - s); P.SpinCd = Math.Max(0, P.SpinCd - s); P.FlareCd = Math.Max(0, P.FlareCd - s); P.PulseCd = Math.Max(0, P.PulseCd - s);
        if (P.Hp < P.MaxHp) P.Hp = Math.Min(P.MaxHp, P.Hp + (Lit ? 4f * s : Math.Max(0, noDmgT - Math.Max(T.RegenDelay, before)) * P.RegenPerSec));
    }

    // ───────────────────────── шаг боя ─────────────────────────
    public void Step()
    {
        float dt = Dt; S.Time += dt; P.Time += dt;
        invuln -= dt; noDmgT += dt; chillT -= dt; atkCd -= dt; reactT -= dt;
        P.DashCd = Math.Max(0, P.DashCd - dt); P.SpinCd = Math.Max(0, P.SpinCd - dt); P.FlareCd = Math.Max(0, P.FlareCd - dt); P.PulseCd = Math.Max(0, P.PulseCd - dt);
        // восстановление: в свете 4/с, в темноте 0,35/с после 5 с без урона (Game.Update)
        if (P.Hp < P.MaxHp) { if (Lit) P.Hp = Math.Min(P.MaxHp, P.Hp + 4f * dt); else if (noDmgT > T.RegenDelay) P.Hp = Math.Min(P.MaxHp, P.Hp + P.RegenPerSec * dt); }
        int contact = 0, busy = 0; Foe target = null;
        foreach (var f in Foes)
        {
            if (!f.Alive) continue;
            if (f.Arrive > 0 && f.Freeze <= 0) f.Arrive -= dt * (f.Slow > 0 ? .5f : 1f);
            if (f.Arrive <= 0) { contact++; if (target == null || f.Hp < target.Hp * (pr.Uptime > .8f ? .8f : 1.2f)) target = f; if (f.WindT > 0 && !f.IsBoss && f.Def.Kind != "Swarm") busy++; }
            if (f.Burn > 0) { f.Burn -= dt; float bd = 3f * dt * (f.IsBoss ? .3f : 1f); f.Hp -= bd; S.DamageDealt += bd; }
            f.Slow = Math.Max(0, f.Slow - dt); f.Freeze = Math.Max(0, f.Freeze - dt);
            if (f.EMod == 3) { f.WardT -= dt; if (f.WardT <= -4f) f.WardT = 3f; }
            if (f.IsBoss) BossShield(f, dt);
        }
        Reap();
        // свет героя (аура) и способности
        float aura = P.AuraDps;
        if (aura > 0) foreach (var f in Foes) if (f.Alive && f.Arrive <= 0) { float share = f.Ranged ? Math.Clamp((P.LightR * (P.Has(Tech.HeroLamp) ? .55f : .6f) - 50f) / 55f, 0f, 1f) : 1f; f.Hp -= aura * dt * share * (f.IsBoss ? .3f : 1f); S.DamageDealt += aura * dt * share; }
        Abilities(contact, target);
        PlayerAttack(target, contact);
        for (int fi = 0, fn = Foes.Count; fi < fn; fi++) { var f = Foes[fi]; if (f.Alive && f.Arrive <= 0) { if (f.IsBoss) BossAct(f, dt); else EnemyAct(f, dt, ref busy); } }
        Reap();
    }

    void Reap()
    {
        for (int i = 0; i < Foes.Count; i++) if (!Foes[i].Dead && Foes[i].Hp <= 0) Kill(Foes[i]);
        Foes.RemoveAll(f => f.Dead);
    }

    void BossShield(Foe f, float dt)
    {
        if (!f.Boss.NeedsLight) return;
        if (f.Hp >= f.MaxHp * .66f) { f.ShieldCycle = 0; return; }
        if (f.ShieldCycle == 0) { f.ShieldCycle = 1; puzzleT = PuzzleTime; vulnT = 0; }
        if (vulnT > 0) { vulnT -= dt; if (vulnT <= 0) puzzleT = PuzzleTime; return; }
        puzzleT -= dt; if (puzzleT <= 0) vulnT = f.Boss.Id == 4 ? 6f : 8f;
    }
    float PuzzleTime => pr.Name == "novice" ? 30f : pr.Name == "average" ? 16f : pr.Name == "expert" ? 10f : 8f;

    void Abilities(int contact, Foe target)
    {
        if (target == null) return;
        bool boss = target.IsBoss;
        if (pr.UseSpin && P.Has(Tech.Spin) && P.SpinCd <= 0 && (contact >= 2 || boss) && (P.Cur == null || P.Cur.Cls == (int)WClass.Sword))
        {
            P.SpinCd = 3f; float k = P.SpinScale;
            foreach (var f in Foes) if (f.Alive && f.Arrive <= 0) Hurt(f, P.PlayerDamage * 1.2f * k, true);
        }
        if (pr.UseFlare && P.Has(Tech.Flare) && P.FlareCd <= 0 && (contact >= 2 || boss)) { P.FlareCd = 8f; flareT = P.FlareDuration; }
        if (pr.UsePulse && P.Has(Tech.Pulse) && P.PulseCd <= 0 && (contact >= 3 || (boss && P.Hp < P.MaxHp * .5f)))
        {
            P.PulseCd = P.PulseCooldown;
            foreach (var f in Foes) if (f.Alive && f.Arrive <= 0) { Hurt(f, 8f, false); if (!f.IsBoss) f.AtkT += 1f; if (f.Ranged) f.WindT = 0; }
        }
        if (flareT > 0) { flareT -= Dt; foreach (var f in Foes) if (f.Alive && f.Arrive <= 0) { f.Hp -= 6f * Dt * (f.IsBoss ? .3f : 1f); S.DamageDealt += 6f * Dt; } }
    }
    float flareT;
    /// <summary>Допущение: удары боссов по площади и с упреждением уклонить труднее обычных (доля успешных уклонений, прошедших проверку реакции).</summary>
    public const float BossDodgeFactor = .8f;

    void PlayerAttack(Foe target, int contact)
    {
        var w = P.Cur; bool ranged = w != null && w.Cls != (int)WClass.Sword;
        float cd = w == null ? 1f : w.Cd;
        if (ranged) cd *= 1f - P.Se(Tech.QuickDraw);
        if (atkCd > 0) return;
        // дальнее оружие стреляет, пока враг ещё идёт
        if (ranged && target == null)
        {
            float oc = 1f + P.Se(Tech.Overcharge); float range = (w.Cls == (int)WClass.Bow ? .75f * w.Speed : .9f * w.Speed) * oc * oc;
            foreach (var f in Foes) if (f.Alive && f.Arrive > 0 && f.Arrive < range / Math.Max(10f, f.Speed)) { target = f; break; }
        }
        if (target == null) return;
        float pos = pr.Uptime * (chillT > 0 ? .8f : 1f);
        float ev = target.Def?.Evasive ?? 0f;
        if (!ranged) pos *= 1f - ev; else pos *= 1f - .25f * ev;
        if (target.IsBoss) pos = Math.Min(1f, pos * 1.1f);
        atkCd += .1f; if (!r.Chance(pos)) return; atkCd += cd - .1f;
        if (w == null) { Hurt(target, P.PlayerDamage, true); return; }
        float dmg = P.WeaponDmg(w);
        if (!ranged)
        {
            float half = w.Tier == 1 ? .9f : w.Tier == 2 ? 1.2f : 1.6f; float arc = Math.Min(1f, half / MathF.PI);
            foreach (var f in Foes.ToArray()) if (f.Alive && f.Arrive <= 0 && (f == target || r.Chance(arc))) Hurt(f, dmg, true);
            if (P.E(Fxk.Arc) > 0) for (int i = 0; i < (int)Math.Ceiling(P.E(Fxk.Arc)); i++) { Hurt(target, dmg * .55f, false, true); }
            for (int i = 0; i < (int)P.E(Fxk.Multi); i++) Hurt(target, dmg * .4f, false);
            return;
        }
        int n = 1 + (int)P.E(Fxk.Multi); int pierce = (w.Cls == (int)WClass.Bow ? w.Pierce : 0) + (int)P.E(Fxk.Pierce) + (P.Has(Tech.Piercing) ? 1 : 0);
        float shot = dmg * (n > 1 ? .75f : 1f) * (1f + P.Se(Tech.Marksman));
        for (int i = 0; i < n; i++)
        {
            if (i > 0 && !r.Chance(Math.Min(1f, pos + .1f))) continue;
            Hurt(target, shot, false);
            int left = pierce; foreach (var f in Foes.ToArray()) { if (left <= 0) break; if (f != target && f.Alive && f.Arrive <= 0 && r.Chance(.5f)) { Hurt(f, shot, false); left--; } }
            if (w.Cls == (int)WClass.Staff) foreach (var f in Foes.ToArray()) if (f != target && f.Alive && f.Arrive <= 0 && r.Chance(.35f)) Hurt(f, shot * .6f, false);
        }
        if (P.E(Fxk.Arc) > 0) Hurt(target, dmg * .5f, false, true);
    }

    // ───────────────────────── обычные враги ─────────────────────────
    void EnemyAct(Foe f, float dt, ref int busy)
    {
        if (f.Freeze > 0) return;
        var d = f.Def; float rate = T.EnemyAttackRate * (f.EMod == 2 ? 1.3f : 1f) * (f.EMod == 4 ? 1.05f : 1f);
        if (d.Kind == "Bomber") { f.AtkT -= dt; if (f.AtkT <= 0 && f.WindT <= 0) f.WindT = d.Windup; }
        if (f.WindT > 0)
        {
            f.WindT -= dt; if (f.WindT > 0) return;
            if (d.Kind == "Bomber") { if (!Dodges(.6f, 0, false, true)) DamagePlayer(18f * f.DmgScale, d.Fire || Region == 2, false, f); f.Dead = true; return; }
            bool dodge = Dodges(d.Windup, d.Ranged ? 1f : 0f, d.Ranged, !d.Ranged);
            bool kited = P.Cur != null && P.Cur.Cls != (int)WClass.Sword && !d.Ranged && r.Chance(.3f * pr.DodgeSkill);
            if (dodge || kited) S.Dodged++; else DamagePlayer(d.Dmg * f.DmgScale, d.Fire, d.Cold, f);
            f.AtkT = Math.Max(.2f, (d.Period - d.Windup) / rate * r.Range(.8f, 1.2f)) + Math.Min(0f, f.WindT); return;
        }
        f.AtkT -= dt;
        if (f.AtkT > 0) return;
        bool melee = !d.Ranged; if (melee && d.Windup > 0 && busy >= (int)T.MaxAttackers) { f.AtkT = .3f; return; }
        if (d.Windup <= 0)
        {   // рой, светоед: ударов без предупреждения
            if (d.Kind == "Leech") { if (!(pr.UseDash && P.Has(Tech.Dash) && P.DashCd <= 0 && r.Chance(pr.DashSkill * .5f))) { invuln = 0; DamagePlayer(d.Dmg * f.DmgScale, false, false, f); } else P.DashCd = P.DashCooldown; }
            else if (!Dodges(0, 0, false, true)) DamagePlayer(d.Dmg * f.DmgScale, d.Fire, d.Cold, f); else S.Dodged++;
            f.AtkT = d.Period / rate; return;
        }
        f.WindT = d.Windup + f.AtkT; if (melee) busy++;
    }

    // ───────────────────────── боссы ─────────────────────────
    void BossAct(Foe f, float dt)
    {
        var b = f.Boss; float hpf = f.Hp / f.MaxHp; int ph = hpf >= .66f ? 0 : hpf >= .33f ? 1 : 2; float mul = (ph == 2 ? b.RageMul : 1f) * T.BossTempo;
        bool kiter = P.Cur != null && P.Cur.Cls != (int)WClass.Sword;
        if (f.Hide > 0) { f.Hide -= dt; if (f.Hide <= 0) { f.Hide = 0; f.PendingMove = Array.FindIndex(b.Moves, x => x.Name == "dive"); f.WindT = b.Moves[f.PendingMove].Windup; } return; }
        if (f.Open > 0) { f.Open -= dt; return; }
        if (f.WindT > 0)
        {
            f.WindT -= dt; if (f.WindT > 0) return;
            var mv = b.Moves[f.PendingMove];
            if (mv.Hide > 0 && f.MoveCd[f.PendingMove] != -1f) { f.MoveCd[f.PendingMove] = -1f; f.Hide = mv.Hide * mul; f.WindT = 0; return; }          // нырок: невидим и неуязвим, затем всплытие с ударом
            if (mv.Hide > 0) f.MoveCd[f.PendingMove] = 0;
            if (mv.Dmg > 0)
            {
                bool dodge = Dodges(mv.Windup, mv.Ranged ? .9f : 0f, mv.Ranged, !mv.Ranged) && r.Chance(BossDodgeFactor);
                if (!mv.Ranged && kiter && r.Chance(.35f * pr.DodgeSkill)) dodge = true;
                if (dodge) S.Dodged++; else DamagePlayer((mv.Dmg + mv.Ring * .5f) * f.DmgScale, mv.Fire || b.Fire && mv.Fire, mv.Cold, f);
            }
            if (mv.Summon != null) foreach (var k in mv.Summon) { var d = M.Enemies.FirstOrDefault(x => x.Kind == k); if (d != null) Foes.Add(MakeFoe(d, mv.SummonDanger, 0, false, r.Range(20, 60))); }
            if (mv.Open > 0) f.Open = mv.Open;
            if (mv.Cooldown > 0) f.MoveCd[f.PendingMove] = mv.Cooldown * mul * r.Range(.9f, 1.1f);
            else f.NextT = (mv.Gap * mul + mv.Post) * r.Range(.9f, 1.1f) + Math.Min(0f, f.WindT);
            f.PendingMove = -1; return;
        }
        // обычный удар вблизи (HurtPlayer(12f) раз в Atk секунд)
        f.MeleeT -= dt;
        if (f.MeleeT <= 0)
        {
            f.MeleeT = b.MeleePeriod * r.Range(.9f, 1.1f);
            bool kited = kiter && r.Chance(.5f * pr.DodgeSkill + .1f);
            if (!kited && !Dodges(0, 0, false, true)) DamagePlayer(b.MeleeDmg * f.DmgScale, b.Fire, b.Cold, f);
        }
        for (int i = 0; i < f.MoveCd.Length; i++) if (b.Moves[i].Cooldown > 0 || b.Moves[i].Hide > 0) f.MoveCd[i] = f.MoveCd[i] > 0 ? f.MoveCd[i] - dt : f.MoveCd[i];
        for (int i = 0; i < b.Moves.Length; i++)
        {
            var mv = b.Moves[i]; if (mv.Cooldown <= 0 || hpf >= mv.PhaseBelowHp || f.MoveCd[i] > 0) continue;
            f.PendingMove = i; f.WindT = mv.Windup; return;
        }
        f.NextT -= dt; if (f.NextT > 0) return;
        float sum = 0; foreach (var mv in b.Moves) if (mv.Cooldown <= 0) sum += ph == 0 ? mv.W0 : ph == 1 ? mv.W1 : mv.W2;
        float rr = r.F() * sum; int pick = -1;
        for (int i = 0; i < b.Moves.Length; i++) { var mv = b.Moves[i]; if (mv.Cooldown > 0) continue; rr -= ph == 0 ? mv.W0 : ph == 1 ? mv.W1 : mv.W2; if (rr <= 0 && (ph == 0 ? mv.W0 : ph == 1 ? mv.W1 : mv.W2) > 0) { pick = i; break; } }
        if (pick < 0) { f.NextT = 1f; return; }
        f.PendingMove = pick; f.WindT = b.Moves[pick].Windup + Math.Min(0f, f.NextT); f.MoveCd[pick] = b.Moves[pick].Hide > 0 ? 0 : f.MoveCd[pick];
    }

    // ───────────────────────── прогон ─────────────────────────
    /// <summary>Бой до гибели всех врагов, смерти героя или предела времени.</summary>
    public CombatStats Run(float maxTime)
    {
        while (AnyAlive && !S.Died && S.Time < maxTime) Step();
        return S;
    }
}
