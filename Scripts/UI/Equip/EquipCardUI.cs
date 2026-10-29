using Godot;
using System;

namespace Lamplighter;

/// <summary>Карточка предмета в списке экипировки: иконка, название, строка типа и редкости, метка «Надето» и номер слота хотбара. Выбранная карточка — в золотой рамке.
/// Один щелчок выбирает, двойной — выполняет главное действие (надеть, создать). Стили — Resources/UI/UpgradeTheme.tres, сцена — Scenes/UI/EquipCard.tscn.</summary>
public partial class EquipCardUI : PanelContainer
{
    public int Row = -1;
    public event Action<EquipCardUI> Picked, Activated;
    static readonly Color Gold = new("#ffd23f"), Dim = new("#8a96b0");
    TextureRect icon; Label nameL, subL, tagL, slotL; Tween tw;
    bool selected, hover, bound; string variation = "";

    public override void _Ready()
    {
        icon = GetNode<TextureRect>("%Icon"); nameL = GetNode<Label>("%Name"); subL = GetNode<Label>("%Sub"); tagL = GetNode<Label>("%Tag"); slotL = GetNode<Label>("%Slot");
        MouseFilter = MouseFilterEnum.Stop; PivotOffset = Size / 2;
        MouseEntered += () => { hover = true; Tint(); }; MouseExited += () => { hover = false; Tint(); };
        GuiInput += e =>
        {
            if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left) { if (mb.DoubleClick) Activated?.Invoke(this); else Picked?.Invoke(this); }
        };
    }

    /// <param name="equipped">показывает метку «Надето»; <paramref name="hotSlot"/> — номер слота хотбара (0 — нет); <paramref name="dim"/> — приглушённая карточка (недоступно).</param>
    public void Bind(int row, Texture2D ic, string name, Color nameCol, string sub, Color subCol, bool equipped, int hotSlot, bool sel, bool dim = false, Color? iconTint = null)
    {
        bool newRow = !bound || row != Row; Row = row;
        icon.Texture = ic; icon.Modulate = (iconTint ?? Colors.White) * (dim ? new Color(1, 1, 1, .55f) : Colors.White);
        if (nameL.Text != name) nameL.Text = name; nameL.AddThemeColorOverride("font_color", dim ? Dim : nameCol);
        subL.Visible = !string.IsNullOrEmpty(sub); if (subL.Text != sub) subL.Text = sub ?? ""; subL.AddThemeColorOverride("font_color", subCol);
        tagL.Visible = equipped; slotL.Visible = hotSlot > 0; slotL.Text = hotSlot > 0 ? $"[{hotSlot}]" : "";
        bool becameSel = sel && !selected && bound; selected = sel; bound = true;
        string v = sel ? "CardSelected" : equipped ? "CardOwned" : dim ? "CardLocked" : "CardNormal";
        if (v != variation) { variation = v; ThemeTypeVariation = v; }
        Tint(); if (becameSel) Pulse(new Color(1.5f, 1.4f, 1f), .16f);
    }

    Color Rest() => hover ? new Color(1.2f, 1.2f, 1.2f) : Colors.White;
    void Tint() { tw?.Kill(); tw = CreateTween(); tw.TweenProperty(this, "modulate", Rest(), .08f); }
    public void Pulse(Color from, float time) { tw?.Kill(); Modulate = from; tw = CreateTween(); tw.TweenProperty(this, "modulate", Rest(), time); }
}
