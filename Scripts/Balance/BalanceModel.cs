using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace Lamplighter.Balance;

/// <summary>Числа боевой настройки: копия полей CombatTuning (Resources/Combat/tuning.tres) в обычном классе без Godot.</summary>
public sealed class TuneM
{
    public float EnemyDamage, EnemyHp, EnemySpeed, EnemyAttackRate, SpawnCapBonus, MaxAttackers, ShadeWindup;
    public float[] BiomeDamage, BiomeHp, EliteChance, BossHp, BossDamage;
    public float EliteMinLit, EliteMax, EliteHp, EliteDamage, BossTempo, HitInvuln, RegenRate, RegenDelay, DashCooldown, DashInvuln, ReaperHeal, FearLevel = 5, FearDamage, FearSpawn;
    public float GatherMul, MineTimeMul, ScarcityMul, TechCostMul, WeaponMineMul, XpMul, XpBase, XpLinear, XpQuad;
    public float At(float[] a, int i) => a == null || a.Length == 0 ? 1f : a[Math.Clamp(i, 0, a.Length - 1)];
}

public sealed class SkillM
{
    public string Id, Name; public int Idx, Page, Max; public bool Reward;
    public float[] Curve; public int[][] BaseCost;            // BaseCost уже умножена на TechCostMul, как в игре (TechDef.Cost)
    public int[] ParentTech, ParentMin;
    public string Unit; public float EffBase, EffPer;
    public float Eff(int lv) => lv <= 0 && Unit != "abs" ? 0f : EffBase + EffPer * lv;
    /// <summary>Цена уровня как в Game.LevelCost.</summary>
    public int[][] LevelCost(int level, float costMul = 1f)
    {
        float k = Curve != null && level >= 1 && level <= Curve.Length ? Curve[level - 1] : 1f; k *= costMul;
        if (k <= 0 || BaseCost == null) return Array.Empty<int[]>();
        var r = new int[BaseCost.Length][];
        for (int n = 0; n < r.Length; n++) r[n] = new[] { BaseCost[n][0], BaseCost[n][1] <= 0 ? 0 : Math.Max(1, (int)Math.Ceiling(BaseCost[n][1] * k)) };
        return r;
    }
}

public sealed class WeaponM
{
    public string Id, Name, Bp; public int Cls, Tier, Slots, Pierce; public float Dmg, Cd, Reach, Speed; public bool Unique;
    public string[] FxK; public float[] FxP; public string[] Tags; public int[][] Cost;
}

public sealed class ModM { public int Id; public string Name, Tag, Bp; public string[] FxK; public float[] FxP; public int[][] Cost; }
public sealed class RelicM { public string Id; public string[] Tags, Effects; public int Rarity; public bool Permanent; }
public sealed class SynM { public string Id; public string[] Requires, Grants; }

/// <summary>Враг обычного вида: числа берутся из кода игры (Game.Combat/Ash/Ice/Heart), помечено, откуда.</summary>
public sealed class EnemyM
{
    public string Kind; public int Region;
    public float Hp;                 // Game.BaseHp (читается через reflection, не копируется вручную)
    public float Dmg;                // урон одного удара до множителей (HurtPlayer(N * e.Scale))
    public float Period;             // средний интервал атак в бою, сек (по таймерам e.Cd / e.T2 / e.Atk)
    public float Windup;             // предупреждение до удара, сек (e.Tele)
    public float Speed;              // скорость сближения, пикс/с
    public bool Ranged, Fire, Cold, Soul, Armored, Explodes;
    public int Group = 1;            // число появляющихся сразу (рой — 4)
    public float Evasive;            // доля времени, когда враг вне досягаемости ближнего удара (отступает, держит дистанцию)
    public float Xp, ShardChance;    // Game.KillEnemy
    public float Weight;             // вес в PickKind
    public float ExtraDrop;          // второй вид добычи (шанс)
    public string ExtraItem;
    public string Src;               // где в коде
}

/// <summary>Действие босса. Цикловое (Weight ≠ 0): после паузы Gap босс выбирает действие по весам фазы; периодическое (Cooldown &gt; 0): отдельная перезарядка (кольцо снарядов, уникальная способность).</summary>
public sealed class BossMoveM
{
    public string Name; public float Dmg, Windup, Gap, Post, Open, Hide, Ring, Cooldown, PhaseBelowHp = 1f; public bool Fire, Cold, Ranged;
    public float W0, W1, W2; public string[] Summon; public float SummonDanger = 1.2f;
}
public sealed class BossM
{
    public int Id; public string Kind; public float Hp, MeleeDmg, MeleePeriod = 1f, RageMul = .65f; public int Region, ShieldFromPhase = 99; public float VulnSeconds;
    public bool Fire, Cold; [System.Text.Json.Serialization.JsonIgnore] public bool NeedsLight => ShieldFromPhase < 99; public BossMoveM[] Moves; public string Src;
}

/// <summary>Допущения о мире. Реальный мир (генерация, маршруты, жилы) в симуляторе не воспроизводится — вместо него здесь явные параметры. Всё перечислено в отчёте.</summary>
public sealed class AbstractM
{
    public float TravelPerTile = 3.2f;          // сек на поиск и подход к одной добываемой клетке (до умножения на профиль)
    public float DarkShare0 = .75f, DarkShare1 = .85f, DarkShare2 = .9f, DarkShare3 = .9f, DarkShare4 = 1f;     // доля добычи вне света лампы
    public float LitShareGain = .0f;            // уменьшение тёмной доли за каждый зажжённый фонарь региона
    public float LampTravel = 70f;              // сек: дорога к древнему фонарю и обратно (в тёмной зоне)
    public float GuardedLampChance = .35f;      // доля фонарей со стражами
    public float GuardCount = 3;                // число стражей у охраняемого фонаря
    public float RestTravel = 25f;              // сек до безопасного места для отдыха
    public float RespawnTravel = 45f;           // сек возвращения после смерти
    public float PoiPerMinute = .35f;           // исследованных мест в минуту исследования
    public float PoiRelicChance = .10f;         // шанс реликвии в месте
    public float PoiFragChance = .35f;          // шанс фрагмента чертежа в месте
    public float ChainMineEff = .5f;            // доля соседних клеток, реально добываемых цепным ломанием
    public float MaxHours = 60f;                // предел времени одного прохождения
    public float FarSpitterShare = .6f;         // доля времени дальше 22 клеток от центра (плевуны в лесу)
    public float WaterShare = .35f;             // доля шахт возле воды (угри)
}

public sealed class BalanceModel
{
    public TuneM T = new();
    public SkillM[] Skills = Array.Empty<SkillM>();
    public WeaponM[] Weapons = Array.Empty<WeaponM>();
    public ModM[] Mods = Array.Empty<ModM>();
    public RelicM[] Relics = Array.Empty<RelicM>();
    public SynM[] Syns = Array.Empty<SynM>();
    public EnemyM[] Enemies = Array.Empty<EnemyM>();
    public BossM[] Bosses = Array.Empty<BossM>();
    public int[][] CostFurnace, CostLantern; public int[][][] CostPick;
    public int[] LampCosts0 = { 2, 3, 4, 5, 6 }, LampCosts1 = { 6, 7, 8 }, LampCosts2 = { 9, 10, 11 }, LampCosts3 = { 12, 13, 14 };
    public int ItemCount = 21;
    public int Seed0; public float BaseTechCostMul = 2f;          // TechCostMul, уже вшитый в BaseCost навыков при снимке
    public AbstractM A = new();
    public Dictionary<string, double> Overrides = new();
    public string[] BlueprintIds = Array.Empty<string>(); public int[] BlueprintNeed = Array.Empty<int>();

    static readonly JsonSerializerOptions Js = new() { IncludeFields = true, WriteIndented = false };
    public BalanceModel Clone() => JsonSerializer.Deserialize<BalanceModel>(JsonSerializer.Serialize(this, Js), Js);
    public string ToJson(bool indent = false) => JsonSerializer.Serialize(this, new JsonSerializerOptions { IncludeFields = true, WriteIndented = indent });

    // ───────────────────────── снимок реальных данных игры ─────────────────────────
    static T Sf<T>(string name) => (T)typeof(Game).GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static).GetValue(null);

    /// <summary>Снимает данные игры (Resource, статические таблицы) в обычные классы. Вызывается в главном потоке Godot один раз.</summary>
    public static BalanceModel FromGame()
    {
        Db.Init();
        var m = new BalanceModel();
        var t = Tune.T; var tm = m.T; m.BaseTechCostMul = t.TechCostMul;
        foreach (var f in typeof(TuneM).GetFields())
        {
            var src = typeof(CombatTuning).GetField(f.Name); if (src == null) continue;
            var v = src.GetValue(t);
            if (f.FieldType == typeof(float[])) f.SetValue(tm, (float[])((float[])v).Clone());
            else f.SetValue(tm, Convert.ToSingle(v));
        }
        // навыки
        int n = Game.Techs.Length; m.Skills = new SkillM[n];
        for (int i = 0; i < n; i++)
        {
            var d = Game.SkillOf(i); var td = Game.Techs[i]; var pr = Game.ParentReqs(i);
            var bc = td.Cost;
            m.Skills[i] = new SkillM
            {
                Id = ((Tech)i).ToString(), Name = td.Name, Idx = i, Page = td.Page, Max = Math.Max(1, d.MaxLevel), Reward = td.Reward,
                Curve = (float[])(d.CostCurve ?? new[] { 1f }).Clone(), BaseCost = bc?.Select(x => new[] { x[0], x[1] }).ToArray(),
                ParentTech = pr.Select(x => x.tech).ToArray(), ParentMin = pr.Select(x => x.min).ToArray(), Unit = d.Unit ?? "", EffBase = d.EffBase, EffPer = d.EffPer
            };
        }
        m.Weapons = Db.Weapons.Select(w => new WeaponM
        {
            Id = w.Id, Name = w.Name, Bp = w.Bp, Cls = (int)w.Cls, Tier = w.Tier, Slots = w.Slots, Pierce = w.Pierce, Dmg = w.Dmg, Cd = w.Cd, Reach = w.Reach, Speed = w.Speed, Unique = w.Unique,
            FxK = w.Inherent.Select(x => x.k.ToString()).ToArray(), FxP = w.Inherent.Select(x => x.p).ToArray(), Tags = w.Tags, Cost = w.Cost?.Select(x => new[] { x[0], x[1] }).ToArray()
        }).ToArray();
        m.Mods = Db.Mods.Select(x => new ModM { Id = x.Id, Name = x.Name, Tag = x.Tag, Bp = x.Bp, FxK = x.Eff.Select(e => e.k.ToString()).ToArray(), FxP = x.Eff.Select(e => e.p).ToArray(), Cost = x.Cost.Select(c => new[] { c[0], c[1] }).ToArray() }).ToArray();
        m.Relics = Db.Relics.Select(r => new RelicM { Id = r.Id, Tags = r.Tags, Effects = r.Effects, Rarity = r.Rarity, Permanent = r.Permanent }).ToArray();
        m.Syns = Db.Synergies.Select(s => new SynM { Id = s.Id, Requires = s.Requires, Grants = s.Grants }).ToArray();
        m.BlueprintIds = Db.Bps.Select(b => b.Id).ToArray(); m.BlueprintNeed = Db.Bps.Select(b => b.Need).ToArray();
        m.CostFurnace = Sf<int[][]>("CostFurnace"); m.CostLantern = Sf<int[][]>("CostLantern"); m.CostPick = Sf<int[][][]>("CostPick");
        var bh = typeof(Game).GetMethod("BaseHp", BindingFlags.NonPublic | BindingFlags.Static);
        float Hp(EK k) => (float)bh.Invoke(null, new object[] { k });
        m.Enemies = EnemyTable(Hp); m.Bosses = BossTable(Hp);
        return m;
    }

    // ───────────────────────── таблицы врагов ─────────────────────────
    // Периоды: Shade — Tele .38 + рывок .22 + пауза .8 + Cd .5–1.1 (≈2.3 с); Wraith — Cd 2.2–3.2; Spitter — Cd 1.8–2.8 + Tele .5; Eel — Cd 1.6–2.4; Beetle — Cd 1.4–2.4 + стоп 1 с.
    static EnemyM E(string k, int reg, float hp, float dmg, float period, float windup, float speed, float xp, float shard, float w, string src, bool ranged = false, bool fire = false, bool cold = false,
        bool soul = false, int group = 1, float evasive = 0f, bool armored = false, bool expl = false, string extra = null, float extraP = 0f) =>
        new() { Kind = k, Region = reg, Hp = hp, Dmg = dmg, Period = period, Windup = windup, Speed = speed, Xp = xp, ShardChance = shard, Weight = w, Src = src, Ranged = ranged, Fire = fire, Cold = cold, Soul = soul, Group = group, Evasive = evasive, Armored = armored, Explodes = expl, ExtraItem = extra, ExtraDrop = extraP };

    static EnemyM[] EnemyTable(Func<EK, float> hp) => new[]
    {
        E("Shade", 0, hp(EK.Shade), 9, 2.3f, .38f, 36, 15, .7f, 5, "Game.Combat.cs:404", soul: true, evasive: .15f),
        E("Wraith", 0, hp(EK.Wraith), 7, 2.7f, .35f, 58, 12, .45f, 3, "Game.Combat.cs:421", soul: true, evasive: .35f),
        E("Spitter", 0, hp(EK.Spitter), 7, 2.8f, .5f, 44, 18, .6f, 2.5f, "Game.Combat.cs:435", ranged: true, soul: true, evasive: .55f),
        E("Swarm", 0, hp(EK.Swarm), 3.5f, .7f, 0f, 75, 3, .12f, 2, "Game.Combat.cs:459", soul: true, group: 4),
        E("Bomber", 0, hp(EK.Bomber), 18, 99f, 1f, 55, 16, .5f, 2, "Game.Combat.cs:463/555", expl: true),
        E("Leech", 1, hp(EK.Leech), 2.5f, 1f, 0f, 40, 14, .5f, 4, "Game.Combat.cs:489", soul: true),
        E("Beetle", 1, hp(EK.Beetle), 12.5f, 2.8f, .7f, 40, 26, .7f, 3, "Game.Combat.cs:503", armored: true, extra: "Crystal", extraP: .3f),
        E("Eel", 1, hp(EK.Eel), 8, 2f, .45f, 60, 22, .6f, 4, "Game.Combat.cs:473", soul: true, extra: "SilverOre", extraP: .25f),
        E("Spitter", 1, hp(EK.Spitter), 7, 2.8f, .5f, 44, 18, .6f, 1.2f, "Game.Combat.cs:435", ranged: true, soul: true, evasive: .55f),
        E("Swarm", 1, hp(EK.Swarm), 3.5f, .7f, 0f, 75, 3, .12f, 1.5f, "Game.Combat.cs:459", soul: true, group: 4),
        E("Imp", 2, hp(EK.Imp), 7, 1.8f, 0f, 60, 24, .6f, 4, "Game.Ash.cs:119", ranged: true, fire: true, evasive: .4f, extra: "Ember", extraP: .3f),
        E("Golem", 2, hp(EK.Golem), 15, 3.2f, .9f, 28, 40, .9f, 2.5f, "Game.Ash.cs:124", fire: false, extra: "Ember", extraP: .3f),
        E("Spirit", 2, hp(EK.Spirit), 7, 2.5f, .5f, 50, 26, .6f, 3, "Game.Ash.cs:143", ranged: true, fire: true, soul: true, evasive: .5f),
        E("Bomber", 2, hp(EK.Bomber), 18, 99f, 1f, 55, 16, .5f, 1, "Game.Combat.cs:555", fire: true, expl: true),
        E("IceWolf", 3, hp(EK.IceWolf), 8, 2.7f, .45f, 70, 28, .55f, 4, "Game.Ice.cs:73", cold: true, evasive: .3f, extra: "Frost", extraP: .3f),
        E("FrostPriest", 3, hp(EK.FrostPriest), 7, 3.6f, .5f, 40, 30, .6f, 3, "Game.Ice.cs:87", ranged: true, cold: true, evasive: .5f, extra: "Frost", extraP: .3f),
        E("Sentinel", 3, hp(EK.Sentinel), 14, 2.9f, .8f, 30, 50, .9f, 2.5f, "Game.Ice.cs:101", cold: true, armored: true, extra: "Frost", extraP: .3f),
        E("Spitter", 3, hp(EK.Spitter), 7, 2.8f, .5f, 44, 18, .6f, 1, "Game.Combat.cs:435", ranged: true, soul: true, evasive: .55f),
        E("Mirage", 4, hp(EK.Mirage), 21, 3.1f, .6f, 45, 34, .5f, 3, "Game.Heart.cs:178", ranged: true, evasive: .5f),
        E("Weaver", 4, hp(EK.Weaver), 8, 3.4f, .6f, 40, 36, .7f, 3, "Game.Heart.cs:207", ranged: true, evasive: .45f),
        E("Stalker", 4, hp(EK.Stalker), 10, 3.6f, .6f, 70, 40, .6f, 3, "Game.Heart.cs:222", evasive: .3f),
    };

    // Фазы: 0 — здоровье ≥ 66%, 1 — 33–66%, 2 — ниже 33%. Паузы Gap и вес действий — из UpdateWarden/UpdateDepthWarden/UpdateAshWarden/UpdateWinter/UpdateSource; поведение
    // отдельных состояний Ash/Winter/Source (состояния 3–7) сведено к ближайшим по смыслу действиям с урона/предупреждения из кода — это приближение, отмечено в отчёте.
    static BossM[] BossTable(Func<EK, float> hp) => new[]
    {
        new BossM { Id = 1, Kind = "Warden", Hp = hp(EK.Warden), Region = 0, MeleeDmg = 12, MeleePeriod = 1f, RageMul = .65f, Src = "Game.Combat.cs:558 + Game.BossSpecials.cs", Moves = new[] {
            new BossMoveM { Name = "slam", Dmg = 22, Windup = .9f, Gap = 2.2f, W0 = .66f, W1 = .33f, W2 = .33f },
            new BossMoveM { Name = "dash", Dmg = 18, Windup = 1.1f, Post = .8f, Gap = 1.8f, W0 = .34f, W1 = .34f, W2 = .34f },
            new BossMoveM { Name = "summon", Windup = .8f, Gap = 3f, W1 = .33f, W2 = .33f, Summon = new[] { "Swarm", "Swarm", "Swarm" } },
            new BossMoveM { Name = "ring", Dmg = 12, Windup = .3f, Cooldown = 4f, Ranged = true, PhaseBelowHp = .33f },
            new BossMoveM { Name = "vortex", Dmg = 16, Windup = 1f, Cooldown = 10.5f, Open = 1.8f, PhaseBelowHp = .5f } } },
        new BossM { Id = 2, Kind = "DepthWarden", Hp = hp(EK.DepthWarden), Region = 1, MeleeDmg = 10, MeleePeriod = 1f, RageMul = .6f, Src = "Game.Combat.cs:624 + Game.BossSpecials.cs", Moves = new[] {
            new BossMoveM { Name = "dive", Dmg = 18, Windup = .35f, Hide = 2.2f, Post = 1.1f, Gap = 1.6f, W0 = .75f, W1 = .25f, W2 = .25f },
            new BossMoveM { Name = "tide", Dmg = 20, Windup = 1f, Gap = 2f, Ring = 10f, W0 = .25f, W1 = .25f, W2 = .25f },
            new BossMoveM { Name = "summon", Windup = 1f, Gap = 2.6f, W1 = .22f, W2 = .22f, Summon = new[] { "Eel", "Leech" } },
            new BossMoveM { Name = "darkness", Windup = 1.2f, Gap = 2.4f, W1 = .28f, W2 = .28f },
            new BossMoveM { Name = "whirlpool", Dmg = 16, Windup = 1f, Cooldown = 10.5f, Open = 2.4f, PhaseBelowHp = .6f } } },
        new BossM { Id = 3, Kind = "AshWarden", Hp = hp(EK.AshWarden), Region = 2, MeleeDmg = 12, MeleePeriod = 1f, RageMul = .6f, Fire = true, Src = "Game.Ash.cs:162", Moves = new[] {
            new BossMoveM { Name = "pillars", Dmg = 13, Windup = .8f, Gap = 2.4f, Fire = true, W0 = .5f, W1 = .25f, W2 = .2f },
            new BossMoveM { Name = "stomp", Dmg = 20, Windup = .9f, Gap = 2.2f, Ring = 8f, Fire = true, W0 = .5f, W1 = .2f },
            new BossMoveM { Name = "charge", Dmg = 18, Windup = .7f, Post = .6f, Gap = 2f, Fire = true, W1 = .35f, W2 = .6f },
            new BossMoveM { Name = "wave", Dmg = 12, Windup = .7f, Gap = 2f, Ring = 8f, Fire = true, Ranged = true, W1 = .2f, W2 = .2f },
            new BossMoveM { Name = "overheat", Dmg = 18, Windup = 1f, Cooldown = 10.5f, Open = 2.2f, Fire = true, PhaseBelowHp = .5f } } },
        new BossM { Id = 4, Kind = "WinterWarden", Hp = hp(EK.WinterWarden), Region = 3, MeleeDmg = 12, MeleePeriod = 1f, RageMul = .65f, ShieldFromPhase = 1, VulnSeconds = 6f, Cold = true, Src = "Game.Ice.cs:129", Moves = new[] {
            new BossMoveM { Name = "ice-blocks", Dmg = 14, Windup = .6f, Gap = 2.4f, Cold = true, W0 = .4f, W1 = .3f, W2 = .25f },
            new BossMoveM { Name = "summon", Windup = .8f, Gap = 2.6f, W0 = .3f, W1 = .2f, W2 = .2f, Summon = new[] { "IceWolf", "IceWolf" } },
            new BossMoveM { Name = "charge", Dmg = 16, Windup = .9f, Gap = 2f, Cold = true, W0 = .3f, W1 = .25f, W2 = .2f },
            new BossMoveM { Name = "fan", Dmg = 14, Windup = .7f, Gap = 2.2f, Cold = true, Ranged = true, W1 = .25f, W2 = .35f },
            new BossMoveM { Name = "blizzard", Dmg = 21, Windup = 1f, Cooldown = 10f, Open = 1.8f, Cold = true, Ranged = true, PhaseBelowHp = .66f } } },
        new BossM { Id = 5, Kind = "Source", Hp = hp(EK.Source), Region = 4, MeleeDmg = 14, MeleePeriod = 1.2f, RageMul = .62f, ShieldFromPhase = 1, VulnSeconds = 8f, Src = "Game.Heart.cs:252", Moves = new[] {
            new BossMoveM { Name = "absorb", Dmg = 9, Windup = .9f, Gap = 2.4f, W0 = .3f, W1 = .25f, W2 = .2f },
            new BossMoveM { Name = "volley", Dmg = 14, Windup = .75f, Gap = 2.2f, Ranged = true, W0 = .3f, W1 = .15f, W2 = .25f },
            new BossMoveM { Name = "summon", Windup = .8f, Gap = 2.4f, W0 = .2f, W1 = .2f, W2 = .15f, Summon = new[] { "Mirage", "Weaver" } },
            new BossMoveM { Name = "strike", Dmg = 14, Windup = .7f, Gap = 2.2f, W0 = .2f, W1 = .4f, W2 = .4f },
            new BossMoveM { Name = "echo", Dmg = 20, Windup = 1f, Cooldown = 9f, Open = 1.8f, PhaseBelowHp = .66f } } },
    };

    // ───────────────────────── реестр параметров ─────────────────────────
    public sealed class Param
    {
        public string Key, Group; public double Min, Max, Step; public Func<BalanceModel, double> Get; public Action<BalanceModel, double> Set;
    }

    /// <summary>Параметры, которые оптимизатор вправе менять. Каждый — ключ, границы и шаг; границы заданы разумными долями от текущего значения.</summary>
    public IEnumerable<Param> Params()
    {
        foreach (var f in typeof(TuneM).GetFields())
        {
            if (f.FieldType == typeof(float))
            {
                var fi = f; double v = Convert.ToDouble(fi.GetValue(T)); if (v == 0) continue;
                yield return new Param { Key = "tuning." + f.Name, Group = "tuning", Min = v * .5, Max = v * 2.0, Step = v * .1, Get = mm => Convert.ToDouble(fi.GetValue(mm.T)), Set = (mm, x) => fi.SetValue(mm.T, (float)x) };
            }
            else if (f.FieldType == typeof(float[]))
            {
                var arr = (float[])f.GetValue(T); var fi = f;
                for (int i = 0; i < arr.Length; i++)
                {
                    int ii = i; double v = arr[i]; if (v == 0) continue;
                    yield return new Param { Key = $"tuning.{f.Name}[{i}]", Group = "tuning", Min = v * .5, Max = v * 2.0, Step = v * .08, Get = mm => ((float[])fi.GetValue(mm.T))[ii], Set = (mm, x) => ((float[])fi.GetValue(mm.T))[ii] = (float)x };
                }
            }
        }
        foreach (var s in Skills)
        {
            if (s.Reward || string.IsNullOrEmpty(s.Unit) || s.EffPer == 0) continue; int si = s.Idx; double v = s.EffPer;
            yield return new Param { Key = $"skill.{s.Id}.EffPer", Group = "skill", Min = v * .4, Max = v * 2.5, Step = v * .12, Get = mm => mm.Skills[si].EffPer, Set = (mm, x) => mm.Skills[si].EffPer = (float)x };
            yield return new Param { Key = $"skill.{s.Id}.CostMul", Group = "skillcost", Min = .5, Max = 2.5, Step = .12, Get = mm => mm.Overrides.GetValueOrDefault($"skill.{s.Id}.CostMul", 1.0), Set = (mm, x) => { mm.Overrides[$"skill.{s.Id}.CostMul"] = x; } };
        }
        foreach (var w in Weapons)
        {
            if (w.Unique && w.Cd == 0) continue; int wi = Array.IndexOf(Weapons, w); double v = w.Dmg;
            yield return new Param { Key = $"weapon.{w.Id}.Dmg", Group = "weapon", Min = v * .6, Max = v * 1.6, Step = v * .08, Get = mm => mm.Weapons[wi].Dmg, Set = (mm, x) => mm.Weapons[wi].Dmg = (float)x };
        }
        for (int i = 0; i < Enemies.Length; i++)
        {
            int ei = i; var e = Enemies[i]; string k = $"enemy.{e.Kind}@{e.Region}";
            yield return new Param { Key = k + ".Dmg", Group = "enemy", Min = e.Dmg * .5, Max = e.Dmg * 2, Step = e.Dmg * .1, Get = mm => mm.Enemies[ei].Dmg, Set = (mm, x) => mm.Enemies[ei].Dmg = (float)x };
            yield return new Param { Key = k + ".Hp", Group = "enemy", Min = e.Hp * .5, Max = e.Hp * 2, Step = e.Hp * .1, Get = mm => mm.Enemies[ei].Hp, Set = (mm, x) => mm.Enemies[ei].Hp = (float)x };
        }
        for (int i = 0; i < Bosses.Length; i++)
        {
            int bi = i; double h = Bosses[i].Hp;
            yield return new Param { Key = $"boss.{Bosses[i].Kind}.Hp", Group = "boss", Min = h * .6, Max = h * 1.6, Step = h * .08, Get = mm => mm.Bosses[bi].Hp, Set = (mm, x) => mm.Bosses[bi].Hp = (float)x };
        }
    }

    public bool SetParam(string key, double value)
    {
        foreach (var p in Params()) if (p.Key == key) { p.Set(this, value); Overrides[key] = value; return true; }
        return false;
    }

    public double GetParam(string key)
    {
        foreach (var p in Params()) if (p.Key == key) return p.Get(this);
        return double.NaN;
    }

    public double CostMul(int skill) => Overrides.GetValueOrDefault($"skill.{Skills[skill].Id}.CostMul", 1.0) * (BaseTechCostMul > 0 ? T.TechCostMul / BaseTechCostMul : 1.0);

    /// <summary>Применяет набор изменений к копии модели.</summary>
    public BalanceModel With(IDictionary<string, double> ov)
    {
        var c = Clone(); if (ov == null) return c;
        var all = c.Params().ToDictionary(p => p.Key);
        foreach (var kv in ov) { if (all.TryGetValue(kv.Key, out var p)) { p.Set(c, kv.Value); c.Overrides[kv.Key] = kv.Value; } else Diag("override ignored (unknown key): " + kv.Key); }
        return c;
    }

    static void Diag(string s) => GD.PrintErr("[balance] " + s);
}
