using Godot;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Инвентарь (I): сетка стопок ресурсов с перетаскиванием между слотами.
/// Сцена Scenes/UI/InventoryWindow.tscn; обновляются только изменившиеся ячейки.</summary>
public partial class InventoryWindow : Control
{
    public Game G;
    Control panel; GridContainer grid; Label nameL, desc, slotsL; readonly List<UiSlot> slots = new();
    readonly List<(int item, int n)> shown = new(); int shownSel = -1; string lastDesc = "";

    public override void _Ready()
    {
        slotsL = GetNode<Label>("%SlotUsage");
        preview = GetNode<UiSlot>("CarryPreview");
        previewIcon = preview.GetNode<TextureRect>("%Icon");
        panel = GetNode<Control>("%Panel"); grid = GetNode<GridContainer>("%Grid"); nameL = GetNode<Label>("%Name"); desc = GetNode<Label>("%Desc");
    }

    public bool IsOver(Vector2 p) => Visible && new Rect2(Vector2.Zero, panel.Size).HasPoint(panel.GetGlobalTransform().AffineInverse() * (p * G.UiScaleVp));

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
    bool leftHeld, keepOnClick;
    Vector2 pressPosition, carryPointer;
    UiSlot preview;
    TextureRect previewIcon;

    Texture2D ItemTexture(int item) => G.InventoryItemTexture(item);

    void CancelCarry()
    {
        int previousSource = source;
        source = -1;
        carriedCount = 0;
        leftHeld = false;
        if (preview != null) preview.Hide();
        if (previousSource >= 0 && previousSource < shown.Count) UpdateSlot(previousSource);
    }

    void PositionCarry(Vector2 viewportPosition)
    {
        var iconCenter = previewIcon.GetGlobalTransformWithCanvas() * (previewIcon.Size * .5f);
        preview.Position += GetGlobalTransformWithCanvas().AffineInverse().BasisXform(viewportPosition - iconCenter);
    }

    void ShowCarry(Vector2 viewportPosition)
    {
        carryPointer = viewportPosition;
        preview.Set(ItemTexture(carriedItem), carriedCount.ToString(), "", SlotState.Normal);
        preview.Show();
        PositionCarry(viewportPosition);
        UpdateSlot(source);
    }

    void UpdateSlot(int index)
    {
        var (item, count) = shown[index];
        if (preview.Visible && index == source && item == carriedItem)
            count = Mathf.Max(0, count - carriedCount);
        var slot = Slot(index);
        slot.Visible = true;
        slot.Set(count > 0 ? ItemTexture(item) : null, count > 0 ? count.ToString() : "", "",
            index == G.WinCur ? SlotState.Selected : SlotState.Normal);
    }

    void PlaceCarry(int target, Vector2 position)
    {
        int quick = G.GameHudNode?.QuickSlotAt(position * G.UiScaleVp) ?? -1;
        if (quick >= 0) G.BindQuickSlot(quick, carriedItem);
        else if (target >= 0)
        {
            if (G.MoveInventoryStack(source, target, carriedItem, carriedCount)) G.WinCur = target;
        }
        else if (!IsOver(position) && !G.BeginInventoryPlacement(carriedItem, source)) G.DropInventoryStack(source, carriedItem, carriedCount);
        CancelCarry();
    }

    public override void _Input(InputEvent e)
    {
        if (G == null || !Visible || G.Window != Win.Inventory || G.Menu != Mn.None || G.ChatOpen || G.ResUi?.Active == true) return;
        if (e is InputEventKey key && key.Pressed && !key.Echo)
        {
            if (key.Keycode == Key.Escape && source >= 0) CancelCarry();
            else if (key.Keycode is Key.Enter or Key.KpEnter && source < 0 && G.WinCur < shown.Count && G.UseInventoryItem(shown[G.WinCur].item, G.WinCur)) { }
            else if (key.Keycode == Key.Q)
            {
                var current = G.InventorySlots();
                if (source >= 0) { G.DropInventoryStack(source, carriedItem, carriedCount); CancelCarry(); }
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
            return;
        }
        if (e is not InputEventMouseButton button || (button.ButtonIndex != MouseButton.Left && button.ButtonIndex != MouseButton.Right)) return;

        var position = button.Position / G.UiScaleVp;
        int target = slots.FindIndex(s => s.Visible && new Rect2(Vector2.Zero, s.Size).HasPoint(s.GetGlobalTransform().AffineInverse() * button.Position));
        if (!button.Pressed)
        {
            if (!leftHeld || button.ButtonIndex != MouseButton.Left) return;
            if (position.DistanceTo(pressPosition) > 5 || preview?.Visible == true) PlaceCarry(target, position);
            else if (keepOnClick) { leftHeld = false; ShowCarry(button.Position); }
            else CancelCarry();
        }
        else if (source >= 0)
        {
            if (button.CtrlPressed && target == source && source < shown.Count && shown[source].item == carriedItem)
            {
                carriedCount = Mathf.Min(carriedCount + 1, shown[source].n);
                ShowCarry(button.Position);
            }
            else if (button.ButtonIndex == MouseButton.Left) PlaceCarry(target, position);
            else return;
        }
        else
        {
            if (target < 0) return;
            G.WinCur = target;
            var (item, count) = shown[target];
            if (button.ButtonIndex == MouseButton.Left && button.DoubleClick && G.UseInventoryItem(item, target)) { }
            else if (button.ButtonIndex == MouseButton.Right && button.ShiftPressed)
            {
                if (item >= 0 && count > 0 && G.ResUi?.HasActions(item) == true) G.ResUi.Open(item);
            }
            else if (count > 0 && Game.UsesSlot(item))
            {
                source = target;
                carriedItem = item;
                carriedCount = button.CtrlPressed ? 1 : button.ButtonIndex == MouseButton.Right ? (count + 1) / 2 : count;
                leftHeld = button.ButtonIndex == MouseButton.Left;
                keepOnClick = button.CtrlPressed;
                pressPosition = position;
                if (!leftHeld) ShowCarry(button.Position);
            }
        }
        GetViewport().SetInputAsHandled();
    }


    public override void _Process(double delta)
    {
        if (G == null || G.W == null) return;
        bool show = G.Window == Win.Inventory; Visible = show; if (!show) { CancelCarry(); return; }
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
        if (source >= 0 && (source >= vis.Count || vis[source].item != carriedItem || vis[source].n < carriedCount)) CancelCarry();
        if (preview.Visible) PositionCarry(carryPointer);
        if (vis.Count == 0 || vis[G.WinCur].n <= 0) { nameL.Text = Loc.T("Пустой слот"); desc.Text = ""; lastDesc = ""; return; }
        int c = vis[G.WinCur].item;
        if (c < 0)
        {
            var entry = G.InventoryBuild(c);
            nameL.Text = $"{Loc.T(G.InventoryItemName(c))} ×{vis[G.WinCur].n}";
            desc.Text = Loc.T(entry == null ? "Enter или двойной клик — использовать" : "Enter или двойной клик — установить");
            lastDesc = "";
            return;
        }
        nameL.Text = $"{It.Ic(c)} {Loc.T(It.Names[c])} ×{G.Inv[c]}";
        string d = Game.Tradable(c) ? string.Format(Loc.T("Цена продажи: {0} · цена покупки: {1}"), G.SellPrice(c), G.BuyPrice(c)) : Loc.T("Не продаётся");
        if (G.Stock[c] > 0) d += "\n" + string.Format(Loc.T("На складе: {0}"), G.Stock[c]);
        d += "\n" + Loc.T("ПКМ — половина · Ctrl+клик — по 1\nQ — выбросить · Ctrl+Q — 1 · Shift+ПКМ — действия");
        if (d != lastDesc) { lastDesc = d; desc.Text = d; }
    }
}
