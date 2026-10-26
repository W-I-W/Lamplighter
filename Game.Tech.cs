using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

public enum Tech
{
    Lens, Dash, Reaper, WideSwing, FarLanterns, Spin, Searing, Flare, Sight, Pulse,
    SilverEdge, Optics, CombatLamp, HeroLamp, SilverGears, AutoDrill, Boots, Beam,
    FlameEdge, HeatShield, Collector, ForgeHeat, FireLens, Fireproof
}

public sealed class TechDef
{
    public string Name, Desc, Req; public int[][] Cost; public Func<Game, bool> Ok; public int Page; public bool Reward;
}

public partial class Game
{
    static int[][] C(params int[] flat)
    {
        var r = new int[flat.Length / 2][];
        for (int i = 0; i < r.Length; i++) r[i] = new[] { flat[i * 2], flat[i * 2 + 1] };
        return r;
    }

    public static readonly string[] TechPages = { "Основные", "Глубины", "Пепел" };

    public static readonly TechDef[] Techs =
    {
        new() { Name = "Линза света", Desc = "Ваш свет шире на 30%", Req = "Постройте печь", Cost = C(It.Copper, 4, It.Shard, 2), Ok = g => g.HasFurnace },
        new() { Name = "Рывок [Shift]", Desc = "Быстрый рывок, неуязвимость, срывает светоедов", Req = "Медная кирка", Cost = C(It.Iron, 3, It.Copper, 3), Ok = g => g.Tier >= 1 },
        new() { Name = "Жатва теней", Desc = "Убийство лечит 3 здоровья", Req = "Победите 8 врагов", Cost = C(It.Shard, 4, It.Iron, 2), Ok = g => g.Kills >= 8 },
        new() { Name = "Широкий замах", Desc = "Добыча ломает и соседнюю клетку", Req = "Постройте кузницу", Cost = C(It.Iron, 5, It.Wood, 10), Ok = g => g.HasBuilding(Tl.Forge) },
        new() { Name = "Дальний свет", Desc = "Ваши фонари светят на 50% дальше", Req = "Поставьте свой фонарь", Cost = C(It.Copper, 6, It.Shard, 4), Ok = g => g.Lamps.Count >= 1 },
        new() { Name = "Круговой удар [ПКМ]", Desc = "Удар по всем врагам вокруг", Req = "Зажгите древний фонарь", Cost = C(It.Iron, 6, It.Shard, 3), Ok = g => g.LitCount >= 1 },
        new() { Name = "Жгучий свет", Desc = "Враги в вашем свете горят", Req = "Зажгите 2 древних фонаря", Cost = C(It.Aether, 2, It.Shard, 4), Ok = g => g.LitCount >= 2 },
        new() { Name = "Сигнальная ракета [Q]", Desc = "Бросок: свет и ожоги на 10 сек", Req = "Исследуйте 2 места", Cost = C(It.Shard, 3, It.Relic, 1), Ok = g => g.PoisDone >= 2 },
        new() { Name = "Эфирное зрение", Desc = "Места интереса видны сквозь тьму", Req = "Исследуйте 5 мест", Cost = C(It.Aether, 3, It.Relic, 1), Ok = g => g.PoisDone >= 5 },
        new() { Name = "Световой импульс [F]", Desc = "Награда за победу над Хранителем леса", Req = "Победите Хранителя Тёмного леса", Cost = C(), Ok = g => g.BossDown(1), Reward = true },

        new() { Name = "Серебряное лезвие", Desc = "+50% урона по теням; пробивает броню жуков", Req = "Зажгите фонарь в шахтах", Cost = C(It.Silver, 4, It.Iron, 3), Ok = g => g.LitIn(1) >= 1, Page = 1 },
        new() { Name = "Кристальная оптика", Desc = "Открывает кристальный фонарь [9] и зеркала [0]", Req = "Добудьте кристалл", Cost = C(It.Crystal, 4, It.Silver, 3), Ok = g => g.Inv[It.Crystal] >= 1 || g.Tier >= 3, Page = 1 },
        new() { Name = "Боевой фонарь", Desc = "Открывает боевой фонарь [8]: жжёт врагов рядом", Req = "Зажгите фонарь в шахтах", Cost = C(It.Silver, 3, It.Iron, 3, It.Shard, 3), Ok = g => g.LitIn(1) >= 1, Page = 1 },
        new() { Name = "Звёздный фонарь героя", Desc = "Свет героя +25%, сильнее жжёт врагов вокруг", Req = "Изучите Линзу света", Cost = C(It.Crystal, 4, It.Silver, 4, It.Shard, 4), Ok = g => g.Has(Tech.Lens), Page = 1 },
        new() { Name = "Серебряные шестерни", Desc = "Всё производство деревни быстрее в 1,5 раза", Req = "Постройте кузницу", Cost = C(It.Silver, 6, It.Iron, 4), Ok = g => g.HasBuilding(Tl.Forge), Page = 1 },
        new() { Name = "Автобур [Z]", Desc = "Новая постройка: сам добывает серебро и кристаллы", Req = "Фонарь в шахтах и склад", Cost = C(It.Crystal, 5, It.Silver, 5, It.Iron, 4), Ok = g => g.LitIn(1) >= 1 && g.HasBuilding(Tl.Warehouse), Page = 1 },
        new() { Name = "Болотные сапоги", Desc = "Вода почти не замедляет", Req = "Откройте путь в шахты", Cost = C(It.Silver, 3, It.Wood, 10, It.Crystal, 2), Ok = g => g.BeaconLevel >= 2, Page = 1 },
        new() { Name = "Прожектор [R]", Desc = "Награда Хранителя глубин: луч, что отражается зеркалами", Req = "Победите Хранителя глубин", Cost = C(), Ok = g => g.BossDown(2), Page = 1, Reward = true },

        new() { Name = "Огненное лезвие", Desc = "Удары поджигают врагов", Req = "Откройте путь в пустоши", Cost = C(It.Ember, 4, It.Iron, 4), Ok = g => g.BeaconLevel >= 3, Page = 2 },
        new() { Name = "Теплозащита", Desc = "Огонь и горячий пепел вредят на 60% меньше, дым слабее", Req = "Откройте путь в пустоши", Cost = C(It.Obsidian, 3, It.Silver, 3), Ok = g => g.BeaconLevel >= 3, Page = 2 },
        new() { Name = "Пепельный коллектор [X]", Desc = "Новая постройка: добывает обсидиан и уголь", Req = "Откройте путь в пустоши", Cost = C(It.Obsidian, 4, It.Silver, 4, It.Crystal, 3), Ok = g => g.BeaconLevel >= 3, Page = 2 },
        new() { Name = "Раскалённые горны", Desc = "Кузница работает в 2 раза быстрее", Req = "Кузница и путь в пустоши", Cost = C(It.Ember, 6, It.Obsidian, 4), Ok = g => g.BeaconLevel >= 3 && g.HasBuilding(Tl.Forge), Page = 2 },
        new() { Name = "Огненные линзы", Desc = "Фонари светят на 30% дальше, боевые поджигают", Req = "Зажгите фонарь в пустошах", Cost = C(It.Ember, 5, It.Crystal, 4, It.Obsidian, 3), Ok = g => g.LitIn(2) >= 1, Page = 2 },
        new() { Name = "Огнестойкость", Desc = "Награда Пепельного стража: иммунитет к огню и дыму", Req = "Победите Пепельного стража", Cost = C(), Ok = g => g.BossDown(3), Page = 2, Reward = true },
    };

    public int TechCount { get { int n = 0; for (int i = 0; i < Techs.Length; i++) if (Has((Tech)i)) n++; return n; } }

    public static List<int> PageList(int page)
    {
        var l = new List<int>(); for (int i = 0; i < Techs.Length; i++) if (Techs[i].Page == page) l.Add(i); return l;
    }

    void BuyTech(int n)
    {
        var list = PageList(TechPage).FindAll(i => !Techs[i].Reward);
        if (n < 0 || n >= list.Count) return;
        int i = list[n]; var t = Techs[i];
        if (Has((Tech)i)) { Toast("Уже изучено"); return; }
        if (!t.Ok(this)) { Fail("Пока недоступно: " + t.Req); return; }
        if (!Can(t.Cost)) { Fail("Не хватает: " + NeedStr(t.Cost)); return; }
        Pay(t.Cost); TechMask |= 1 << i; GiveXp(30);
        Burst(PPos, new Color("#8ff0ff"), 20, 100);
        Announce("Изучено: " + t.Name, t.Desc, new Color("#8ff0ff"), "tech");
        Save();
    }
}
