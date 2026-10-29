using System;
using System.Collections.Generic;

namespace Lamplighter.Balance;

public enum SkillPolicy { Random, Cheapest, Archetype, Greedy }

/// <summary>Сборка, к которой привязывается опытный игрок: порядок навыков, класс оружия, вставки, теги реликвий.</summary>
public sealed class Archetype
{
    public string Name; public WClass Weapon; public Tech[] Skills; public int[] Mods; public string[] Tags;
}

/// <summary>Профиль виртуального игрока. Отличия — в поведении (реакция, уклонение, выбор навыков, отдых, готовность к боссам), а не в множителях урона и здоровья.</summary>
public sealed class Profile
{
    public string Name;
    // бой
    public float ReactMean, ReactSd;       // время реакции на предупреждение врага, с
    public float DodgeSkill;               // вероятность удачно уклониться, если предупреждение замечено вовремя
    public float Uptime;                   // доля времени, когда удар/выстрел попадает в цель (позиция, прицел)
    public float Flank;                    // шанс зайти жуку/стражу со спины
    public bool UseDash, UseSpin, UseFlare, UsePulse;
    public float DashSkill;                // шанс, что рывок сработает в окно неуязвимости (0,2 с)
    // выживание
    public float RestBelow;                // отдыхать в свете, если здоровье ниже доли
    public float BossRisk;                 // допустимое отношение ожидаемого урона к запасу здоровья перед боссом
    public bool SimulatedReadiness;        // оценивает готовность пробными боями
    // экономика
    public float GatherWaste;              // множитель времени дороги при добыче
    public float WasteStock;               // сколько лишнего сырья копит сверх нужного (доля)
    public bool UsesAxe;
    public float ExploreShare;             // доля времени на исследование мест (чертежи, реликвии)
    public int[] LampsWanted;              // сколько фонарей зажигает по регионам
    public bool LightBossLamps;
    // прокачка
    public SkillPolicy Skills; public bool PicksArchetype; public bool BuysMods; public bool OptimizesRelics;
    public float SpendDelay;               // доля покупок, которые откладываются (невнимательность)
    public float PanicDeaths;              // после стольких смертей подряд на одном этапе идёт фармить опыт
    public int QuitDeaths;                 // сколько смертей на одном этапе игрок терпит, прежде чем бросить игру
    public string[] ForcedArchetype;       // при непустом наборе выбирает только из них

    public static Profile Novice() => new()
    {
        Name = "novice", ReactMean = .62f, ReactSd = .18f, DodgeSkill = .28f, Uptime = .55f, Flank = .05f, UseDash = false, UseSpin = false, UseFlare = false, UsePulse = false, DashSkill = .15f,
        RestBelow = .15f, BossRisk = 2.4f, QuitDeaths = 60, GatherWaste = 1.6f, WasteStock = .6f, UsesAxe = false, ExploreShare = .08f, LampsWanted = new[] { 5, 3, 3, 3, 0 }, LightBossLamps = false,
        Skills = SkillPolicy.Random, PicksArchetype = false, BuysMods = false, OptimizesRelics = false, SpendDelay = .3f, PanicDeaths = 4
    };

    public static Profile Average() => new()
    {
        Name = "average", ReactMean = .38f, ReactSd = .10f, DodgeSkill = .48f, Uptime = .72f, Flank = .25f, UseDash = true, UseSpin = true, UseFlare = false, UsePulse = true, DashSkill = .4f,
        RestBelow = .4f, BossRisk = 1.3f, QuitDeaths = 120, GatherWaste = 1.2f, WasteStock = .3f, UsesAxe = true, ExploreShare = .15f, LampsWanted = new[] { 5, 3, 3, 3, 0 }, LightBossLamps = true,
        Skills = SkillPolicy.Cheapest, PicksArchetype = false, BuysMods = true, OptimizesRelics = false, SpendDelay = .1f, PanicDeaths = 3
    };

    public static Profile Expert() => new()
    {
        Name = "expert", ReactMean = .26f, ReactSd = .06f, DodgeSkill = .64f, Uptime = .85f, Flank = .6f, UseDash = true, UseSpin = true, UseFlare = true, UsePulse = true, DashSkill = .6f,
        RestBelow = .55f, BossRisk = 1.05f, QuitDeaths = 200, GatherWaste = 1.0f, WasteStock = .1f, UsesAxe = true, ExploreShare = .2f, LampsWanted = new[] { 3, 2, 2, 2, 0 }, LightBossLamps = true,
        Skills = SkillPolicy.Archetype, PicksArchetype = true, BuysMods = true, OptimizesRelics = true, SpendDelay = 0f, PanicDeaths = 2
    };

    public static Profile Optimizer() => new()
    {
        Name = "optimizer", ReactMean = .24f, ReactSd = .05f, DodgeSkill = .68f, Uptime = .85f, Flank = .6f, UseDash = true, UseSpin = true, UseFlare = true, UsePulse = true, DashSkill = .65f,
        RestBelow = .6f, BossRisk = 1.05f, QuitDeaths = 300, GatherWaste = .85f, WasteStock = 0f, UsesAxe = true, ExploreShare = .2f, LampsWanted = new[] { 2, 1, 1, 1, 0 }, LightBossLamps = false,
        Skills = SkillPolicy.Greedy, PicksArchetype = false, BuysMods = true, OptimizesRelics = true, SimulatedReadiness = true, SpendDelay = 0f, PanicDeaths = 2
    };

    /// <summary>Калибровка профилей из balance/profiles.json: {"expert": {"DodgeSkill": 0.6}} — без перекомпиляции. Основная неопределённость модели — именно эти числа.</summary>
    public static Dictionary<string, Dictionary<string, double>> Calibration = new();
    public static Profile ByName(string n) { var p = ByName0(n); if (Calibration.TryGetValue(p.Name, out var ov)) foreach (var kv in ov) { var f = typeof(Profile).GetField(kv.Key); if (f == null) continue; if (f.FieldType == typeof(float)) f.SetValue(p, (float)kv.Value); else if (f.FieldType == typeof(int)) f.SetValue(p, (int)kv.Value); else if (f.FieldType == typeof(bool)) f.SetValue(p, kv.Value != 0); } return p; }
    static Profile ByName0(string n) => n.ToLowerInvariant() switch
    {
        "novice" or "newbie" => Novice(), "average" or "mid" => Average(), "expert" => Expert(), "optimizer" or "opt" => Optimizer(),
        "probe-nolamps" => With(Optimizer(), "probe-nolamps", q => q.LampsWanted = new[] { 0, 0, 0, 0, 0 }),
        "probe-maxlamps" => With(Expert(), "probe-maxlamps", q => q.LampsWanted = new[] { 5, 3, 3, 3, 0 }),
        "probe-rush" => With(Optimizer(), "probe-rush", q => { q.BossRisk = 99f; q.SimulatedReadiness = false; q.QuitDeaths = 100000; q.RestBelow = 0f; q.PanicDeaths = 100000; }),
        _ => throw new ArgumentException("unknown profile " + n)
    };
    static Profile With(Profile p, string name, Action<Profile> f) { p.Name = name; f(p); return p; }
    /// <summary>Стратегии для поиска эксплойтов: без фонарей (враги слабее), все фонари, «нахрапом» без подготовки и отдыха (смерть почти бесплатна).</summary>
    public static readonly string[] Probes = { "probe-nolamps", "probe-maxlamps", "probe-rush" };
    public static readonly string[] All = { "novice", "average", "expert", "optimizer" };

    public static readonly Archetype[] Archetypes =
    {
        new() { Name = "blade-light", Weapon = WClass.Sword, Tags = new[] { "light" }, Mods = new[] { 5, 3 },
            Skills = new[] { Tech.Lens, Tech.Dash, Tech.Reaper, Tech.Spin, Tech.HeroLamp, Tech.LensII, Tech.Searing, Tech.SilverEdge, Tech.Flare, Tech.FarLanterns } },
        new() { Name = "fire-sword", Weapon = WClass.Sword, Tags = new[] { "fire" }, Mods = new[] { 0, 6, 3 },
            Skills = new[] { Tech.Dash, Tech.Reaper, Tech.Spin, Tech.FlameEdge, Tech.HeatShield, Tech.Searing, Tech.Lens, Tech.FireLens, Tech.SilverEdge } },
        new() { Name = "frost-sword", Weapon = WClass.Sword, Tags = new[] { "ice" }, Mods = new[] { 1, 3 },
            Skills = new[] { Tech.Dash, Tech.Reaper, Tech.Spin, Tech.FrostEdge, Tech.FrostArmor, Tech.Lens, Tech.SilverEdge, Tech.HeatShield } },
        new() { Name = "storm-sword", Weapon = WClass.Sword, Tags = new[] { "storm" }, Mods = new[] { 2, 3, 5 },
            Skills = new[] { Tech.Dash, Tech.Reaper, Tech.Spin, Tech.Lens, Tech.SilverEdge, Tech.HeatShield, Tech.FrostArmor } },
        new() { Name = "archer", Weapon = WClass.Bow, Tags = new[] { "storm", "ice" }, Mods = new[] { 3, 4, 2 },
            Skills = new[] { Tech.Marksman, Tech.Dash, Tech.QuickDraw, Tech.Piercing, Tech.Overcharge, Tech.Reaper, Tech.Lens, Tech.HeatShield } },
        new() { Name = "mage-staff", Weapon = WClass.Staff, Tags = new[] { "fire", "ice" }, Mods = new[] { 6, 0, 1 },
            Skills = new[] { Tech.Marksman, Tech.Dash, Tech.QuickDraw, Tech.Overcharge, Tech.Piercing, Tech.Reaper, Tech.Lens, Tech.HeatShield, Tech.FrostArmor } },
    };
}
