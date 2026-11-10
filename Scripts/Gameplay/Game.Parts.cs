using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Детали инструментов и оружия. Кузница (отдельная постройка, [E]) делает только рабочие части: бойки кирок, лезвие топора, клинки, плечи луков, навершия посохов.
/// Рабочий стол делает доски (из дерева) и рукоять (из 2 досок) и собирает предмет: рукоять + рабочая часть = кирка, топор или оружие.
/// Детали лежат в запасе (как постройки) под ключами «part_…»: занимают слоты сумки, переносятся в ячейки крафта, сохраняются вместе с героем.</summary>
public sealed class PartDef { public string Key, Name; public int Icon; public int[][] Cost; public int Tier = -1; public string Weapon; }

/// <summary>Рецепт Рабочего стола: компоненты (ресурсы или детали — id предмета инвентаря), результат в ячейке (постройка/деталь) или сразу выданный предмет (инструмент, оружие).</summary>
public sealed class BenchRecipe
{
    public string Key, Name; public Texture2D Icon; public int[][] Cost; public BuildEntry Build;
    public int ResultItem = -1, ResultN = 1;          // ResultItem — что ляжет в ячейку результата; -1 — предмет выдаётся сразу (Give)
    public Func<bool> Give; public string Kind = "Рецепт";
}

public partial class Game
{
    public const int PartIconBase = 34;
    public const string PartPlank = "part_plank", PartHandle = "part_handle", PartAxe = "part_axe";
    public static string PartPick(int tier) => "part_pick" + tier;
    public static string PartWeapon(string id) => "part_w_" + id;
    static readonly string[] PickHeadNames = { "Каменный боёк", "Медный боёк", "Железный боёк", "Кристальный боёк", "Обсидиановый боёк" };
    public static readonly int[][] CostPickHead0 = { new[] { It.Stone, 4 } };
    public static readonly int[][] CostAxeHead = { new[] { It.Stone, 3 } };
    public static readonly int[][] CostPlank = { new[] { It.Wood, 1 } };
    public const int PlanksPerWood = 2, PlanksPerHandle = 2;

    static string WeaponPartName(WClass c) => c switch { WClass.Bow => "Плечи лука", WClass.Staff => "Навершие посоха", _ => "Клинок" };
    static int WeaponPartIcon(WClass c) => PartIconBase + (c == WClass.Bow ? 9 : c == WClass.Staff ? 10 : 8);

    List<PartDef> partDefs;
    /// <summary>Все детали: доска, рукоять, бойки кирок (5 тиров), лезвие топора, рабочие части оружия (по одной на оружие с ценой).</summary>
    public List<PartDef> PartDefs()
    {
        if (partDefs != null) return partDefs;
        partDefs = new()
        {
            new PartDef { Key = PartPlank, Name = "Доска", Icon = PartIconBase },
            new PartDef { Key = PartHandle, Name = "Рукоять", Icon = PartIconBase + 1 },
        };
        for (int t = 0; t <= 4; t++) partDefs.Add(new PartDef { Key = PartPick(t), Name = PickHeadNames[t], Icon = PartIconBase + 2 + t, Cost = t == 0 ? CostPickHead0 : CostPick[t - 1], Tier = t });
        partDefs.Add(new PartDef { Key = PartAxe, Name = "Лезвие топора", Icon = PartIconBase + 7, Cost = CostAxeHead });
        foreach (var w in CraftableWeapons()) partDefs.Add(new PartDef { Key = PartWeapon(w.Id), Name = WeaponPartName(w.Cls), Icon = WeaponPartIcon(w.Cls), Cost = w.Cost, Weapon = w.Id });
        return partDefs;
    }

    public PartDef PartOf(string key) => key != null && key.StartsWith("part_") ? PartDefs().Find(p => p.Key == key) : null;
    internal bool IsPartItem(int item) => item <= -2 && PartOf(InventoryKey(item)) != null;
    internal int PartItem(string key) => InventoryItemId(key);

    /// <summary>Название детали; у рабочей части оружия — с названием оружия («Клинок: Меч путника»), обе части переводятся отдельно.</summary>
    internal string PartName(PartDef p) => p.Weapon != null && Db.Weapon(p.Weapon) is { } w ? Loc.T(p.Name) + ": " + Loc.T(w.Name) : Loc.T(p.Name);

    /// <summary>Сколько предмета (ресурса или детали) есть у героя.</summary>
    internal int HaveItem(int item) => item >= 0 ? Inv[item] : StockOf(InventoryKey(item));
    /// <summary>Добавить/списать ресурс или деталь (детали и постройки — в запасе).</summary>
    internal void AddItem(int item, int d) { if (item >= 0) Inv[item] += d; else StockAdd(InventoryKey(item), d); }
    internal string ItemName(int item) => item >= 0 ? Loc.T(It.Names[item]) : PartOf(InventoryKey(item)) is { } p ? PartName(p) : Loc.T(InventoryItemName(item));

    // ───────────────────────── кузница: рабочие части ─────────────────────────
    /// <summary>Рецепты кузницы для деталей (добавляются в группу «forge»).</summary>
    void AddForgePartRecipes(List<Recipe> list)
    {
        foreach (var p in PartDefs())
        {
            if (p.Cost == null) continue;
            var pd = p; Func<bool> known = p.Tier >= 0 ? () => PickHeadKnown(pd.Tier) : p.Key == PartAxe ? () => !OwnedTools.Contains("axe") : () => BpOk(Db.Weapon(pd.Weapon)?.Bp) && !HasWeapon(pd.Weapon);
            list.Add(new Recipe { Id = pd.Key, Group = "forge", Name = pd.Name, NameFn = () => PartName(pd), Desc = PartDesc(pd), Icon = pd.Icon, Cost = pd.Cost, Multi = true, Known = known, Done = () => false, Craft = n => CraftPart(pd, n) });
        }
    }

    static string PartDesc(PartDef p) => p.Tier >= 0 ? "Рабочая часть кирки. Соберите кирку на Рабочем столе: рукоять + боёк" : p.Key == PartAxe ? "Рабочая часть топора. Рабочий стол: рукоять + лезвие" : "Рабочая часть оружия. Рабочий стол: рукоять + деталь";

    /// <summary>Боёк кирки нужного тира показывается, пока такая кирка ещё не собрана: следующий тир (или любой до него, если кирки нет — например, она осталась в могиле).</summary>
    bool PickHeadKnown(int t) => OwnedTools.Contains("pickaxe") ? t == Tier + 1 : t <= Tier + 1;

    void CraftPart(PartDef p, int qty)
    {
        int made = 0, item = PartItem(p.Key);
        for (int i = 0; i < qty && Can(p.Cost); i++) { if (Room(item) <= 0 && !PayFreesRoom(p.Cost, item)) { FullMsg(); break; } Pay(p.Cost); StockAdd(p.Key, 1); made++; }
        if (made > 0) { Diag.Mark("first_craft"); GiveXp(2 * made); Sfx.Play("build", -4); Toast($"{Loc.T("Создано")}: {PartName(p)} ×{made}"); }
    }

    // ───────────────────────── рабочий стол: доски, рукоять, сборка ─────────────────────────
    /// <summary>Рецепты Рабочего стола: доски и рукоять, сборка инструментов и оружия, затем постройки.</summary>
    public List<BenchRecipe> BenchEntries()
    {
        var l = new List<BenchRecipe>();
        int plank = PartItem(PartPlank), handle = PartItem(PartHandle);
        l.Add(new BenchRecipe { Key = PartPlank, Name = "Доска", Icon = UiIcons.Item(PartIconBase), Cost = CostPlank, ResultItem = plank, ResultN = PlanksPerWood, Kind = "Деталь" });
        l.Add(new BenchRecipe { Key = PartHandle, Name = "Рукоять", Icon = UiIcons.Item(PartIconBase + 1), Cost = new[] { new[] { plank, PlanksPerHandle } }, ResultItem = handle, Kind = "Деталь" });
        for (int t = 0; t <= 4; t++)
        {
            int tt = t; bool own = OwnedTools.Contains("pickaxe");
            if (own ? t != Tier + 1 : t > Tier + 1) continue;                  // собирается следующая кирка (без кирки — любая до следующего тира)
            l.Add(new BenchRecipe { Key = "tool_pickaxe" + t, Name = PickNames[t], Icon = t == 0 ? UiIcons.ItemFixed(UiIcons.Pickaxe) : UiIcons.Item(UiIcons.PickIndex(t)), Cost = new[] { new[] { handle, 1 }, new[] { PartItem(PartPick(t)), 1 } }, Give = () => AssemblePickaxe(tt), Kind = "Инструмент" });
        }
        if (!OwnedTools.Contains("axe")) l.Add(new BenchRecipe { Key = "tool_axe", Name = ToolDef.Get("axe").Name, Icon = UiIcons.Item(UiIcons.Axe), Cost = new[] { new[] { handle, 1 }, new[] { PartItem(PartAxe), 1 } }, Give = () => AssembleAxe(), Kind = "Инструмент" });
        foreach (var w in CraftableWeapons())
        {
            if (HasWeapon(w.Id) || !BpOk(w.Bp)) continue; var wd = w;
            l.Add(new BenchRecipe { Key = "w_" + w.Id, Name = w.Name, Icon = UiIcons.Item(HotIcon(RefWeapon(w.Id))), Cost = new[] { new[] { handle, 1 }, new[] { PartItem(PartWeapon(w.Id)), 1 } }, Give = () => AssembleWeapon(wd), Kind = "Оружие" });
        }
        foreach (var e in BuildEntries())
            if (!BuiltOnce(e) && e.Unlocked && e.CostArr != null) l.Add(new BenchRecipe { Key = e.Key, Name = e.Name, Icon = UiIcons.Tile(e.Tile), Cost = e.Raw ?? e.CostArr, Build = e, ResultItem = InventoryItemId(e.Key), Kind = "Постройка" });
        return l;
    }

    bool AssemblePickaxe(int t)
    {
        bool own = OwnedTools.Contains("pickaxe");
        if (own && Tier >= t) { Toast("Такая кирка уже есть"); return false; }
        if (!own) { OwnedTools.Add("pickaxe"); AutoAssign(RefTool("pickaxe")); if (OwnedTools.Count == 1 || CurW < 0 && !OwnedTools.Contains(CurTool)) HotSelect(HotSlotOf(RefTool("pickaxe"))); }
        if (t > Tier) { Tier = t; Diag.Mark("first_tool_upgrade"); }
        GiveXp(own ? 40 + 20 * Tier : 10); Shake = 2; Burst(PPos, new Color("#ffe27a"), 14, 80); Sfx.Play("upgrade");
        Announce(PickNames[Tier], Tier > 0 ? PickGain[Tier] : ToolDef.Get("pickaxe").Desc, new Color("#ffe27a"), "upgrade"); Save();
        return true;
    }

    bool AssembleAxe()
    {
        if (OwnedTools.Contains("axe")) { Toast("Этот инструмент уже есть"); return false; }
        OwnedTools.Add("axe"); AutoAssign(RefTool("axe")); GiveXp(10); Sfx.Play("build");
        Announce("Инструмент: " + ToolDef.Get("axe").Name, ToolDef.Get("axe").Desc, new Color("#ffd23f"), "upgrade"); Save(); return true;
    }

    bool AssembleWeapon(WeaponDef w)
    {
        if (HasWeapon(w.Id)) { Toast("Это оружие уже есть"); return false; }
        GiveWeapon(w.Id); HotEquip(RefWeapon(w.Id)); GiveXp(25); Sfx.Play("build"); Save(); return true;
    }
}
