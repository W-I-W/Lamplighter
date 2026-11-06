using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

public enum EK { Shade, Wraith, Spitter, Swarm, Bomber, Warden, Eel, Leech, Beetle, DepthWarden, Imp, Golem, Spirit, AshWarden, IceWolf, FrostPriest, Sentinel, WinterWarden, Mirage, Decoy, Weaver, Stalker, Source }

public sealed class Enemy
{
    public EK Kind; public Vector2 Pos, Kb, DashDir, Face = new(1, 0); public Vector2? Home; public Poi Guard;
    public float Hp, MaxHp, Atk, CdRec, Flash, Phase, Age, Cd = 1.5f, T1, T2, T3, Dash, Tele, Scale = 1f, Burn;
    // визуальное состояние (только отрисовка): стадии атаки, попадание, след рывка
    public float VTele0, VAtk = 9f, VPrevAtk, VPrevTele, VPrevDash, VHit = 9f, VHitMul = 1f, VTrailT; public Vector2 VHitDir; public Vector2[] VTrail = new Vector2[3];
    public float RaidCd = 2f, RaidT; public int RaidSt; public Vector2 RaidDir, RaidHome; public float LostT, Over, SpecT = 8f, SpecTim, SpecAng, SpecEmit; public int SpecPh, SpecN, SpecVol; public int NetId; public long Target = 1, LastHitBy; public float RetargetT, SeenT; public Vector2 NetPos; public int State, EMod; public bool Id2; public float WardT, RallyT; public bool Shield, Hidden, Elite, Carrier; public float VulnT; public Vector2 Mark; public float Slow, Freeze; public GEvent Ev;
    public BossPhaseController PhaseCtl;                  // фазы босса (данные BossPhaseBook); у гостя — по фазе, присланной хостом
    public bool IsBoss => Kind == EK.Warden || Kind == EK.DepthWarden || Kind == EK.AshWarden || Kind == EK.WinterWarden || Kind == EK.Source;
    public float Radius => Kind switch
    {
        EK.Warden => 20, EK.DepthWarden => 15, EK.AshWarden => 15, EK.WinterWarden => 15, EK.Source => 16, EK.Sentinel => 8, EK.Weaver => 6, EK.Swarm => 3, EK.Bomber => 6, EK.Shade => 6, EK.Beetle => 7, EK.Golem => 9, EK.Leech => 4, _ => 5
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
    float spawnT = 4; float ctxDmg = 1f, stillT, edgeT; const float EdgeWait = 2.5f;       // сколько секунд игрок должен простоять у края света, чтобы враги пошли в налёт


    public static string EnemyName(EK k) => k switch
    {
        EK.Shade => "Тень", EK.Wraith => "Призрак", EK.Spitter => "Плевун", EK.Swarm => "Мрак-рой", EK.Bomber => "Подрывник", EK.Warden => "Пожиратель Рощи",
        EK.Eel => "Глубинный угорь", EK.Leech => "Светоед", EK.Beetle => "Кристальный жук", EK.DepthWarden => "Хранитель глубин",
        EK.Imp => "Огненный бес", EK.Golem => "Пепельный голем", EK.Spirit => "Дух пепла", EK.IceWolf => "Ледяной волк", EK.FrostPriest => "Ледяной жрец", EK.Sentinel => "Зеркальный страж",
        EK.WinterWarden => "Хранитель Зимы", EK.Mirage => "Иллюзорник", EK.Decoy => "Мираж", EK.Weaver => "Ткач Тьмы", EK.Stalker => "Пустотный охотник", EK.Source => "Источник Тьмы", _ => "Пепельный страж"
    };

    // ───────────────────────── способности ─────────────────────────
    void TryDash()
    {
        if (Window != Win.None || TechOpen || MapOpen || Menu != Mn.None) return;          // в меню рывок невозможен
        if (Has(Tech.Dash) && DashCd <= 0 && DashT <= 0) Diag.Count("dash");
        if (!Has(Tech.Dash) || DashCd > 0 || DashT > 0) return;
        var d = LastMoveDir.LengthSquared() > .01f ? LastMoveDir : (Aim - PPos).Normalized();
        if (d.LengthSquared() < .01f) d = new Vector2(Facing, 0);
        DashDir = d; DashT = .18f; DashCd = DashCooldown; Invuln = Mathf.Max(Invuln, Tune.T.DashInvuln); Sfx.Play("dash"); RelicOnDash(); OnDash(PPos, d);
    }

    void TrySpin()
    {
        if (!Has(Tech.Spin) || SpinCd > 0) return;
        if (CurDef != null && CurDef.Cls != WClass.Sword) { Toast("Круговой удар работает только с оружием ближнего боя"); return; }
        SpinCd = 3f; SpinT = .25f; float spinK = SpinScale; Sfx.Play("spin"); Swing = .22f;
        foreach (var e in new List<Enemy>(Enemies))
            if (!e.Hidden && e.Pos.DistanceTo(PPos) < 32 * spinK + e.Radius) Hurt(e, PlayerDamage * 1.2f * spinK, (e.Pos - PPos).Normalized(), true);
        Burst(PPos, new Color("#fff2b0"), 12, 80);
    }

    void TryFlare()
    {
        if (FlareCd <= 0) Diag.Count("flare");
        if (!Has(Tech.Flare) || FlareCd > 0) return;
        var d = Aim - PPos; if (d.Length() > 110) d = d.Normalized() * 110;
        Beacons.Add(new Beacon { Pos = PPos + d, Dur = FlareDuration, R = 72, Burn = 6f });
        FlareCd = 8f; Sfx.Play("flare"); Burst(PPos + d, new Color("#ffd86b"), 14, 70);
    }

    void TryPulse()
    {
        if (PulseCd <= 0) Diag.Count("pulse");
        if (!Has(Tech.Pulse) || PulseCd > 0) return;
        PulseCd = PulseCooldown; Sfx.Play("pulse"); Shake = 5; Flash = .3f; LightBlockT = 0; RelicOnPulse(); OnPulse(PPos);
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
                    if (!e.Hidden && e.Pos.DistanceTo(b.Pos) < BeaconR(b) * .85f) HurtTick(e, b.Burn * dt * (e.IsBoss ? .3f : 1f), true);
        }
    }

    // ───────────────────────── спавн ─────────────────────────
    bool LineClear(Vector2 a, Vector2 b)
    {
        float len = a.DistanceTo(b);
        for (float s = 8; s < len; s += 8) { var t = ToTile(a.Lerp(b, s / len)); if (IsWall(W.Get(t.X, t.Y))) return false; }
        return true;
    }

    int spawnIdx; public bool TestNoSpawn;          // только проверки: отключает случайное появление врагов

    /// <summary>Появление врагов у каждого игрока по очереди: хост выбирает по одному игроку за такт, поэтому темп на игрока прежний, а враги появляются возле всех, а не только возле хоста.
    /// Запас считается по врагам рядом с этим игроком.</summary>
    void UpdateSpawning(float dt)
    {
        if (NetClient || RiftOn || TestNoSpawn) return;
        var targets = new List<Vector2> { PPos }; if (NetHost) foreach (var r in Remotes()) if (r.Hp > 0) targets.Add(r.Pos);
        spawnT -= dt; if (spawnT > 0) return;
        spawnIdx = (spawnIdx + 1) % targets.Count; var tp = targets[spawnIdx];
        if (ArenaHolding(tp) != null) { spawnT = 1f; return; }                    // в арене врагов добавляет только сам страж
        if (IsLit(tp)) { spawnT = .5f / targets.Count; return; }
        int reg = RegionAt(tp);
        if (Finished && reg < 4) { spawnT = 1f; return; }
        float nearUnlit = float.MaxValue;
        for (int i = 1; i < W.Ancients.Count; i++) if (!W.Ancients[i].Lit && !W.Ancients[i].Boss && W.Ancients[i].Region == reg) nearUnlit = Mathf.Min(nearUnlit, W.Ancients[i].Pos.DistanceTo(tp));
        bool guarded = nearUnlit < 9 * TS;
        float spent = 0; int ranged = 0;                                           // бюджет опасности: каждый вид стоит по-своему, дальнобойных рядом не больше MaxRangedNear
        foreach (var e in Enemies) if (e.Guard == null && !e.IsBoss && e.Pos.DistanceTo(tp) < 420f) { spent += EnemyCost(e); if (EnemyBalance.IsRanged(e.Kind)) ranged++; }
        float budget = EncounterBudget(reg, guarded, targets.Count);
        spawnT = rng.RandfRange(2.2f, 3.8f) * Tune.T.SpawnDelayMul * (guarded ? .55f : 1f) / targets.Count;
        if (spent >= budget) return;
        for (int tries = 0; tries < 24; tries++)
        {
            float a2 = rng.Randf() * Mathf.Tau, d = rng.RandfRange(130, 200);
            var p = tp + new Vector2(Mathf.Cos(a2), Mathf.Sin(a2)) * d;
            var t = ToTile(p);
            if (!W.Walkable(t.X, t.Y) || IsLit(p) || RegionAt(p) != reg || ArenaHolding(p) != null) continue;
            if (reg != 0 && !LineClear(tp, p)) continue;
            bool nearOther = false; foreach (var o in targets) if (o != tp && o.DistanceTo(p) < 120f) { nearOther = true; break; }         // не прямо перед другим игроком
            if (nearOther) continue;
            var k = PickKind(p, reg);
            if (k == EK.Eel && !WaterAt(p)) continue;
            float kc = EnemyBalance.Cost(k) * (k == EK.Swarm ? 4 : 1);
            if (spent + kc > budget + .01f || (EnemyBalance.IsRanged(k) && ranged >= Tune.T.MaxRangedNear)) continue;     // не помещается в бюджет или слишком много стрелков
            if (k == EK.Swarm) for (int s2 = 0; s2 < 4; s2++) SpawnEnemy(p + new Vector2(rng.RandfRange(-8, 8), rng.RandfRange(-8, 8)), k);
            else { var ne = SpawnEnemy(p, k); TryMakeElite(ne, reg); }
            return;
        }
    }

    float killHealPool = -1;                                  // лечение за убийства не бесконечно: запас до 20% здоровья, пополняется 2% здоровья в секунду
    void KillHealGain(float amt)
    {
        if (killHealPool < 0) killHealPool = MaxHp * .2f;
        float g = Mathf.Min(amt, killHealPool); killHealPool -= g; Hp = Mathf.Min(MaxHp, Hp + g);
    }

    /// <summary>Стоимость живого врага в бюджете столкновения: фиксированная стоимость вида (+ элитность).</summary>
    static float EnemyCost(Enemy e) => EnemyBalance.Cost(e.Kind) + (e.Elite ? EnemyBalance.EliteCost : 0f);

    /// <summary>Бюджет опасности рядом с игроком: растёт с числом зажжённых фонарей региона (больше врагов вместе, но не сильнее), ограничен потолком региона.</summary>
    public float EncounterBudget(int reg, bool guarded, int players)
    {
        var tn = Tune.T; float b = CombatTuning.At(tn.EncounterBase, reg) + tn.EncounterPerLamp * (reg == 0 ? LitCount : LitIn(reg));
        b = Math.Min(b, CombatTuning.At(tn.EncounterMax, reg));
        if (guarded) b += 3f; if (Level < tn.FearLevel) b += tn.FearSpawn;
        b *= CombatTuning.At(tn.CoopCap, CoopIdx);   // в группе врагов одновременно больше, но каждый вид прежний
        return b * tn.SpawnCapMul;
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
        else if (reg == 3)
        {
            kinds = new[] { EK.IceWolf, EK.FrostPriest, EK.Sentinel, EK.Spitter };
            w = new float[] { 4, 3, 2.5f, 1 };
        }
        else if (reg == 4)
        {
            kinds = new[] { EK.Mirage, EK.Weaver, EK.Stalker };
            w = new float[] { 3, 3, 3 };
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

    static float BaseHp(EK k) => EnemyBalance.Hp(k);                    // фиксированное здоровье вида: EnemyBalance

    /// <summary>Рядом идёт бой с боссом: призванные им враги (и все остальные) на свету не горят.</summary>
    bool BossFightNear(Vector2 p) { foreach (var b in Enemies) if (b.IsBoss && b.Hp > 0 && b.Pos.DistanceTo(p) < 420f) return true; return false; }

    public Enemy SpawnEnemy(Vector2 p, EK k, Poi guard = null, float scale = -1)      // scale не используется: характеристики вида фиксированы (EnemyBalance)
    {
        bool boss = k == EK.Warden || k == EK.DepthWarden || k == EK.AshWarden || k == EK.WinterWarden || k == EK.Source;
        if (NetClient) return new Enemy { Kind = k, Pos = p, Hp = 1, MaxHp = 1 };            // клиент врагов не создаёт: их присылает хост
        var (dm, hm) = EnemyMuls(k, p);
        float hp = BaseHp(k) * hm; scale = dm;
        var e = new Enemy { Kind = k, Pos = p, Hp = hp, MaxHp = hp, Phase = rng.Randf() * 6.28f, Scale = scale, Guard = guard, Cd = rng.RandfRange(.8f, 2f) };
        e.NetId = ++nextEnemyId; if (guard != null) e.Home = p;
        if (boss) { e.Home = p; e.T1 = 2.5f; e.T3 = 4f; }
        Enemies.Add(e); return e;
    }

    // ───────────────────────── боссы ─────────────────────────
    float bossT;
    void BossKilled(Enemy e)
    {
        int id = e.Kind == EK.Warden ? 1 : e.Kind == EK.DepthWarden ? 2 : e.Kind == EK.AshWarden ? 3 : e.Kind == EK.WinterWarden ? 4 : 5;
        BossMask |= 1 << (id - 1); if (Boss == e) Boss = null;
        if (id == 1 && !NetClient) GroveDie(e.Pos);      // у гостя гибель рощи показывает RpcBossDown
        if (NetHost) Net?.SendBossDown(id, e.Pos);                      // эффекты победы видят все
        GiveWeapon(id == 1 ? "sword_u" : id == 2 ? "bow_u" : id == 3 ? "staff_u" : id == 4 ? "staff_w" : "");
        GiveRelic(id == 1 ? "r_dawnprism" : id == 2 ? "r_winterheart" : id == 3 ? "r_emberheart" : id == 4 ? "r_prismcore" : null);
        if (id == 5) { FinalBossKilled(e); return; }
        GiveLoot(It.Core, 1, e.Pos + new Vector2(0, -20));
        GiveLoot(It.Shard, 6, e.Pos); GiveLoot(It.Relic, 2, e.Pos + new Vector2(0, -8));
        string title, sub;
        if (id == 1)
        {
            TechMask |= 1 << (int)Tech.Pulse; GiveLoot(It.Aether, 3, e.Pos + new Vector2(0, -16));
            title = "Пожиратель Рощи повержен!"; sub = "Роща свободна. Первое ядро света! Улучшите маяк [E]";
        }
        else if (id == 2)
        {
            TechMask |= 1 << (int)Tech.Beam; GiveLoot(It.Crystal, 4, e.Pos + new Vector2(0, -16)); GiveLoot(It.Silver, 3, e.Pos + new Vector2(0, -24));
            title = "Хранитель глубин повержен!"; sub = "Второе ядро и ПРОЖЕКТОР [R]. Улучшите маяк";
        }
        else if (id == 4)
        {
            TechMask |= 1 << (int)Tech.Prism; GiveLoot(It.Frost, 6, e.Pos + new Vector2(0, -16)); GiveLoot(It.Crystal, 6, e.Pos + new Vector2(0, -24)); if (!NetClient) ClearIceBlocks();
            title = "Хранитель Зимы повержен!"; sub = "Четвёртое ядро. Улучшите маяк — откроется Сердце Тьмы";
        }
        else
        {
            TechMask |= 1 << (int)Tech.Fireproof; GiveLoot(It.Ember, 4, e.Pos + new Vector2(0, -16)); GiveLoot(It.Obsidian, 3, e.Pos + new Vector2(0, -24));
            title = "Пепельный страж повержен!"; sub = "Третье ядро и огнестойкость";
        }
        GiveXp(150 + 50 * id); Flash = .8f; Shake = 8; VictoryFx(e.Pos, id == 4 ? new Color("#bfe9ff") : id == 3 ? new Color("#ff9a5a") : id == 2 ? new Color("#7ac8ff") : new Color("#d58cff"));
        Burst(e.Pos, new Color("#d58cff"), 50, 180);
        Announce(title, sub, new Color("#ffe9a0"), "ancient");
        Save();
    }

    // ───────────────────────── урон ─────────────────────────
    static bool SoulKind(EK k) => k == EK.Shade || k == EK.Wraith || k == EK.Swarm || k == EK.Leech || k == EK.Eel || k == EK.Spirit || k == EK.Spitter;

    public void Hurt(Enemy e, float dmg, Vector2 dir, bool melee = false, bool light = false)
    {
        if (e.Hidden) return;
        if (NetClient && e.NetId > 0) { Net?.SendEnemyHit(e.NetId, dmg, dir, melee, light); e.Flash = .15f; e.VHit = 0; Fx("enemy_hit", e.Pos); return; }       // хост считает урон
        e.LastHitBy = hitSrc != 0 ? hitSrc : LocalPeer;                                 // последний удар определяет, кому достанется награда
        bool remote = hitSrc != 0; var from = remote ? PosOf(hitSrc) ?? PPos : PPos;      // удар гостя: умения и позиция хоста в расчёте не участвуют
        if (e.Kind == EK.Decoy) { e.Hp = 0; Burst(e.Pos, new Color("#c080ff"), 8, 60); return; }
        float m = EnemyDefenseMul(e, from, light);
        if (melee && !remote && Has(Tech.SilverEdge) && SoulKind(e.Kind)) m *= 1f + E(Tech.SilverEdge);
        if (melee && e.Kind == EK.Beetle)
        {
            float cosA = e.Face.Normalized().Dot((from - e.Pos).Normalized());
            if (cosA > .35f) { m *= .2f + .08f * (remote ? 0 : Lv(Tech.SilverEdge)); Burst(e.Pos + e.Face * 6, new Color("#e4f4ff"), 4, 60); Sfx.Play("tick", -2, .6f); }
        }
        if (!remote && Flags.Contains("DarkHunter")) dmg *= InDark ? 1.25f : .9f;       // «Плащ теней»: сильнее в темноте, слабее на свету
        dmg *= m; ApplyEnemyDamage(e, dmg);
        e.Flash = .15f; e.Kb += dir * (e.IsBoss ? 14f : e.Kind == EK.Wraith ? 160f : e.Kind == EK.Golem || e.Kind == EK.Beetle ? 50f : 120f);
        if (e.Kind == EK.Golem && rng.Randf() < .6f) AddZone(e.Pos, 40, 0, 3.5f, 0f, true);
        if (melee && !remote && Has(Tech.FlameEdge)) e.Burn = Mathf.Max(e.Burn, E(Tech.FlameEdge));
        if (melee && !remote && Has(Tech.FrostEdge)) { e.Slow = Mathf.Max(e.Slow, E(Tech.FrostEdge)); if (!e.IsBoss && Lv(Tech.FrostEdge) >= 3 && rng.Randf() < .05f * (Lv(Tech.FrostEdge) - 2)) e.Freeze = Mathf.Max(e.Freeze, 1f); }
        if (melee && !remote && Tier >= 4) e.Burn = Mathf.Max(e.Burn, 2f);
        Fx("enemy_hit", e.Pos); OnEnemyHit(e, dmg, dir);
        bool kill = e.Hp <= 0, heavy = kill || (e.MaxHp > 0 && dmg >= e.MaxHp * .3f);
        if (melee && !remote) HitFeel(e, heavy, kill);
        Texts.Add(new FText { P = e.Pos + new Vector2(0, -8 - e.Radius), S = ((int)Mathf.Round(dmg)).ToString() + (heavy ? "!" : ""), Life = heavy ? .85f : .6f, C = heavy ? new Color("#ffd24a") : m < 1 ? new Color("#9aa6bf") : new Color("#ffd0d0") });
    }

    float hitStop;
    /// <summary>Ощущение удара ближнего боя: короткая заморозка, тряска и лишние искры. Сильнее при добивании и тяжёлом ударе; в сетевой игре заморозки нет, чтобы не рассинхронизировать мир.</summary>
    void HitFeel(Enemy e, bool heavy, bool kill)
    {
        if (!NetService.Active) hitStop = Mathf.Max(hitStop, (kill ? .075f : heavy ? .06f : .03f) * Mathf.Clamp(Settings.Fx, 0f, 1.5f));
        Shake = Mathf.Max(Shake, kill ? 2.4f : heavy ? 1.6f : .6f);
        if (heavy) Burst(e.Pos, new Color("#ffe9a0"), kill ? 14 : 8, 120);
    }

    float EnemyDefenseMul(Enemy e, Vector2 from, bool light)
    {
        float m = 1f;
        if (e.Shield && (e.Kind == EK.WinterWarden || e.Kind == EK.Source)) m *= .06f;
        if (e.Kind == EK.Sentinel && !light) m *= .55f;       // свет выгоден, но обычное оружие не упирается в стену
        if (e.Over > 0) m *= 1.4f;
        if (e.Kind == EK.Warden) m *= GroveDamageMul(e, from);
        if (e.EMod == EWarded && e.WardT > 0) m *= .35f;
        return m;
    }

    void ApplyEnemyDamage(Enemy e, float dmg)
    {
        float hp0 = e.Hp;
        e.Hp -= Mathf.Max(0, dmg);
        if (e.IsBoss) e.Hp = Mathf.Max(e.Hp, Mathf.Min(hp0, e.MaxHp * PhaseOf(e).FloorFrac));
        if (e.Kind == EK.Warden) GroveAfterHurt(e, hp0 - e.Hp);
    }

    /// <summary>Тот же щит и границы фаз, без отбрасывания и эффектов попадания на каждом кадре.</summary>
    public void HurtTick(Enemy e, float dmg, bool light = false)
        => HurtTickAt(e, dmg, light, hitSrc != 0 ? PosOf(hitSrc) ?? PPos : PPos);

    void HurtTickAt(Enemy e, float dmg, bool light, Vector2 from, bool credit = true)
    {
        if (e.Hidden || e.Hp <= 0 || !float.IsFinite(dmg) || dmg <= 0) return;
        if (NetClient) { QueueEnemyDot(e, dmg, light); return; }
        if (credit) e.LastHitBy = hitSrc != 0 ? hitSrc : LocalPeer;
        ApplyEnemyDamage(e, dmg * EnemyDefenseMul(e, from, light));
        e.Flash = Mathf.Max(e.Flash, .04f);
    }

    /// <summary>Свет жжёт врагов (освещённая земля, аура героя), поэтому пока игрок стоит в свете, обычные враги держатся от света дальше его удара: останавливаются у границы, а оказавшись близко (свет вырос) отходят. Когда игрок выходит в тьму, враг нападает, даже если зайдёт на свет.</summary>
    /// <summary>Игрок на свету, но у самого края тени (в ~40 пикселях от неё).</summary>
    bool AtLightEdge()
    {
        if (!IsLit(PPos)) return false;
        for (int i = 0; i < 8; i++) if (!IsLit(PPos + Vector2.FromAngle(i * Mathf.Pi / 4) * 40f)) return true;
        return false;
    }

    /// <summary>Налёт есть только у Тени и Призрака — остальные виды остаются на границе света. Игрок на свету у края тени и не заходит в неё: враг из тени замирает, делает быстрый рывок через свет, ударяет и отскакивает обратно в тень.
    /// Состояния: 1 — замер и целится, 2 — рывок, 3 — возврат на место, откуда начал. Возвращает true, пока враг занят налётом (обычное избегание света отключено).</summary>
    bool RaidTick(Enemy e, float dt, ref Vector2 vel, Vector2 dir, float dist, bool lit)
    {
        bool can = !e.IsBoss && e.Ev == null && e.Guard == null && e.Freeze <= 0 && (e.Kind == EK.Shade || e.Kind == EK.Wraith);
        if (!can || BossFightNear(e.Pos)) { e.RaidSt = 0; return false; }
        switch (e.RaidSt)
        {
            case 0:
                e.RaidCd -= dt;
                if (e.RaidCd <= 0 && !lit && edgeT >= EdgeWait && dist > 30f && dist < 130f)
                {
                    if (AtkSlot()) { e.RaidSt = 1; e.RaidT = .45f; e.RaidHome = e.Pos; }
                    else e.RaidCd = .6f;
                }
                else if (e.RaidCd <= 0) e.RaidCd = .5f;
                return false;
            case 1:
                vel = Vector2.Zero; e.RaidT -= dt; e.Flash = Mathf.Max(e.Flash, .04f);
                if (e.RaidT <= 0) { e.RaidSt = 2; e.RaidDir = dir; e.RaidT = Mathf.Clamp((dist + 40f) / 280f, .4f, .65f); Sfx.Play("dash", -8, 1.3f); }
                return true;
            case 2:
                vel = e.RaidDir * 280f; e.RaidT -= dt;
                if (dist < 14f && e.Atk <= 0) { e.Atk = .9f; HurtPlayer(8f * e.Scale, -dir); e.RaidT = 0; }
                if (e.RaidT <= 0) { e.RaidSt = 3; e.RaidT = 2.2f; }
                return true;
            default:
                { var back = e.RaidHome - e.Pos; e.RaidT -= dt; vel = back.Length() > 6f ? back.Normalized() * 160f : Vector2.Zero; }
                if (e.RaidT <= 0 || e.Pos.DistanceTo(e.RaidHome) <= 6f) { e.RaidSt = 0; e.RaidCd = rng.RandfRange(2.5f, 4.5f); }
                return true;
        }
    }

    /// <summary>Враг прячется за камнем или деревом: игрок достаёт его, а враг за преградой ударить не может. Он отходит от преграды в сторону, откуда видно героя (сам выходит на линию атаки), а если такой нет, отступает назад из зоны досягаемости.</summary>
    const float ShadeSlamR = 30f;                                           // радиус удара тени по земле

    Vector2 AvoidCover(Enemy e, Vector2 vel, Vector2 toPlayerDir, float dist)
    {
        if (e.Kind == EK.Wraith || e.Kind == EK.Spirit || e.Kind == EK.Stalker || e.Kind == EK.Mirage || e.Kind == EK.Decoy || e.Kind == EK.Eel || e.Kind == EK.Beetle) return vel;
        float reach = Mathf.Max(48f, ShotRange() * .6f) + 10f;
        if (dist > reach || dist < 4f || LineClear(e.Pos, PPos)) return vel;
        var tan = toPlayerDir.Orthogonal();
        foreach (var d in new[] { tan, -tan, tan.Rotated(.6f), (-tan).Rotated(-.6f) })
            for (float step = 16f; step <= 32f; step += 16f)
            { var c = e.Pos + d * step; if (!IsWall(W.Get(ToTile(c).X, ToTile(c).Y)) && LineClear(c, PPos)) return d * Mathf.Max(vel.Length(), 55f); }
        return -toPlayerDir * 55f;
    }

    Vector2 AvoidLight(Enemy e, Vector2 vel, Vector2 toPlayerDir, float auraR)
    {
        if (BossFightNear(e.Pos) || !IsLit(PPos)) return vel;              // игрок вышел из света — враг нападает, даже если зайдёт на свет
        float Reach = Mathf.Max(48f, ShotRange() * .6f);                     // дальше досягаемости оружия в руках: ближний бой — 48, лук и посох — 60% дальности выстрела (дальше стрела слабеет)
        bool Hurts(Vector2 p) => IsLit(p) || (AuraDps > 0 && p.DistanceTo(PPos) < auraR + 6f);
        if (Hurts(e.Pos) || Hurts(e.Pos + toPlayerDir * (Reach * .6f)))
        {
            var away = -toPlayerDir; if (away == Vector2.Zero) away = Vector2.Right;
            foreach (var d in new[] { away, away.Rotated(Mathf.Pi / 4), away.Rotated(-Mathf.Pi / 4), away.Rotated(Mathf.Pi / 2), away.Rotated(-Mathf.Pi / 2) }) if (!Hurts(e.Pos + d * 22f)) return d * 55f;
            return away * 55f;
        }
        if (vel.LengthSquared() < 1f) return vel;
        return Hurts(e.Pos + vel.Normalized() * Reach) ? Vector2.Zero : vel;
    }

    public void HurtPlayer(float dmg, Vector2 kb, bool fire = false)
    {
        if (Window == Win.Trade) Window = Win.None;
        if (aiRemote != 0) { Net?.SendHurt(aiRemote, dmg * ctxDmg, kb, fire); return; }                  // враг выбрал другого игрока: урон решает хост и шлёт ему
        if (Invuln > 0) return;
        if (fire && Has(Tech.Fireproof)) return;
        dmg = Mathf.Max(dmg * ctxDmg, Tune.T.EnemyMinHit);
        if (Flags.Contains("Greed")) dmg *= 1.2f;                                      // «Печать жадности»
        if (fire && Has(Tech.HeatShield)) dmg *= HeatTaken;
        if (fire) dmg *= 1f - N("HeatResist");
        dmg = Mathf.Max(0, dmg) * FearMul; Hp -= dmg; Invuln = Tune.T.HitInvuln; regenDelay = Tune.T.RegenDelay; Shake = 3f;
        pVel += kb * 120f; Sfx.Play("hurt");
        Fx("player_hurt", PPos, fire ? new Color("#ff9a2e") : null);
        Texts.Add(new FText { P = PPos + new Vector2(0, -12), S = "-" + (int)Mathf.Round(dmg), Life = .8f, C = new Color("#ff6060") });
        if (Hp <= 0) Die();
    }

    Vector2? aiRespawn;                                                            // точка возрождения, пока UpdateEnemies временно подменяет PPos

    void Die()
    {
        Diag.Mark("first_death"); EventsOnDeath(); RiftOnDeath();
        if (!NetService.Active) { ResetGuardFights(); Enemies.Clear(); Projs.Clear(); Boss = null; }        // в сети враги принадлежат хосту
        LightBlockT = 0; LeechT = 0; Zones.Clear(); ClearHeartTemps();
        Diag.Count("deaths");
        int lost = BuryPlayer();                                                   // все ресурсы, инструменты и оружие остаются в могиле там, где игрок пал
        bool lvLoss = Diff == 1 && Level > 1;                                      // сложный режим: смерть стоит уровня
        bool xpLoss = Diff == 1 && !lvLoss && Xp > 0;                              // на 1-м уровне терять нечего, кроме набранного опыта
        if (lvLoss) { Level--; MaxHp = Mathf.Max(60f, MaxHp - 10f); Xp = 0; }
        else if (xpLoss) Xp = 0;
        PPos = RespawnPos(); aiRespawn = PPos; Hp = MaxHp; Invuln = 2f; Save();
        if (lost > 0 && (lvLoss || xpLoss)) Toast("Ресурсы, инструменты и оружие остались в могиле: разбейте её");
        Toast(lvLoss ? $"Тьма поглотила вас. Уровень потерян: {Level}" : xpLoss ? "Тьма поглотила вас. Опыт уровня потерян" : lost > 0 ? "Тьма поглотила вас. Ресурсы, инструменты и оружие остались в могиле: разбейте её" : "Тьма поглотила вас. Прогресс сохранён");
        cam.ResetSmoothing();
    }

    /// <summary>Враг убит (на хосте или в одиночной игре). Награду получает тот, кто нанёс последний удар: если это гость, хост оставляет себе только мир (прогресс, события), а личное — опыт, счётчик, лечение,
    /// добыча, награды элиты, события и стража — гостю присылает признак убитого (RpcKillCredit), и всё считается по его собственным умениям.</summary>
    void KillEnemy(Enemy e)
    {
        Diag.Mark(e.IsBoss ? "first_boss_kill" : "first_kill"); OnEnemyDeath(e);
        if (e.IsBoss && NetHost)                                                       // босс: награда каждому игроку в мире, не только нанёсшему последний удар
        {
            KillRewards(e);
            foreach (var r in Remotes()) Net?.SendKillCredit(r.PeerId, e);
            return;
        }
        long killer = NetHost ? KillerOf(e) : LocalPeer;
        if (killer != LocalPeer) { KillWorldOnly(e); Net?.SendKillCredit(killer, e); return; }
        KillRewards(e);
    }

    /// <summary>Часть убийства, которая принадлежит миру, а не убийце: завершение события, взрыв нестабильного, прогресс стража. Выполняет хост, когда добычу получает гость.</summary>
    void KillWorldOnly(Enemy e)
    {
        if (e.Ev != null && (e.Ev.Kind == "elite" || e.Ev.Kind == "caravan" && e.Carrier)) e.Ev.Done = true;
        if (e.IsBoss)
        {
            int id = e.Kind == EK.Warden ? 1 : e.Kind == EK.DepthWarden ? 2 : e.Kind == EK.AshWarden ? 3 : e.Kind == EK.WinterWarden ? 4 : 5;
            BossMask |= 1 << (id - 1); if (Boss == e) Boss = null; Net?.SendBossDown(id, e.Pos); if (id == 1) GroveDie(e.Pos);
            if (id == 4) ClearIceBlocks(); if (id == 5) ClearHeartTemps();
            Save();
        }
        else if (e.Elite && e.EMod == EVolatile) Explode(e);
    }

    /// <summary>Личная награда за убитого врага (у того, кто его убил).</summary>
    void KillRewards(Enemy e)
    {
        Kills++;
        if (e.Ev != null) EvEnemyKilled(e);
        if (e.IsBoss) { BossKilled(e); return; }
        EliteKilled(e);
        float xp = e.Kind switch { EK.IceWolf => 28, EK.FrostPriest => 30, EK.Sentinel => 50, EK.Mirage => 34, EK.Weaver => 36, EK.Stalker => 40, EK.Decoy => 0, EK.Shade => 15, EK.Wraith => 12, EK.Spitter => 18, EK.Bomber => 16, EK.Eel => 22, EK.Leech => 14, EK.Beetle => 26, EK.Imp => 24, EK.Golem => 40, EK.Spirit => 26, _ => 3 };
        float chance = e.Kind switch { EK.IceWolf => .55f, EK.FrostPriest => .6f, EK.Sentinel => .9f, EK.Mirage => .5f, EK.Weaver => .7f, EK.Stalker => .6f, EK.Decoy => 0f, EK.Shade => .7f, EK.Wraith => .45f, EK.Spitter => .6f, EK.Bomber => .5f, EK.Eel => .6f, EK.Leech => .5f, EK.Beetle => .7f, EK.Imp => .6f, EK.Golem => .9f, EK.Spirit => .6f, _ => .12f };
        xp *= 1f + .3f * RegionAt(e.Pos);
        if (Flags.Contains("Greed")) { xp *= 2f; chance = Mathf.Min(1f, chance * 2f); }
        if (Flags.Contains("KillEmbers")) AddZone(e.Pos, 22, 0, 4f, 14f, false, true);
        GiveXp(xp);
        Burst(e.Pos, new Color("#a060ff"), e.Kind == EK.Swarm ? 5 : 14, 90);
        if (E(Fxk.Blast) > 0 && !inBlast) BlastAt(e.Pos, CurDef != null ? WeaponDmg(CurDef) : PlayerDamage);
        if (N("KillHeal") > 0) KillHealGain(N("KillHeal"));
        if (Has(Tech.Reaper)) { KillHealGain(ReaperHeal); Texts.Add(new FText { P = PPos + new Vector2(0, -14), S = "+" + ReaperHeal.ToString("0.#"), Life = .7f, C = new Color("#7ad45a") }); }
        if (rng.Randf() < chance) LootFor(e, It.Shard, e.Kind == EK.Golem ? 2 : 1, e.Pos);
        if (e.Kind == EK.Beetle && rng.Randf() < .3f) LootFor(e, It.Crystal, 1, e.Pos + new Vector2(0, -8));
        if ((e.Kind == EK.IceWolf || e.Kind == EK.Sentinel || e.Kind == EK.FrostPriest) && rng.Randf() < .3f) LootFor(e, It.Frost, 1, e.Pos + new Vector2(0, -8));
        if (e.Kind == EK.Weaver || e.Kind == EK.Stalker) { if (rng.Randf() < .5f) LootFor(e, It.Shard, 1, e.Pos + new Vector2(0, -10)); }
        if (e.Kind == EK.Eel && rng.Randf() < .25f) LootFor(e, It.SilverOre, 1, e.Pos + new Vector2(0, -8));
        if ((e.Kind == EK.Imp || e.Kind == EK.Golem) && rng.Randf() < .3f) LootFor(e, It.Ember, 1, e.Pos + new Vector2(0, -8));
    }

    // ───────────────────────── ИИ врагов ─────────────────────────
    void UpdateEnemies(float dt)
    {
        if (NetClient) { ClientEnemies(dt); return; }
        var realP = PPos; aiRespawn = null; RecordTrail(dt, realP); int attached = 0; CountBusyAttackers(); stillT = pVel.Length() < 8f ? stillT + dt : 0f; if (AtLightEdge()) edgeT += dt; else if (IsLit(PPos)) edgeT = 0f;       // счётчик сбрасывается, только когда игрок ушёл вглубь света; шаг в тень его не обнуляет
        for (int i = Enemies.Count - 1; i >= 0; i--)
        {
            if (i >= Enemies.Count) continue;                                  // смерть врага (взрыв, цепочка) могла убрать нескольких сразу
            if (aiRespawn is Vector2 rp) { realP = rp; aiRespawn = null; }     // игрок погиб от удара этого же прохода — не возвращаем его на место смерти
            var e = Enemies[i]; PPos = realP; ChooseTarget(e, dt, realP);
            if (!e.IsBoss && Tune.T.EnemyAtkPause > 1f) { float dec = e.CdRec - e.Cd; if (dec > 0 && dec <= dt * 2.5f) e.Cd += dec * (1f - 1f / Tune.T.EnemyAtkPause); e.CdRec = e.Cd; }       // паузы в EnemyAtkPause раз длиннее: возвращаем часть того, что отсчитали за прошлый такт
            EliteTick(e, dt); if (e.Over > 0) e.Over -= dt; e.Cd -= dt * EnemyCdMul(e); ctxDmg = e.IsBoss ? BossDamageMul(e.Kind) : EnemyBalance.Dmg(e.Kind); int projN = Projs.Count;
            e.Age += dt; e.Flash = Mathf.Max(0, e.Flash - dt); e.Atk = Mathf.Max(0, e.Atk - (e.IsBoss ? dt : dt / Tune.T.EnemyAtkPause));
            e.Kb *= Mathf.Max(0, 1 - 9 * dt);
            e.Slow = Mathf.Max(0, e.Slow - dt); e.Freeze = Mathf.Max(0, e.Freeze - dt);
            if (e.Burn > 0) { e.Burn -= dt; HurtTickAt(e, 3f * dt * (e.IsBoss ? .3f : 1f), false, PosOf(e.LastHitBy) ?? realP, false); if (rng.Randf() < dt * 12) Burst(e.Pos, new Color("#ff9a2e"), 1, 25); }
            if (e.IsBoss) BossPhaseTick(e);
            var to = PPos - e.Pos; float dist = to.Length(); var dir = dist > .01f ? to / dist : Vector2.Zero;
            var perp = new Vector2(-dir.Y, dir.X);
            bool guard = e.Guard != null || e.IsBoss || (e.Ev != null && e.Ev.Kind != "riftx");
            if (!guard && NearestPlayerDist(e.Pos) > 380) { Enemies.RemoveAt(i); continue; }

            bool lit = !e.IsBoss && e.Ev == null && IsLit(e.Pos) && !BossFightNear(e.Pos);        // враги испытаний и событий на свету не горят: факел игрока не заменяет бой
            if (lit)
            {
                e.Hp -= 5f * dt; e.Flash = .05f;
                if (rng.Randf() < dt * 20) Burst(e.Pos, new Color("#ffe9a0"), 1, 30);
                if (e.Hp <= 0) { Burst(e.Pos, new Color("#ffe9a0"), 10, 60); Enemies.RemoveAt(i); continue; }
            }
            float auraR = PlayerLightR * (Has(Tech.HeroLamp) ? .55f : .6f);
            if (AuraDps > 0 && e.Pos.DistanceTo(realP) < auraR && !e.Hidden)
            {
                HurtTickAt(e, AuraDps * dt * (e.IsBoss ? .3f : 1f), true, realP);
                if (rng.Randf() < dt * 10) Burst(e.Pos, new Color("#ffe27a"), 1, 25);
            }
            float k = lit ? .5f : 1f;
            bool water = WaterAt(e.Pos);
            bool aquatic = e.Kind == EK.Eel || e.Kind == EK.DepthWarden;
            float wm = aquatic ? (water ? 1.4f : .45f) : (water ? .65f : 1f);
            bool aggro = e.IsBoss ? (e.Home.HasValue && NearestPlayerDist(e.Home.Value) < Tune.T.BossLeashTiles * TS) : (!guard || dist < 150);        // босс не бросает бой, пока игрок в пределах поводка
            Vector2 vel = Vector2.Zero;
            e.Hidden = false;

            if (aggro) e.LostT = 0;
            if (e.IsBoss && aggro && e.Kind != EK.Warden && e.Kind != EK.DepthWarden && e.Freeze <= 0)       // подготовка и замахи боссов идут быстрее (Роща и Хранитель глубин настроены отдельно)
            { float extra = dt * (1f / Tune.T.BossWarnMul - 1f); if (e.T2 > extra) e.T2 -= extra; if (e.Tele > extra) e.Tele -= extra; }
            if (!aggro)
            {
                e.LostT += dt;
                if (e.IsBoss && e.Hp < e.MaxHp && e.LostT > Tune.T.BossRegenDelay) { e.Hp = Mathf.Min(e.MaxHp, e.Hp + e.MaxHp * Tune.T.BossRegenRate * dt); if (rng.Randf() < dt * 6) Burst(e.Pos + new Vector2(0, -8), new Color("#9be07a"), 1, 20); }   // игрок далеко — страж восстанавливает здоровье
                if (e.Home.HasValue) { var h = e.Home.Value - e.Pos; if (h.Length() > 20) vel = h.Normalized() * 28; }
            }
            else if (e.Freeze > 0) { e.LostT = 0; }
            else switch (e.Kind)
            {
                case EK.Shade:
                    // подкрадывается, замирает с заметной подготовкой (тень вздрагивает), затем бьёт по земле волной вокруг себя (без рывка), после чего короткая пауза — тут её можно ударить
                    if (e.State == 1) { vel = Vector2.Zero; e.Tele -= dt; if (e.Tele <= 0) { e.State = 2; e.Dash = .16f; Sfx.Play("dash", -8, .7f); Burst(e.Pos, new Color("#7a4aa8"), 14, 70); Fx("tile_break", e.Pos, new Color("#7a4aa8")); if (dist < ShadeSlamR + 4f && e.Atk <= 0) { e.Atk = .9f; HurtPlayer(9f * e.Scale, -dir); } } }      // удар по площади: герой уходит от тени за время подготовки, а не пережидает рывок
                    else if (e.State == 2)
                    {
                        vel = Vector2.Zero; e.Dash -= dt;
                        if (e.Dash <= 0) { e.State = 3; e.T2 = .8f; }
                    }
                    else if (e.State == 3) { e.T2 -= dt; vel = perp * (e.Id2 ? 28f : -28f) * k; if (e.T2 <= 0) { e.State = 0; e.Cd = rng.RandfRange(.5f, 1.1f); } }
                    else
                    {
                        vel = dir * (36f + 2f * LitCount) * k;
                        if (dist < 40) { vel = (dir * .4f + perp * (e.Id2 ? 1f : -1f)).Normalized() * 34f * k; }
                        if (dist < 44 && e.Cd <= 0 && AtkSlot()) { e.State = 1; e.Tele = Tune.T.ShadeWindup; e.Id2 = rng.Randf() < .5f; }
                    }
                    break;
                case EK.Wraith:
                    e.Cd -= dt;
                    if (e.Tele > 0) { e.Tele -= dt; if (e.Tele <= 0) { e.Dash = Mathf.Min(Mathf.Clamp((dist + 10f) / Mathf.Max(60f, 170f * k), .32f, .8f), 110f / Mathf.Max(30f, 170f * k)); e.DashDir = dir; } }
                    else if (e.Dash > 0)
                    {
                        e.Dash -= dt; vel = e.DashDir * 170f * k;
                        if (dist < 13 && e.Atk <= 0) { e.Atk = .9f; HurtPlayer(7f * e.Scale, -dir); }
                    }
                    else
                    {
                        vel = (dir + perp * Mathf.Sin(Time * 3f + e.Phase) * .8f).Normalized() * 58f * k;
                        if (e.Cd <= 0 && dist < 100 && AtkSlot()) { e.Tele = .35f; e.Cd = rng.RandfRange(2.2f, 3.2f); }
                    }
                    break;
                case EK.Spitter:
                    {
                        // держит дистанцию, стреляет с упреждением и только при прямой видимости; без неё меняет позицию, а не стреляет в стену
                        e.Cd -= dt; bool los = dist < 190 && LineClear(e.Pos, PPos);
                        if (e.Tele > 0)
                        {
                            e.Tele -= dt; e.Mark = PPos + pVel * (dist / 100f) * .8f;
                            if (e.Tele <= 0)
                            {
                                if (LineClear(e.Pos, e.Mark)) { Projs.Add(new Proj { Pos = e.Pos, Vel = (e.Mark - e.Pos).Normalized() * 100f, Dmg = 7f * e.Scale }); Sfx.Play("shoot", -5); }
                                e.Cd = rng.RandfRange(1.8f, 2.8f);
                            }
                        }
                        else
                        {
                            float side = Mathf.Sign(Mathf.Sin(Time * .7f + e.Phase));
                            if (!los) vel = (dir + perp * side * .5f).Normalized() * 44f;
                            else if (dist < 64) vel = (-dir + perp * side * .4f).Normalized() * 50f;
                            else if (dist > 105) vel = dir * 42f; else vel = perp * side * 26f;
                            vel *= k;
                            if (e.Cd <= 0 && los && dist < 150) e.Tele = .5f;
                        }
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
                case EK.Warden: vel = UpdateGrove(e, dt, dir, dist); break;
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
                        // тяжёлый: медленно идёт, перед ударом по площади замирает (предупреждение), после удара остаётся открытым на секунду
                        if (e.State == 1)
                        {
                            vel = Vector2.Zero; e.Tele -= dt;
                            if (e.Tele <= 0)
                            {
                                Shake = Mathf.Max(Shake, 4); Sfx.Play("boom", -6, 1.4f); for (int q = 0; q < 14; q++) { float a = q * Mathf.Tau / 14; Parts.Add(new Particle { P = e.Pos, V = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 80, Life = .35f, Max = .35f, C = new Color("#cfe6ff"), Size = 2 }); }
                                if (dist < 36) HurtPlayer(14f * e.Scale, -dir); e.State = 3; e.T2 = 1f;
                            }
                        }
                        else if (e.State == 3) { vel = Vector2.Zero; e.T2 -= dt; if (e.T2 <= 0) { e.State = 0; e.Cd = rng.RandfRange(1.4f, 2.4f); } }
                        else
                        {
                            vel = e.Face * 32f * wm * k;
                            if (dist < 14 && e.Atk <= 0) { e.Atk = .9f; HurtPlayer(11f * e.Scale, -dir); }
                            if (dist < 36 && e.Cd <= 0 && AtkSlot()) { e.State = 1; e.Tele = .7f; }
                        }
                    }
                    break;
                default: vel = UpdateOtherEnemy(e, dt, dir, dist, perp, k); break;
            }
            if (!e.IsBoss) vel *= EnemySpeedMul(e);
            for (int pj = projN; pj < Projs.Count; pj++) Projs[pj].Dmg *= e.IsBoss ? ctxDmg : e.Kind == EK.Spitter ? 1f : Tune.T.EnemyDamage * EnemyBalance.Dmg(e.Kind);
            ctxDmg = 1f;
            bool raiding = RaidTick(e, dt, ref vel, dir, dist, lit);
            if (!raiding && !e.IsBoss && e.Ev == null && !(e.Kind == EK.Leech && e.State == 1)) { vel = AvoidCover(e, vel, dir, dist); vel = AvoidLight(e, vel, dir, auraR); }
            if (e.Kind == EK.Leech && e.State == 1) { }
            else { var np = Move(e.Pos, ((e.Slow > 0 ? vel * (e.IsBoss ? .75f : .5f) : vel) * (e.Kind == EK.Eel || e.Kind == EK.DepthWarden || e.Kind == EK.Beetle ? 1f : wm) + e.Kb) * dt, Mathf.Min(5f, e.Radius), e.Kind == EK.Wraith || e.Kind == EK.Warden || e.Kind == EK.DepthWarden || e.Kind == EK.AshWarden || e.Kind == EK.Spirit || e.Kind == EK.WinterWarden || e.Kind == EK.Source || e.Kind == EK.Mirage || e.Kind == EK.Decoy || e.Kind == EK.Stalker);
                if (!e.IsBoss && Barriers.Count > 0 && BarrierCross(e.Pos, np)) { e.Kb = Vector2.Zero; } else e.Pos = np; }
            if (e.Kind == EK.Warden && e.Home is Vector2 gh) { e.Pos = gh; e.Kb = Vector2.Zero; }      // Пожиратель Рощи врос в землю

            // враг не заходит внутрь игрока: иначе мелкий враг шёл «в игроке» с его скоростью, и по нему нельзя было попасть
            if (!e.IsBoss && e.Kind != EK.Leech && e.Kind != EK.Swarm)
            {
                float minD = e.Radius + 6f; var away = e.Pos - PPos; float dd = away.Length();
                if (dd < minD) { var np2 = PPos + (dd > .01f ? away / dd : Vector2.FromAngle(e.Phase)) * minD; if (!Collides(np2, Mathf.Min(5f, e.Radius), false)) e.Pos = np2; }
            }
            if (!e.IsBoss && e.Kind != EK.Leech)
                foreach (var o in Enemies) if (o != e && !o.IsBoss) { var dd = e.Pos - o.Pos; float l = dd.Length(); if (l > .01f && l < 8) { var np3 = e.Pos + dd / l * (8 - l) * .3f; if (!Collides(np3, Mathf.Min(5f, e.Radius), false)) e.Pos = np3; } }

            if (e.Hp <= 0) { var dead = e; Enemies.RemoveAt(i); KillEnemy(dead); }
        }
        if (aiRespawn is Vector2 rp2) { realP = rp2; aiRespawn = null; }
        PPos = realP; aiRemote = 0;
        GroveTick(dt);
        if (attached == 0) LeechT = Mathf.Max(0, LeechT - dt);
    }

    void Explode(Enemy e)
    {
        Burst(e.Pos, new Color("#ff9a2e"), 24, 130); Burst(e.Pos, new Color("#ffe27a"), 10, 80);
        Sfx.Play("boom"); Shake = Mathf.Max(Shake, 4);
        if (PPos.DistanceTo(e.Pos) < 38) HurtPlayer(18f * e.Scale, (PPos - e.Pos).Normalized() * 2f, e.Kind != EK.Bomber || RegionAt(e.Pos) == 2);
    }

    /// <summary>Хранитель глубин: ныряет, бьёт приливом, призывает существ и гасит свет.</summary>
    Vector2 UpdateDepthWarden(Enemy e, float dt, Vector2 dir, float dist, bool water)
    {
        float hpf = e.Hp / e.MaxHp; int ph = PhaseNum(e); float mul = (ph >= 3 ? .6f : 1f) * BossTempoMul();          // фазы — данные BossPhaseBook (66% / 33%)
        if (e.State == 20) return BossSpecialState(e, dt, dir, dist, hpf);
        Vector2 vel = Vector2.Zero; float wm = water ? 1.4f : .6f;
        switch (e.State)
        {
            case 0:     // плывёт к игроку
                vel = dir * 34f * wm; e.T1 -= dt; if (stillT > 1.8f) e.T1 -= dt * 1.5f; if (TryBossSpecial(e, dt, hpf, dist, dir)) break;
                if (dist < 24 && e.Atk <= 0) { e.Atk = 1f; HurtPlayer(10f, -dir); }
                if (e.T1 <= 0)
                {
                    float r = rng.Randf();
                    if (ph >= 2 && r < .22f) { e.State = 5; e.T2 = 1f; }
                    else if (ph >= 2 && r < .5f) { e.State = 6; e.T2 = 1.2f; }
                    else if (r < .75f) { e.State = 1; e.T2 = 2.2f * mul; }
                    else { e.State = 4; e.T2 = .7f; e.Mark = PPos; }
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
                    e.State = 3; e.T2 = .8f;
                }
                break;
            case 3:     // оглушён и уязвим
                e.T2 -= dt; if (e.T2 <= 0) { e.State = 0; e.T1 = 1.1f * mul; }
                break;
            case 4:     // прилив по отмеченной точке
                e.T2 -= dt;
                if (e.T2 <= 0)
                {
                    Shake = 5; Sfx.Play("boom", -2, 1.2f);
                    for (int i = 0; i < 20; i++) { float a = i * Mathf.Tau / 20; Parts.Add(new Particle { P = e.Mark, V = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 90, Life = .5f, Max = .5f, C = new Color("#7ac8ff"), Size = 2 }); }
                    if (PPos.DistanceTo(e.Mark) < 52) HurtPlayer(20f, (PPos - e.Mark).Normalized());
                    RingShot(e.Pos, hpf < .5f ? 10 : 6, new Color("#7ac8ff"));
                    e.State = 0; e.T1 = 1.4f * mul;
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
                    Announce("Свет погас!", "Хранитель гасит фонари: [Q] и [F] ещё горят", new Color("#7ac8ff"), "boss");
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
        if (NetClient) { foreach (var pr in Projs) pr.Pos += pr.Vel * dt; return; }           // клиент только показывает снаряды хоста
        for (int i = Projs.Count - 1; i >= 0; i--)
        {
            var p = Projs[i]; p.Life -= dt; p.Pos += p.Vel * dt;
            var t = ToTile(p.Pos);
            if (p.Dark && IsLit(p.Pos)) { Burst(p.Pos, new Color("#ffe27a"), 4, 40); Projs.RemoveAt(i); continue; }
            if (p.Life <= 0 || World.Blocks(W.Get(t.X, t.Y)) && W.Get(t.X, t.Y) != Tl.Deep && W.Get(t.X, t.Y) != Tl.Stone) {      // снаряды врагов пролетают сквозь камни, но не сквозь стены и постройки
                 Burst(p.Pos, p.C, 3, 30); if (p.Zone) AddZone(p.Pos, 22, .3f, 4f, 6f); Projs.RemoveAt(i); continue; }
            if (NetHost) { bool hitRemote = false; foreach (var r in Remotes()) if (r.Hp > 0 && p.Pos.DistanceTo(r.Pos) < 6f) { Net?.SendHurt(r.PeerId, p.Dmg, p.Vel.Normalized() * .8f, p.Fire); hitRemote = true; break; } if (hitRemote) { Projs.RemoveAt(i); continue; } }
            if (p.Pos.DistanceTo(PPos) < 6f)
            {
                if (Invuln <= 0) { HurtPlayer(p.Dmg, p.Vel.Normalized() * .8f, p.Fire); if (p.Cold) Chill(2.2f); if (p.Zone) AddZone(p.Pos, 22, .3f, 4f, 6f); Projs.RemoveAt(i); }
            }
        }
    }

}
