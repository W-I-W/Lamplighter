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
        new[] { "хранит 60 каждого ресурса, связь 14", "хранит 120, связь 18", "хранит 250, связь 24, передаёт ресурсы вам рядом", "хранит 500, связь 32", "хранит 800, связь 40" },
        new[] { "2 дерева / 6 с", "2 дерева / 4 с", "3 дерева / 3 с", "4 дерева / 2,2 с", "5 деревьев / 1,6 с" },
        new[] { "камень и медная руда / 6 с", "+ железная руда / 5 с", "+ осколки света / 3,6 с", "+ серебряная руда / 2,8 с", "очень быстро / 2 с" },
        new[] { "плавит руду со склада / 4 с", "плавка / 2,5 с", "плавка / 1,5 с", "плавка / 1 с", "плавка / 0,7 с" },
        new[] { "серебряная руда и кристаллы / 12 с", "быстрее / 9 с", "быстрее / 6 с", "очень быстро / 4,5 с", "молниеносно / 3 с" },
        new[] { "обсидиан и уголь пепла / 12 с", "быстрее / 9 с", "быстрее / 6 с", "очень быстро / 4,5 с", "молниеносно / 3 с" },
        new[] { "делает шестерни и линзы / 8 с", "быстрее / 6 с", "быстрее / 4,5 с", "быстрее / 3,5 с", "быстрее / 2,5 с" },
        new[] { "связь 14: подключает здания к складу", "связь 20", "связь 28" },
        new[] { "торговля: базовые товары, чертежи", "торговля: редкие материалы и вставки", "торговля: реликвии и снаряжение" },
        new[] { "ледяной кристалл и серебро / 12 с", "быстрее / 9 с", "быстрее / 6 с", "очень быстро / 4,5 с", "молниеносно / 3 с" },
        new[] { "2 места для жителей", "3 места, цветы у входа", "4 места, скамейка и фонарь" },
    };
    static readonly float[][] BInterval = { null, new[] { 6f, 4f, 3f, 2.2f, 1.6f }, new[] { 6f, 5f, 3.6f, 2.8f, 2f }, new[] { 4f, 2.5f, 1.5f, 1f, .7f }, new[] { 12f, 9f, 6f, 4.5f, 3f }, new[] { 12f, 9f, 6f, 4.5f, 3f }, new[] { 8f, 6f, 4.5f, 3.5f, 2.5f }, null, null, new[] { 12f, 9f, 6f, 4.5f, 3f }, null };
    static readonly int[] WhCap = { 20, 60, 120, 250, 500, 800 };
    static readonly int[] WhLink = { 14, 18, 24, 32, 40 };
    static readonly int[] HubLink = { 14, 20, 28 };
    public static readonly string[][] BModes =
    {
        null, new[] { "Авто" }, new[] { "Авто", "Камень", "Медная руда", "Железная руда", "Серебряная руда", "Осколки света" },
        new[] { "Авто", "Медь", "Железо", "Серебро" }, new[] { "Авто", "Серебряная руда", "Кристаллы" }, new[] { "Авто", "Обсидиан", "Уголь пепла" },
        new[] { "Авто", "Шестерни", "Линзы" }, null, null, new[] { "Авто", "Ледяной кристалл", "Серебряная руда" }, null
    };
    public static readonly string[] PrioNames = { "Высокий", "Обычный", "Низкий" };
    public static float BIntervalOf(int bk, int lv) => BInterval[bk] != null ? BInterval[bk][lv - 1] : 1f;
    public static int MaxLv(int bk) => bk == 7 || bk == 8 || bk == 10 ? 3 : 5;

    public static int[][] BCost(int bk, int toLevel) => BCosts[bk][toLevel - 1];
    public bool BUnlocked(int bk) => bk switch
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
        Burst(b.Pos, new Color("#d9b27c"), 16, 70); Sfx.Play("build"); GiveXp(30);
        Announce($"Построено: {BNames[bk]}", BDesc[bk][0] + (bk == 7 ? " · дальше ставьте узлы цепочкой" : ""), new Color("#d9b27c"), "build");
        Save(); return b;
    }

    void UpgradeBuilding(Building b)
    {
        int bk = BIndex(b.Type);
        if (b.Level >= MaxLv(bk)) { Fail("Здание уже улучшено до максимума"); return; }
        var cost = BCost(bk, b.Level + 1);
        if (!Can(cost)) { Fail($"Улучшение: нужно {CostStr(cost)}"); return; }
        Pay(cost);
        if (NetClient) { Net?.SendBuildCmd(2, BKey(b), 0, 0); Sfx.Play("build"); Toast("Улучшение отправлено хосту"); return; }
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
        if (prod && !b.Off && b.Status != 0 && b.Status != 4) ix.Warn("Остановлено: " + StatusText(b.Status));
        if (!(b.Linked || b.Forced) && b.Type != Tl.Warehouse) ix.Warn("Нет связи со складом: ставьте узлы снабжения [B]");
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
        else ix.Info("Максимальный уровень · [P] управление");
        ix.Action("X", "Разобрать — вернуть в запас"); ix.X = () => DemolishBuilding(b);
    }

    public static bool IsProducer(Tl t) => t == Tl.Sawmill || t == Tl.Mine || t == Tl.Forge || t == Tl.Drill || t == Tl.Collector || t == Tl.Refinery || t == Tl.Cryo;
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
    float whAuto, ambT, netT;
    List<Building> order = new();

    float NodeRange(Building b) => b.Type == Tl.Warehouse ? WhLink[b.Level - 1] * TS : b.Type == Tl.Hub ? HubLink[b.Level - 1] * TS : 0f;

    /// <summary>Раз в секунду: какие здания подключены к складу через узлы; порядок обработки по приоритету.</summary>
    void RecomputeNetwork()
    {
        var nodes = Buildings.FindAll(b => b.Type == Tl.Warehouse || b.Type == Tl.Hub);
        var conn = new List<Building>(); var q = new Queue<Building>();
        foreach (var n in nodes) if (n.Type == Tl.Warehouse) { conn.Add(n); q.Enqueue(n); }
        while (q.Count > 0)
        {
            var c = q.Dequeue();
            foreach (var n in nodes) if (!conn.Contains(n) && n.Pos.DistanceTo(c.Pos) <= Mathf.Max(NodeRange(c), NodeRange(n))) { conn.Add(n); q.Enqueue(n); }
        }
        foreach (var b in Buildings)
        {
            bool l = b.Forced;
            if (!l) foreach (var n in conn) if (n.Pos.DistanceTo(b.Pos) <= NodeRange(n) + 1) { l = true; break; }
            b.Linked = l;
        }
        order = new List<Building>(Buildings); order.Sort((a, b) => a.Prio.CompareTo(b.Prio));
        RecomputeVillagers();
    }

    void Put(Building b, int item, int n)
    {
        if (b.Linked || b.Forced) { int add = Math.Min(n, StockCap - Stock[item]); if (add > 0) Stock[item] += add; }
        else { int add = Math.Min(n, 8 - b.Local[item]); if (add > 0) b.Local[item] += add; }
    }
    bool Room(Building b, int item) => (b.Linked || b.Forced) ? Stock[item] < StockCap : b.Local[item] < 8;
    int Have(int item) => Stock[item];

    void UpdateVillage(float dt)
    {
        netT -= dt; if (netT <= 0) { netT = 1f; RecomputeNetwork(); }
        ambT -= dt;
        if (NetClient) { UpdateVillagers(dt); return; }              // клиент: производство считает хост
        foreach (var b in order)
        {
            if (!IsProducer(b.Type)) continue;
            int bk = BIndex(b.Type);
            if (b.Off) { b.Status = 4; continue; }
            bool linked = b.Linked || b.Forced;
            float rate = b.WorkMul * ProdMul * (b.Type == Tl.Forge && Has(Tech.ForgeHeat) ? 1f + E(Tech.ForgeHeat) : 1f);
            float interval = BInterval[bk][b.Level - 1];
            bool working = false;
            switch (b.Type)
            {
                case Tl.Sawmill:
                    if (!Room(b, It.Wood)) { b.Status = 2; break; }
                    b.Status = linked ? 0 : 3; working = true; b.Prog += dt * rate;
                    if (b.Prog >= interval) { b.Prog = 0; Produce(It.Wood, b.Level <= 2 ? 2 : b.Level == 3 ? 3 : b.Level == 4 ? 4 : 5, b); }
                    break;
                case Tl.Mine:
                    b.Status = linked ? 0 : 3; working = true; b.Prog += dt * rate;
                    if (b.Prog >= interval)
                    {
                        b.Prog = 0;
                        int item;
                        if (b.Mode == 0) { int cyc = b.Level == 1 ? 2 : b.Level == 2 ? 3 : b.Level == 3 ? 4 : 5; int step = b.Count++ % cyc; item = step == 0 ? It.Stone : step == 1 ? It.CopperOre : step == 2 ? It.IronOre : step == 3 ? It.Shard : It.SilverOre; }
                        else item = b.Mode switch { 1 => It.Stone, 2 => It.CopperOre, 3 => It.IronOre, 4 => It.SilverOre, _ => It.Shard };
                        bool allowed = b.Mode == 0 || (b.Mode == 3 && b.Level >= 2) || (b.Mode == 5 && b.Level >= 3) || (b.Mode == 4 && b.Level >= 4) || b.Mode == 1 || b.Mode == 2;
                        if (!allowed) { b.Status = 5; break; }
                        if (!Room(b, item)) { b.Status = 2; if (b.Mode == 0) b.Count--; } else Produce(item, b.Level >= 5 ? 3 : b.Level >= 3 ? 2 : 1, b);
                    }
                    break;
                case Tl.Drill:
                    b.Status = linked ? 0 : 3; working = true; b.Prog += dt * rate;
                    if (b.Prog >= interval)
                    {
                        b.Prog = 0; int item = b.Mode == 1 ? It.SilverOre : b.Mode == 2 ? It.Crystal : ((b.Count++ % 3) == 2 ? It.Crystal : It.SilverOre);
                        if (!Room(b, item)) { b.Status = 2; if (b.Mode == 0) b.Count--; } else Produce(item, b.Level >= 3 ? 2 : 1, b);
                    }
                    break;
                case Tl.Cryo:
                    b.Status = linked ? 0 : 3; working = true; b.Prog += dt * rate;
                    if (b.Prog >= interval)
                    {
                        b.Prog = 0; int item = b.Mode == 2 ? It.SilverOre : b.Mode == 1 ? It.Frost : ((b.Count++ % 3) == 2 ? It.SilverOre : It.Frost);
                        if (!Room(b, item)) { b.Status = 2; if (b.Mode == 0) b.Count--; } else Produce(item, b.Level >= 3 ? 2 : 1, b);
                    }
                    break;
                case Tl.Collector:
                    b.Status = linked ? 0 : 3; working = true; b.Prog += dt * rate;
                    if (b.Prog >= interval)
                    {
                        b.Prog = 0; int item = b.Mode == 1 ? It.Obsidian : b.Mode == 2 ? It.Ember : ((b.Count++ % 2 == 0) ? It.Obsidian : It.Ember);
                        if (!Room(b, item)) { b.Status = 2; if (b.Mode == 0) b.Count--; } else Produce(item, b.Level >= 3 ? 2 : 1, b);
                    }
                    break;
                case Tl.Forge:
                    {
                        if (!linked) { b.Status = 3; break; }
                        int ore = -1;
                        int[] order2 = b.Mode == 1 ? new[] { It.CopperOre } : b.Mode == 2 ? new[] { It.IronOre } : b.Mode == 3 ? new[] { It.SilverOre } : new[] { It.CopperOre, It.IronOre, It.SilverOre };
                        foreach (var o in order2) if (Stock[o] > 0) { ore = o; break; }
                        int ingot = ore < 0 ? -1 : It.IngotOf(ore);
                        if (ore < 0 || Stock[It.Wood] < 1) { b.Status = 1; break; }
                        if (Stock[ingot] >= StockCap) { b.Status = 2; break; }
                        b.Status = 0; working = true; b.Prog += dt * rate;
                        if (b.Prog >= interval) { b.Prog = 0; Stock[ore]--; Stock[It.Wood]--; Produce(ingot, 1, b); GiveXp(.5f); ForgeMilestone(); }
                        break;
                    }
                case Tl.Refinery:
                    {
                        if (!linked) { b.Status = 3; break; }
                        // рецепты: шестерня = 2 медных + 1 железный слиток; линза = 1 кристалл + 1 серебряный слиток
                        bool canGear = Stock[It.Copper] >= 2 && Stock[It.Iron] >= 1 && Stock[It.Gear] < StockCap;
                        bool canLens = Stock[It.Crystal] >= 1 && Stock[It.Silver] >= 1 && Stock[It.Lens] < StockCap;
                        int rec = b.Mode == 1 ? (canGear ? 1 : -1) : b.Mode == 2 ? (canLens ? 2 : -1) : (Stock[It.Gear] <= Stock[It.Lens] * 2 && canGear ? 1 : canLens ? 2 : canGear ? 1 : -1);
                        if (rec < 0) { b.Status = 1; break; }
                        b.Status = 0; working = true; b.Prog += dt * rate;
                        if (b.Prog >= interval)
                        {
                            b.Prog = 0;
                            if (rec == 1) { Stock[It.Copper] -= 2; Stock[It.Iron]--; Produce(It.Gear, 1, b); } else { Stock[It.Crystal]--; Stock[It.Silver]--; Produce(It.Lens, 1, b); }
                            GiveXp(1f);
                        }
                        break;
                    }
            }
            if (working && ambT <= 0 && rng.Randf() < .5f)
            {
                var col = b.Type == Tl.Forge || b.Type == Tl.Collector || b.Type == Tl.Refinery ? new Color("#ff9a2e") : b.Type == Tl.Sawmill ? new Color("#d9b27c") : b.Type == Tl.Drill ? new Color("#bfe9ff") : new Color("#9aa0ae");
                Parts.Add(new Particle { P = b.Pos + new Vector2(b.Type == Tl.Forge ? 4 : rng.RandfRange(-5, 5), b.Type == Tl.Forge ? -8 : 2), V = new Vector2(rng.RandfRange(-8, 8), -rng.RandfRange(10, 24)), Life = .7f, Max = .7f, C = col, Size = 1.5f });
            }
        }
        if (ambT <= 0) ambT = .25f;

        var wh = Buildings.Find(x => x.Type == Tl.Warehouse);
        if (wh != null && wh.Level >= 3 && wh.Pos.DistanceTo(PPos) < (wh.Level >= 4 ? 200 : 96) && StockTotal > 0)
        {
            whAuto -= dt;
            if (whAuto <= 0) { whAuto = .25f; for (int i = 0; i < It.Count; i++) if (Stock[i] > 0 && i != It.Coins) { Stock[i]--; Inv[i]++; Texts.Add(new FText { P = PPos + new Vector2(rng.RandfRange(-8, 8), -12), S = "+1 " + It.Names[i], Life = .8f, C = It.Col[i] }); break; } }
        }

        UpdateVillagers(dt);
    }

    void Produce(int item, int n, Building b)
    {
        int before = (b.Linked || b.Forced) ? Stock[item] : b.Local[item];
        Put(b, item, n);
        int add = ((b.Linked || b.Forced) ? Stock[item] : b.Local[item]) - before; if (add <= 0) return;
        Texts.Add(new FText { P = b.Pos + new Vector2(0, -10), S = "+" + add, Life = .9f, C = It.Col[item] });
        Sfx.Play("tick", -10, 1.2f);
    }

    // ───────────────────────── открытия ─────────────────────────
    float unlockT;
    public List<Goal> Goals = new();
    public const long TutTechOpened = 1L << 60, TutExplored = 1L << 61;       // признаки героя: открывал дерево навыков, выходил в Тьму со светом
    static readonly string[] TutorialTitles = { "Собрать первые ресурсы", "Открыть навыки [T]", "Изучить «Искру света»", "Исследовать Тьму" };
    string lastTutorial = "";

    /// <summary>Начальная цель одной короткой строкой в чат: только когда сменилась цель обучения.</summary>
    void TutorialChat()
    {
        string t = Goals.Count > 0 && Array.IndexOf(TutorialTitles, Goals[0].Title) >= 0 ? Goals[0].Title : "";
        if (t != "" && t != lastTutorial) Notify.Post(MsgKind.System, MsgTone.Cyan, Loc.T("Цель") + ": " + Loc.T(t));
        lastTutorial = t;
    }

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
                Ann |= 1L << (12 + i);
                Announce("Доступно улучшение: " + Techs[i].Name, "Меню [T], вкладка «" + TechPages[Techs[i].Page] + "»", new Color("#8ff0ff"), "tech");
            }
        if (Has(Tech.Spark) && (Ann & TutExplored) == 0 && PPos.DistanceTo(W.Ancients[0].Pos) > AncientR(W.Ancients[0]) + 12f) Ann |= TutExplored;
        Goals = BuildGoals();                             // у каждого игрока свои задания: по его инвентарю, оружию и навыкам, а мир (постройки, фонари, стражи) общий
        TutorialChat();
    }
}
