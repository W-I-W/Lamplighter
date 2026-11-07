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

    internal string InventoryItemName(int item)
    {
        if (item >= 0) return It.Names[item];
        string key = InventoryKey(item);
        if (key.StartsWith("relic:")) return Db.Relic(key.Substring(6))?.Title ?? "";
        if (key.StartsWith("mod:")) return Db.Mods[int.Parse(key.Substring(4))].Name;
        return IsInventoryGear(key) ? HotName(key) : InventoryBuild(item)?.Name ?? "";
    }

    internal string InventoryItemDetails(int item)
    {
        if (item >= 0) return "";
        string key = InventoryKey(item);
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
        return Loc.T(InventoryBuild(item)?.Hint ?? "");
    }

    internal Texture2D InventoryItemTexture(int item)
    {
        if (item >= 0) return UiIcons.Item(item);
        string key = InventoryKey(item);
        if (key.StartsWith("relic:")) return UiIcons.Item(It.Relic);
        if (key.StartsWith("mod:")) return UiIcons.Item(ModIcon(Db.Mods[int.Parse(key.Substring(4))].Id));
        if (IsInventoryGear(key)) return UiIcons.Item(HotIcon(key));
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

    internal bool MoveToQuickSlot(int source, int target, int item, int count)
    {
        var bag = InventorySlots();
        if (source < 0 || source >= bag.Count || bag[source].item != item || !UsesSlot(item) ||
            count <= 0 || count > bag[source].n) return false;

        int fromCount = bag[source].n;
        int previousItem = quickItems[target], previousCount = quickCounts[target];
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
        for (int i = inventorySlots.Count - 1; empty > free; i--)
        {
            if (inventorySlots[i].n != 0) continue;
            inventorySlots.RemoveAt(i);
            empty--;
        }
        while (empty++ < free) inventorySlots.Add(EmptyStack);
        return inventorySlots;
    }

    internal bool MoveInventoryStack(int source, int target, int item, int count)
    {
        var slots = InventorySlots();
        if (source < 0 || target < 0 || source >= slots.Count || target >= slots.Count || source == target) return false;
        var from = slots[source];
        var to = slots[target];
        if (from.item != item || !UsesSlot(item) || count <= 0 || count > from.n) return false;
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

    internal bool DropInventoryStack(int source, int item, int count)
    {
        var slots = InventorySlots();
        if (source < 0 || source >= slots.Count) return false;
        var stack = slots[source];
        if (stack.item != item || item < 0 || count > stack.n || !TryDrop(item, count)) return false;
        inventorySlots[source] = count == stack.n ? EmptyStack : (item, stack.n - count);
        slotTotals[item] -= count;
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
