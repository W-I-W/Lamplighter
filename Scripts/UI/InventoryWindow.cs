using Godot;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Инвентарь (I): полный список ресурсов с количеством. Сцена Scenes/UI/InventoryWindow.tscn.</summary>
public partial class InventoryWindow : Control
{
    public Game G;
    Control panel; GridContainer grid; Label nameL, desc; readonly List<UiSlot> slots = new();

    public override void _Ready()
    {
        panel = GetNode<Control>("%Panel"); grid = GetNode<GridContainer>("%Grid"); nameL = GetNode<Label>("%Name"); desc = GetNode<Label>("%Desc");
        var slotScene = GD.Load<PackedScene>("res://Scenes/UI/Slot.tscn");
        for (int i = 0; i < It.Count; i++) { var s = slotScene.Instantiate<UiSlot>(); s.Index = i; s.Clicked += i2 => G.WinCur = i2; grid.AddChild(s); slots.Add(s); }
    }

    public bool IsOver(Vector2 p) => Visible && panel.GetGlobalRect().HasPoint(p);

    public override void _Process(double delta)
    {
        if (G == null || G.W == null) return;
        bool show = G.Window == Win.Inventory; Visible = show; if (!show) return;
        for (int i = 0; i < It.Count; i++)
        {
            int n = G.Inv[i]; slots[i].Set(UiIcons.Item(i), n > 0 ? (n >= 10000 ? $"{n / 1000}k" : n.ToString()) : "", "", i == G.WinCur ? SlotState.Selected : SlotState.Normal, 0, "", n > 0 ? 1f : .3f);
        }
        int c = Mathf.Clamp(G.WinCur, 0, It.Count - 1);
        nameL.Text = $"{It.Ic(c)} {Loc.T(It.Names[c])} ×{G.Inv[c]}";
        string d = Game.Tradable(c) ? string.Format(Loc.T("Цена продажи: {0} · цена покупки: {1}"), G.SellPrice(c), G.BuyPrice(c)) : Loc.T("Не продаётся");
        if (G.Stock[c] > 0) d += "\n" + string.Format(Loc.T("На складе: {0}"), G.Stock[c]);
        desc.Text = d;
    }
}
