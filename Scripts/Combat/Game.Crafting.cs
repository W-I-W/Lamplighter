using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lamplighter;

/// <summary>Рецепт единого окна крафта. Group: hand — ручной крафт (без станции); furnace/forge — рецепты станции, доступны только рядом с ней.</summary>
public sealed class Recipe
{
    public string Id, Group, Name, Desc; public Func<string> NameFn; public string Title => NameFn?.Invoke() ?? Name; public int Icon = -1; public int[][] Cost; public Func<int[][]> CostFn; public bool Multi;
    public int[][] CostNow => CostFn != null ? CostFn() : Cost;
    public Func<bool> Known, Done; public Action<int> Craft; public Func<int> Extra;      // Extra — число уже стоящих в очереди
}

/// <summary>Состояние рецепта: изучен и хватает материалов / не хватает / уже есть. Неизученные рецепты в списке не показываются.</summary>
public enum RecState { Ready, Missing, Owned }

/// <summary>Единое окно крафта: ручной крафт + динамическая группа ближайшей станции. Использует прежние функции крафта оружия, модификаторов, улучшения кирки и плавки.</summary>
public partial class Game
{
    public string CraftStation;                          // "furnace" / "forge" / null — станция, с которой взаимодействует игрок
    public int CraftQty;                                 // 0..3 → ×1, ×5, ×10, максимум
    public bool StationOpened;                           // окно открыто через [E] у станции: только тогда её рецепты видны и доступны
    public bool CraftOnlyReady;                          // фильтр «Доступные»
    public string CraftMode = "hand";                    // показанная группа: ручной крафт или рецепты открытой станции
    public float StationFlashT; string lastStation;
    public static readonly int[] QtySteps = { 1, 5, 10, int.MaxValue };

    public static string GroupName(string g) => g == "hand" ? "Ручной крафт" : g == "furnace" ? "Печь" : "Кузница";

    /// <summary>Ближайшая станция в радиусе взаимодействия.</summary>
    public string StationNear()
    {
        bool f = FurnaceNear(56, out var fa); var b = BuildingNear(PPos, 56); bool forge = b != null && b.Type == Tl.Forge;
        if (f && forge) return CraftStation == "forge" ? "forge" : "furnace";
        return f ? "furnace" : forge ? "forge" : null;
    }

    /// <summary>Станция, рецептами которой можно пользоваться сейчас (вышли из радиуса — группа пропадает).</summary>
    public string ActiveStation() { var s = StationOpened ? StationNear() : null; if (s != CraftStation) CraftStation = s; if (s != lastStation) { lastStation = s; if (s != null) StationFlashT = 1.6f; } return s; }

    public void OpenStation(string station)
    {
        CraftStation = station; StationOpened = true; OpenWin(Win.Equip); WinTab = 2; WinCur = 0; StationFlashT = 1.6f; lastStation = station; CraftMode = station;
    }

    List<Recipe> recipes;
    void BuildRecipes()
    {
        recipes = new();
        int[][] C(params int[] a) { var l = new int[a.Length / 2][]; for (int i = 0; i < l.Length; i++) l[i] = new[] { a[i * 2], a[i * 2 + 1] }; return l; }
        // ручной крафт: инструменты
        foreach (var (id, cost) in new[] { ("axe", C(It.Wood, 5, It.Stone, 3)) })
        {
            var td = ToolDef.Get(id); string tid = id; var tc = cost;
            recipes.Add(new Recipe { Id = "tool_" + id, Group = "hand", Name = td.Name, Desc = td.Desc, Icon = td.Icon, Cost = tc, Known = () => true, Done = () => OwnedTools.Contains(tid), Craft = n => CraftTool(tid, tc) });
        }
        // печь: плавка слитков (очередь общая и работает после закрытия окна)
        foreach (var ore in new[] { It.CopperOre, It.IronOre, It.SilverOre })
        {
            int o = ore, ingot = It.IngotOf(ore);
            recipes.Add(new Recipe { Id = "smelt_" + ingot, Group = "furnace", Name = It.Names[ingot], Desc = $"1 {It.Names[o].ToLowerInvariant()} + 1 дерево → слиток. Плавка идёт и после закрытия окна", Icon = ingot, Cost = C(o, 1, It.Wood, 1), Multi = true,
                Known = () => true, Done = () => false, Extra = () => SmeltQ.FindAll(x => x == o).Count, Craft = n => SmeltOre(o, n) });
        }
        // печь и кузница: улучшение кирки, оружие, модификаторы (как раньше требовали печь или кузницу рядом)
        foreach (var g in new[] { "furnace", "forge" })
        {
            string grp = g;
            recipes.Add(new Recipe { Id = "pick_" + g, Group = g, Name = "Улучшение кирки", Desc = "Новая кирка добывает быстрее и бьёт сильнее", Icon = UiIcons.Pickaxe, Known = () => Tier < 4, Done = () => false,
                CostFn = () => Tier < 4 ? CostPick[Tier] : null, Craft = n => UpgradePick() });
            foreach (var wdef in Db.Weapons)
            {
                var ud = wdef; recipes.Add(new Recipe { Id = $"up_{ud.Id}_{g}", Group = g, Name = "Улучшение: " + ud.Name, NameFn = () => $"{Loc.T("Улучшение")}: {Loc.T(ud.Name)} +{WeaponLv(ud.Id) + 1}", Desc = "+10% урона за уровень. Улучшать можно только рядом с печью или кузницей", Icon = HotIcon(RefWeapon(ud.Id)),
                    Known = () => HasWeapon(ud.Id) && WeaponLv(ud.Id) < WeaponMaxLv, Done = () => false, CostFn = () => WeaponLv(ud.Id) < WeaponMaxLv ? WeaponUpCost(ud.Tier, WeaponLv(ud.Id) + 1) : null, Craft = n => UpgradeWeapon(Owned.FindIndex(o => o.Id == ud.Id)) });
            }
            recipes.Add(new Recipe { Id = "up_axe_" + g, Group = g, Name = "Улучшение топора", NameFn = () => $"{Loc.T("Улучшение топора")} +{AxeLv + 1}", Desc = "+30% к добыче и +10% к урону за уровень", Icon = ToolDef.Get("axe").Icon,
                Known = () => OwnedTools.Contains("axe") && AxeLv < AxeMaxLv, Done = () => false, CostFn = () => AxeLv < AxeMaxLv ? AxeUpCost(AxeLv + 1) : null, Craft = n => UpgradeAxe() });
            foreach (var w in CraftableWeapons())
            {
                var wd = w; recipes.Add(new Recipe { Id = $"w_{wd.Id}_{g}", Group = g, Name = wd.Name, Desc = wd.Desc, Icon = HotIcon(RefWeapon(wd.Id)), Cost = wd.Cost, Known = () => BpOk(wd.Bp), Done = () => HasWeapon(wd.Id), Craft = n => CraftWeapon(wd) });
            }
            for (int m = 0; m < Db.Mods.Length; m++)
            {
                int mi = m; var md = Db.Mods[m];
                recipes.Add(new Recipe { Id = $"m_{mi}_{g}", Group = g, Name = "Вставка: " + md.Name, Desc = md.Desc, Icon = It.Relic, Cost = md.Cost, Multi = true, Known = () => BpOk(md.Bp), Done = () => false, Craft = n => { for (int i = 0; i < n; i++) if (!CraftMod(mi)) break; } });
            }
        }
    }

    /// <summary>Рецепты группы, которые игрок знает (изученные). Группы станции — только для активной станции.</summary>
    public List<Recipe> RecipesOf(string group)
    {
        if (recipes == null) BuildRecipes();
        var l = new List<Recipe>(); foreach (var r in recipes) if (r.Group == group && r.Known()) l.Add(r); return l;
    }

    public List<string> CraftGroups() { var l = new List<string> { "hand" }; var s = ActiveStation(); if (s != null) l.Add(s); return l; }

    /// <summary>Группа, рецепты которой показаны сейчас: выбранный режим, если он ещё доступен (станция открыта и рядом), иначе ручной крафт.</summary>
    public string CraftGroup() { var g = CraftGroups(); if (!g.Contains(CraftMode)) CraftMode = "hand"; return CraftMode; }

    /// <summary>Рецепты показанной группы: изученные, доступные первыми, затем нехватка, затем уже имеющиеся; фильтр «Доступные» оставляет только Ready.</summary>
    public List<(string group, Recipe r)> CraftRows()
    {
        string g = CraftGroup(); var rows = new List<(string, Recipe)>();
        foreach (var r in RecipesOf(g).OrderBy(x => StateOf(x) == RecState.Ready ? 0 : StateOf(x) == RecState.Missing ? 1 : 2)) if (!CraftOnlyReady || StateOf(r) == RecState.Ready) rows.Add((g, r));
        return rows;
    }

    string craftSelId; int craftCurSeen = -1;
    public void CraftResetSel() { craftSelId = null; craftCurSeen = -1; }
    /// <summary>Выбор в сетке привязан к рецепту, а не к номеру клетки: когда порядок меняется (ресурсы добыты, предмет создан), выбранный рецепт остаётся выбранным. При открытии выбирается первый.</summary>
    public int CraftSync(List<(string group, Recipe r)> rows)
    {
        if (rows.Count == 0) { WinCur = 0; craftSelId = null; craftCurSeen = -1; return -1; }
        if (WinCur != craftCurSeen || craftSelId == null) { WinCur = Math.Clamp(WinCur, 0, rows.Count - 1); craftSelId = rows[WinCur].r.Id; }
        else { int i = rows.FindIndex(x => x.r.Id == craftSelId); WinCur = i >= 0 ? i : Math.Clamp(WinCur, 0, rows.Count - 1); craftSelId = rows[WinCur].r.Id; }
        craftCurSeen = WinCur; return WinCur;
    }

    public RecState StateOf(Recipe r) { var c = r.CostNow; return r.Done() ? RecState.Owned : (c == null || Can(c)) ? RecState.Ready : RecState.Missing; }

    /// <summary>Сколько штук можно сделать из имеющихся материалов.</summary>
    public int MaxCraft(Recipe r)
    {
        var cost = r.CostNow; if (cost == null) return 1; int n = int.MaxValue;
        foreach (var c in cost) n = Math.Min(n, Inv[c[0]] / Math.Max(1, c[1]));
        return n == int.MaxValue ? 0 : n;
    }

    /// <summary>Выполняет рецепт: ручной — сразу, станции — только пока игрок рядом с ней.</summary>
    public void CraftRecipe(Recipe r)
    {
        if (r.Group != "hand" && ActiveStation() != r.Group) { Fail("Рецепты станции — пока вы рядом"); return; }
        if (StateOf(r) == RecState.Owned) { Toast("Это уже есть"); return; }
        CraftRecipe(r, r.Multi ? Math.Min(QtySteps[CraftQty], Math.Max(1, MaxCraft(r))) : 1);
    }

    /// <summary>Создать qty штук (для рецептов с количеством); расход и очередь решает сам рецепт.</summary>
    public void CraftRecipe(Recipe r, int qty)
    {
        if (r.Group != "hand" && ActiveStation() != r.Group) { Fail("Рецепты станции — пока вы рядом"); return; }
        if (StateOf(r) == RecState.Owned) { Toast("Это уже есть"); return; }
        qty = r.Multi ? Math.Clamp(qty, 1, Math.Max(1, MaxCraft(r))) : 1;
        var rc = r.CostNow; if (rc != null && !Can(rc)) { Fail("Не хватает: " + NeedStr(rc)); return; }
        r.Craft(qty);
    }

    /// <summary>Плавка: ставит в очередь печи qty руды (1 руда + 1 дерево на слиток); очередь живёт сама и сохраняется.</summary>
    void SmeltOre(int ore, int qty)
    {
        int loaded = 0; FurnaceNear(56, out var at);
        while (loaded < qty && Inv[ore] > 0 && Inv[It.Wood] > 0) { Inv[ore]--; Inv[It.Wood]--; SmeltQ.Add(ore); loaded++; }
        if (loaded == 0) { Fail("Нужна руда и дерево (топливо): по 1 дереву на слиток"); return; }
        SmeltAt = at; Sfx.Play("build", -4); Toast($"Плавка: {loaded} шт.");
    }
}
