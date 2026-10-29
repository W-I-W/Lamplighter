using Godot;

namespace Lamplighter;

/// <summary>Узел дерева навыков: рамка по состоянию, иконка 32×32, ряд уровней и «3/5»; под узлом (при приближении) название. Мышью управляет холст дерева — сам узел клики не принимает.</summary>
public partial class SkillNodeUI : Control
{
    public const float W = 48, H = 56;
    public int Tech = -1; public Vector2 Grid;
    static readonly Color Green = new("#9be07a"), Gold = new("#ffd23f"), Red = new("#f06a5e"), Grey = new("#6a7288"), TextCol = new("#d8e0f0");
    PanelContainer frame; TextureRect icon; SkillPipsUI pips; Label lvl, nameL; Tween tw;
    bool hover, bound, selected; TechState state; int lastLv = -1;

    public override void _Ready()
    {
        frame = GetNode<PanelContainer>("%Frame"); icon = GetNode<TextureRect>("%Icon"); pips = GetNode<SkillPipsUI>("%Pips"); lvl = GetNode<Label>("%Lvl"); nameL = GetNode<Label>("%Name");
        MouseFilter = MouseFilterEnum.Ignore; Size = new Vector2(W, H); CustomMinimumSize = Size; PivotOffset = Size / 2;
    }

    public Rect2 Rect => new(Position, Size);

    public void Bind(int tech, int lv, int max, TechState st, bool sel, Texture2D ic, string title, bool showName)
    {
        Tech = tech; bool newLevel = bound && lv != lastLv; lastLv = lv;
        if (icon.Texture != ic) icon.Texture = ic;
        pips.Set(max, lv, lv >= max ? Gold : new Color("#8ff0ff")); lvl.Text = $"{lv}/{max}";
        lvl.AddThemeColorOverride("font_color", lv >= max ? Gold : lv > 0 ? TextCol : Grey);
        nameL.Visible = showName; if (nameL.Text != title) nameL.Text = title;
        nameL.AddThemeColorOverride("font_color", sel ? Gold : st == TechState.Locked ? Grey : TextCol);
        bool changed = !bound || st != state || sel != selected; state = st; selected = sel; bound = true;
        if (changed)
        {
            frame.ThemeTypeVariation = sel ? "CardSelected" : st switch { TechState.Owned => "CardOwned", TechState.Available => "CardAvail", TechState.Missing => "CardMissing", _ => lv > 0 ? "CardAvail" : "CardLocked" };
            icon.Modulate = st == TechState.Locked && lv == 0 ? new Color(.45f, .5f, .6f) : st == TechState.Missing && lv == 0 ? new Color(.85f, .78f, .78f) : Colors.White;
            Tint();
        }
        if (newLevel) Pulse(new Color(2f, 1.8f, .8f), .45f);
    }

    public void SetHover(bool h) { if (hover == h) return; hover = h; Tint(); }
    Color Rest() { float b = hover ? 1.18f : 1f; return new Color(b, b, b, 1f); }
    void Tint() { tw?.Kill(); tw = CreateTween(); tw.TweenProperty(this, "modulate", Rest(), .08f); }
    /// <summary>Короткая золотая вспышка: покупка уровня, открытие новой ступени.</summary>
    public void Pulse(Color from, float time) { tw?.Kill(); Modulate = from; tw = CreateTween(); tw.TweenProperty(this, "modulate", Rest(), time); }
}
