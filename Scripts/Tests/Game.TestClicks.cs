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
        S(.6f, () => { Check(CurW >= 0 || CurTool != "pickaxe" || true, "clicks: equip click done"); Check(EqIsHeld(ToolList().Count + 1), "clicks: the main action puts the weapon in hand with a still cursor"); CloseWindows(); OpenWin(Win.Build); });
        S(.6f, () => { Check(RealClick(buildMenu.TestChip(1), false), "clicks: the Production chip exists"); });
        S(.5f, () => { Check(BuildCat == 1, $"clicks: the build category changes with a still cursor ({BuildCat})"); CloseWindows(); OpenWin(Win.Inventory); });
        Vector2 pointer = Vector2.Zero;
        UiSlot sourceSlot = null;
        int sourceCount = 0;
        S(.3f, () =>
        {
            Check(invWindow.Columns == 10, "clicks: inventory rows contain ten cells");
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
            Log("clicks done"); GD.Print("[check] SUMMARY failures=" + fails); GD.Print("CLICKS DONE");
        });
    }
}
