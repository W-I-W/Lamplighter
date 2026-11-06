using Godot;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Инвентарь (I): бесконечный список ресурсов, у которых количество больше нуля. Нулевые ресурсы скрыты, остальные сдвигаются без пустых промежутков.
/// Сцена Scenes/UI/InventoryWindow.tscn; обновляются только изменившиеся ячейки.</summary>
public partial class InventoryWindow : Control
{
    public Game G;
    Control panel; GridContainer grid; Label nameL, desc, slotsL; readonly List<UiSlot> slots = new();
    readonly List<(int item, int n)> shown = new(); int shownSel = -1, shownFree = -1; string lastDesc = "";

    public override void _Ready()
    {
        slotsL = new Label { ThemeTypeVariation = "DimLabel" }; GetNode<Control>("%Panel/V/Head").AddChild(slotsL); GetNode<Control>("%Panel/V/Head").MoveChild(slotsL, 1);
        panel = GetNode<Control>("%Panel"); grid = GetNode<GridContainer>("%Grid"); nameL = GetNode<Label>("%Name"); desc = GetNode<Label>("%Desc");
    }

    public bool IsOver(Vector2 p) => Visible && panel.GetGlobalRect().HasPoint(p);

    UiSlot Slot(int i)
    {
        while (slots.Count <= i)
        {
            var s = GD.Load<PackedScene>("res://Scenes/UI/Slot.tscn").Instantiate<UiSlot>(); int idx = slots.Count; s.Index = idx;
            s.Clicked += i2 => G.WinCur = i2; s.RightClicked += i2 => { if (i2 < shown.Count && G.ResUi != null && G.ResUi.HasActions(shown[i2].item)) { G.WinCur = i2; G.ResUi.Open(shown[i2].item); } };
            grid.AddChild(s); slots.Add(s);
        }
        return slots[i];
    }

    public override void _Process(double delta)
    {
        if (G == null || G.W == null) return;
        bool show = G.Window == Win.Inventory; Visible = show; if (!show) return;
        var vis = G.InvStacks(); int free = Mathf.Max(0, G.SlotCap - G.SlotsUsed()), total = vis.Count + free; G.WinCur = Mathf.Clamp(G.WinCur, 0, Mathf.Max(0, vis.Count - 1));
        bool listChanged = vis.Count != shown.Count || free != shownFree; if (!listChanged) for (int i = 0; i < vis.Count; i++) if (vis[i] != shown[i]) { listChanged = true; break; }
        if (listChanged) { shown.Clear(); shown.AddRange(vis); shownFree = free; shownSel = -1; for (int i = total; i < slots.Count; i++) slots[i].Visible = false; }
        for (int i = 0; i < total; i++)
        {
            if (i >= vis.Count) { if (listChanged) Slot(i).Set(null, "", "", SlotState.Normal, 0, "", .3f); if (listChanged) slots[i].Visible = true; continue; }       // пустой слот
            var (it, n) = vis[i]; bool sel = i == G.WinCur;
            if (!listChanged && !(sel != (i == shownSel))) continue;       // ячейка не изменилась
            var s = Slot(i); s.Visible = true;
            s.Set(UiIcons.Item(it), n.ToString(), "", sel ? SlotState.Selected : SlotState.Normal, 0, "", n > 0 ? 1f : .45f);
        }
        shownSel = G.WinCur;
        string su = string.Format(Loc.T("Слоты {0}/{1}"), G.SlotsUsed(), G.SlotCap); if (slotsL.Text != su) slotsL.Text = su;
        if (vis.Count == 0) { nameL.Text = Loc.T("Инвентарь пуст"); desc.Text = Loc.T("Добывайте ресурсы: они появятся здесь"); return; }
        int c = vis[G.WinCur].item;
        nameL.Text = $"{It.Ic(c)} {Loc.T(It.Names[c])} ×{G.Inv[c]}";
        string d = Game.Tradable(c) ? string.Format(Loc.T("Цена продажи: {0} · цена покупки: {1}"), G.SellPrice(c), G.BuyPrice(c)) : Loc.T("Не продаётся");
        if (G.Stock[c] > 0) d += "\n" + string.Format(Loc.T("На складе: {0}"), G.Stock[c]);
        d += "\n" + Loc.T("ПКМ — действия");
        if (d != lastDesc) { lastDesc = d; desc.Text = d; }
    }
}
