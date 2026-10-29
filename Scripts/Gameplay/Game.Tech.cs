using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

public enum Tech
{
    Lens, Dash, Reaper, WideSwing, FarLanterns, Spin, Searing, Flare, Sight, Pulse,
    SilverEdge, Optics, CombatLamp, HeroLamp, SilverGears, AutoDrill, Boots, Beam,
    FlameEdge, HeatShield, Collector, ForgeHeat, FireLens, Fireproof,
    Prism, SpikeBoots, FrostEdge, FrostArmor,
    Marksman, QuickDraw, Piercing, Overcharge,       // навыки дальнего боя (лук и посох)
    LensII, FarLanternsII,                           // вторые ступени: открываются после максимума первых (в маске TechMask их нет, только в SkillLv)
    Regen                                            // регенерация здоровья вне света (в маске TechMask её нет, только в SkillLv)
}

public sealed class TechDef
{
    public string Name, Desc, Req; public Func<Game, bool> Ok; public int Page; public bool Reward; public SkillDef S;
    int[][] baseCost, scaled;
    /// <summary>Базовая цена навыка: из таблицы, умноженная на TechCostMul из tuning.tres. Цена каждого уровня — база × кривая SkillDef.CostCurve (Game.LevelCost).</summary>
    public int[][] Cost
    {
        get
        {
            if (baseCost == null) return null; if (scaled != null) return scaled;
            var r = new int[baseCost.Length][]; float m = Tune.T.TechCostMul;
            for (int i = 0; i < r.Length; i++) r[i] = new[] { baseCost[i][0], baseCost[i][1] <= 0 ? 0 : (int)System.Math.Ceiling(baseCost[i][1] * m) };
            return scaled = r;
        }
        set { baseCost = value; scaled = null; }
    }
}

public partial class Game
{
    public static int[][] C(params int[] flat)
    {
        var r = new int[flat.Length / 2][];
        for (int i = 0; i < r.Length; i++) r[i] = new[] { flat[i * 2], flat[i * 2 + 1] };
        return r;
    }

    public static readonly string[] TechPages = { "Основные", "Глубины", "Пепел", "Лёд" };

    public static readonly TechDef[] Techs =
    {
        new() { Name = "Линза света I", Desc = "Ваш свет шире на 2% за уровень", Req = "Постройте печь", Cost = C(It.Copper, 4, It.Shard, 2), Ok = g => g.HasFurnace },
        new() { Name = "Рывок [Shift]", Desc = "Быстрый рывок, неуязвимость, срывает светоедов. Уровни сокращают перезарядку", Req = "Медная кирка", Cost = C(It.Iron, 3, It.Copper, 3), Ok = g => g.Tier >= 1 },
        new() { Name = "Жатва теней", Desc = "Убийство лечит; с каждым уровнем лечение сильнее", Req = "Победите 8 врагов", Cost = C(It.Shard, 4, It.Iron, 2), Ok = g => g.Kills >= 8 },
        new() { Name = "Широкий замах", Desc = "Добыча ломает и соседнюю клетку", Req = "Постройте кузницу", Cost = C(It.Iron, 5, It.Wood, 10), Ok = g => g.HasBuilding(Tl.Forge) },
        new() { Name = "Дальний свет I", Desc = "Ваши фонари светят дальше на 4% за уровень", Req = "Поставьте свой фонарь", Cost = C(It.Copper, 6, It.Shard, 4), Ok = g => g.Lamps.Count >= 1 },
        new() { Name = "Круговой удар [ПКМ]", Desc = "Удар по всем врагам вокруг (только оружие ближнего боя и кирка). Уровни расширяют круг и усиливают удар", Req = "Зажгите древний фонарь", Cost = C(It.Iron, 6, It.Shard, 3), Ok = g => g.LitCount >= 1 },
        new() { Name = "Жгучий свет", Desc = "Враги в вашем свете горят; уровни усиливают ожог", Req = "Зажгите 2 древних фонаря", Cost = C(It.Aether, 2, It.Shard, 4), Ok = g => g.LitCount >= 2 },
        new() { Name = "Сигнальная ракета [Q]", Desc = "Бросок: свет и ожоги. Уровни увеличивают длительность", Req = "Исследуйте 2 места", Cost = C(It.Shard, 3, It.Relic, 1), Ok = g => g.PoisDone >= 2 },
        new() { Name = "Эфирное зрение", Desc = "Места интереса видны сквозь тьму; уровни увеличивают радиус", Req = "Исследуйте 5 мест", Cost = C(It.Aether, 3, It.Relic, 1), Ok = g => g.PoisDone >= 5 },
        new() { Name = "Световой импульс [F]", Desc = "Награда за победу над Хранителем леса. Уровни сокращают перезарядку", Req = "Победите Хранителя Тёмного леса", Cost = C(It.Aether, 3, It.Shard, 3), Ok = g => g.BossDown(1), Reward = true },

        new() { Name = "Серебряное лезвие", Desc = "Урон по теням больше на 10% за уровень; пробивает броню жуков", Req = "Зажгите фонарь в шахтах", Cost = C(It.Silver, 4, It.Iron, 3), Ok = g => g.LitIn(1) >= 1, Page = 1 },
        new() { Name = "Кристальная оптика", Desc = "Открывает кристальный фонарь и зеркала [B]", Req = "Добудьте кристалл", Cost = C(It.Crystal, 4, It.Silver, 3), Ok = g => g.Inv[It.Crystal] >= 1 || g.Tier >= 3, Page = 1 },
        new() { Name = "Боевой фонарь", Desc = "Открывает боевой фонарь [B]: жжёт врагов рядом", Req = "Зажгите фонарь в шахтах", Cost = C(It.Silver, 3, It.Iron, 3, It.Shard, 3), Ok = g => g.LitIn(1) >= 1, Page = 1 },
        new() { Name = "Звёздный фонарь героя", Desc = "Свет героя +5% за уровень, сильнее жжёт врагов вокруг", Req = "Изучите Линзу света", Cost = C(It.Crystal, 4, It.Silver, 4, It.Shard, 4), Ok = g => g.Has(Tech.Lens), Page = 1 },
        new() { Name = "Серебряные шестерни", Desc = "Производство деревни быстрее на 10% за уровень", Req = "Постройте кузницу", Cost = C(It.Silver, 6, It.Iron, 4), Ok = g => g.HasBuilding(Tl.Forge), Page = 1 },
        new() { Name = "Автобур [B]", Desc = "Новая постройка: сам добывает серебро и кристаллы", Req = "Фонарь в шахтах и склад", Cost = C(It.Crystal, 5, It.Silver, 5, It.Iron, 4), Ok = g => g.LitIn(1) >= 1 && g.HasBuilding(Tl.Warehouse), Page = 1 },
        new() { Name = "Болотные сапоги", Desc = "Вода меньше замедляет: +7% скорости за уровень", Req = "Откройте путь в шахты", Cost = C(It.Silver, 3, It.Wood, 10, It.Crystal, 2), Ok = g => g.BeaconLevel >= 2, Page = 1 },
        new() { Name = "Прожектор [R]", Desc = "Награда Хранителя глубин: луч, что отражается зеркалами", Req = "Победите Хранителя глубин", Cost = C(), Ok = g => g.BossDown(2), Page = 1, Reward = true },

        new() { Name = "Огненное лезвие", Desc = "Удары поджигают врагов; поджог длится дольше с каждым уровнем", Req = "Откройте путь в пустоши", Cost = C(It.Ember, 4, It.Iron, 4), Ok = g => g.BeaconLevel >= 3, Page = 2 },
        new() { Name = "Теплозащита", Desc = "Огонь, пепел и дым вредят меньше на 12% за уровень", Req = "Откройте путь в пустоши", Cost = C(It.Obsidian, 3, It.Silver, 3), Ok = g => g.BeaconLevel >= 3, Page = 2 },
        new() { Name = "Пепельный коллектор [B]", Desc = "Новая постройка: добывает обсидиан и уголь", Req = "Откройте путь в пустоши", Cost = C(It.Obsidian, 4, It.Silver, 4, It.Crystal, 3), Ok = g => g.BeaconLevel >= 3, Page = 2 },
        new() { Name = "Раскалённые горны", Desc = "Кузница работает быстрее на 20% за уровень", Req = "Кузница и путь в пустоши", Cost = C(It.Ember, 6, It.Obsidian, 4), Ok = g => g.BeaconLevel >= 3 && g.HasBuilding(Tl.Forge), Page = 2 },
        new() { Name = "Огненные линзы", Desc = "Фонари светят дальше на 6% за уровень, боевые поджигают", Req = "Зажгите фонарь в пустошах", Cost = C(It.Ember, 5, It.Crystal, 4, It.Obsidian, 3), Ok = g => g.LitIn(2) >= 1, Page = 2 },
        new() { Name = "Огнестойкость", Desc = "Награда Пепельного стража: иммунитет к огню и дыму", Req = "Победите Пепельного стража", Cost = C(), Ok = g => g.BossDown(3), Page = 2, Reward = true },

        new() { Name = "Усиленные световые кристаллы", Desc = "Награда: светокристаллы +22%, лучи длиннее", Req = "Победите Хранителя Зимы", Cost = C(), Ok = g => g.BossDown(4), Page = 3, Reward = true },
        new() { Name = "Ледяные шипы", Desc = "Лёд больше не скользит под ногами", Req = "Откройте путь в монастырь", Cost = C(It.Frost, 3, It.Silver, 3), Ok = g => g.BeaconLevel >= 4, Page = 3 },
        new() { Name = "Морозное лезвие", Desc = "Удары замедляют врагов, высокие уровни замораживают", Req = "Откройте путь в монастырь", Cost = C(It.Frost, 4, It.Iron, 4), Ok = g => g.BeaconLevel >= 4, Page = 3 },
        new() { Name = "Тёплые меха", Desc = "Холод вредит меньше на 12% за уровень", Req = "Откройте путь в монастырь", Cost = C(It.Frost, 4, It.Obsidian, 2), Ok = g => g.BeaconLevel >= 4, Page = 3 },
        new() { Name = "Меткий глаз", Desc = "Лук и посох бьют сильнее на 5% за уровень", Req = "Создайте лук или посох", Cost = C(It.Iron, 4, It.Shard, 4), Ok = g => g.HasRangedWeapon() },
        new() { Name = "Быстрая тетива", Desc = "Лук и посох стреляют чаще на 4% за уровень", Req = "Создайте лук или посох и убейте 30 врагов", Cost = C(It.Iron, 5, It.Copper, 6), Ok = g => g.HasRangedWeapon() && g.Kills >= 30 },
        new() { Name = "Пробивающий выстрел", Desc = "Снаряды дальнего боя пробивают ещё одного врага", Req = "Метко: изучите «Меткий глаз»", Cost = C(It.Silver, 4, It.Crystal, 3), Ok = g => g.Has(Tech.Marksman) },
        new() { Name = "Эфирный заряд", Desc = "Снаряды летят быстрее и дальше на 6% за уровень", Req = "Изучите «Меткий глаз» и зажгите 2 фонаря", Cost = C(It.Aether, 3, It.Crystal, 3, It.Shard, 6), Ok = g => g.Has(Tech.Marksman) && g.LitCount >= 2 },
        new() { Name = "Линза света II", Desc = "Ваш свет шире ещё на 4% за уровень", Req = "Зажгите фонарь в шахтах", Cost = C(It.Silver, 6, It.Crystal, 4), Ok = g => g.LitIn(1) >= 1 },
        new() { Name = "Дальний свет II", Desc = "Ваши фонари светят дальше ещё на 6% за уровень", Req = "Зажгите фонарь в шахтах", Cost = C(It.Silver, 5, It.Crystal, 5), Ok = g => g.LitIn(1) >= 1 },
        new() { Name = "Регенерация", Desc = "Здоровье восстанавливается быстрее вне света; каждый уровень добавляет скорость", Req = "Победите 5 врагов", Cost = C(It.Shard, 3, It.Copper, 3), Ok = g => g.Kills >= 5 },
    };

    public bool HasRangedWeapon() { foreach (var o in Owned) { var d = Db.Weapon(o.Id); if (d != null && d.Cls != WClass.Sword) return true; } return false; }

    /// <summary>Для проверок: поднимает n-й (по порядку дерева) не наградный навык категории до максимума, пока хватает ресурсов.</summary>
    public void BuyTech(int n)
    {
        var list = PageList(TechPage).FindAll(i => !Techs[i].Reward); if (n < 0 || n >= list.Count) return;
        int t = list[n]; for (int k = SkillLv[t]; k < SkillMax(t); k++) if (!LearnTech(t)) break;
    }

    public int TechCount { get { int n = 0; for (int i = 0; i < Techs.Length; i++) if (Has((Tech)i)) n++; return n; } }

    /// <summary>Навыки категории в порядке дерева: сверху вниз, слева направо (по Pos из SkillDef).</summary>
    public static List<int> PageList(int page)
    {
        var res = new List<int>(); for (int i = 0; i < Techs.Length; i++) if (Techs[i].Page == page) res.Add(i);
        res.Sort((a, b) => { var pa = SkillOf(a).Pos; var pb = SkillOf(b).Pos; int c = pa.Y.CompareTo(pb.Y); return c != 0 ? c : pa.X.CompareTo(pb.X); });
        return res;
    }
}
