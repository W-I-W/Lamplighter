using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lamplighter;

/// <summary>Ставимый сундук: [E] открывает окно переноса. В сундуке 20 ячеек; ручной перенос сохраняет их порядок.
/// Содержимое — часть мира (сохраняется вместе с ним). В кооперативе сундуком управляет хозяин мира: у гостя он только виден.</summary>
public partial class Game
{
    public const int ChestSlots = 20, ChestCols = 10;                  // сундук в окне — две строки по десять ячеек, как инвентарь
    public static readonly int[][] CostChest = { new[] { It.Wood, 20 }, new[] { It.Stone, 5 } };
    readonly Dictionary<int, ChestCell[]> Chests = new();
    public Vector2I ChestAt = new(-1, -1); public string ChestMsg = "";

    ChestCell[] ChestCells(Vector2I tile)
    {
        int key = World.Key(tile.X, tile.Y);
        if (!Chests.TryGetValue(key, out var cells)) Chests[key] = cells = new ChestCell[ChestSlots];
        return cells;
    }

    public bool ChestEmpty(int x, int y) => !Chests.TryGetValue(World.Key(x, y), out var cells) ||
        Array.TrueForAll(cells, cell => cell == null);

    int ChestItem(ChestCell cell)
    {
        if (cell == null) return -1;
        if (cell.Key.StartsWith("res:") && int.TryParse(cell.Key.AsSpan(4), out int item)) return item;
        return InventoryItemId(cell.Key);
    }

    ChestCell MakeChestCell(int item, int count)
    {
        var cell = new ChestCell { Key = item >= 0 ? $"res:{item}" : InventoryKey(item), Count = count };
        if (cell.Key == RefTool("axe")) cell.Level = AxeLv;
        else if (IsWeaponRef(cell.Key))
        {
            var weapon = Owned.Find(w => w.Id == RefId(cell.Key));
            cell.Level = weapon.Lv;
            cell.Mods = weapon.Mods.ToArray();
        }
        return cell;
    }

    void ChangeOwnership(ChestCell cell, int item, int delta)
    {
        if (item >= 0) { Inv[item] += delta; return; }
        string key = cell.Key;
        if (key.StartsWith("mod:")) { ModInv[int.Parse(key.AsSpan(4))] += delta; return; }
        if (!IsInventoryGear(key)) { StockAdd(key, delta); return; }

        string id = RefId(key);
        if (delta > 0)
        {
            if (IsToolRef(key))
            {
                OwnedTools.Add(id);
                if (id == "axe") AxeLv = cell.Level;
            }
            else if (IsWeaponRef(key))
            {
                var weapon = new OwnedWeapon { Id = id, Lv = cell.Level };
                if (cell.Mods != null) weapon.Mods.AddRange(cell.Mods);
                Owned.Add(weapon);
            }
            else OwnedRelics.Add(id);
            return;
        }

        for (int i = 0; i < Hot.Length; i++) if (Hot[i] == key) Hot[i] = null;
        if (IsToolRef(key))
        {
            OwnedTools.Remove(id);
            if (id == "axe") AxeLv = 0;
            if (CurTool == id) { CurTool = ""; MineStop(); }
        }
        else if (IsWeaponRef(key))
        {
            int index = Owned.FindIndex(w => w.Id == id);
            Owned.RemoveAt(index);
            if (CurW == index) { CurW = -1; MineStop(); }
            else if (CurW > index) CurW--;
        }
        else
        {
            OwnedRelics.Remove(id);
            EquippedRelics.Remove(id);
        }
        RecomputeEffects();
    }

    internal bool CanTakeChestItem(int item) => !IsUniqueChestItem(item) || InventoryCount(item) == 0;

    bool IsUniqueChestItem(int item)
    {
        string key = InventoryKey(item);
        return IsToolRef(key) || IsWeaponRef(key) || key.StartsWith("relic:");
    }

    void BuildChest()
    {
        if (!Can(CostChest)) { Fail("Для сундука нужно: 20 дерева и 5 камня"); return; }
        var t = PlaceTarget(); if (t == null) return;
        Pay(CostChest); SetObj(t.Value.X, t.Value.Y, Tl.StorageChest);
        PlaceFx(t.Value, new Color("#b07a40"), 1.1f); Sfx.Play("build");
        GiveXp(10); Toast("Сундук готов"); Save();
    }

    /// <summary>Выпавшее из сломанного сундука: ресурсы падают на землю, снаряжение и реликвии возвращаются герою. Содержимое сундука очищается.</summary>
    void SpillChest(Vector2I tile)
    {
        int key = World.Key(tile.X, tile.Y);
        if (!Chests.TryGetValue(key, out var cells)) return;
        Chests.Remove(key);
        var at = TileCenter(tile.X, tile.Y);
        foreach (var cell in cells)
        {
            if (cell == null || cell.Count <= 0) continue;
            int item = ChestItem(cell);
            if (item >= 0) { if (!SpawnLoot(item, cell.Count, at)) Give(item, cell.Count, at); }
            else ChangeOwnership(cell, item, cell.Count);
        }
    }

    /// <summary>Ломается сундук (рабочий инструмент): всё содержимое выпадает. Поставленный сундук возвращается в запас, найденный в мире даёт доски.</summary>
    void BreakStorageChest(Vector2I tile, Vector2 at)
    {
        SpillChest(tile);
        int key = World.Key(tile.X, tile.Y);
        if (Placed.Contains(key)) { Placed.Remove(key); StockAdd("chest", 1); Toast("Сундук: в запасе " + StockOf("chest")); }
        else { Give(It.Wood, rng.RandiRange(2, 3), at); GiveXp(1); }
        if (Window == Win.Chest && ChestAt == tile) CloseWindows();
    }

    /// <summary>Складывает добычу в ячейки сундука: одинаковые ресурсы объединяются в стопки по StackMax.</summary>
    void FillChest(Vector2I tile, IEnumerable<(int item, int n)> loot)
    {
        var cells = ChestCells(tile); var total = new Dictionary<int, int>();
        foreach (var (item, n) in loot) if (n > 0) total[item] = total.GetValueOrDefault(item) + n;
        int slot = 0;
        foreach (var (item, n0) in total)
            for (int left = n0; left > 0 && slot < cells.Length; slot++) { int k = Math.Min(left, StackMax); cells[slot] = MakeChestCell(item, k); left -= k; }
    }

    public void OpenChest(int x, int y)
    {
        if (NetClient) { Fail("Сундуком управляет хозяин мира"); return; }
        CloseWindows(); Window = Win.Chest; ChestAt = new Vector2I(x, y); WinTab = 1; WinCur = 0; ChestMsg = ""; Sfx.Play("tick");
    }

    /// <summary>Закрывает окно, если сундук убрали или игрок ушёл.</summary>
    void UpdateChest()
    {
        if (Window != Win.Chest) return;
        if (W.Get(ChestAt.X, ChestAt.Y) != Tl.StorageChest || PPos.DistanceTo(TileCenter(ChestAt.X, ChestAt.Y)) > 72f) CloseWindows();
    }

    public List<(int item, int n)> ChestStacks()
    {
        var stacks = new List<(int item, int n)>(ChestSlots);
        foreach (var cell in ChestCells(ChestAt)) stacks.Add((ChestItem(cell), cell?.Count ?? 0));
        return stacks;
    }

    public void ChestClick(bool fromChest, int index, bool half, bool all)
    {
        if (Window != Win.Chest) return;
        IReadOnlyList<(int item, int n)> slots = fromChest ? ChestStacks() : InventorySlots();
        if (index < 0 || index >= slots.Count) return;
        WinTab = fromChest ? 1 : 0;
        WinCur = index;
        var (item, count) = slots[index];
        if (count == 0) return;
        if (all && Movable(item)) { ChestMove(!fromChest, item, 0); return; }
        if (half) count = (count + 1) / 2;
        if (fromChest) WithdrawChestToBag(index, -1, item, count);
        else DepositChestSlot(index, false, item, count);
    }

    /// <summary>Клавиши окна сундука. WinTab: 1 — выбрана ячейка сундука, 0 — инвентаря; WinCur — номер ячейки.</summary>
    bool ChestNav(Key k, bool shift)
    {
        int inv = InventorySlots().Count; bool inChest = WinTab == 1; int len = inChest ? ChestSlots : inv; if (len <= 0) return k is Key.Left or Key.Right or Key.Up or Key.Down;
        WinCur = Math.Clamp(WinCur, 0, len - 1);
        switch (k)
        {
            case Key.Left: WinCur = Math.Max(0, WinCur - 1); return true;
            case Key.Right: WinCur = Math.Min(len - 1, WinCur + 1); return true;
            case Key.Up: if (WinCur >= ChestCols) WinCur -= ChestCols; else if (!inChest) { WinTab = 1; WinCur = Math.Min(ChestSlots - 1, (ChestSlots - 1) / ChestCols * ChestCols + WinCur % ChestCols); } return true;
            case Key.Down: if (WinCur + ChestCols < len) WinCur += ChestCols; else if (inChest && inv > 0) { WinTab = 0; WinCur = Math.Min(inv - 1, WinCur % ChestCols); } return true;
            case Key.Tab: if (inChest ? inv > 0 : true) { WinTab = inChest ? 0 : 1; WinCur = Math.Min(WinCur, (WinTab == 1 ? ChestSlots : inv) - 1); } return true;
            case Key.Enter: case Key.KpEnter: ChestClick(inChest, WinCur, false, shift); return true;
        }
        return false;
    }

    void ChestFeedback(bool deposit, int item, int count)
    {
        Sfx.Play("pickup", -4, deposit ? 1f : 1.2f);
        ChestMsg = (deposit ? "Положено: " : "Взято: ") + InventoryItemName(item) + " ×" + count;
    }

    public int ChestMove(bool deposit, int item, int count)
    {
        if (Window != Win.Chest || !Movable(item) || !UsesSlot(item)) return 0;
        var cells = ChestCells(ChestAt);
        int limit = count > 0 ? count : int.MaxValue;
        int moved = 0;
        if (deposit)
        {
            int available = Math.Min(limit, Inv[item]);
            if (available == 0) return 0;
            moved = StoreChestCell(MakeChestCell(item, available), -1);
            Inv[item] -= moved;
        }
        else
        {
            int room = Math.Min(limit, Room(item));
            for (int i = 0; i < cells.Length && moved < room; i++)
            {
                if (ChestItem(cells[i]) != item) continue;
                int taken = Math.Min(cells[i].Count, room - moved);
                RemoveChestCount(i, taken);
                moved += taken;
            }
            Inv[item] += moved;
        }
        if (moved > 0) ChestFeedback(deposit, item, moved);
        return moved;
    }

    // Сначала заполняем подходящие стопки, затем пустые ячейки. Для ручного переноса
    // target указывает единственную ячейку, которую разрешено изменить.
    int StoreChestCell(ChestCell stack, int target)
    {
        var cells = ChestCells(ChestAt);
        int remaining = stack.Count;
        if (target >= 0)
        {
            var old = cells[target];
            if (old == null) { cells[target] = stack; return stack.Count; }
            if (old.Key != stack.Key || IsUniqueChestItem(ChestItem(stack))) return 0;
            int count = Math.Min(remaining, StackMax - old.Count);
            old.Count += count;
            return count;
        }

        if (!IsUniqueChestItem(ChestItem(stack)))
            foreach (var cell in cells)
            {
                if (remaining == 0) break;
                if (cell?.Key != stack.Key) continue;
                int count = Math.Min(remaining, StackMax - cell.Count);
                cell.Count += count;
                remaining -= count;
            }

        for (int i = 0; i < cells.Length && remaining > 0; i++)
        {
            if (cells[i] != null) continue;
            int count = Math.Min(remaining, StackMax);
            cells[i] = new ChestCell { Key = stack.Key, Count = count, Level = stack.Level, Mods = stack.Mods };
            remaining -= count;
        }
        return stack.Count - remaining;
    }

    void RemoveChestCount(int source, int count)
    {
        var cells = ChestCells(ChestAt);
        cells[source].Count -= count;
        if (cells[source].Count == 0) cells[source] = null;
    }

    void SwapWithChest(int source, bool quick, int target, ChestCell outgoing)
    {
        var cells = ChestCells(ChestAt);
        var incoming = cells[target];
        int item = ChestItem(outgoing), other = ChestItem(incoming);
        if (!CanTakeChestItem(other)) return;
        if (!quick && IsInventoryGear(item) && !IsInventoryGear(other) && SlotsUsed() >= SlotCap)
        {
            FullMsg();
            return;
        }

        ChangeOwnership(outgoing, item, -outgoing.Count);
        ChangeOwnership(incoming, other, incoming.Count);
        cells[target] = outgoing;
        if (quick)
        {
            quickItems[source] = other;
            quickCounts[source] = incoming.Count;
            SelectedQuickSlot = source;
        }
        else
        {
            inventorySlots[source] = (other, incoming.Count);
            TrackBagChange(item, -outgoing.Count);
            TrackBagChange(other, incoming.Count);
        }
        ChestFeedback(true, item, outgoing.Count);
    }

    internal void DepositChestSlot(int source, bool quick, int item, int count, int target = -1)
    {
        if (Window != Win.Chest || !UsesSlot(item) || count <= 0 || target >= ChestSlots) return;
        var bag = InventorySlots();
        if (!quick && (source < 0 || source >= bag.Count)) return;
        var stack = quick ? QuickStack(source) : bag[source];
        if (stack.item != item || stack.n < count) return;
        var stored = MakeChestCell(item, count);
        if (target >= 0 && ChestCells(ChestAt)[target] is { } occupied && occupied.Key != stored.Key)
        {
            if (count == stack.n) SwapWithChest(source, quick, target, stored);
            return;
        }
        int moved = StoreChestCell(stored, target);
        if (moved == 0) { Fail("В сундуке нет места"); return; }

        ChangeOwnership(stored, item, -moved);
        if (quick) RemoveQuickCount(source, moved);
        else
        {
            inventorySlots[source] = moved == stack.n ? EmptyStack : (item, stack.n - moved);
            TrackBagChange(item, -moved);
        }
        ChestFeedback(true, item, moved);
    }

    internal void WithdrawChestToBag(int source, int target, int item, int count)
    {
        if (Window != Win.Chest || source < 0 || source >= ChestSlots || count <= 0) return;
        var bag = InventorySlots();
        var cell = ChestCells(ChestAt)[source];
        if (cell == null || ChestItem(cell) != item || !CanTakeChestItem(item)) return;
        int room = IsInventoryGear(item) ? int.MaxValue : Room(item);
        int moved = Math.Min(count, Math.Min(cell.Count, room));
        int targetCount = 0;
        if (target >= 0)
        {
            if (target >= bag.Count) return;
            var other = bag[target];
            if (other.n > 0 && other.item != item)
            {
                if (count >= cell.Count) SwapWithChest(target, false, source, MakeChestCell(other.item, other.n));
                return;
            }
            if (other.n == 0 && !IsInventoryGear(item) && SlotsUsed() >= SlotCap) return;
            targetCount = other.n;
            moved = Math.Min(moved, StackMax - targetCount);
        }
        if (moved <= 0) { FullMsg(); return; }

        ChangeOwnership(cell, item, moved);
        RemoveChestCount(source, moved);
        if (target >= 0)
        {
            inventorySlots[target] = (item, targetCount + moved);
            TrackBagChange(item, moved);
        }
        ChestFeedback(false, item, moved);
    }

    /// <summary>Shift+ЛКМ по ячейке сундука: сначала в хотбар (в такую же стопку, потом в пустой слот), остаток — в рюкзак. count — сколько перенести (Ctrl+Shift: один предмет).</summary>
    internal void ShiftChestOut(int source, int count)
    {
        if (Window != Win.Chest || source < 0 || source >= ChestSlots) return;
        var cells = ChestCells(ChestAt);
        if (cells[source] == null) return;
        int item = ChestItem(cells[source]);
        if (!CanTakeChestItem(item)) return;
        int remaining = Math.Min(count, cells[source].Count);
        int Left() => cells[source]?.Count ?? 0;
        for (int pass = 0; pass < 2 && remaining > 0; pass++)
            for (int i = 0; i < QuickSlotCount && remaining > 0 && Left() > 0; i++)
            {
                var (qi, qn) = QuickStack(i);
                bool match = pass == 0 ? qn > 0 && qi == item && qn < StackMax : qn == 0;
                if (!match) continue;
                int before = Left();
                MoveChestToQuick(source, i, remaining, false);
                remaining -= before - Left();
            }
        if (remaining > 0 && Left() > 0) WithdrawChestToBag(source, -1, item, remaining);
    }

    internal void MoveChestToQuick(int source, int target, int count, bool swap)
    {
        if (Window != Win.Chest || source < 0 || source >= ChestSlots) return;
        var cells = ChestCells(ChestAt);
        var cell = cells[source];
        var (other, otherCount) = QuickStack(target);
        if (cell == null)
        {
            if (swap && otherCount > 0) DepositChestSlot(target, true, other, otherCount, source);
            return;
        }

        int item = ChestItem(cell);
        if (!CanTakeChestItem(item) || count <= 0) return;
        count = Math.Min(count, cell.Count);
        if (!swap && otherCount > 0 && other != item && count == cell.Count) swap = true;
        if (swap && count != cell.Count) return;
        if (!swap && otherCount > 0 && other != item) return;
        if (!swap) count = Math.Min(count, StackMax - otherCount);
        if (count == 0) return;

        if (swap && otherCount > 0)
        {
            SwapWithChest(target, true, source, MakeChestCell(other, otherCount));
            return;
        }
        ChangeOwnership(cell, item, count);
        RemoveChestCount(source, count);
        quickItems[target] = item;
        quickCounts[target] = otherCount + count;
        SelectedQuickSlot = target;
        ChestFeedback(false, item, count);
    }

    internal void MoveChestStack(int source, int target, int count)
    {
        if (Window != Win.Chest || source < 0 || source >= ChestSlots || target < 0 || target >= ChestSlots ||
            source == target || count <= 0) return;
        var cells = ChestCells(ChestAt);
        var from = cells[source];
        if (from == null || count > from.Count) return;
        var to = cells[target];
        if (to != null && (to.Key != from.Key || IsUniqueChestItem(ChestItem(from))))
        {
            if (count == from.Count) (cells[source], cells[target]) = (to, from);
            return;
        }
        if (to == null && count == from.Count)
        {
            (cells[source], cells[target]) = (null, from);
            return;
        }
        count = Math.Min(count, StackMax - (to?.Count ?? 0));
        if (count == 0) return;
        if (to == null) cells[target] = new ChestCell { Key = from.Key, Count = count, Level = from.Level, Mods = from.Mods };
        else to.Count += count;
        RemoveChestCount(source, count);
    }

    // ───────────────────────── сохранение ─────────────────────────
    void SaveChests(WorldSave save)
    {
        var keys = new List<int>();
        var contents = new List<ChestCell[]>();
        foreach (var (key, cells) in Chests)
        {
            if (Array.TrueForAll(cells, cell => cell == null)) continue;
            keys.Add(key);
            contents.Add(cells.Select(cell => cell == null ? null :
                new ChestCell { Key = cell.Key, Count = cell.Count, Level = cell.Level, Mods = cell.Mods }).ToArray());
        }
        save.ChestK = keys.ToArray();
        save.ChestCells = contents.ToArray();
        save.ChestV = null;
    }

    void LoadChests(WorldSave save)
    {
        Chests.Clear();
        if (save.ChestK == null) return;
        for (int i = 0; i < save.ChestK.Length; i++)
        {
            var cells = new ChestCell[ChestSlots];
            if (save.ChestCells != null && i < save.ChestCells.Length && save.ChestCells[i] is { } stored)
            {
                Array.Copy(stored, cells, Math.Min(stored.Length, cells.Length));
            }
            else if (save.ChestV != null && i < save.ChestV.Length && save.ChestV[i] is { } totals)
            {
                int slot = 0;
                for (int item = 0; item < Math.Min(It.Count, totals.Length) && slot < cells.Length; item++)
                {
                    if (!UsesSlot(item)) continue;
                    for (int count = totals[item]; count > 0 && slot < cells.Length; count -= StackMax)
                        cells[slot++] = new ChestCell { Key = $"res:{item}", Count = Math.Min(StackMax, count) };
                }
            }
            Chests[save.ChestK[i]] = cells;
        }
    }
}
