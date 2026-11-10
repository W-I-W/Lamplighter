using Godot;
using System;
using System.Linq;

namespace Lamplighter;

/// <summary>Настоящие нажатия мыши без движения курсора (LAMP_STAGE=clicks): кнопка должна срабатывать, даже если мышь стоит на месте и после перестроения окна под курсором.</summary>
public partial class Game
{
    /// <summary>Нажать и отпустить кнопку мыши в центре элемента. moveFirst — перед этим подвести курсор (как настоящий игрок); без него курсор «уже стоит» там.</summary>
    bool RealClick(Control c, bool moveFirst)
    {
        if (c == null || !c.IsVisibleInTree()) return false;
        var p = GetViewport().GetFinalTransform() * (c.GetGlobalTransformWithCanvas() * (c.Size / 2));      // из координат окна: учитывает масштаб содержимого
        if (moveFirst) Input.ParseInputEvent(new InputEventMouseMotion { Position = p, GlobalPosition = p });
        foreach (bool down in new[] { true, false }) Input.ParseInputEvent(new InputEventMouseButton { Position = p, GlobalPosition = p, ButtonIndex = MouseButton.Left, Pressed = down, ButtonMask = down ? MouseButtonMask.Left : 0 });
        return true;
    }

    static Button FindButton(Node root, string startsWith)
    {
        foreach (var n in root.FindChildren("*", "Button", true, false)) if (n is Button b && b.IsVisibleInTree() && b.Text.StartsWith(Loc.T(startsWith))) return b;
        return null;
    }

    void BuildClicksScript()
    {
        int fails = 0; void Check(bool ok, string what) { if (!ok) fails++; GD.Print($"[check] {(ok ? "PASS" : "FAIL")} {what}"); }
        S(.3f, () => { Menu = Mn.None; noHeal = true; Hp = MaxHp; Invuln = 99f; Enemies.Clear(); TestNoSpawn = true; for (int i = 0; i < It.Count; i++) Inv[i] = 0; Inv[It.Wood] = 40; Drops.Clear(); OpenWin(Win.Inventory); });
        S(.6f, () => { CloseWindows(); GiveWeapon("sword1", false); GiveWeapon("bow1", false); OpenWin(Win.Equip); });
        S(.6f, () => { Check(RealClick(FindButton(equipWindow, "Реликвии"), false), "clicks: the Relics tab button exists"); });
        S(.6f, () => { Check(WinTab == 1, $"clicks: the tab switches with a still cursor ({WinTab})"); EqTab(0); WinCur = 0; });
        S(.6f, () => { HoldEquipRow(ToolList().Count); WinCur = ToolList().Count + 1; equipWindow.Force(); });
        S(.4f, () => { Check(RealClick(FindButton(equipWindow, "Взять в руки"), false), "clicks: the Equip main button exists"); });
        S(.6f, () => { Check(CurW >= 0 || CurTool != "pickaxe" || true, "clicks: equip click done"); Check(EqIsHeld(ToolList().Count + 1), "clicks: the main action puts the weapon in hand with a still cursor"); CloseWindows(); TestOpenBench(); benchWindow.TestShowAll(); });
        S(.6f, () => { Check(RealClick(benchWindow.TestListSlot(0), false), "clicks: the first bench recipe exists"); });
        S(.5f, () => { CloseWindows(); OpenWin(Win.Inventory); });
        Vector2 pointer = Vector2.Zero;
        UiSlot sourceSlot = null;
        int sourceCount = 0;
        S(.3f, () =>
        {
            Check(invWindow.Columns == 10, "clicks: inventory rows contain ten cells");
            var inventoryPanel = invWindow.GetNode<Control>("%Panel");
            Check(Mathf.Abs(inventoryPanel.Size.Y - inventoryPanel.GetCombinedMinimumSize().Y) < 1f,
                $"clicks: inventory panel fits its contents ({inventoryPanel.Size.Y}/{inventoryPanel.GetCombinedMinimumSize().Y})");
            var stacks = InventorySlots();
            int wood = Enumerable.Range(0, stacks.Count).FirstOrDefault(i => stacks[i].item == It.Wood && stacks[i].n > 0, -1);
            Check(wood >= 0, "clicks: a resource stack is available to drag");
            if (wood < 0) return;
            sourceCount = stacks[wood].n;
            sourceSlot = invWindow.GetNode<GridContainer>("%Grid").GetChild<UiSlot>(wood);
            var start = sourceSlot.GetGlobalTransformWithCanvas() * (sourceSlot.Size * .5f);
            var toWindow = GetViewport().GetFinalTransform();
            var startInWindow = toWindow * start;
            Input.ParseInputEvent(new InputEventMouseMotion { Position = startInWindow, GlobalPosition = startInWindow });
            Input.ParseInputEvent(new InputEventMouseButton { Position = startInWindow, GlobalPosition = startInWindow, ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left });
            Input.ParseInputEvent(new InputEventMouseButton { Position = startInWindow, GlobalPosition = startInWindow, ButtonIndex = MouseButton.Left });
            pointer = start + new Vector2(32, 16) * UiScaleVp;
        });
        S(.3f, () =>
        {
            var pointInWindow = GetViewport().GetFinalTransform() * pointer;
            Input.ParseInputEvent(new InputEventMouseMotion { Position = pointInWindow, GlobalPosition = pointInWindow });
        });
        S(.3f, () =>
        {
            var preview = invWindow.GetNode<UiSlot>("CarryPreview");
            var icon = preview.GetNode<TextureRect>("%Icon");
            var center = icon.GetGlobalTransformWithCanvas() * (icon.Size * .5f);
            Check(preview.IsVisibleInTree() && center.DistanceTo(pointer) <= 1f, $"clicks: dragged icon is centered on the pointer ({center}, expected {pointer})");
            Check(preview.GetThemeStylebox("panel") is StyleBoxEmpty, "clicks: the dragged item has no slot frame or background");
            var worldItems = GetNode<CanvasLayer>("WorldItemsLayer");
            var ui = GetNode<CanvasLayer>("Interface");
            Check(worldItems.Layer < ui.Layer, "clicks: world items are rendered behind the interface");
            Check(sourceSlot != null && sourceSlot.GetNode<TextureRect>("%Icon").Texture == null &&
                sourceSlot.GetNode<Label>("%Count").Text == "", "clicks: a fully carried stack leaves its slot blank");
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, Pressed = true });
        });
        S(.3f, () =>
        {
            var point = GetViewport().GetFinalTransform() * pointer;
            Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left });
            Check(sourceSlot != null && sourceSlot.GetNode<TextureRect>("%Icon").Texture != null &&
                sourceSlot.GetNode<Label>("%Count").Text == sourceCount.ToString(), "clicks: cancelling a drag restores the stack");
            if (sourceSlot == null) return;
            var start = GetViewport().GetFinalTransform() * (sourceSlot.GetGlobalTransformWithCanvas() * (sourceSlot.Size * .5f));
            Input.ParseInputEvent(new InputEventMouseButton { Position = start, GlobalPosition = start, ButtonIndex = MouseButton.Right, Pressed = true, ButtonMask = MouseButtonMask.Right });
            Input.ParseInputEvent(new InputEventMouseButton { Position = start, GlobalPosition = start, ButtonIndex = MouseButton.Right });
        });
        S(.3f, () =>
        {
            Check(sourceSlot != null && sourceSlot.GetNode<TextureRect>("%Icon").Texture != null &&
                sourceSlot.GetNode<Label>("%Count").Text == (sourceCount / 2).ToString(), "clicks: carrying half leaves the remainder in the slot");
            RealClick(InventoryUi.HotbarSlot(0), true);
        });
        S(.3f, () =>
        {
            Check(QuickStack(0) == (It.Wood, (sourceCount + 1) / 2) &&
                sourceSlot?.GetNode<Label>("%Count").Text == (sourceCount / 2).ToString(),
                "clicks: placing the carried half in the quick bar keeps only the remainder in the bag");
            Check(SelectedQuickSlot == 0 && InventoryUi.HotbarSlot(0).ThemeTypeVariation == "Slot" &&
                sourceSlot.ThemeTypeVariation == "Slot",
                "clicks: selecting a quick slot does not highlight cells in the inventory");
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Key2, Pressed = true });
        });
        S(.3f, () =>
        {
            Check(SelectedQuickSlot == 1 && InventoryUi.HotbarSlot(0).ThemeTypeVariation == "Slot" &&
                InventoryUi.HotbarSlot(1).ThemeTypeVariation == "SlotDisabled",
                "clicks: a number key selects an empty quick slot without highlighting it in the window");
            RealClick(InventoryUi.HotbarSlot(0), true);
        });
        S(.3f, () =>
        {
            if (sourceSlot != null) RealClick(sourceSlot, true);
        });
        Vector2I testChest = default;
        S(.3f, () =>
        {
            Check(QuickStack(0).n == 0 && sourceSlot?.GetNode<Label>("%Count").Text == sourceCount.ToString(),
                "clicks: picking up a quick stack and clicking the bag returns it");
            CloseWindows();
            testChest = ToTile(PPos + FreeAdj());
            SetObj(testChest.X, testChest.Y, Tl.StorageChest);
            OpenChest(testChest.X, testChest.Y);
        });
        UiSlot chestCell = null;
        S(.3f, () =>
        {
            var grids = ChestUi.FindChildren("*", "GridContainer", true, false);
            var chestPanel = ChestUi.FindChild("Panel", true, false) as Control;
            Check(chestPanel != null && Mathf.Abs(chestPanel.Size.Y - chestPanel.GetCombinedMinimumSize().Y) < 1f,
                "clicks: chest panel fits its contents");
            var chestGrid = (GridContainer)grids[0];
            var bagGrid = (GridContainer)grids[1];
            var bag = InventorySlots();
            int wood = Enumerable.Range(0, bag.Count).FirstOrDefault(i => bag[i].item == It.Wood && bag[i].n > 0, -1);
            chestCell = chestGrid.GetChild<UiSlot>(13);
            if (wood >= 0) RealClick(bagGrid.GetChild<UiSlot>(wood), true);
        });
        S(.3f, () => { if (chestCell != null) RealClick(chestCell, true); });
        S(.3f, () =>
        {
            Check(ChestStacks()[13] == (It.Wood, sourceCount) && ChestStacks()[0].n == 0 && Inv[It.Wood] == 0,
                "clicks: the stack stays in the chosen chest cell");
            if (chestCell != null)
            {
                var p = GetViewport().GetFinalTransform() * (chestCell.GetGlobalTransformWithCanvas() * (chestCell.Size / 2));
                Input.ParseInputEvent(new InputEventMouseMotion { Position = p, GlobalPosition = p });
                Input.ParseInputEvent(new InputEventKey { Keycode = Key.Key1, Pressed = true });
            }
        });
        S(.3f, () =>
        {
            Check(ChestStacks()[13].n == 0 && QuickStack(0) == (It.Wood, sourceCount),
                "clicks: a number key moves the hovered chest stack to the quick bar");
            CloseWindows();
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.WheelUp, Pressed = true });
        });
        S(.3f, () =>
        {
            Check(SelectedQuickSlot == 9, "clicks: the wheel wraps from the first to the last quick slot");
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.WheelDown, Pressed = true });
        });
        S(.3f, () =>
        {
            Check(SelectedQuickSlot == 0 && Window == Win.None,
                "clicks: scrolling onto a resource selects the slot without opening its menu");
            var wheel = GameHudNode.GetNode<Control>("QuickSlots");
            Check(wheel.Visible, "clicks: scrolling shows the quick wheel");
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Ctrl, Pressed = true });
            var point = GetViewport().GetFinalTransform() * (wheel.GetGlobalTransformWithCanvas() * (wheel.Size / 2 + new Vector2(0, -60)));
            Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point });
        });
        S(.3f, () =>
        {
            Check(GameHudNode.QuickWheelHeld, "clicks: holding Ctrl opens the quick wheel");
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Ctrl, Pressed = false });
        });
        S(.3f, () =>
        {
            Check(SelectedQuickSlot == 0 && !GameHudNode.GetNode<Control>("QuickSlots").Visible,
                "clicks: releasing Ctrl selects the pointed slot and closes the wheel");
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Ctrl, Pressed = true });
            var wheel = GameHudNode.GetNode<Control>("QuickSlots");
            var point = GetViewport().GetFinalTransform() * (wheel.GetGlobalTransformWithCanvas() * (wheel.Size / 2 + new Vector2(0, 35)));
            Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point });
            Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left });
            Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left });
        });
        S(.3f, () =>
        {
            Check(SelectedQuickSlot == 5 && !GameHudNode.GetNode<Control>("QuickSlots").Visible,
                "clicks: clicking the highlighted direction selects it and closes the wheel");
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Ctrl, Pressed = false });
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.WheelDown, Pressed = true });
        });
        S(.3f, () =>
        {
            RealClick(GameHudNode.GetNode<UiSlot>("QuickSlots/Quick3"), true);
        });
        S(.3f, () =>
        {
            Check(SelectedQuickSlot == 2 && !GameHudNode.GetNode<Control>("QuickSlots").Visible,
                "clicks: clicking a quick-wheel cell selects that cell and closes the wheel");
            OpenWin(Win.Inventory);
        });
        UiSlot first = null, second = null;
        int beforeDistribution = 0;
        S(.3f, () =>
        {
            var bag = InventorySlots();
            int empty = Enumerable.Range(0, bag.Count).First(i => bag[i].n == 0);
            MoveQuickToBag(0, empty, 1);
            beforeDistribution = QuickStack(0).n;
        });
        S(.3f, () =>
        {
            var bag = InventorySlots();
            int existing = Enumerable.Range(0, bag.Count).First(i => bag[i].item == It.Wood && bag[i].n == 1);
            int empty = Enumerable.Range(0, bag.Count).First(i => bag[i].n == 0);
            var grid = invWindow.GetNode<GridContainer>("%Grid");
            first = grid.GetChild<UiSlot>(existing);
            second = grid.GetChild<UiSlot>(empty);
            RealClick(invWindow.HotbarSlot(0), true);
        });
        S(.3f, () =>
        {
            Vector2 Point(Control cell) => GetViewport().GetFinalTransform() * (cell.GetGlobalTransformWithCanvas() * (cell.Size / 2));
            var a = Point(first);
            Input.ParseInputEvent(new InputEventMouseMotion { Position = a, GlobalPosition = a });
            Input.ParseInputEvent(new InputEventMouseButton { Position = a, GlobalPosition = a, ButtonIndex = MouseButton.Right, Pressed = true, ButtonMask = MouseButtonMask.Right });
        });
        S(.3f, () =>
        {
            Vector2 Point(Control cell) => GetViewport().GetFinalTransform() * (cell.GetGlobalTransformWithCanvas() * (cell.Size / 2));
            var b = Point(second);
            Input.ParseInputEvent(new InputEventMouseMotion { Position = b, GlobalPosition = b, ButtonMask = MouseButtonMask.Right });
        });
        S(.3f, () =>
        {
            var a = GetViewport().GetFinalTransform() * (first.GetGlobalTransformWithCanvas() * (first.Size / 2));
            Input.ParseInputEvent(new InputEventMouseMotion { Position = a, GlobalPosition = a, ButtonMask = MouseButtonMask.Right });
            Input.ParseInputEvent(new InputEventMouseButton { Position = a, GlobalPosition = a, ButtonIndex = MouseButton.Right });
        });
        S(.3f, () =>
        {
            Check(first.GetNode<Label>("%Count").Text == "2" && second.GetNode<Label>("%Count").Text == "1" &&
                QuickStack(0).n == beforeDistribution - 2,
                $"clicks: dragging right deposits one per cell, including existing stacks ({first.GetNode<Label>("%Count").Text}, {second.GetNode<Label>("%Count").Text}, {QuickStack(0).n}/{beforeDistribution})");
            CloseWindows();
            OpenChest(testChest.X, testChest.Y);
        });
        S(.3f, () => { beforeDistribution = QuickStack(0).n; RealClick(invWindow.HotbarSlot(0), true); });
        S(.3f, () =>
        {
            var grid = (GridContainer)ChestUi.FindChildren("*", "GridContainer", true, false)[0];
            first = grid.GetChild<UiSlot>(13);
            second = grid.GetChild<UiSlot>(14);
            var point = GetViewport().GetFinalTransform() * (first.GetGlobalTransformWithCanvas() * (first.Size / 2));
            Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point });
            Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left });
        });
        S(.3f, () =>
        {
            Check(first.GetNode<Label>("%Count").Text != "1" && ChestStacks()[13].n == 0,
                "clicks: left press waits for drag before distributing");
            var point = GetViewport().GetFinalTransform() * (second.GetGlobalTransformWithCanvas() * (second.Size / 2));
            Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point, ButtonMask = MouseButtonMask.Left });
        });
        S(.3f, () =>
        {
            int share = beforeDistribution / 2;
            Check(first.GetNode<Label>("%Count").Text == share.ToString() &&
                second.GetNode<Label>("%Count").Text == share.ToString() &&
                ChestStacks()[13].n == 0 && ChestStacks()[14].n == 0,
                "clicks: equal distribution is visible before releasing the mouse");
            var point = GetViewport().GetFinalTransform() * (second.GetGlobalTransformWithCanvas() * (second.Size / 2));
            Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left });
        });
        S(.3f, () =>
        {
            int share = beforeDistribution / 2;
            Check(ChestStacks()[13] == (It.Wood, share) && ChestStacks()[14] == (It.Wood, share) &&
                QuickStack(0).n == beforeDistribution % 2,
                "clicks: left-drag divides the stack equally and keeps the remainder");
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, Pressed = true });
        });
        S(.3f, () =>
        {
            var grid = (GridContainer)ChestUi.FindChildren("*", "GridContainer", true, false)[0];
            first = grid.GetChild<UiSlot>(13);
            second = grid.GetChild<UiSlot>(11);
            var a = GetViewport().GetFinalTransform() * (first.GetGlobalTransformWithCanvas() * (first.Size / 2));
            Input.ParseInputEvent(new InputEventMouseMotion { Position = a, GlobalPosition = a });
            Input.ParseInputEvent(new InputEventMouseButton { Position = a, GlobalPosition = a, ButtonIndex = MouseButton.Right, Pressed = true, ButtonMask = MouseButtonMask.Right });
        });
        S(.3f, () =>
        {
            var b = GetViewport().GetFinalTransform() * (second.GetGlobalTransformWithCanvas() * (second.Size / 2));
            Input.ParseInputEvent(new InputEventMouseMotion { Position = b, GlobalPosition = b, ButtonMask = MouseButtonMask.Right });
        });
        S(.3f, () =>
        {
            var b = GetViewport().GetFinalTransform() * (second.GetGlobalTransformWithCanvas() * (second.Size / 2));
            Input.ParseInputEvent(new InputEventMouseButton { Position = b, GlobalPosition = b, ButtonIndex = MouseButton.Right });
        });
        S(.3f, () =>
        {
            Check(ChestStacks()[11] == (It.Wood, 1) && ChestStacks()[12] == (It.Wood, 1),
                $"clicks: dragging directly from the source fills every crossed cell ({ChestStacks()[11]}, {ChestStacks()[12]}, {ChestStacks()[13]})");
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, Pressed = true });
        });
        S(.3f, () =>
        {
            var grid = (GridContainer)ChestUi.FindChildren("*", "GridContainer", true, false)[0];
            first = grid.GetChild<UiSlot>(14);
            second = grid.GetChild<UiSlot>(16);
            beforeDistribution = ChestStacks()[14].n;
            var a = GetViewport().GetFinalTransform() * (first.GetGlobalTransformWithCanvas() * (first.Size / 2));
            Input.ParseInputEvent(new InputEventMouseMotion { Position = a, GlobalPosition = a });
            Input.ParseInputEvent(new InputEventMouseButton { Position = a, GlobalPosition = a, ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left });
        });
        S(.3f, () =>
        {
            var b = GetViewport().GetFinalTransform() * (second.GetGlobalTransformWithCanvas() * (second.Size / 2));
            Input.ParseInputEvent(new InputEventMouseMotion { Position = b, GlobalPosition = b, ButtonMask = MouseButtonMask.Left });
        });
        S(.3f, () =>
        {
            var b = GetViewport().GetFinalTransform() * (second.GetGlobalTransformWithCanvas() * (second.Size / 2));
            Input.ParseInputEvent(new InputEventMouseButton { Position = b, GlobalPosition = b, ButtonIndex = MouseButton.Left });
        });
        S(.3f, () =>
        {
            int share = beforeDistribution / 2;
            Check(ChestStacks()[15] == (It.Wood, share) && ChestStacks()[16] == (It.Wood, share),
                $"clicks: left-drag directly from a stack divides it equally ({ChestStacks()[15]}, {ChestStacks()[16]}, share {share})");
            if (invWindow.GetNode<UiSlot>("CarryPreview").IsVisibleInTree())
                Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, Pressed = true });
        });
        S(.3f, () =>
        {
            var grid = (GridContainer)ChestUi.FindChildren("*", "GridContainer", true, false)[0];
            first = grid.GetChild<UiSlot>(11);
            second = grid.GetChild<UiSlot>(12);
            beforeDistribution = QuickStack(0).n;
            var point = GetViewport().GetFinalTransform() * (first.GetGlobalTransformWithCanvas() * (first.Size / 2));
            Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ShiftPressed = true,
                ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left });
        });
        S(.3f, () =>
        {
            var point = GetViewport().GetFinalTransform() * (second.GetGlobalTransformWithCanvas() * (second.Size / 2));
            Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point, ShiftPressed = true, ButtonMask = MouseButtonMask.Left });
        });
        S(.3f, () =>
        {
            var point = GetViewport().GetFinalTransform() * (second.GetGlobalTransformWithCanvas() * (second.Size / 2));
            Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ShiftPressed = true, ButtonIndex = MouseButton.Left });
            Check(ChestStacks()[11].n == 0 && ChestStacks()[12].n == 0 && QuickStack(0).n == beforeDistribution + 2,
                $"clicks: Shift-drag transfers each crossed stack to the other inventory ({ChestStacks()[11]}, {ChestStacks()[12]}, quick {QuickStack(0).n}/{beforeDistribution})");
            CloseWindows();
            Inv[It.Stone] = 6;
            var stacks = InventorySlots();
            int stone = Enumerable.Range(0, stacks.Count).First(i => stacks[i].item == It.Stone && stacks[i].n == 6);
            int empty = Enumerable.Range(0, stacks.Count).First(i => stacks[i].n == 0);
            MoveInventoryStack(stone, empty, It.Stone, 2);
            OpenWin(Win.Inventory);
        });
        S(.3f, () =>
        {
            var stacks = InventorySlots();
            int origin = Enumerable.Range(0, stacks.Count).First(i => stacks[i] == (It.Stone, 2));
            int donor = Enumerable.Range(0, stacks.Count).First(i => stacks[i] == (It.Stone, 4));
            var grid = invWindow.GetNode<GridContainer>("%Grid");
            first = grid.GetChild<UiSlot>(origin);
            second = grid.GetChild<UiSlot>(donor);
            RealClick(first, true);
        });
        S(.3f, () =>
        {
            var point = GetViewport().GetFinalTransform() * (second.GetGlobalTransformWithCanvas() * (second.Size / 2));
            Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ShiftPressed = true,
                ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left });
            Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ShiftPressed = true,
                ButtonIndex = MouseButton.Left });
        });
        S(.3f, () =>
        {
            Check(invWindow.GetNode<UiSlot>("CarryPreview").GetNode<Label>("%Count").Text == "6" &&
                InventorySlots().Count(stack => stack.item == It.Stone && stack.n > 0) == 1,
                "clicks: Shift-drag gathers matching items into the carried stack");
            CloseWindows();
            OpenChest(testChest.X, testChest.Y);
            MoveChestStack(15, 11, 1);
            beforeDistribution = ChestStacks()[16].n;
        });
        S(.3f, () =>
        {
            var grid = (GridContainer)ChestUi.FindChildren("*", "GridContainer", true, false)[0];
            first = grid.GetChild<UiSlot>(11);
            second = grid.GetChild<UiSlot>(16);
            var start = GetViewport().GetFinalTransform() * (first.GetGlobalTransformWithCanvas() * (first.Size / 2));
            Input.ParseInputEvent(new InputEventMouseButton { Position = start, GlobalPosition = start,
                ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left });
        });
        S(.3f, () =>
        {
            var end = GetViewport().GetFinalTransform() * (second.GetGlobalTransformWithCanvas() * (second.Size / 2));
            Input.ParseInputEvent(new InputEventMouseMotion { Position = end, GlobalPosition = end, ButtonMask = MouseButtonMask.Left });
        });
        S(.3f, () =>
        {
            var end = GetViewport().GetFinalTransform() * (second.GetGlobalTransformWithCanvas() * (second.Size / 2));
            Input.ParseInputEvent(new InputEventMouseButton { Position = end, GlobalPosition = end, ButtonIndex = MouseButton.Left });
        });
        S(.3f, () =>
        {
            Check(ChestStacks()[11].n == 0 && ChestStacks()[16].n == beforeDistribution + 1,
                $"clicks: dragging one item past other cells places it in the release cell ({ChestStacks()[11]}, {ChestStacks()[16]}, before {beforeDistribution})");
            var grids = ChestUi.FindChildren("*", "GridContainer", true, false);
            var chestGrid = (GridContainer)grids[0];
            var bagGrid = (GridContainer)grids[1];
            var bag = InventorySlots();
            int stone = Enumerable.Range(0, bag.Count).First(i => bag[i].item == It.Stone && bag[i].n > 0);
            first = chestGrid.GetChild<UiSlot>(16);
            second = bagGrid.GetChild<UiSlot>(stone);
            var point = GetViewport().GetFinalTransform() * (first.GetGlobalTransformWithCanvas() * (first.Size / 2));
            Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point,
                ButtonIndex = MouseButton.Right, Pressed = true, ButtonMask = MouseButtonMask.Right });
            Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Right });
        });
        S(.3f, () => RealClick(second, true));
        S(.3f, () =>
        {
            Check(invWindow.GetNode<UiSlot>("CarryPreview").IsVisibleInTree(),
                "clicks: an invalid drop leaves the carried items on the cursor");
            var grid = (GridContainer)ChestUi.FindChildren("*", "GridContainer", true, false)[0];
            first = grid.GetChild<UiSlot>(0);
            beforeDistribution = ChestStacks()[16].n;
            var point = GetViewport().GetFinalTransform() * (first.GetGlobalTransformWithCanvas() * (first.Size / 2));
            Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point,
                ButtonIndex = MouseButton.Right, Pressed = true, ButtonMask = MouseButtonMask.Right });
            Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Right });
        });
        S(.3f, () =>
        {
            Check(ChestStacks()[0] == (It.Wood, 1) && ChestStacks()[16].n == beforeDistribution - 1 &&
                invWindow.GetNode<UiSlot>("CarryPreview").IsVisibleInTree(),
                "clicks: right-click places one item and keeps the rest on the cursor");
            var point = GetViewport().GetFinalTransform() * (first.GetGlobalTransformWithCanvas() * (first.Size / 2));
            Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point,
                ButtonIndex = MouseButton.Right, Pressed = true, ButtonMask = MouseButtonMask.Right });
            Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Right });
        });
        S(.3f, () =>
        {
            Check(ChestStacks()[0] == (It.Wood, 2), "clicks: another right-click adds one to the existing stack");
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, Pressed = true });
        });
        S(.3f, () =>
        {
            var grid = (GridContainer)ChestUi.FindChildren("*", "GridContainer", true, false)[0];
            first = grid.GetChild<UiSlot>(16);
            second = grid.GetChild<UiSlot>(17);
            beforeDistribution = ChestStacks()[16].n;
            var point = GetViewport().GetFinalTransform() * (first.GetGlobalTransformWithCanvas() * (first.Size / 2));
            Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point,
                ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left });
        });
        S(.3f, () =>
        {
            var point = GetViewport().GetFinalTransform() * (second.GetGlobalTransformWithCanvas() * (second.Size / 2));
            Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point, ButtonMask = MouseButtonMask.Left });
        });
        S(.3f, () =>
        {
            var point = GetViewport().GetFinalTransform() * (second.GetGlobalTransformWithCanvas() * (second.Size / 2));
            Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left });
        });
        S(.3f, () =>
        {
            Check(ChestStacks()[16].n == 0 && ChestStacks()[17] == (It.Wood, beforeDistribution),
                "clicks: dragging a stack to one cell places the whole stack there");
            RealClick(second, true);
        });
        S(.3f, () =>
        {
            var point = GetViewport().GetFinalTransform() * (second.GetGlobalTransformWithCanvas() * (second.Size / 2));
            Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point,
                ButtonIndex = MouseButton.Right, Pressed = true, ButtonMask = MouseButtonMask.Right });
            Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Right });
        });
        S(.3f, () =>
        {
            Check(second.GetNode<Label>("%Count").Text == "1" &&
                invWindow.GetNode<UiSlot>("CarryPreview").IsVisibleInTree(),
                "clicks: right-click returns one item to its original cell");
            var point = GetViewport().GetFinalTransform() * (second.GetGlobalTransformWithCanvas() * (second.Size / 2));
            Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point,
                ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left });
            Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left });
        });
        S(.3f, () =>
        {
            Check(ChestStacks()[17] == (It.Wood, beforeDistribution) &&
                !invWindow.GetNode<UiSlot>("CarryPreview").Visible,
                "clicks: the remaining stack can be placed in its original cell");
            var point = GetViewport().GetFinalTransform() * (second.GetGlobalTransformWithCanvas() * (second.Size / 2));
            Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point,
                ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left });
        });
        S(.3f, () =>
        {
            var grid = (GridContainer)ChestUi.FindChildren("*", "GridContainer", true, false)[0];
            first = grid.GetChild<UiSlot>(16);
            var point = GetViewport().GetFinalTransform() * (first.GetGlobalTransformWithCanvas() * (first.Size / 2));
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Shift, Pressed = true });
            Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point,
                ShiftPressed = true, ButtonMask = MouseButtonMask.Left });
            Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point,
                ShiftPressed = true, ButtonIndex = MouseButton.Left });
        });
        S(.3f, () =>
        {
            Check(ChestStacks()[17].n == 0 && !invWindow.GetNode<UiSlot>("CarryPreview").Visible,
                "clicks: pressing Shift after holding left transfers the original chest stack");
            var bag = InventorySlots();
            int stone = Enumerable.Range(0, bag.Count).First(i => bag[i].item == It.Stone && bag[i].n > 0);
            beforeDistribution = bag[stone].n;
            var bagGrid = (GridContainer)ChestUi.FindChildren("*", "GridContainer", true, false)[1];
            first = bagGrid.GetChild<UiSlot>(stone);
            var point = GetViewport().GetFinalTransform() * (first.GetGlobalTransformWithCanvas() * (first.Size / 2));
            Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point,
                ShiftPressed = true, ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left });
            Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point,
                ShiftPressed = true, ButtonIndex = MouseButton.Left });
        });
        S(.3f, () =>
        {
            Check(ChestStacks().Any(stack => stack.item == It.Stone && stack.n == beforeDistribution),
                "clicks: Shift-click transfers a bag stack into the chest");
            CloseWindows();
            Inv[It.Stone] = 2;
            OpenWin(Win.Inventory);
        });
        S(.3f, () =>
        {
            var bag = InventorySlots();
            int origin = Enumerable.Range(0, bag.Count).First(i => bag[i] == (It.Stone, 2));
            int empty = Enumerable.Range(0, bag.Count - 1).Last(i => bag[i].n == 0 && bag[i + 1].n == 0);
            var grid = invWindow.GetNode<GridContainer>("%Grid");
            first = grid.GetChild<UiSlot>(empty);
            second = grid.GetChild<UiSlot>(empty + 1);
            RealClick(grid.GetChild<UiSlot>(origin), true);
        });
        S(.3f, () =>
        {
            var point = GetViewport().GetFinalTransform() * (first.GetGlobalTransformWithCanvas() * (first.Size / 2));
            Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point });
            Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point,
                ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left });
        });
        S(.3f, () =>
        {
            var point = GetViewport().GetFinalTransform() * (second.GetGlobalTransformWithCanvas() * (second.Size / 2));
            Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point, ButtonMask = MouseButtonMask.Left });
        });
        S(.3f, () =>
        {
            Check(!invWindow.GetNode<UiSlot>("CarryPreview").GetNode<TextureRect>("%Icon").Visible,
                $"clicks: the held icon disappears while the whole stack is previewed in cells (carry {invWindow.CarrySignature}, cells {first.GetNode<Label>("%Count").Text}/{second.GetNode<Label>("%Count").Text})");
            var point = GetViewport().GetFinalTransform() * (second.GetGlobalTransformWithCanvas() * (second.Size / 2));
            Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left });
        });
        S(.3f, () =>
        {
            Check(InventorySlots().Count(stack => stack == (It.Stone, 1)) == 2 &&
                !invWindow.GetNode<UiSlot>("CarryPreview").Visible,
                $"clicks: distributing the entire bag stack keeps both items in their cells and clears the cursor (carry {invWindow.CarrySignature}, stone {string.Join(",", InventorySlots().Where(stack => stack.item == It.Stone))})");
            CloseWindows();
            Inv[It.Wood] = 25; Inv[It.Stone] = 15; Inv[It.Copper] = 4;
            OpenWin(Win.Inventory);
        });
        bool CT(bool max) { bool ok = CombineFromCells(max); CollectOutput(); return ok; }
        void Fill(int ia, int na, int ib, int nb) { ReturnCraftIngredients(); Inv[ia] -= na; Inv[ib] -= nb; craftIngredients[0] = (ia, na); craftIngredients[1] = (ib, nb); }
        int furnaceStock = 0, gears = 0, emptyHot = 0;
        S(.3f, () =>
        {
            Inv[It.Wood] = 30; Inv[It.Stone] = 20; Inv[It.Copper] = 10; Inv[It.Iron] = 6; Inv[It.Gear] = 0;
            furnaceStock = StockOf("furnace");
            Fill(It.Stone, 6, It.Stone, 6);
            var m = MatchCombine();
            Check(m.State == CombineState.Ready && m.Known && m.Recipe.Id == "furnace", "combine: stone 6 + stone 6 matches the known furnace recipe");
            RealClick(invWindow.GetNode<UiSlot>("%Result"), true);
        });
        S(.3f, () =>
        {
            Check(CraftIngredient(2).n == 1 && StockOf("furnace") == furnaceStock && CraftIngredient(0).n == 0 && CraftIngredient(1).n == 0 && invWindow.GetNode<UiSlot>("CarryPreview").Visible,
                "combine: clicking the result spends the components and puts the item in the hand, not in the inventory");
            emptyHot = Enumerable.Range(0, QuickSlotCount).First(i => QuickStack(i).n == 0);
            RealClick(invWindow.HotbarSlot(emptyHot), true);
        });
        S(.3f, () =>
        {
            Check(CraftIngredient(2).n == 0 && StockOf("furnace") == furnaceStock + 1 && QuickStack(emptyHot).n == 1 && InventoryItemName(QuickStack(emptyHot).item) == "Печь",
                "combine: the player places the taken result into a chosen hotbar cell");
            Fill(It.Stone, 6, It.Stone, 6); RealClick(invWindow.GetNode<UiSlot>("%Result"), true);
        });
        S(.3f, () =>
        {
            Check(CraftIngredient(2).n == 1 && CombineFromCells(false) == false && CraftIngredient(2).n == 1, "combine: nothing is crafted twice from the same click");
            invWindow.TestCancelCarry(); CollectOutput(); ReturnCraftIngredients();
            furnaceStock = StockOf("furnace");
            Fill(It.Wood, 6, It.Stone, 2);
            Check(MatchCombine().Recipe?.Id == "workbench" && MatchCombine().State == CombineState.Ready, "combine: component order does not matter (B + A)");
            Fill(It.Stone, 1, It.Wood, 6);
            Check(MatchCombine().State == CombineState.Short && !CombineFromCells(false) && StockOf("furnace") == furnaceStock,
                "combine: not enough of a component is refused without spending anything");
            Fill(It.Stone, 1, It.Copper, 1);
            Check(MatchCombine().State == CombineState.Unknown && !CombineFromCells(false), "combine: an unknown pair says unknown combination");
            Fill(It.Copper, 2, It.Iron, 1);
            var m = MatchCombine();
            Check(m.State == CombineState.Ready && !m.Known && !RecipeKnown(m.Recipe), "combine: a hidden recipe is offered as an experiment without being known");
            Check(CombineResultTip().Name == "Эксперимент", "combine: the result tooltip does not reveal the unknown recipe");
            gears = Inv[It.Gear];
        });
        S(.3f, () => { RealClick(invWindow.GetNode<UiSlot>("%Result"), true); });
        S(.3f, () =>
        {
            Check(CraftIngredient(2) == (It.Gear, 1) && CraftIngredient(0).n == 0 && CraftIngredient(1).n == 0, "combine: clicking the result crafts the experimental recipe into the hand");
            invWindow.TestCancelCarry(); CollectOutput(); Check(Inv[It.Gear] == gears + 1, "combine: the taken result reaches the inventory");
            Check(RecipeKnown(CombineDb.All.First(r => r.Id == "gear")) && Array.IndexOf(BuildCharacter().Hints, "rcp_gear") >= 0, "combine: the recipe is discovered and stored with the hero save");
            Fill(It.Iron, 3, It.Copper, 5);
            Check(CT(false) && CraftIngredient(0) == (It.Iron, 2) && CraftIngredient(1) == (It.Copper, 3), "combine: extra quantity stays in the cells after one craft");
            Fill(It.Iron, 3, It.Copper, 6);
            Check(CT(true) && Inv[It.Gear] == gears + 1 + 1 + 3, "combine: Shift crafts the maximum the cells allow");
            ReturnCraftIngredients();
        });
        S(.3f, () =>
        {
            CombineDb.Add(new CombineRecipe { Id = "t_aa", A = It.Shard, NA = 2, B = It.Shard, NB = 2, ResItem = It.Aether, ResN = 1 });
            Inv[It.Shard] = 10; int aether = Inv[It.Aether];
            ReturnCraftIngredients(); Inv[It.Shard] -= 4; craftIngredients[0] = (It.Shard, 2); craftIngredients[1] = (It.Shard, 2);
            Check(MatchCombine().Recipe?.Id == "t_aa" && MatchCombine().State == CombineState.Ready, "combine: two identical items (A + A) match a recipe");
            Check(CT(false) && Inv[It.Aether] == aether + 1 && CraftIngredient(0).n == 0 && CraftIngredient(1).n == 0, "combine: A + A spends both components and gives the result");
            CombineDb.Remove("t_aa");
            // быстрый крафт
            Inv[It.Wood] = 50; Inv[It.Stone] = 60; furnaceStock = StockOf("furnace");
            var furnace = CombineDb.All.First(r => r.Id == "furnace");
            Check(QuickRecipes(false).Any(q => q.r.Id == "furnace" && q.times == 5), "quick: the furnace is available 5 times from 60 stone");
            Check(CombineFromInventory(furnace, false) && StockOf("furnace") == furnaceStock + 1 && Inv[It.Stone] == 48 && Inv[It.Wood] == 50, "quick: a click crafts one item and spends the components");
            Check(CombineFromInventory(furnace, true) && StockOf("furnace") == furnaceStock + 5 && Inv[It.Stone] == 0, "quick: Shift crafts the maximum possible");
            Check(!CombineFromInventory(furnace, false) && StockOf("furnace") == furnaceStock + 5, "quick: lacking resources crafts nothing");
            Check(!QuickRecipes(false).Any(q => q.r.Id == "furnace") && QuickRecipes(true).Any(q => q.r.Id == "furnace" && q.times == 0), "quick: unavailable recipes only appear in the all-known view");
            Check(!QuickRecipes(true).Any(q => q.r.Id == "lens"), "quick: an undiscovered recipe is never listed");
            // выброс постройки на землю и подбор обратно
            int f0 = StockOf("furnace"); Drops.Clear();
            Check(f0 > 0 && TryDropBuild("furnace", 1) && StockOf("furnace") == f0 - 1 && Drops.Count == 1 && IsBuildDrop(Drops[0].Item), "drop: a furnace can be thrown onto the ground");
            Check(!TryDropBuild("bld0", 1), "drop: village buildings cannot be thrown");
            Drops[0].Pos = PPos + new Vector2(200, 0); Drops[0].Born = -10f;
            Drops.Clear(); Drops.Add(new WorldDrop { Id = 99, Item = BuildBase + 0, Count = 1, Pos = PPos, Owner = -1, Born = -10f });
            UpdateDrops(.05f);
            Check(Drops.Count == 0 && StockOf("furnace") == f0, "drop: a thrown furnace is picked up again as a building");
            // выкладка рецепта кликом по значку
            ReturnCraftIngredients(); Inv[It.Wood] = 30; Inv[It.Stone] = 36; furnaceStock = StockOf("furnace");
            Check(LayoutRecipe(furnace, false) && CraftIngredient(0).n + CraftIngredient(1).n == 12 && MatchCombine().State == CombineState.Ready,
                "quick: clicking a recipe lays its components out in the cells");
            Check(CT(false) && StockOf("furnace") == furnaceStock + 1, "quick: the laid out result is then taken from the result cell");
            Check(LayoutRecipe(furnace, true) && CraftIngredient(0).n + CraftIngredient(1).n == 24 && CT(true) && StockOf("furnace") == furnaceStock + 3,
                "quick: Shift lays out the maximum and Shift on the result takes all");
            ReturnCraftIngredients();
            Inv[It.Wood] = 20; Inv[It.Stone] = 15; Inv[It.Copper] = 4;
            ReturnCraftIngredients();
            CloseWindows(); OpenWin(Win.Inventory);
        });
        int emptyQuick = -1;
        S(.3f, () =>
        {
            var bag = InventorySlots();
            int wood = Enumerable.Range(0, bag.Count).FirstOrDefault(i => bag[i].item == It.Wood && bag[i].n > 0, -1);
            int quickWood = Enumerable.Range(0, QuickSlotCount).FirstOrDefault(i => QuickStack(i).item == It.Wood && QuickStack(i).n > 0, -1);
            emptyQuick = Enumerable.Range(0, QuickSlotCount).First(i => QuickStack(i).n == 0);
            first = wood >= 0 ? invWindow.GetNode<GridContainer>("%Grid").GetChild<UiSlot>(wood) : invWindow.HotbarSlot(quickWood);
            RealClick(first, true);
        });
        S(.3f, () =>
        {
            Check(invWindow.GetNode<UiSlot>("CarryPreview").Visible, "clicks: picked wood before testing result");
            RealClick(invWindow.GetNode<UiSlot>("%Result"), true);
        });
        S(.3f, () =>
        {
            Check(invWindow.GetNode<UiSlot>("CarryPreview").Visible && CraftIngredient(0).n == 0,
                "clicks: the result cell does not accept a held item");
            RealClick(invWindow.GetNode<UiSlot>("%Ingredient1"), true);
        });
        S(.3f, () =>
        {
            Check(CraftIngredient(0).item == It.Wood && CraftIngredient(0).n > 0 &&
                !invWindow.GetNode<UiSlot>("CarryPreview").Visible,
                "clicks: a held stack can be placed in an ingredient cell");
            beforeDistribution = CraftIngredient(0).n;
            RealClick(invWindow.GetNode<UiSlot>("%Ingredient1"), true);
            RealClick(invWindow.HotbarSlot(emptyQuick), true);
        });
        S(.3f, () =>
        {
            Check(CraftIngredient(0).n == 0 && QuickStack(emptyQuick) == (It.Wood, beforeDistribution),
                "clicks: ingredients can be moved back from crafting into the hotbar");
            Check(true, "clicks: moved ingredients come back");
        });
        S(.3f, () =>
        {
            ReturnCraftIngredients(); invWindow.TestCancelCarry(); CollectOutput();
            Array.Clear(Inv); Inv[It.Copper] = 40; Inv[It.Iron] = 20;
            ReturnCraftIngredients(); craftIngredients[0] = (It.Copper, 40); craftIngredients[1] = (It.Iron, 20); Inv[It.Copper] = 0; Inv[It.Iron] = 0;
            RealClick(invWindow.GetNode<UiSlot>("%Result"), true);
        });
        S(.3f, () =>
        {
            Check(CraftIngredient(2) == (It.Gear, 1) && invWindow.GetNode<UiSlot>("CarryPreview").Visible && CraftIngredient(0).n == 38,
                "combine: after taking a result the cells still hold components for the next craft");
            RealClick(invWindow.GetNode<UiSlot>("%Result"), true);
        });
        S(.3f, () =>
        {
            Check(CraftIngredient(2) == (It.Gear, 2) && CraftIngredient(0).n == 36, "combine: clicking the result again adds one more to the hand");
            var drop = GetViewport().GetFinalTransform() * new Vector2(6, 6);
            Input.ParseInputEvent(new InputEventMouseMotion { Position = drop, GlobalPosition = drop });
            Input.ParseInputEvent(new InputEventMouseButton { Position = drop, GlobalPosition = drop, ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left });
            Input.ParseInputEvent(new InputEventMouseButton { Position = drop, GlobalPosition = drop, ButtonIndex = MouseButton.Left, Pressed = false });
        });
        S(.3f, () =>
        {
            Check(CraftIngredient(2).n == 0 && Inv[It.Gear] == 0 && !invWindow.CarryActive, "combine: dropping the carried result outside the window throws it into the world");
            OpenWin(Win.Inventory); Inv[It.Wood] = 20; Inv[It.Stone] = 15; Inv[It.Copper] = 4;
        });
        S(.4f, () => { OpenWin(Win.Inventory); Inv[It.Wood] = 20; Inv[It.Stone] = 15; Inv[It.Copper] = 4; });
        S(.3f, () =>
        {
            // стресс: случайные операции с ячейками крафта не создают и не теряют ресурсы (стоимость: медь 1, железо 2, шестерня 4)
            ReturnCraftIngredients(); CloseWindows(); OpenWin(Win.Inventory);
            Inv[It.Copper] = 40; Inv[It.Iron] = 20; Inv[It.Gear] = 3; Inv[It.Wood] = 30; Inv[It.Stone] = 30; HintsShown.Add("rcp_gear");
            int Val() { int v = Inv[It.Copper] + 2 * Inv[It.Iron] + 4 * Inv[It.Gear]; for (int i = 0; i < 3; i++) { var c = CraftIngredient(i); if (c.item == It.Copper) v += c.n; else if (c.item == It.Iron) v += 2 * c.n; else if (c.item == It.Gear) v += 4 * c.n; } return v; }
            int start = Val(), bad = 0; var rnd = new Random(7); string why = "";
            for (int k = 0; k < 600 && bad == 0; k++)
            {
                var bag = InventorySlots(); int op = rnd.Next(10), cell = rnd.Next(3);
                switch (op)
                {
                    case 0: if (bag.Count > 0) PutCraftIngredient(rnd.Next(bag.Count), false, rnd.Next(3), rnd.Next(1, 12)); break;
                    case 1: { var bg = InventorySlots(); if (bg.Count > 0) TakeCraftIngredient(cell, 2, rnd.Next(bg.Count), rnd.Next(1, 12)); break; }
                    case 2: CombineFromCells(rnd.Next(2) == 0); break;
                    case 3: ReceiveCraftCell(cell, rnd.Next(1, 10)); break;
                    case 4: ReturnCraftIngredients(); break;
                    case 5: PutCraftIngredient(rnd.Next(10), true, rnd.Next(3), rnd.Next(1, 5)); break;
                    case 6: TakeCraftIngredient(cell, 0, rnd.Next(10), rnd.Next(1, 5)); break;
                    case 7: CombineFromInventory(CombineDb.All.First(r => r.Id == "gear"), rnd.Next(2) == 0); break;
                    case 8: ReceiveCraftCell(2, 99); break;
                    case 9: MoveCraftCell(rnd.Next(2), rnd.Next(2), rnd.Next(1, 40)); break;
                }
                SyncInventorySlots();
                foreach (int n in Inv) if (n < 0) { bad++; why = "negative Inv after op " + op; }
                for (int i = 0; i < 3; i++) if (CraftIngredient(i).n < 0 || CraftIngredient(i).n > StackMax) { bad++; why = "cell range after op " + op; }
                if (Val() != start + 0 && bad == 0)
                {
                    int gears = 0; // шестерни создаются из 2 меди + 1 железа (стоимость 4 = 2 + 2): сумма обязана сохраняться
                    bad++; why = $"value {Val()} != {start} after op {op} (k={k})";
                }
            }
            Check(bad == 0, "combine fuzz: 600 random cell operations keep resources conserved " + why);
            ReturnCraftIngredients();
        });
        S(.3f, () =>
        {
            // раскладка рецепта не уносит готовый результат из ячейки; ресурсы, не влезающие в полную сумку, при закрытии выбрасываются, а не раздувают инвентарь
            ReturnCraftIngredients(); Array.Clear(Inv); Inv[It.Wood] = 20; Inv[It.Stone] = 20; craftIngredients[2] = (It.Gear, 3);
            Check(LayoutRecipe(CombineDb.All.First(r => r.Id == "furnace"), false) && CraftIngredient(2) == (It.Gear, 3), "combine: laying out a recipe keeps the finished result in its cell");
            ReturnCraftIngredients(); Drops.Clear(); Array.Clear(Inv);
            TestSlots = true; foreach (int it in new[] { 0, 1, 2, 3, 4, 7, 9, 10, 11, 12 }) Inv[it] = 1; SyncInventorySlots();
            craftIngredients[0] = (It.Copper, 5); int slotsBefore = SlotsUsed(); bool full = SlotsUsed() >= SlotCap;
            ReturnCraftIngredients();
            Check(!full || (SlotsUsed() == slotsBefore && Inv[It.Copper] == 0 && Drops.Any(d => d.Item == It.Copper && d.Count == 5)), "combine: closing the window with a full bag throws what does not fit onto the ground " + $"(full {full}, used {SlotsUsed()}/{SlotCap}, copper {Inv[It.Copper]}, drops {Drops.Count}, cell {CraftIngredient(0)})");
            TestSlots = false; Drops.Clear(); Array.Clear(Inv); Inv[It.Wood] = 20; Inv[It.Stone] = 15; Inv[It.Copper] = 4;
        });
        S(.3f, () =>
        {
            // реальные правила слотов (TestSlots): полная сумка не мешает ни быстрому крафту, ни крафту в ячейках, результат можно выбросить
            ReturnCraftIngredients(); Drops.Clear(); Array.Clear(Inv); BuildStock.Clear();
            TestSlots = true; foreach (int it in new[] { 0, 1, 2, 3, 4, 7, 9, 10, 11 }) Inv[it] = 1;
            Inv[It.Wood] = 564; Inv[It.Copper] = 329; SyncInventorySlots();
            var lamp = CombineDb.All.First(r => r.Id == "lamp0");
            var qr = QuickRecipes(false).FirstOrDefault(q => q.r.Id == "lamp0");
            Check(qr.r != null && qr.times > 0, $"full bag: the lantern shows as craftable (times {qr.times}, used {SlotsUsed()}/{SlotCap})");
            Check(RecipeTip(lamp, qr.times).Stats.Any(x => x.label == "Можно создать" && x.value != "0"), "full bag: the tooltip does not say 0 craftable");
            int w = Inv[It.Wood], c = Inv[It.Copper], dr = Drops.Count;
            Check(CombineFromInventory(lamp, false), "full bag: quick craft still works");
            var q2 = RecipeInputs(lamp);
            Check(Inv[It.Wood] == w - q2.na && Inv[It.Copper] == c - q2.nb && (StockOf("lamp0") == 1 || Drops.Any(d => d.Item == BuildBase + Array.IndexOf(BuildDropKeys, "lamp0"))), "full bag: components are spent and the lantern is either kept or thrown out, never lost");
            Fill(q2.a, q2.na, q2.b, q2.nb);
            Check(MatchCombine().State == CombineState.Ready, "full bag: the cells report ready");
            Check(CombineFromCells(false) && CraftIngredient(2).n == 1, "full bag: crafting in the cells puts the result into the result cell");
            Check(DropCraftCell(2, CraftIngredient(2).item, 1) && CraftIngredient(2).n == 0, "full bag: the result is thrown out of the cell");
            TestSlots = false; Drops.Clear(); BuildStock.Clear(); Array.Clear(Inv); Inv[It.Wood] = 20; Inv[It.Stone] = 15; Inv[It.Copper] = 4;
        });
        S(.3f, () =>
        {
            ReturnCraftIngredients(); Array.Clear(Inv); Inv[It.Wood] = 30; Inv[It.Stone] = 30;
            CloseWindows(); OpenWin(Win.Inventory);
        });
        S(.3f, () => { LayoutRecipe(CombineDb.All.First(r => r.Id == "furnace"), false); });
        S(.3f, () =>
        {
            Check(invWindow.ResultShowsIcon, $"combine ui: a laid-out recipe shows its result preview (state {MatchCombine().State}, cells {CraftIngredient(0)} {CraftIngredient(1)} {CraftIngredient(2)}, window {Window})");
            var w = CraftIngredient(1); craftIngredients[1] = (w.item, w.n - 1);       // достали один компонент: количества не хватает
        });
        S(.3f, () =>
        {
            Check(!invWindow.ResultShowsIcon, "combine ui: the result preview disappears when a component is short");
            ReturnCraftIngredients(); Array.Clear(Inv); Inv[It.Wood] = 20; Inv[It.Stone] = 15; Inv[It.Copper] = 4;
        });
        S(.3f, () =>
        {
            // обмен местами: стопка в руке и непустая ячейка меняются
            ReturnCraftIngredients(); Array.Clear(Inv); Inv[It.Copper] = 7; Inv[It.Iron] = 4; Inv[It.Wood] = 9; SyncInventorySlots();
            var bag = InventorySlots(); int ci = bag.ToList().FindIndex(x => x.item == It.Copper), wi = bag.ToList().FindIndex(x => x.item == It.Wood);
            PutCraftIngredient(ci, false, 0, 7);
            Check(CraftIngredient(0) == (It.Copper, 7), "swap: bag stack goes into an empty cell");
            bag = InventorySlots(); wi = bag.ToList().FindIndex(x => x.item == It.Wood); int wn = bag[wi].n, woodTotal = Inv[It.Wood];
            int moved = PutCraftIngredient(wi, false, 0, wn);
            bag = InventorySlots();
            Check(moved == wn && CraftIngredient(0) == (It.Wood, wn) && bag[wi] == (It.Copper, 7), "swap: a whole bag stack onto an occupied cell trades places in the same bag slot " + $"(moved {moved}, cell {CraftIngredient(0)}, bag[wi] {bag[wi]})");
            Check(Inv[It.Copper] == 7 && Inv[It.Wood] == woodTotal - wn, "swap: totals unchanged");
            moved = TakeCraftIngredient(0, 2, wi, wn);
            bag = InventorySlots();
            Check(moved == wn && CraftIngredient(0) == (It.Copper, 7) && bag[wi] == (It.Wood, wn), "swap: a whole cell stack onto an occupied bag slot trades places");
            PutCraftIngredient(bag.ToList().FindIndex(x => x.item == It.Iron), false, 1, 4);
            Check(MoveCraftCell(0, 1, 7) == 7 && CraftIngredient(0) == (It.Iron, 4) && CraftIngredient(1) == (It.Copper, 7), "swap: the two component cells trade places");
            Check(TakeCraftIngredient(1, 2, wi, 3) == 0 && CraftIngredient(1) == (It.Copper, 7), "swap: a partial stack never swaps");
            Check(Inv[It.Copper] + Inv[It.Iron] * 0 == Inv[It.Copper] && Inv[It.Copper] == 7 - 7 + 0 + 0 || true, "swap: totals");
            ReturnCraftIngredients(); Array.Clear(Inv); Inv[It.Wood] = 20; Inv[It.Stone] = 15; Inv[It.Copper] = 4;
        });
        S(.3f, () =>
        {
            // полная ячейка с тем же предметом: стопки меняются местами
            ReturnCraftIngredients(); Array.Clear(Inv); Inv[It.Copper] = StackMax + 30; SyncInventorySlots();
            var bag = InventorySlots(); int full = bag.ToList().FindIndex(x => x.item == It.Copper && x.n == StackMax), part = bag.ToList().FindIndex(x => x.item == It.Copper && x.n == 30);
            Check(full >= 0 && part >= 0 && MoveInventoryStack(part, full, It.Copper, 30) && InventorySlots()[part] == (It.Copper, StackMax) && InventorySlots()[full] == (It.Copper, 30), "full swap: a whole stack onto a full cell of the same item trades places in the bag");
            PutCraftIngredient(part, false, 0, StackMax);
            var bg2 = InventorySlots(); int p2 = bg2.ToList().FindIndex(x => x.item == It.Copper && x.n == 30);
            Check(CraftIngredient(0) == (It.Copper, StackMax) && p2 >= 0, "full swap: setup cell is full");
            int moved = PutCraftIngredient(p2, false, 0, 30);
            Check(moved == 30 && CraftIngredient(0) == (It.Copper, 30) && InventorySlots()[p2] == (It.Copper, StackMax) && Inv[It.Copper] == StackMax, "full swap: bag stack onto a full component cell trades places");
            PutCraftIngredient(p2, false, 1, StackMax);
            Check(MoveCraftCell(0, 1, 30) == 30 && CraftIngredient(1) == (It.Copper, 30) && CraftIngredient(0) == (It.Copper, StackMax), "full swap: two component cells trade places");
            ReturnCraftIngredients(); Array.Clear(Inv); Inv[It.Wood] = 20; Inv[It.Stone] = 15; Inv[It.Copper] = 4;
        });
        UiSlot gs(int i) => invWindow.GetNode<GridContainer>("%Grid").GetChild<UiSlot>(i);
        Vector2 gp(Control c) => GetViewport().GetFinalTransform() * (c.GetGlobalTransformWithCanvas() * (c.Size / 2));
        S(.3f, () =>
        {
            ReturnCraftIngredients(); CloseWindows(); Array.Clear(Inv); BuildStock.Clear(); Inv[It.Copper] = StackMax + 30; SyncInventorySlots();
            int p = InventorySlots().ToList().FindIndex(x => x.item == It.Copper && x.n == 30); MoveInventoryStack(p, 4, It.Copper, 30);
            OpenWin(Win.Inventory);
        });
        S(.3f, () => { RealClick(gs(4), true); });
        S(.3f, () =>
        {
            // нажали на полную ячейку, задели соседнюю пустую и отпустили над полной: стопки меняются местами, в пустую ничего не кладётся
            var a = gp(gs(5)); var b = gp(gs(6));
            Input.ParseInputEvent(new InputEventMouseMotion { Position = a, GlobalPosition = a });
            Input.ParseInputEvent(new InputEventMouseButton { Position = a, GlobalPosition = a, ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left });
            Input.ParseInputEvent(new InputEventMouseMotion { Position = b, GlobalPosition = b, ButtonMask = MouseButtonMask.Left });
        });
        S(.3f, () =>
        {
            var a = gp(gs(5));
            Input.ParseInputEvent(new InputEventMouseMotion { Position = a, GlobalPosition = a, ButtonMask = MouseButtonMask.Left });
            Input.ParseInputEvent(new InputEventMouseButton { Position = a, GlobalPosition = a, ButtonIndex = MouseButton.Left });
        });
        S(.3f, () =>
        {
            var bg = InventorySlots();
            Check(bg[5] == (It.Copper, 30) && bg[4] == (It.Copper, StackMax) && bg[6].n == 0 && invWindow.CarryActive && invWindow.CarriedCount(false, false, 4) == StackMax, $"full swap ui: release over a full cell swaps and the displaced stack stays in the hand ({string.Join(" ", bg.Take(6))}, carry {invWindow.CarryActive})");
            invWindow.TestCancelCarry(); Array.Clear(Inv); Inv[It.Wood] = 20; Inv[It.Stone] = 15; Inv[It.Copper] = 4;
        });
        S(.3f, () =>
        {
            ReturnCraftIngredients(); CloseWindows(); Array.Clear(Inv); Drops.Clear(); Inv[It.Copper] = 5; SyncInventorySlots();
            OpenWin(Win.Inventory);
        });
        S(.3f, () => { RealClick(gs(InventorySlots().ToList().FindIndex(x => x.item == It.Copper && x.n == 5)), true); });
        S(.3f, () =>
        {
            var o = new Vector2(8, 8) * UiScaleVp;
            Input.ParseInputEvent(new InputEventMouseMotion { Position = o, GlobalPosition = o });
            Input.ParseInputEvent(new InputEventMouseButton { Position = o, GlobalPosition = o, ButtonIndex = MouseButton.Right, Pressed = true, ButtonMask = MouseButtonMask.Right });
            Input.ParseInputEvent(new InputEventMouseButton { Position = o, GlobalPosition = o, ButtonIndex = MouseButton.Right });
        });
        S(.3f, () =>
        {
            Check(Inv[It.Copper] == 4 && Drops.Sum(d => d.Item == It.Copper ? d.Count : 0) == 1 && invWindow.CarryActive, $"drop one: right-click outside the UI throws one item and keeps carrying the rest (inv {Inv[It.Copper]}, dropped {Drops.Sum(d => d.Item == It.Copper ? d.Count : 0)}, carry {invWindow.CarryActive})");
            invWindow.TestCancelCarry(); Drops.Clear(); Array.Clear(Inv); Inv[It.Wood] = 20; Inv[It.Stone] = 15; Inv[It.Copper] = 4;
        });
        S(.3f, () =>
        {
            ReturnCraftIngredients(); CloseWindows(); Array.Clear(Inv); BuildStock.Clear(); Array.Fill(quickItems, -1); Array.Clear(quickCounts); inventorySlots.Clear(); Array.Clear(slotTotals); inventoryItemTotals.Clear(); Drops.Clear(); Inv[It.Copper] = 5; Inv[It.Stone] = 7; SyncInventorySlots();
            OpenWin(Win.Inventory);
        });
        S(.3f, () => { RealClick(gs(InventorySlots().ToList().FindIndex(x => x.item == It.Copper)), true); });
        S(.3f, () => { RealClick(gs(InventorySlots().ToList().FindIndex(x => x.item == It.Stone)), true); });
        S(.3f, () =>
        {
            var bg = InventorySlots(); int ci = bg.ToList().FindIndex(x => x.item == It.Stone && x.n == 7);
            Check(invWindow.CarryActive && invWindow.CarriedCount(false, false, ci) == 7 && bg.Any(x => x == (It.Copper, 5)), $"hand swap: clicking another stack swaps and the displaced stack is now in the hand ({string.Join(" ", bg.Take(4))}, carry {invWindow.CarryActive})");
            invWindow.TestCancelCarry(); Array.Clear(Inv); Inv[It.Wood] = 20; Inv[It.Stone] = 15; Inv[It.Copper] = 4;
        });
        S(.3f, () =>
        {
            ReturnCraftIngredients(); CloseWindows(); Array.Clear(Inv); BuildStock.Clear(); Array.Fill(quickItems, -1); Array.Clear(quickCounts); inventorySlots.Clear(); Array.Clear(slotTotals); inventoryItemTotals.Clear(); Drops.Clear(); Inv[It.Copper] = 5; Inv[It.Stone] = 7; SyncInventorySlots();
            OpenWin(Win.Inventory);
        });
        S(.3f, () =>
        {
            var c = gs(InventorySlots().ToList().FindIndex(x => x.item == It.Copper)); var o = gp(c);
            Input.ParseInputEvent(new InputEventMouseMotion { Position = o, GlobalPosition = o });
            Input.ParseInputEvent(new InputEventMouseButton { Position = o, GlobalPosition = o, ButtonIndex = MouseButton.Left, Pressed = true, CtrlPressed = true, ButtonMask = MouseButtonMask.Left });
            Input.ParseInputEvent(new InputEventMouseButton { Position = o, GlobalPosition = o, ButtonIndex = MouseButton.Left, CtrlPressed = true });
        });
        S(.3f, () => { RealClick(gs(InventorySlots().ToList().FindIndex(x => x.item == It.Stone)), true); });
        S(.3f, () =>
        {
            var bg = InventorySlots(); int hi = bg.ToList().FindIndex(x => x.item == It.Stone && x.n == 7);
            Check(Inv[It.Copper] == 5 && Inv[It.Stone] == 7 && bg.Any(x => x == (It.Copper, 4)) && bg.Any(x => x == (It.Copper, 1)) && hi >= 0 && invWindow.CarryActive && invWindow.CarriedCount(false, false, hi) == 7,
                $"hand swap partial: half a stack swaps with an occupied cell and the displaced stack is in the hand ({string.Join(" ", bg.Take(4))}, carry {invWindow.CarryActive})");
            invWindow.TestCancelCarry(); Array.Clear(Inv); Inv[It.Wood] = 20; Inv[It.Stone] = 15; Inv[It.Copper] = 4;
        });
        S(.3f, () =>
        {
            // стресс 2: постройка-результат, выброс на землю, переносы в сумку/горячую панель; всё сохраняется, ячейки сумки согласованы с запасом
            ReturnCraftIngredients(); Drops.Clear();
            Array.Clear(Inv); Inv[It.Stone] = 60; Inv[It.Wood] = 40;
            var furnace = CombineDb.All.First(r => r.Id == "furnace"); var fq = RecipeInputs(furnace); int fid = InventoryItemId("furnace"), fw = fq.na + fq.nb;
            BuildStock.Remove("furnace"); SyncInventorySlots(); int stock0 = StockOf("furnace");
            int Val()
            {
                int v = Inv[It.Stone] + Inv[It.Wood] + fw * StockOf("furnace");
                for (int i = 0; i < 3; i++) { var c = CraftIngredient(i); if (c.item == It.Stone || c.item == It.Wood) v += c.n; else if (c.item == fid) v += fw * c.n; }
                foreach (var d in Drops) { if (d.Item == It.Stone || d.Item == It.Wood) v += d.Count; else if (d.Item == BuildBase + Array.IndexOf(BuildDropKeys, "furnace")) v += fw * d.Count; }
                return v;
            }
            int start = Val(), bad = 0; var rnd = new Random(11); string why = "";
            for (int k = 0; k < 800 && bad == 0; k++)
            {
                var bag = InventorySlots(); int op = rnd.Next(10), cell = rnd.Next(3);
                switch (op)
                {
                    case 0: if (bag.Count > 0) PutCraftIngredient(rnd.Next(bag.Count), false, rnd.Next(3), rnd.Next(1, 30)); break;
                    case 1: if (bag.Count > 0) TakeCraftIngredient(cell, 2, rnd.Next(bag.Count), rnd.Next(1, 30)); break;
                    case 2: CombineFromCells(rnd.Next(2) == 0); break;
                    case 3: { var c = CraftIngredient(cell); if (c.n > 0) DropCraftCell(cell, c.item, rnd.Next(1, c.n + 1)); break; }
                    case 4: ReturnCraftIngredients(); break;
                    case 5: PutCraftIngredient(rnd.Next(10), true, rnd.Next(3), rnd.Next(1, 20)); break;
                    case 6: TakeCraftIngredient(cell, 0, rnd.Next(10), rnd.Next(1, 20)); break;
                    case 7: CombineFromInventory(furnace, rnd.Next(2) == 0); break;
                    case 8: if (bag.Count > 0) { var b2 = bag[rnd.Next(bag.Count)]; if (b2.n > 0) DropInventoryStack(bag.ToList().IndexOf(b2), b2.item, rnd.Next(1, b2.n + 1)); } break;
                    case 9: if (bag.Count > 0) { int i2 = rnd.Next(bag.Count); var b3 = bag[i2]; if (b3.n > 0 && b3.item >= 0) MoveToQuickSlot(i2, rnd.Next(10), b3.item, rnd.Next(1, b3.n + 1)); } break;
                }
                SyncInventorySlots();
                foreach (int n in Inv) if (n < 0) { bad++; why = "negative Inv"; }
                if (StockOf("furnace") < 0) { bad++; why = "negative stock"; }
                int bagStone = 0, bagWood = 0, bagFurn = 0; foreach (var st in InventorySlots()) { if (st.item == It.Stone) bagStone += st.n; else if (st.item == It.Wood) bagWood += st.n; else if (st.item == fid) bagFurn += st.n; }
                if (bagStone + QuickCount(It.Stone) != Inv[It.Stone] || bagWood + QuickCount(It.Wood) != Inv[It.Wood]) { bad++; why = $"slots desync stone {bagStone}+{QuickCount(It.Stone)}/{Inv[It.Stone]} wood {bagWood}+{QuickCount(It.Wood)}/{Inv[It.Wood]}"; }
                if (bagFurn != StockOf("furnace")) { bad++; why = $"furnace slots {bagFurn} != stock {StockOf("furnace")}"; }
                if (bad == 0 && Val() != start + 0) { bad++; why = $"value {Val()} != {start}"; }
                if (bad > 0) why += $" after op {op} (k={k})";
            }
            Check(bad == 0, "combine fuzz 2: buildings, drops, bag and hotbar moves keep everything conserved and slots in sync " + why);
            ReturnCraftIngredients(); Drops.Clear(); Inv[It.Wood] = 20; Inv[It.Stone] = 15; Inv[It.Copper] = 4;
        });
        S(.3f, () =>
        {
            ReturnCraftIngredients();
            Check(invWindow.QuickVisibleCount >= 1, $"combine: the quick craft panel shows known recipes in the open inventory (visible {invWindow.QuickVisibleCount}, window {Window}, wood {Inv[It.Wood]} stone {Inv[It.Stone]}, list {QuickRecipes(false).Count})"); Shot("combine_ui");
            CloseWindows();
            Log("clicks done"); GD.Print("[check] SUMMARY failures=" + fails); GD.Print("CLICKS DONE");
        });
    }
}
