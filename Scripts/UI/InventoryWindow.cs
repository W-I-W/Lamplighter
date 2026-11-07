using Godot;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Инвентарь (I): сетка стопок ресурсов с перетаскиванием между слотами.
/// Сцена Scenes/UI/InventoryWindow.tscn; обновляются только изменившиеся ячейки.</summary>
public partial class InventoryWindow : Control
{
    public Game G;
    Control panel, info; HotbarRow hotbar; GridContainer grid; Label nameL, desc, slotsL; readonly List<UiSlot> slots = new();
    readonly List<(int item, int n)> shown = new(); string lastDesc = "";
    public int Columns => grid.Columns;

    public override void _Ready()
    {
        slotsL = GetNode<Label>("%SlotUsage");
        preview = GetNode<UiSlot>("CarryPreview");
        previewIcon = preview.GetNode<TextureRect>("%Icon");
        panel = GetNode<Control>("%Panel"); grid = GetNode<GridContainer>("%Grid"); nameL = GetNode<Label>("%Name"); desc = GetNode<Label>("%Desc");
        info = GetNode<Control>("Panel/V/Info"); info.Reparent(this, false); info.Visible = false; info.MouseFilter = MouseFilterEnum.Ignore;
        desc.CustomMinimumSize = new Vector2(260, 0); desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        var box = grid.GetParent();
        var gap = new Control { CustomMinimumSize = new Vector2(0, 3) }; box.AddChild(gap); box.MoveChild(gap, grid.GetIndex() + 1);
        hotbar = HotbarRow.Create(); box.AddChild(hotbar); box.MoveChild(hotbar, gap.GetIndex() + 1);
    }

    public bool IsOver(Vector2 p) => panel.IsVisibleInTree() && new Rect2(Vector2.Zero, panel.Size).HasPoint(panel.GetGlobalTransform().AffineInverse() * (p * G.UiScaleVp));

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
    bool leftHeld, sourceQuick, sourceChest;
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

    internal int CarriedCount(bool quick, bool chest, int index) =>
        preview.Visible && source == index && sourceQuick == quick && sourceChest == chest ? carriedCount : 0;

    internal string CarrySignature => preview.Visible ? $"{source}:{sourceQuick}:{sourceChest}:{carriedCount}:{distributionButton}:{distributing}:{distributionOrder.Count}" : "";

    internal (int item, int n) DisplayStack(int area, int index, int item, int count)
    {
        count -= CarriedCount(area == 0, area == 1, index);
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

    (int item, int n) SourceStack()
    {
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
        if (!sourceQuick && !sourceChest && carryWindow == Win.Inventory &&
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
        preview.Set(ItemTexture(carriedItem), remaining > 1 ? remaining.ToString() : "", "", SlotState.Normal);
        preview.Show();
        PositionCarry(viewportPosition);
        if (!sourceQuick && !sourceChest && !ChestOpen) UpdateSlot(source);
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
        else if (!ChestOpen && !sourceQuick && !sourceChest && !IsOver(position))
        {
            if (G.BeginInventoryPlacement(carriedItem, source) ||
                G.DropInventoryStack(source, carriedItem, carriedCount)) CancelCarry();
            return;
        }
        if (!targetCell) return;
        var remaining = SourceStack();
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
        int quick = QuickAt(position), chest = ChestAt(position), bag = BagAt(position);
        int area = quick >= 0 ? 0 : chest >= 0 ? 1 : bag >= 0 ? 2 : -1;
        int target = area == 0 ? quick : area == 1 ? chest : bag;
        return (area, target);
    }

    (int item, int n) CellStack(int area, int target) => area == 0 ? G.QuickStack(target) :
        area == 1 ? G.ChestStacks()[target] : G.InventorySlots()[target];

    bool IsSourceCell(int area, int target) => target == source &&
        (area == 0 && sourceQuick || area == 1 && sourceChest || area == 2 && !sourceQuick && !sourceChest);

    int VisibleCellCount(int area, int target)
    {
        int count = CellStack(area, target).n;
        return IsSourceCell(area, target) ? Mathf.Max(0, count - carriedCount) : count;
    }

    bool CanDistribute(int area, int target)
    {
        if (area < 0) return false;
        if (IsSourceCell(area, target)) return carriedCount > 0;
        if (sourceChest && area != 1 && !G.CanTakeChestItem(carriedItem)) return false;
        var destination = CellStack(area, target);
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
        amount = Mathf.Min(amount, Mathf.Min(carriedCount, Game.StackMax - destination.n));
        if (amount <= 0) return 0;
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
        var result = CellStack(area, target);
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

    void FinishDistribution(Vector2 position)
    {
        if (distributing)
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
        if (area < 0 || !shiftVisited.Add((area, index)) ||
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
        if (source >= 0 && leftHeld && e is InputEventKey shiftKey &&
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
                int bagCell = BagAt(pointer), chestCell = ChestAt(pointer);
                if (bagCell >= 0) G.SwapBagQuick(bagCell, quickIndex);
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
                if (sourceQuick && source >= 0) { CancelCarry(); }
                else if (source >= 0) { G.DropInventoryStack(source, carriedItem, carriedCount); CancelCarry(); }
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
            if (leftHeld && motion.ShiftPressed)
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
            if (button.ShiftPressed && button.ButtonIndex == MouseButton.Left && preview.Visible)
            {
                shiftCollect = true;
                shiftPosition = button.Position;
                shiftVisited.Clear();
                CollectAt(button.Position);
            }
            else if (button.CtrlPressed && button.ButtonIndex == MouseButton.Left &&
                !sourceQuick && !sourceChest && target == source && SourceStack() is var stack && stack.item == carriedItem)
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
            if (target < 0 && chest < 0 && quick < 0) return;
            IReadOnlyList<(int item, int n)> cells = chest >= 0 ? G.ChestStacks() : G.InventorySlots();
            int index = chest >= 0 ? chest : target;
            var (item, count) = quick >= 0 ? G.QuickStack(quick) : index < cells.Count ? cells[index] : (-1, 0);
            if (button.ButtonIndex == MouseButton.Left && button.ShiftPressed)
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
                source = sourceQuick ? quick : sourceChest ? chest : target;
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


    public override void _Process(double delta)
    {
        if (G == null || G.W == null) return;
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
        if (!show) { info.Visible = false; return; }
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
        UpdateTip(vis);
    }

    /// <summary>Подсказка у курсора — только пока мышь над непустой ячейкой.</summary>
    void UpdateTip(IReadOnlyList<(int item, int n)> vis)
    {
        var mouse = GetViewport().GetMousePosition();
        int h = BagAt(mouse), q = h >= 0 ? -1 : hotbar.QuickAt(mouse);
        (int item, int n) cur = h >= 0 && h < vis.Count ? vis[h] : q >= 0 ? G.QuickStack(q) : (-1, 0);
        if (cur.n <= 0 || preview.Visible) { info.Visible = false; return; }
        int c = cur.item;
        string d = G.InventoryItemDetails(c);
        if (string.IsNullOrWhiteSpace(d)) { info.Visible = false; return; }
        nameL.Text = $"{Loc.T(G.InventoryItemName(c))} ×{cur.n}";
        if (d != lastDesc) { lastDesc = d; desc.Text = d; }
        info.ResetSize();
        var at = GetGlobalTransformWithCanvas().AffineInverse() * mouse + new Vector2(12, 14);
        at.X = Mathf.Clamp(at.X, 0, Mathf.Max(0, Size.X - info.Size.X)); at.Y = Mathf.Clamp(at.Y, 0, Mathf.Max(0, Size.Y - info.Size.Y));
        info.Position = at; info.Visible = true;
    }
}
