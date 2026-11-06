using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Godot;

namespace Lamplighter.Balance;

/// <summary>Проверяет корректность входных данных, не объявляя трудную игру ошибкой симулятора.</summary>
public static class ModelHealth
{
    public static string SourceHash()
    {
        string root = ProjectSettings.GlobalizePath("res://");
        var files = Directory.GetFiles(Path.Combine(root, "Scripts"), "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(Path.Combine(root, "Resources"), "*.tres", SearchOption.AllDirectories)).OrderBy(f => f, StringComparer.Ordinal);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string file in files)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(Path.GetRelativePath(root, file).Replace('\\', '/')));
            hash.AppendData(new byte[] { 0 }); hash.AppendData(File.ReadAllBytes(file)); hash.AppendData(new byte[] { 0 });
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }
    public static string ModelHash(BalanceModel m) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(m.ToJson()))).ToLowerInvariant();
    // Godot loads the game assembly from bytes; Assembly.Location can be empty.
    public static string AssemblyHash() => Convert.ToHexString(SHA256.HashData(typeof(Game).Module.ModuleVersionId.ToByteArray())).ToLowerInvariant();

    public static List<string> Errors(BalanceModel m)
    {
        var errors = new List<string>();
        void Need(bool ok, string what) { if (!ok) errors.Add(what); }
        bool Positive(float n) => float.IsFinite(n) && n > 0;
        foreach (var field in typeof(TuneM).GetFields())
        {
            if (field.FieldType == typeof(float)) Need(float.IsFinite((float)field.GetValue(m.T)), "Неконечный параметр tuning." + field.Name);
            if (field.FieldType == typeof(float[])) Need(field.GetValue(m.T) is float[] values && values.All(float.IsFinite), "Некорректный массив tuning." + field.Name);
        }
        Need(m.SchemaVersion == 2, "Несовместимая версия снимка модели; пересоздайте model.json текущей игрой.");
        Need(m.Bosses.Length == 5 && m.Enemies.Length > 0 && m.Weapons.Length > 0, "Неполные таблицы игры.");
        Need(Positive(m.T.SpawnDelayMul) && Positive(m.T.EnemyHp) && Positive(m.T.EnemyDamage) && Positive(m.T.BossTempo), "Боевые множители должны быть конечными и положительными.");
        Need(m.T.BossHp?.Length == 5 && m.T.BossHp.All(Positive), "Здоровье пяти боссов должно быть положительным.");
        Need(m.T.BossDamage?.Length == 5 && m.T.BossDamage.All(Positive), "Урон пяти боссов должен быть положительным.");
        Need(m.T.EncounterBase?.Length == 5 && m.T.EncounterMax?.Length == 5 && m.T.EncounterBase.All(Positive) && m.T.EncounterMax.All(Positive), "Бюджеты столкновений нужны для пяти регионов.");
        Need(Positive(m.T.BossWarnMul) && m.T.MaxRangedNear >= 0 && Positive(m.A.MaxHours), "Некорректные предупреждения, лимит стрелков или предел времени.");
        Need(m.Weapons.All(w => Positive(w.Cd) && Positive(w.Dmg)), "Урон/пауза оружия должны быть положительными.");
        Need(m.Bosses.SelectMany(b => b.Moves).All(a => a.Windup >= 0 && float.IsFinite(a.Windup) && a.Gap >= 0 && a.FollowupHitShare >= 0 && a.FollowupHitShare <= 1 && (a.Followups == null || a.Followups.All(v => Positive(v)))), "Некорректная атака босса.");
        return errors;
    }

    public static List<string> ContractErrors(string path)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) { errors.Add("Нет свежей сверки с Game: используйте tools/balance.ps1 check."); return errors; }
        string log = File.ReadAllText(path);
        if (!log.Contains("MODEL CONTRACT DONE") || !log.Contains("SUMMARY failures=0") || log.Contains("[check] FAIL") || log.Contains("EXCEPTION")) errors.Add("Сверка модели с настоящим Game не прошла.");
        if (!log.Contains("source_sha256=" + SourceHash()) || !log.Contains("assembly_sha256=" + AssemblyHash())) errors.Add("Исходники или сборка изменились после сверки с Game.");
        return errors;
    }
}
