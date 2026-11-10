using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Слоты инвентаря: стопка — до 99 штук одного ресурса, в начале игры 10 слотов, дальше слоты добавляет навык «Рюкзак» (+2 за уровень).
/// Разделённая стопка занимает дополнительный слот; монеты слотов не занимают. Inv[] хранит общие количества, а сетка хранит размеры и порядок отдельных стопок.
/// Награды и возврат вещей из могилы лимит не режет: слотов может стать больше, чем есть, тогда новые предметы не подбираются, пока место не освободится.</summary>
public partial class Game
{
    public const int StackMax = 99, SlotsBase = 10, SlotsPerPackLv = 2;
    float fullMsgT = -9f;

    /// <summary>Сколько слотов сейчас у героя.</summary>
    public int SlotCap => SlotsBase + Mathf.RoundToInt(E(Tech.Pack));
    public static bool UsesSlot(int item) => item != It.Coins;
    public bool TestSlots;                                      // автотесты: лимит слотов включён только там, где он проверяется (в остальных проверках инвентарь забит произвольно)

    static readonly (int item, int n) EmptyStack = (-1, 0);
    readonly List<(int item, int n)> inventorySlots = new();
    readonly int[] slotTotals = new int[It.Count];

    public int SlotsUsed()
    {
        SyncInventorySlots();
        int used = 0;
        foreach (var stack in inventorySlots) if (stack.n > 0 && !IsInventoryGear(stack.item) && UsesSlot(stack.item)) used++;
        return used;
    }

    readonly List<string> inventoryItemKeys = new();
    readonly Dictionary<string, int> inventoryItemTotals = new();

    int InventoryItemId(string key)
    {
        int i = inventoryItemKeys.IndexOf(key);
        if (i < 0) { i = inventoryItemKeys.Count; inventoryItemKeys.Add(key); }
        return -2 - i;
    }

    string InventoryKey(int item) => item <= -2 && -2 - item < inventoryItemKeys.Count ? inventoryItemKeys[-2 - item] : "";
    static bool IsInventoryGear(string key) => key.Contains(':');
    bool IsInventoryGear(int item) => IsInventoryGear(InventoryKey(item));
    internal bool CanFillEmptyInventorySlot(int item) => IsInventoryGear(item) || SlotsUsed() < SlotCap;

    internal BuildEntry InventoryBuild(int item)
    {
        string key = InventoryKey(item);
        return key.Length > 0 && !IsInventoryGear(key) ? EntryOf(key) : null;
    }

    int InventoryCount(int item)
    {
        if (item >= 0) return Inv[item];
        string key = InventoryKey(item);
        if (key.StartsWith("relic:")) return OwnedRelics.Contains(key.Substring(6)) ? 1 : 0;
        if (key.StartsWith("mod:")) return ModInv[int.Parse(key.Substring(4))];
        return IsInventoryGear(key) ? (HotValid(key) ? 1 : 0) : StockOf(key);
    }

    internal string InventoryItemName(int item) => item >= 0 ? It.Names[item] : KeyName(InventoryKey(item));

    internal string KeyName(string key)
    {
        if (key.StartsWith("relic:")) return Db.Relic(key.Substring(6))?.Title ?? "";
        if (key.StartsWith("mod:")) return Db.Mods[int.Parse(key.Substring(4))].Name;
        if (PartOf(key) is { } part) return PartName(part);
        return IsInventoryGear(key) ? HotName(key) : BuildOfKey(key)?.Name ?? "";
    }

    BuildEntry BuildOfKey(string key) => key.Length > 0 && !IsInventoryGear(key) ? EntryOf(key) : null;

    internal string InventoryItemDetails(int item) => item >= 0 ? "" : KeyDetails(InventoryKey(item));

    internal string KeyDetails(string key)
    {
        if (key.StartsWith("relic:")) return Loc.T(Db.Relic(key.Substring(6))?.Desc ?? "");
        if (key.StartsWith("mod:"))
        {
            int index = int.Parse(key.Substring(4));
            return index >= 0 && index < Db.Mods.Length ? Loc.T(Db.Mods[index].Desc) : "";
        }
        if (IsToolRef(key))
        {
            var tool = ToolDef.Get(RefId(key));
            return Loc.T(tool.Desc ?? "");
        }
        if (IsWeaponRef(key))
        {
            var weapon = Db.Weapon(RefId(key));
            if (weapon == null) return "";
            return $"{Loc.T("Базовый урон")}: {weapon.Dmg:0.#}\n{Loc.T(weapon.Desc ?? "")}".TrimEnd();
        }
        if (PartOf(key) is { } part) return Loc.T(PartDesc(part));
        return Loc.T(BuildOfKey(key)?.Hint ?? "");
    }

    static string TagName(string t) => Loc.T(t switch { "fire" => "Огонь", "ice" => "Лёд", "light" => "Свет", "storm" => "Гроза", _ => t });
    static readonly string[] FxLabels = { "Поджог", "Замедление", "Заморозка", "Молния", "Залп", "Пробой", "Сифон", "Взрыв", "Дуга" };
    static readonly Color TipGood = new("#8fe07a"), TipGold = new("#ffd36a"), TipWarm = new("#ffb36a");

    /// <summary>Данные красивой подсказки: название, тип, характеристики (если есть) и описание.</summary>
    internal ItemTipData InventoryItemTip(int item, int count)
    {
        if (item >= 0)
        {
            var r = new ItemTipData { Name = It.Names[item], Count = count, Kind = "Ресурс" };
            r.Stat("Стопка", $"{count}/{StackMax}"); return r;
        }
        return KeyTip(InventoryKey(item), count);
    }

    /// <summary>Подсказка по ключу предмета («tool:…», «weapon:…», «relic:…», «mod:…», чертёж постройки) — для окон, где предмета нет в инвентаре.</summary>
    internal ItemTipData KeyTip(string key, int count = 1)
    {
        var d = new ItemTipData { Name = KeyName(key), Count = count, Desc = KeyDetails(key) };
        if (key.StartsWith("relic:"))
        {
            var r = Db.Relic(key.Substring(6)); if (r == null) return d;
            int rar = Mathf.Clamp(r.Rarity, 0, Db.RarCol.Length - 1); d.Accent = Db.RarCol[rar];
            d.Kind = $"Реликвия · {Loc.T(Db.RarName[rar])}";
            d.Stat("Действие", r.Permanent ? "Постоянное" : "Временное");
            if (r.Tags.Length > 0) d.Stat("Теги", string.Join(", ", System.Linq.Enumerable.Select(r.Tags, TagName)), TipWarm);
        }
        else if (key.StartsWith("mod:"))
        {
            int i = int.Parse(key.Substring(4)); if (i < 0 || i >= Db.Mods.Length) return d;
            var m = Db.Mods[i]; d.Accent = new Color("#5aa8ff"); d.Kind = "Модификатор оружия";
            foreach (var (k, p) in m.Eff) d.Stat(FxLabels[(int)k], $"+{p:0.#}", TipGood);
            if (!string.IsNullOrEmpty(m.Tag)) d.Stat("Стихия", TagName(m.Tag), TipWarm);
        }
        else if (IsToolRef(key))
        {
            var t = ToolDef.Get(RefId(key)); if (t == null) return d;
            d.Accent = TipGold; d.Kind = "Инструмент";
            d.Stat("Скорость добычи", $"×{t.MiningPower:0.##}", TipGood);
            d.Stat("Урон в бою", $"×{t.CombatDamage:0.##}", TipGood);
            d.Stat("Скорость удара", $"×{1f / Mathf.Max(.01f, t.AttackInterval):0.##}");
            if (t.AttackRange != 0) d.Stat("Дальность", $"{t.AttackRange:+0;-0}");
            foreach (var (n, v) in new[] { ("Дерево", t.EffWood), ("Камень", t.EffStone), ("Руда", t.EffOre), ("Завалы", t.EffRubble) })
                if (!Mathf.IsEqualApprox(v, 1f)) d.Stat(n, $"×{v:0.##}", v > 1f ? TipGood : TipWarm);
        }
        else if (IsWeaponRef(key))
        {
            var w = Db.Weapon(RefId(key)); if (w == null) return d;
            d.Accent = Db.RarCol[(int)w.Rar];
            d.Kind = $"{Loc.T(Db.ClassNames[(int)w.Cls])} · {Loc.T(Db.RarName[(int)w.Rar])}" + (w.Unique ? " · " + Loc.T("Уникальное") : "");
            d.Stat("Урон", $"{w.Dmg:0.#}", TipGood);
            if (w.Cd > 0) d.Stat("Перезарядка", $"{w.Cd:0.##} " + Loc.T("с"));
            if (w.Reach > 0) d.Stat("Дальность", $"{w.Reach:0.#}");
            if (w.Speed > 0) d.Stat("Скорость снаряда", $"{w.Speed:0.#}");
            if (w.Pierce > 0) d.Stat("Пробитие", $"{w.Pierce}");
            foreach (var (k, p) in w.Inherent) d.Stat(FxLabels[(int)k], $"{p:0.#}", TipWarm);
            if (w.Tags.Length > 0) d.Stat("Стихия", string.Join(", ", System.Linq.Enumerable.Select(w.Tags, TagName)), TipWarm);
            d.Stat("Слоты модов", $"{w.Slots}", new Color("#5aa8ff"));
        }
        else if (PartOf(key) is { } part)
        {
            d.Accent = TipWarm; d.Kind = "Деталь";
            d.Stat("Стопка", $"{count}/{StackMax}");
        }
        else if (BuildOfKey(key) is { } b)
        {
            d.Accent = TipGold; d.Kind = "Постройка";
            if (!string.IsNullOrEmpty(b.Cost)) d.Stat("Стоимость", b.Cost);
        }
        return d;
    }

    /// <summary>Подсказка рецепта быстрого крафта: что получится, нужные компоненты (есть/нужно) и сколько раз можно создать.</summary>
    internal ItemTipData RecipeTip(CombineRecipe r, int times)
    {
        int res = RecipeResultItem(r), n = RecipeResultCount(r);
        var d = new ItemTipData { Name = InventoryItemName(res), Count = n, Kind = "Рецепт", Accent = TipGold };
        var q = RecipeInputs(r); var bad = new Color("#f06a5e");
        if (q.a == q.b) d.Stat(It.Names[q.a], $"{Inv[q.a]}/{q.na + q.nb}", Inv[q.a] >= q.na + q.nb ? TipGood : bad);
        else { d.Stat(It.Names[q.a], $"{Inv[q.a]}/{q.na}", Inv[q.a] >= q.na ? TipGood : bad); d.Stat(It.Names[q.b], $"{Inv[q.b]}/{q.nb}", Inv[q.b] >= q.nb ? TipGood : bad); }
        d.Stat("Можно создать", times.ToString(), times > 0 ? TipGood : bad);
        if (times > 1) d.Stat("Shift + клик", "максимум", TipGold);
        return d;
    }

    /// <summary>Подсказка ячейки результата в блоке «A + B».</summary>
    internal ItemTipData CombineResultTip()
    {
        if (craftIngredients[2].n > 0) return InventoryItemTip(craftIngredients[2].item, craftIngredients[2].n);
        var m = MatchCombine();
        if (m.Recipe == null) return m.State == CombineState.Unknown ? new ItemTipData { Name = "Неизвестная комбинация", Kind = "Результата нет", Accent = new Color("#6a7288") } : null;
        if (!m.Known) { var e = new ItemTipData { Name = "Эксперимент", Kind = "Новый рецепт", Accent = new Color("#c070ff") }; e.Stat("Результат", "?", new Color("#c070ff")); return e; }
        return RecipeTip(m.Recipe, CellTimes(RecipeInputs(m.Recipe)));
    }

    /// <summary>«Нужно: Камень ×10 + Дерево ×5» — для подписи, когда в ячейках не хватает количества.</summary>
    internal string RecipeNeedText(CombineRecipe r)
    {
        var q = RecipeInputs(r);
        return q.a == q.b ? $"{Loc.T("Нужно")}: {Loc.T(It.Names[q.a])} ×{q.na + q.nb}" : $"{Loc.T("Нужно")}: {Loc.T(It.Names[q.a])} ×{q.na} + {Loc.T(It.Names[q.b])} ×{q.nb}";
    }

    /// <summary>Подсказка постройки из меню строительства/крафта: стоимость, запас, причина блокировки.</summary>
    internal ItemTipData BuildEntryTip(BuildEntry e)
    {
        var d = new ItemTipData { Name = e.Name, Kind = "Постройка", Accent = TipGold };
        if (!e.Unlocked) { d.Kind = "Закрыто"; d.Accent = new Color("#6a7288"); if (!string.IsNullOrEmpty(e.Lock)) d.Stat("Откроется", e.Lock, TipWarm); }
        if (e.CostArr != null && e.Stock == 0)                                                   // строка Cost содержит значки-символы: выводим по строке на ресурс
            foreach (var c in e.CostArr) { int have = HaveOf(c[0]); d.Stat(It.Names[c[0]], $"{have}/{c[1]}", have >= c[1] ? TipGood : new Color("#f06a5e")); }
        if (e.Stock > 0) d.Stat("В запасе", $"{e.Stock}", TipGood);
        foreach (var (label, value) in BuildTraits(e.Key)) d.Stat(label, value, TipGood);
        return d;
    }

    /// <summary>Свойства построек в виде характеристик (вместо текстовых описаний).</summary>
    static (string, string)[] BuildTraits(string key) => key switch
    {
        "furnace" => new[] { ("Плавка", "1 руда + 1 дерево"), ("Результат", "Слиток") },
        "chest" => new[] { ("Слотов", ChestSlots.ToString()), ("В стопке", StackMax.ToString()) },
        "workbench" => new[] { ("Создание", "Постройки") },
        "lamp0" => new[] { ("Свет", "Постоянный") },
        "lamp1" => new[] { ("Свет", "Постоянный"), ("Урон врагам", "Рядом") },
        "lamp2" => new[] { ("Свет", "Лучом") },
        "mirror" => new[] { ("Луч", "Поворачивает") },
        "amp" => new[] { ("Фонари рядом", "Усиливает") },
        "barrier" => new[] { ("Задерживает", "Теней"), ("Длина линии", "до 9 клеток") },
        _ when key.StartsWith("decor") => new[] { ("Бонус", "Производство") },
        _ => System.Array.Empty<(string, string)>(),
    };

    internal Texture2D InventoryItemTexture(int item)
    {
        if (item >= 0) return UiIcons.Item(item);
        string key = InventoryKey(item);
        if (key.StartsWith("relic:")) return UiIcons.Item(It.Relic);
        if (key.StartsWith("mod:")) return UiIcons.Item(ModIcon(Db.Mods[int.Parse(key.Substring(4))].Id));
        if (IsInventoryGear(key)) return UiIcons.Item(HotIcon(key));
        if (PartOf(key) is { } part) return UiIcons.Item(part.Icon);
        return InventoryBuild(item) is { } entry ? UiIcons.Tile(entry.Tile) : null;
    }

    internal bool UseInventoryItem(int item, int slot)
    {
        if (InventoryCount(item) <= 0) return false;
        if (BeginInventoryPlacement(item, slot)) return true;
        string key = InventoryKey(item);
        if (key.StartsWith("relic:")) { ToggleRelic(key.Substring(6)); return true; }
        if (key.StartsWith("mod:"))
        {
            OpenWin(Win.Equip);
            WinCur = ToolList().Count + Math.Max(0, CurW);
            EqPick = int.Parse(key.Substring(4));
            return true;
        }
        if (HotValid(key)) { HotEquip(key); return true; }
        return false;
    }

    internal const int QuickSlotCount = 10;
    readonly int[] quickItems = { -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 };
    readonly int[] quickCounts = new int[QuickSlotCount];
    internal int SelectedQuickSlot { get; private set; } = -1;

    internal (int item, int n) QuickStack(int slot)
    {
        SyncQuickSlots();
        return (quickItems[slot], quickCounts[slot]);
    }

    int QuickCount(int item)
    {
        int count = 0;
        for (int i = 0; i < QuickSlotCount; i++)
            if (quickItems[i] == item) count += quickCounts[i];
        return count;
    }

    void SyncQuickSlots()
    {
        for (int i = 0; i < QuickSlotCount; i++)
        {
            if (quickCounts[i] == 0) continue;
            int item = quickItems[i], available = InventoryCount(item);
            for (int j = 0; j < i; j++)
                if (quickItems[j] == item) available -= quickCounts[j];
            quickCounts[i] = Math.Clamp(available, 0, quickCounts[i]);
            if (quickCounts[i] == 0) quickItems[i] = -1;
        }
    }

    /// <summary>Обмен, когда в руке только часть стопки: часть ложится в занятую ячейку, а вытесненная стопка уходит в свободную ячейку сумки (home) — оттуда она берётся в руку.</summary>
    internal bool PartialSwap(bool srcQuick, int src, int count, bool dstQuick, int dst, out int home)
    {
        home = -1; var bag = InventorySlots();
        var from = srcQuick ? QuickStack(src) : src >= 0 && src < bag.Count ? bag[src] : EmptyStack;
        var to = dstQuick ? QuickStack(dst) : dst >= 0 && dst < bag.Count ? bag[dst] : EmptyStack;
        if (from.item < 0 || to.item < 0 || count <= 0 || from.n <= count || to.n <= 0 || to.item == from.item || !UsesSlot(from.item) || !UsesSlot(to.item)) return false;
        if (srcQuick == dstQuick && src == dst) return false;
        int free = -1; for (int i = 0; i < bag.Count; i++) if (bag[i].n == 0) { free = i; break; }
        if (free < 0) { FullMsg(); return false; }
        if (srcQuick) quickCounts[src] = from.n - count; else inventorySlots[src] = (from.item, from.n - count);
        if (dstQuick) { quickItems[dst] = from.item; quickCounts[dst] = count; SelectedQuickSlot = dst; } else inventorySlots[dst] = (from.item, count);
        inventorySlots[free] = to;
        foreach (int it in new[] { from.item, to.item }) { int sum = 0; foreach (var st in inventorySlots) if (st.item == it) sum += st.n; slotTotals[it] = sum; }
        home = free; return true;
    }

    internal bool MoveToQuickSlot(int source, int target, int item, int count)
    {
        var bag = InventorySlots();
        if (source < 0 || source >= bag.Count || bag[source].item != item || !UsesSlot(item) ||
            count <= 0 || count > bag[source].n) return false;

        int fromCount = bag[source].n;
        int previousItem = quickItems[target], previousCount = quickCounts[target];
        if (previousItem == item && previousCount >= StackMax && count == fromCount && fromCount < StackMax) { SwapBagQuick(source, target); return true; }
        if (previousCount > 0 && previousItem != item)
        {
            if (count != fromCount) return false;
            if (!IsInventoryGear(previousItem) && IsInventoryGear(item) && SlotsUsed() >= SlotCap)
            {
                FullMsg();
                return false;
            }
            inventorySlots[source] = (previousItem, previousCount);
            TrackBagChange(previousItem, previousCount);
            previousCount = 0;
        }
        else
        {
            count = Math.Min(count, StackMax - previousCount);
            if (count == 0) return false;
            inventorySlots[source] = count == fromCount ? EmptyStack : (item, fromCount - count);
        }
        quickItems[target] = item;
        quickCounts[target] = previousCount + count;
        SelectedQuickSlot = target;
        TrackBagChange(item, -count);
        return true;
    }

    void TrackBagChange(int item, int change)
    {
        if (item >= 0) slotTotals[item] += change;
        else
        {
            string key = InventoryKey(item);
            inventoryItemTotals.TryGetValue(key, out int count);
            inventoryItemTotals[key] = count + change;
        }
    }

    internal void ClearQuickSlot(int slot)
    {
        SyncInventorySlots();
        int item = quickItems[slot], count = quickCounts[slot];
        if (count == 0) return;
        if (!IsInventoryGear(item) && Room(item) < count) { FullMsg(); return; }
        quickItems[slot] = -1;
        quickCounts[slot] = 0;
        SyncInventorySlots();
    }

    internal bool MoveQuickToBag(int quick, int target, int count)
    {
        var bag = InventorySlots();
        var (item, available) = QuickStack(quick);
        if (count <= 0 || count > available || target < 0 || target >= bag.Count) return false;
        var (other, otherCount) = bag[target];
        if (other == item && otherCount >= StackMax && count == available && available < StackMax) { SwapBagQuick(target, quick); return true; }
        if (otherCount > 0 && other != item)
        {
            if (count != available || (!IsInventoryGear(item) && IsInventoryGear(other) && SlotsUsed() >= SlotCap)) return false;
            return MoveToQuickSlot(target, quick, other, otherCount);
        }
        if (otherCount == 0 && !IsInventoryGear(item) && SlotsUsed() >= SlotCap) return false;
        count = Math.Min(count, StackMax - otherCount);
        if (count <= 0) return false;
        inventorySlots[target] = (item, otherCount + count);
        TrackBagChange(item, count);
        RemoveQuickCount(quick, count);
        return true;
    }

    void RemoveQuickCount(int slot, int count)
    {
        quickCounts[slot] -= count;
        if (quickCounts[slot] == 0) quickItems[slot] = -1;
    }

    internal void SwapBagQuick(int bagIndex, int quick)
    {
        var bag = InventorySlots();
        if (bagIndex < 0 || bagIndex >= bag.Count) return;
        var (item, count) = bag[bagIndex];
        var (other, otherCount) = QuickStack(quick);
        if (count == 0) { MoveQuickToBag(quick, bagIndex, otherCount); return; }
        if (otherCount == 0 || item != other) { MoveToQuickSlot(bagIndex, quick, item, count); return; }
        inventorySlots[bagIndex] = (item, otherCount);
        quickCounts[quick] = count;
        TrackBagChange(item, otherCount - count);
        SelectedQuickSlot = quick;
    }

    internal void MoveQuickStack(int source, int target, int count)
    {
        if (source == target || count <= 0 || count > quickCounts[source]) return;
        int item = quickItems[source], other = quickItems[target], otherCount = quickCounts[target];
        if (otherCount >= StackMax && item == other && count == quickCounts[source] && count < StackMax)
        {
            (quickCounts[source], quickCounts[target]) = (otherCount, count);
            SelectedQuickSlot = target; return;
        }
        if (otherCount > 0 && item != other)
        {
            if (count != quickCounts[source]) return;
            quickItems[source] = other;
            quickCounts[source] = otherCount;
            otherCount = 0;
        }
        else
        {
            count = Math.Min(count, StackMax - otherCount);
            RemoveQuickCount(source, count);
        }
        quickItems[target] = item;
        quickCounts[target] = otherCount + count;
        SelectedQuickSlot = target;
    }

    internal void ShiftBagToQuick(int source)
    {
        var bag = InventorySlots();
        if (source < 0 || source >= bag.Count || bag[source].n == 0) return;
        var (item, remaining) = bag[source];
        for (int i = 0; i < QuickSlotCount && remaining > 0; i++)
        {
            if (quickItems[i] != item || quickCounts[i] >= StackMax) continue;
            int count = Math.Min(remaining, StackMax - quickCounts[i]);
            if (MoveToQuickSlot(source, i, item, count)) remaining -= count;
        }
        int empty = Array.IndexOf(quickItems, -1);
        if (remaining > 0 && empty >= 0) MoveToQuickSlot(source, empty, item, remaining);
    }

    string[] SaveQuickSlots()
    {
        SyncQuickSlots();
        var saved = new string[QuickSlotCount];
        for (int i = 0; i < saved.Length; i++)
        {
            int item = quickItems[i];
            if (item >= 0) saved[i] = "res:" + item;
            else if (item < -1) saved[i] = InventoryKey(item);
        }
        return saved;
    }

    int[] SaveQuickCounts() => (int[])quickCounts.Clone();

    void LoadQuickSlots(string[] saved, int[] counts = null)
    {
        if (saved == null) return;
        SyncInventorySlots();
        Array.Fill(quickItems, -1);
        Array.Clear(quickCounts);
        for (int i = 0; i < Math.Min(saved.Length, QuickSlotCount); i++)
        {
            string key = saved[i];
            if (string.IsNullOrEmpty(key)) continue;
            if (key.StartsWith("res:") && int.TryParse(key.Substring(4), out int item) && item >= 0 && item < It.Count)
                quickItems[i] = item;
            else
            {
                int index = inventoryItemKeys.IndexOf(key);
                if (index >= 0) quickItems[i] = -2 - index;
            }
            int assigned = quickItems[i];
            if (assigned == -1 || !UsesSlot(assigned)) { quickItems[i] = -1; continue; }
            int count = counts == null ? StackMax : i < counts.Length ? counts[i] : 0;
            int available = Math.Max(0, InventoryCount(assigned) - QuickCount(assigned));
            quickCounts[i] = Math.Clamp(count, 0, Math.Min(StackMax, available));
            if (quickCounts[i] == 0) quickItems[i] = -1;
        }
        SyncInventorySlots();
    }

    void CycleQuickSlot(int direction)
    {
        int current = SelectedQuickSlot < 0 ? (direction > 0 ? -1 : 0) : SelectedQuickSlot;
        SelectQuickSlot((current + direction + QuickSlotCount) % QuickSlotCount);
    }

    internal void SelectQuickSlot(int slot)
    {
        SelectedQuickSlot = slot;
        if (Window != Win.None) return;
        var (item, count) = QuickStack(SelectedQuickSlot);
        string gear = InventoryKey(item);
        if (count > 0 && HotValid(gear)) HotEquip(gear);
        else { BareChosen = true; CurTool = ""; EquipNone(); }       // пустой слот или ресурс/блок: оружие и инструмент убираются, в руке — выбранный предмет
    }

    /// <summary>Выбранный в хотбаре не-снаряжение (ресурс, блок): его значок рисуется в руке.</summary>
    internal Texture2D HandResourceTexture()
    {
        if (SelectedQuickSlot < 0) return null;
        var (item, n) = QuickStack(SelectedQuickSlot);
        if (n <= 0 || HotValid(InventoryKey(item))) return null;
        return InventoryItemTexture(item);
    }

    internal void UseQuickSlot(int slot)
    {
        SelectedQuickSlot = slot;
        var (item, n) = QuickStack(slot);
        if (n <= 0) return;
        if (UseInventoryItem(item, -2 - slot)) return;
    }


    void SyncInventorySlots()
    {
        SyncQuickSlots();
        for (int item = 0; item < It.Count; item++)
        {
            if (!UsesSlot(item)) continue;
            int count = Inv[item] - QuickCount(item);
            AdjustInventoryStacks(item, count - slotTotals[item]);
            slotTotals[item] = count;
        }
        foreach (var key in BuildStock.Keys) InventoryItemId(key);
        foreach (var tool in ToolDef.Ids) if (OwnedTools.Contains(tool)) InventoryItemId(RefTool(tool));
        foreach (var weapon in Owned) InventoryItemId(RefWeapon(weapon.Id));
        foreach (var relic in OwnedRelics) InventoryItemId("relic:" + relic);
        for (int i = 0; i < ModInv.Length; i++) if (ModInv[i] > 0) InventoryItemId("mod:" + i);
        for (int i = 0; i < inventoryItemKeys.Count; i++)
        {
            string key = inventoryItemKeys[i];
            int count = InventoryCount(-2 - i) - QuickCount(-2 - i);
            inventoryItemTotals.TryGetValue(key, out int previous);
            if (count == previous) continue;
            AdjustInventoryStacks(-2 - i, count - previous);
            inventoryItemTotals[key] = count;
        }
    }

    void AdjustInventoryStacks(int item, int delta)
    {
        if (delta < 0)
        {
            for (int i = inventorySlots.Count - 1; i >= 0 && delta < 0; i--)
            {
                var stack = inventorySlots[i];
                if (stack.item != item) continue;
                int take = Math.Min(stack.n, -delta);
                inventorySlots[i] = take == stack.n ? EmptyStack : (item, stack.n - take);
                delta += take;
            }
            return;
        }

        for (int i = 0; i < inventorySlots.Count && delta > 0; i++)
        {
            var stack = inventorySlots[i];
            if (stack.item != item) continue;
            int put = Math.Min(delta, StackMax - stack.n);
            inventorySlots[i] = (item, stack.n + put);
            delta -= put;
        }
        for (int i = 0; delta > 0; i++)
        {
            if (i == inventorySlots.Count) inventorySlots.Add(EmptyStack);
            if (inventorySlots[i].n != 0) continue;
            int put = Math.Min(delta, StackMax);
            inventorySlots[i] = (item, put);
            delta -= put;
        }
    }

    bool CanStoreCraftedBuild(string key, int[][] cost)
    {
        if (testing && !TestSlots) return true;
        int used = SlotsUsed(), buildItem = InventoryItemId(key);
        foreach (var stack in inventorySlots)
            if (stack.item == buildItem && stack.n < StackMax) return true;

        var spending = new int[It.Count];
        foreach (var c in cost) spending[c[0]] += c[1];
        for (int i = inventorySlots.Count - 1; i >= 0; i--)
        {
            var stack = inventorySlots[i];
            if (stack.item < 0 || spending[stack.item] == 0) continue;
            int take = Math.Min(spending[stack.item], stack.n);
            spending[stack.item] -= take;
            if (take == stack.n) used--;
        }
        return used < SlotCap;
    }

    BuildEntry inventoryPlacement;
    int inventoryPlacementSlot;

    internal bool BeginInventoryPlacement(int item, int slot)
    {
        var entry = InventoryBuild(item);
        if (entry == null || entry.Stock <= 0) return false;
        if (!entry.Unlocked) { Fail("Недоступно: " + entry.Lock); return false; }
        CloseWindows();
        Placing = inventoryPlacement = entry;
        inventoryPlacementSlot = slot;
        return true;
    }


    void ConsumePlacedInventoryStack(BuildEntry entry)
    {
        if (entry != inventoryPlacement) return;
        int i = inventoryPlacementSlot;
        int item = InventoryItemId(entry.Key);
        if (i <= -2)
        {
            int quick = -2 - i;
            if (quickItems[quick] != item) return;
            if (--quickCounts[quick] == 0) { quickItems[quick] = -1; Placing = null; }
            return;
        }
        if (i < 0 || i >= inventorySlots.Count || inventorySlots[i].item != item) return;
        var stack = inventorySlots[i];
        inventorySlots[i] = stack.n == 1 ? EmptyStack : (item, stack.n - 1);
        inventoryItemTotals[entry.Key]--;
    }

    public IReadOnlyList<(int item, int n)> InventorySlots()
    {
        int free = Math.Max(0, SlotCap - SlotsUsed());
        int empty = 0;
        foreach (var stack in inventorySlots) if (stack.n == 0) empty++;
        // Keep cell indices stable until the carried stack has finished moving.
        for (int i = inventorySlots.Count - 1; empty > free && !(InventoryUi?.CarryActive ?? false); i--)
        {
            if (inventorySlots[i].n != 0) continue;
            inventorySlots.RemoveAt(i);
            empty--;
        }
        while (empty++ < free) inventorySlots.Add(EmptyStack);
        return inventorySlots;
    }

    // Read a just-modified cell before InventorySlots can remove empty cells and shift indices.
    internal (int item, int n) InventorySlotBeforeResize(int index) =>
        index >= 0 && index < inventorySlots.Count ? inventorySlots[index] : EmptyStack;

    internal bool MoveInventoryStack(int source, int target, int item, int count)
    {
        var slots = InventorySlots();
        if (source < 0 || target < 0 || source >= slots.Count || target >= slots.Count || source == target) return false;
        var from = slots[source];
        var to = slots[target];
        if (from.item != item || !UsesSlot(item) || count <= 0 || count > from.n) return false;
        if (to.item == item && to.n >= StackMax && count == from.n && from.n < StackMax)      // полная ячейка с тем же предметом: стопки меняются местами
        {
            (inventorySlots[source], inventorySlots[target]) = (to, from);
            return true;
        }
        if (to.item == item) count = Math.Min(count, StackMax - to.n);
        else if (to.n > 0)
        {
            if (count != from.n || !UsesSlot(to.item)) return false;
            (inventorySlots[source], inventorySlots[target]) = (to, from);
            return true;
        }
        if (count <= 0) return false;
        inventorySlots[target] = (item, to.n + count);
        inventorySlots[source] = count == from.n ? EmptyStack : (item, from.n - count);
        return true;
    }

    /// <summary>Выбросить стопку прямо из слота хотбара.</summary>
    internal bool DropQuickStack(int slot, int item, int count)
    {
        var (have, n) = QuickStack(slot);
        if (have != item || count <= 0 || count > n || !(item >= 0 ? TryDrop(item, count) : TryDropBuild(InventoryKey(item), count))) return false;
        RemoveQuickCount(slot, count);
        return true;
    }

    internal bool DropInventoryStack(int source, int item, int count)
    {
        var slots = InventorySlots();
        if (source < 0 || source >= slots.Count) return false;
        var stack = slots[source];
        if (stack.item != item || count > stack.n || !(item >= 0 ? TryDrop(item, count) : TryDropBuild(InventoryKey(item), count))) return false;
        inventorySlots[source] = count == stack.n ? EmptyStack : (item, stack.n - count);
        TrackBagChange(item, -count);
        return true;
    }


    /// <summary>Сколько штук ресурса ещё поместится: остаток неполной стопки плюс свободные слоты.</summary>
    public int Room(int item)
    {
        if (!UsesSlot(item) || (testing && !TestSlots)) return int.MaxValue;
        SyncInventorySlots();
        int stackLeft = 0;
        foreach (var stack in inventorySlots) if (stack.item == item) stackLeft += StackMax - stack.n;
        return stackLeft + Math.Max(0, SlotCap - SlotsUsed()) * StackMax;
    }

    void FullMsg() { if (Time - fullMsgT > 2.5f) { fullMsgT = Time; Fail("Инвентарь полон: освободите слот или изучите «Рюкзак» у Стола исследований"); } }

    /// <summary>Добавляет ресурс в инвентарь с учётом слотов. Лишнее падает на землю у <paramref name="at"/> (если там можно выбросить), иначе пропадает. Возвращает, сколько легло в инвентарь.</summary>
    public int AddInv(int item, int n, Vector2 at)
    {
        if (n <= 0) return 0; int room = Room(item), put = Math.Min(n, room);
        if (put > 0) Inv[item] += put;
        if (put < n) { FullMsg(); SpawnLoot(item, n - put, at); }
        return put;
    }

}
