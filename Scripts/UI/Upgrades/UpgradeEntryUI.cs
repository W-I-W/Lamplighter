using Godot;
using System;

namespace Lamplighter;

/// <summary>Карточка улучшения в списке: иконка, название и индикатор состояния. Стили и размеры — в сцене UpgradeEntry.tscn и теме UpgradeTheme.tres;
/// здесь только состояния (изучено / доступно / не хватает / закрыто), выбор и короткие анимации.</summary>
public partial class UpgradeEntryUI : PanelContainer
{
    public int Tech = -1;
    public event Action<UpgradeEntryUI> Picked;

    static readonly Color Green = new("#9be07a"), Gold = new("#ffd23f"), Red = new("#f06a5e"), Grey = new("#6a7288"), TextCol = new("#d8e0f0");
    static Texture2D check, lockTex, dot;
    TextureRect icon, stateIcon; Label nameL; Tween tw;
    TechState state = TechState.Locked; bool selected, hover, bound;

    public override void _Ready()
    {
        icon = GetNode<TextureRect>("%Icon"); stateIcon = GetNode<TextureRect>("%State"); nameL = GetNode<Label>("%Name");
        check ??= GD.Load<Texture2D>("res://Assets/UI/Upgrades/state_check.png"); lockTex ??= GD.Load<Texture2D>("res://Assets/UI/Upgrades/state_lock.png"); dot ??= GD.Load<Texture2D>("res://Assets/UI/Upgrades/state_dot.png");
        MouseFilter = MouseFilterEnum.Stop; PivotOffset = Size / 2;
        MouseEntered += () => { hover = true; Tint(); };
        MouseExited += () => { hover = false; Tint(); };
        GuiInput += e => { if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left) Picked?.Invoke(this); };
    }

    /// <summary>Заполняет карточку; перерисовывается только при изменении данных.</summary>
    public void Bind(int tech, TechState st, bool sel, string title, Texture2D ic)
    {
        bool changed = !bound || tech != Tech || st != state || sel != selected;
        if (tech != Tech || !bound) { nameL.Text = title; icon.Texture = ic; }
        bool becameSelected = sel && !selected && bound;
        Tech = tech; state = st; selected = sel; bound = true;
        if (!changed) return;
        ThemeTypeVariation = sel ? "CardSelected" : st switch { TechState.Owned => "CardOwned", TechState.Available => "CardAvail", TechState.Missing => "CardMissing", _ => "CardLocked" };
        stateIcon.Texture = st == TechState.Owned ? check : st == TechState.Locked ? lockTex : dot;
        stateIcon.Modulate = st == TechState.Owned ? Green : st == TechState.Available ? Gold : st == TechState.Missing ? Red : Grey;
        nameL.AddThemeColorOverride("font_color", st == TechState.Owned ? Green : st == TechState.Locked ? Grey : TextCol);
        Tint();
        if (becameSelected) Pulse(new Color(1.5f, 1.4f, 1f), .18f);
    }

    Color RestColor() { float b = hover ? 1.22f : 1f; float d = state == TechState.Locked && !selected ? .72f : 1f; return new Color(b * d, b * d, b * d, 1f); }

    /// <summary>Базовая яркость: закрытые карточки слегка затемнены, при наведении — светлее.</summary>
    void Tint()
    {
        icon.Modulate = state == TechState.Locked ? new Color(.55f, .6f, .7f) : state == TechState.Missing ? new Color(.9f, .85f, .85f) : Colors.White;
        tw?.Kill(); tw = CreateTween(); tw.TweenProperty(this, "modulate", RestColor(), .08f);
    }

    /// <summary>Короткая вспышка: выбор карточки и успешное изучение.</summary>
    public void Pulse(Color from, float time)
    {
        tw?.Kill(); Modulate = from; tw = CreateTween(); tw.TweenProperty(this, "modulate", RestColor(), time);
    }

    public void LearnFx(Control fxParent)
    {
        Pulse(new Color(2f, 1.8f, .8f), .5f);
        if (fxParent != null) UiFx.Burst(fxParent, UiFx.LocalPoint(fxParent, this, new Vector2(14, Size.Y / 2)), Gold, 14, 24);
    }
}
