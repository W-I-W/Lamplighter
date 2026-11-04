using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Фаза босса — данные конкретного босса, а не интерфейса: id, название, порог (доля здоровья, с которой фаза начинается; у первой — 1),
/// набор атак этой фазы и параметры (темп — множитель пауз между атаками, урон — множитель урона, цвет вспышки на шкале).</summary>
public sealed class BossPhaseData
{
    public string Id = "", Name = ""; public float Threshold = 1f; public string[] Attacks = Array.Empty<string>();
    public float Tempo = 1f, Damage = 1f; public Color Tint = new("#d58cff");
    public bool Has(string attack) => Array.IndexOf(Attacks, attack) >= 0;
}

/// <summary>Справочник фаз боссов. Пороги совпадают с реальными переходами в их ИИ (ИИ читает номер фазы у контроллера).</summary>
public static class BossPhaseBook
{
    static BossPhaseData P(string id, string name, float th, float tempo, string tint, params string[] atk) => new() { Id = id, Name = name, Threshold = th, Tempo = tempo, Tint = new Color(tint), Attacks = atk };

    static readonly Dictionary<EK, BossPhaseData[]> book = new()
    {
        // Пожиратель Рощи: 100–70 / 70–35 / 35–0
        [EK.Warden] = new[] {
            P("awaken", "Пробуждение", 1f, 1f, "#9be07a", "root", "branch", "thorns"),
            P("absorb", "Поглощение", .70f, .85f, "#c8e04a", "root", "branch", "thorns", "spores", "absorb"),
            P("heart", "Сердце Тьмы", .35f, .62f, "#b04aff", "rootseries", "branch", "thorns", "spores", "hunger") },
        [EK.DepthWarden] = new[] {
            P("tide", "Прилив", 1f, 1f, "#7ac8ff", "dive", "tide"),
            P("dark", "Тёмные воды", .66f, 1f, "#5a8cff", "dive", "tide", "summon", "darkness", "whirlpool"),
            P("abyss", "Бездна", .33f, .6f, "#3a5aff", "dive", "tide", "summon", "darkness", "whirlpool") },
        [EK.AshWarden] = new[] {
            P("smolder", "Тление", 1f, 1f, "#ffb36a", "pillars", "stomp"),
            P("flame", "Пламя", .66f, 1f, "#ff8a3a", "pillars", "stomp", "smoke", "summon", "overheat"),
            P("rage", "Ярость", .33f, .6f, "#ff4a2a", "charge", "smoke", "pillars", "stomp", "overheat", "firetrail") },
        [EK.WinterWarden] = new[] {
            P("frost", "Стужа", 1f, 1f, "#bfe9ff", "iceblocks", "summon", "shards"),
            P("mirror", "Зеркальный щит", .66f, 1f, "#9fe8ff", "iceblocks", "freeze", "summon", "shards", "blizzard", "prismshield"),
            P("storm", "Метель", .33f, .65f, "#e8faff", "iceblocks", "freeze", "summon", "shards", "blizzard", "prismshield") },
        [EK.Source] = new[] {
            P("dark", "Тьма", 1f, 1f, "#d58cff", "drain", "volley", "summon", "darkzones"),
            P("distort", "Искажение", .66f, .85f, "#a060ff", "void", "falselights", "firefrost", "volley", "summon", "echo", "pylonshield"),
            P("lastlight", "Последний свет", .33f, .62f, "#ffe27a", "void", "falselights", "firefrost", "drain", "volley", "summon", "echo", "pylonshield") },
    };

    static readonly BossPhaseData[] single = { P("one", "Бой", 1f, 1f, "#d58cff") };

    public static BossPhaseData[] For(EK k) => book.TryGetValue(k, out var p) ? p : single;

    public static string Roman(int n) => n switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", 6 => "VI", 7 => "VII", 8 => "VIII", _ => n.ToString() };
}

/// <summary>Ведёт фазу босса по его здоровью. Правила: каждый переход случается ровно один раз; за один вызов Update — не больше одного шага, поэтому даже огромный урон
/// не пропускает фазы (каждая проходит свой переход по очереди); лечение не возвращает в прошлую фазу (индекс только растёт); FloorFrac — ниже этой доли удар не опустит
/// здоровье, пока не случился переход, чтобы следующая фаза не была проскочена целиком.</summary>
public sealed class BossPhaseController
{
    public readonly BossPhaseData[] Phases;
    public int Index { get; private set; }
    public int Transitions { get; private set; }                // сколько переходов уже было (для проверки «ровно один раз»)
    public float ChangedAt = -99f;                               // время последнего перехода (Game.Time) — вспышка и подпись на шкале

    public BossPhaseController(BossPhaseData[] phases)
    {
        if (phases == null || phases.Length == 0) phases = BossPhaseBook.For((EK)(-1));
        for (int i = 1; i < phases.Length; i++) if (phases[i].Threshold >= phases[i - 1].Threshold) throw new ArgumentException("boss phase thresholds must decrease");
        Phases = phases;
    }

    public int Count => Phases.Length;
    public int Number => Index + 1;
    public BossPhaseData Current => Phases[Index];
    public BossPhaseData Next => Index + 1 < Phases.Length ? Phases[Index + 1] : null;

    /// <summary>Шаг по здоровью (доля 0..1). true — фаза сменилась (ровно на одну).</summary>
    public bool Update(float hpFrac)
    {
        if (Index + 1 >= Phases.Length || !(hpFrac <= Phases[Index + 1].Threshold)) return false;
        Index++; Transitions++; return true;
    }

    /// <summary>Фаза, присланная хостом: принимается только вперёд и тоже по одному шагу за вызов (true — сменилась).</summary>
    public bool ApplyNet(int idx)
    {
        if (idx <= Index || Index + 1 >= Phases.Length) return false;
        Index++; Transitions++; return true;
    }

    /// <summary>Нижняя граница здоровья для удара: пока переход не случился, здоровье не опускается ниже порога следующей фазы (лишний урон теряется) — так ни одна фаза не проскакивает.</summary>
    public float FloorFrac => Index + 1 < Phases.Length ? Phases[Index + 1].Threshold : 0f;
}
