using Godot;
using System;
using System.Text;

namespace Lamplighter;

/// <summary>Панель подробностей выбранного навыка (справа): иконка, название, описание, уровень в виде ряда, текущий и следующий эффект, общий бонус ступеней, стоимость следующего уровня,
/// требования и кнопка «Улучшить до N/M» (на максимуме — «МАКСИМАЛЬНЫЙ УРОВЕНЬ»). Пересобирается, только когда меняется состояние (подпись).</summary>
public partial class SkillDetailsUI : PanelContainer
{
    public event Action LearnPressed;
    static readonly Color Green = new("#9be07a"), Gold = new("#ffd23f"), Red = new("#f06a5e"), Grey = new("#8a96b0"), TextCol = new("#d8e0f0"), Cyan = new("#8ff0ff");
    VBoxContainer box, foot; string sig = "";
    public Button LearnButton { get; private set; }
    public string LearnText => LearnButton?.Text ?? "";
    public bool LearnEnabled => LearnButton != null && !LearnButton.Disabled && LearnButton.Visible;

    public override void _Ready() { box = GetNode<VBoxContainer>("%Box"); foot = GetNode<VBoxContainer>("%Foot"); }

    Label L(string t, Color? c = null, bool wrap = true, string variation = null, bool raw = false)
    {
        var l = new Label { Text = raw ? t : Loc.T(t), MouseFilter = MouseFilterEnum.Ignore }; if (variation != null) l.ThemeTypeVariation = variation; if (c.HasValue) l.AddThemeColorOverride("font_color", c.Value);
        if (wrap) { l.AutowrapMode = TextServer.AutowrapMode.WordSmart; l.SizeFlagsHorizontal = SizeFlags.ExpandFill; l.CustomMinimumSize = new Vector2(60, 0); } return l;
    }

    static string Roman(int i) => i switch { 1 => "I", 2 => "II", 3 => "III", _ => i.ToString() };

    public void Show(Game g, int tech)
    {
        var sb = new StringBuilder(); sb.Append(tech).Append('|').Append(tech >= 0 ? g.SkillLv[tech] : 0).Append('|').Append(Loc.Lang);
        if (tech >= 0) { sb.Append('|').Append((int)g.StateOfTech(tech)).Append('|').Append(g.MissingParents(tech)); var c = Game.LevelCost(tech, g.SkillLv[tech] + 1); foreach (var x in c) sb.Append('|').Append(g.Inv[x[0]] >= x[1]).Append(x[1]); if (Game.Techs[tech].Ok != null) sb.Append(Game.Techs[tech].Ok(g)); }
        if (sb.ToString() == sig) return; sig = sb.ToString();
        foreach (var n in box.GetChildren()) { box.RemoveChild(n); n.QueueFree(); } foreach (var n in foot.GetChildren()) { foot.RemoveChild(n); n.QueueFree(); } LearnButton = null;
        if (tech < 0) { box.AddChild(L("Выберите навык в дереве", Grey)); return; }
        var t = Game.Techs[tech]; var d = Game.SkillOf(tech); int lv = g.SkillLv[tech], mx = Game.SkillMax(tech); var st = g.StateOfTech(tech);
        var head = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; head.AddThemeConstantOverride("separation", 6); box.AddChild(head);
        head.AddChild(new TextureRect { Texture = UpgradeIcons.ForTech(tech), CustomMinimumSize = new Vector2(64, 64), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, TextureFilter = TextureFilterEnum.Nearest, SizeFlagsVertical = SizeFlags.ShrinkBegin, MouseFilter = MouseFilterEnum.Ignore });
        var tt = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore }; head.AddChild(tt);
        tt.AddChild(L(t.Name, lv >= mx ? Gold : TextCol, true, "GoldLabel"));
        tt.AddChild(L($"{Loc.T("Уровень")} {lv}/{mx}", lv >= mx ? Gold : Cyan, false, null, true));
        var pips = new SkillPipsUI { CustomMinimumSize = new Vector2(0, 8), MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ExpandFill }; pips.Set(mx, lv, lv >= mx ? Gold : Cyan); tt.AddChild(pips);
        box.AddChild(L(t.Desc, TextCol));

        if (!string.IsNullOrEmpty(d.Unit))
        {
            box.AddChild(L(d.EffLabel, Grey, true, "DimLabel"));
            box.AddChild(Row("Сейчас", lv > 0 ? d.EffText(lv) : "—", lv > 0 ? Gold : Grey));
            if (lv < mx) { box.AddChild(Row("Следующий уровень", d.EffText(lv + 1), Green)); if (d.Unit == "%" || d.Unit == "cut") box.AddChild(Row("Прирост", (d.Unit == "cut" ? "-" : "+") + (Mathf.Abs(d.EffPer) * 100f).ToString("0.#") + "%", Green)); }
            string group = GroupBonus(g, tech); if (group != "") box.AddChild(Row("Все ступени", group, Gold));
        }

        if (lv >= mx) { foot.AddChild(L("МАКСИМАЛЬНЫЙ УРОВЕНЬ", Gold, true, "GoldLabel")); }
        else
        {
            var cost = Game.LevelCost(tech, lv + 1);
            if (cost.Length > 0) { box.AddChild(L("Стоимость следующего уровня", Grey, true, "DimLabel")); var wrapRow = new HFlowContainer { MouseFilter = MouseFilterEnum.Ignore }; wrapRow.AddThemeConstantOverride("h_separation", 6); box.AddChild(wrapRow); foreach (var c in cost) { bool ok = g.Inv[c[0]] >= c[1]; wrapRow.AddChild(Chip(c[0], $"{g.Inv[c[0]]}/{c[1]}", ok ? Green : Red)); } }
            string miss = g.MissingParents(tech);
            if (miss != "") { box.AddChild(L("Требуется", Grey, true, "DimLabel")); box.AddChild(L(miss, Red, true, null, true)); }
            if (lv == 0 && !string.IsNullOrEmpty(t.Req)) { bool ok = t.Ok != null && t.Ok(g); box.AddChild(L((ok ? "✓ " : "") + Loc.T(t.Req), ok ? Green : Grey, true, null, true)); }
            if (lv == 0 && t.Reward) box.AddChild(L("Это награда: её даёт победа над стражем", Grey));
            string label = Loc.T("Улучшить до") + $" {lv + 1}/{mx}"; if (lv == 0 && mx == 1) label = Loc.T("Изучить");
            LearnButton = new Button { Text = label, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(0, 22), Disabled = st != TechState.Available };
            if (st == TechState.Available) LearnButton.AddThemeColorOverride("font_color", Gold); LearnButton.Pressed += () => LearnPressed?.Invoke(); foot.AddChild(LearnButton);
        }
    }

    /// <summary>Общий бонус связанных ступеней (первая + вторая), когда у навыка есть вторая ступень.</summary>
    static string GroupBonus(Game g, int tech)
    {
        int a = -1, b = -1; var t = (Tech)tech;
        if (t == Tech.Lens || t == Tech.LensII) { a = (int)Tech.Lens; b = (int)Tech.LensII; } else if (t == Tech.FarLanterns || t == Tech.FarLanternsII) { a = (int)Tech.FarLanterns; b = (int)Tech.FarLanternsII; }
        if (a < 0) return ""; float v = Game.SkillOf(a).Eff(g.SkillLv[a]) + Game.SkillOf(b).Eff(g.SkillLv[b]); float mx = Game.SkillOf(a).Eff(Game.SkillMax(a)) + Game.SkillOf(b).Eff(Game.SkillMax(b));
        return $"+{v * 100f:0.#}% / +{mx * 100f:0.#}%";
    }

    Control Row(string name, string value, Color col)
    {
        var h = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; var n = L(name, Grey, false); n.SizeFlagsHorizontal = SizeFlags.ExpandFill; h.AddChild(n); h.AddChild(L(value, col, false, null, true)); return h;
    }

    Control Chip(int item, string text, Color col)
    {
        var h = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; h.AddThemeConstantOverride("separation", 2);
        h.AddChild(new TextureRect { Texture = UiIcons.Item(item), CustomMinimumSize = new Vector2(12, 12), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, TextureFilter = TextureFilterEnum.Nearest, SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore, TooltipText = Loc.T(It.Names[item]) });
        h.AddChild(L(text, col, false, null, true)); return h;
    }

    public void ClickLearn() => LearnPressed?.Invoke();
}
