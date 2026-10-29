using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Lamplighter.Balance;

public sealed class Objective
{
    public double Loss; public Dictionary<string, double> Parts = new();
    public static double Out(double v, double lo, double hi, double scale) { if (double.IsNaN(v)) return 4; double d = v < lo ? lo - v : v > hi ? v - hi : 0; return (d / scale) * (d / scale); }

    /// <summary>Несколько целей сразу: сложность по профилям, темп, разрыв между профилями, разнообразие сборок, плавность боссов, экономика, польза навыков. Один «процент побед» не оптимизируется.</summary>
    public static Objective Eval(Summary s, Targets t, List<SkillRow> skills = null)
    {
        var o = new Objective(); double diff = 0, pace = 0, gap = 0, builds = 0, boss = 0, eco = 0, sk = 0;
        foreach (var p in s.Profiles)
        {
            if (!t.Profiles.TryGetValue(p.Profile, out var g)) continue;
            diff += Out(p.WinRate, g.WinMin, g.WinMax, .12) + Out(p.StuckRate + p.TimeoutRate, 0, g.QuitMax, .05) + Out(p.DeathsPerHour, 0, g.DeathsPerHourMax, g.DeathsPerHourMax * .5);
            double mid = (g.HoursMin + g.HoursMax) / 2; pace += p.Wins < 3 ? 4 : Out(p.HoursMedian, g.HoursMin, g.HoursMax, mid * .35);
            foreach (var kv in g.Milestones) { int mi = Array.IndexOf(Ms.Names, kv.Key); if (mi >= 0 && p.MilestoneN[mi] >= 3) pace += Out(p.MilestoneMedian[mi], kv.Value[0], kv.Value[1], (kv.Value[0] + kv.Value[1]) / 2 * .3) * .7; }
        }
        var no = s.Profiles.FirstOrDefault(p => p.Profile == "novice"); var ex = s.Profiles.FirstOrDefault(p => p.Profile == "expert"); var av = s.Profiles.FirstOrDefault(p => p.Profile == "average"); var op = s.Profiles.FirstOrDefault(p => p.Profile == "optimizer");
        if (no != null && ex != null && no.Wins >= 3 && ex.Wins >= 3) gap += Out(no.HoursMedian / ex.HoursMedian, t.NoviceExpertMin, t.NoviceExpertMax, .5);
        if (no != null && av != null && ex != null) gap += Out(no.WinRate - av.WinRate, -1, .02, .1) + Out(av.WinRate - ex.WinRate, -1, .02, .1);
        if (op != null && ex != null && op.Wins >= 3 && ex.Wins >= 3) gap += Out(op.HoursMedian / ex.HoursMedian, t.OptimizerSpeedMin, 1.5, .25);
        var ar = s.Archs.Where(a => a.Profile == "expert" && a.N >= 15).ToList();
        if (ar.Count >= 2) { builds += Out(ar.Max(a => a.WinRate) - ar.Min(a => a.WinRate), 0, t.BuildSpreadMax, .12); var tm = ar.Where(a => !double.IsNaN(a.HoursMedian)).ToList(); if (tm.Count >= 2) builds += Out(tm.Max(a => a.HoursMedian) / tm.Min(a => a.HoursMedian), 1, t.BuildTimeRatioMax, .3); }
        foreach (var p in s.Profiles.Where(p => p.Profile == "average" || p.Profile == "expert"))
        {
            for (int i = 0; i < 5; i++) { if (!double.IsNaN(p.BossAttempts[i])) boss += Out(p.BossAttempts[i], 1, t.BossAttemptsMax, 1.5) * .3; }
            if (p.MilestoneN[Ms.Boss1] > 3) boss += Out(p.MilestoneMedian[Ms.Boss1] * 60, t.FirstBossMinMinutes, 1e9, 15);
        }
        foreach (var it in s.Items) if (it.Gathered > 30) eco += Out(it.PercentLeft, 0, t.SurplusMax, .25) * .1;
        if (skills != null)
        {
            var c = skills.Where(r => r.Category == "combat" && r.CostSec > 0 && r.Significant && r.GainPct > 0).ToList(); if (c.Count > 5)
            {
                double mu = Mx.Median(c.Where(r => r.Level == 1).Select(r => r.GainPer100s).DefaultIfEmpty(1).ToList()), mp = Mx.Median(c.Where(r => r.Level > 1).Select(r => r.GainPer100s).DefaultIfEmpty(1).ToList());
                int bad = c.Count(r => { double md = r.Level == 1 ? mu : mp; return r.GainPer100s > md * t.SkillEffRatioMax || r.GainPer100s < md * t.SkillEffRatioMin; });
                sk += (double)bad / c.Count * 3; sk += skills.Count(r => r.Category == "combat" && !r.Significant && 2 * r.GainSe < t.SkillMinGainPct * 2) / (double)Math.Max(1, skills.Count(r => r.Category == "combat")) * 2;
            }
        }
        o.Parts["difficulty"] = diff * t.WDifficulty; o.Parts["pace"] = pace * t.WPace; o.Parts["gap"] = gap * t.WSkillGap; o.Parts["builds"] = builds * t.WBuilds; o.Parts["boss"] = boss * t.WBoss; o.Parts["economy"] = eco * t.WEconomy; o.Parts["skills"] = sk * t.WSkills;
        o.Loss = o.Parts.Values.Sum(); return o;
    }
}

public sealed class OptimizeOptions
{
    public int Iterations = 12, Lambda = 6, Train = 150, Valid = 600, Threads; public ulong Seed = 1; public string[] Profiles = Profile.All; public HashSet<string> Groups = new() { "tuning", "boss" };
    public bool UseSkillBench; public int BenchK = 60; public double TargetLoss = 1.0; public CancellationToken Ct;
    public Action<string> Log = Console.WriteLine;
}

public sealed class OptimizeResult
{
    public Dictionary<string, double> Overrides = new(); public Objective BaseTrain, BestTrain, BaseValid, BestValid; public Summary BaseValidSummary, BestValidSummary, BaseTrainSummary, BestTrainSummary;
    public List<(int iter, double loss, string change, bool accepted)> History = new(); public bool Verified; public string Verdict; public BalanceModel BestModel; public int Evaluations;
}

public static class Optimizer
{
    static readonly HashSet<string> Skip = new() { "tuning.ScarcityMul", "tuning.WeaponMineMul", "tuning.EliteMinLit", "tuning.MaxAttackers", "tuning.EliteMax", "tuning.BossTempo2", "tuning.SpawnCapBonus" };
    static readonly HashSet<string> Ints = new();

    static Summary Run(BalanceModel m, OptimizeOptions o, int per, int offset, Targets t)
    {
        var b = Runner.Simulate(m, o.Profiles, per, o.Seed, o.Threads, o.Ct, null, offset);
        return Summary.Build(b, m, t);
    }

    static Objective Score(BalanceModel m, Summary s, OptimizeOptions o, Targets t)
    {
        List<SkillRow> rows = o.UseSkillBench ? Bench.Skills(m, o.BenchK, o.Seed, o.Threads) : null; return Objective.Eval(s, t, rows);
    }

    /// <summary>Параметры-кандидаты по диагнозу: направление (+1 увеличить, −1 уменьшить), вес.</summary>
    static List<(string key, int dir, double w)> Diagnose(Summary s, Targets t, BalanceModel m, OptimizeOptions o)
    {
        var l = new List<(string, int, double)>();
        void Add(string k, int d, double w) { if (m.Params().Any(p => p.Key == k)) l.Add((k, d, w)); }
        int[] pace = { 0 };
        foreach (var p in s.Profiles)
        {
            if (!t.Profiles.TryGetValue(p.Profile, out var g)) continue;
            double wp = p.Profile == "average" || p.Profile == "expert" ? 1.2 : 1.0;
            if (p.WinRate < g.WinMin) { foreach (var k in new[] { "tuning.EnemyHp", "tuning.EnemyDamage", "tuning.BiomeHp[1]", "tuning.BiomeHp[2]", "tuning.BiomeDamage[2]", "tuning.BiomeDamage[3]", "tuning.BiomeHp[3]", "tuning.BiomeHp[4]" }) Add(k, -1, wp); for (int i = 0; i < 5; i++) { Add($"tuning.BossHp[{i}]", -1, wp * 1.3); Add($"tuning.BossDamage[{i}]", -1, wp); } Add("tuning.RegenRate", +1, .5); }
            if (p.WinRate > g.WinMax) { foreach (var k in new[] { "tuning.EnemyHp", "tuning.EnemyDamage", "tuning.BiomeHp[1]", "tuning.BiomeDamage[2]" }) Add(k, +1, wp); for (int i = 0; i < 5; i++) { Add($"tuning.BossHp[{i}]", +1, wp); Add($"tuning.BossDamage[{i}]", +1, wp * .8); } }
            if (p.Wins > 3 && p.HoursMedian > g.HoursMax) { foreach (var k in new[] { "tuning.XpBase", "tuning.XpLinear", "tuning.XpQuad", "tuning.MineTimeMul", "tuning.TechCostMul" }) Add(k, -1, wp); Add("tuning.GatherMul", +1, wp * .8); }
            if (p.Wins > 3 && p.HoursMedian < g.HoursMin) { foreach (var k in new[] { "tuning.XpBase", "tuning.XpLinear", "tuning.XpQuad", "tuning.MineTimeMul", "tuning.TechCostMul" }) Add(k, +1, wp); Add("tuning.GatherMul", -1, wp * .8); }
            if (p.Wins <= 3) { Add("tuning.EnemyHp", -1, 1.5); Add("tuning.XpMul", +1, 1); }
            if (p.DeathsPerHour > g.DeathsPerHourMax) { Add("tuning.EnemyDamage", -1, 1); Add("tuning.HitInvuln", +1, .6); }
        }
        foreach (var p in s.Profiles.Where(p => p.Profile == "average" || p.Profile == "expert"))
            for (int i = 0; i < 5; i++) if (!double.IsNaN(p.BossAttempts[i]) && p.BossAttempts[i] > t.BossAttemptsMax) { Add($"tuning.BossHp[{i}]", -1, 2); Add($"tuning.BossDamage[{i}]", -1, 1.5); }
        foreach (var f in s.Findings)
        {
            if (f.Area == "builds" && o.Groups.Contains("weapon")) foreach (var a in s.Archs.Where(a => a.Profile == "expert" && a.N >= 15))
                {
                    var arch = Profile.Archetypes.FirstOrDefault(x => x.Name == a.Arch); if (arch == null) continue; double mean = s.Archs.Where(x => x.Profile == "expert" && x.N >= 15).Average(x => x.WinRate);
                    int dir = a.WinRate < mean - t.BuildSpreadMax / 2 ? +1 : a.WinRate > mean + t.BuildSpreadMax / 2 ? -1 : 0; if (dir == 0) continue;
                    foreach (var w in m.Weapons.Where(w => w.Cls == (int)arch.Weapon && !w.Unique)) Add($"weapon.{w.Id}.Dmg", dir, 1.2);
                    if (o.Groups.Contains("skill")) foreach (var sk in arch.Skills.Take(5)) Add($"skill.{sk}.EffPer", dir, .8);
                }
            if (f.Area == "enemy" && o.Groups.Contains("enemy") && f.Fix != null)
            {
                var mm = System.Text.RegularExpressions.Regex.Match(f.Fix, @"enemy\.(\w+)\."); if (mm.Success)
                    foreach (var pp in m.Params().Where(x => x.Key.StartsWith($"enemy.{mm.Groups[1].Value}@") && x.Key.EndsWith(".Dmg"))) l.Add((pp.Key, f.Fix.StartsWith("снизить") ? -1 : +1, 1.5));
            }
            if (f.Area == "economy") Add("tuning.GatherMul", -1, .5);
            if (f.Area == "exploit") { Add("tuning.TechCostMul", +1, .6); }
        }
        return l;
    }

    public static OptimizeResult Run(BalanceModel baseModel, Targets t, OptimizeOptions o)
    {
        var rng = new Rng(Rng.Mix(o.Seed, 99)); var res = new OptimizeResult();
        var allP = baseModel.Params().Where(p => o.Groups.Contains(p.Group) && !Skip.Contains(p.Key)).ToList();
        var byKey = allP.ToDictionary(p => p.Key);
        o.Log($"[opt] параметров доступно: {allP.Count}; групп: {string.Join(",", o.Groups)}; обучающих прогонов на профиль: {o.Train}; проверочных: {o.Valid}");
        var cur = baseModel.Clone(); var curOv = new Dictionary<string, double>();
        var sTrain = Run(cur, o, o.Train, 0, t); res.BaseTrainSummary = sTrain; var oTrain = Score(cur, sTrain, o, t); res.BaseTrain = oTrain; res.Evaluations++;
        o.Log($"[opt] старт: потеря={oTrain.Loss:F3} ({Fmt(oTrain)})");
        double scale = 1.0; double bestLoss = oTrain.Loss; int stall = 0;
        for (int it = 1; it <= o.Iterations && !o.Ct.IsCancellationRequested && bestLoss > o.TargetLoss; it++)
        {
            var diag = Diagnose(sTrain, t, cur, o).Where(d => byKey.ContainsKey(d.key)).ToList();
            BalanceModel bestC = null; Dictionary<string, double> bestOv = null; Summary bestS = null; Objective bestO = null; string bestChange = "";
            for (int c = 0; c < o.Lambda && !o.Ct.IsCancellationRequested; c++)
            {
                var ov = new Dictionary<string, double>(curOv); var cand = cur.Clone(); var changes = new List<string>();
                int nm = 1 + rng.Int(0, 3 + stall / 3);
                for (int q = 0; q < nm; q++)
                {
                    string key; int dir;
                    if (diag.Count > 0 && rng.Chance(.75f)) { double sum = diag.Sum(d => d.w), rr = rng.F() * sum; var pick = diag[0]; foreach (var d in diag) { rr -= d.w; if (rr <= 0) { pick = d; break; } } key = pick.key; dir = pick.dir; if (rng.Chance(.2f)) dir = -dir; }
                    else { var p = allP[rng.Int(0, allP.Count - 1)]; key = p.Key; dir = rng.Chance(.5f) ? 1 : -1; }
                    var par = byKey[key]; double v = par.Get(cand), step = par.Step * scale * rng.Range(.5f, 1.5f) * (rng.Chance(.2f) ? 3.0 : 1.0); double nv = Math.Clamp(v + dir * step, par.Min, par.Max);
                    if (Math.Abs(nv - v) < par.Step * .02) continue; par.Set(cand, nv); ov[key] = nv; changes.Add($"{key}:{v:G4}→{nv:G4}");
                }
                if (changes.Count == 0) continue;
                var s = Run(cand, o, o.Train, 0, t); res.Evaluations++; var ob = Score(cand, s, o, t);
                if (bestO == null || ob.Loss < bestO.Loss) { bestO = ob; bestC = cand; bestOv = ov; bestS = s; bestChange = string.Join("; ", changes); }
            }
            if (bestO != null && bestO.Loss < bestLoss - 1e-6)
            {
                bestLoss = bestO.Loss; cur = bestC; curOv = bestOv; sTrain = bestS; scale = Math.Min(2.0, scale * 1.15); stall = 0;
                res.History.Add((it, bestO.Loss, bestChange, true)); o.Log($"[opt] #{it}: потеря {bestO.Loss:F3} ({Fmt(bestO)}) ← {bestChange}");
            }
            else { scale = scale * .8; if (scale < .5) scale = 2.0; stall++; res.History.Add((it, bestO?.Loss ?? double.NaN, bestChange, false)); o.Log($"[opt] #{it}: улучшения нет (лучший кандидат {bestO?.Loss:F3} ≥ {bestLoss:F3}), масштаб шага {scale:F2}"); if (stall >= 14) break; }
        }
        res.Overrides = curOv; res.BestModel = cur; res.BestTrain = Score(cur, sTrain, o, t); res.BestTrainSummary = sTrain;
        if (o.Ct.IsCancellationRequested)
        {
            res.BaseValid = res.BaseTrain; res.BestValid = res.BestTrain; res.BaseValidSummary = res.BaseTrainSummary; res.BestValidSummary = sTrain; res.Verified = false;
            res.Verdict = "остановлено пользователем до проверки на независимых сидах: предложение НЕ подтверждено"; o.Log("[opt] " + res.Verdict); return res;
        }
        // независимая проверка на других сидах (смещение номеров прогонов)
        o.Log($"[opt] проверка на независимых сидах (по {o.Valid} прогонов на профиль)…");
        res.BaseValidSummary = Run(baseModel, o, o.Valid, 1_000_000, t); res.BaseValid = Score(baseModel, res.BaseValidSummary, o, t);
        res.BestValidSummary = Run(cur, o, o.Valid, 1_000_000, t); res.BestValid = Score(cur, res.BestValidSummary, o, t);
        res.Verified = curOv.Count > 0 && res.BestValid.Loss < res.BaseValid.Loss * .95;
        res.Verdict = curOv.Count == 0 ? "изменений не найдено" : res.Verified ? $"подтверждено: потеря на независимых сидах {res.BaseValid.Loss:F2} → {res.BestValid.Loss:F2}" : $"НЕ подтверждено: потеря на независимых сидах {res.BaseValid.Loss:F2} → {res.BestValid.Loss:F2} (улучшение на обучающих сидах {res.BaseTrain.Loss:F2} → {res.BestTrain.Loss:F2} может быть подгонкой)";
        o.Log("[opt] " + res.Verdict); return res;
    }

    /// <summary>Убирает из предложения изменения, без которых результат не ухудшается (обратное исключение на обучающих сидах): короткий список правок вместо десятков.
    /// Итог проверяется на независимых сидах так же, как и сам поиск.</summary>
    public static OptimizeResult Prune(BalanceModel baseModel, Dictionary<string, double> ov, Targets t, OptimizeOptions o)
    {
        var res = new OptimizeResult(); var cur = new Dictionary<string, double>(ov);
        double Eval(Dictionary<string, double> d) { var m = baseModel.With(d); res.Evaluations++; return Score(m, Run(m, o, o.Train, 0, t), o, t).Loss; }
        double loss = Eval(cur); res.BaseTrain = Score(baseModel, Run(baseModel, o, o.Train, 0, t), o, t); double start = loss;
        o.Log($"[prune] правок: {cur.Count}, потеря на обучающих сидах {res.BaseTrain.Loss:F2} → {loss:F2}");
        var order = cur.Keys.OrderBy(k => Math.Abs(cur[k] - baseModel.GetParam(k)) / Math.Max(1e-9, Math.Abs(baseModel.GetParam(k)))).ToList();
        foreach (var k in order)
        {
            if (o.Ct.IsCancellationRequested) break; var trial = new Dictionary<string, double>(cur); trial.Remove(k); double l = Eval(trial);
            if (l <= loss + Math.Max(.05, loss * .03)) { cur = trial; loss = Math.Min(loss, l); o.Log($"[prune] убрана правка {k} (потеря {l:F2})"); }
        }
        res.Overrides = cur; res.BestModel = baseModel.With(cur); res.BestTrain = Score(res.BestModel, Run(res.BestModel, o, o.Train, 0, t), o, t); res.BaseTrainSummary = Run(baseModel, o, o.Train, 0, t); res.BestTrainSummary = Run(res.BestModel, o, o.Train, 0, t);
        res.BaseValidSummary = Run(baseModel, o, o.Valid, 1_000_000, t); res.BaseValid = Score(baseModel, res.BaseValidSummary, o, t);
        res.BestValidSummary = Run(res.BestModel, o, o.Valid, 1_000_000, t); res.BestValid = Score(res.BestModel, res.BestValidSummary, o, t);
        res.Verified = res.BestValid.Loss < res.BaseValid.Loss * .95;
        res.Verdict = $"{(res.Verified ? "подтверждено" : "НЕ подтверждено")}: правок {ov.Count} → {cur.Count}; потеря на независимых сидах {res.BaseValid.Loss:F2} → {res.BestValid.Loss:F2}";
        o.Log("[prune] " + res.Verdict); return res;
    }

    static string Fmt(Objective o) => string.Join(", ", o.Parts.Where(kv => kv.Value > 0.005).Select(kv => $"{kv.Key} {kv.Value:F2}"));
}
