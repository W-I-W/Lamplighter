using Godot;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Инвентарь (I): сетка стопок ресурсов с перетаскиванием между слотами.
/// Сцена Scenes/UI/InventoryWindow.tscn; обновляются только изменившиеся ячейки.</summary>
public partial class InventoryWindow : Control
{
    public Game G;
    Control panel, info; HotbarRow hotbar; GridContainer grid; Label nameL, desc, slotsL; readonly List<UiSlot> slots = new();
    readonly List<(int item, int n)> shown = new(); int shownSel = -1; string lastDesc = "";
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
    bool distributing;
    readonly HashSet<(int area, int index)> distributed = new();
    Win carryWindow, pointerWindow;
    Vector2 pressPosition, carryPointer, pointer;
    UiSlot preview;
    TextureRect previewIcon;

    Texture2D ItemTexture(int item) => G.InventoryItemTexture(item);

    internal int CarriedCount(bool quick, bool chest, int index) =>
        preview.Visible && source == index && sourceQuick == quick && sourceChest == chest ? carriedCount : 0;

    internal string CarrySignature => preview.Visible ? $"{source}:{sourceQuick}:{sourceChest}:{carriedCount}" : "";

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
        distributed.Clear();
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
        preview.Set(ItemTexture(carriedItem), carriedCount > 1 ? carriedCount.ToString() : "", "", SlotState.Normal);
        preview.Show();
        PositionCarry(viewportPosition);
        if (!sourceQuick && !sourceChest && !ChestOpen) UpdateSlot(source);
    }

    void UpdateSlot(int index)
    {
        var (item, count) = shown[index];
        count = Mathf.Max(0, count - CarriedCount(false, false, index));
        var slot = Slot(index);
        slot.Visible = true;
        slot.Set(count > 0 ? ItemTexture(item) : null, count > 0 ? count.ToString() : "", "",
            index == G.WinCur ? SlotState.Selected : SlotState.Normal);
    }

    void PlaceCarry(int target, int chest, int quick, Vector2 position)
    {
        var stack = SourceStack();
        if (stack.item != carriedItem || stack.n < carriedCount)
        {
            CancelCarry();
            return;
        }
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
            G.WinTab = 0;
            G.WinCur = target;
        }
        else if (!ChestOpen && !sourceQuick && !sourceChest && !IsOver(position) &&
            !G.BeginInventoryPlacement(carriedItem, source)) G.DropInventoryStack(source, carriedItem, carriedCount);
        CancelCarry();
    }

    void DistributeOne(Vector2 position)
    {
        int quick = QuickAt(position), chest = ChestAt(position), bag = BagAt(position);
        int area = quick >= 0 ? 0 : chest >= 0 ? 1 : bag >= 0 ? 2 : -1;
        int target = area == 0 ? quick : area == 1 ? chest : bag;
        if (area < 0 || !distributed.Add((area, target))) return;
        if ((area == 0 && sourceQuick || area == 1 && sourceChest ||
             area == 2 && !sourceQuick && !sourceChest) && target == source) return;

        var destination = area == 0 ? G.QuickStack(target) :
            area == 1 ? G.ChestStacks()[target] : G.InventorySlots()[target];
        if (destination.n >= Game.StackMax || destination.n > 0 && destination.item != carriedItem) return;

        var origin = SourceStack();
        if (origin.item != carriedItem || origin.n < carriedCount) { CancelCarry(); return; }
        if (area == 0)
        {
            if (sourceQuick) G.MoveQuickStack(source, target, 1);
            else if (sourceChest) G.MoveChestToQuick(source, target, 1, false);
            else G.MoveToQuickSlot(source, target, carriedItem, 1);
        }
        else if (area == 1)
        {
            if (sourceChest) G.MoveChestStack(source, target, 1);
            else G.DepositChestSlot(source, sourceQuick, carriedItem, 1, target);
        }
        else
        {
            if (sourceQuick) G.MoveQuickToBag(source, target, 1);
            else if (sourceChest) G.WithdrawChestToBag(source, target, carriedItem, 1);
            else G.MoveInventoryStack(source, target, carriedItem, 1);
        }

        var result = area == 0 ? G.QuickStack(target) :
            area == 1 ? G.ChestStacks()[target] : G.InventorySlots()[target];
        if (result.item != carriedItem || result.n != destination.n + 1) return;
        carriedCount--;
        if (carriedCount == 0) CancelCarry();
        else ShowCarry(position);
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
        if (source >= 0 && carryWindow != G.Window) CancelCarry();
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
            else if (!ChestOpen && (key.Keycode is Key.Enter or Key.KpEnter) && source < 0 && G.WinCur < shown.Count && G.UseInventoryItem(shown[G.WinCur].item, G.WinCur)) { }
            else if (!ChestOpen && key.Keycode == Key.Q)
            {
                var current = G.InventorySlots();
                if (sourceQuick && source >= 0) { CancelCarry(); }
                else if (source >= 0) { G.DropInventoryStack(source, carriedItem, carriedCount); CancelCarry(); }
                else if (G.WinCur < current.Count && current[G.WinCur].n > 0)
                {
                    var stack = current[G.WinCur];
                    G.DropInventoryStack(G.WinCur, stack.item, key.CtrlPressed ? 1 : stack.n);
                }
            }
            else return;
            GetViewport().SetInputAsHandled();
            return;
        }
        if (source >= 0 && e is InputEventMouseMotion motion)
        {
            var pos = motion.Position / G.UiScaleVp;
            if (preview.Visible)
            {
                carryPointer = motion.Position;
                PositionCarry(carryPointer);
            }
            else if (!leftHeld || pos.DistanceTo(pressPosition) > 5) ShowCarry(motion.Position);
            if (distributing) DistributeOne(motion.Position);
            return;
        }
        if (e is not InputEventMouseButton button || (button.ButtonIndex != MouseButton.Left && button.ButtonIndex != MouseButton.Right)) return;

        var position = button.Position / G.UiScaleVp;
        int target = BagAt(pointer), chest = ChestAt(pointer);
        int quick = QuickAt(pointer);
        if (!button.Pressed)
        {
            if (button.ButtonIndex == MouseButton.Right && distributing)
            {
                distributing = false;
                distributed.Clear();
                GetViewport().SetInputAsHandled();
                return;
            }
            if (!leftHeld || button.ButtonIndex != MouseButton.Left) return;
            if (position.DistanceTo(pressPosition) > 5 || preview.Visible) PlaceCarry(target, chest, quick, position);
            else { leftHeld = false; ShowCarry(button.Position); }
        }
        else if (source >= 0)
        {
            if (button.ButtonIndex == MouseButton.Right && preview.Visible)
            {
                distributing = true;
                distributed.Clear();
                DistributeOne(button.Position);
            }
            else if (button.CtrlPressed && !sourceQuick && !sourceChest && target == source && SourceStack() is var stack && stack.item == carriedItem)
            {
                carriedCount = Mathf.Min(carriedCount + 1, stack.n);
                ShowCarry(button.Position);
            }
            else if (button.ButtonIndex == MouseButton.Left) PlaceCarry(target, chest, quick, position);
            else return;
        }
        else
        {
            if (target < 0 && chest < 0 && quick < 0) return;
            if (target >= 0 || chest >= 0) { G.WinCur = chest >= 0 ? chest : target; G.WinTab = chest >= 0 ? 1 : 0; }
            IReadOnlyList<(int item, int n)> cells = chest >= 0 ? G.ChestStacks() : G.InventorySlots();
            int index = chest >= 0 ? chest : target;
            var (item, count) = quick >= 0 ? G.QuickStack(quick) : index < cells.Count ? cells[index] : (-1, 0);
            if (button.ButtonIndex == MouseButton.Left && button.ShiftPressed)
            {
                if (ChestOpen)
                {
                    if (chest >= 0 && count > 0) G.ShiftChestOut(chest, button.CtrlPressed ? 1 : count);
                    else if (count > 0) G.DepositChestSlot(quick >= 0 ? quick : target, quick >= 0, item, button.CtrlPressed ? 1 : count);
                }
                else if (quick >= 0) G.ClearQuickSlot(quick);
                else G.ShiftBagToQuick(target);
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
                if (!leftHeld) ShowCarry(button.Position);
            }
        }
        GetViewport().SetInputAsHandled();
    }


    public override void _Process(double delta)
    {
        if (G == null || G.W == null) return;
        bool show = G.Window == Win.Inventory;
        Visible = show || ChestOpen;
        if (!Visible) pointerWindow = Win.None;
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
        var vis = G.InventorySlots(); int total = vis.Count; G.WinCur = Mathf.Clamp(G.WinCur, 0, Mathf.Max(0, vis.Count - 1));
        bool listChanged = vis.Count != shown.Count; if (!listChanged) for (int i = 0; i < vis.Count; i++) if (vis[i] != shown[i]) { listChanged = true; break; }
        if (listChanged) { shown.Clear(); shown.AddRange(vis); shownSel = -1; for (int i = total; i < slots.Count; i++) slots[i].Visible = false; }
        for (int i = 0; i < total; i++)
        {
            bool sel = i == G.WinCur;
            if (!listChanged && !(sel != (i == shownSel))) continue;       // ячейка не изменилась
            UpdateSlot(i);
        }
        shownSel = G.WinCur;
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
