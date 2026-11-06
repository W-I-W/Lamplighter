using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lamplighter;

/// <summary>Корень из земли: предупреждение (трещина на почве) Warn секунд, затем корень выходит и бьёт тех, кто стоит в круге R.</summary>
public sealed class GroveRoot { public Vector2 Pos; public float Warn, T, R = 12f, Dmg; public bool Hit; }
/// <summary>Удар ветвью по дуге: сектор кольца R0..R1 вокруг ствола, центр Ang, полуширина Half.</summary>
public sealed class GroveSweep { public Vector2 C; public float Ang, Half, R0, R1, Warn, T, Dmg; public bool Hit; }
/// <summary>Облако ядовитых спор: после Warn секунд опасно Dur секунд (урон тиками раз в полсекунды).</summary>
public sealed class GroveSpore { public Vector2 Pos; public float R, Warn, Dur, T, Tick; }
/// <summary>«Голод Рощи»: круг вокруг ствола делится на N секторов; биты Safe — безопасные сектора (их всегда минимум два, они не соседние).</summary>
public sealed class GroveHunger { public Vector2 C; public float Ang0, R, Warn, T, Dmg; public int N, Safe; public bool Hit; }
/// <summary>Декоративная частица (лист, кора, спора, росток) — не пересылается по сети.</summary>
public sealed class GroveBit { public Vector2 P, V; public float Life, Max, Rot, Spin; public int Kind, Frame; }
/// <summary>Гибель Пожирателя: ядро мигает, трещины, корни рассыпаются, земля оживает (только показ).</summary>
public sealed class GroveDeathFx { public Vector2 Pos; public float T; }

/// <summary>Пожиратель Рощи — первый босс (страж Тёмного леса, идентификатор EK.Warden сохранён для сохранений и прогресса). Огромное заражённое дерево в центре своей арены:
/// не ходит, не прыгает и не делает рывков — атакуют корни и ветви. Фазы и наборы атак — данные BossPhaseBook (100–70 «Пробуждение», 70–35 «Поглощение», 35–0 «Сердце Тьмы»).
/// Хост решает всё (здоровье, фазы, цели, атаки, зоны, уязвимость, смерть, награды) и рассылает события атак (RpcGrove); гости рисуют предупреждения у себя, урон им присылает хост.
/// Защита от «сыра»: удары вблизи (шипы у ствола, ветвь) и на средней дистанции (корни, споры), корень под тем, кто долго стоит на месте, мгновенная реакция на урон издалека и из-за стены арены.</summary>
public partial class Game
{
    public readonly List<GroveRoot> GroveRoots = new();
    public readonly List<GroveSweep> GroveSweeps = new();
    public readonly List<GroveSpore> GroveSpores = new();
    public readonly List<GroveHunger> GroveHungers = new();
    public readonly List<GroveBit> GroveBits = new();
    public GroveDeathFx GroveDeath;
    public Vector2[] GroveNodes = Array.Empty<Vector2>();                     // поглощение: узлы-корни, из которых тянется сила
    public float GroveAbsorbT, GroveAbsorbDur; public bool GroveAbsorbBuff;   // идёт поглощение (сколько осталось), лечение или усиление
    public float GroveInfect;                                                 // доля арены, покрытая заражённой землёй (растёт с фазами; только показ)
    public float GroveHealed; public int GroveAbsorbs, GroveHungerN, GroveFarN, GroveStillN; // счётчики боя (лимит лечения, проверки)
    float groveAbsorbHit, groveAbsorbHealNow, groveHungerCd, groveAbsorbCd, groveThornCd, groveFarCd, groveChaseT, groveAmbT;
    readonly Dictionary<long, (Vector2 anchor, float still)> groveStill = new();

    public const float GroveHealCapPerAbsorb = .07f, GroveHealCapTotal = .14f;   // доля MaxHp: за одно поглощение и за весь бой
    const float GroveInterrupt = .05f;                                          // урон по открытому ядру (доля MaxHp), который срывает поглощение
    const float GroveWarn = .7f;                                                // множитель времени предупреждения атак: корни и ветви срабатывают быстрее, увернуться «на автомате» нельзя
    public const float GroveTransition = 1.6f;                                  // переход фазы: ствол неуязвим

    float GroveArenaR(Enemy e) { var ar = ArenaHolding(e.Pos); return ar != null ? (ar.Radius + .5f) * TS : 150f; }

    /// <summary>Сброс боя (новый бой, проигрыш, смерть босса): никаких висящих зон, корней и счётчиков.</summary>
    public void GroveReset(bool keepDeath = true)
    {
        GroveRoots.Clear(); GroveSweeps.Clear(); GroveSpores.Clear(); GroveHungers.Clear(); GroveNodes = Array.Empty<Vector2>(); GroveAbsorbT = 0; GroveAbsorbBuff = false;
        GroveHealed = 0; GroveAbsorbs = 0; GroveHungerN = 0; GroveFarN = 0; GroveStillN = 0; groveAbsorbHit = 0; groveAbsorbHealNow = 0; groveHungerCd = 6f; groveAbsorbCd = 3f; groveThornCd = 0; groveFarCd = 0; groveChaseT = 0;
        groveStill.Clear(); if (!keepDeath) GroveDeath = null;
    }

    // ───────────────────────── ИИ (хост) ─────────────────────────
    Vector2 UpdateGrove(Enemy e, float dt, Vector2 dir, float dist)
    {
        if (e.Home is Vector2 h) e.Pos = h; e.Kb = Vector2.Zero;                   // неподвижен: ни отбрасывания, ни шагов
        var pc = PhaseOf(e); var ph = pc.Current; int pn = pc.Index;
        float tempo = ph.Tempo * BossTempoMul() * (e.T3 > 0 ? .75f : 1f);
        if (e.T3 > 0) e.T3 -= dt;
        groveHungerCd -= dt; groveAbsorbCd -= dt; groveThornCd -= dt;
        e.Shield = pn >= 2 && e.Over <= 0 && e.State != 9;                       // «Сердце Тьмы»: кора закрыла ядро, урон по стволу слабый — пока ядро не откроется
        switch (e.State)
        {
            case 9:     // переход фазы: ствол неуязвим, корни бьют по земле
                e.T2 -= dt;
                if (rng.Randf() < dt * 18) GroveBurst(e.Pos + new Vector2(rng.RandfRange(-40, 40), rng.RandfRange(-100, -30)), 0, 1, 50);
                if (e.T2 <= 0) { e.State = 0; e.T1 = 1.1f * tempo; }
                break;
            case 0:
                e.T1 -= dt; if (e.T1 <= 0) GroveChoose(e, pc, dist, tempo);
                break;
            case 1: case 2: case 3:      // анимации каста корней, ветви, спор — сами удары идут по своим таймерам
                e.T2 -= dt; if (e.T2 <= 0) { e.State = 0; e.T1 = (pn == 2 ? .7f : 1.0f) * tempo; }
                break;
            case 4:     // поглощение зелени: ядро открыто, линии к корням
                GroveAbsorbT -= dt;
                if (!GroveAbsorbBuff)
                {
                    float cap = Mathf.Min(GroveHealCapPerAbsorb * e.MaxHp - groveAbsorbHealNow, GroveHealCapTotal * e.MaxHp - GroveHealed);
                    float heal = Mathf.Clamp(e.MaxHp * .022f * GroveNodes.Length * dt, 0, Mathf.Max(0, cap));
                    heal = Mathf.Min(heal, e.MaxHp - e.Hp);
                    if (heal > 0) { e.Hp += heal; GroveHealed += heal; groveAbsorbHealNow += heal; }
                }
                if (GroveAbsorbT <= 0)
                {
                    GroveAbsorbT = 0; GroveNodes = Array.Empty<Vector2>(); e.Over = 0;
                    if (GroveAbsorbBuff) { e.T3 = 9f; Toast("Пожиратель Рощи напитался силой: атакует быстрее"); }
                    GroveNet(5, GroveAbsorbBuff ? 2 : 1);
                    e.State = 0; e.T1 = 1.2f * tempo; groveAbsorbCd = 14f;
                }
                break;
            case 5:     // «Голод Рощи»: замах (сам удар — по таймеру сектора)
                e.T2 -= dt;
                if (e.T2 <= 0) { e.State = 6; e.T2 = 4.5f; e.Over = 4.5f; GroveNet(7, 4.5f); Burst(e.Pos + new Vector2(0, -54), new Color("#ffe27a"), 24, 120); }
                break;
            case 6: case 7:  // ядро открыто после «Голода» / оглушение после сорванного поглощения
                e.T2 -= dt; if (e.T2 <= 0) { e.State = 0; e.T1 = 1f * tempo; }
                break;
        }
        return Vector2.Zero;
    }

    /// <summary>Выбор следующей атаки из набора текущей фазы (данные фазы, а не код интерфейса).</summary>
    void GroveChoose(Enemy e, BossPhaseController pc, float dist, float tempo)
    {
        var ph = pc.Current; int pn = pc.Index; var target = PPos;
        float near = NearestPlayerDist(e.Pos);
        if (ph.Has("thorns") && near < 38f && groveThornCd <= 0) { GroveThorns(e, pn); e.State = 1; e.T2 = .8f; groveThornCd = 4.5f; return; }
        if (ph.Has("hunger") && groveHungerCd <= 0) { GroveHungerStart(e); return; }
        if (ph.Has("absorb") && groveAbsorbCd <= 0 && GroveAbsorbT <= 0) { GroveAbsorbStart(e); return; }
        var opts = new List<(string a, float w)>();
        if (ph.Has("root")) opts.Add(("root", 3f));
        if (ph.Has("rootseries")) opts.Add(("rootseries", 3.5f));
        if (ph.Has("branch")) opts.Add(("branch", dist < 104 ? 3.5f : .6f));
        if (ph.Has("spores")) opts.Add(("spores", 2.2f));
        float sum = opts.Sum(o => o.w), r = rng.Randf() * sum; string pick = opts.Count > 0 ? opts[0].a : "root";
        foreach (var o in opts) { r -= o.w; if (r <= 0) { pick = o.a; break; } }
        switch (pick)
        {
            case "root":
                {
                    float w = (pn == 0 ? .95f : .8f) * GroveWarn;
                    GroveRootAt(target + pVelOf(target) * .25f, w, 14f); GroveRootAt(target + pVelOf(target) * .6f, w + .15f, 14f);          // под героем и на опережение бегущему
                    GroveRootAt(target + Vector2.FromAngle(rng.Randf() * Mathf.Tau) * rng.RandfRange(26f, 44f), w + .1f, 14f); GroveRootOthers(target, .95f * GroveWarn, 12f);
                }
                e.State = 1; e.T2 = .8f; Sfx.Play("break", -4, .55f); break;
            case "rootseries":
                {
                    var d = (target - e.Pos); if (d.LengthSquared() < 1) d = Vector2.Down; d = d.Normalized();
                    float len = Mathf.Max(dist + 30f, 80f); int n = Mathf.Clamp((int)(len / 22f), 4, 7);
                    for (int i = 0; i < n; i++) GroveRootAt(e.Pos + d * (30f + i * 22f), (.55f + i * .13f) * GroveWarn, 12f);
                    GroveRootOthers(target, .7f * GroveWarn, 12f); e.State = 1; e.T2 = .7f; Sfx.Play("break", -2, .5f); break;
                }
            case "branch":
                {
                    var d = target - e.Pos; float ang = d.LengthSquared() < 1 ? Mathf.Pi / 2 : d.Angle();
                    var s = new GroveSweep { C = e.Pos + new Vector2(0, -10), Ang = ang, Half = pn == 2 ? 1.2f : .95f, R0 = 14f, R1 = 100f, Warn = (pn == 2 ? .75f : 1f) * GroveWarn, Dmg = 16f };
                    GroveSweeps.Add(s); GroveNet(2, s.C.X, s.C.Y, s.Ang, s.Half, s.R0, s.R1, s.Warn);
                    e.State = 2; e.T2 = s.Warn + .35f; Sfx.Play("dash", -6, .45f); break;
                }
            default:    // споры
                {
                    int n = pn == 2 ? 4 : 3;
                    GroveSporeAt(target, 22f, 1.0f, 5f);
                    foreach (var r2 in Remotes()) if (r2.Hp > 0 && r2.Pos.DistanceTo(e.Pos) < 260 && r2.Pos.DistanceTo(target) > 30) { GroveSporeAt(r2.Pos, 22f, 1.0f, 5f); n--; }
                    for (int i = 1; i < n; i++) GroveSporeAt(target + Vector2.FromAngle(rng.Randf() * Mathf.Tau) * rng.RandfRange(34, 70), 20f, 1.1f + i * .15f, 5f);
                    e.State = 3; e.T2 = 1f; Sfx.Play("flare", -6, .5f); break;
                }
        }
    }

    Vector2 pVelOf(Vector2 target) => target == PPos && aiRemote == 0 ? pVel : Vector2.Zero;

    /// <summary>Корень под каждым другим игроком на арене (в кооперативе бьют всех, не только цель).</summary>
    void GroveRootOthers(Vector2 except, float warn, float dmg)
    {
        int n = 0;
        foreach (var (id, p) in GrovePlayers()) { if (p.DistanceTo(except) < 20f || ++n > 3) continue; GroveRootAt(p, warn, dmg); }
    }

    /// <summary>Шипы у ствола: кольцо корней вокруг — ответ на тех, кто прилип к стволу.</summary>
    void GroveThorns(Enemy e, int pn)
    {
        int n = pn == 2 ? 9 : 7; float a0 = rng.Randf() * Mathf.Tau;
        for (int i = 0; i < n; i++) GroveRootAt(e.Pos + Vector2.FromAngle(a0 + i * Mathf.Tau / n) * 26f, .7f, 12f);
        Sfx.Play("break", -2, .7f);
    }

    void GroveAbsorbStart(Enemy e)
    {
        float R = GroveArenaR(e) * .72f, a0 = rng.Randf() * Mathf.Tau;
        GroveNodes = new Vector2[3]; for (int i = 0; i < 3; i++) GroveNodes[i] = e.Pos + Vector2.FromAngle(a0 + i * Mathf.Tau / 3) * R;
        GroveAbsorbBuff = GroveHealed >= GroveHealCapTotal * e.MaxHp - 1f;               // лечение исчерпано — дальше поглощение только усиливает
        GroveAbsorbDur = GroveAbsorbT = 4.2f; groveAbsorbHit = 0; groveAbsorbHealNow = 0; GroveAbsorbs++;
        e.State = 4; e.Over = GroveAbsorbDur;                                                 // ядро открыто, пока тянет силу
        var a = new List<float> { GroveAbsorbDur, GroveAbsorbBuff ? 1 : 0 }; foreach (var p in GroveNodes) { a.Add(p.X); a.Add(p.Y); }
        GroveNet(4, a.ToArray());
        if (GroveAbsorbs == 1) Announce("Поглощение зелени!", "Пожиратель тянет силу из корней. Бейте открытое ядро, чтобы сорвать", new Color("#c8e04a"), "boss");
        Sfx.Play("pulse", -4, .5f);
    }

    void GroveHungerStart(Enemy e)
    {
        int n = 8; int s0 = rng.RandiRange(0, n - 1);
        int safe = (1 << s0) | (1 << ((s0 + 3) % n)) | (1 << ((s0 + 5) % n));            // три безопасных сектора, ни один не соседний с другим
        var hg = new GroveHunger { C = e.Pos, Ang0 = rng.Randf() * Mathf.Tau, R = GroveArenaR(e) + 24f, Warn = 1.5f, Dmg = 30f, N = n, Safe = safe };
        GroveHungers.Add(hg); GroveHungerN++; groveHungerCd = 13f;
        GroveNet(6, hg.C.X, hg.C.Y, hg.Ang0, hg.R, hg.Warn, hg.N, hg.Safe);
        e.State = 5; e.T2 = hg.Warn;
        if (GroveHungerN == 1) Announce("Голод Рощи!", "Встаньте в светлый сектор. После удара ядро открыто", new Color("#b04aff"), "boss");
        Sfx.Play("boss", -2, .7f); Shake = Mathf.Max(Shake, 3);
    }

    /// <summary>Сектор «Голода», в котором стоит точка (-1 — вне круга).</summary>
    public static int HungerSector(GroveHunger h, Vector2 p)
    {
        var d = p - h.C; if (d.Length() > h.R) return -1;
        float a = Mathf.PosMod(d.Angle() - h.Ang0, Mathf.Tau); return Mathf.Clamp((int)(a / (Mathf.Tau / h.N)), 0, h.N - 1);
    }
    public static bool HungerSafe(GroveHunger h, Vector2 p) { int s = HungerSector(h, p); return s < 0 || (h.Safe & (1 << s)) != 0; }

    void GroveRootAt(Vector2 p, float warn, float dmg)
    {
        var r = new GroveRoot { Pos = p, Warn = warn, Dmg = dmg }; GroveRoots.Add(r); if (GroveRoots.Count > 60) GroveRoots.RemoveAt(0);
        GroveNet(1, p.X, p.Y, warn, r.R);
    }

    void GroveSporeAt(Vector2 p, float r, float warn, float dur)
    {
        GroveSpores.Add(new GroveSpore { Pos = p, R = r, Warn = warn, Dur = dur }); if (GroveSpores.Count > 24) GroveSpores.RemoveAt(0);
        GroveNet(3, p.X, p.Y, r, warn, dur);
    }

    /// <summary>Все живые игроки поблизости от боя: (пир, позиция); 0 — этот компьютер. Вызывается вне цикла ИИ, где PPos — настоящая позиция.</summary>
    IEnumerable<(long id, Vector2 p)> GrovePlayers()
    {
        if (Hp > 0) yield return (0, groveLocal ?? PPos);
        foreach (var r in Remotes()) if (r.Hp > 0) yield return (r.PeerId, r.Pos);
    }
    Vector2? groveLocal;

    /// <summary>Урон всем игрокам, кто попал в область (хост): себе — HurtPlayer, гостю — SendHurt.</summary>
    void GroveHit(Func<Vector2, bool> inside, float dmg, Vector2 from)
    {
        if (NetClient) return;
        float mul = BossDamageMul(EK.Warden) * (Boss?.Kind == EK.Warden && Boss.T3 > 0 ? 1.25f : 1f);
        foreach (var (id, p) in GrovePlayers().ToList())
        {
            if (!inside(p)) continue; var kb = (p - from).LengthSquared() > .01f ? (p - from).Normalized() : Vector2.Up;
            if (id == 0) { long sv = aiRemote; aiRemote = 0; float c = ctxDmg; ctxDmg = mul; HurtPlayer(dmg, kb); ctxDmg = c; aiRemote = sv; }
            else Net?.SendHurt(id, dmg * mul, kb, false);
        }
    }

    /// <summary>Урон по Пожирателю: множитель (переход — неуязвим, кора «Сердца Тьмы» — слабый урон, удар из-за стены — слабый), реакция на дальний удар и срыв поглощения.</summary>
    float GroveDamageMul(Enemy e, Vector2 from)
    {
        if (e.State == 9) { Burst(e.Pos + new Vector2(0, -54), new Color("#e8ffd0"), 3, 50); return 0f; }
        float m = 1f;
        if (e.Shield) { m *= .3f; if (rng.Randf() < .5f) GroveBurst(e.Pos + new Vector2(rng.RandfRange(-12, 12), -40), 1, 2, 50); }
        var ar = ArenaHolding(e.Pos); bool outside = ar != null && !ar.Contains(from, 2); bool far = from.DistanceTo(e.Pos) > 150f;
        if (outside) m *= .25f;
        if ((outside || far) && groveFarCd <= 0)
        {
            groveFarCd = 1.6f; GroveFarN++;
            GroveRootAt(from, .55f, 14f);                                                   // корень под стрелком
            var d = (from - (e.Pos + new Vector2(0, -50))).Normalized();
            for (int i = -1; i <= 1; i++) Projs.Add(new Proj { Pos = e.Pos + new Vector2(0, -50), Vel = d.Rotated(i * .14f) * 120f, Dmg = 8f * BossDamageMul(EK.Warden), C = new Color("#7ad45a"), Life = 3f });
            Sfx.Play("shoot", -4, .6f);
        }
        return m;
    }

    void GroveAfterHurt(Enemy e, float dmg)
    {
        if (e.State != 4 || GroveAbsorbT <= 0) return;
        groveAbsorbHit += dmg;
        if (groveAbsorbHit < e.MaxHp * GroveInterrupt) return;
        GroveAbsorbT = 0; GroveNodes = Array.Empty<Vector2>(); e.State = 7; e.T2 = 2.2f; e.Over = 2.2f; groveAbsorbCd = 12f;
        Announce("Поглощение прервано!", "Ядро открыто — бейте!", new Color("#ffe27a"), "ancient"); Shake = Mathf.Max(Shake, 4);
        GroveBurst(e.Pos + new Vector2(0, -54), 1, 14, 110); GroveNet(5, 0);
    }

    /// <summary>Хост: смена фазы Пожирателя — переход (ствол неуязвим), подсказка.</summary>
    void GrovePhase(Enemy e, BossPhaseController pc)
    {
        e.State = 9; e.T2 = GroveTransition; e.Over = 0; GroveAbsorbT = 0; GroveNodes = Array.Empty<Vector2>();
        groveHungerCd = pc.Index == 2 ? 2.5f : groveHungerCd; groveAbsorbCd = pc.Index == 1 ? 1.5f : groveAbsorbCd;
        Toast(pc.Index == 1 ? "Пожиратель выпускает споры и тянет силу из корней" : "Кора закрыла ядро: ждите «Голод Рощи» и бейте открытое ядро");
    }

    // ───────────────────────── такт (хост и гость) ─────────────────────────
    /// <summary>Таймеры корней, ветвей, спор и «Голода»: хост наносит урон, гость только показывает. Плюс защита от стояния на месте и от боя из-за стены (хост).</summary>
    void GroveTick(float dt)
    {
        groveLocal = PPos;
        var boss = Enemies.Find(x => x.Kind == EK.Warden && x.Hp > 0);
        groveFarCd -= dt;
        for (int i = GroveRoots.Count - 1; i >= 0; i--)
        {
            var r = GroveRoots[i]; r.T += dt;
            if (!r.Hit && r.T >= r.Warn)
            {
                r.Hit = true; GroveBurst(r.Pos, 1, 6, 60); GroveBurst(r.Pos, 2, 3, 40); if (r.Pos.DistanceTo(PPos) < 200) { Shake = Mathf.Max(Shake, 1.5f); Sfx.Play("break", -8, .6f + rng.Randf() * .2f); }
                var rr = r; GroveHit(p => p.DistanceTo(rr.Pos) < rr.R + 3f, r.Dmg, r.Pos);
            }
            if (r.T > r.Warn + .75f) GroveRoots.RemoveAt(i);
        }
        for (int i = GroveSweeps.Count - 1; i >= 0; i--)
        {
            var s = GroveSweeps[i]; s.T += dt;
            if (!s.Hit && s.T >= s.Warn)
            {
                s.Hit = true; Sfx.Play("spin", -4, .45f); Shake = Mathf.Max(Shake, 2.5f);
                for (int k = 0; k < 10; k++) GroveBurst(s.C + Vector2.FromAngle(s.Ang + rng.RandfRange(-s.Half, s.Half)) * rng.RandfRange(s.R0, s.R1), 0, 1, 40);
                var ss = s; GroveHit(p => { var d = p - ss.C; float l = d.Length(); return l >= ss.R0 - 4 && l <= ss.R1 + 4 && Mathf.Abs(Mathf.AngleDifference(ss.Ang, d.Angle())) <= ss.Half + .08f; }, s.Dmg, s.C);
            }
            if (s.T > s.Warn + .4f) GroveSweeps.RemoveAt(i);
        }
        for (int i = GroveSpores.Count - 1; i >= 0; i--)
        {
            var s = GroveSpores[i]; s.T += dt;
            if (s.T >= s.Warn)
            {
                s.Tick -= dt;
                if (s.Tick <= 0) { s.Tick = .5f; var ss = s; GroveHit(p => p.DistanceTo(ss.Pos) < ss.R, 4f, s.Pos); }
                if (rng.Randf() < dt * 6) GroveBurst(s.Pos + new Vector2(rng.RandfRange(-s.R, s.R) * .7f, rng.RandfRange(-s.R, s.R) * .5f), 2, 1, 10);
            }
            if (s.T > s.Warn + s.Dur) GroveSpores.RemoveAt(i);
        }
        for (int i = GroveHungers.Count - 1; i >= 0; i--)
        {
            var h = GroveHungers[i]; h.T += dt;
            if (!h.Hit && h.T >= h.Warn)
            {
                h.Hit = true; Shake = Mathf.Max(Shake, 7); Flash = Mathf.Max(Flash, .25f); Sfx.Play("boom", 0, .55f);
                for (int s = 0; s < h.N; s++) if ((h.Safe & (1 << s)) == 0) for (int k = 0; k < 6; k++) { float a = h.Ang0 + (s + rng.Randf()) * Mathf.Tau / h.N; GroveBurst(h.C + Vector2.FromAngle(a) * rng.RandfRange(20, h.R), k % 2, 1, 50); }
                var hh = h; GroveHit(p => !HungerSafe(hh, p), h.Dmg, h.C);
            }
            if (h.T > h.Warn + .6f) GroveHungers.RemoveAt(i);
        }
        if (boss != null)
        {
            int pn = PhaseOf(boss).Index; GroveInfect = Mathf.MoveToward(GroveInfect, pn == 0 ? .45f : pn == 1 ? .72f : 1f, dt * .25f);      // заражённая земля расползается с фазами
            if (!NetClient) GroveAntiAbuse(boss, dt);
            GroveTrunkPush(boss);
            groveAmbT -= dt; if (groveAmbT <= 0) { groveAmbT = .25f; if (boss.Pos.DistanceTo(PPos) < 300) GroveBurst(boss.Pos + new Vector2(rng.RandfRange(-46, 46), rng.RandfRange(-110, -70)), 0, 1, 8); }
        }
        else if (GroveDeath == null) GroveInfect = Mathf.MoveToward(GroveInfect, 0, dt * .3f);
        if (GroveDeath != null) { GroveDeath.T += dt; GroveDeathTick(dt); if (GroveDeath.T > 9f) GroveDeath = null; }
        for (int i = GroveBits.Count - 1; i >= 0; i--)
        {
            var b = GroveBits[i]; b.Life -= dt; if (b.Life <= 0) { GroveBits.RemoveAt(i); continue; }
            b.P += b.V * dt; b.Rot += b.Spin * dt;
            if (b.Kind == 0) b.V = new Vector2(b.V.X * (1 - dt) + Mathf.Sin(b.Life * 5 + b.Rot) * 10 * dt, Mathf.MoveToward(b.V.Y, 16f, 40 * dt));      // листья планируют вниз
            else if (b.Kind == 1) b.V.Y += 160 * dt;                                                                                            // кора падает
            else b.V *= 1 - dt * 1.5f;
        }
        if (GroveBits.Count > 260) GroveBits.RemoveRange(0, GroveBits.Count - 260);
    }

    /// <summary>Хост: наказание за стояние на месте (корень под ногами) и корни, догоняющие тех, кто держится далеко от ствола.</summary>
    void GroveAntiAbuse(Enemy boss, float dt)
    {
        if (boss.State == 9) return;
        int pn = PhaseOf(boss).Index; float limit = pn == 0 ? 3.2f : pn == 1 ? 2.6f : 2.1f;
        var seen = new HashSet<long>();
        foreach (var (id, p) in GrovePlayers().ToList())
        {
            seen.Add(id); if (p.DistanceTo(boss.Pos) > 260) continue;
            (Vector2 anchor, float still) cur = groveStill.TryGetValue(id, out var v) ? v : (p, 0f);
            if (p.DistanceTo(cur.anchor) > 12f) cur = (p, 0f); else cur.still += dt;
            if (cur.still > limit && !GroveRoots.Exists(r => !r.Hit && r.Pos.DistanceTo(p) < 16)) { GroveRootAt(p, .6f, 14f); GroveStillN++; cur = (p, 0f); }
            groveStill[id] = cur;
        }
        foreach (var k in groveStill.Keys.ToList()) if (!seen.Contains(k)) groveStill.Remove(k);
        groveChaseT -= dt;
        if (groveChaseT <= 0)
        {
            groveChaseT = 2.4f;
            foreach (var (id, p) in GrovePlayers().ToList()) { float d = p.DistanceTo(boss.Pos); if (d > 140f && d < 330f) GroveRootAt(p, .75f, 12f); }      // далеко от ствола не отсидеться
        }
    }

    /// <summary>Ствол твёрдый: свой герой не проходит сквозь него (гость — у себя).</summary>
    void GroveTrunkPush(Enemy boss)
    {
        var d = PPos - boss.Pos; float l = d.Length(); const float R = 18f;
        if (l >= R) return; var np = boss.Pos + (l > .01f ? d / l : Vector2.Down) * R;
        if (!Collides(np, 5f, false)) PPos = np;
    }

    void GroveBurst(Vector2 p, int kind, int n, float speed)
    {
        for (int i = 0; i < n; i++)
        {
            float life = kind == 0 ? rng.RandfRange(1.2f, 2.2f) : kind == 1 ? rng.RandfRange(.5f, .9f) : rng.RandfRange(.9f, 1.6f);
            var v = Vector2.FromAngle(rng.Randf() * Mathf.Tau) * rng.RandfRange(.3f, 1f) * speed; if (kind == 1) v.Y -= speed * .6f;
            GroveBits.Add(new GroveBit { P = p, V = v, Life = life, Max = life, Rot = rng.Randf() * Mathf.Tau, Spin = rng.RandfRange(-6, 6), Kind = kind, Frame = rng.RandiRange(0, 3) });
        }
    }

    // ───────────────────────── гибель ─────────────────────────
    /// <summary>Показ гибели (хост, победивший гость и остальные гости — у каждого один раз за бой).</summary>
    public void GroveDie(Vector2 pos)
    {
        if (GroveDeath != null && GroveDeath.T < 8f) return;
        GroveDeath = new GroveDeathFx { Pos = pos }; GroveRoots.Clear(); GroveSweeps.Clear(); GroveSpores.Clear(); GroveHungers.Clear(); GroveNodes = Array.Empty<Vector2>(); GroveAbsorbT = 0;
        Sfx.Play("boss", 0, .5f);
    }

    void GroveDeathTick(float dt)
    {
        var d = GroveDeath; float t = d.T;
        if (t < 1.6f && rng.Randf() < dt * 30) GroveBurst(d.Pos + new Vector2(rng.RandfRange(-40, 40), rng.RandfRange(-100, 0)), 1, 1, 60);      // кора трескается
        if (t > .8f && t < 2.6f && rng.Randf() < dt * 22) GroveBurst(d.Pos + Vector2.FromAngle(rng.Randf() * Mathf.Tau) * rng.RandfRange(20, 90), 1, 1, 30);   // корни рассыпаются
        if (t - dt < 1.5f && t >= 1.5f) { Flash = Mathf.Max(Flash, .6f); Burst(d.Pos + new Vector2(0, -54), new Color("#fff2b0"), 40, 160); Sfx.Play("ancient", -2, 1.1f); GroveBurst(d.Pos + new Vector2(0, -60), 0, 30, 120); }
        if (t > 1.6f && t < 4f && rng.Randf() < dt * 16) GroveBurst(d.Pos + Vector2.FromAngle(rng.Randf() * Mathf.Tau) * rng.RandfRange(10, 150), 3, 1, 10);    // земля оживает: ростки и светлячки
        GroveInfect = Mathf.MoveToward(GroveInfect, 0, dt * .5f);
    }

    // ───────────────────────── сеть ─────────────────────────
    /// <summary>Хост → гости: событие атаки (гость рисует предупреждение и удар у себя; урон присылает хост).</summary>
    void GroveNet(int ev, params float[] a) { if (NetHost) Net?.SendGrove(ev, a); }

    /// <summary>Гость: событие Пожирателя от хоста.</summary>
    public void NetGrove(int ev, float[] a)
    {
        if (!NetClient || a == null || a.Length > 32) return; foreach (var v in a) if (!float.IsFinite(v)) return;
        bool Ok(float x, float y) => x >= 0 && y >= 0 && x <= World.Wd * TS && y <= World.Ht * TS;
        switch (ev)
        {
            case 1: if (a.Length >= 4 && Ok(a[0], a[1])) GroveRoots.Add(new GroveRoot { Pos = new Vector2(a[0], a[1]), Warn = Mathf.Clamp(a[2], 0, 3), R = Mathf.Clamp(a[3], 4, 30) }); break;
            case 2: if (a.Length >= 7 && Ok(a[0], a[1])) GroveSweeps.Add(new GroveSweep { C = new Vector2(a[0], a[1]), Ang = a[2], Half = Mathf.Clamp(a[3], .1f, 3f), R0 = Mathf.Clamp(a[4], 0, 200), R1 = Mathf.Clamp(a[5], 0, 220), Warn = Mathf.Clamp(a[6], 0, 3) }); break;
            case 3: if (a.Length >= 5 && Ok(a[0], a[1])) GroveSpores.Add(new GroveSpore { Pos = new Vector2(a[0], a[1]), R = Mathf.Clamp(a[2], 4, 60), Warn = Mathf.Clamp(a[3], 0, 3), Dur = Mathf.Clamp(a[4], 0, 10) }); break;
            case 4:
                if (a.Length < 2) break;
                GroveAbsorbDur = GroveAbsorbT = Mathf.Clamp(a[0], 0, 10); GroveAbsorbBuff = a[1] > 0; GroveAbsorbs++;
                var nodes = new List<Vector2>(); for (int i = 2; i + 1 < a.Length && nodes.Count < 6; i += 2) if (Ok(a[i], a[i + 1])) nodes.Add(new Vector2(a[i], a[i + 1]));
                GroveNodes = nodes.ToArray(); break;
            case 5:
                GroveAbsorbT = 0; GroveNodes = Array.Empty<Vector2>();
                if (a.Length > 0 && a[0] == 0) { Announce("Поглощение прервано!", "Ядро открыто — бейте!", new Color("#ffe27a"), "ancient"); var b = Enemies.Find(x => x.Kind == EK.Warden); if (b != null) GroveBurst(b.Pos + new Vector2(0, -54), 1, 14, 110); }
                break;
            case 6: if (a.Length >= 7 && Ok(a[0], a[1])) { GroveHungers.Add(new GroveHunger { C = new Vector2(a[0], a[1]), Ang0 = a[2], R = Mathf.Clamp(a[3], 10, 400), Warn = Mathf.Clamp(a[4], 0, 4), N = Math.Clamp((int)a[5], 2, 16), Safe = (int)a[6] }); GroveHungerN++; } break;
            case 7: { var b = Enemies.Find(x => x.Kind == EK.Warden); if (b != null) Burst(b.Pos + new Vector2(0, -54), new Color("#ffe27a"), 24, 120); break; }
        }
    }

    // ───────────────────────── фазы всех боссов ─────────────────────────
    /// <summary>Контроллер фаз босса (создаётся по данным BossPhaseBook при первом обращении); null — не босс.</summary>
    public static BossPhaseController PhaseOf(Enemy e) => e == null || !e.IsBoss ? null : e.PhaseCtl ??= new BossPhaseController(BossPhaseBook.For(e.Kind));

    /// <summary>Номер фазы с единицы (для ИИ существующих боссов).</summary>
    static int PhaseNum(Enemy e) => PhaseOf(e)?.Number ?? 1;

    /// <summary>Хост: шаг контроллера фаз по здоровью (не больше одного перехода за такт).</summary>
    void BossPhaseTick(Enemy e)
    {
        var pc = PhaseOf(e); if (pc == null || !pc.Update(e.Hp / Mathf.Max(1f, e.MaxHp))) return;
        OnBossPhase(e, pc);
        if (e.Kind == EK.Warden) GrovePhase(e, pc);
    }

    /// <summary>Смена фазы у всех (хост и гость): звук, тряска, отметка времени для вспышки и подписи на шкале.</summary>
    void OnBossPhase(Enemy e, BossPhaseController pc)
    {
        pc.ChangedAt = Time; Sfx.Play("boss", -3, .85f); Sfx.Play("pulse", -8, 1.4f); Shake = Mathf.Max(Shake, 4);
        Burst(e.Pos + new Vector2(0, e.Kind == EK.Warden ? -54 : -8), pc.Current.Tint, 26, 130);
        Log2($"boss phase {e.Kind} -> {pc.Number}/{pc.Count} {pc.Current.Id}");
    }

    /// <summary>Ближайшая к точке часть тела врага: у Пожирателя Рощи это весь ствол от основания до ядра (по нему попадают и в ядро, и в корни), у остальных — центр.</summary>
    public static Vector2 BodyPoint(Enemy e, Vector2 p)
    {
        if (e.Kind != EK.Warden) return e.Pos;
        var a = e.Pos; var b = e.Pos + new Vector2(0, -64); var ab = b - a; float t = Mathf.Clamp((p - a).Dot(ab) / ab.LengthSquared(), 0, 1); return a + ab * t;
    }
    public static float BodyDist(Enemy e, Vector2 p) => BodyPoint(e, p).DistanceTo(p);

    void Log2(string s) { if (testing) GD.Print($"[boss] {s}"); }
}
