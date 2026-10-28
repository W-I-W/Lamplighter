using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Рецепт единого окна крафта. Group: hand — ручной крафт (без станции); furnace/forge — рецепты станции, доступны только рядом с ней.</summary>
public sealed class Recipe
{
    public string Id, Group, Name, Desc; public int Icon = -1; public int[][] Cost; public Func<int[][]> CostFn; public bool Multi;
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
    public readonly HashSet<string> CraftFold = new();   // свёрнутые группы
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
    public string ActiveStation() { var s = StationNear(); if (s != CraftStation) CraftStation = s; if (s != lastStation) { lastStation = s; if (s != null) StationFlashT = 1.6f; } return s; }

    public void OpenStation(string station)
    {
        CraftStation = station; OpenWin(Win.Equip); WinTab = 2; WinCur = 0; StationFlashT = 1.6f; lastStation = station;
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

    /// <summary>Плоский список строк окна: заголовок группы, затем её рецепты (если группа не свёрнута).</summary>
    public List<(string group, Recipe r)> CraftRows()
    {
        var rows = new List<(string, Recipe)>();
        foreach (var g in CraftGroups()) { rows.Add((g, null)); if (!CraftFold.Contains(g)) foreach (var r in RecipesOf(g)) rows.Add((g, r)); }
        return rows;
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
        int qty = r.Multi ? Math.Min(QtySteps[CraftQty], Math.Max(1, MaxCraft(r))) : 1;
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
