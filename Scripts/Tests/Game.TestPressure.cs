using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Давление тьмы (LAMP_STAGE=pressure, LAMP_LV, LAMP_DMGMUL, LAMP_KILLINT, LAMP_LIT=кол-во зажжённых фонарей): герой стоит в тёмной зоне, враги появляются как в игре; бот идёт к ближайшему врагу и бьёт.
/// Печатает урон и убийства по 30-секундным отрезкам: сколько здоровья теряет игрок, который почти не уклоняется (пессимистично) и не лечится.</summary>
public partial class Game
{
    void BuildPressureScript()
    {
        int lv = int.TryParse(OS.GetEnvironment("LAMP_LV"), out var ll) ? ll : 20; float dmgMul = float.TryParse(OS.GetEnvironment("LAMP_DMGMUL"), out var dm) ? dm : 3f;
        float killInt = float.TryParse(OS.GetEnvironment("LAMP_KILLINT"), out var ki) ? ki : .4f; int lit = int.TryParse(OS.GetEnvironment("LAMP_LIT"), out var li) ? li : 3;
        Vector2 spot = default;
        S(.3f, () =>
        {
            Level = lv; Tier = Math.Min(4, lv / 8); MaxHp = CombatMath.BaseHealth(lv); Hp = MaxHp; EventsEnabled = false; noHeal = true;
            for (int i = 1; i <= lit && i < W.Ancients.Count; i++) { W.Ancients[i].Lit = true; W.Ancients[i].Anim = 1; }
            spot = FindDarkArena(); PPos = spot; Enemies.Clear(); Projs.Clear(); Invuln = 0; spawnT = 1f;
            Log($"PRESSURE lv={lv} maxhp={MaxHp} dmg={PlayerDamage:F1} litCount={LitCount} spot={ToTile(spot)} lit={IsLit(spot)}");
        });
        S(.1f, () =>
        {
            float hit = 0, seg = 0, segTaken = 0, lastHp = Hp, total = 0; int kills = 0, segKills = 0; bool dead = false; int peak = 0;
            for (float t = 0; t < 180 && !dead; t += .033f)
            {
                Time += .033f; Invuln = Mathf.Max(0, Invuln - .033f); regenDelay = 99;
                UpdateSpawning(.033f); UpdateEnemies(.033f); UpdateProjectiles(.033f); peak = Math.Max(peak, Enemies.Count);
                hit -= .033f;
                if (hit <= 0)
                {
                    Enemy best = null; float bd = 56f; foreach (var e in Enemies) if (e.Hp > 0 && e.Age > 0) { float d = e.Pos.DistanceTo(PPos); if (d < bd) { bd = d; best = e; } }
                    if (best != null) { best.Hp -= PlayerDamage * dmgMul; hit = killInt; if (best.Hp <= 0) { kills++; segKills++; } }
                    else { Enemy near = null; float nd = 150f; foreach (var e in Enemies) if (e.Hp > 0) { float d = e.Pos.DistanceTo(PPos); if (d < nd) { nd = d; near = e; } } hit = 0; if (near != null) PPos += (near.Pos - PPos).Normalized() * 3f; }
                }
                foreach (var e in Enemies.ToArray()) if (e.Hp <= 0) Enemies.Remove(e);
                if (Hp < lastHp) { total += lastHp - Hp; segTaken += lastHp - Hp; } lastHp = Hp;
                if (Hp <= 0) { dead = true; Log($"PRESSURE DEAD at t={t:F0}s"); }
                seg += .033f; if (seg >= 30) { Log($"PRESSURE 30s: taken={segTaken:F0} kills={segKills} alive={Enemies.Count} hp={Hp:F0}/{MaxHp:F0}"); seg = 0; segTaken = 0; segKills = 0; }
            }
            Log($"PRESSURE END dead={dead} taken={total:F0} kills={kills} peakAlive={peak}");
        });
    }
}
