using Godot;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Инвентарь (I): сетка стопок ресурсов с перетаскиванием между слотами.
/// Сцена Scenes/UI/InventoryWindow.tscn; обновляются только изменившиеся ячейки.</summary>
public partial class InventoryWindow : Control
{
    public Game G;
    Control panel; ItemTipPanel tipPanel; HotbarRow hotbar; GridContainer grid; Label slotsL; readonly List<UiSlot> slots = new();
    UiSlot[] craftSlots, craftCells; UiSlot craftResult; Control craftPanel;Button tabAvailable, tabAll; GridContainer quickGrid; readonly List<UiSlot> quickSlots = new();
    List<(CombineRecipe r, int times)> quickList = new(); bool quickAll; string quickSig = "";
    readonly List<(int item, int n)> shown = new();
    public int Columns => grid.Columns;

    public override void _Ready()
    {
        slotsL = GetNode<Label>("%SlotUsage");
        preview = GetNode<UiSlot>("CarryPreview");
        previewIcon = preview.GetNode<TextureRect>("%Icon");
        craftSlots = new[] { GetNode<UiSlot>("%Ingredient1"), GetNode<UiSlot>("%Ingredient2") };
        craftResult = GetNode<UiSlot>("%Result"); craftCells = new[] { craftSlots[0], craftSlots[1], craftResult };
        panel = GetNode<Control>("%Panel"); grid = GetNode<GridContainer>("%Grid");
        var oldInfo = GetNode<Control>("%Panel/V/Info"); oldInfo.GetParent().RemoveChild(oldInfo); oldInfo.QueueFree();
        tipPanel = ItemTipPanel.Create(this);
        var box = grid.GetParent();
        var gap = new Control { CustomMinimumSize = new Vector2(0, 3) }; box.AddChild(gap); box.MoveChild(gap, grid.GetIndex() + 1);
        hotbar = HotbarRow.Create(); box.AddChild(hotbar); box.MoveChild(hotbar, gap.GetIndex() + 1);
        BuildCraftPanel();
    }

    /// <summary>Блок создания справа от основной панели: «A + B → результат», кнопка «Создать» и быстрый крафт известных рецептов.</summary>
    void BuildCraftPanel()
    {
        var craft = GetNode<Control>("Center/Panel/V/Craft"); var center = panel.GetParent();
        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; row.AddThemeConstantOverride("separation", 3);
        center.AddChild(row); panel.Reparent(row, false); panel.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        var cp = new PanelContainer { SizeFlagsVertical = SizeFlags.ShrinkBegin }; craftPanel = cp; row.AddChild(cp);
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 3); cp.AddChild(v);
        var title = new Label { Text = Loc.T("Создание"), ThemeTypeVariation = "GoldLabel" }; v.AddChild(title);
        craft.Reparent(v, false); craft.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        v.AddChild(new HSeparator());
        v.AddChild(new Label { Text = Loc.T("Быстрый крафт"), ThemeTypeVariation = "GoldLabel" });
        var head = new HBoxContainer(); head.AddThemeConstantOverride("separation", 2); v.AddChild(head);
        
        tabAvailable = QuickTab("Доступные", false); tabAll = QuickTab("Все известные", true); head.AddChild(tabAvailable); head.AddChild(tabAll);
        var sc = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(QuickCols * 25, 2 * 25 + 2), SizeFlagsVertical = SizeFlags.ExpandFill };
        v.AddChild(sc);
        quickGrid = new GridContainer { Columns = QuickCols }; quickGrid.AddThemeConstantOverride("h_separation", 1); quickGrid.AddThemeConstantOverride("v_separation", 1); sc.AddChild(quickGrid);
        UpdateQuickTabs();
    }

    const int QuickCols = 6;
    internal void TestCancelCarry() => CancelCarry();
    internal bool ResultShowsIcon => craftResult.GetNode<TextureRect>("%Icon").Texture != null;
    internal int QuickVisibleCount => quickSlots.FindAll(q => q.Visible).Count;

    Button QuickTab(string text, bool all)
    {
        var b = new Button { Text = Loc.T(text), FocusMode = FocusModeEnum.None };
        b.Pressed += () => { quickAll = all; quickSig = ""; UpdateQuickTabs(); };
        return b;
    }

    void UpdateQuickTabs()
    {
        tabAvailable.ThemeTypeVariation = quickAll ? "Tab" : "TabSelected"; tabAll.ThemeTypeVariation = quickAll ? "TabSelected" : "Tab";
    }

    UiSlot QuickSlot(int i)
    {
        while (quickSlots.Count <= i)
        {
            var s = GD.Load<PackedScene>("res://Scenes/UI/Slot.tscn").Instantiate<UiSlot>(); int idx = quickSlots.Count; s.Index = idx;
            s.Clicked += k => { if (k < quickList.Count) G.LayoutRecipe(quickList[k].r, Input.IsKeyPressed(Key.Shift)); };
            quickGrid.AddChild(s); quickSlots.Add(s);
        }
        return quickSlots[i];
    }

    /// <summary>Обновляет быстрый крафт, только когда изменились запасы, открытые рецепты или режим.</summary>
    void RefreshQuickCraft()
    {
        string sig = G.QuickSignature() + (quickAll ? "A" : "a");
        if (sig == quickSig) return;
        quickSig = sig; quickList = G.QuickRecipes(quickAll);
        for (int i = 0; i < quickList.Count; i++)
        {
            var (r, times) = quickList[i]; var s = QuickSlot(i); s.Visible = true; int idx = i;
            int res = G.RecipeResultItem(r), n = G.RecipeResultCount(r);
            s.Set(G.InventoryItemTexture(res), times > 1 ? times.ToString() : n > 1 ? "×" + n : "", "", times > 0 ? SlotState.Normal : SlotState.Disabled, 0f, "", times > 0 ? 1f : .45f);
            ItemTipHost.Bind(s, () => idx < quickList.Count ? G.RecipeTip(quickList[idx].r, quickList[idx].times) : null);
        }
        for (int i = quickList.Count; i < quickSlots.Count; i++) quickSlots[i].Visible = false;
    }

    /// <summary>Состояние блока «A + B»: результат, подпись и кнопка.</summary>
    void RefreshCombine()
    {
        var m = G.MatchCombine(); var r = m.Recipe;
        if (preview.Visible && sourceCraft && source < 2) r = null;                                  // компонент взят в руку: рецепт уже не собран
        var (oi, on) = DisplayStack(3, 2, G.CraftIngredient(2).item, G.CraftIngredient(2).n);
        if (on > 0) craftResult.Set(on > 0 ? G.InventoryItemTexture(oi) : null, on > 0 ? on.ToString() : "", "", SlotState.Normal);
        else if (r != null && m.Known && m.State == Game.CombineState.Ready) craftResult.Set(G.InventoryItemTexture(G.RecipeResultItem(r)), G.RecipeResultCount(r) > 1 ? G.RecipeResultCount(r).ToString() : "", "", SlotState.Normal, 0f, "", .6f);       // предпросмотр только когда создать действительно можно
        else if (r != null && !m.Known && m.State == Game.CombineState.Ready) craftResult.Set(null, "", "", SlotState.Ok, 0f, "?");
        else craftResult.Set(null, "", "", SlotState.Normal);
    }

    public bool IsOver(Vector2 p) => OverControl(panel, p) || OverControl(craftPanel, p);
    bool OverControl(Control c, Vector2 p) => c != null && c.IsVisibleInTree() && new Rect2(Vector2.Zero, c.Size).HasPoint(c.GetGlobalTransform().AffineInverse() * (p * G.UiScaleVp));

    UiSlot Slot(int i)
    {
        while (slots.Count <= i)
        {
            var s = GD.Load<PackedScene>("res://Scenes/UI/Slot.tscn").Instantiate<UiSlot>(); int idx = slots.Count; s.Index = idx;
            grid.AddChild(s); slots.Add(s);
        }
        return slots[i];
    }

    int source = -1, carriedItem = -1, carriedCount;
    bool leftHeld, sourceQuick, sourceChest, sourceCraft;
    MouseButton distributionButton;
    bool distributing;
    bool initialRightGrab;
    bool distributionMoved;
    bool sourceExitPending;
    Vector2 distributionStart;
    readonly HashSet<(int area, int index)> distributed = new();
    readonly List<(int area, int index)> distributionOrder = new();
    bool shiftTransfer;
    bool shiftCollect;
    bool repaintSlots;
    Vector2 shiftPosition;
    readonly HashSet<(int area, int index)> shiftVisited = new();
    Win carryWindow, pointerWindow;
    Vector2 pressPosition, carryPointer, pointer;
    UiSlot preview;
    TextureRect previewIcon;

    Texture2D ItemTexture(int item) => G.InventoryItemTexture(item);

    internal bool CarryActive => source >= 0;

    internal int CarriedCount(bool quick, bool chest, int index) =>
        preview.Visible && !sourceCraft && source == index && sourceQuick == quick && sourceChest == chest ? carriedCount : 0;

    internal string CarrySignature => preview.Visible ? $"{source}:{sourceQuick}:{sourceChest}:{carriedCount}:{distributionButton}:{distributing}:{distributionOrder.Count}" : "";

    internal (int item, int n) DisplayStack(int area, int index, int item, int count)
    {
        count -= area == 3 ? (preview.Visible && sourceCraft && source == index ? carriedCount : 0) : CarriedCount(area == 0, area == 1, index);
        if (distributing && distributionButton == MouseButton.Left && distributionOrder.Count > 0 &&
            distributed.Contains((area, index)))
        {
            int share = Mathf.Min(DistributionShare(), Game.StackMax - count);
            if (share > 0) { item = carriedItem; count += share; }
        }
        return (item, Mathf.Max(0, count));
    }

    int PreviewRemaining()
    {
        if (!distributing || distributionButton != MouseButton.Left || distributionOrder.Count == 0) return carriedCount;
        int share = DistributionShare();
        int assigned = 0;
        foreach (var (area, index) in distributionOrder)
            assigned += Mathf.Min(share, Game.StackMax - VisibleCellCount(area, index));
        return carriedCount - assigned;
    }

    int DistributionShare() => distributionMoved ? carriedCount / distributionOrder.Count : 1;

    internal UiSlot HotbarSlot(int i) => ChestOpen ? G.ChestUi.HotbarSlot(i) : hotbar.Slot(i);

    bool ChestOpen => G.Window == Win.Chest;

    int QuickAt(Vector2 p)
    {
        return ChestOpen ? G.ChestUi.QuickAt(p) : hotbar.QuickAt(p);      // хотбар HUD под затемнением недоступен: только ряд внутри окна
    }

    int BagAt(Vector2 p) => ChestOpen ? G.ChestUi.CellAt(p, false) :
        slots.FindIndex(s => s.IsVisibleInTree() && new Rect2(Vector2.Zero, s.Size)
            .HasPoint(s.GetGlobalTransformWithCanvas().AffineInverse() * p));

    int ChestAt(Vector2 p) => ChestOpen ? G.ChestUi.CellAt(p, true) : -1;

    int CraftAt(Vector2 p) => ChestOpen ? -1 : System.Array.FindIndex(craftCells, s => Hit(s, p));
    static bool Hit(Control s, Vector2 p) => s.IsVisibleInTree() && new Rect2(Vector2.Zero, s.Size).HasPoint(s.GetGlobalTransformWithCanvas().AffineInverse() * p);

    (int item, int n) SourceStack()
    {
        if (sourceCraft) return G.CraftIngredient(source);
        if (sourceQuick) return G.QuickStack(source);
        IReadOnlyList<(int item, int n)> cells = sourceChest ? G.ChestStacks() : G.InventorySlots();
        return source < cells.Count ? cells[source] : (-1, 0);
    }

    void CancelCarry()
    {
        int previousSource = source;
        source = -1;
        carriedCount = 0;
        leftHeld = false;
        distributing = false;
        shiftCollect = false;
        initialRightGrab = false;
        distributionMoved = false;
        sourceExitPending = false;
        distributionButton = MouseButton.None;
        distributed.Clear();
        distributionOrder.Clear();
        repaintSlots = true;
        if (preview != null) preview.Hide();
        if (!sourceQuick && !sourceChest && !sourceCraft && carryWindow == Win.Inventory &&
            previousSource >= 0 && previousSource < shown.Count) UpdateSlot(previousSource);
    }

    void PositionCarry(Vector2 viewportPosition)
    {
        var iconCenter = previewIcon.GetGlobalTransformWithCanvas() * (previewIcon.Size * .5f);
        preview.Position += GetGlobalTransformWithCanvas().AffineInverse().BasisXform(viewportPosition - iconCenter);
    }

    void ShowCarry(Vector2 viewportPosition)
    {
        carryPointer = viewportPosition;
        int remaining = PreviewRemaining();
        previewIcon.Visible = remaining > 0;
        preview.Set(ItemTexture(carriedItem), remaining > 1 ? remaining.ToString() : "", "", SlotState.Normal);
        preview.Show();
        PositionCarry(viewportPosition);
        if (!sourceQuick && !sourceChest && !sourceCraft && !ChestOpen && source < shown.Count) UpdateSlot(source);
    }

    void UpdateSlot(int index)
    {
        var (item, count) = shown[index];
        (item, count) = DisplayStack(2, index, item, count);
        var slot = Slot(index);
        slot.Visible = true;
        slot.Set(count > 0 ? ItemTexture(item) : null, count > 0 ? count.ToString() : "", "", SlotState.Normal);
    }

    void PlaceCarry(int target, int chest, int quick, Vector2 position)
    {
        int craft = CraftAt(pointer);
        if (sourceCraft && craft < 0 && target < 0 && chest < 0 && quick < 0 && !ChestOpen && !IsOver(position))
        {
            int amount = carriedCount, item = carriedItem, cell = source;
            CancelCarry();
            G.DropCraftCell(cell, item, amount);                      // ресурс выбрасывается; постройки на землю выбрасывать нельзя (в игре на земле лежат только ресурсы и реликвии) — они остаются в сумке, установка не начинается
            return;
        }
        if (sourceCraft || craft >= 0)
        {
            int area = craft >= 0 ? 3 : quick >= 0 ? 0 : target >= 0 ? 2 : -1;
            int index = craft >= 0 ? craft : quick >= 0 ? quick : target;
            if (IsSourceCell(area, index)) { CancelCarry(); return; }
            if (area >= 0)
            {
                MoveCarried(area, index, carriedCount);
                if (carriedCount == 0)
                {
                    var swapped = SourceStack();                                            // обмен: на месте взятой стопки теперь лежит вытесненная — она остаётся в руке
                    if (swapped.n > 0 && !IsSourceCell(area, index)) { carriedItem = swapped.item; carriedCount = swapped.n; ShowCarry(pointer); }
                    else CancelCarry();
                }
                else ShowCarry(pointer);
            }
            return;
        }
        var stack = SourceStack();
        if (stack.item != carriedItem || stack.n < carriedCount)
        {
            CancelCarry();
            return;
        }
        bool sameCell = sourceQuick && quick == source || sourceChest && chest == source ||
            !sourceQuick && !sourceChest && target == source;
        if (sameCell) { CancelCarry(); return; }
        bool targetCell = quick >= 0 || chest >= 0 || target >= 0;
        (int item, int n) dst = quick >= 0 ? G.QuickStack(quick) : target >= 0 && chest < 0 && target < G.InventorySlots().Count ? G.InventorySlots()[target] : (-1, 0);
        if (!sourceChest && chest < 0 && !sourceCraft && carriedCount < stack.n && dst.n > 0 && dst.item != carriedItem && (quick >= 0 || target >= 0))     // в руке часть стопки: она ложится в занятую ячейку, вытесненная стопка берётся в руку
        {
            if (G.PartialSwap(sourceQuick, source, carriedCount, quick >= 0, quick >= 0 ? quick : target, out int home))
            {
                var old = G.InventorySlots()[home]; source = home; sourceQuick = false; carriedItem = old.item; carriedCount = old.n; repaintSlots = true; ShowCarry(pointer);
            }
            return;
        }
        bool fullSwap = !sourceChest && chest < 0 && dst.item == carriedItem && dst.n >= Game.StackMax && carriedCount == stack.n && stack.n < Game.StackMax;      // полная ячейка с тем же предметом: стопки меняются местами
        if (quick >= 0)
        {
            if (sourceQuick) G.MoveQuickStack(source, quick, carriedCount);
            else if (sourceChest) G.MoveChestToQuick(source, quick, carriedCount, false);
            else G.MoveToQuickSlot(source, quick, carriedItem, carriedCount);
        }
        else if (chest >= 0)
        {
            if (sourceChest) G.MoveChestStack(source, chest, carriedCount);
            else G.DepositChestSlot(source, sourceQuick, carriedItem, carriedCount, chest);
        }
        else if (target >= 0)
        {
            if (sourceQuick) G.MoveQuickToBag(source, target, carriedCount);
            else if (sourceChest) G.WithdrawChestToBag(source, target, carriedItem, carriedCount);
            else G.MoveInventoryStack(source, target, carriedItem, carriedCount);
        }
        else if (!ChestOpen && !sourceChest && !IsOver(position))
        {
            if ((sourceQuick ? G.DropQuickStack(source, carriedItem, carriedCount) : G.DropInventoryStack(source, carriedItem, carriedCount)) ||
                G.BeginInventoryPlacement(carriedItem, sourceQuick ? -2 - source : source)) CancelCarry();           // клик за окном выбрасывает; установка начинается только для предметов, которые выбросить нельзя
            return;
        }
        if (fullSwap) { carriedCount = SourceStack().n; ShowCarry(pointer); return; }                  // обмен: вытесненная стопка остаётся в руке
        if (!targetCell) return;
        var remaining = SourceStack();
        if (remaining.n > 0 && remaining.item != carriedItem) { carriedItem = remaining.item; carriedCount = remaining.n; ShowCarry(pointer); return; }     // обмен: в руке теперь вытесненная стопка
        int moved = remaining.item == carriedItem ? Mathf.Max(0, stack.n - remaining.n) : carriedCount;
        if (moved >= carriedCount) CancelCarry();
        else if (moved > 0)
        {
            carriedCount -= moved;
            ShowCarry(pointer);
        }
    }

    (int area, int index) CellAt(Vector2 position)
    {
        int quick = QuickAt(position), chest = ChestAt(position), bag = BagAt(position), craft = CraftAt(position);
        int area = quick >= 0 ? 0 : chest >= 0 ? 1 : bag >= 0 ? 2 : craft >= 0 ? 3 : -1;
        int target = area == 0 ? quick : area == 1 ? chest : area == 2 ? bag : craft;
        return (area, target);
    }

    (int item, int n) CellStack(int area, int target) => area == 0 ? G.QuickStack(target) :
        area == 1 ? G.ChestStacks()[target] : area == 3 ? G.CraftIngredient(target) : G.InventorySlots()[target];

    bool IsSourceCell(int area, int target) => target == source &&
        (area == 0 && sourceQuick || area == 1 && sourceChest || area == 3 && sourceCraft || area == 2 && !sourceQuick && !sourceChest && !sourceCraft);

    int VisibleCellCount(int area, int target)
    {
        int count = CellStack(area, target).n;
        return IsSourceCell(area, target) ? Mathf.Max(0, count - carriedCount) : count;
    }

    bool CanDistribute(int area, int target)
    {
        if (area < 0) return false;
        if (IsSourceCell(area, target)) return carriedCount > 0;
        if (area == 3 && (sourceChest || carriedItem < 0) || sourceCraft && area is not (0 or 2 or 3)) return false;
        if (sourceCraft && area == 3 && (source >= 2 || target >= 2)) return false;
        if (area == 3 && target >= 2) return false;
        if (area == 3) return CellStack(3, target).n < Game.StackMax || CellStack(3, target).item != carriedItem || carriedCount == SourceStack().n && carriedCount < Game.StackMax;   // другой предмет заменяет компонент; полная ячейка меняется местами
        if (sourceChest && area != 1 && !G.CanTakeChestItem(carriedItem)) return false;
        var destination = CellStack(area, target);
        if (sourceCraft && destination.n > 0 && (destination.item != carriedItem || destination.n >= Game.StackMax))   // в ячейку с другим предметом или полную: обмен целой стопкой
            return source < 2 && destination.item >= 0 && carriedItem >= 0 && carriedCount == CellStack(3, source).n && (destination.item != carriedItem || carriedCount < Game.StackMax);
        if (area == 2 && destination.n == 0 && (sourceQuick || sourceChest) &&
            !G.CanFillEmptyInventorySlot(carriedItem)) return false;
        return destination.n < Game.StackMax && (destination.n == 0 || destination.item == carriedItem);
    }

    int MoveCarried(int area, int target, int amount)
    {
        if (!CanDistribute(area, target)) return 0;
        var destination = CellStack(area, target);
        var origin = SourceStack();
        if (origin.item != carriedItem || origin.n < carriedCount) return 0;
        if (IsSourceCell(area, target))
        {
            int returned = Mathf.Min(amount, carriedCount);
            carriedCount -= returned;
            return returned;
        }
        if (sourceCraft && area == 3) { int t = G.MoveCraftCell(source, target, amount); carriedCount -= t; return t; }
        if ((area == 3 || sourceCraft) && destination.n > 0 && (destination.item != carriedItem || destination.n >= Game.StackMax)) destination = (-1, 0);          // замена/обмен: прежний предмет окажется на месте взятого
        amount = Mathf.Min(amount, Mathf.Min(carriedCount, Game.StackMax - destination.n));
        if (amount <= 0) return 0;
        if (area == 3 || sourceCraft)
        {
            int transferred = sourceCraft ? G.TakeCraftIngredient(source, area, target, amount) : G.PutCraftIngredient(source, sourceQuick, target, amount);
            carriedCount -= transferred;
            return transferred;
        }
        if (area == 0)
        {
            if (sourceQuick) G.MoveQuickStack(source, target, amount);
            else if (sourceChest) G.MoveChestToQuick(source, target, amount, false);
            else G.MoveToQuickSlot(source, target, carriedItem, amount);
        }
        else if (area == 1)
        {
            if (sourceChest) G.MoveChestStack(source, target, amount);
            else G.DepositChestSlot(source, sourceQuick, carriedItem, amount, target);
        }
        else
        {
            if (sourceQuick) G.MoveQuickToBag(source, target, amount);
            else if (sourceChest) G.WithdrawChestToBag(source, target, carriedItem, amount);
            else G.MoveInventoryStack(source, target, carriedItem, amount);
        }
        var result = area == 2 ? G.InventorySlotBeforeResize(target) : CellStack(area, target);
        int moved = result.item == carriedItem ? Mathf.Max(0, result.n - destination.n) : 0;
        carriedCount -= moved;
        return moved;
    }

    void VisitDistributionCell(Vector2 position, bool final = false)
    {
        if (distributionButton == MouseButton.Left && carriedCount == 1 && !final) return;
        var (area, target) = CellAt(position);
        if (area >= 0)
        {
            if (sourceExitPending && IsSourceCell(area, target)) return;
            if (!IsSourceCell(area, target)) sourceExitPending = false;
        }
        if (!CanDistribute(area, target) || !distributed.Add((area, target))) return;
        distributionOrder.Add((area, target));
        repaintSlots = true;
        if (distributionButton == MouseButton.Right && MoveCarried(area, target, 1) > 0)
        {
            if (carriedCount == 0) CancelCarry();
            else ShowCarry(position);
        }
        else if (distributionButton == MouseButton.Left)
            ShowCarry(position);
    }

    void VisitDistributionPath(Vector2 from, Vector2 to)
    {
        float distance = from.DistanceTo(to);
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / (8f * G.UiScaleVp)));
        for (int i = 1; i <= steps && source >= 0; i++)
            VisitDistributionCell(from.Lerp(to, (float)i / steps));
    }

    /// <summary>Ячейка под курсором занята другим предметом или полна тем же, а в руке целая стопка: клик меняет их местами.</summary>
    bool ReleaseIsSwap(Vector2 position)
    {
        var (area, index) = CellAt(position);
        if (area < 0 || area == 1 || sourceChest || IsSourceCell(area, index) || CanDistribute(area, index)) return false;
        var dest = CellStack(area, index); var origin = SourceStack();
        return dest.n > 0 && (carriedCount == origin.n || !sourceCraft && area != 3 && dest.item != carriedItem) &&
            (dest.item != carriedItem || dest.n >= Game.StackMax && carriedCount < Game.StackMax);
    }

    void FinishDistribution(Vector2 position)
    {
        if (distributing && distributionButton == MouseButton.Left && distributionOrder.Count <= 1 && ReleaseIsSwap(position))
        {
            distributing = false; distributionOrder.Clear(); distributed.Clear();                   // отпустили над занятой ячейкой: это обмен, а не раздача в клетку, которую задели по пути
            PlaceCarry(BagAt(position), ChestAt(position), QuickAt(position), position / G.UiScaleVp);
        }
        else if (distributing)
        {
            VisitDistributionPath(carryPointer, position);
            if (source < 0) return;
            if (distributionButton == MouseButton.Left)
            {
                if (carriedCount == 1 && distributionOrder.Count == 0)
                    VisitDistributionCell(position, true);
                if (source < 0) return;
                distributing = false;
                if (distributionOrder.Count > 0)
                {
                    int share = DistributionShare();
                    foreach (var (area, index) in distributionOrder)
                        if (share > 0) MoveCarried(area, index, share);
                    if (carriedCount == 0) { CancelCarry(); return; }
                    ShowCarry(position);
                }
            }
        }
        else if (!distributing && !initialRightGrab)
            PlaceCarry(BagAt(position), ChestAt(position), QuickAt(position), position / G.UiScaleVp);
        initialRightGrab = false;
        distributionMoved = false;
        distributionButton = MouseButton.None;
        distributing = false;
        leftHeld = false;
        distributed.Clear();
        distributionOrder.Clear();
        repaintSlots = true;
    }

    void ShiftTransferAt(Vector2 position, bool single)
    {
        var (area, index) = CellAt(position);
        if (area < 0 || !shiftVisited.Add((area, index))) return;
        var (item, count) = CellStack(area, index);
        if (count <= 0) return;
        int amount = single ? 1 : count;
        if (ChestOpen)
        {
            if (area == 1) G.ShiftChestOut(index, amount);
            else G.DepositChestSlot(index, area == 0, item, amount);
        }
        else if (area == 0) G.ClearQuickSlot(index);
        else if (area == 2) G.ShiftBagToQuick(index);
    }

    void ShiftTransferPath(Vector2 from, Vector2 to, bool single)
    {
        int steps = Mathf.Max(1, Mathf.CeilToInt(from.DistanceTo(to) / (8f * G.UiScaleVp)));
        for (int i = 1; i <= steps; i++) ShiftTransferAt(from.Lerp(to, (float)i / steps), single);
    }

    void CollectAt(Vector2 position)
    {
        var (area, index) = CellAt(position);
        if (area < 0 || area == 3 || !shiftVisited.Add((area, index)) ||
            (area == 0 && sourceQuick || area == 1 && sourceChest ||
             area == 2 && !sourceQuick && !sourceChest) && index == source) return;
        var donor = CellStack(area, index);
        var origin = SourceStack();
        if (donor.item != carriedItem || donor.n <= 0 || origin.item != carriedItem) return;
        int amount = Mathf.Min(donor.n, Mathf.Min(Game.StackMax - carriedCount, Game.StackMax - origin.n));
        if (amount <= 0) return;

        if (sourceQuick)
        {
            if (area == 0) G.MoveQuickStack(index, source, amount);
            else if (area == 1) G.MoveChestToQuick(index, source, amount, false);
            else G.MoveToQuickSlot(index, source, carriedItem, amount);
        }
        else if (sourceChest)
        {
            if (area == 0) G.DepositChestSlot(index, true, carriedItem, amount, source);
            else if (area == 1) G.MoveChestStack(index, source, amount);
            else G.DepositChestSlot(index, false, carriedItem, amount, source);
        }
        else
        {
            if (area == 0) G.MoveQuickToBag(index, source, amount);
            else if (area == 1) G.WithdrawChestToBag(index, source, carriedItem, amount);
            else G.MoveInventoryStack(index, source, carriedItem, amount);
        }
        var result = SourceStack();
        if (result.item != carriedItem || result.n <= origin.n) return;
        carriedCount += result.n - origin.n;
        repaintSlots = true;
        ShowCarry(position);
    }

    public override void _Input(InputEvent e)
    {
        if (G == null || G.Window is not (Win.Inventory or Win.Chest) || G.Menu != Mn.None || G.ChatOpen) return;
        if (pointerWindow != G.Window)
        {
            pointerWindow = G.Window;
            pointer = GetViewport().GetMousePosition();
        }
        if (e is InputEventMouse mouse) pointer = mouse.Position;
        if (shiftCollect && e is InputEventMouseMotion collectMotion)
        {
            if (!collectMotion.ShiftPressed) { shiftCollect = false; shiftVisited.Clear(); return; }
            int steps = Mathf.Max(1, Mathf.CeilToInt(shiftPosition.DistanceTo(collectMotion.Position) / (8f * G.UiScaleVp)));
            for (int i = 1; i <= steps; i++) CollectAt(shiftPosition.Lerp(collectMotion.Position, (float)i / steps));
            shiftPosition = collectMotion.Position;
            carryPointer = collectMotion.Position;
            PositionCarry(carryPointer);
            GetViewport().SetInputAsHandled();
            return;
        }
        if (shiftCollect && e is InputEventMouseButton collectRelease &&
            collectRelease.ButtonIndex == MouseButton.Left && !collectRelease.Pressed)
        {
            shiftCollect = false;
            shiftVisited.Clear();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (shiftTransfer && e is InputEventMouseMotion shiftMotion)
        {
            if (!shiftMotion.ShiftPressed) { shiftTransfer = false; shiftVisited.Clear(); return; }
            ShiftTransferPath(shiftPosition, shiftMotion.Position, shiftMotion.CtrlPressed);
            shiftPosition = shiftMotion.Position;
            GetViewport().SetInputAsHandled();
            return;
        }
        if (shiftTransfer && e is InputEventMouseButton shiftRelease &&
            shiftRelease.ButtonIndex == MouseButton.Left && !shiftRelease.Pressed)
        {
            shiftTransfer = false;
            shiftVisited.Clear();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (source >= 0 && carryWindow != G.Window) CancelCarry();
        if (source >= 0 && !sourceCraft && leftHeld && e is InputEventKey shiftKey &&
            shiftKey.Keycode == Key.Shift && shiftKey.Pressed && !shiftKey.Echo)
        {
            Vector2 start = pressPosition * G.UiScaleVp;
            CancelCarry();
            shiftTransfer = true;
            shiftVisited.Clear();
            ShiftTransferAt(start, shiftKey.CtrlPressed);
            shiftPosition = pointer;
            GetViewport().SetInputAsHandled();
            return;
        }
        if (e is InputEventKey key && key.Pressed && !key.Echo)
        {
            if (key.Keycode == Key.Escape && source >= 0) CancelCarry();
            else if (key.Keycode >= Key.Key0 && key.Keycode <= Key.Key9 && source < 0)
            {
                int quickIndex = ((int)key.Keycode - (int)Key.Key0 + 9) % 10;
                int bagCell = BagAt(pointer), chestCell = ChestAt(pointer), craftCell = CraftAt(pointer);
                if (craftCell >= 0)                                                                     // клавиша 1–0 над ячейкой крафта, как над ячейкой сумки: стопка уходит в этот слот панели
                {
                    var cs = G.CraftIngredient(craftCell);
                    if (cs.n > 0) G.TakeCraftIngredient(craftCell, 0, quickIndex, cs.n);
                    repaintSlots = true;
                }
                else if (bagCell >= 0) G.SwapBagQuick(bagCell, quickIndex);
                else if (chestCell >= 0)
                {
                    int count = G.ChestStacks()[chestCell].n;
                    G.MoveChestToQuick(chestCell, quickIndex, count, true);
                }
                else return;
            }
            else if (!ChestOpen && (key.Keycode is Key.Enter or Key.KpEnter) && source < 0)
            {
                int hovered = BagAt(pointer);
                if (hovered < 0 || hovered >= shown.Count || !G.UseInventoryItem(shown[hovered].item, hovered)) return;
            }
            else if (!ChestOpen && key.Keycode == Key.Q)
            {
                var current = G.InventorySlots();
                if (sourceQuick && source >= 0) { G.DropQuickStack(source, carriedItem, carriedCount); CancelCarry(); }
                else if (sourceCraft)
                {
                    int amount = carriedCount, item = carriedItem, cell = source;
                    CancelCarry(); G.DropCraftCell(cell, item, amount);
                }
                else if (source >= 0) { G.DropInventoryStack(source, carriedItem, carriedCount); CancelCarry(); }
                else if (CraftAt(pointer) is int hc && hc >= 0 && G.CraftIngredient(hc).n > 0)
                {
                    var (ci, cn) = G.CraftIngredient(hc); G.DropCraftCell(hc, ci, key.CtrlPressed ? 1 : cn);
                    repaintSlots = true;
                }
                else if (QuickAt(pointer) is int hq && hq >= 0)
                {
                    var (qi, qn) = G.QuickStack(hq);
                    if (qn > 0) G.DropQuickStack(hq, qi, key.CtrlPressed ? 1 : qn);
                }
                else if (BagAt(pointer) is int hovered && hovered >= 0 && hovered < current.Count && current[hovered].n > 0)
                {
                    var stack = current[hovered];
                    G.DropInventoryStack(hovered, stack.item, key.CtrlPressed ? 1 : stack.n);
                }
            }
            else return;
            GetViewport().SetInputAsHandled();
            return;
        }
        if (source >= 0 && e is InputEventMouseMotion motion)
        {
            if (!sourceCraft && leftHeld && motion.ShiftPressed)
            {
                Vector2 start = pressPosition * G.UiScaleVp;
                CancelCarry();
                shiftTransfer = true;
                shiftVisited.Clear();
                ShiftTransferAt(start, motion.CtrlPressed);
                ShiftTransferPath(start, motion.Position, motion.CtrlPressed);
                shiftPosition = motion.Position;
                GetViewport().SetInputAsHandled();
                return;
            }
            var pos = motion.Position / G.UiScaleVp;
            Vector2 previous = carryPointer;
            if (preview.Visible)
            {
                carryPointer = motion.Position;
                PositionCarry(carryPointer);
            }
            else if (!leftHeld || pos.DistanceTo(pressPosition) > 5)
            {
                ShowCarry(motion.Position);
                if (leftHeld)
                {
                    distributionButton = MouseButton.Left;
                    distributionStart = pressPosition * G.UiScaleVp;
                    distributing = true;
                    sourceExitPending = true;
                    previous = distributionStart;
                }
            }
            if (distributionButton != MouseButton.None)
            {
                if (distributionButton == MouseButton.Left &&
                    motion.Position.DistanceTo(distributionStart) > 5 * G.UiScaleVp)
                    distributionMoved = true;
                if (!distributing && motion.Position.DistanceTo(distributionStart) > 5 * G.UiScaleVp)
                {
                    distributing = true;
                    VisitDistributionCell(distributionStart);
                }
                if (distributing && source >= 0) VisitDistributionPath(previous, motion.Position);
            }
            return;
        }
        if (e is not InputEventMouseButton button || (button.ButtonIndex != MouseButton.Left && button.ButtonIndex != MouseButton.Right)) return;

        var position = button.Position / G.UiScaleVp;
        int target = BagAt(pointer), chest = ChestAt(pointer);
        int quick = QuickAt(pointer);
        if (button.Pressed && button.ButtonIndex == MouseButton.Right && source >= 0 && preview.Visible && !sourceChest && !ChestOpen &&
            target < 0 && chest < 0 && quick < 0 && CraftAt(pointer) < 0 && !IsOver(position))
        {
            bool dropped = sourceCraft ? G.DropCraftCell(source, carriedItem, 1) :                  // правая кнопка за пределами окна: выбросить одну штуку из руки
                sourceQuick ? G.DropQuickStack(source, carriedItem, 1) : G.DropInventoryStack(source, carriedItem, 1);
            if (dropped) { carriedCount--; if (carriedCount <= 0) CancelCarry(); else { repaintSlots = true; ShowCarry(button.Position); } }
            GetViewport().SetInputAsHandled();
            return;
        }
        if (!ChestOpen && button.Pressed && button.ButtonIndex == MouseButton.Left && Hit(craftResult, pointer))
        {
            if (source < 0 && G.CraftIngredient(2).n == 0) G.CombineFromCells(button.ShiftPressed);
            else if (source == 2 && sourceCraft && preview.Visible)                                    // в руке результат: следующий крафт добавляется к нему
            {
                int before = G.CraftIngredient(2).n;
                if (G.CombineFromCells(button.ShiftPressed)) { carriedCount += G.CraftIngredient(2).n - before; ShowCarry(button.Position); }
                GetViewport().SetInputAsHandled(); return;
            }
        }
                                                 // клик по готовому результату: он появляется в ячейке и берётся в руку
        int craftTarget = CraftAt(pointer);
        if (!button.Pressed)
        {
            if (button.ButtonIndex == distributionButton && distributionButton != MouseButton.None)
            {
                FinishDistribution(button.Position);
                GetViewport().SetInputAsHandled();
                return;
            }
            if (!leftHeld || button.ButtonIndex != MouseButton.Left) return;
            if (position.DistanceTo(pressPosition) > 5 || preview.Visible) PlaceCarry(target, chest, quick, position);
            else { leftHeld = false; ShowCarry(button.Position); }
        }
        else if (source >= 0)
        {
            if (button.ShiftPressed && button.ButtonIndex == MouseButton.Left && preview.Visible && !sourceCraft)
            {
                shiftCollect = true;
                shiftPosition = button.Position;
                shiftVisited.Clear();
                CollectAt(button.Position);
            }
            else if (button.CtrlPressed && button.ButtonIndex == MouseButton.Left &&
                !sourceQuick && !sourceChest && !sourceCraft && target == source && SourceStack() is var stack && stack.item == carriedItem)
            {
                carriedCount = Mathf.Min(carriedCount + 1, stack.n);
                ShowCarry(button.Position);
            }
            else if (preview.Visible && button.ButtonIndex is MouseButton.Left or MouseButton.Right)
            {
                distributionButton = button.ButtonIndex;
                distributionStart = button.Position;
                distributing = button.ButtonIndex == MouseButton.Right;
                sourceExitPending = false;
                distributionMoved = false;
                distributed.Clear();
                distributionOrder.Clear();
                if (distributing) VisitDistributionCell(button.Position);
            }
            else return;
        }
        else
        {
            if (target < 0 && chest < 0 && quick < 0 && craftTarget < 0) return;
            IReadOnlyList<(int item, int n)> cells = chest >= 0 ? G.ChestStacks() : G.InventorySlots();
            int index = craftTarget >= 0 ? craftTarget : chest >= 0 ? chest : target;
            var (item, count) = craftTarget >= 0 ? G.CraftIngredient(index) : quick >= 0 ? G.QuickStack(quick) : index >= 0 && index < cells.Count ? cells[index] : (-1, 0);
            if (button.ButtonIndex == MouseButton.Left && button.ShiftPressed && craftTarget >= 0)
            {
                if (count > 0) G.ReceiveCraftCell(craftTarget, count);                       // Shift: вся стопка из ячейки крафта уходит в инвентарь
                repaintSlots = true;
            }
            else if (button.ButtonIndex == MouseButton.Left && button.ShiftPressed)
            {
                shiftTransfer = true;
                shiftPosition = button.Position;
                shiftVisited.Clear();
                ShiftTransferAt(button.Position, button.CtrlPressed);
            }
            else if (!ChestOpen && button.ButtonIndex == MouseButton.Left && button.DoubleClick && target >= 0 && G.UseInventoryItem(item, target)) { }
            else if (count > 0 && Game.UsesSlot(item))
            {
                sourceQuick = quick >= 0;
                sourceChest = chest >= 0;
                sourceCraft = craftTarget >= 0;
                source = sourceCraft ? craftTarget : sourceQuick ? quick : sourceChest ? chest : target;
                carryWindow = G.Window;
                carriedItem = item;
                carriedCount = button.CtrlPressed ? 1 : button.ButtonIndex == MouseButton.Right ? (count + 1) / 2 : count;
                leftHeld = button.ButtonIndex == MouseButton.Left;
                pressPosition = position;
                if (!leftHeld)
                {
                    ShowCarry(button.Position);
                    distributionButton = MouseButton.Right;
                    distributionStart = button.Position;
                    initialRightGrab = true;
                    sourceExitPending = true;
                }
            }
        }
        GetViewport().SetInputAsHandled();
    }


    bool tipsBound;
    void BindTips()
    {
        if (tipsBound || ItemTipHost.I == null) return; tipsBound = true;
        ItemTipHost.Bind(craftResult, () => G.CombineResultTip());
        for (int i = 0; i < craftSlots.Length; i++)                                                    // ячейки компонентов: та же подсказка, что у ячеек сумки
        {
            int idx = i;
            ItemTipHost.Bind(craftSlots[i], () => { var c = G.CraftIngredient(idx); return c.n > 0 ? G.InventoryItemTip(c.item, c.n) : null; });
        }
    }

    public override void _Process(double delta)
    {
        if (G == null || G.W == null) return;
        BindTips();
        bool show = G.Window == Win.Inventory;
        Visible = show || ChestOpen;
        if (!Visible || G.Menu != Mn.None) { pointerWindow = Win.None; shiftTransfer = false; shiftCollect = false; shiftVisited.Clear(); }
        panel.Visible = show;
        if (source >= 0)
        {
            if (carryWindow != G.Window || G.Menu != Mn.None) CancelCarry();
            else
            {
                var stack = SourceStack();
                if (stack.item != carriedItem || stack.n < carriedCount) CancelCarry();
            }
        }
        if (preview.Visible) PositionCarry(carryPointer);
        if (!show) { tipPanel.Visible = false; return; }
        var vis = G.InventorySlots(); int total = vis.Count;
        bool listChanged = vis.Count != shown.Count; if (!listChanged) for (int i = 0; i < vis.Count; i++) if (vis[i] != shown[i]) { listChanged = true; break; }
        if (listChanged) { shown.Clear(); shown.AddRange(vis); for (int i = total; i < slots.Count; i++) slots[i].Visible = false; }
        for (int i = 0; i < total; i++)
        {
            if (!listChanged && !distributing && !repaintSlots) continue;
            UpdateSlot(i);
        }
        repaintSlots = false;
        string su = string.Format(Loc.T("Слоты {0}/{1}"), G.SlotsUsed(), G.SlotCap); if (slotsL.Text != su) slotsL.Text = su;
        hotbar.Refresh(G);
        for (int i = 0; i < craftSlots.Length; i++)
        {
            var (item, n) = G.CraftIngredient(i);
            (item, n) = DisplayStack(3, i, item, n);
            craftSlots[i].Set(n > 0 ? ItemTexture(item) : null, n > 0 ? n.ToString() : "", "", SlotState.Normal);
        }
        craftPanel.Visible = show; RefreshCombine(); RefreshQuickCraft();
        UpdateTip(vis);
    }

    /// <summary>Подсказка у курсора — только пока мышь над непустой ячейкой.</summary>
    void UpdateTip(IReadOnlyList<(int item, int n)> vis)
    {
        var mouse = GetViewport().GetMousePosition();
        int h = BagAt(mouse), q = h >= 0 ? -1 : hotbar.QuickAt(mouse);
        (int item, int n) cur = h >= 0 && h < vis.Count ? vis[h] : q >= 0 ? G.QuickStack(q) : (-1, 0);
        if (cur.n <= 0 || preview.Visible) { tipPanel.Visible = false; return; }
        tipPanel.Set(G.InventoryItemTip(cur.item, cur.n)); tipPanel.ShowAt(this, mouse);
    }
}
