using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Lamplighter.Balance;

public sealed class Extras
{
    public List<SkillRow> SkillRows; public List<WeaponRow> WeaponRows; public List<(string mod, double gainPct, double costSec)> ModRows;
    public Dictionary<string, double> Proposal, ProposalBase; public string Notes; public List<BossRow> BossRows; public Summary Probes;
}

public static class Report
{
    static readonly CultureInfo C = CultureInfo.InvariantCulture;
    static readonly JsonSerializerOptions Js = new() { IncludeFields = true, WriteIndented = true, NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals };

    public static string F(double v, string f = "F2") => double.IsNaN(v) || double.IsInfinity(v) ? "–" : v.ToString(f, C);
    static string Pc(double v) => double.IsNaN(v) ? "–" : (v * 100).ToString("F0", C) + "%";
    static string Ci(double lo, double hi) => $"{Pc(lo)}–{Pc(hi)}";

    public static void WriteAll(string dir, string name, BalanceModel m, Runner.Batch b, Summary s, Targets tg, Extras ex = null, bool writeRuns = true)
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, name + ".summary.json"), JsonSerializer.Serialize(s, Js));
        if (ex != null)
        {
            if (ex.SkillRows != null) File.WriteAllText(Path.Combine(dir, name + ".skills.json"), JsonSerializer.Serialize(ex.SkillRows, Js));
            if (ex.WeaponRows != null) File.WriteAllText(Path.Combine(dir, name + ".weapons.json"), JsonSerializer.Serialize(ex.WeaponRows, Js));
            if (ex.Proposal != null) File.WriteAllText(Path.Combine(dir, name + ".overrides.json"), JsonSerializer.Serialize(ex.Proposal, Js));
        }
        File.WriteAllText(Path.Combine(dir, name + ".model.json"), m.ToJson(true));
        if (writeRuns) WriteRunsCsv(Path.Combine(dir, name + ".runs.csv"), b, m);
        WriteSummaryCsv(Path.Combine(dir, name + ".summary.csv"), s);
        if (ex?.SkillRows != null) WriteSkillsCsv(Path.Combine(dir, name + ".skills.csv"), ex.SkillRows);
        File.WriteAllText(Path.Combine(dir, name + ".md"), Markdown(name, m, b, s, tg, ex));
    }

    static void WriteRunsCsv(string path, Runner.Batch b, BalanceModel m)
    {
        var sb = new StringBuilder("profile,arch,seed,outcome,hours,deaths,level,kills,tier,beacon,weapon,stuck_reason,deaths_r0,deaths_r1,deaths_r2,deaths_r3,deaths_r4,deaths_b1,deaths_b2,deaths_b3,deaths_b4,deaths_b5,t_fight_h,t_gather_h,t_travel_h,t_rest_h,t_craft_h,t_explore_h");
        foreach (var n in Ms.Names) sb.Append(",ms_" + n);
        sb.AppendLine();
        foreach (var r in b.Runs)
        {
            sb.Append($"{r.Profile},{r.Arch},{r.Seed},{r.Out},{F(r.Hours, "F3")},{r.Deaths},{r.Level},{r.Kills},{r.Tier},{r.Beacon},{r.Weapon},\"{r.StuckReason}\"");
            foreach (var d in r.DeathsRegion) sb.Append("," + d); foreach (var d in r.DeathsBoss) sb.Append("," + d);
            foreach (var t in new[] { r.TFight, r.TGather, r.TTravel, r.TRest, r.TCraft, r.TExplore }) sb.Append("," + F(t / 3600.0, "F3"));
            foreach (var x in r.Ms) sb.Append("," + (x < 0 ? "" : F(x, "F3")));
            sb.AppendLine();
        }
        File.WriteAllText(path, sb.ToString());
    }

    static void WriteSummaryCsv(string path, Summary s)
    {
        var sb = new StringBuilder("profile,n,win_rate,win_lo,win_hi,stuck_rate,timeout_rate,hours_mean,hours_median,hours_ci95,hours_p10,hours_p90,hours_sd,deaths_mean,deaths_median,deaths_sd,deaths_per_hour,level_median\n");
        foreach (var p in s.Profiles) sb.AppendLine(string.Join(",", p.Profile, p.N, F(p.WinRate, "F4"), F(p.WinLo, "F4"), F(p.WinHi, "F4"), F(p.StuckRate, "F4"), F(p.TimeoutRate, "F4"), F(p.HoursMean), F(p.HoursMedian), F(p.HoursCi), F(p.HoursP10), F(p.HoursP90), F(p.HoursSd), F(p.DeathsMean), F(p.DeathsMedian), F(p.DeathsSd), F(p.DeathsPerHour), F(p.LevelMedian)));
        File.WriteAllText(path, sb.ToString());
    }

    static void WriteSkillsCsv(string path, List<SkillRow> rows)
    {
        var sb = new StringBuilder("skill,level,max,category,cost_sec,gain_pct,gain_se,gain_per_100s,significant,note\n");
        foreach (var r in rows) sb.AppendLine($"{r.Skill},{r.Level},{r.MaxLevel},{r.Category},{F(r.CostSec, "F1")},{F(r.GainPct)},{F(r.GainSe)},{F(r.GainPer100s)},{r.Significant},\"{r.Note}\"");
        File.WriteAllText(path, sb.ToString());
    }

    // ───────────────────────── Markdown ─────────────────────────
    public static string Markdown(string name, BalanceModel m, Runner.Batch b, Summary s, Targets tg, Extras ex)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Отчёт о балансе: {name}\n");
        sb.AppendLine($"* Прогонов всего: **{s.TotalRuns}** ({s.PerProfile} на профиль), сид **{s.Seed}**, потоков {s.Threads}, время расчёта {F(s.Seconds, "F1")} с ({F(s.TotalRuns / Math.Max(.001, s.Seconds), "F0")} прогонов/с).");
        sb.AppendLine($"* Доверительные интервалы — 95% (Уилсон для долей, нормальное приближение для среднего, порядковые статистики для медианы). Профили с числом прогонов < {tg.MinSims} помечены как предварительные.");
        sb.AppendLine("* Это модель встреч и экономики, а не запуск настоящей игры: ограничения и допущения — в конце отчёта.\n");
        sb.AppendLine("## Профили игроков\n");
        sb.AppendLine("| Профиль | N | Победы (95% ДИ) | Застряли | Не уложились | Время победы, ч: медиана (ДИ) | среднее ± SD | p10–p90 | Смертей: ср./медиана | Смертей/ч | Уровень (мед.) |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (var p in s.Profiles)
            sb.AppendLine($"| {p.Profile}{(p.LowConfidence ? " ⚠" : "")} | {p.N} | {Pc(p.WinRate)} ({Ci(p.WinLo, p.WinHi)}) | {Pc(p.StuckRate)} | {Pc(p.TimeoutRate)} | {F(p.HoursMedian, "F1")} ({F(p.HoursMedLo, "F1")}–{F(p.HoursMedHi, "F1")}) | {F(p.HoursMean, "F1")} ± {F(p.HoursSd, "F1")} | {F(p.HoursP10, "F1")}–{F(p.HoursP90, "F1")} | {F(p.DeathsMean, "F1")} / {F(p.DeathsMedian, "F0")} | {F(p.DeathsPerHour)} | {F(p.LevelMedian, "F0")} |");
        sb.AppendLine("\n## Ключевые этапы (медианное время, ч; в скобках доля дошедших)\n");
        sb.Append("| Этап |"); foreach (var p in s.Profiles) sb.Append($" {p.Profile} |"); sb.AppendLine(); sb.Append("|---|"); foreach (var _ in s.Profiles) sb.Append("---|"); sb.AppendLine();
        for (int i = 0; i < Ms.Names.Length; i++) { sb.Append($"| {Ms.Names[i]} |"); foreach (var p in s.Profiles) sb.Append($" {F(p.MilestoneMedian[i], "F2")} ({Pc((double)p.MilestoneN[i] / p.N)}, ур. {F(p.LevelAtMs[i], "F0")}) |"); sb.AppendLine(); }
        sb.AppendLine("\n## Сложность по ходу прохождения\n");
        sb.AppendLine("Среднее число смертей на этапе (регион — в обычных вылазках, босс — в бою со стражем) и попыток на босса.\n");
        sb.AppendLine("| Профиль | Регион 0–4: смерти | Босс 1–5: смерти | Босс 1–5: попыток | Босс 1–5: победа с 1-й попытки |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var p in s.Profiles) sb.AppendLine($"| {p.Profile} | {string.Join(" / ", p.DeathsRegion.Select(x => F(x, "F2")))} | {string.Join(" / ", p.DeathsBoss.Select(x => F(x, "F2")))} | {string.Join(" / ", p.BossAttempts.Select(x => F(x, "F1")))} | {string.Join(" / ", p.BossFirstTry.Select(Pc))} |");
        sb.AppendLine("\nУровень героя по часам (медиана): ");
        sb.AppendLine("| Профиль | " + string.Join(" | ", Enumerable.Range(0, 12).Select(h => $"{h}ч")) + " |"); sb.AppendLine("|---|" + string.Concat(Enumerable.Repeat("---|", 12)));
        foreach (var p in s.Profiles) sb.AppendLine($"| {p.Profile} | " + string.Join(" | ", p.LevelAtHour.Select(x => F(x, "F0"))) + " |");
        sb.AppendLine("\n## Куда уходит время (часы на прохождение, среднее)\n");
        sb.AppendLine("| Профиль | Бой/охота | Добыча | Дорога | Отдых | Крафт | Исследование |"); sb.AppendLine("|---|---|---|---|---|---|---|");
        foreach (var p in s.Profiles) sb.AppendLine($"| {p.Profile} | {F(p.TFight)} | {F(p.TGather)} | {F(p.TTravel)} | {F(p.TRest)} | {F(p.TCraft)} | {F(p.TExplore)} |");
        sb.AppendLine("\n## Сборки (профили с выбором сборки)\n");
        sb.AppendLine("| Профиль | Сборка | N | Победы (ДИ) | Медиана, ч | Смертей |"); sb.AppendLine("|---|---|---|---|---|---|");
        foreach (var a in s.Archs.OrderBy(a => Array.IndexOf(Profile.All, a.Profile)).ThenByDescending(a => a.WinRate)) sb.AppendLine($"| {a.Profile} | {a.Arch} | {a.N}{(a.N < tg.MinPerArch ? " ⚠" : "")} | {Pc(a.WinRate)} ({Ci(a.WinLo, a.WinHi)}) | {F(a.HoursMedian, "F1")} | {F(a.DeathsMean, "F1")} |");
        sb.AppendLine("\n## Итоговое оружие\n");
        sb.AppendLine("| Оружие | Доля прогонов | Победы | " + string.Join(" | ", Profile.All) + " |"); sb.AppendLine("|---|---|---|" + string.Concat(Enumerable.Repeat("---|", Profile.All.Length)));
        foreach (var w in s.Weapons) sb.AppendLine($"| {w.Weapon} | {Pc((double)w.N / s.TotalRuns)} | {Pc(w.WinRate)} | " + string.Join(" | ", Profile.All.Select(pn => Pc(w.Share.GetValueOrDefault(pn)))) + " |");
        sb.AppendLine("\n## Враги (кто убивает)\n");
        sb.AppendLine("| Враг | Появлений | Смертей героя | Смертей на 1000 появлений |"); sb.AppendLine("|---|---|---|---|");
        foreach (var e in s.Enemies.OrderByDescending(e => e.DeathsPerThousand)) sb.AppendLine($"| {e.Enemy} | {F(e.Spawned, "F0")} | {e.Deaths} | {F(e.DeathsPerThousand)} |");
        sb.AppendLine("\n## Ресурсы (прохождения с победой)\n");
        sb.AppendLine("| Ресурс | Добыто (ср.) | Осталось в конце | Доля неизрасходованного |"); sb.AppendLine("|---|---|---|---|");
        foreach (var i in s.Items.Where(i => i.Gathered > 0 || i.EndInv > 0)) sb.AppendLine($"| {i.Item} | {F(i.Gathered, "F0")} | {F(i.EndInv, "F0")} | {Pc(i.PercentLeft)} |");
        sb.AppendLine("\n_Осколки, ядра и реликвии не считаются добытыми «в добыче»: они приходят с врагов и боссов. Деревня, торговля и квесты не симулируются._\n");
        if (ex?.SkillRows != null) SkillSection(sb, ex.SkillRows, tg);
        if (ex?.WeaponRows != null) WeaponSection(sb, ex, tg);
        if (ex?.BossRows != null) BossSection(sb, ex.BossRows);
        if (ex?.Probes != null) ProbeSection(sb, s, ex.Probes);
        sb.AppendLine("\n## Найденные проблемы\n");
        if (s.Findings.Count == 0) sb.AppendLine("Проблем по заданным целям не обнаружено (или данных недостаточно для вывода)."); else
        {
            sb.AppendLine("| Серьёзность | Область | Что найдено | Предлагаемое изменение |"); sb.AppendLine("|---|---|---|---|");
            foreach (var f in s.Findings.OrderBy(f => f.Severity == "high" ? 0 : f.Severity == "warn" ? 1 : 2)) sb.AppendLine($"| {f.Severity} | {f.Area} | {f.Text} | {f.Fix} |");
        }
        if (ex?.Proposal != null && ex.Proposal.Count > 0)
        {
            sb.AppendLine("\n## Предлагаемые изменения параметров\n"); sb.AppendLine("Файл `" + name + ".overrides.json` — оригинальные ресурсы игры не менялись.\n");
            sb.AppendLine("| Параметр | Было | Станет |"); sb.AppendLine("|---|---|---|");
            foreach (var kv in ex.Proposal) sb.AppendLine($"| `{kv.Key}` | {F(ex.ProposalBase?.GetValueOrDefault(kv.Key, double.NaN) ?? double.NaN, "G4")} | {F(kv.Value, "G4")} |");
        }
        Limitations(sb, m);
        return sb.ToString();
    }

    static void SkillSection(StringBuilder sb, List<SkillRow> rows, Targets tg)
    {
        sb.AppendLine("\n## Эффективность навыков по уровням\n");
        sb.AppendLine("Метод: контрольные схватки с тремя врагами региона, один и тот же набор сидов для соседних уровней. «Прирост» — изменение оценки `1/(урон на убийство + 0,3·время на убийство)`; «цена» — в секундах труда при текущей добыче. Эффективность = прирост на 100 с цены. `±` — стандартная ошибка прироста; уровень считается значимым при |прирост| > 2·SE.\n");
        var sig = rows.Where(r => r.Category == "combat" && r.Significant && r.GainPct > 0 && r.CostSec > 0).ToList();
        double medUnlock = Mx.Median(sig.Where(r => r.Level == 1).Select(r => r.GainPer100s).DefaultIfEmpty(1).ToList()), medUp = Mx.Median(sig.Where(r => r.Level > 1).Select(r => r.GainPer100s).DefaultIfEmpty(1).ToList());
        sb.AppendLine($"Опорные значения (медиана значимых боевых уровней, % на 100 с): первый уровень навыка (открытие) — **{F(medUnlock)}**, улучшения — **{F(medUp)}**. Сравнение идёт внутри своей группы: «слишком силён» — более {tg.SkillEffRatioMax}× медианы группы, «слаб» — менее {tg.SkillEffRatioMin}×.\n");
        sb.AppendLine("| Навык | Ур. | Категория | Цена, с | Прирост, % (±SE) | % на 100 с | Оценка |"); sb.AppendLine("|---|---|---|---|---|---|---|");
        foreach (var r in rows)
        {
            double med = r.Level == 1 ? medUnlock : medUp;
            string verdict = r.Category switch
            {
                "combat" => !r.Significant ? (2 * r.GainSe < tg.SkillMinGainPct * 2 ? "бесполезный уровень (подтверждено)" : "эффект не выявлен: мало данных") : r.GainPct < tg.SkillMinGainPct ? "бесполезный уровень" : (r.GainPer100s > med * tg.SkillEffRatioMax ? "слишком силён" : (r.GainPer100s < med * tg.SkillEffRatioMin ? "слаб" : "норма")),
                "not-simulated" => "не симулируется", "combat-partial" => "частично (см. примечание)", "safety" => "через допущение о свете", _ => "через допущение"
            };
            sb.AppendLine($"| {r.Skill} | {r.Level}/{r.MaxLevel} | {r.Category} | {F(r.CostSec, "F0")} | {F(r.GainPct)} (±{F(r.GainSe)}) | {F(r.GainPer100s)} | {verdict} |");
        }
        var notes = rows.Where(r => !string.IsNullOrEmpty(r.Note)).Select(r => $"* **{r.Skill}**: {r.Note}").Distinct();
        sb.AppendLine("\n" + string.Join("\n", notes));
    }

    static void BossSection(StringBuilder sb, List<BossRow> rows)
    {
        sb.AppendLine("\n## Боссы: шанс победы в дуэли по уровню героя\n");
        sb.AppendLine("Герой без особых навыков (рывок 3, жатва 3), оружие тира региона, полное здоровье, `expert`-поведение; в каждой ячейке — доля побед из N дуэлей и среднее время боя. Показывает, на каком уровне босс перестаёт быть угрозой, независимо от того, когда его решит атаковать игрок.\n");
        sb.AppendLine("| Босс | Уровень героя → шанс победы (среднее время боя) |"); sb.AppendLine("|---|---|");
        foreach (var g in rows.GroupBy(r => r.Boss)) sb.AppendLine($"| {g.Key} | " + string.Join(" · ", g.OrderBy(r => r.Level).Select(r => $"**{r.Level}**: {Pc(r.Win)} ({F(r.Seconds, "F0")} с)")) + " |");
    }

    static void ProbeSection(StringBuilder sb, Summary main, Summary pr)
    {
        sb.AppendLine("\n## Поиск эксплойтов: стратегии-пробы\n");
        sb.AppendLine("Те же правила игры, но осознанно «ломающее» поведение: без фонарей (DangerScale растёт на 0,18 за фонарь), все фонари, «нахрапом» без подготовки и отдыха (смерть стоит только до 25 осколков и дороги). Сравнение с профилем `optimizer`.\n");
        var opt = main.Profiles.FirstOrDefault(p => p.Profile == "optimizer");
        sb.AppendLine("| Стратегия | N | Победы (ДИ) | Медиана, ч (ДИ) | Смертей | Время к оптимизатору |"); sb.AppendLine("|---|---|---|---|---|---|");
        if (opt != null) sb.AppendLine($"| optimizer (база) | {opt.N} | {Pc(opt.WinRate)} ({Ci(opt.WinLo, opt.WinHi)}) | {F(opt.HoursMedian, "F2")} ({F(opt.HoursMedLo, "F2")}–{F(opt.HoursMedHi, "F2")}) | {F(opt.DeathsMean, "F1")} | 1.00× |");
        foreach (var p in pr.Profiles) sb.AppendLine($"| {p.Profile} | {p.N} | {Pc(p.WinRate)} ({Ci(p.WinLo, p.WinHi)}) | {F(p.HoursMedian, "F2")} ({F(p.HoursMedLo, "F2")}–{F(p.HoursMedHi, "F2")}) | {F(p.DeathsMean, "F1")} | {(opt != null ? F(p.HoursMedian / opt.HoursMedian) + "×" : "–")} |");
    }

    static void WeaponSection(StringBuilder sb, Extras ex, Targets tg)
    {
        sb.AppendLine("\n## Баланс оружия и вставок\n");
        sb.AppendLine("| Оружие | Класс | Тир | Оценка DPS | Урон на убийство | Время на убийство, с | Относительно медианы тира | Примечание |"); sb.AppendLine("|---|---|---|---|---|---|---|---|");
        foreach (var w in ex.WeaponRows) sb.AppendLine($"| {w.Weapon} | {w.Class} | {w.Tier} | {F(w.Dps, "F1")} | {F(w.DmgPerKill)} | {F(w.TimePerKill)} | {F(w.RelToTier)} | {w.Note}{(w.RelToTier > 1.35 ? " **сильнее нормы**" : w.RelToTier < .7 ? " **слабее нормы**" : "")} |");
        if (ex.ModRows != null)
        {
            sb.AppendLine("\n| Вставка | Прирост оценки на Серебряном клинке, % | Цена, с |"); sb.AppendLine("|---|---|---|");
            foreach (var (mod, g, c) in ex.ModRows) sb.AppendLine($"| {mod} | {F(g)} | {F(c, "F0")} |");
        }
    }

    static void Limitations(StringBuilder sb, BalanceModel m)
    {
        sb.AppendLine("\n## Ограничения и допущения симулятора\n");
        sb.AppendLine("**Воспроизведено по коду игры (числа читаются из игры или сверяются командой `validate`):** формулы опыта и уровней, здоровье героя, урон оружия, свет героя, настройки боя из `tuning.tres`, масштаб врагов по биому и числу зажжённых фонарей, здоровье врагов и боссов, эффекты оружия и вставок (ожог, замедление, заморозка, цепь, залп, пробой, взрыв, дуга, сифон), реликвии и синергии, дерево навыков (уровни, цены, родители, условия открытия), цены инструментов и оружия, добыча (время добычи клетки, выход, `GatherMul`, цепная добыча), награды за убийства.\n");
        sb.AppendLine("**Упрощено (модель, а не копия):**");
        sb.AppendLine("* Бой — покадровая модель (шаг 0,05–0,1 с) без физики и карты: враг сближается за время расстояние/скорость, затем идёт цикл «пауза → предупреждение → удар» с таймерами из кода. Уклонение зависит от времени реакции игрока и длительности предупреждения (разные для профилей). Скользящие/кружащие манёвры врагов, стены, подбор позиции отражены только долей `Evasive` и `Uptime`.");
        sb.AppendLine("* Мир не генерируется: вместо поиска жил — время на клетку `TravelPerTile` и доля добычи вне света; доступность ресурсов по регионам взята из таблиц добычи и открывается ядрами босса.");
        sb.AppendLine("* Боссы — набор ударов с уроном/предупреждением/перезарядкой, переписанный по коду, но без полной геометрии (вихрь, водоворот, перегрев, метель, эхо). Щит Хранителя Зимы и Источника — цикл «головоломка → уязвимость» с временем головоломки по профилю.");
        sb.AppendLine("* Исследование мест, чертежи (фрагменты) и реликвии: число мест в минуту и шансы находок — допущения (`A.PoiPerMinute`, `PoiFragChance`, `PoiRelicChance`); настоящие таблицы наград мест не воспроизводятся.");
        sb.AppendLine("* **Не симулируются:** деревня и жители (производство, шестерни, линзы), торговля и монеты, квесты и события, головоломки фонарей (лишь время `LampTravel` и стражи), фоновый жар/холод/дым, водные зоны, эффекты Эндгейма, кооператив, фонарь героя. Навыки производства деревни (`Collector`, `SilverGears`, `ForgeHeat`, `AutoDrill`) помечены «не симулируется» и **не** считаются бесполезными.");
        sb.AppendLine("* Боссов 5 и конец игры: победа = убийство Источника Тьмы. Финал и послеигровой режим не проходят.");
        sb.AppendLine("\n**Допущения о мире (настраиваемые, `BalanceModel.A`):**\n");
        sb.AppendLine("| Параметр | Значение |"); sb.AppendLine("|---|---|");
        foreach (var f in typeof(AbstractM).GetFields()) sb.AppendLine($"| {f.Name} | {F(Convert.ToDouble(f.GetValue(m.A)), "G4")} |");
        sb.AppendLine("\nВсе абсолютные времена прохождения зависят от этих допущений; **относительные сравнения** (до/после, профили, сборки, навыки) надёжнее абсолютных. Результаты стоит откалибровать по реальному игровому телеметрическому прогону (`LAMP_DIAG`).");
    }

    // ───────────────────────── сравнение ─────────────────────────
    public static string CompareMd(string titleA, Summary a, string titleB, Summary b, Targets tg, Dictionary<string, double> diff)
    {
        var sb = new StringBuilder(); sb.AppendLine($"# Сравнение: {titleA} → {titleB}\n");
        if (diff != null && diff.Count > 0) { sb.AppendLine("## Изменённые параметры\n"); sb.AppendLine("| Параметр | Значение |"); sb.AppendLine("|---|---|"); foreach (var kv in diff) sb.AppendLine($"| `{kv.Key}` | {F(kv.Value, "G4")} |"); sb.AppendLine(); }
        sb.AppendLine("## Профили\n");
        sb.AppendLine("| Профиль | Победы до | Победы после | Δ | Медиана ч до | после | Δ | Смертей/ч до | после |"); sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
        foreach (var pa in a.Profiles)
        {
            var pb = b.Profiles.FirstOrDefault(x => x.Profile == pa.Profile); if (pb == null) continue;
            bool sig = pa.WinHi < pb.WinLo || pb.WinHi < pa.WinLo;
            sb.AppendLine($"| {pa.Profile} | {Pc(pa.WinRate)} ({Ci(pa.WinLo, pa.WinHi)}) | {Pc(pb.WinRate)} ({Ci(pb.WinLo, pb.WinHi)}) | {(pb.WinRate - pa.WinRate) * 100:+0.0;-0.0}{(sig ? " ✔значимо" : "")} | {F(pa.HoursMedian, "F1")} | {F(pb.HoursMedian, "F1")} | {F(pb.HoursMedian - pa.HoursMedian, "+0.0;-0.0")} | {F(pa.DeathsPerHour)} | {F(pb.DeathsPerHour)} |");
        }
        sb.AppendLine("\n## Этапы (медиана, ч)\n"); sb.AppendLine("| Этап | " + string.Join(" | ", a.Profiles.Select(p => p.Profile + " до → после")) + " |"); sb.AppendLine("|---|" + string.Concat(Enumerable.Repeat("---|", a.Profiles.Count)));
        for (int i = 0; i < Ms.Names.Length; i++) sb.AppendLine($"| {Ms.Names[i]} | " + string.Join(" | ", a.Profiles.Select(pa => { var pb = b.Profiles.FirstOrDefault(x => x.Profile == pa.Profile); return $"{F(pa.MilestoneMedian[i])} → {F(pb?.MilestoneMedian[i] ?? double.NaN)}"; })) + " |");
        sb.AppendLine("\n## Проблемы\n"); sb.AppendLine($"До: {a.Findings.Count(f => f.Severity != "info")} (из них серьёзных {a.Findings.Count(f => f.Severity == "high")}); после: {b.Findings.Count(f => f.Severity != "info")} (серьёзных {b.Findings.Count(f => f.Severity == "high")}).\n");
        sb.AppendLine("Осталось после: "); foreach (var f in b.Findings.Where(f => f.Severity != "info")) sb.AppendLine($"* [{f.Area}] {f.Text}");
        return sb.ToString();
    }
}
