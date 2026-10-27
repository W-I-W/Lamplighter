using Godot;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Меню строительства (B): категории, сетка иконок, название, цена, доступность и подсказка по размещению. Сцена Scenes/UI/BuildMenu.tscn.
/// Управление: стрелки/Tab/Enter с клавиатуры либо мышь (клик по слоту выбирает, повторный клик строит).</summary>
public partial class BuildMenu : Control
{
    public Game G;
    Control panel; GridContainer grid; Label nameL, hint, status, cost, place; TextureRect preview; readonly Button[] tabs = new Button[4]; readonly List<UiSlot> slots = new();

    public override void _Ready()
    {
        panel = GetNode<Control>("%Panel"); grid = GetNode<GridContainer>("%Grid"); nameL = GetNode<Label>("%Name"); hint = GetNode<Label>("%Hint"); status = GetNode<Label>("%Status");
        cost = GetNode<Label>("%Cost"); place = GetNode<Label>("%Place"); preview = GetNode<TextureRect>("%Preview");
        for (int i = 0; i < 4; i++) { int ci = i; tabs[i] = GetNode<Button>($"%Tab{i}"); tabs[i].Pressed += () => { G.BuildCat = ci; G.WinCur = 0; }; }
        var slotScene = GD.Load<PackedScene>("res://Scenes/UI/Slot.tscn");
        for (int i = 0; i < 24; i++) { var s = slotScene.Instantiate<UiSlot>(); s.Index = i; s.Clicked += OnSlot; grid.AddChild(s); slots.Add(s); }
    }

    void OnSlot(int i)
    {
        if (G.WinCur == i) G.PressWindowKey(Key.Enter); else G.WinCur = i;
    }

    public bool IsOver(Vector2 p) => Visible && panel.GetGlobalRect().HasPoint(p);

    public override void _Process(double delta)
    {
        if (G == null || G.W == null) return;
        bool show = G.Window == Win.Build; Visible = show; if (!show) return;
        var list = G.BuildEntriesFiltered();
        if (G.WinCur >= list.Count) G.WinCur = Mathf.Max(0, list.Count - 1);
        for (int i = 0; i < tabs.Length; i++) { tabs[i].Text = Loc.T(Game.BuildCatNames[i]); tabs[i].ThemeTypeVariation = i == G.BuildCat ? "TabSelected" : "Tab"; }
        for (int i = 0; i < slots.Count; i++)
        {
            bool has = i < list.Count; slots[i].Visible = has; if (!has) continue; var e = list[i];
            var st = i == G.WinCur ? SlotState.Selected : !e.Unlocked ? SlotState.Disabled : e.Affordable ? SlotState.Ok : SlotState.Normal;
            slots[i].Set(UiIcons.Tile(e.Tile), "", "", st, 0, "", e.Unlocked ? 1f : .35f);
        }
        if (list.Count == 0) { nameL.Text = ""; return; }
        var s = list[G.WinCur]; nameL.Text = Loc.T(s.Name); preview.Texture = UiIcons.Tile(s.Tile); preview.Modulate = new Color(1, 1, 1, s.Unlocked ? 1f : .4f);
        hint.Text = Loc.T(s.Hint);
        if (!s.Unlocked) { status.Text = Loc.T("Закрыто: ") + Loc.T(s.Lock); status.AddThemeColorOverride("font_color", new Color("#ffb36a")); }
        else if (!s.Affordable) { status.Text = Loc.T("Не хватает: ") + Loc.T(G.MissingText(s.CostArr)); status.AddThemeColorOverride("font_color", new Color("#ffb36a")); }
        else { status.Text = Loc.T("Можно строить"); status.AddThemeColorOverride("font_color", new Color("#9be07a")); }
        cost.Text = s.CostArr == null ? "" : Loc.T("Цена: ") + G.NeedShortText(s.CostArr);
        place.Text = Loc.T("Enter — выбрать, затем курсор на свободную клетку рядом: она подсветится зелёным");
    }
}
