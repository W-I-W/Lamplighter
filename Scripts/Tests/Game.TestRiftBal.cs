using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Оценка баланса разломов (LAMP_STAGE=riftbal, LAMP_RIFT=уровень, LAMP_LV=уровень героя): неподвижный герой без уклонений рубит ближайшего врага в досягаемости раз в KILLINT с.
/// Печатает полученный урон и время по волнам. Это пессимистичная модель: настоящий игрок двигается и уклоняется, но и бьёт не всегда.</summary>
public partial class Game
{
    void BuildRiftBalScript()
    {
        int tier = int.TryParse(OS.GetEnvironment("LAMP_RIFT"), out var tt) ? tt : 8; int lv = int.TryParse(OS.GetEnvironment("LAMP_LV"), out var ll) ? ll : 40;
        float dmgMul = float.TryParse(OS.GetEnvironment("LAMP_DMGMUL"), out var dm) ? dm : 3f; float killInt = float.TryParse(OS.GetEnvironment("LAMP_KILLINT"), out var ki) ? ki : .4f;
        S(.3f, () =>
        {
            BossMask |= 31; BeaconLevel = 6; Finished = true; Dawn = 1; Level = lv; Tier = 4; MaxHp = CombatMath.BaseHealth(lv); Hp = MaxHp; EventsEnabled = false;
            foreach (var a in W.Ancients) { a.Lit = true; a.Anim = 1; }
            RiftUnlocked = 8; StartRift(tier); Invuln = 0; Log($"RIFTBAL tier={tier} lv={lv} maxhp={MaxHp} waves={RiftWaves} cap={RiftCap} dmg={PlayerDamage:F1}");
        });
        S(.1f, () =>
        {
            float t = 0, hit = 0, lastHp = Hp, taken = 0; int wave = 0, kills = 0; float waveT0 = 0; float waveTaken = 0; bool dead = false; float minHp = Hp;
            for (; t < 600 && RiftOn && !dead; t += .033f)
            {
                Time += .033f; Invuln = Mathf.Max(0, Invuln - .033f); UpdateEndgame(.033f); UpdateEnemies(.033f); UpdateProjectiles(.033f);
                if (RiftState == 1) { WindowKey(Key.Key1, false); }
                hit -= .033f;
                if (hit <= 0)
                {
                    Enemy best = null; float bd = 56f; foreach (var e in Enemies) if (e.Ev == riftEv && e.Hp > 0 && e.Age > 0) { float d = e.Pos.DistanceTo(PPos); if (d < bd) { bd = d; best = e; } }
                    if (best != null) { best.Hp -= PlayerDamage * dmgMul; hit = killInt; if (best.Hp <= 0) { kills++; best.Hp = 0; } }
                    else { Enemy near = null; float nd = 1e9f; foreach (var e in Enemies) if (e.Ev == riftEv && e.Hp > 0) { float d = e.Pos.DistanceTo(PPos); if (d < nd) { nd = d; near = e; } } hit = 0; if (near != null) PPos += (near.Pos - PPos).Normalized() * 8f; }      // герой идёт к ближайшему врагу (8 px за шаг: быстрее реального бега, зато бот не стоит)
                }
                foreach (var e in Enemies.ToArray()) if (e.Hp <= 0 && e.Ev == riftEv) Enemies.Remove(e);
                if (Hp < lastHp) { taken += lastHp - Hp; waveTaken += lastHp - Hp; } lastHp = Hp; minHp = Mathf.Min(minHp, Hp);
                if (RiftWave != wave) { Log($"RIFTBAL wave {wave} done t={t - waveT0:F0}s taken={waveTaken:F0} kills={kills}"); wave = RiftWave; waveT0 = t; waveTaken = 0; }
                if (((int)(t * 30)) % 900 == 0) { float sd = 0; int n = 0; foreach (var e in Enemies) if (e.Ev == riftEv) { sd += e.Pos.DistanceTo(PPos); n++; } Log($"RIFTBAL probe t={t:F0} alive={n} avgDist={(n > 0 ? sd / n : 0):F0} quota={riftQuota} wave={RiftWave} lit={IsLit(PPos)}"); }
                if (Hp <= 0) { dead = true; Log($"RIFTBAL DEAD at t={t:F0}s wave {RiftWave}"); }
                if (Hp < MaxHp && Hp > 0) { /* регенерации героя не учитываем: пессимистично */ }
                if (Hp > 0 && Hp < lastHp) lastHp = Hp;
                Hp = Mathf.Max(Hp, 0.0001f) > 0 && !dead ? Hp : Hp;
            }
            Log($"RIFTBAL END on={RiftOn} dead={dead} t={t:F0}s taken={taken:F0} minHp={minHp:F0}/{MaxHp} kills={kills}");
        });
    }
}
