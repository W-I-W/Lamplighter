using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Lamplighter.Balance;

public static class Mx
{
    public static double Mean(IList<double> x) => x.Count == 0 ? double.NaN : x.Average();
    public static double Sd(IList<double> x) { if (x.Count < 2) return 0; double m = x.Average(); return Math.Sqrt(x.Sum(v => (v - m) * (v - m)) / (x.Count - 1)); }
    public static double Pct(IList<double> x, double q) { if (x.Count == 0) return double.NaN; var s = x.OrderBy(v => v).ToArray(); double pos = q * (s.Length - 1); int i = (int)pos; double f = pos - i; return i + 1 < s.Length ? s[i] * (1 - f) + s[i + 1] * f : s[i]; }
    public static double Median(IList<double> x) => Pct(x, .5);
    /// <summary>Половина ширины 95% доверительного интервала среднего (нормальное приближение).</summary>
    public static double CiHalf(IList<double> x) => x.Count < 2 ? double.NaN : 1.96 * Sd(x) / Math.Sqrt(x.Count);
    /// <summary>Интервал Уилсона для доли.</summary>
    public static (double lo, double hi) Wilson(int k, int n)
    {
        if (n == 0) return (double.NaN, double.NaN); double z = 1.96, p = (double)k / n, d = 1 + z * z / n, c = p + z * z / (2 * n), a = z * Math.Sqrt(p * (1 - p) / n + z * z / (4.0 * n * n));
        return ((c - a) / d, (c + a) / d);
    }
    /// <summary>Приближённый 95% интервал медианы (порядковые статистики, биномиальное приближение).</summary>
    public static (double lo, double hi) MedianCi(IList<double> x)
    {
        if (x.Count < 5) return (double.NaN, double.NaN); var s = x.OrderBy(v => v).ToArray(); int n = s.Length; double h = 0.98 * Math.Sqrt(n);
        return (s[Math.Max(0, (int)(n / 2.0 - h))], s[Math.Min(n - 1, (int)(n / 2.0 + h))]);
    }
}

/// <summary>Массовый запуск: каждый прогон независим, сид прогона = f(общий сид, профиль, номер), поэтому результат не зависит от числа потоков и порядка.</summary>
public static class Runner
{
    public sealed class Batch { public List<RunResult> Runs = new(); public double Seconds; public int Threads; public bool Cancelled; public ulong BaseSeed; public int PerProfile; }

    public static Batch Simulate(BalanceModel model, string[] profiles, int perProfile, ulong baseSeed, int threads = 0, CancellationToken ct = default, Action<int, int> progress = null, int seedOffset = 0)
    {
        var sw = Stopwatch.StartNew(); int total = profiles.Length * perProfile;
        var results = new RunResult[total]; int done = 0;
        threads = threads <= 0 ? Math.Max(1, Environment.ProcessorCount - 1) : threads;
        var opts = new ParallelOptions { MaxDegreeOfParallelism = threads, CancellationToken = ct };
        bool cancelled = false;
        try
        {
            Parallel.For(0, total, opts, i =>
            {
                int pi = i / perProfile, ri = i % perProfile;
                var prof = Profile.ByName(profiles[pi]);
                ulong seed = Rng.Mix(baseSeed, (ulong)pi + 1, (ulong)(ri + seedOffset) + 1, 77);
                var c = new Campaign(model, prof, seed);
                results[i] = c.Run();
                int d = Interlocked.Increment(ref done); if (progress != null && (d % 100 == 0 || d == total)) progress(d, total);
            });
        }
        catch (OperationCanceledException) { cancelled = true; }
        var b = new Batch { Seconds = sw.Elapsed.TotalSeconds, Threads = threads, Cancelled = cancelled, BaseSeed = baseSeed, PerProfile = perProfile };
        foreach (var r in results) if (r != null) b.Runs.Add(r);
        return b;
    }

    /// <summary>Хэш всех результатов — для проверки воспроизводимости.</summary>
    public static ulong Hash(IEnumerable<RunResult> runs)
    {
        ulong h = 1469598103934665603UL;
        foreach (var r in runs)
        {
            h = (h ^ r.Seed) * 1099511628211UL; h = (h ^ (ulong)r.Out) * 1099511628211UL; h = (h ^ (ulong)BitConverter.SingleToInt32Bits(r.Hours)) * 1099511628211UL;
            h = (h ^ (ulong)r.Deaths) * 1099511628211UL; h = (h ^ (ulong)r.Level) * 1099511628211UL;
        }
        return h;
    }
}

// ───────────────────────── сводка ─────────────────────────
public sealed class ProfileSummary
{
    public string Profile; public int N, Wins, Stuck, Timeout; public double WinRate, WinLo, WinHi, StuckRate, TimeoutRate;
    public double HoursMean, HoursMedian, HoursCi, HoursP10, HoursP90, HoursSd, HoursMedLo, HoursMedHi;
    public double DeathsMean, DeathsMedian, DeathsSd, DeathsPerHour, LevelMedian, KillsMean;
    public double[] MilestoneMedian = new double[Ms.Names.Length]; public int[] MilestoneN = new int[Ms.Names.Length]; public double[] LevelAtMs = new double[Ms.Names.Length];
    public double[] DeathsRegion = new double[5], DeathsBoss = new double[5], BossAttempts = new double[5], BossFirstTry = new double[5];
    public double[] LevelAtHour = new double[12]; public bool LowConfidence;
    public double TFight, TGather, TTravel, TRest, TCraft, TExplore;
}

public sealed class ArchSummary { public string Profile, Arch; public int N, Wins; public double WinRate, WinLo, WinHi, HoursMedian; public double DeathsMean; }
public sealed class EnemyStat { public string Enemy; public double Spawned, Killed, DeathsPerThousand; public int Deaths; }
public sealed class ItemStat { public string Item; public double Gathered, EndInv, Surplus, PercentLeft; }
public sealed class SkillUse { public string Skill; public Dictionary<string, double> BoughtRate = new(), MeanLevel = new(); }
public sealed class WeaponUse { public string Weapon; public Dictionary<string, double> Share = new(); public double WinRate; public int N; }

public sealed class Finding { public string Severity, Area, Text; public double Value; public string Fix; }

public sealed class Summary
{
    public int TotalRuns; public double Seconds; public int Threads; public ulong Seed; public int PerProfile;
    public List<ProfileSummary> Profiles = new(); public List<ArchSummary> Archs = new(); public List<EnemyStat> Enemies = new();
    public List<ItemStat> Items = new(); public List<SkillUse> Skills = new(); public List<WeaponUse> Weapons = new(); public List<Finding> Findings = new();

    public static Summary Build(Runner.Batch b, BalanceModel m, Targets tg)
    {
        var s = new Summary { TotalRuns = b.Runs.Count, Seconds = b.Seconds, Threads = b.Threads, Seed = b.BaseSeed, PerProfile = b.PerProfile };
        foreach (var g in b.Runs.GroupBy(r => r.Profile).OrderBy(g => { int ix = Array.IndexOf(Profile.All, g.Key); return ix < 0 ? 99 : ix; }))
        {
            var rs = g.ToList(); var ps = new ProfileSummary { Profile = g.Key, N = rs.Count };
            ps.Wins = rs.Count(r => r.Out == Outcome.Won); ps.Stuck = rs.Count(r => r.Out == Outcome.Stuck); ps.Timeout = rs.Count(r => r.Out == Outcome.Timeout);
            ps.WinRate = (double)ps.Wins / ps.N; (ps.WinLo, ps.WinHi) = Mx.Wilson(ps.Wins, ps.N); ps.StuckRate = (double)ps.Stuck / ps.N; ps.TimeoutRate = (double)ps.Timeout / ps.N;
            var wt = rs.Where(r => r.Out == Outcome.Won).Select(r => (double)r.Hours).ToList();
            if (wt.Count > 0) { ps.HoursMean = wt.Average(); ps.HoursMedian = Mx.Median(wt); ps.HoursCi = Mx.CiHalf(wt); ps.HoursP10 = Mx.Pct(wt, .1); ps.HoursP90 = Mx.Pct(wt, .9); ps.HoursSd = Mx.Sd(wt); (ps.HoursMedLo, ps.HoursMedHi) = Mx.MedianCi(wt); }
            var dd = rs.Select(r => (double)r.Deaths).ToList(); ps.DeathsMean = dd.Average(); ps.DeathsMedian = Mx.Median(dd); ps.DeathsSd = Mx.Sd(dd);
            ps.DeathsPerHour = rs.Sum(r => r.Deaths) / Math.Max(.01, rs.Sum(r => (double)r.Hours));
            ps.LevelMedian = Mx.Median(rs.Select(r => (double)r.Level).ToList()); ps.KillsMean = rs.Average(r => (double)r.Kills);
            for (int i = 0; i < Ms.Names.Length; i++)
            {
                var v = rs.Where(r => r.Ms[i] >= 0).ToList(); ps.MilestoneN[i] = v.Count;
                ps.MilestoneMedian[i] = v.Count > 0 ? Mx.Median(v.Select(r => (double)r.Ms[i]).ToList()) : double.NaN; ps.LevelAtMs[i] = v.Count > 0 ? Mx.Median(v.Select(r => (double)r.LevelAtMs[i]).ToList()) : double.NaN;
            }
            for (int i = 0; i < 5; i++)
            {
                ps.DeathsRegion[i] = rs.Average(r => (double)r.DeathsRegion[i]); ps.DeathsBoss[i] = rs.Average(r => (double)r.DeathsBoss[i]);
                var att = rs.Where(r => r.BossAttempts[i] > 0).ToList(); ps.BossAttempts[i] = att.Count > 0 ? att.Average(r => (double)r.BossAttempts[i]) : double.NaN; ps.BossFirstTry[i] = att.Count > 0 ? att.Count(r => r.BossAttempts[i] == 1 && r.DeathsBoss[i] == 0) / (double)att.Count : double.NaN;
            }
            for (int h = 0; h < 12; h++) { var v = rs.Select(r => (double)r.LevelAtHour[h]).Where(x => x > 0).ToList(); ps.LevelAtHour[h] = v.Count > 0 ? Mx.Median(v) : double.NaN; }
            ps.TFight = rs.Average(r => (double)r.TFight) / 3600; ps.TGather = rs.Average(r => (double)r.TGather) / 3600; ps.TTravel = rs.Average(r => (double)r.TTravel) / 3600; ps.TRest = rs.Average(r => (double)r.TRest) / 3600; ps.TCraft = rs.Average(r => (double)r.TCraft) / 3600; ps.TExplore = rs.Average(r => (double)r.TExplore) / 3600;
            ps.LowConfidence = ps.N < tg.MinSims; s.Profiles.Add(ps);
        }
        foreach (var g in b.Runs.GroupBy(r => (r.Profile, r.Arch)))
        {
            var rs = g.ToList(); int w = rs.Count(r => r.Out == Outcome.Won); var (lo, hi) = Mx.Wilson(w, rs.Count);
            var wt = rs.Where(r => r.Out == Outcome.Won).Select(r => (double)r.Hours).ToList();
            s.Archs.Add(new ArchSummary { Profile = g.Key.Profile, Arch = g.Key.Arch, N = rs.Count, Wins = w, WinRate = (double)w / rs.Count, WinLo = lo, WinHi = hi, HoursMedian = wt.Count > 0 ? Mx.Median(wt) : double.NaN, DeathsMean = rs.Average(r => (double)r.Deaths) });
        }
        // враги
        var sp = new Dictionary<string, double>(); var kl = new Dictionary<string, int>();
        foreach (var r in b.Runs) { foreach (var kv in r.Spawned) sp[kv.Key.Split('@')[0]] = sp.GetValueOrDefault(kv.Key.Split('@')[0]) + kv.Value; foreach (var kv in r.Killers) kl[kv.Key] = kl.GetValueOrDefault(kv.Key) + kv.Value; }
        foreach (var k in sp.Keys.Union(kl.Keys).OrderBy(x => x)) s.Enemies.Add(new EnemyStat { Enemy = k, Spawned = sp.GetValueOrDefault(k), Deaths = kl.GetValueOrDefault(k), DeathsPerThousand = sp.GetValueOrDefault(k) > 0 ? 1000.0 * kl.GetValueOrDefault(k) / sp[k] : double.NaN });
        // ресурсы
        var names = (string[])typeof(It).GetField("Names").GetValue(null);
        for (int i = 0; i < It.Count; i++)
        {
            var wins = b.Runs.Where(r => r.Out == Outcome.Won).ToList(); if (wins.Count == 0) wins = b.Runs;
            double g = wins.Average(r => (double)r.Gathered[i]), e = wins.Average(r => (double)r.EndInv[i]);
            s.Items.Add(new ItemStat { Item = names[i], Gathered = g, EndInv = e, Surplus = e, PercentLeft = g > 0 ? e / g : 0 });
        }
        // навыки
        for (int i = 0; i < m.Skills.Length; i++)
        {
            var su = new SkillUse { Skill = m.Skills[i].Id };
            foreach (var g in b.Runs.GroupBy(r => r.Profile)) { su.BoughtRate[g.Key] = g.Average(r => r.SkillLv[i] > 0 ? 1.0 : 0.0); su.MeanLevel[g.Key] = g.Average(r => (double)r.SkillLv[i]); }
            s.Skills.Add(su);
        }
        foreach (var g in b.Runs.GroupBy(r => r.Weapon).OrderByDescending(g => g.Count()))
        {
            var wu = new WeaponUse { Weapon = g.Key, N = g.Count(), WinRate = g.Average(r => r.Out == Outcome.Won ? 1.0 : 0.0) };
            foreach (var pg in b.Runs.GroupBy(r => r.Profile)) wu.Share[pg.Key] = pg.Count(r => r.Weapon == g.Key) / (double)pg.Count(); s.Weapons.Add(wu);
        }
        Detect(s, m, tg, b); return s;
    }

    static void Detect(Summary s, BalanceModel m, Targets tg, Runner.Batch b)
    {
        void F(string sev, string area, string text, double v = double.NaN, string fix = "") => s.Findings.Add(new Finding { Severity = sev, Area = area, Text = text, Value = v, Fix = fix });
        ProfileSummary P(string n) => s.Profiles.FirstOrDefault(p => p.Profile == n);
        foreach (var ps in s.Profiles)
        {
            if (ps.LowConfidence) F("info", "stats", $"{ps.Profile}: {ps.N} прогонов < {tg.MinSims}: выводы по профилю предварительные");
            if (!tg.Profiles.TryGetValue(ps.Profile, out var t)) continue; bool ok = !ps.LowConfidence;
            string sev = ok ? "warn" : "info";
            if (ps.WinHi < t.WinMin) F(sev, "difficulty", $"{ps.Profile}: побеждает {ps.WinRate:P0} (ДИ {ps.WinLo:P0}–{ps.WinHi:P0}), цель ≥ {t.WinMin:P0}: слишком сложно", ps.WinRate, "уменьшить EnemyHp/EnemyDamage/BossHp");
            if (ps.WinLo > t.WinMax) F(sev, "difficulty", $"{ps.Profile}: побеждает {ps.WinRate:P0} (ДИ {ps.WinLo:P0}–{ps.WinHi:P0}), цель ≤ {t.WinMax:P0}: слишком легко", ps.WinRate, "увеличить EnemyHp/EnemyDamage/BossHp");
            if (ps.Wins > 5 && ps.HoursMedian < t.HoursMin) F(sev, "pace", $"{ps.Profile}: медианное время {ps.HoursMedian:F1} ч < цели {t.HoursMin} ч: прогрессия слишком быстрая", ps.HoursMedian, "увеличить XpBase/XpLinear/XpQuad, MineTimeMul, TechCostMul");
            if (ps.Wins > 5 && ps.HoursMedian > t.HoursMax) F(sev, "pace", $"{ps.Profile}: медианное время {ps.HoursMedian:F1} ч > цели {t.HoursMax} ч: прогрессия слишком медленная", ps.HoursMedian, "уменьшить XpBase/XpLinear/XpQuad, MineTimeMul, TechCostMul");
            if (ps.DeathsPerHour > t.DeathsPerHourMax) F("warn", "difficulty", $"{ps.Profile}: {ps.DeathsPerHour:F1} смертей/ч > {t.DeathsPerHourMax}", ps.DeathsPerHour);
            if (ps.StuckRate > t.QuitMax) F("warn", "softlock", $"{ps.Profile}: {ps.StuckRate:P1} прогонов застряли или бросили игру (> {t.QuitMax:P0}); причины: {string.Join("; ", b.Runs.Where(r => r.Profile == ps.Profile && r.Out == Outcome.Stuck).Select(r => r.StuckReason).GroupBy(x => x).OrderByDescending(g => g.Count()).Take(3).Select(g => g.Key + " ×" + g.Count()))}", ps.StuckRate);
            foreach (var kv in t.Milestones) { int mi = Array.IndexOf(Ms.Names, kv.Key); if (mi < 0 || ps.MilestoneN[mi] < 5) continue; double v = ps.MilestoneMedian[mi]; if (v < kv.Value[0] || v > kv.Value[1]) F(sev, "pace", $"{ps.Profile}: этап «{kv.Key}» наступает на {v:F2}-м часе (цель {kv.Value[0]}–{kv.Value[1]} ч, PLAYTEST.md)", v); }
            if (ps.TimeoutRate > tg.StuckMax) F("warn", "pace", $"{ps.Profile}: {ps.TimeoutRate:P1} прогонов не уложились в {m.A.MaxHours} ч", ps.TimeoutRate);
        }
        // порядок профилей
        var no = P("novice"); var av = P("average"); var ex = P("expert"); var op = P("optimizer");
        if (no != null && av != null && ex != null && no.Wins > 5 && av.Wins > 5 && ex.Wins > 5)
        {
            double ratio = no.HoursMedian / ex.HoursMedian;
            if (ratio < tg.NoviceExpertMin) F("warn", "skill-gap", $"новичок проходит лишь в {ratio:F2}× дольше эксперта (цель ≥ {tg.NoviceExpertMin}): навык игрока почти не влияет на темп", ratio);
            if (ratio > tg.NoviceExpertMax) F("warn", "skill-gap", $"новичок проходит в {ratio:F2}× дольше эксперта (цель ≤ {tg.NoviceExpertMax}): порог входа слишком высок", ratio);
            if (!(no.WinRate <= av.WinRate + .02 && av.WinRate <= ex.WinRate + .02)) F("warn", "skill-gap", $"процент побед не растёт с мастерством: {no.WinRate:P0} / {av.WinRate:P0} / {ex.WinRate:P0}");
        }
        if (op != null && ex != null && op.Wins > 5 && ex.Wins > 5)
        {
            double sp = op.HoursMedian / ex.HoursMedian;
            if (sp < tg.OptimizerSpeedMin) F("high", "exploit", $"оптимизатор проходит за {sp:P0} времени эксперта (цель ≥ {tg.OptimizerSpeedMin:P0}): найдена слишком выгодная стратегия (меньше фонарей = слабее враги, жадная прокачка)", sp, "проверить DangerScale (+0,18 за фонарь) и стоимость навыков с лучшей отдачей");
        }
        // сборки
        foreach (var g in s.Archs.Where(a => a.Profile == "expert" && a.N >= tg.MinPerArch).ToList().GroupBy(a => a.Profile))
        {
            var lst = g.ToList(); if (lst.Count < 2) continue; var best = lst.OrderByDescending(a => a.WinRate).First(); var worst = lst.OrderBy(a => a.WinRate).First();
            if (best.WinLo > worst.WinHi + tg.BuildSpreadMax) F("warn", "builds", $"сборка «{best.Arch}» побеждает {best.WinRate:P0} (ДИ {best.WinLo:P0}–{best.WinHi:P0}), «{worst.Arch}» только {worst.WinRate:P0} (ДИ {worst.WinLo:P0}–{worst.WinHi:P0}): разрыв значим (> {tg.BuildSpreadMax:P0})", best.WinRate - worst.WinRate, "подтянуть слабую сборку: оружие/вставки/навыки её ветки");
            var tm = lst.Where(a => !double.IsNaN(a.HoursMedian)).ToList(); if (tm.Count >= 2) { var fast = tm.OrderBy(a => a.HoursMedian).First(); var slow = tm.OrderByDescending(a => a.HoursMedian).First(); if (slow.HoursMedian / fast.HoursMedian > tg.BuildTimeRatioMax) F("warn", "builds", $"сборка «{fast.Arch}» проходит за {fast.HoursMedian:F1} ч, «{slow.Arch}» за {slow.HoursMedian:F1} ч (разрыв {slow.HoursMedian / fast.HoursMedian:F2}× > {tg.BuildTimeRatioMax}×)", slow.HoursMedian / fast.HoursMedian); }
        }
        // боссы и скачки
        foreach (var ps in s.Profiles.Where(p => p.Profile == "average" || p.Profile == "expert"))
        {
            for (int i = 0; i < 5; i++)
            {
                if (double.IsNaN(ps.BossAttempts[i])) continue;
                if (ps.BossAttempts[i] > tg.BossAttemptsMax) F("warn", "boss", $"{ps.Profile}: босс {i + 1} требует в среднем {ps.BossAttempts[i]:F1} попыток (> {tg.BossAttemptsMax}): резкий скачок сложности", ps.BossAttempts[i], $"уменьшить BossHp[{i}]/BossDamage[{i}]");
                if (ps.BossFirstTry[i] > tg.BossFirstTryMax && i > 0) F("info", "boss", $"{ps.Profile}: босс {i + 1} побеждается с первой попытки в {ps.BossFirstTry[i]:P0} случаев (> {tg.BossFirstTryMax:P0}): угрозы почти нет", ps.BossFirstTry[i]);
                if (i > 0 && !double.IsNaN(ps.BossAttempts[i - 1]) && ps.BossAttempts[i] > 3 * Math.Max(1, ps.BossAttempts[i - 1]) && ps.BossAttempts[i] > 2) F("warn", "boss", $"{ps.Profile}: сложность босса {i + 1} ({ps.BossAttempts[i]:F1} попыток) втрое выше босса {i} ({ps.BossAttempts[i - 1]:F1})", ps.BossAttempts[i]);
            }
            if (ps.MilestoneN[Ms.Boss1] > 5 && ps.MilestoneMedian[Ms.Boss1] * 60 < tg.FirstBossMinMinutes) F("warn", "early-game", $"{ps.Profile}: первый босс убит на {ps.MilestoneMedian[Ms.Boss1] * 60:F0}-й минуте (< {tg.FirstBossMinMinutes} мин): слишком лёгкое начало", ps.MilestoneMedian[Ms.Boss1] * 60);
        }
        // враги
        var good = s.Enemies.Where(e => e.Spawned > 2000).ToList(); if (good.Count > 3)
        {
            double med = Mx.Median(good.Select(e => e.DeathsPerThousand).ToList());
            foreach (var e in good) { if (e.DeathsPerThousand > med * tg.EnemyOutlier && e.Deaths > 20) F("warn", "enemy", $"враг «{e.Enemy}»: {e.DeathsPerThousand:F2} смертей на 1000 появлений (медиана {med:F2}, ×{e.DeathsPerThousand / med:F1}) — слишком опасен", e.DeathsPerThousand, $"снизить enemy.{e.Enemy}.Dmg"); if (e.DeathsPerThousand < med / tg.EnemyOutlier && med > 0.05) F("info", "enemy", $"враг «{e.Enemy}»: почти не убивает ({e.DeathsPerThousand:F3} на 1000, медиана {med:F2}) — слабый", e.DeathsPerThousand, $"поднять enemy.{e.Enemy}.Dmg/Hp"); }
        }
        // ресурсы
        foreach (var it in s.Items)
        {
            int idx = Array.IndexOf(names(), it.Item); if (idx < 0) continue; if (it.Gathered < 20) continue;
            if (it.PercentLeft > tg.SurplusMax) F("info", "economy", $"{it.Item}: к концу остаётся {it.PercentLeft:P0} добытого ({it.EndInv:F0} шт.) — избыток", it.PercentLeft, "уменьшить выход или повысить цены");
        }
    }
    static string[] names() => (string[])typeof(It).GetField("Names").GetValue(null);
}

/// <summary>Целевые показатели. Предложены как разумные по умолчанию и требуют согласования; файл balance/targets.json правится вручную.</summary>
public sealed class Targets
{
    public sealed class PT { public double WinMin, WinMax, HoursMin, HoursMax, DeathsPerHourMax, QuitMax = .05; public Dictionary<string, double[]> Milestones = new(); }
    public Dictionary<string, PT> Profiles = new()
    {
        // Опорные точки темпа — из PLAYTEST.md: к 1 ч первый страж, к 3 ч не менее 2 открытых биомов (маяк ≥ 3), 5–10 ч у увлечённых игроков. Итоговое время — предложение, нужно согласовать.
        ["novice"] = new PT { WinMin = .45, WinMax = .85, HoursMin = 10, HoursMax = 30, DeathsPerHourMax = 12, QuitMax = .25, Milestones = new() { ["boss1"] = new[] { .6, 2.5 }, ["beacon3"] = new[] { 2.5, 7 } } },
        ["average"] = new PT { WinMin = .75, WinMax = .96, HoursMin = 6, HoursMax = 16, DeathsPerHourMax = 6, QuitMax = .10, Milestones = new() { ["boss1"] = new[] { .5, 1.25 }, ["beacon3"] = new[] { 1.5, 3.6 } } },
        ["expert"] = new PT { WinMin = .9, WinMax = 1.0, HoursMin = 4, HoursMax = 10, DeathsPerHourMax = 4, QuitMax = .05, Milestones = new() { ["boss1"] = new[] { .4, 1.0 }, ["beacon3"] = new[] { 1.2, 3.0 } } },
        ["optimizer"] = new PT { WinMin = .92, WinMax = 1.0, HoursMin = 3, HoursMax = 9, DeathsPerHourMax = 4, QuitMax = .05, Milestones = new() { ["boss1"] = new[] { .3, 1.0 } } },
    };
    public double NoviceExpertMin = 1.4, NoviceExpertMax = 4.0, OptimizerSpeedMin = .65, BuildSpreadMax = .10, BuildTimeRatioMax = 1.35;
    public double BossAttemptsMax = 3.5, BossFirstTryMax = .85, FirstBossMinMinutes = 25, StuckMax = .03, SurplusMax = .6, EnemyOutlier = 3.0;
    public int MinSims = 300, MinPerArch = 40;
    public double SkillMinGainPct = .4, SkillEffRatioMax = 4.0, SkillEffRatioMin = .15;
    /// <summary>Веса целей в общей функции оптимизации.</summary>
    public double WDifficulty = 1.0, WPace = 1.0, WSkillGap = .6, WBuilds = .8, WEconomy = .4, WBoss = .6, WSkills = .5;
}
