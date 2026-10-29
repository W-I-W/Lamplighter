using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using Godot;

namespace Lamplighter.Balance;

/// <summary>Командная строка: godot --headless --path . -- balance &lt;команда&gt; [--параметры]. Запуск не создаёт мира и не читает сохранений (Game._Ready возвращается до загрузки).</summary>
public static class BalanceCli
{
    static readonly JsonSerializerOptions Js = new() { IncludeFields = true, WriteIndented = true, NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals };

    public static bool Requested()
    {
        var a = OS.GetCmdlineUserArgs(); if (a.Length > 0 && a[0] == "balance") return true;
        return OS.GetEnvironment("LAMP_BALANCE") != "";
    }

    static string Root => ProjectSettings.GlobalizePath("res://").TrimEnd('/', '\\');
    static string OutRoot => Path.Combine(Root, "balance", "out");

    sealed class Args
    {
        public string Cmd = "help"; public Dictionary<string, string> Kv = new(); public HashSet<string> Flags = new();
        public static Args Parse(string[] a)
        {
            var r = new Args(); int i = 0; if (a.Length > 0 && a[0] == "balance") i = 1;
            if (i < a.Length && !a[i].StartsWith("--")) r.Cmd = a[i++];
            for (; i < a.Length; i++)
            {
                if (!a[i].StartsWith("--")) continue; string k = a[i].Substring(2);
                if (k.Contains('=')) { r.Kv[k.Substring(0, k.IndexOf('='))] = k.Substring(k.IndexOf('=') + 1); continue; }
                if (i + 1 < a.Length && !a[i + 1].StartsWith("--")) r.Kv[k] = a[++i]; else r.Flags.Add(k);
            }
            return r;
        }
        public string S(string k, string d = null) => Kv.TryGetValue(k, out var v) ? v : d;
        public int I(string k, int d) => Kv.TryGetValue(k, out var v) && int.TryParse(v, out var x) ? x : d;
        public double D(string k, double d) => Kv.TryGetValue(k, out var v) && double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x) ? x : d;
        public bool F(string k) => Flags.Contains(k);
    }

    static void Say(string s) { GD.Print("[balance] " + s); }

    public static void Run(Node root)
    {
        root.SetProcess(false); root.SetPhysicsProcess(false); root.SetProcessInput(false);
        int code = 0;
        try
        {
            var raw = OS.GetCmdlineUserArgs(); var env = OS.GetEnvironment("LAMP_BALANCE");
            if (raw.Length == 0 && env != "") raw = ("balance " + env).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var a = Args.Parse(raw);
            code = Dispatch(a);
        }
        catch (Exception e) { GD.PrintErr("[balance] ERROR: " + e); code = 2; }
        root.GetTree().Quit(code);
    }

    static int Dispatch(Args a)
    {
        switch (a.Cmd)
        {
            case "simulate": return Simulate(a, false);
            case "analyze": return Simulate(a, true);
            case "optimize": return Optimize(a);
            case "prune": return Optimize(a, true);
            case "compare": return Compare(a);
            case "report": return ReportCmd(a);
            case "validate": return Validate.Run(a.I("n", 120), a.I("threads", 0));
            case "targets": return WriteTargets(a);
            case "apply": return Apply(a);
            default: Help(); return a.Cmd == "help" ? 0 : 1;
        }
    }

    static void Help()
    {
        Say("Команды: simulate | analyze | optimize | prune | compare | report | validate | targets | apply");
        Say("Общие параметры: --n <прогонов на профиль> --profiles novice,average,expert,optimizer --seed <число> --threads <0=авто> --name <имя> --overrides <файл.json> --targets <файл.json> --max-hours <ч>");
        Say("simulate: массовый прогон, отчёт JSON/CSV/MD в balance/out/<name>.   analyze: simulate + эффективность навыков, оружие, вставки (--bench-k <схваток>).");
        Say("optimize: --iters <12> --lambda <6> --train <150> --valid <600> --groups tuning,boss,weapon,skill,enemy --skill-bench.  Результат — предложение balance/proposals/<name>.json, ресурсы игры не меняются.");
        Say("prune: --overrides <предложение> — убирает из предложения правки, не влияющие на результат, и проверяет остаток на независимых сидах.");
        Say("compare: --a <overrides|base> --b <overrides>.   report: --from <папка/имя>.   validate: проверки модели (+ стадия LAMP_STAGE=balance — сверка с настоящей игрой).");
        Say("apply: --overrides <файл> --confirm — записывает только параметры tuning.* в Resources/Combat/tuning.tres (с копией); остальное выводит списком.");
    }

    // ───────────────────────── общее ─────────────────────────
    static BalanceModel LoadModel(Args a, string overridesKey = "overrides")
    {
        string cal = Path.Combine(Root, "balance", "profiles.json"); if (File.Exists(cal)) Profile.Calibration = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, double>>>(File.ReadAllText(cal));
        var m = a.S("model") != null ? JsonSerializer.Deserialize<BalanceModel>(File.ReadAllText(a.S("model")), new JsonSerializerOptions { IncludeFields = true }) : BalanceModel.FromGame();
        if (a.D("max-hours", 0) > 0) m.A.MaxHours = (float)a.D("max-hours", 60);
        var ov = a.S(overridesKey); if (!string.IsNullOrEmpty(ov) && ov != "base") m = m.With(LoadOverrides(ov));
        return m;
    }

    static Dictionary<string, double> LoadOverrides(string path)
    {
        if (!File.Exists(path)) path = Path.Combine(Root, "balance", "proposals", path.EndsWith(".json") ? path : path + ".json");
        var d = JsonSerializer.Deserialize<Dictionary<string, double>>(File.ReadAllText(path), new JsonSerializerOptions { IncludeFields = true });
        return d;
    }

    static Targets LoadTargets(Args a)
    {
        string p = a.S("targets") ?? Path.Combine(Root, "balance", "targets.json");
        if (File.Exists(p)) return JsonSerializer.Deserialize<Targets>(File.ReadAllText(p), new JsonSerializerOptions { IncludeFields = true });
        return new Targets();
    }

    static int WriteTargets(Args a)
    {
        string p = a.S("targets") ?? Path.Combine(Root, "balance", "targets.json"); Directory.CreateDirectory(Path.GetDirectoryName(p));
        File.WriteAllText(p, JsonSerializer.Serialize(new Targets(), Js)); Say("целевые показатели (предложение, требуют согласования): " + p); return 0;
    }

    static string[] Profiles(Args a) => (a.S("profiles") ?? string.Join(",", Profile.All)).Split(',', StringSplitOptions.RemoveEmptyEntries);

    static CancellationToken StopToken()
    {
        var cts = new CancellationTokenSource(); string stop = Path.Combine(Root, "balance", "STOP"); try { if (File.Exists(stop)) File.Delete(stop); } catch { }
        var t = new Thread(() => { while (!cts.IsCancellationRequested) { Thread.Sleep(500); if (File.Exists(stop)) { cts.Cancel(); Say("получен сигнал остановки (balance/STOP)"); } } }) { IsBackground = true }; t.Start();
        return cts.Token;
    }

    static Dictionary<string, object> Brief(Summary s) => new()
    {
        ["total_runs"] = s.TotalRuns, ["seconds"] = Math.Round(s.Seconds, 2),
        ["profiles"] = s.Profiles.Select(p => new { p.Profile, p.N, win = Math.Round(p.WinRate, 4), win_lo = Math.Round(p.WinLo, 4), win_hi = Math.Round(p.WinHi, 4), median_hours = Math.Round(p.HoursMedian, 2), mean_deaths = Math.Round(p.DeathsMean, 2), stuck = Math.Round(p.StuckRate, 4) }).ToList(),
        ["findings"] = s.Findings.Where(f => f.Severity != "info").Select(f => f.Area + ": " + f.Text).ToList()
    };

    // ───────────────────────── simulate / analyze ─────────────────────────
    static int Simulate(Args a, bool analyze)
    {
        var m = LoadModel(a); var tg = LoadTargets(a); int n = a.I("n", 1000); ulong seed = (ulong)a.I("seed", 1); string name = a.S("name") ?? (analyze ? "analysis" : "simulation");
        var ct = StopToken(); int lastPct = -10;
        Say($"{(analyze ? "analyze" : "simulate")}: {n} прогонов × {Profiles(a).Length} профилей, сид {seed}");
        var b = Runner.Simulate(m, Profiles(a), n, seed, a.I("threads", 0), ct, (d, tot) => { int pc = d * 100 / tot; if (pc >= lastPct + 10) { lastPct = pc; Say($"  {pc}% ({d}/{tot})"); } });
        var s = Summary.Build(b, m, tg); Extras ex = null;
        if (analyze && !ct.IsCancellationRequested)
        {
            int k = a.I("bench-k", 1500); Say("стенд навыков…");
            ex = new Extras { SkillRows = Bench.Skills(m, k, seed, a.I("threads", 0)), WeaponRows = Bench.Weapons(m, k, seed), ModRows = Bench.Mods(m, k, seed), BossRows = Bench.Bosses(m, Math.Max(100, k / 3), seed) };
            Say("пробы эксплойтов…"); var pb = Runner.Simulate(m, Profile.Probes, Math.Max(100, n / 3), seed, a.I("threads", 0), ct); ex.Probes = Summary.Build(pb, m, tg);
            AddBenchFindings(s, ex, tg);
        }
        string dir = Path.Combine(a.S("out") ?? OutRoot, name); Report.WriteAll(dir, name, m, b, s, tg, ex);
        Say($"готово за {s.Seconds:F1} с{(b.Cancelled ? " (ОСТАНОВЛЕНО)" : "")}; отчёты: {dir}");
        Say("RESULT " + JsonSerializer.Serialize(Brief(s)));
        return b.Cancelled ? 3 : 0;
    }

    static void AddBenchFindings(Summary s, Extras ex, Targets tg)
    {
        var optS = s.Profiles.FirstOrDefault(p => p.Profile == "optimizer");
        if (optS != null && ex.Probes != null) foreach (var p in ex.Probes.Profiles)
            {
                if (p.Wins > 10 && optS.Wins > 10 && p.HoursMedHi < optS.HoursMedLo * 0.85) s.Findings.Add(new Finding { Severity = "high", Area = "exploit", Text = $"стратегия «{p.Profile}» проходит игру за {p.HoursMedian:F2} ч против {optS.HoursMedian:F2} ч у оптимизатора ({p.HoursMedian / optS.HoursMedian:P0}, доверительные интервалы не пересекаются)", Value = p.HoursMedian / optS.HoursMedian, Fix = p.Profile == "probe-nolamps" ? "фонари делают врагов сильнее (DangerScale +0,18) и их пропуск выгоден: ослабить рост DangerScale либо дать фонарям награду" : p.Profile == "probe-rush" ? "смерть почти бесплатна: ввести цену смерти (потеря ресурсов/времени)" : "" });
                if (p.Profile == "probe-rush" && p.WinRate > 0.5) s.Findings.Add(new Finding { Severity = "high", Area = "exploit", Text = $"«нахрапом» без подготовки и отдыха игру проходят в {p.WinRate:P0} случаев (смертей в среднем {p.DeathsMean:F0}): наказание за смерть ({T0(ex)}) не удерживает от стратегии «умирать до победы»", Value = p.WinRate, Fix = "ввести значимую цену смерти" });
            }
        if (ex.BossRows != null) foreach (var g in ex.BossRows.GroupBy(r => r.Boss)) { var ordered = g.OrderBy(r => r.Level).ToList(); var first = ordered.First(); if (first.Win > .9) s.Findings.Add(new Finding { Severity = "info", Area = "boss", Text = $"{g.Key}: даже на уровне {first.Level} (минимальный в сетке) герой побеждает в {first.Win:P0} дуэлей — босс не требует подготовки", Fix = $"поднять BossHp/BossDamage для босса {g.Key}" }); var last = ordered.Last(); if (last.Win < .5) s.Findings.Add(new Finding { Severity = "warn", Area = "boss", Text = $"{g.Key}: даже на уровне {last.Level} шанс победы лишь {last.Win:P0} — прогресс упирается в босса", Fix = $"снизить BossHp/BossDamage для босса {g.Key}" }); }
        var comb = ex.SkillRows.Where(r => r.Category == "combat" && r.CostSec > 0).ToList(); if (comb.Count == 0) return;
        var sigRows = comb.Where(r => r.Significant && r.GainPct > 0).ToList();
        double medU = Mx.Median(sigRows.Where(r => r.Level == 1).Select(r => r.GainPer100s).DefaultIfEmpty(1).ToList()), medP = Mx.Median(sigRows.Where(r => r.Level > 1).Select(r => r.GainPer100s).DefaultIfEmpty(1).ToList());
        foreach (var g in comb.GroupBy(r => r.Skill))
        {
            var useless = g.Where(r => (!r.Significant && 2 * r.GainSe < tg.SkillMinGainPct * 2) || (r.Significant && r.GainPct < tg.SkillMinGainPct)).ToList();
            if (useless.Count > 0) s.Findings.Add(new Finding { Severity = "warn", Area = "skills", Text = $"{g.Key}: уровни {string.Join(",", useless.Select(r => r.Level))} из {g.First().MaxLevel} не дают значимого прироста в бою (< {tg.SkillMinGainPct}%)", Fix = $"поднять skill.{g.Key}.EffPer или снизить цену" });
            var strong = g.Where(r => r.Significant && r.GainPer100s > (r.Level == 1 ? medU : medP) * tg.SkillEffRatioMax).ToList();
            if (strong.Count > 0) s.Findings.Add(new Finding { Severity = "warn", Area = "skills", Text = $"{g.Key}: уровни {string.Join(",", strong.Select(r => r.Level))} в {strong.Max(r => r.GainPer100s / (r.Level == 1 ? medU : medP)):F1}× эффективнее медианы своей группы", Fix = $"снизить skill.{g.Key}.EffPer или поднять цену" });
        }
        foreach (var w in ex.WeaponRows) { if (w.RelToTier > 1.35) s.Findings.Add(new Finding { Severity = "warn", Area = "weapons", Text = $"оружие {w.Weapon} на {(w.RelToTier - 1) * 100:F0}% сильнее медианы своего тира", Fix = $"weapon.{w.Weapon}.Dmg вниз" }); if (w.RelToTier < .7) s.Findings.Add(new Finding { Severity = "warn", Area = "weapons", Text = $"оружие {w.Weapon} на {(1 - w.RelToTier) * 100:F0}% слабее медианы своего тира", Fix = $"weapon.{w.Weapon}.Dmg вверх" }); }
        foreach (var (mod, g, c) in ex.ModRows) if (g < 0.5) s.Findings.Add(new Finding { Severity = "info", Area = "mods", Text = $"вставка «{mod}» почти не меняет результат схватки ({g:F1}%)", Fix = "" });
    }

    static string T0(Extras ex) => "потеря до 25 осколков и дорога";

    // ───────────────────────── optimize ─────────────────────────
    static int Optimize(Args a, bool prune = false)
    {
        var m = prune ? LoadModel(a, "none") : LoadModel(a); var tg = LoadTargets(a); string name = a.S("name") ?? "opt";
        var o = new OptimizeOptions { Iterations = a.I("iters", 12), Lambda = a.I("lambda", 6), Train = a.I("train", 150), Valid = a.I("valid", 600), Threads = a.I("threads", 0), Seed = (ulong)a.I("seed", 1), Profiles = Profiles(a), Ct = StopToken(), Log = Say, UseSkillBench = a.F("skill-bench"), TargetLoss = a.D("target-loss", 1.0) };
        if (a.S("groups") != null) o.Groups = new HashSet<string>(a.S("groups").Split(',')); if (o.UseSkillBench) o.Groups.Add("skill");
        var res = prune ? Optimizer.Prune(m, LoadOverrides(a.S("overrides") ?? throw new ArgumentException("--overrides")), tg, o) : Optimizer.Run(m, tg, o);
        string dir = Path.Combine(a.S("out") ?? OutRoot, name); Directory.CreateDirectory(dir); Directory.CreateDirectory(Path.Combine(Root, "balance", "proposals"));
        string prop = Path.Combine(Root, "balance", "proposals", name + ".json");
        File.WriteAllText(prop, JsonSerializer.Serialize(res.Overrides, Js)); File.WriteAllText(Path.Combine(dir, name + ".overrides.json"), JsonSerializer.Serialize(res.Overrides, Js));
        var sb = new StringBuilder("iter,loss,accepted,change\n"); foreach (var h in res.History) sb.AppendLine($"{h.iter},{Report.F(h.loss, "F4")},{h.accepted},\"{h.change}\""); File.WriteAllText(Path.Combine(dir, name + ".history.csv"), sb.ToString());
        var baseB = new Runner.Batch { PerProfile = o.Valid, BaseSeed = o.Seed }; var bestB = new Runner.Batch { PerProfile = o.Valid, BaseSeed = o.Seed };
        var baseVals = res.Overrides.Keys.ToDictionary(k => k, k => m.GetParam(k));
        var ex = new Extras { Proposal = res.Overrides, ProposalBase = baseVals, Notes = res.Verdict };
        var rep = new StringBuilder();
        rep.AppendLine($"# Оптимизация баланса: {name}\n\n**Итог:** {res.Verdict}\n");
        rep.AppendLine($"Оценок модели: {res.Evaluations}, итераций: {res.History.Count}. Обучающие сиды и проверочные сиды не пересекаются (смещение номеров прогонов 1 000 000).\n");
        rep.AppendLine("| Часть функции потерь | Обучение до | Обучение после | Проверка до | Проверка после |"); rep.AppendLine("|---|---|---|---|---|");
        foreach (var k in res.BaseTrain.Parts.Keys) rep.AppendLine($"| {k} | {Report.F(res.BaseTrain.Parts[k])} | {Report.F(res.BestTrain.Parts[k])} | {Report.F(res.BaseValid.Parts[k])} | {Report.F(res.BestValid.Parts[k])} |");
        rep.AppendLine($"| **сумма** | {Report.F(res.BaseTrain.Loss)} | {Report.F(res.BestTrain.Loss)} | {Report.F(res.BaseValid.Loss)} | {Report.F(res.BestValid.Loss)} |\n");
        rep.AppendLine("## Предлагаемые параметры\n\n| Параметр | Было | Станет |\n|---|---|---|");
        foreach (var kv in res.Overrides) rep.AppendLine($"| `{kv.Key}` | {Report.F(baseVals[kv.Key], "G4")} | {Report.F(kv.Value, "G4")} |");
        rep.AppendLine($"\nФайл предложения: `balance/proposals/{name}.json`. Оригинальные ресурсы **не изменены**; применение: `apply --overrides {name} --confirm` (только `tuning.*`).\n");
        rep.AppendLine(Report.CompareMd("до (проверочные сиды)", res.BaseValidSummary, "после (проверочные сиды)", res.BestValidSummary, tg, res.Overrides));
        File.WriteAllText(Path.Combine(dir, name + ".optimization.md"), rep.ToString());
        Report.WriteAll(dir, name + ".before", m, baseB, res.BaseValidSummary, tg, null, false); Report.WriteAll(dir, name + ".after", res.BestModel, bestB, res.BestValidSummary, tg, ex, false);
        Say($"предложение: {prop}; отчёты: {dir}");
        Say("RESULT " + JsonSerializer.Serialize(new { verified = res.Verified, verdict = res.Verdict, loss_train = new[] { res.BaseTrain.Loss, res.BestTrain.Loss }, loss_valid = new[] { res.BaseValid.Loss, res.BestValid.Loss }, changes = res.Overrides.Count, proposal = prop }));
        return 0;
    }

    // ───────────────────────── compare ─────────────────────────
    static int Compare(Args a)
    {
        var tg = LoadTargets(a); var basem = LoadModel(a, "none"); int n = a.I("n", 1000); ulong seed = (ulong)a.I("seed", 1); var ct = StopToken();
        var ma = a.S("a") is string pa && pa != "base" ? basem.With(LoadOverrides(pa)) : basem; var mb = a.S("b") is string pb && pb != "base" ? basem.With(LoadOverrides(pb)) : basem;
        var ba = Runner.Simulate(ma, Profiles(a), n, seed, a.I("threads", 0), ct); var bb = Runner.Simulate(mb, Profiles(a), n, seed, a.I("threads", 0), ct);
        var sa = Summary.Build(ba, ma, tg); var sb = Summary.Build(bb, mb, tg);
        var diff = new Dictionary<string, double>(); foreach (var k in mb.Overrides.Keys.Union(ma.Overrides.Keys)) diff[k] = mb.Overrides.GetValueOrDefault(k, double.NaN);
        string name = a.S("name") ?? "compare"; string dir = Path.Combine(a.S("out") ?? OutRoot, name); Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, name + ".md"), Report.CompareMd(a.S("a") ?? "base", sa, a.S("b") ?? "base", sb, tg, diff));
        File.WriteAllText(Path.Combine(dir, name + ".json"), JsonSerializer.Serialize(new { a = sa, b = sb }, Js));
        Say("сравнение: " + Path.Combine(dir, name + ".md")); Say("RESULT " + JsonSerializer.Serialize(new { a = Brief(sa), b = Brief(sb) })); return 0;
    }

    static int ReportCmd(Args a)
    {
        string from = a.S("from") ?? throw new ArgumentException("--from <папка/имя>"); string dir = Path.GetDirectoryName(from), name = Path.GetFileName(from);
        if (!Directory.Exists(dir)) dir = Path.Combine(OutRoot, dir ?? "");
        var s = JsonSerializer.Deserialize<Summary>(File.ReadAllText(Path.Combine(dir, name + ".summary.json")), new JsonSerializerOptions { IncludeFields = true, NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals });
        var m = JsonSerializer.Deserialize<BalanceModel>(File.ReadAllText(Path.Combine(dir, name + ".model.json")), new JsonSerializerOptions { IncludeFields = true });
        Extras ex = null; string sk = Path.Combine(dir, name + ".skills.json"); if (File.Exists(sk)) ex = new Extras { SkillRows = JsonSerializer.Deserialize<List<SkillRow>>(File.ReadAllText(sk), new JsonSerializerOptions { IncludeFields = true, NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals }) };
        string wp = Path.Combine(dir, name + ".weapons.json"); if (File.Exists(wp)) { ex ??= new Extras(); ex.WeaponRows = JsonSerializer.Deserialize<List<WeaponRow>>(File.ReadAllText(wp), new JsonSerializerOptions { IncludeFields = true, NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals }); }
        File.WriteAllText(Path.Combine(dir, name + ".md"), Report.Markdown(name, m, null, s, LoadTargets(a), ex)); Say("отчёт обновлён: " + Path.Combine(dir, name + ".md")); return 0;
    }

    // ───────────────────────── apply ─────────────────────────
    static int Apply(Args a)
    {
        if (!a.F("confirm")) { Say("Без --confirm ничего не меняется. Это запись в Resources/Combat/tuning.tres; перед записью создаётся копия."); return 1; }
        var ov = LoadOverrides(a.S("overrides") ?? throw new ArgumentException("--overrides")); string path = Path.Combine(Root, "Resources", "Combat", "tuning.tres");
        var lines = File.ReadAllLines(path).ToList(); File.Copy(path, path + ".bak_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"), true); int applied = 0;
        foreach (var kv in ov.Where(k => k.Key.StartsWith("tuning.")).GroupBy(k => k.Key.Substring(7).Split('[')[0]))
        {
            string field = kv.Key; int idx = lines.FindIndex(l => l.StartsWith(field + " = ")); if (idx < 0) { Say("поле не найдено в tuning.tres: " + field); continue; }
            string val = lines[idx].Substring(field.Length + 3).Trim(); var inv = System.Globalization.CultureInfo.InvariantCulture;
            if (val.StartsWith("PackedFloat32Array(")) { var arr = val.Substring(19).TrimEnd(')').Split(',').Select(x => x.Trim()).ToArray(); foreach (var e in kv) { int i = int.Parse(e.Key.Split('[')[1].TrimEnd(']')); arr[i] = e.Value.ToString("G6", inv); } lines[idx] = $"{field} = PackedFloat32Array({string.Join(", ", arr)})"; applied += kv.Count(); }
            else { lines[idx] = $"{field} = {kv.First().Value.ToString("G6", inv)}"; applied++; }
        }
        File.WriteAllLines(path, lines); Say($"применено параметров tuning: {applied}. Остальные ({ov.Count(k => !k.Key.StartsWith("tuning."))}) нужно менять вручную: skill.* — tools/gen_skills.py, weapon.* — Scripts/Combat/Data.cs, boss/enemy — Game.Combat.cs");
        foreach (var k in ov.Where(k => !k.Key.StartsWith("tuning."))) Say($"  вручную: {k.Key} = {k.Value:G5}"); return 0;
    }
}
