using Godot;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Инвентарь (I): бесконечный список ресурсов, у которых количество больше нуля. Нулевые ресурсы скрыты, остальные сдвигаются без пустых промежутков.
/// Сцена Scenes/UI/InventoryWindow.tscn; обновляются только изменившиеся ячейки.</summary>
public partial class InventoryWindow : Control
{
    public Game G;
    Control panel; GridContainer grid; Label nameL, desc; readonly List<UiSlot> slots = new();
    readonly List<int> shown = new(); int[] shownCnt = System.Array.Empty<int>(); int shownSel = -1; string lastDesc = "";

    public override void _Ready()
    {
        panel = GetNode<Control>("%Panel"); grid = GetNode<GridContainer>("%Grid"); nameL = GetNode<Label>("%Name"); desc = GetNode<Label>("%Desc");
    }

    public bool IsOver(Vector2 p) => Visible && panel.GetGlobalRect().HasPoint(p);

    UiSlot Slot(int i)
    {
        while (slots.Count <= i)
        {
            var s = GD.Load<PackedScene>("res://Scenes/UI/Slot.tscn").Instantiate<UiSlot>(); int idx = slots.Count; s.Index = idx;
            s.Clicked += i2 => G.WinCur = i2; s.RightClicked += i2 => { if (i2 < shown.Count && G.ResUi != null && G.ResUi.HasActions(shown[i2])) { G.WinCur = i2; G.ResUi.Open(shown[i2]); } };
            grid.AddChild(s); slots.Add(s);
        }
        return slots[i];
    }

    public override void _Process(double delta)
    {
        if (G == null || G.W == null) return;
        bool show = G.Window == Win.Inventory; Visible = show; if (!show) return;
        var vis = G.InvVisible(); G.WinCur = Mathf.Clamp(G.WinCur, 0, Mathf.Max(0, vis.Count - 1));
        bool listChanged = vis.Count != shown.Count; if (!listChanged) for (int i = 0; i < vis.Count; i++) if (vis[i] != shown[i]) { listChanged = true; break; }
        if (listChanged) { shown.Clear(); shown.AddRange(vis); shownCnt = new int[vis.Count]; shownSel = -1; for (int i = vis.Count; i < slots.Count; i++) slots[i].Visible = false; }
        for (int i = 0; i < vis.Count; i++)
        {
            int it = vis[i], n = G.Inv[it]; bool sel = i == G.WinCur;
            if (!listChanged && shownCnt[i] == n && !(sel != (i == shownSel))) continue;       // ячейка не изменилась
            var s = Slot(i); s.Visible = true; shownCnt[i] = n;
            s.Set(UiIcons.Item(it), n >= 10000 ? $"{n / 1000}k" : n.ToString(), "", sel ? SlotState.Selected : SlotState.Normal, 0, "", n > 0 ? 1f : .45f);
        }
        shownSel = G.WinCur;
        if (vis.Count == 0) { nameL.Text = Loc.T("Инвентарь пуст"); desc.Text = Loc.T("Добывайте ресурсы: они появятся здесь"); return; }
        int c = vis[G.WinCur];
        nameL.Text = $"{It.Ic(c)} {Loc.T(It.Names[c])} ×{G.Inv[c]}";
        string d = Game.Tradable(c) ? string.Format(Loc.T("Цена продажи: {0} · цена покупки: {1}"), G.SellPrice(c), G.BuyPrice(c)) : Loc.T("Не продаётся");
        if (G.Stock[c] > 0) d += "\n" + string.Format(Loc.T("На складе: {0}"), G.Stock[c]);
        d += "\n" + Loc.T("ПКМ — действия");
        if (d != lastDesc) { lastDesc = d; desc.Text = d; }
    }
}
