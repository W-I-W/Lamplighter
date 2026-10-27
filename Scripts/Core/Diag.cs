using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Lamplighter;

/// <summary>Локальная диагностика (выключена по умолчанию; включается в настройках или LAMP_DIAG=1).
/// Ничего не отправляет в сеть: пишет user://diagnostics.json (или в папку теста). Это технические метрики, а не плейтест.</summary>
public static class Diag
{
    public static bool Enabled;
    static readonly Dictionary<string, float> firsts = new();
    static readonly Dictionary<string, float> sums = new();
    static readonly Dictionary<string, int> counts = new();
    static float lastLog, t, sampleT, writeT, fpsMin = 999, fpsSum; static int fpsN, peakNodes, peakChunks; static double peakMemMb;
    public static string OutPath = "user://diagnostics.json";

    public static void Reset() { firsts.Clear(); sums.Clear(); counts.Clear(); t = 0; sampleT = 0; writeT = 0; fpsMin = 999; fpsSum = 0; fpsN = 0; peakNodes = 0; peakChunks = 0; peakMemMb = 0; }
    public static void Mark(string name) { if (Enabled && !firsts.ContainsKey(name)) firsts[name] = t; }
    public static void Add(string name, float v) { if (Enabled) sums[name] = sums.GetValueOrDefault(name) + v; }
    public static void Count(string name) { if (Enabled) counts[name] = counts.GetValueOrDefault(name) + 1; }

    public static void Update(float dt, Game g)
    {
        if (!Enabled) return;
        t += dt; sampleT -= dt; writeT -= dt;
        if (sampleT <= 0)
        {
            sampleT = 2f; float fps = (float)Engine.GetFramesPerSecond();
            if (fps > 1) { fpsMin = Math.Min(fpsMin, fps); fpsSum += fps; fpsN++; }
            peakNodes = Math.Max(peakNodes, (int)Performance.GetMonitor(Performance.Monitor.ObjectNodeCount));
            peakChunks = Math.Max(peakChunks, g.LoadedChunks);
            peakMemMb = Math.Max(peakMemMb, Performance.GetMonitor(Performance.Monitor.MemoryStatic) / 1048576.0);
        }
        if (writeT <= 0) { writeT = 30f; Write(g); }
    }

    public static void Write(Game g)
    {
        if (!Enabled) return;
        try
        {
            var inv = new Dictionary<string, int>(); for (int i = 0; i < It.Count; i++) if (g.Inv[i] > 0) inv[It.Names[i]] = g.Inv[i];
            var o = new
            {
                session_secs = t, first_events_secs = firsts, totals = sums, counts,
                territories_lit = g.TotalLit, level = g.Level, beacon = g.BeaconLevel, inventory = inv,
                fps_avg = fpsN > 0 ? fpsSum / fpsN : 0, fps_min = fpsMin, peak_nodes = peakNodes, peak_chunks = peakChunks, loaded_chunks = g.LoadedChunks, peak_static_mem_mb = peakMemMb
            };
            using var f = FileAccess.Open(OutPath, FileAccess.ModeFlags.Write);
            f?.StoreString(JsonSerializer.Serialize(o, new JsonSerializerOptions { WriteIndented = true }));
            // журнал по сессиям (одна строка в минуту ~ раз в 60 с), чтобы сравнивать 15 минут / час / 3 часа / 5-10 часов
            if (t - lastLog >= 60f || lastLog == 0)
            {
                lastLog = t == 0 ? 0.001f : t;
                var logPath = OutPath.Replace("diagnostics.json", "diagnostics_log.jsonl");
                using var lf = FileAccess.Open(logPath, FileAccess.FileExists(logPath) ? FileAccess.ModeFlags.ReadWrite : FileAccess.ModeFlags.Write);
                if (lf != null) { lf.SeekEnd(); lf.StoreLine(JsonSerializer.Serialize(new { at = DateTime.Now.ToString("s"), session_secs = t, play_secs = g.PlaySecs, level = g.Level, beacon = g.BeaconLevel, lit = g.TotalLit, tier = g.Tier, buildings = g.Buildings.Count, villagers = g.Villagers.Count, relics = g.OwnedRelics.Count, bosses = g.BossCount, mine_secs = sums.GetValueOrDefault("mine_secs"), dark_secs = sums.GetValueOrDefault("dark_secs"), deaths = counts.GetValueOrDefault("deaths"), fast_travel = counts.GetValueOrDefault("fast_travel"), fps_min = fpsMin })); }
            }
        }
        catch { }
    }
}
