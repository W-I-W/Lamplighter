using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Lamplighter.Balance;

public static partial class BalanceCli
{
    static int CheckSuite(Args a)
    {
        var m = LoadModel(a);
        string name = a.S("name", "check"), dir = Path.Combine(a.S("out") ?? OutRoot, name);
        Directory.CreateDirectory(dir);
        var errors = ModelHealth.Errors(m);
        var contract = ModelHealth.ContractErrors(a.S("contract-log"));
        bool verified = contract.Count == 0;
        if (a.S("contract-log") != null) errors.AddRange(contract);
        if (errors.Count == 0 && Validate.Run(a.I("validation-n", 12), a.I("threads", 0)) != 0) errors.Add("Не прошли проверки модели: подробности в журнале команды.");
        string path = Path.Combine(dir, "quality.json");
        if (errors.Count > 0)
        {
            File.WriteAllText(path, JsonSerializer.Serialize(new { schema_version = 1, status = "model_error", errors }, Js));
            foreach (string e in errors) Say("MODEL ERROR: " + e);
            Say("RESULT " + JsonSerializer.Serialize(new { status = "model_error", quality = path }, Js)); return 2;
        }
        int code = Simulate(a, true); if (code != 0) return code;
        var summary = JsonSerializer.Deserialize<Summary>(File.ReadAllText(Path.Combine(dir, name + ".summary.json")), Js);
        var findings = summary.Findings.Where(f => f.Severity != "info").ToArray();
        string status = !verified ? "unverified" : findings.Length > 0 ? "balance_review" : "ready";
        var limits = new[] {
            "Модель одиночной игры: без полной геометрии мира и арен, кооператива и подбора физической добычи.",
            "Вероятность попадания дополнительных корней — явное допущение FollowupHitShare, а не физическая симуляция.",
            "Лимит подготовки бота не равен проигрышу в бою; время победы считается только среди завершивших.",
            "Реликвии с пространственными эффектами и hit-stop воспроизводятся приближённо; нужен живой плейтест." };
        File.WriteAllText(path, JsonSerializer.Serialize(new {
            schema_version = 1, status, runtime_verified = verified,
            source_sha256 = ModelHealth.SourceHash(), assembly_sha256 = ModelHealth.AssemblyHash(), model_sha256 = ModelHealth.ModelHash(m),
            seed = summary.Seed, summary.TotalRuns, summary.Threads,
            profiles = summary.Profiles.Select(p => new { p.Profile, p.N, p.Wins, p.WinRate, p.WinLo, p.WinHi, p.LowConfidence, p.PreparationStops, p.AttemptStops,
                median_hours = p.Wins > 0 ? (double?)p.HoursMedian : null, p.DeathsMean }),
            findings, limitations = limits, verification_notes = contract
        }, Js));
        var md = new StringBuilder("# Проверка баланса\n\n");
        md.AppendLine($"Статус: **{status}**. Сверка с настоящим Game: **{(verified ? "пройдена" : "не выполнялась")}**. Прогонов: {summary.TotalRuns}.\n");
        md.AppendLine("| Профиль | Прошли / N | Остановились на подготовке | Лимит попыток боя | Время победы, ч | Смертей, среднее |\n|---|---:|---:|---:|---:|---:|");
        foreach (var p in summary.Profiles) md.AppendLine($"| {p.Profile} | {p.Wins}/{p.N} | {p.PreparationStops} | {p.AttemptStops} | {(p.Wins > 0 ? Report.F(p.HoursMedian) : "нет побед")} | {Report.F(p.DeathsMean)} |");
        md.AppendLine("\nПодробные сценарии, доверительные интервалы, оружие и навыки — в " + name + ".md.\n");
        md.AppendLine("## Границы модели\n"); foreach (string limit in limits) md.AppendLine("- " + limit);
        File.WriteAllText(Path.Combine(dir, "CHECK.md"), md.ToString());
        Say("RESULT " + JsonSerializer.Serialize(new { status, runtime_verified = verified, quality = path, report = Path.Combine(dir, "CHECK.md"), findings = findings.Length }));
        return a.F("strict") ? !verified ? 2 : findings.Length > 0 ? 1 : 0 : 0;
    }
}
