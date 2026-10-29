using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Godot;

namespace Lamplighter.Balance;

/// <summary>Проверки модели без запуска мира: данные снимаются с настоящей игры, числа врагов сверяются с исходниками, прогоны воспроизводимы и не зависят от потоков, сохранения не трогаются.
/// Сверка формул с работающим Game — стадия LAMP_STAGE=balance (Game.TestBalance.cs).</summary>
public static class Validate
{
    static int fails;
    static void Check(bool ok, string what) { if (!ok) fails++; GD.Print($"[check] {(ok ? "PASS" : "FAIL")} {what}"); }

    static string Root => ProjectSettings.GlobalizePath("res://").TrimEnd('/', '\\');
    static string Src(string rel) => File.ReadAllText(Path.Combine(Root, "Scripts", rel));

    /// <summary>(файл, фрагмент) — числа, которые симулятор перенёс в таблицы. Если кто-то поменял их в игре, проверка падает и таблицы нужно обновить.</summary>
    static readonly (string file, string snippet, string what)[] Probes =
    {
        ("Combat/Game.Combat.cs", "HurtPlayer(9f * e.Scale", "Тень: урон 9"), ("Combat/Game.Combat.cs", "HurtPlayer(7f * e.Scale, -dir); }", "Призрак: урон 7"),
        ("Combat/Game.Combat.cs", "Dmg = 7f * e.Scale }", "Плевун: снаряд 7"), ("Combat/Game.Combat.cs", "HurtPlayer(3.5f * e.Scale", "Рой: урон 3,5"),
        ("Combat/Game.Combat.cs", "HurtPlayer(8f * e.Scale", "Угорь: урон 8"), ("Combat/Game.Combat.cs", "HurtPlayer(2.5f * e.Scale", "Светоед: 2,5"),
        ("Combat/Game.Combat.cs", "HurtPlayer(14f * e.Scale, -dir); e.State = 3", "Жук: удар 14"), ("Combat/Game.Combat.cs", "HurtPlayer(11f * e.Scale", "Жук: удар 11"),
        ("Combat/Game.Combat.cs", "HurtPlayer(18f * e.Scale, (PPos - e.Pos)", "Подрывник: взрыв 18"), ("Combat/Game.Combat.cs", "e.Tele = .35f; e.Cd = rng.RandfRange(2.2f, 3.2f)", "Призрак: подготовка и перезарядка"),
        ("Combat/Game.Combat.cs", "e.Tele = .45f; e.Cd = rng.RandfRange(1.6f, 2.4f)", "Угорь: подготовка"), ("Combat/Game.Combat.cs", "spawnT = rng.RandfRange(2.2f, 3.8f)", "темп появления врагов"),
        ("Combat/Game.Combat.cs", "3 + LitCount * 2 : 4 + LitIn(reg) * 2", "запас врагов на территории"), ("Combat/Game.Combat.cs", "xp *= 1f + .3f * RegionAt(e.Pos)", "опыт за убийство растёт с регионом"),
        ("Combat/Game.Combat.cs", "EK.Shade => 15", "опыт тени"), ("Combat/Game.Combat.cs", "EK.Shade => .7f", "шанс осколка с тени"), ("Combat/Game.Combat.cs", "EK.Golem => 40", "опыт голема"),
        ("Combat/Game.Combat.cs", "float auraR = PlayerLightR * (Has(Tech.HeroLamp) ? .55f : .6f)", "радиус ауры"), ("Combat/Game.Combat.cs", "e.Hp -= 3f * dt * (e.IsBoss ? .3f : 1f)", "ожог 3 урона в секунду"),
        ("Combat/Game.Combat.cs", "Inv[It.Shard] -= lost", "потеря осколков при смерти"), ("Combat/Game.Combat.cs", "Math.Min(25, Inv[It.Shard] / 4)", "потеря осколков ≤25"),
        ("Combat/Game.Ash.cs", "HurtPlayer(7f * e.Scale, -dir, true)", "Бес: урон 7"), ("Combat/Game.Ash.cs", "HurtPlayer(15f * e.Scale", "Голем: удар 15"),
        ("Combat/Game.Ice.cs", "HurtPlayer(8f * e.Scale, -dir); Chill(2f)", "Волк: урон 8"), ("Combat/Game.Ice.cs", "HurtPlayer(14f * e.Scale, -dir); Chill(2.5f)", "Страж: удар 14"),
        ("Combat/Game.Heart.cs", "HurtPlayer(10f * e.Scale, -dir)", "Охотник: удар 10"), ("Combat/Game.Heart.cs", "HurtPlayer(6f * e.Scale, -dir)", "Ткач: удар 6"),
        ("Combat/Game.Weapons.cs", "WeaponDmg(WeaponDef w) => (w.Dmg + .35f * (Level - 1)) * (1f + N(\"Dmg\"))", "урон оружия"),
        ("Combat/Game.Weapons.cs", "AtkCd = w.Cd * (1f - E(Tech.QuickDraw))", "перезарядка лука"), ("Combat/Game.Weapons.cs", "e.Burn = Mathf.Max(e.Burn, 2f + b)", "длительность ожога"),
        ("Combat/Game.Weapons.cs", ".45f + .2f * b", "шанс ожога"), ("Combat/Game.Weapons.cs", "dmg * .45f", "урон цепи 45%"),
        ("Combat/Game.Difficulty.cs", "t.EnemyHp * CombatTuning.At(t.BiomeHp, reg)", "множитель здоровья врагов"), ("Combat/Game.Difficulty.cs", "Give(It.Shard, 3, e.Pos", "награда за элитного"),
        ("Core/Game.cs", "MaxHp += 10", "здоровье за уровень"), ("Core/Game.cs", "PlayerDamage => (2f + 2f * Tier + .4f * (Level - 1))", "урон без оружия"),
        ("Core/Game.cs", "MineSpeed => (1f + .7f * Tier) * (1f + .06f * (Level - 1))", "скорость добычи"), ("Core/Game.cs", "XpBase + Tune.T.XpLinear * Level + Tune.T.XpQuad * Level * Level", "опыт на уровень"),
        ("Core/Game.cs", "SmeltTime = 0.9f", "время плавки"), ("Core/Game.cs", "Tl.Tree => 2.2f", "время добычи дерева"), ("Core/Game.cs", "Tl.Iron => 1, Tl.MeteorOre => 1, Tl.FrostOre => 3", "требования тира"),
        ("Core/Game.cs", "regenDelay <= 0) Hp = Mathf.Min(MaxHp, Hp + RegenPerSec * dt)", "восстановление здоровья"), ("Core/Game.cs", "Hp + 4f * dt", "восстановление в свете 4/с"),
        ("Combat/Game.Combat.cs", "DangerScale => 1f + .18f * LitCount + .12f * LitIn(1)", "масштаб опасности"),
    };

    static readonly string[] NumKeysUsed = { "Dmg", "MineSpd", "HeroRadius", "KillHeal", "HeatResist", "MineChain" };

    public static int Run(int n, int threads)
    {
        fails = 0; var m = BalanceModel.FromGame(); var tg = new Targets();
        GD.Print($"[validate] модель: навыков {m.Skills.Length}, оружия {m.Weapons.Length}, вставок {m.Mods.Length}, врагов {m.Enemies.Length}, реликвий {m.Relics.Length}, синергий {m.Syns.Length}");
        // 1. соответствие данным игры
        Check(m.Skills.Length == Game.Techs.Length && m.Skills.Length == Enum.GetValues<Tech>().Length, "навыков в модели столько же, сколько в игре");
        Check(m.Skills.All(s => s.Max >= 1 && s.Curve.Length >= 1 && Enum.TryParse<Tech>(s.Id, out _)), "каждый навык модели — реальный Tech с уровнями и кривой цены");
        Check(m.Enemies.All(e => Enum.TryParse<EK>(e.Kind, out _)) && m.Bosses.All(b => Enum.TryParse<EK>(b.Kind, out _)), "каждый враг модели — реальный вид EK");
        Check(m.Enemies.All(e => e.Hp > 0) && m.Bosses.All(b => b.Hp > 100), "здоровье врагов считано из Game.BaseHp");
        Check(m.Weapons.All(w => Db.Weapon(w.Id) != null) && m.Weapons.Length == Db.Weapons.Length, "оружие совпадает с таблицей Db");
        Check(m.Mods.SelectMany(x => x.FxK).All(k => Enum.TryParse<Fxk>(k, out _)), "эффекты вставок — реальные Fxk");
        Check(m.Relics.SelectMany(r => r.Effects).All(e => { var k = e.Split(':')[0]; return k == "Flag" || Enum.TryParse<Fxk>(k, out _) || NumKeysUsed.Contains(k) || KnownNum(k); }), "эффекты реликвий известны игре");
        Check(m.T.EnemyHp > 0 && m.T.BiomeHp.Length == 5 && m.T.BossHp.Length == 5, "настройки боя считаны из tuning.tres");
        var t0 = Tune.T; Check(Math.Abs(m.T.XpBase - t0.XpBase) < 1e-6 && Math.Abs(m.T.EnemyDamage - t0.EnemyDamage) < 1e-6, "числа модели равны числам Resource");
        // 2. числа врагов в исходниках
        foreach (var (file, snip, what) in Probes) { bool ok; try { ok = Src(file).Contains(snip); } catch { ok = false; } Check(ok, $"исходник содержит числа таблицы: {what}"); }
        // 3. ключи Num, используемые симулятором, существуют в игре
        string all = string.Join("\n", Directory.GetFiles(Path.Combine(Root, "Scripts"), "*.cs", SearchOption.AllDirectories).Where(f => !f.Contains("Balance")).Select(File.ReadAllText));
        foreach (var k in NumKeysUsed) Check(all.Contains($"N(\"{k}\")"), $"ключ эффекта N(\"{k}\") есть в игре (симулятор не придумывает механик)");
        // 4. воспроизводимость и независимость от потоков
        var profs = new[] { "novice", "average", "expert", "optimizer" };
        var b1 = Runner.Simulate(m, profs, n, 12345, 1); var b2 = Runner.Simulate(m, profs, n, 12345, 1); var b3 = Runner.Simulate(m, profs, n, 12345, Math.Max(2, threads <= 0 ? System.Environment.ProcessorCount - 1 : threads));
        var b4 = Runner.Simulate(m, profs, n, 54321, 2);
        Check(Runner.Hash(b1.Runs) == Runner.Hash(b2.Runs), $"один сид → одинаковый результат ({n * 4} прогонов, хэш {Runner.Hash(b1.Runs):x})");
        Check(Runner.Hash(b1.Runs) == Runner.Hash(b3.Runs), $"результат не зависит от числа потоков (1 против {b3.Threads})");
        Check(Runner.Hash(b1.Runs) != Runner.Hash(b4.Runs), "другой сид → другие результаты");
        Check(b1.Runs.All(r => r.Seed != 0) && b1.Runs.Select(r => r.Seed).Distinct().Count() == b1.Runs.Count, "сиды прогонов уникальны");
        // 5. сохранения не затронуты
        string ud = OS.GetUserDataDir(); ulong h0 = DirHash(ud);
        Runner.Simulate(m, new[] { "expert" }, 20, 7, 2); Check(DirHash(ud) == h0, $"сохранения игры (user://) не изменились после симуляции ({ud})");
        string src = string.Join("\n", Directory.GetFiles(Path.Combine(Root, "Scripts", "Balance"), "*.cs").Where(f => !f.EndsWith("Validate.cs") && !f.EndsWith("BalanceCli.cs")).Select(File.ReadAllText));
        Check(!Regex.IsMatch(src, @"user://|SaveDir|SavePath|Settings\.|\.Save\(\)"), "код симулятора не обращается к сохранениям и настройкам игрока");
        Check(!Regex.IsMatch(src, @":\s*Node\b|new Node\b|AddChild\(|Instantiate\("), "симулятор не создаёт Node и сцены");
        // 6. разумность: все профили доходят до конца хотя бы иногда и порядок профилей осмысленный
        var s = Summary.Build(b1, m, tg);
        Check(s.Profiles.All(p => p.Wins > 0), "каждый профиль хотя бы иногда проходит игру (иначе симуляция сломана, а не игра сложна): " + string.Join(", ", s.Profiles.Select(p => $"{p.Profile} {p.WinRate:P0}")));
        Check(b1.Runs.All(r => r.Hours >= 0 && r.Hours <= m.A.MaxHours + 1 && r.Level >= 1 && r.Deaths >= 0), "значения прогонов в допустимых границах");
        // 7. чувствительность: более сильные враги → больше смертей (направление эффекта верное)
        var strong = m.With(new Dictionary<string, double> { ["tuning.EnemyDamage"] = m.T.EnemyDamage * 1.6, ["tuning.EnemyHp"] = m.T.EnemyHp * 1.4 });
        var sb = Runner.Simulate(strong, new[] { "average" }, Math.Max(40, n), 12345, 2); var sbase = Runner.Simulate(m, new[] { "average" }, Math.Max(40, n), 12345, 2);
        Check(sb.Runs.Average(r => r.Deaths) > sbase.Runs.Average(r => r.Deaths), $"усиление врагов повышает число смертей ({sbase.Runs.Average(r => r.Deaths):F1} → {sb.Runs.Average(r => r.Deaths):F1})");
        var cheap = m.With(new Dictionary<string, double> { ["tuning.XpBase"] = m.T.XpBase * .4, ["tuning.XpLinear"] = m.T.XpLinear * .4, ["tuning.XpQuad"] = m.T.XpQuad * .4 });
        var cb = Runner.Simulate(cheap, new[] { "average" }, Math.Max(40, n), 12345, 2);
        Check(cb.Runs.Average(r => r.Level) > sbase.Runs.Average(r => r.Level), $"дешёвый опыт повышает уровень ({sbase.Runs.Average(r => r.Level):F1} → {cb.Runs.Average(r => r.Level):F1})");
        GD.Print($"[validate] SUMMARY failures={fails}"); return fails == 0 ? 0 : 1;
    }

    static bool KnownNum(string k) => Directory.GetFiles(Path.Combine(Root, "Scripts"), "*.cs", SearchOption.AllDirectories).Any(f => !f.Contains("Balance") && File.ReadAllText(f).Contains($"N(\"{k}\")"));

    static ulong DirHash(string dir)
    {
        ulong h = 1469598103934665603UL; if (!Directory.Exists(dir)) return h;
        foreach (var f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Where(f => !f.EndsWith(".log")).OrderBy(x => x)) { var fi = new FileInfo(f); h = (h ^ (ulong)fi.Length) * 1099511628211UL; h = (h ^ (ulong)fi.LastWriteTimeUtc.Ticks) * 1099511628211UL; foreach (var c in f) h = (h ^ c) * 1099511628211UL; }
        return h;
    }
}
