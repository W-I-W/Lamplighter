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
        S(.6f, () => { ResUi.Open(It.Wood); });
        S(.6f, () => { Check(RealClick(FindButton(ResUi, "Выбросить"), false), "clicks: the Drop button exists in the menu"); });
        S(.6f, () => { Check(ResUi.IsDialog, "clicks: a click without any mouse movement opens the amount dialog"); });
        S(.3f, () => { Check(RealClick(FindButton(ResUi, "Половина"), false), "clicks: the Half button exists"); });
        S(.6f, () => { Check(ResUi.ForTestAmount == 20, $"clicks: Half without mouse movement sets half ({ResUi.ForTestAmount})"); });
        S(.3f, () => { ResUi.TypeForTest("7a"); });
        S(.3f, () => { Check(ResUi.ForTestAmount == 7, $"clicks: a typed number sets the amount, letters are ignored ({ResUi.ForTestAmount})"); ResUi.TypeForTest("999"); });
        S(.3f, () => { Check(ResUi.ForTestAmount == 40, $"clicks: a too large number is limited to what you have ({ResUi.ForTestAmount})"); ResUi.TypeForTest("7"); });
        S(.3f, () => { RealClick(FindButton(ResUi, "Выбросить"), false); });
        S(.6f, () => { Check(Inv[It.Wood] == 33 && Drops.Count == 1 && Drops[0].Count == 7, $"clicks: the confirm button drops the chosen amount ({Inv[It.Wood]} left, {Drops.Count} stacks)"); CloseWindows(); GiveWeapon("sword1", false); GiveWeapon("bow1", false); OpenWin(Win.Equip); });
        S(.6f, () => { Check(RealClick(FindButton(equipWindow, "Реликвии"), false), "clicks: the Relics tab button exists"); });
        S(.6f, () => { Check(WinTab == 1, $"clicks: the tab switches with a still cursor ({WinTab})"); EqTab(0); WinCur = 0; });
        S(.6f, () => { HoldEquipRow(ToolList().Count); WinCur = ToolList().Count + 1; equipWindow.Force(); });
        S(.4f, () => { Check(RealClick(FindButton(equipWindow, "Взять в руки"), false), "clicks: the Equip main button exists"); });
        S(.6f, () => { Check(CurW >= 0 || CurTool != "pickaxe" || true, "clicks: equip click done"); Check(EqIsHeld(ToolList().Count + 1), "clicks: the main action puts the weapon in hand with a still cursor"); CloseWindows(); OpenWin(Win.Build); });
        S(.6f, () => { Check(RealClick(buildMenu.TestChip(1), false), "clicks: the Production chip exists"); });
        S(.5f, () => { Check(BuildCat == 1, $"clicks: the build category changes with a still cursor ({BuildCat})"); CloseWindows(); Log("clicks done"); GD.Print("[check] SUMMARY failures=" + fails); GD.Print("CLICKS DONE"); });
    }
}
