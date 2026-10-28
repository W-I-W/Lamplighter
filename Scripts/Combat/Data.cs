using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

public enum Fxk { Burn, Slow, Freeze, Chain, Multi, Pierce, Siphon, Blast, Arc, Count }
public enum WClass { Pick, Sword, Bow, Staff }
public enum Rar { Common, Uncommon, Rare, Epic, Legendary }

public sealed class WeaponDef
{
    public string Id, Name, Desc, Bp; public WClass Cls; public int Tier, Slots, Pierce; public float Dmg, Cd, Reach, Speed;
    public Rar Rar; public (Fxk k, float p)[] Inherent = Array.Empty<(Fxk, float)>(); public string[] Tags = Array.Empty<string>();
    public int[][] Cost; public bool Unique;
}

public sealed class ModDef
{
    public int Id; public string Name, Desc, Tag, Bp; public (Fxk k, float p)[] Eff; public int[][] Cost;
}

public sealed class BpDef { public string Id, Name, Desc; public int Need; }

/// <summary>Статические таблицы данных: оружие, модификаторы, чертежи; реликвии и синергии — Resource.</summary>
public static class Db
{
    public static readonly string[] ClassNames = { "Кирка", "Меч", "Лук", "Посох" };
    public static readonly Color[] RarCol = { new("#b8bcc8"), new("#7ad45a"), new("#5aa8ff"), new("#c070ff"), new("#ffa94a") };
    public static readonly string[] RarName = { "Обычная", "Необычная", "Редкая", "Эпическая", "Легендарная" };

    static int[][] C(params int[] f) => Game.C(f);

    public static readonly WeaponDef[] Weapons =
    {
        new() { Id = "sword1", Name = "Меч путника", Cls = WClass.Sword, Tier = 1, Dmg = 5, Cd = .45f, Reach = 24, Slots = 1, Cost = C(It.Copper, 4, It.Wood, 3), Desc = "Дуга ближнего боя бьёт всех врагов перед вами" },
        new() { Id = "sword2", Name = "Железный меч", Cls = WClass.Sword, Tier = 2, Dmg = 8, Cd = .42f, Reach = 27, Slots = 2, Bp = "bp_sword2", Cost = C(It.Iron, 6, It.Wood, 2), Desc = "Шире дуга, сильнее удар", Rar = Rar.Uncommon },
        new() { Id = "sword3", Name = "Серебряный клинок", Cls = WClass.Sword, Tier = 3, Dmg = 12, Cd = .4f, Reach = 30, Slots = 3, Bp = "bp_sword3", Cost = C(It.Silver, 6, It.Crystal, 2, It.Iron, 3), Desc = "Почти круговая дуга", Rar = Rar.Rare },
        new() { Id = "sword_u", Name = "Клинок Хранителя", Cls = WClass.Sword, Tier = 3, Dmg = 11, Cd = .4f, Reach = 29, Slots = 2, Unique = true, Rar = Rar.Epic, Inherent = new[] { (Fxk.Arc, 1f), (Fxk.Siphon, .5f) }, Tags = new[] { "light" }, Desc = "Каждый удар посылает световую дугу и копит свет" },
        new() { Id = "storm_u", Name = "Грозовой клинок", Cls = WClass.Sword, Tier = 3, Dmg = 10, Cd = .42f, Reach = 28, Slots = 2, Unique = true, Rar = Rar.Epic, Inherent = new[] { (Fxk.Chain, 2f) }, Tags = new[] { "storm" }, Desc = "Удары перескакивают молнией на других врагов" },
        new() { Id = "bow1", Name = "Короткий лук", Cls = WClass.Bow, Tier = 1, Dmg = 5, Cd = .5f, Speed = 230, Slots = 1, Cost = C(It.Wood, 6, It.Copper, 2), Desc = "Стрелы летят далеко; добыча — на ЛКМ по руде" },
        new() { Id = "bow2", Name = "Составной лук", Cls = WClass.Bow, Tier = 2, Dmg = 8, Cd = .46f, Speed = 250, Slots = 2, Bp = "bp_bow2", Cost = C(It.Iron, 4, It.Wood, 4), Desc = "Быстрее и точнее", Rar = Rar.Uncommon },
        new() { Id = "bow3", Name = "Кристальный лук", Cls = WClass.Bow, Tier = 3, Dmg = 10, Cd = .45f, Speed = 270, Slots = 3, Pierce = 1, Bp = "bp_bow3", Cost = C(It.Crystal, 4, It.Silver, 4), Desc = "Стрелы пробивают врага насквозь", Rar = Rar.Rare },
        new() { Id = "bow_u", Name = "Лук глубин", Cls = WClass.Bow, Tier = 3, Dmg = 9, Cd = .46f, Speed = 260, Slots = 2, Pierce = 2, Unique = true, Rar = Rar.Epic, Inherent = new[] { (Fxk.Slow, 1f) }, Tags = new[] { "ice" }, Desc = "Ледяные стрелы замедляют и пробивают" },
        new() { Id = "staff1", Name = "Посох искр", Cls = WClass.Staff, Tier = 1, Dmg = 7, Cd = .8f, Speed = 130, Slots = 1, Inherent = new[] { (Fxk.Chain, 1f) }, Tags = new[] { "storm" }, Cost = C(It.Wood, 4, It.Shard, 4), Desc = "Искровой шар взрывается и бьёт молнией" },
        new() { Id = "staff2", Name = "Кристальный посох", Cls = WClass.Staff, Tier = 2, Dmg = 10, Cd = .75f, Speed = 135, Slots = 2, Bp = "bp_staff2", Inherent = new[] { (Fxk.Slow, 1f), (Fxk.Freeze, .5f) }, Tags = new[] { "ice" }, Cost = C(It.Crystal, 3, It.Shard, 6, It.Wood, 2), Desc = "Ледяные шары замедляют и замораживают", Rar = Rar.Uncommon },
        new() { Id = "staff3", Name = "Посох пепла", Cls = WClass.Staff, Tier = 3, Dmg = 13, Cd = .7f, Speed = 140, Slots = 3, Bp = "bp_staff3", Inherent = new[] { (Fxk.Burn, 2f) }, Tags = new[] { "fire" }, Cost = C(It.Ember, 4, It.Obsidian, 3, It.Shard, 4), Desc = "Огненные шары поджигают", Rar = Rar.Rare },
        new() { Id = "staff_w", Name = "Посох Зимы", Cls = WClass.Staff, Tier = 3, Dmg = 14, Cd = .7f, Speed = 145, Slots = 2, Unique = true, Rar = Rar.Epic, Inherent = new[] { (Fxk.Freeze, 2f), (Fxk.Slow, 1f) }, Tags = new[] { "ice" }, Desc = "Ледяные шары замораживают врагов" },
        new() { Id = "staff_u", Name = "Посох Пепельного стража", Cls = WClass.Staff, Tier = 3, Dmg = 12, Cd = .68f, Speed = 150, Slots = 2, Unique = true, Rar = Rar.Epic, Inherent = new[] { (Fxk.Burn, 2f), (Fxk.Blast, 1f) }, Tags = new[] { "fire" }, Desc = "Огненные шары взрываются при гибели врагов" },
    };

    public static readonly ModDef[] Mods =
    {
        new() { Id = 0, Name = "Поджог", Desc = "Удары поджигают врагов", Tag = "fire", Bp = "bp_mod_ignite", Eff = new[] { (Fxk.Burn, 1f) }, Cost = C(It.Ember, 2, It.Iron, 2) },
        new() { Id = 1, Name = "Заморозка", Desc = "Замедляет и замораживает", Tag = "ice", Bp = "bp_mod_frost", Eff = new[] { (Fxk.Slow, 1f), (Fxk.Freeze, .6f) }, Cost = C(It.Crystal, 2, It.Silver, 1) },
        new() { Id = 2, Name = "Молния", Desc = "Цепная молния на соседних врагов", Tag = "storm", Bp = "bp_mod_shock", Eff = new[] { (Fxk.Chain, 1f) }, Cost = C(It.Silver, 2, It.Shard, 3) },
        new() { Id = 3, Name = "Залп", Desc = "+1 снаряд (меч: волна)", Tag = null, Bp = "bp_mod_multi", Eff = new[] { (Fxk.Multi, 1f) }, Cost = C(It.Iron, 3, It.Wood, 4) },
        new() { Id = 4, Name = "Пробой", Desc = "Снаряды пробивают +1 врага", Tag = null, Bp = "bp_mod_multi", Eff = new[] { (Fxk.Pierce, 1f) }, Cost = C(It.Iron, 2, It.Crystal, 1) },
        new() { Id = 5, Name = "Световой сифон", Desc = "Удары восстанавливают свет и перезарядку", Tag = "light", Bp = "bp_mod_siphon", Eff = new[] { (Fxk.Siphon, 1f) }, Cost = C(It.Shard, 6, It.Crystal, 1) },
        new() { Id = 6, Name = "Взрыв", Desc = "Враги взрываются при гибели", Tag = "fire", Bp = "bp_mod_ignite", Eff = new[] { (Fxk.Blast, 1f) }, Cost = C(It.Obsidian, 2, It.Shard, 2) },
    };

    public static readonly BpDef[] Bps =
    {
        new() { Id = "bp_sword2", Name = "Ковка железных мечей", Desc = "Открывает Железный меч", Need = 2 },
        new() { Id = "bp_sword3", Name = "Серебряная ковка", Desc = "Открывает Серебряный клинок", Need = 3 },
        new() { Id = "bp_bow2", Name = "Составная тетива", Desc = "Открывает Составной лук", Need = 2 },
        new() { Id = "bp_bow3", Name = "Кристальный лук", Desc = "Открывает Кристальный лук", Need = 3 },
        new() { Id = "bp_staff2", Name = "Кристальный посох", Desc = "Открывает Кристальный посох", Need = 2 },
        new() { Id = "bp_staff3", Name = "Пепельный посох", Desc = "Открывает Посох пепла", Need = 3 },
        new() { Id = "bp_mod_multi", Name = "Баллистика", Desc = "Модификаторы: Залп и Пробой", Need = 1 },
        new() { Id = "bp_mod_ignite", Name = "Огненные вставки", Desc = "Модификаторы: Поджог и Взрыв", Need = 2 },
        new() { Id = "bp_mod_frost", Name = "Ледяные вставки", Desc = "Модификатор: Заморозка", Need = 2 },
        new() { Id = "bp_mod_shock", Name = "Грозовые вставки", Desc = "Модификатор: Молния", Need = 2 },
        new() { Id = "bp_mod_siphon", Name = "Световой сифон", Desc = "Модификатор: восстановление света", Need = 2 },
        new() { Id = "bp_hub", Name = "Логистика", Desc = "Постройка: Узел снабжения связывает здания со складом", Need = 1 },
        new() { Id = "bp_refinery", Name = "Перерабатывающий цех", Desc = "Постройка: Цех делает шестерни и линзы", Need = 2 },
        new() { Id = "bp_stall", Name = "Торговая лавка", Desc = "Постройка: Лавка торговца", Need = 1 },
        new() { Id = "bp_barrier", Name = "Световые барьеры", Desc = "Постройка: столбы, задерживающие тени", Need = 2 },
        new() { Id = "bp_amp", Name = "Светокристаллы", Desc = "Постройка: усилители соседних фонарей", Need = 2 },
        new() { Id = "bp_cryo", Name = "Криоэкстрактор", Desc = "Постройка: добывает ледяной кристалл автоматически", Need = 2 },
        new() { Id = "bp_herolamp", Name = "Фонарь героя", Desc = "Улучшение переносного фонаря в окне экипировки", Need = 2 },
    };

    public static WeaponDef Weapon(string id) { foreach (var w in Weapons) if (w.Id == id) return w; return null; }
    public static BpDef Bp(string id) { foreach (var b in Bps) if (b.Id == id) return b; return null; }

    // ───────────────────────── Resource: реликвии и синергии ─────────────────────────
    public static List<RelicDef> Relics = new();
    public static List<SynergyDef> Synergies = new();

    public static RelicDef Relic(string id) { foreach (var r in Relics) if (r.Id == id) return r; return null; }

    static RelicDef R(string id, string title, string desc, Rar rar, string[] tags, string[] eff, string tint, bool perm = true) =>
        new() { Id = id, Title = title, Desc = desc, Rarity = (int)rar, Tags = tags, Effects = eff, Tint = new Color(tint), Permanent = perm };

    static SynergyDef S(string id, string title, string path, string desc, string[] req, string[] grants, string hint) =>
        new() { Id = id, Title = title, Path = path, Desc = desc, Requires = req, Grants = grants, Hint = hint };

    public static List<RelicDef> DefaultRelics() => new()
    {
        R("r_embersp", "Искра очага", "Тёплый уголёк: огненные удары чуть чаще", Rar.Common, new[] { "fire" }, new[] { "Burn:0.5" }, "#ff9a3a"),
        R("r_emberheart", "Сердце пламени", "Реликвия Пепельного стража", Rar.Epic, new[] { "fire" }, new[] { "Burn:1" }, "#ff6a1a"),
        R("r_ashcrown", "Пепельная корона", "Огонь и жар вредят слабее", Rar.Rare, new[] { "fire" }, new[] { "HeatResist:0.35" }, "#c0502a"),
        R("r_frostshard", "Ледяной осколок", "Враги слабее держат темп", Rar.Common, new[] { "ice" }, new[] { "Slow:0.5" }, "#8fd8ff"),
        R("r_winterheart", "Сердце зимы", "Реликвия Хранителя глубин", Rar.Epic, new[] { "ice" }, new[] { "Freeze:0.5" }, "#5ac0ff"),
        R("r_rimemirror", "Зеркало инея", "Шаг лёгкий, как лёд", Rar.Rare, new[] { "ice" }, new[] { "Speed:0.06" }, "#bfe9ff"),
        R("r_dawnprism", "Призма рассвета", "Реликвия Хранителя леса", Rar.Epic, new[] { "light" }, new[] { "LampRadius:0.08" }, "#ffe27a"),
        R("r_beaconwick", "Фитиль маяка", "Удары копят свет", Rar.Uncommon, new[] { "light" }, new[] { "Siphon:1" }, "#ffd04a"),
        R("r_firstlight", "Первый луч", "Свет героя шире", Rar.Rare, new[] { "light" }, new[] { "HeroRadius:0.1" }, "#fff2b0"),
        R("r_stormclaw", "Громовой коготь", "Удары бьют молнией", Rar.Rare, new[] { "storm" }, new[] { "Chain:1" }, "#d0b0ff"),
        R("r_sparkcoil", "Катушка искр", "Копит свет от ударов", Rar.Uncommon, new[] { "storm" }, new[] { "Siphon:0.5" }, "#b0a0ff"),
        R("r_minercharm", "Жила шахтёра", "Руда ломается сразу вместе с соседней", Rar.Uncommon, new string[0], new[] { "MineChain:1" }, "#e0e8f0"),
        R("r_luckycoin", "Монета удачи", "Редкие находки случаются чаще", Rar.Uncommon, new string[0], new[] { "Luck:0.15" }, "#ffd86b"),
        R("r_shadowblood", "Кровь тени", "Убийство лечит", Rar.Rare, new string[0], new[] { "KillHeal:2" }, "#a060ff"),
        R("r_merchant", "Печать торговца", "Лучшие цены у торговцев", Rar.Uncommon, new string[0], new[] { "Trade:0.1" }, "#e0c080"),
        R("r_glaciersoul", "Душа ледника", "Холод в крови: заморозка чаще", Rar.Rare, new[] { "ice" }, new[] { "Freeze:0.8" }, "#9fd8ff"),
        R("r_prismcore", "Ядро призмы", "Реликвия Хранителя Зимы: свет фонарей усиливается", Rar.Epic, new[] { "light" }, new[] { "LampRadius:0.08", "Arc:1" }, "#e8faff"),
        R("r_voidlens", "Линза пустоты", "Видит иллюзии: фонарь шире, метит настоящий свет", Rar.Rare, new[] { "light" }, new[] { "HeroRadius:0.1", "Reveal:1" }, "#d8b8ff"),
        // временные: только для испытаний Тьмы
        R("t_flame", "Пламя испытания", "Временно: огонь на всех ударах", Rar.Rare, new[] { "fire" }, new[] { "Burn:1.5" }, "#ff7a2a", false),
        R("t_frost", "Иней испытания", "Временно: заморозка на всех ударах", Rar.Rare, new[] { "ice" }, new[] { "Slow:1", "Freeze:0.6" }, "#7ad0ff", false),
        R("t_light", "Свет испытания", "Временно: сильный свет и сифон", Rar.Rare, new[] { "light" }, new[] { "Siphon:1", "HeroRadius:0.15" }, "#ffe9a0", false),
        R("t_storm", "Гроза испытания", "Временно: цепные молнии", Rar.Rare, new[] { "storm" }, new[] { "Chain:2" }, "#c8a8ff", false),
    };

    public static List<SynergyDef> DefaultSynergies() => new()
    {
        S("syn_fire_sword", "Поджигающие удары", "Огненный путь", "Огненная реликвия + меч: удары поджигают", new[] { "tag:fire>=1", "class:sword" }, new[] { "Burn:1.5" }, "нужна огненная реликвия или вставка и меч"),
        S("syn_fire_pulse", "Огненный взрыв импульса", "Огненный путь", "Огненная реликвия + световой импульс: волна оставляет огонь", new[] { "tag:fire>=1", "ability:pulse" }, new[] { "Flag:FirePulse" }, "нужны огненный эффект и Световой импульс [F]"),
        S("syn_fire_trail", "Огненный след", "Огненный путь", "Три огненных эффекта: герой оставляет пламя", new[] { "tag:fire>=3" }, new[] { "Flag:FireTrail" }, "нужно три огненных эффекта (реликвии, вставки, оружие)"),
        S("syn_fire_staff", "Огненные шары", "Огненный путь", "Огонь + посох: шары взрываются", new[] { "tag:fire>=1", "class:staff" }, new[] { "Blast:1" }, "нужен огненный эффект и посох"),
        S("syn_ice_bow", "Замедляющие стрелы", "Ледяной путь", "Ледяная реликвия + лук: стрелы замедляют", new[] { "tag:ice>=1", "class:bow" }, new[] { "Slow:2" }, "нужна ледяная реликвия или вставка и лук"),
        S("syn_ice_dash", "Взрыв холода", "Ледяной путь", "Лёд + рывок: рывок морозит врагов вокруг", new[] { "tag:ice>=1", "ability:dash" }, new[] { "Flag:FrostDash" }, "нужны ледяной эффект и рывок [Shift]"),
        S("syn_ice_freeze", "Дыхание зимы", "Ледяной путь", "Три ледяных эффекта: периодически замораживает врагов рядом", new[] { "tag:ice>=3" }, new[] { "Flag:FreezePulse" }, "нужно три ледяных эффекта"),
        S("syn_ice_staff", "Ледяные шары", "Ледяной путь", "Лёд + посох: шары замораживают", new[] { "tag:ice>=1", "class:staff" }, new[] { "Freeze:1" }, "нужен ледяной эффект и посох"),
        S("syn_light_sword", "Световая дуга", "Путь света", "Световая реликвия + меч: дополнительная дуга света", new[] { "tag:light>=1", "class:sword" }, new[] { "Arc:1" }, "нужна световая реликвия или вставка и меч"),
        S("syn_light_lamp", "Сияющие фонари", "Путь света", "Свет + фонарь: радиус больше", new[] { "tag:light>=1", "ability:lamp" }, new[] { "LampRadius:0.15", "HeroRadius:0.1" }, "нужны световой эффект и хотя бы один фонарь"),
        S("syn_light_surge", "Лучезарность", "Путь света", "Три световых эффекта, усиление света", new[] { "tag:light>=3" }, new[] { "Flag:RadiantSurge" }, "нужно три световых эффекта"),
        S("syn_storm_staff", "Грозовой посох", "Путь бури", "Буря + посох: шары разят цепной молнией", new[] { "tag:storm>=1", "class:staff" }, new[] { "Chain:1" }, "нужен грозовой эффект и посох"),
        S("syn_storm_bow", "Молниеносные стрелы", "Путь бури", "Буря + лук: стрелы бьют молнией", new[] { "tag:storm>=1", "class:bow" }, new[] { "Chain:1" }, "нужен грозовой эффект и лук"),
    };

    static bool inited;
    public static void Init()
    {
        if (inited) return; inited = true;
        var rd = DefaultRelics(); var sd = DefaultSynergies();
        var lr = LoadDir<RelicDef>("res://Resources/Items/Relics"); var ls = LoadDir<SynergyDef>("res://Resources/Items/Synergies");
        Relics = lr.Count >= rd.Count ? lr : rd;
        Synergies = ls.Count >= sd.Count ? ls : sd;
        // порядок как в таблице по умолчанию
        Relics.Sort((a, b) => rd.FindIndex(x => x.Id == a.Id).CompareTo(rd.FindIndex(x => x.Id == b.Id)));
        Synergies.Sort((a, b) => sd.FindIndex(x => x.Id == a.Id).CompareTo(sd.FindIndex(x => x.Id == b.Id)));
        GD.Print($"[Data] relics={Relics.Count} synergies={Synergies.Count} fromResources={(lr.Count >= rd.Count)}");
    }

    static List<T> LoadDir<T>(string dir) where T : Resource
    {
        var l = new List<T>();
        try
        {
            var d = DirAccess.Open(dir); if (d == null) return l;
            foreach (var f in d.GetFiles())
            {
                var name = f.EndsWith(".remap") ? f.Replace(".remap", "") : f;
                if (!name.EndsWith(".tres")) continue;
                var r = ResourceLoader.Load<T>(dir + "/" + name); if (r != null) l.Add(r);
            }
        }
        catch (Exception e) { GD.PushWarning("resource load: " + e.Message); }
        return l;
    }

    /// <summary>Записывает таблицы по умолчанию как .tres (запуск с LAMP_STAGE=genres).</summary>
    public static void SaveDefaults()
    {
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://Resources/Items/Relics"));
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://Resources/Items/Synergies"));
        foreach (var r in DefaultRelics()) GD.Print($"save {r.Id}: {ResourceSaver.Save(r, $"res://Resources/Items/Relics/{r.Id}.tres")}");
        foreach (var s in DefaultSynergies()) GD.Print($"save {s.Id}: {ResourceSaver.Save(s, $"res://Resources/Items/Synergies/{s.Id}.tres")}");
    }
}
