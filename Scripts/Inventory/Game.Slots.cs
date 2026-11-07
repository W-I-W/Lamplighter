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

    internal (int item, int n) QuickStack(int slot)
    {
        int item = quickItems[slot];
        return (item, InventoryCount(item));
    }

    internal void BindQuickSlot(int slot, int item)
    {
        if (InventoryCount(item) > 0) quickItems[slot] = item;
    }

    internal void ClearQuickSlot(int slot) => quickItems[slot] = -1;

    string[] SaveQuickSlots()
    {
        var saved = new string[QuickSlotCount];
        for (int i = 0; i < saved.Length; i++)
        {
            int item = quickItems[i];
            if (item >= 0) saved[i] = "res:" + item;
            else if (item < -1) saved[i] = InventoryKey(item);
        }
        return saved;
    }

    void LoadQuickSlots(string[] saved)
    {
        if (saved == null) return;
        SyncInventorySlots();
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
        }
    }

    internal void UseQuickSlot(int slot)
    {
        var (item, n) = QuickStack(slot);
        if (n <= 0) return;
        var bag = InventorySlots();
        for (int i = 0; i < bag.Count; i++)
        {
            if (bag[i].item != item) continue;
            if (UseInventoryItem(item, i)) return;
            if (Window != Win.Inventory) OpenWin(Win.Inventory);
            WinCur = i;
            if (item >= 0 && ResUi?.HasActions(item) == true) ResUi.Open(item);
            return;
        }
    }


    void SyncInventorySlots()
    {
        for (int item = 0; item < It.Count; item++)
        {
            if (!UsesSlot(item)) continue;
            AdjustInventoryStacks(item, Inv[item] - slotTotals[item]);
            slotTotals[item] = Inv[item];
        }
        foreach (var key in BuildStock.Keys) InventoryItemId(key);
        foreach (var tool in ToolDef.Ids) if (OwnedTools.Contains(tool)) InventoryItemId(RefTool(tool));
        foreach (var weapon in Owned) InventoryItemId(RefWeapon(weapon.Id));
        foreach (var relic in OwnedRelics) InventoryItemId("relic:" + relic);
        for (int i = 0; i < ModInv.Length; i++) if (ModInv[i] > 0) InventoryItemId("mod:" + i);
        for (int i = 0; i < inventoryItemKeys.Count; i++)
        {
            string key = inventoryItemKeys[i];
            int count = InventoryCount(-2 - i);
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
