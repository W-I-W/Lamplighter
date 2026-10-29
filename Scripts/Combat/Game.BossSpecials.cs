using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Уникальная способность каждого из пяти боссов (этап 10). Включается в более поздней фазе, имеет предупреждение (Tele), заканчивается периодом уязвимости там, где это
/// соответствует роли босса, и рассчитана на одного игрока: просветы между лучами и снарядами, рывок и выход из зоны всегда возможны. Состояние 20 общее, разветвление идёт по виду босса.
/// Хранитель леса — теневой вихрь (спираль), Хранитель глубин — водоворот (притяжение), Пепельный страж — перегрев (кольцо огня), Хранитель Зимы — метель (веера с просветом),
/// Источник — эхо (удар по следам игрока, наказывает стояние на месте).</summary>
public partial class Game
{
    readonly List<Vector2> trail = new(); float trailRec;

    void RecordTrail(float dt, Vector2 p) { trailRec -= dt; if (trailRec > 0) return; trailRec = .5f; trail.Add(p); if (trail.Count > 4) trail.RemoveAt(0); }

    static string SpecName(EK k) => k switch { EK.Warden => "Теневой вихрь!", EK.DepthWarden => "Водоворот!", EK.AshWarden => "Перегрев!", EK.WinterWarden => "Метель!", _ => "Эхо тьмы!" };
    static string SpecHint(EK k) => k switch
    {
        EK.Warden => "Двигайтесь по дуге: между лучами есть просветы. После вихря страж открыт",
        EK.DepthWarden => "Выплывайте из воронки или нырните рывком: у центра удар. После него хранитель открыт",
        EK.AshWarden => "Выйдите из кольца огня: между пятнами есть проходы. Страж перегрет и уязвим",
        EK.WinterWarden => "Три веера летят в вас: уходите в просвет, не стойте на месте",
        _ => "Тьма бьёт по вашим следам: не стойте на месте"
    };

    /// <summary>Вызывается в момент выбора следующего действия босса: запускает особую способность, если фаза позволяет и перезарядка прошла.</summary>
    bool TryBossSpecial(Enemy e, float dt, float hpf, float dist, Vector2 dir)
    {
        e.SpecT -= dt; if (e.SpecT > 0 || e.T1 > 0) return false;
        bool ok = e.Kind switch { EK.Warden => hpf < .5f, EK.DepthWarden => hpf < .6f, EK.AshWarden => hpf < .5f, EK.WinterWarden => hpf < .66f, EK.Source => hpf < .66f, _ => false };
        if (!ok) return false;
        e.State = 20; e.SpecPh = 0; e.T2 = e.Kind == EK.Source ? 1f : e.Kind == EK.WinterWarden ? .75f : .9f; e.Tele = e.T2; e.DashDir = dir; e.SpecAng = rng.Randf() * Mathf.Tau; e.SpecN++;
        e.SpecT = (e.Kind switch { EK.Warden => 9f, EK.DepthWarden => 10f, EK.AshWarden => 11f, EK.WinterWarden => 9f, _ => 12f }) * BossTempoMul();
        if (e.SpecN == 1) Announce(SpecName(e.Kind), SpecHint(e.Kind), new Color("#ffd86b"), "boss");
        Sfx.Play("boss", -6, 1.5f); return true;
    }

    Vector2 BossSpecialState(Enemy e, float dt, Vector2 dir, float dist, float hpf)
    {
        Vector2 vel = Vector2.Zero; float tempo = BossTempoMul();
        if (e.SpecPh == 0)
        {
            e.T2 -= dt; if (e.Tele > 0) e.Tele -= dt;
            if (e.Kind == EK.DepthWarden && rng.Randf() < dt * 14) Burst(e.Pos + Vector2.FromAngle(rng.Randf() * Mathf.Tau) * 60, new Color("#7ac8ff"), 1, 35);
            if (e.T2 > 0) return vel;
            e.SpecPh = 1; e.SpecTim = e.Kind switch { EK.Warden => 2.6f, EK.DepthWarden => 2.4f, EK.AshWarden => .2f, EK.WinterWarden => 2.1f, _ => .3f }; e.SpecVol = 0; e.SpecEmit = 0;
            if (e.Kind == EK.AshWarden)
            {
                for (int i = 0; i < 6; i++) AddZone(e.Pos + Vector2.FromAngle(e.SpecAng + i * Mathf.Tau / 6) * 62f, 18, 1.1f, 4f, 12f);
                Sfx.Play("flare", -2, .6f); Shake = Mathf.Max(Shake, 4);
            }
            else if (e.Kind == EK.Source)
            {
                foreach (var p in trail) AddZone(p, 24, .9f, 4.5f, 10f, false, false, false, true);
                LightBlockT = Mathf.Max(LightBlockT, 2.5f); Sfx.Play("pulse", -3, .6f); Shake = Mathf.Max(Shake, 3);
            }
            return vel;
        }
        e.SpecTim -= dt; bool done = e.SpecTim <= 0;
        switch (e.Kind)
        {
            case EK.Warden:
                e.SpecEmit -= dt; e.SpecAng += 2.4f * dt;
                if (e.SpecEmit <= 0)
                {
                    e.SpecEmit = .13f;
                    for (int s = 0; s < 2; s++) Projs.Add(new Proj { Pos = e.Pos, Vel = Vector2.FromAngle(e.SpecAng + s * Mathf.Pi) * 72f, Dmg = 7f, C = new Color("#d58cff"), Life = 3.2f });
                    if (rng.Randf() < .25f) Sfx.Play("shoot", -10, .8f);
                }
                if (done) { if (hpf < .33f) { e.State = 2; e.T2 = .6f; e.DashDir = dir; e.Over = 0; } else { e.State = 4; e.T2 = 1f; e.Over = 1.8f; } e.SpecPh = 0; }
                break;
            case EK.DepthWarden:
                if (aiRemote == 0 && dist < 140 && DashT <= 0) pVel += (e.Pos - PPos).Normalized() * 150f * dt;
                if (rng.Randf() < dt * 20) Burst(e.Pos + Vector2.FromAngle(rng.Randf() * Mathf.Tau) * 110, new Color("#7ac8ff"), 1, 30);
                if (done)
                {
                    if (dist < 48) { Shake = 6; HurtPlayer(16f, -dir); Burst(e.Pos, new Color("#9fd8ff"), 20, 110); }
                    e.State = 3; e.T2 = 1.3f; e.Over = 1.6f; e.SpecPh = 0;
                }
                break;
            case EK.AshWarden:
                if (done) { e.State = 7; e.T2 = 1.5f; e.Over = 2.2f; e.SpecPh = 0; }
                break;
            case EK.WinterWarden:
                e.SpecEmit -= dt;
                if (e.SpecEmit <= 0 && e.SpecVol < 3)
                {
                    e.SpecEmit = .65f; e.SpecVol++; int gap = rng.RandiRange(1, 5); var aim = (PPos - e.Pos).Normalized();
                    for (int i = 0; i < 7; i++) if (i != gap) Projs.Add(new Proj { Pos = e.Pos, Vel = aim.Rotated((i - 3) * .17f) * 95f, Dmg = 6.5f, C = new Color("#9fe8ff"), Cold = true, Life = 3.2f });
                    Sfx.Play("shoot", -3, 1.3f);
                }
                if (done) { e.State = 0; e.T1 = 2.4f * tempo; e.SpecPh = 0; }
                break;
            default:
                if (done) { e.State = 0; e.T1 = 2.6f * tempo; e.SpecPh = 0; }
                break;
        }
        return vel;
    }
}
