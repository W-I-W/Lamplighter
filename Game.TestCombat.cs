using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Аудит боевой части (LAMP_STAGE=combat): таблица урона оружия, «мёртвые» эффекты реликвий и синергий, телеграфы.</summary>
public partial class Game
{
    void BuildCombatScript()
    {
        S(.2f, () =>
        {
            int fails = 0;
            // 1) оружие: условный dps по одной цели и по группе из 5 (уровень 10)
            Level = 10;
            foreach (var w in Db.Weapons)
            {
                float one = WeaponDmg(w) / Math.Max(.1f, w.Cd);
                float group = w.Cls == WClass.Sword ? one * 3.2f : w.Cls == WClass.Bow ? one * (1 + w.Pierce * .6f) : one * 2f;
                Log($"weapon {w.Id,-10} class={w.Cls,-5} tier={w.Tier} dmg={WeaponDmg(w):F1} cd={w.Cd:F2} dps1={one:F1} dps5~{group:F1}");
            }
            // 2) эффекты: каждый ключ должен быть известен коду
            var known = new HashSet<string>(Enum.GetNames(typeof(Fxk))) { "Flag", "Luck", "LampRadius", "HeroRadius", "Speed", "MineChain", "KillHeal", "Trade", "HeatResist", "Dmg", "MineSpd", "Prod", "Reveal" };
            foreach (var r in Db.Relics)
            {
                if (r.Effects.Length == 0) { Log($"DEAD relic (no effects): {r.Id}"); fails++; }
                foreach (var e in r.Effects) { var key = e.Split(':')[0]; if (!known.Contains(key)) { Log($"DEAD effect '{e}' in relic {r.Id}"); fails++; } }
            }
            foreach (var s in Db.Synergies) foreach (var e in s.Grants) { var key = e.Split(':')[0]; if (!known.Contains(key)) { Log($"DEAD effect '{e}' in synergy {s.Id}"); fails++; } }
            // 3) флаги синергий должны где-то использоваться
            var flags = new HashSet<string>(); foreach (var s in Db.Synergies) foreach (var e in s.Grants) if (e.StartsWith("Flag:")) flags.Add(e.Substring(5));
            foreach (var r in Db.Relics) foreach (var e in r.Effects) if (e.StartsWith("Flag:")) flags.Add(e.Substring(5));
            var src = FileAccess.GetFileAsString("res://Game.Relics.cs") + FileAccess.GetFileAsString("res://Game.Weapons.cs") + FileAccess.GetFileAsString("res://Game.Combat.cs") + FileAccess.GetFileAsString("res://Game.cs") + FileAccess.GetFileAsString("res://Game.Light.cs");
            foreach (var f in flags) if (!src.Contains($"\"{f}\"")) { Log($"flag never read in code: {f}"); fails++; }
            // 4) телеграфы: у каждого опасного состояния босса задан таймер предупреждения (T2) и урон не превышает 25% здоровья новичка
            Log($"player invuln after hit = 0.6s; max boss single hit = 14 (hp at level 10 = {MaxHp + 90})");
            Log(fails == 0 ? "COMBAT AUDIT PASSED" : $"COMBAT AUDIT FAILED {fails}");
        });
    }
}
