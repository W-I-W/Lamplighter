using Godot;
using System.Collections.Generic;
using System.Text;

namespace Lamplighter;

/// <summary>Окно улучшений (T): вкладки открытых категорий, слева двумерное дерево навыков (SkillTreeCanvasUI, ~72% ширины), справа подробности (SkillDetailsUI, ~28%).
/// Сцена Scenes/UI/UpgradeWindow.tscn. Окно ничего не решает: категории открывает UpgradeProgressionSystem, уровни покупает Game.LearnTech.
/// Управление: мышь (вкладки, узлы, перетаскивание, колесо, кнопки) и клавиши (стрелки по дереву, Tab/PgUp/PgDn категории, Enter улучшить, Home центр).</summary>
public partial class UpgradeWindowUI : Control
{
    public Game G;
    static readonly Color Gold = new("#ffd23f");
    Control panel; HBoxContainer tabsBox; SkillDetailsUI details; SkillTreeCanvasUI canvas; Label count, title, keys;
    readonly List<Button> tabs = new(); Button btnCenter, btnFit, btnIn, btnOut;
    Texture2D star; bool wasOpen; int shownPage = -1; string tabSig = "", treeSig = "", keysLang = ""; float t; Tween bodyTw, openTw;

    public override void _Ready()
    {
        panel = GetNode<Control>("%Panel"); tabsBox = GetNode<HBoxContainer>("%Tabs"); details = GetNode<SkillDetailsUI>("%Details"); canvas = GetNode<SkillTreeCanvasUI>("%Canvas");
        count = GetNode<Label>("%Count"); title = GetNode<Label>("%Title"); keys = GetNode<Label>("%Keys");
        star = GD.Load<Texture2D>("res://Assets/UI/Upgrades/state_star.png");
        details.LearnPressed += ClickLearn;
        var head = panel.GetNode<HBoxContainer>("V/Head");
        btnCenter = Bind(head, "Center", canvas.CenterOnSelected);
        btnFit = Bind(head, "Fit", canvas.FitAll);
        btnOut = Bind(head, "ZoomOut", () => canvas.Zoom(-1));
        btnIn = Bind(head, "ZoomIn", () => canvas.Zoom(1));
    }

    static Button Bind(Control parent, string name, System.Action action)
    {
        var button = parent.GetNode<Button>(name);
        button.Pressed += action;
        return button;
    }

    // ── действия (те же методы вызывают элементы окна и проверки)
    public void ClickLearn() => G.LearnSelected();
    public void ClickNode(int tech) => canvas.ClickNode(tech);
    public void ClickTab(int i) { if (i >= 0 && i < tabs.Count && tabs[i].Visible && tabs[i].HasMeta("page")) G.SelectTechPage((int)tabs[i].GetMeta("page")); }
    public int TabCount { get { int n = 0; foreach (var b in tabs) if (b.Visible) n++; return n; } }
    public int NodeCount => canvas.NodeCount;
    public int LinkCount => canvas.LinkCount;
    public SkillTreeCanvasUI Canvas => canvas;
    public SkillDetailsUI Details => details;
    public string TabText(int i) => i >= 0 && i < tabs.Count ? tabs[i].Text : "";
    public bool TabBadge(int i) => i >= 0 && i < tabs.Count && tabs[i].GetNode<TextureRect>("Badge").Visible;
    public bool IsOver(Vector2 p) => Visible && panel.GetGlobalRect().HasPoint(p);
    public void ForceRefresh() { treeSig = ""; tabSig = ""; Refresh(); }

    public override void _Process(double delta)
    {
        if (G == null || G.W == null || G.Upgrades == null) return;
        bool show = G.TechOpen && G.Menu == Mn.None; Visible = show;
        if (!show) { wasOpen = false; return; }
        t += (float)delta;
        if (!wasOpen)
        {
            wasOpen = true; shownPage = -1; tabSig = ""; treeSig = ""; panel.PivotOffset = panel.Size / 2; panel.Modulate = new Color(1, 1, 1, 0);
            openTw?.Kill(); openTw = CreateTween(); openTw.TweenProperty(panel, "modulate", Colors.White, .12f);
        }
        Refresh();
    }

    void Refresh()
    {
        canvas.G = G; G.EnsureTechSel(); RefreshTabs();
        var sb = new StringBuilder(); sb.Append(G.TechPage).Append('|').Append(G.TechSel).Append('|').Append(Loc.Lang).Append('|').Append(canvas.ZoomStep).Append('|').Append((int)G.UiScaleVp);
        foreach (var i in Game.PageList(G.TechPage)) sb.Append(G.SkillLv[i]).Append((int)G.StateOfTech(i)).Append(',');
        string sg = sb.ToString(); if (sg != treeSig) { treeSig = sg; canvas.Refresh(); }
        if (shownPage != G.TechPage) { shownPage = G.TechPage; bodyTw?.Kill(); canvas.Modulate = new Color(1, 1, 1, 0); bodyTw = CreateTween(); bodyTw.TweenProperty(canvas, "modulate", Colors.White, .15f); }
        if (G.TechLearned >= 0)
        {
            int i = G.TechLearned; G.TechLearned = -1; canvas.FlashNode(i); UiFx.Burst(canvas, canvas.NodeCentreLocal(i), Gold, 14, 24, .5f);
            if (G.SkillLv[i] >= Game.SkillMax(i)) canvas.FlashChildren(i);
        }
        var (own, total) = G.TechTotals(); count.Text = $"{own}/{total}";
        bool atTable = G.TechAtTable || G.FreeResearch; string kl = Loc.Lang + atTable + G.TableLevel; if (kl != keysLang) { keysLang = kl; keys.Text = Loc.T(atTable ? "ЛКМ выбрать · тащить/СКМ двигать · колесо масштаб · Enter улучшить · Tab категория · Esc закрыть" : "ЛКМ выбрать · колесо масштаб · Tab категория · изучение — у Стола · T/Esc закрыть"); title.Text = G.TechAtTable ? Loc.T("Стол исследований") + $" · {Loc.T("ур.")}{G.TableLevel}" : Loc.T("Навыки: просмотр"); btnCenter.Text = Loc.T("Центр"); btnFit.Text = Loc.T("Всё"); btnOut.Text = "-"; btnIn.Text = "+"; }
        details.Show(G, G.TechSel);
    }

    void RefreshTabs()
    {
        var vis = G.Upgrades.Visible(); var sb = new StringBuilder();
        foreach (var c in vis) sb.Append(c.Id).Append(c.Page == G.TechPage ? '*' : '.').Append(G.Upgrades.IsNew(c.Id) ? 'n' : '-').Append('|');
        sb.Append(Loc.Lang); string sig = sb.ToString();
        if (sig != tabSig)
        {
            tabSig = sig;
            for (int i = 0; i < vis.Count; i++)
            {
                var c = vis[i];
                if (i >= tabs.Count)
                {
                    var b = new Button { FocusMode = FocusModeEnum.None, ClipText = false };
                    var badge = new TextureRect { Name = "Badge", Texture = star, MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.Nearest, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, Modulate = Gold };
                    b.AddChild(badge); badge.AnchorLeft = 1; badge.AnchorRight = 1; badge.OffsetLeft = -9; badge.OffsetRight = -1; badge.OffsetTop = 1; badge.OffsetBottom = 9;
                    b.Pressed += () => ClickTab(tabs.IndexOf(b));
                    tabsBox.AddChild(b); tabs.Add(b);
                }
                var tab = tabs[i]; tab.Visible = true; tab.SetMeta("page", c.Page); tab.Text = Loc.T(c.Title); tab.Icon = CenteredIcon(UpgradeIcons.Get(c.Icon)); tab.AddThemeConstantOverride("icon_max_width", 12);
                bool isNew = G.Upgrades.IsNew(c.Id);
                tab.ThemeTypeVariation = c.Page == G.TechPage ? "TabSelected" : "Tab";
                tab.GetNode<TextureRect>("Badge").Visible = isNew;
                tab.AddThemeColorOverride("font_color", isNew ? Gold : new Color("#d8e0f0"));
                tab.AddThemeColorOverride("icon_normal_color", Colors.White);                   // иконка в своих цветах: новизну показывают звёздочка и золотой текст (золото перекрашивало голубой лёд в зелёный)
                if (G.TabAnim.Remove(c.Id)) PlayTabAppear(tab);
            }
            for (int i = vis.Count; i < tabs.Count; i++) tabs[i].Visible = false;
        }
        float a = .65f + .35f * Mathf.Sin(t * 6f);
        for (int i = 0; i < vis.Count && i < tabs.Count; i++) if (G.Upgrades.IsNew(vis[i].Id)) tabs[i].GetNode<TextureRect>("Badge").Modulate = new Color(1f, .85f, .25f, a);
    }

    static readonly System.Collections.Generic.Dictionary<Texture2D, Texture2D> centered = new();
    /// <summary>Обрезает иконку по видимым пикселям и вписывает в квадрат: рисунок стоит строго по центру клетки.</summary>
    static Texture2D CenteredIcon(Texture2D t)
    {
        if (t == null) return null; if (centered.TryGetValue(t, out var r)) return r;
        var img = t.GetImage(); if (img == null) return t; if (img.IsCompressed()) img.Decompress();
        var u = img.GetUsedRect(); if (u.Size.X <= 0 || u.Size.Y <= 0) return t;
        int n = Mathf.Max(u.Size.X, u.Size.Y); var o = Image.CreateEmpty(n, n, false, Image.Format.Rgba8);
        o.BlitRect(img, u, new Vector2I((n - u.Size.X) / 2, (n - u.Size.Y) / 2));
        return centered[t] = ImageTexture.CreateFromImage(o);
    }

    /// <summary>Появление новой вкладки: быстрое проявление и золотые пиксельные искры.</summary>
    void PlayTabAppear(Button tab)
    {
        tab.Modulate = new Color(1, 1, 1, 0); var tw = tab.CreateTween(); tw.TweenProperty(tab, "modulate", Colors.White, .35f);
        CallDeferred(nameof(TabSparks), tab);
    }

    void TabSparks(Button tab) { if (IsInstanceValid(tab)) UiFx.Burst(this, UiFx.LocalPoint(this, tab, tab.Size / 2), Gold, 16, 26, .6f); }
}
