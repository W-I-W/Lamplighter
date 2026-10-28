using Godot;

namespace Lamplighter;

/// <summary>Перетаскивание предметов мышью: из окна экипировки (вкладка «Оружие») и между слотами хотбара. Короткий клик без движения — прежнее действие
/// (слот хотбара выбирается, строка окна — «надеть»).</summary>
public partial class Game
{
    string dragRef; int dragFrom = -1, dragRow; Vector2 dragStart; bool dragCand;
    public bool DragActive; public Vector2 DragPos;
    public int DragIcon => DragActive ? HotIcon(dragRef) : -1;

    bool HotDragInput(InputEvent e)
    {
        if (FinStage != 0 || Placing != null || GameHudNode == null || Menu != Mn.None) { if (dragCand) { dragCand = false; DragActive = false; } return false; }
        if (e is InputEventMouseMotion mm)
        {
            if (!dragCand) return false;
            var p = mm.Position / UiScaleVp; DragPos = p;
            if (!DragActive && p.DistanceTo(dragStart) > 5f) DragActive = true;
            return DragActive;
        }
        if (e is not InputEventMouseButton mb || mb.ButtonIndex != MouseButton.Left) return false;
        var q = mb.Position / UiScaleVp;
        if (mb.Pressed)
        {
            string r = null; int from = -1, row = -1; int s = GameHudNode.HotSlotAt(q);
            if (s >= 0 && Hot[s] != null) { r = Hot[s]; from = s; }
            else if (Window == Win.Equip && WinTab == 0 && hud != null) { row = hud.RowHit(q); if (row >= 0) r = EquipRef(row); }
            if (r == null) return false;
            dragRef = r; dragFrom = from; dragRow = row; dragStart = q; DragPos = q; dragCand = true; DragActive = false; OverUi = true;
            return true;
        }
        if (!dragCand) return false;
        dragCand = false; bool was = DragActive; DragActive = false;
        if (was) { int t = GameHudNode.HotSlotAt(q); if (t >= 0) HotAssign(t, dragRef); return true; }
        if (dragFrom >= 0) HotSelect(dragFrom); else if (dragRow >= 0) { WinCur = dragRow; WindowKey(Key.Enter, false); }
        return true;
    }
}
