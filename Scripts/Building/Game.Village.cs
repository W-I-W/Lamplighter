using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

public partial class Game
{
    public List<Building> Buildings = new();
    public List<Villager> Villagers = new();
    public int[] Stock = new int[It.Count];
    public int PoisDone { get { int n = 0; foreach (var p in W.Pois) if (p.State == 3) n++; return n; } }

    public static readonly string[] BNames = { "Склад", "Лесопилка", "Шахта", "Кузница", "Автобур", "Коллектор пепла", "Перерабатывающий цех", "Узел снабжения", "Торговая лавка", "Криоэкстрактор", "Жилой дом" };
    public static readonly Tl[] BTypes = { Tl.Warehouse, Tl.Sawmill, Tl.Mine, Tl.Forge, Tl.Drill, Tl.Collector, Tl.Refinery, Tl.Hub, Tl.Stall, Tl.Cryo, Tl.House };
    public const int BCount = 11;
    /// <summary>Остались только постройки с понятной ролью: склад (общий сундук), кузница (станция крафта), лавка (торговля), дом (жильё). Остальные индексы зарезервированы под старые сохранения и сеть.</summary>
    public static bool BActive(int bk) => bk == 0 || bk == 3 || bk == 8 || bk == 10;
    static readonly int[][][][] BCosts =
    {
        new[] { C(It.Wood, 15, It.Stone, 10), C(It.Wood, 20, It.Stone, 20, It.Copper, 4), C(It.Iron, 6, It.Relic, 1), C(It.Silver, 6, It.Crystal, 3), C(It.Gear, 4, It.Lens, 2, It.Crystal, 4) },
        new[] { C(It.Wood, 12, It.Stone, 6, It.Copper, 2), C(It.Wood, 15, It.Copper, 6), C(It.Iron, 6, It.Aether, 2), C(It.Silver, 6, It.Crystal, 3), C(It.Gear, 3, It.Lens, 2, It.Silver, 6) },
        new[] { C(It.Stone, 10, It.Copper, 4, It.Wood, 6), C(It.Iron, 5, It.Wood, 10), C(It.Iron, 8, It.Aether, 3), C(It.Silver, 8, It.Crystal, 4), C(It.Gear, 4, It.Lens, 3, It.Silver, 6) },
        new[] { C(It.Stone, 15, It.Copper, 6, It.Iron, 3), C(It.Iron, 6, It.Relic, 1), C(It.Iron, 10, It.Aether, 3), C(It.Silver, 8, It.Crystal, 4), C(It.Gear, 4, It.Lens, 3, It.Crystal, 4) },
        new[] { C(It.Crystal, 5, It.Silver, 5, It.Iron, 4), C(It.Silver, 4, It.Iron, 4, It.Wood, 6), C(It.Silver, 6, It.Crystal, 3), C(It.Crystal, 5, It.Silver, 6, It.Relic, 1), C(It.Gear, 3, It.Lens, 3, It.AncCrystal, 1) },
        new[] { C(It.Obsidian, 4, It.Silver, 4, It.Crystal, 3), C(It.Ember, 3, It.Obsidian, 3), C(It.Ember, 4, It.Silver, 4), C(It.Obsidian, 5, It.Ember, 5, It.Crystal, 3), C(It.Gear, 3, It.Lens, 3, It.AncCrystal, 1) },
        new[] { C(It.Iron, 8, It.Copper, 10, It.Stone, 15), C(It.Iron, 8, It.Silver, 4), C(It.Silver, 8, It.Crystal, 4), C(It.Gear, 4, It.Lens, 2), C(It.Gear, 6, It.Lens, 4, It.Crystal, 6) },
        new[] { C(It.Copper, 6, It.Iron, 3, It.Wood, 8), C(It.Iron, 5, It.Silver, 3), C(It.Silver, 6, It.Crystal, 3) },
        new[] { C(It.Wood, 20, It.Stone, 15, It.Copper, 6), C(It.Silver, 4, It.Coins, 100), C(It.Crystal, 4, It.Coins, 300) },
        new[] { C(It.Frost, 4, It.Silver, 4, It.Crystal, 3), C(It.Frost, 4, It.Gear, 2), C(It.Frost, 6, It.Lens, 2), C(It.Frost, 8, It.Lens, 3, It.Crystal, 4), C(It.Gear, 4, It.Lens, 4, It.AncCrystal, 1) },
        new[] { C(It.Wood, 20, It.Stone, 8), C(It.Wood, 24, It.Stone, 14, It.Copper, 4), C(It.Wood, 30, It.Stone, 20, It.Iron, 4) },
    };
    static readonly string[][] BDesc =
    {
        new[] { "общий сундук: 60 каждого ресурса", "120", "250, отдаёт ресурсы вам рядом", "500", "800" },
        new[] { "2 дерева / 6 с", "2 дерева / 4 с", "3 дерева / 3 с", "4 дерева / 2,2 с", "5 деревьев / 1,6 с" },
        new[] { "камень и медная руда / 6 с", "+ железная руда / 5 с", "+ осколки света / 3,6 с", "+ серебряная руда / 2,8 с", "очень быстро / 2 с" },
        new[] { "станция крафта: оружие, вставки, шестерни и линзы" },
        new[] { "серебряная руда и кристаллы / 12 с", "быстрее / 9 с", "быстрее / 6 с", "очень быстро / 4,5 с", "молниеносно / 3 с" },
        new[] { "обсидиан и уголь пепла / 12 с", "быстрее / 9 с", "быстрее / 6 с", "очень быстро / 4,5 с", "молниеносно / 3 с" },
        new[] { "делает шестерни и линзы / 8 с", "быстрее / 6 с", "быстрее / 4,5 с", "быстрее / 3,5 с", "быстрее / 2,5 с" },
        new[] { "связь 14: подключает здания к складу", "связь 20", "связь 28" },
        new[] { "торговля: базовые товары, чертежи", "торговля: редкие материалы и вставки", "торговля: реликвии и снаряжение" },
        new[] { "ледяной кристалл и серебро / 12 с", "быстрее / 9 с", "быстрее / 6 с", "очень быстро / 4,5 с", "молниеносно / 3 с" },
        new[] { "2 места для жителей", "3 места, цветы у входа", "4 места, скамейка и фонарь" },
    };
    static readonly int[] WhCap = { 20, 60, 120, 250, 500, 800 };
    public static int MaxLv(int bk) => bk == 3 ? 1 : bk == 7 || bk == 8 || bk == 10 ? 3 : 5;

    public static int[][] BCost(int bk, int toLevel) => BCosts[bk][toLevel - 1];
    public bool BUnlocked(int bk) => BActive(bk) && bk switch
    {
        0 => HasFurnace, 1 => HasBuilding(Tl.Warehouse), 2 => LitCount >= 1, 3 => Villagers.Count >= 1, 4 => Has(Tech.AutoDrill), 5 => Has(Tech.Collector),
        6 => BpDone.Contains("bp_refinery"), 7 => BpDone.Contains("bp_hub"), 8 => BpDone.Contains("bp_stall"), 10 => Villagers.Count >= 1, _ => BpDone.Contains("bp_cryo")
    };
    public static string BLock(int bk) => bk switch
    {
        0 => "Постройте печь", 1 => "Постройте склад", 2 => "Зажгите древний фонарь", 3 => "Спасите жителя", 4 => "Изучите «Автобур» [T]", 5 => "Изучите «Коллектор» [T]",
        6 => "Чертёж «Цех» [J]", 7 => "Чертёж «Логистика» [J]", 8 => "Чертёж «Лавка» (даёт житель)", 10 => "Спасите жителя", _ => "Чертёж «Криоэкстрактор» [J]"
    };

    public bool HasBuilding(Tl t) { foreach (var b in Buildings) if (b.Type == t) return true; return false; }
    public int LevelAt(int x, int y) { foreach (var b in Buildings) if (b.X == x && b.Y == y) return b.Level; return 1; }
    public Building BuildingNear(Vector2 p, float r)
    {
        Building best = null; float bd = r;
        foreach (var b in Buildings) { float d = b.Pos.DistanceTo(p); if (d < bd) { bd = d; best = b; } }
        return best;
    }
    public int StockCap { get { int lv = 0; foreach (var b in Buildings) if (b.Type == Tl.Warehouse) lv = Math.Max(lv, b.Level); return WhCap[lv]; } }
    public int StockTotal { get { int n = 0; foreach (var x in Stock) n += x; return n; } }
    public static int BIndex(Tl t) => Array.IndexOf(BTypes, t);

    readonly HashSet<int> manualLoaded = new();

    /// <summary>Жители после загрузки: из сохранения (с постоянными Id) или, у старых сохранений, из спасённых мест и путников — с теми же Id, что получат новые игры.</summary>
    void InitVillage()
    {
        RemoveRetiredBuildings();
        foreach (var b in Buildings) if (manualLoaded.Contains(BKey(b))) b.Manual = true;
        var wh = Buildings.Find(b => b.Type == Tl.Warehouse);
        var home = wh != null ? wh.Pos + new Vector2(0, 16) : TileCenter(W.Spawn.X + 2, W.Spawn.Y);
        int rescued = 0;
        if (loadedVills != null)
        {
            foreach (var s in loadedVills) { var v = AddVillager(s.Id, home, s.Prof, s.Src, s.Name); if (v != null) { v.Work = s.Work; v.Home = s.Home; v.Talk = s.Talk; } }
            rescued = loadedVills.Length;
        }
        else
        {
            for (int i = 0; i < W.Pois.Count; i++) { var p = W.Pois[i]; if ((p.Kind == PK.Captive || p.Kind == PK.Survivor) && p.State == 3) { AddVillager(1000 + i, home + new Vector2(rescued * 10, 0), null, "poi"); rescued++; } }
            for (int i = 0; i < ExtraVillagers; i++) { AddVillager(5000 + i, home + new Vector2((rescued + i) * 10, 0), null, "traveler"); }
        }
        loadedVills = null;
        if (rescued > 0 && BpDone.Add("bp_stall")) { Frag["bp_stall"] = 1; }
        RecomputeVillagers();
        foreach (var v in Villagers)         // безопасное место после загрузки: у рабочего места или у дома/маяка
        {
            var wb = BuildingByKey(v.Work); var s = wb != null ? WorkSpot(wb) : IdleSpot(v, 3);
            if (s.X < 0) s = SafeSpot(); if (s.X >= 0) v.Pos = TileCenter(s.X, s.Y); v.NetPos = v.Pos;
        }
    }

    /// <summary>Старые сохранения: убранные типы построек исчезают, а вложенные в них ресурсы (цена всех уровней и накопленное внутри) возвращаются в инвентарь.</summary>
    void RemoveRetiredBuildings()
    {
        var gone = Buildings.FindAll(b => !BActive(BIndex(b.Type)));
        if (gone.Count == 0) return;
        foreach (var b in gone)
        {
            int bk = BIndex(b.Type);
            if (bk >= 0) for (int lv = 1; lv <= b.Level && lv <= BCosts[bk].Length; lv++) foreach (var c in BCost(bk, lv)) Inv[c[0]] += c[1];
            for (int i = 0; i < It.Count; i++) if (b.Local[i] > 0) Inv[i] += b.Local[i];
            Buildings.Remove(b); SetObj(b.X, b.Y, Tl.None);
        }
        foreach (var k in new List<string>(BuildStock.Keys)) if (k.StartsWith("bld") && int.TryParse(k.Substring(3), out var kb) && kb >= 0 && kb < BCount && !BActive(kb)) { foreach (var c in BCost(kb, 1)) Inv[c[0]] += c[1] * BuildStock[k]; BuildStock.Remove(k); }
        Toast($"Убрано построек: {gone.Count} - ресурсы возвращены");
    }

    VillSave[] SaveVillagers()
    {
        var a = new VillSave[Villagers.Count];
        for (int i = 0; i < a.Length; i++) { var v = Villagers[i]; a[i] = new VillSave { Id = v.Id, Name = v.Name, Prof = v.Prof, Src = v.Src, Work = v.Work, Home = v.Home, Talk = v.Talk, X = v.Pos.X, Y = v.Pos.Y }; }
        return a;
    }

    int[] SaveManual() { var l = new List<int>(); foreach (var b in Buildings) if (b.Manual) l.Add(BKey(b)); return l.ToArray(); }

    // ───────────────────────── строительство ─────────────────────────
    public void BuildBuilding(int bk)
    {
        if (!BUnlocked(bk)) { Fail($"{BNames[bk]} пока недоступна: {BLock(bk)}"); return; }
        if (!BActive(bk)) { Fail($"{BNames[bk]} больше недоступна"); return; }
        if (bk != 10 && HasBuilding(BTypes[bk])) { Fail($"{BNames[bk]} уже построена — улучшайте её [U]"); return; }
        if (bk == 10 && Buildings.FindAll(h => h.Type == Tl.House).Count >= 8) { Fail("Жилых домов уже достаточно (8)"); return; }
        var cost = BCost(bk, 1);
        if (!Can(cost)) { Fail($"{BNames[bk]}: нужно {CostStr(cost)}"); return; }
        var t = PlaceTarget(true); if (t == null) return;
        int reg = World.RegionOf(t.Value.X);
        if (bk == 4 && reg != 1) { Fail("Автобур: только в Затопленных шахтах"); return; }
        if (bk == 9 && reg != 3) { Fail("Криоэкстрактор: только в Ледяном монастыре"); return; }
        if (bk == 5 && reg != 2) { Fail("Коллектор: только в Пепельных пустошах"); return; }
        Pay(cost);
        if (NetClient) { Net?.SendBuildCmd(1, bk, t.Value.X, t.Value.Y); Burst(TileCenter(t.Value.X, t.Value.Y), new Color("#d9b27c"), 10, 50); Sfx.Play("build"); Toast("Постройка отправлена хосту"); return; }
        CreateBuilding(bk, t.Value);
    }

    /// <summary>Создание здания без оплаты: общий путь одиночной игры и хоста.</summary>
    Building CreateBuilding(int bk, Vector2I t)
    {
        var b = new Building { Type = BTypes[bk], X = t.X, Y = t.Y };
        Buildings.Add(b); SetObj(b.X, b.Y, b.Type); netT = 0;
        PlaceFx(t, null, 1.6f); Sfx.Play("build"); GiveXp(30);
        Announce($"Построено: {BNames[bk]}", BDesc[bk][0] + (bk == 7 ? " · дальше ставьте узлы цепочкой" : ""), new Color("#d9b27c"), "build");
        Save(); return b;
    }

    public bool MyBuildingUp;                            // личное: этот герой улучшал здание (для задания; в общем мире здание могли улучшить и другие)

    void UpgradeBuilding(Building b)
    {
        int bk = BIndex(b.Type);
        if (b.Level >= MaxLv(bk)) { Fail("Здание уже улучшено до максимума"); return; }
        var cost = BCost(bk, b.Level + 1);
        if (!Can(cost)) { Fail($"Улучшение: нужно {CostStr(cost)}"); return; }
        Pay(cost); MyBuildingUp = true;
        if (NetClient) { Net?.SendBuildCmd(2, BKey(b), b.Level, bk); Sfx.Play("build"); Toast("Улучшение отправлено хосту"); return; }
        b.Level++; SetObj(b.X, b.Y, b.Type); netT = 0;
        Fx("building_upgrade", b.Pos); GiveXp(40);
        if (b.Type == Tl.Stall) TradeEpoch++;
        Announce($"{BNames[bk]}: уровень {b.Level}", "Новое: " + BDesc[bk][b.Level - 1], new Color("#ffe27a"), "upgrade");
        Save();
    }

    void BuildingIx(Building b, Ix ix)
    {
        int bk = BIndex(b.Type);
        ix.Title = $"{BNames[bk]} — уровень {b.Level}";
        bool prod = IsProducer(b.Type);
        ix.Info($"{BNames[bk]} · ур.{b.Level}", TipRole.Text);
        if (prod)
        {
            var w = WorkerOf(b); var pd = w != null ? ProfDefs.Of(w.Prof) : null; var want = ProfDefs.ForBuilding(b.Type);
            ix.Info(w != null ? $"Работник: {w.Name} · {ProfTitle(w)}" : "Работник: нет" + (want != null ? $" (нужен: {want.Title})" : ""), TipRole.Text);
            if (w != null) ix.Info(pd != null && pd.BuildingTile == b.Type ? $"Бонус: ×{b.WorkMul:0.##} (своя профессия)" : $"Бонус: ×{b.WorkMul:0.##}", TipRole.Ok);
            ix.Info("[P] — сменить работника");
        }
        if (b.Type == Tl.House)
        {
            ix.Info($"Жильцы: {HomeCount(b)}/{HouseCap(b)}", TipRole.Text);
            foreach (var hv in Villagers) if (hv.Home == BKey(b)) ix.Info($"{hv.Name} · {ProfTitle(hv)}");
        }
        if (b.Type == Tl.Stall) { ix.Action("E", "Торговать"); ix.E = () => OpenTrade(null); }
        else if (b.Type == Tl.Forge) { ix.Action("E", "Кузня: создание и улучшение"); ix.E = () => OpenStation("forge"); }
        else
        {
            int tot = StockTotal + LocalTotal(b);
            if (tot > 0)
            {
                var parts = new List<string>(); int more = 0;
                for (int i = 0; i < It.Count; i++) { int n = Stock[i] + b.Local[i]; if (n <= 0) continue; if (parts.Count < 4) parts.Add($"{It.Ic(i)}{n}"); else more++; }
                ix.Action("E", "Забрать ресурсы"); ix.Gives("Получите", string.Join(" ", parts) + (more > 0 ? $" +{more}" : ""), TipRole.Resource); ix.E = () => CollectFrom(b);
            }
        }
        if (b.Level < MaxLv(bk))
        {
            var cost = BCost(bk, b.Level + 1);
            ix.Action("U", $"Улучшить до ур.{b.Level + 1}"); NeedCost(ix, cost); ix.U = () => UpgradeBuilding(b);
            if (b.Level < BDesc[bk].Length) ix.Gives("Новое", BDesc[bk][b.Level], TipRole.Zone);
        }
        else ix.Info("Максимальный уровень");
        ix.Action("X", "Разобрать — вернуть в запас"); ix.X = () => DemolishBuilding(b);
    }

    /// <summary>Пассивного производства в деревне больше нет: ресурсы добывает игрок.</summary>
    public static bool IsProducer(Tl t) => false;
    static int LocalTotal(Building b) { int n = 0; foreach (var x in b.Local) n += x; return n; }

    void CollectFrom(Building b)
    {
        if (NetClient) { Net?.SendBuildCmd(7, BKey(b), 0, 0); return; }
        int tot = 0, row = 0;
        for (int i = 0; i < It.Count; i++)
        {
            int n = Stock[i] + b.Local[i];
            if (n > 0) { Inv[i] += n; tot += n; Texts.Add(new FText { P = PPos + new Vector2(0, -12 - row++ * 13), S = $"{It.Ic(i)}+{n} {It.Names[i]}", Life = 1.2f, C = It.Col[i] }); Stock[i] = 0; b.Local[i] = 0; }
        }
        if (tot > 0) Sfx.Play("pickup");
    }

    // ───────────────────────── сеть и производство ─────────────────────────
    float whAuto, netT;

    /// <summary>Раз в секунду: жильё и назначения жителей.</summary>
    void RecomputeNetwork() => RecomputeVillagers();

    void UpdateVillage(float dt)
    {
        netT -= dt; if (netT <= 0) { netT = 1f; RecomputeNetwork(); }
        if (NetClient) { UpdateVillagers(dt); return; }              // клиент: производство считает хост
        var wh = Buildings.Find(x => x.Type == Tl.Warehouse);
        if (wh != null && wh.Level >= 3 && wh.Pos.DistanceTo(PPos) < (wh.Level >= 4 ? 200 : 96) && StockTotal > 0)
        {
            whAuto -= dt;
            if (whAuto <= 0) { whAuto = .25f; for (int i = 0; i < It.Count; i++) if (Stock[i] > 0 && i != It.Coins) { Stock[i]--; Inv[i]++; Texts.Add(new FText { P = PPos + new Vector2(rng.RandfRange(-8, 8), -12), S = "+1 " + It.Names[i], Life = .8f, C = It.Col[i] }); break; } }
        }

        UpdateVillagers(dt);
    }

    // ───────────────────────── открытия ─────────────────────────
    float unlockT;
    public List<Goal> Goals = new();
    public const long TutTechOpened = 1L << 60, TutExplored = 1L << 61;       // признаки героя: открывал дерево навыков, выходил в Тьму со светом
    static readonly string[] TutorialTitles = { "Собрать ресурсы на печь", "Построить печь [B]", "Создать кирку в кузне [U у печи]", "Изучить навык света [T]", "Исследовать Тьму" };

    void UpdateUnlocks(float dt)
    {
        unlockT -= dt; if (unlockT > 0) return; unlockT = .4f;
        string[] hint = { "Хранилище и центр производства", "Производит дерево само", "Добывает руду сама", "Плавит руду со склада", "Добывает серебро и кристаллы", "Добывает обсидиан и уголь пепла",
            "Шестерни и линзы из слитков [B]", "Подключает здания к складу [B]", "Торговля и покупка чертежей [B]", "Добывает ледяной кристалл [B]", "Жильё для жителей [B]" };
        for (int bk = 0; bk < BCount; bk++)
            if ((Ann & (1L << bk)) == 0 && BUnlocked(bk))
            {
                Ann |= 1L << bk;
                Announce($"Открыта постройка: {BNames[bk]}", hint[bk], new Color("#d9b27c"), "tech");
            }
        for (int i = 0; i < Techs.Length; i++)
            if (!Techs[i].Reward && (Ann & (1L << (12 + i))) == 0 && Upgrades.IsPageUnlocked(Techs[i].Page) && Techs[i].Ok(this))
            {
                Ann |= 1L << (12 + i);                                      // отмечаем без сообщения: список улучшений и цели сами показывают, что доступно
            }
        if (Has(Tech.Spark) && (Ann & TutExplored) == 0 && PPos.DistanceTo(W.Ancients[0].Pos) > AncientR(W.Ancients[0]) + 12f) Ann |= TutExplored;
        Goals = BuildGoals();                             // у каждого игрока свои задания: по его инвентарю, оружию и навыкам, а мир (постройки, фонари, стражи) общий
    }
}
