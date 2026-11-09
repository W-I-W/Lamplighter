using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lamplighter;

/// <summary>Крафт внутри инвентаря: два слота-компонента (A + B → результат) и быстрый крафт известных рецептов.
/// Рецепты — данные (CombineDb); открытые рецепты хранятся в HintsShown («rcp_…») и сохраняются вместе с героем.
/// Проверка, оплата и выдача результата выполняются одной синхронной операцией (Combine), поэтому быстрые нажатия не дублируют предметы.</summary>
public partial class Game
{
    readonly (int item, int n)[] craftIngredients = { (-1, 0), (-1, 0), (-1, 0) };      // 0, 1 — компоненты; 2 — готовый результат, который игрок забирает сам (в инвентарь он попадает только при переносе)
    bool combining;

    internal (int item, int n) CraftIngredient(int index) => craftIngredients[index];

    // ───────────────────────── рецепты ─────────────────────────
    internal bool RecipeKnown(CombineRecipe r) => r.Known || HintsShown.Contains("rcp_" + r.Id);

    /// <summary>Компоненты рецепта: явные или (для построек) из цены в меню построек.</summary>
    internal (int a, int na, int b, int nb) RecipeInputs(CombineRecipe r)
    {
        if (!r.Derived) return (r.A, r.NA, r.B, r.NB);
        var cost = EntryOf(r.Build)?.Raw;
        if (cost == null || cost.Length != 2) return (-1, 0, -1, 0);
        return (cost[0][0], cost[0][1], cost[1][0], cost[1][1]);
    }

    /// <summary>Рецепт доступен в игре (постройка открыта, компоненты заданы).</summary>
    internal bool RecipeUsable(CombineRecipe r)
    {
        if (RecipeInputs(r).a < 0) return false;
        return r.Build == null || EntryOf(r.Build) is { Unlocked: true };
    }

    /// <summary>Идентификатор результата в инвентаре (ресурс или ключ постройки): по нему берутся название, значок и подсказка.</summary>
    internal int RecipeResultItem(CombineRecipe r) => r.Build != null ? InventoryItemId(r.Build) : r.ResItem;
    internal int RecipeResultCount(CombineRecipe r) => r.Build != null ? 1 : r.ResN;

    static bool PairMatches((int a, int na, int b, int nb) q, int x, int y) => q.a == x && q.b == y || q.a == y && q.b == x;

    // ───────────────────────── состояние ячеек «A + B» ─────────────────────────
    internal enum CombineState { Empty, Single, Unknown, Short, Blocked, Ready }

    internal sealed class CombineMatch { public CombineState State; public CombineRecipe Recipe; public bool Known; }

    /// <summary>Что получится из текущих двух ячеек: подходящий рецепт (если пара существует), хватает ли количества и влезет ли результат.</summary>
    internal CombineMatch MatchCombine()
    {
        var s0 = craftIngredients[0]; var s1 = craftIngredients[1];
        if (s0.n <= 0 && s1.n <= 0 && craftIngredients[2].n <= 0) return new CombineMatch { State = CombineState.Empty };
        if (s0.n <= 0 && s1.n <= 0) return new CombineMatch { State = CombineState.Blocked };
        if (s0.n <= 0 || s1.n <= 0) return new CombineMatch { State = CombineState.Single };
        CombineRecipe pair = null;
        foreach (var r in CombineDb.All)
        {
            if (!RecipeUsable(r)) continue;
            var q = RecipeInputs(r);
            if (!PairMatches(q, s0.item, s1.item)) continue;
            pair ??= r;
            if (!HaveInCells(q)) continue;
            return new CombineMatch { Recipe = r, Known = RecipeKnown(r), State = OutputFree(r) ? CombineState.Ready : CombineState.Blocked };
        }
        return pair == null ? new CombineMatch { State = CombineState.Unknown } : new CombineMatch { Recipe = pair, Known = RecipeKnown(pair), State = CombineState.Short };
    }

    /// <summary>Ячейка результата свободна (или в ней тот же предмет с местом в стеке).</summary>
    bool OutputFree(CombineRecipe r)
    {
        var o = craftIngredients[2];
        return o.n <= 0 || o.item == RecipeResultItem(r) && o.n + RecipeResultCount(r) <= StackMax;
    }

    /// <summary>Готовый результат переходит в инвентарь (ресурс — в Inv, постройка — в запас).</summary>
    void DeliverOutput(int item, int amount)
    {
        if (item >= 0) Inv[item] += amount; else StockAdd(InventoryKey(item), amount);
    }

    bool HaveInCells((int a, int na, int b, int nb) q) => CellTimes(q) >= 1;

    /// <summary>Сколько раз рецепт можно выполнить из содержимого двух ячеек.</summary>
    int CellTimes((int a, int na, int b, int nb) q)
    {
        var s0 = craftIngredients[0]; var s1 = craftIngredients[1];
        if (q.a == q.b) return s0.item == q.a && s1.item == q.a ? (s0.n + s1.n) / (q.na + q.nb) : 0;
        if (s0.item == q.a && s1.item == q.b) return Math.Min(s0.n / q.na, s1.n / q.nb);
        if (s0.item == q.b && s1.item == q.a) return Math.Min(s0.n / q.nb, s1.n / q.na);
        return 0;
    }

    // ───────────────────────── перенос компонентов ─────────────────────────
    internal int PutCraftIngredient(int source, bool quick, int target, int amount)
    {
        if (target >= 2) return 0;                                              // в ячейку результата ничего положить нельзя
        if (source < 0 || !quick && source >= InventorySlots().Count) return 0;
        var from = quick ? QuickStack(source) : InventorySlots()[source];
        var to = craftIngredients[target];
        if (from.item < 0 || from.n <= 0) return 0;                              // предметы-постройки компонентами не бывают
        bool replace = to.n > 0 && (to.item != from.item || to.n >= StackMax && amount >= from.n && from.n < StackMax);                       // замена компонента: прежний возвращается в инвентарь
        if (replace)
        {
            int canMove = Math.Min(amount, Math.Min(from.n, StackMax));
            if (canMove == from.n)                                              // вся стопка из руки: ячейка и компонент меняются местами
            {
                var old = to;
                if (quick) { quickItems[source] = old.item; quickCounts[source] = old.n; }
                else { inventorySlots[source] = old; TrackBagChange(from.item, -from.n); TrackBagChange(old.item, old.n); }
                Inv[old.item] += old.n; Inv[from.item] -= from.n;
                craftIngredients[target] = from;
                return from.n;
            }
            if ((quick || canMove < from.n) && !CanFillEmptyInventorySlot(to.item)) return 0;     // некуда вернуть прежний предмет (из горячей панели сумка не освобождается)
            Inv[to.item] += to.n; craftIngredients[target] = (-1, 0); to = (-1, 0);
        }
        int moved = Math.Min(amount, Math.Min(from.n, StackMax - to.n));
        if (moved <= 0) return 0;
        if (quick) RemoveQuickCount(source, moved);
        else
        {
            inventorySlots[source] = from.n == moved ? EmptyStack : (from.item, from.n - moved);
            TrackBagChange(from.item, -moved);
        }
        Inv[from.item] -= moved;
        craftIngredients[target] = (from.item, to.n + moved);
        return moved;
    }

    internal int TakeCraftIngredient(int source, int area, int target, int amount)
    {
        var from = craftIngredients[source];
        if (from.n <= 0) return 0;
        var to = area == 0 ? QuickStack(target) : InventorySlots()[target];
        if (to.n > 0 && (to.item != from.item || to.n >= StackMax && amount >= from.n && from.n < StackMax))
        {
            if (source >= 2 || to.item < 0 || from.item < 0 || amount < from.n) return 0;      // обмен только целой стопкой компонента, результат и постройки не обмениваются
            if (area == 0) { quickItems[target] = from.item; quickCounts[target] = from.n; }
            else { inventorySlots[target] = from; TrackBagChange(from.item, from.n); TrackBagChange(to.item, -to.n); }
            Inv[from.item] += from.n; Inv[to.item] -= to.n;
            craftIngredients[source] = to;
            return from.n;
        }
        if (area == 2 && to.n == 0 && !CanFillEmptyInventorySlot(from.item)) return 0;
        int moved = Math.Min(amount, Math.Min(from.n, StackMax - to.n));
        if (moved <= 0) return 0;
        DeliverOutput(from.item, moved);
        if (area == 0) { quickItems[target] = from.item; quickCounts[target] = to.n + moved; }
        else { inventorySlots[target] = (from.item, to.n + moved); TrackBagChange(from.item, moved); }
        craftIngredients[source] = moved == from.n ? (-1, 0) : (from.item, from.n - moved);
        return moved;
    }

    /// <summary>Вернуть содержимое ячеек в инвентарь. Что не помещается в сумку — выбрасывается на землю (allowDrop), а не раздувает инвентарь сверх лимита.</summary>
    /// <summary>Перенос между ячейками-компонентами: та же стопка складывается, разные предметы меняются местами (целой стопкой).</summary>
    internal int MoveCraftCell(int source, int target, int count)
    {
        if (source == target || source >= 2 || target >= 2) return 0;
        var a = craftIngredients[source]; var b = craftIngredients[target];
        if (a.n <= 0 || count <= 0) return 0;
        if (b.n > 0 && (b.item != a.item || b.n >= StackMax && count >= a.n && a.n < StackMax))
        {
            if (count < a.n) return 0;
            craftIngredients[source] = b; craftIngredients[target] = a; return a.n;
        }
        int moved = Math.Min(count, Math.Min(a.n, StackMax - b.n));
        if (moved <= 0) return 0;
        craftIngredients[target] = (a.item, b.n + moved);
        craftIngredients[source] = moved == a.n ? (-1, 0) : (a.item, a.n - moved);
        return moved;
    }

    internal void ReturnCraftIngredients(bool allowDrop = true, int cells = 3)
    {
        for (int i = 0; i < cells; i++)
        {
            var stack = craftIngredients[i];
            if (stack.n <= 0) continue;
            if (allowDrop && !FitsItem(stack.item, stack.n)) DropCraftCell(i, stack.item, stack.n);
            else { DeliverOutput(stack.item, stack.n); craftIngredients[i] = (-1, 0); }
        }
    }

    /// <summary>Поместится ли стопка в сумку (с учётом уже лежащих стопок и свободных слотов).</summary>
    bool FitsItem(int item, int count)
    {
        if (testing && !TestSlots) return true;
        if (item >= 0 && !UsesSlot(item)) return true;
        SyncInventorySlots();
        int used = 0, room = 0;
        foreach (var st in inventorySlots)
        {
            if (st.n > 0 && !IsInventoryGear(st.item) && UsesSlot(st.item)) used++;
            if (st.item == item && st.n > 0) room += StackMax - st.n;
        }
        return room + Math.Max(0, SlotCap - used) * StackMax >= count;
    }

    /// <summary>Очистить ячейки без выдачи (при смене героя Inv перезаписывается целиком).</summary>
    internal void ClearCraftCells() { for (int i = 0; i < craftIngredients.Length; i++) craftIngredients[i] = (-1, 0); }

    internal void IncludeCraftIngredientsInSave(int[] inventory)
    {
        foreach (var stack in craftIngredients) if (stack.n > 0 && stack.item >= 0) inventory[stack.item] += stack.n;
    }

    /// <summary>Запас построек для сохранения: готовая постройка, лежащая в ячейке результата, считается уже полученной.</summary>
    internal (string[] k, int[] v) BuildStockForSave()
    {
        var d = new Dictionary<string, int>(BuildStock); var o = craftIngredients[2];
        if (o.n > 0 && o.item < 0) { string key = InventoryKey(o.item); d[key] = d.GetValueOrDefault(key) + o.n; }
        return (d.Keys.ToArray(), d.Values.ToArray());
    }

    // ───────────────────────── место под результат ─────────────────────────
    /// <summary>Влезет ли результат рецепта (times раз) в инвентарь после списания компонентов из сумки (spend — списание из Inv, null — из ячеек крафта).</summary>
    bool FitsResult(CombineRecipe r, int times, int[] spend)
    {
        int item = RecipeResultItem(r), count = RecipeResultCount(r) * times;
        if (testing && !TestSlots) return true;
        if (item >= 0 && !UsesSlot(item)) return true;
        SyncInventorySlots();
        var stacks = new List<(int item, int n)>(inventorySlots);
        if (spend != null)
            for (int i = stacks.Count - 1; i >= 0; i--)
            {
                var st = stacks[i];
                if (st.item < 0 || st.n <= 0 || spend[st.item] <= 0) continue;
                int take = Math.Min(spend[st.item], st.n); spend[st.item] -= take;
                stacks[i] = take == st.n ? EmptyStack : (st.item, st.n - take);
            }
        int used = 0, room = 0;
        foreach (var st in stacks)
        {
            if (st.n > 0 && !IsInventoryGear(st.item) && UsesSlot(st.item)) used++;
            if (st.item == item && st.n > 0) room += StackMax - st.n;
        }
        return room + Math.Max(0, SlotCap - used) * StackMax >= count;
    }

    int[] SpendArray(CombineRecipe r, int times)
    {
        var q = RecipeInputs(r); var spend = new int[It.Count]; spend[q.a] += q.na * times; spend[q.b] += q.nb * times; return spend;
    }

    // ───────────────────────── создание ─────────────────────────
    /// <summary>Сколько раз рецепт можно выполнить прямо из инвентаря (запас Inv и место под результат).</summary>
    internal int PoolTimes(CombineRecipe r)
    {
        var q = RecipeInputs(r); if (q.a < 0) return 0;
        int times = q.a == q.b ? Inv[q.a] / (q.na + q.nb) : Math.Min(Inv[q.a] / q.na, Inv[q.b] / q.nb);
        return Math.Min(times, 99);                                          // место в инвентаре не ограничивает: результат, который не влез, выбрасывается на землю
    }

    /// <summary>Можно ли выбросить результат рецепта на землю, когда он не помещается в инвентарь.</summary>
    bool CanDropResult(CombineRecipe r)
    {
        int item = RecipeResultItem(r);
        return item >= 0 ? Movable(item) : Authority && Array.IndexOf(BuildDropKeys, InventoryKey(item)) >= 0;
    }

    /// <summary>Выбросить созданное на землю рядом с игроком (стопками не больше MaxMove).</summary>
    void DropResult(CombineRecipe r, int count)
    {
        int item = RecipeResultItem(r), id = item >= 0 ? item : BuildBase + Array.IndexOf(BuildDropKeys, InventoryKey(item));
        while (count > 0)
        {
            int n = Math.Min(count, MaxMove); count -= n;
            if (Authority) HostDrop(LocalPeer, id, n); else Net?.SendDrop(id, n);
        }
    }

    /// <summary>Создание из двух ячеек: одна операция проверки, списания и выдачи. max — столько раз, сколько позволяют ячейки и место.</summary>
    internal bool CombineFromCells(bool max)
    {
        if (combining) return false;
        combining = true;
        try
        {
            var m = MatchCombine();
            if (m.State == CombineState.Blocked) { Fail("Сначала заберите результат"); return false; }
            if (m.State != CombineState.Ready) { if (m.State == CombineState.Short) Fail("Не хватает ресурсов"); else if (m.State == CombineState.Unknown) Fail("Неизвестная комбинация"); return false; }
            var q = RecipeInputs(m.Recipe);
            int times = max ? Math.Min(CellTimes(q), 99) : 1;
            times = Math.Max(1, Math.Min(times, (StackMax - craftIngredients[2].n) / RecipeResultCount(m.Recipe)));
            if (q.a == q.b)
            {
                int left = (q.na + q.nb) * times;
                for (int i = 0; i < 2 && left > 0; i++)
                {
                    var st = craftIngredients[i]; int used = Math.Min(left, st.n);
                    craftIngredients[i] = used == st.n ? (-1, 0) : (st.item, st.n - used); left -= used;
                }
            }
            else
            {
                for (int i = 0; i < 2; i++)
                {
                    var st = craftIngredients[i]; int need = (st.item == q.a ? q.na : q.nb) * times;
                    craftIngredients[i] = need >= st.n ? (-1, 0) : (st.item, st.n - need);
                }
            }
            int item = RecipeResultItem(m.Recipe), total = RecipeResultCount(m.Recipe) * times;
            craftIngredients[2] = (item, craftIngredients[2].n + total);                 // результат лежит в ячейке, пока игрок не заберёт его сам
            Reward(m.Recipe, times);
            return true;
        }
        finally { combining = false; }
    }

    /// <summary>Выброс результата из руки за пределы окна: предмет попадает в инвентарь и возвращается номер ячейки сумки, где он лёг (или -1).</summary>
    internal int ReceiveCraftOutput(int amount) => ReceiveCraftCell(2, amount);

    /// <summary>Выбросить стопку из ячейки крафта на землю. Если предмет выбросить нельзя, он просто уходит в инвентарь. Предмет в ячейке должен совпадать с ожидаемым (ячейка могла измениться, пока стопка была в руке).</summary>
    internal bool DropCraftCell(int cell, int item, int amount)
    {
        var o = craftIngredients[cell];
        if (o.n <= 0 || o.item != item || amount <= 0) return false;
        amount = Math.Min(amount, o.n);
        DeliverOutput(item, amount); craftIngredients[cell] = amount == o.n ? (-1, 0) : (item, o.n - amount);
        return item >= 0 ? TryDrop(item, amount) : TryDropBuild(InventoryKey(item), amount);
    }

    /// <summary>Перенос стопки из любой ячейки крафта в инвентарь (как у обычной ячейки): возвращает номер ячейки сумки или -1.</summary>
    internal int ReceiveCraftCell(int cell, int amount)
    {
        var o = craftIngredients[cell]; amount = Math.Min(amount, o.n);
        if (amount <= 0) return -1;
        DeliverOutput(o.item, amount); craftIngredients[cell] = amount == o.n ? (-1, 0) : (o.item, o.n - amount);
        var bag = InventorySlots();
        for (int i = bag.Count - 1; i >= 0; i--) if (bag[i].item == o.item && bag[i].n > 0) return i;
        return -1;
    }

    /// <summary>Тестовый помощник: переносит всё из ячейки результата в инвентарь.</summary>
    internal void CollectOutput()
    {
        var o = craftIngredients[2]; if (o.n <= 0) return;
        DeliverOutput(o.item, o.n); craftIngredients[2] = (-1, 0);
    }

    /// <summary>Клик по рецепту в быстром крафте: компоненты выкладываются в ячейки «A + B», результат забирается из ячейки результата. max — набор на максимум повторов.</summary>
    internal bool LayoutRecipe(CombineRecipe r, bool max)
    {
        if (combining || r == null || !RecipeKnown(r) || !RecipeUsable(r)) return false;
        var q = RecipeInputs(r);
        int times = q.a == q.b ? Inv[q.a] / (q.na + q.nb) : Math.Min(Inv[q.a] / q.na, Inv[q.b] / q.nb);
        if (times <= 0) { Fail("Не хватает ресурсов"); return false; }
        times = max ? Math.Min(times, StackMax / Math.Max(q.na, q.nb)) : 1;
        if (q.a == q.b) times = Math.Min(times, StackMax / Math.Max(q.na, q.nb));
        times = Math.Max(1, times);
        ReturnCraftIngredients(true, 2);                                          // готовый результат остаётся в ячейке: его забирает игрок
        times = Math.Min(times, q.a == q.b ? Inv[q.a] / (q.na + q.nb) : Math.Min(Inv[q.a] / q.na, Inv[q.b] / q.nb));
        if (times <= 0) return false;
        Inv[q.a] -= q.na * times; Inv[q.b] -= q.nb * times;
        craftIngredients[0] = (q.a, q.na * times); craftIngredients[1] = (q.b, q.nb * times);
        Sfx.Play("tick", -6, 1.3f);
        return true;
    }

    /// <summary>Быстрый крафт: списывает компоненты из инвентаря. max — максимум, иначе один раз.</summary>
    internal bool CombineFromInventory(CombineRecipe r, bool max)
    {
        if (combining || r == null) return false;
        combining = true;
        try
        {
            if (!RecipeKnown(r) || !RecipeUsable(r)) return false;
            int can = PoolTimes(r);
            if (can > 0 && !FitsResult(r, 1, SpendArray(r, 1)) && !CanDropResult(r)) { Fail("Инвентарь полон"); return false; }
            if (can <= 0)
            {
                var q = RecipeInputs(r);
                bool enough = q.a == q.b ? Inv[q.a] >= q.na + q.nb : Inv[q.a] >= q.na && Inv[q.b] >= q.nb;
                Fail(enough ? "Инвентарь полон" : "Не хватает ресурсов"); return false;
            }
            int times = max ? can : 1;
            bool fits = FitsResult(r, times, SpendArray(r, times));
            var qq = RecipeInputs(r); Inv[qq.a] -= qq.na * times; Inv[qq.b] -= qq.nb * times;
            if (fits) Deliver(r, times);
            else { DropResult(r, RecipeResultCount(r) * times); Reward(r, times); Save(); }
            return true;
        }
        finally { combining = false; }
    }

    /// <summary>Выдача результата: ресурс в Inv или постройка в запас; опыт, открытие рецепта, сохранение.</summary>
    void Deliver(CombineRecipe r, int times)
    {
        if (r.Build != null) StockAdd(r.Build, times); else Inv[r.ResItem] += r.ResN * times;
        Reward(r, times); Save();
    }

    /// <summary>Опыт, звук, открытие рецепта и уведомление о создании.</summary>
    void Reward(CombineRecipe r, int times)
    {
        bool discovered = !RecipeKnown(r);
        if (r.Build != null) { GiveXp(BuildCraftXp(r.Build) * times); Sfx.Play("build", -3, 1.2f); }
        else { GiveXp(r.Xp * times); Sfx.Play("build", -3, 1.4f); }
        string name = Loc.T(InventoryItemName(RecipeResultItem(r))); int total = RecipeResultCount(r) * times;
        if (discovered)
        {
            HintsShown.Add("rcp_" + r.Id);
            Toast(Loc.T("Открыт новый рецепт") + ": " + name, MsgTone.Gold);
        }
        else Toast(Loc.T("Создано") + ": " + name + (total > 1 ? " ×" + total : ""));
    }

    /// <summary>Известные и доступные рецепты для панели быстрого крафта.</summary>
    internal List<(CombineRecipe r, int times)> QuickRecipes(bool all)
    {
        var list = new List<(CombineRecipe, int)>();
        foreach (var r in CombineDb.All)
        {
            if (!RecipeKnown(r) || !RecipeUsable(r)) continue;
            int t = PoolTimes(r);
            if (all || t > 0) list.Add((r, t));
        }
        return list;
    }

    /// <summary>Отпечаток данных быстрого крафта: меняется при получении, расходовании и перемещении ресурсов, открытии рецептов.</summary>
    internal string QuickSignature()
    {
        var sb = new System.Text.StringBuilder();
        foreach (int n in Inv) sb.Append(n).Append(',');
        sb.Append(SlotsUsed()).Append('|').Append(BuildStock.Count).Append('|');
        foreach (var r in CombineDb.All) sb.Append(RecipeKnown(r) ? '1' : '0').Append(RecipeUsable(r) ? '1' : '0');
        return sb.ToString();
    }
}
