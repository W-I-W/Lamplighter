using Godot;
using System.Collections.Generic;
using System.Text;

namespace Lamplighter;

/// <summary>Окно улучшений (T): сверху вкладки только открытых категорий, слева прокручиваемый список компактных карточек, справа подробности.
/// Сцена Scenes/UI/UpgradeWindow.tscn, карточка — UpgradeEntry.tscn. Окно ничего не решает: открытие категорий — UpgradeProgressionSystem, изучение — Game.LearnTech.
/// Управление: мышь (вкладки, карточки, кнопка, колесо) и клавиши (↑/↓, ←/→, Enter).</summary>
public partial class UpgradeWindowUI : Control
{
    public Game G;
    static readonly Color Gold = new("#ffd23f");
    Control panel; HBoxContainer tabsBox; VBoxContainer list; ScrollContainer scroll; UpgradeDetailsUI details; Label count, title; RichTextLabel keys;
    readonly List<Button> tabs = new(); readonly List<UpgradeEntryUI> cards = new();
    PackedScene entryScene; Texture2D star;
    bool wasOpen; int shownPage = -1, shownCur = -1; string tabSig = "", keysLang = ""; float t;
    Tween listTw;

    public override void _Ready()
    {
        ZIndex = 4045;                                      // выше всплывающих сообщений интерфейса (они не должны перекрывать окно)
        panel = GetNode<Control>("%Panel"); tabsBox = GetNode<HBoxContainer>("%Tabs"); list = GetNode<VBoxContainer>("%List"); scroll = GetNode<ScrollContainer>("%Scroll");
        details = GetNode<UpgradeDetailsUI>("%Details"); count = GetNode<Label>("%Count"); title = GetNode<Label>("%Title"); keys = GetNode<RichTextLabel>("%Keys");
        entryScene = GD.Load<PackedScene>("res://Scenes/UI/UpgradeEntry.tscn"); star = GD.Load<Texture2D>("res://Assets/UI/Upgrades/state_star.png");
        details.LearnPressed += ClickLearn;
    }

    // ── действия мыши (те же методы вызывают сигналы элементов и проверки)
    public void ClickLearn() { var l = G.TechList(); if (G.TechCur >= 0 && G.TechCur < l.Count) G.LearnTech(l[G.TechCur]); }
    public void ClickCard(int k) { if (k >= 0 && k < cards.Count && cards[k].Visible && k != G.TechCur) { G.TechCur = k; Sfx.Play("tick", -6, 1.4f); } }
    public void ClickTab(int i) { if (i >= 0 && i < tabs.Count && tabs[i].Visible && tabs[i].HasMeta("page")) G.SelectTechPage((int)tabs[i].GetMeta("page")); }
    public int TabCount { get { int n = 0; foreach (var b in tabs) if (b.Visible) n++; return n; } }
    public int CardCount { get { int n = 0; foreach (var c in cards) if (c.Visible) n++; return n; } }
    public string TabText(int i) => i >= 0 && i < tabs.Count ? tabs[i].Text : "";
    public bool TabBadge(int i) => i >= 0 && i < tabs.Count && tabs[i].GetNode<TextureRect>("Badge").Visible;

    public bool IsOver(Vector2 p) => Visible && panel.GetGlobalRect().HasPoint(p);

    UpgradeEntryUI Card(int i)
    {
        while (cards.Count <= i)
        {
            var c = entryScene.Instantiate<UpgradeEntryUI>(); int idx = cards.Count;
            c.Picked += card => ClickCard(cards.IndexOf(card));
            list.AddChild(c); cards.Add(c);
        }
        return cards[i];
    }

    public override void _Process(double delta)
    {
        if (G == null || G.W == null || G.Upgrades == null) return;
        bool show = G.TechOpen && G.Menu == Mn.None; Visible = show;
        if (!show) { wasOpen = false; return; }
        t += (float)delta;
        if (!wasOpen) { wasOpen = true; shownPage = -1; tabSig = ""; }
        RefreshTabs(); RefreshList();
        var (own, total) = G.TechTotals(); count.Text = $"{own}/{total}";
        string kl = Loc.Lang; if (kl != keysLang) { keysLang = kl; keys.Text = TextFx.Colorize(Loc.T("↑/↓ выбор · ←/→ категория · Enter изучить · T/Esc закрыть")); title.Text = Loc.T("Улучшения"); }
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
                var tab = tabs[i]; tab.Visible = true; tab.SetMeta("page", c.Page); tab.Text = Loc.T(c.Title); tab.Icon = UpgradeIcons.Get(c.Icon);
                bool isNew = G.Upgrades.IsNew(c.Id);
                tab.ThemeTypeVariation = c.Page == G.TechPage ? "TabSelected" : "Tab";
                tab.GetNode<TextureRect>("Badge").Visible = isNew;
                tab.AddThemeColorOverride("font_color", isNew ? Gold : new Color("#d8e0f0"));
                tab.AddThemeColorOverride("icon_normal_color", isNew ? Gold : Colors.White);
                if (G.TabAnim.Remove(c.Id)) PlayTabAppear(tab);
            }
            for (int i = vis.Count; i < tabs.Count; i++) tabs[i].Visible = false;
        }
        // индикатор новизны мягко мигает, пока вкладка не просмотрена
        float a = .65f + .35f * Mathf.Sin(t * 6f);
        for (int i = 0; i < vis.Count && i < tabs.Count; i++) if (G.Upgrades.IsNew(vis[i].Id)) tabs[i].GetNode<TextureRect>("Badge").Modulate = new Color(1f, .85f, .25f, a);
    }

    /// <summary>Появление новой вкладки: быстрое проявление и золотые пиксельные искры.</summary>
    void PlayTabAppear(Button tab)
    {
        tab.Modulate = new Color(1, 1, 1, 0); var tw = tab.CreateTween(); tw.TweenProperty(tab, "modulate", Colors.White, .35f);
        CallDeferred(nameof(TabSparks), tab);
    }

    void TabSparks(Button tab) { if (IsInstanceValid(tab)) UiFx.Burst(this, UiFx.LocalPoint(this, tab, tab.Size / 2), Gold, 16, 26, .6f); }

    void RefreshList()
    {
        var items = G.TechList(); G.TechCur = Mathf.Clamp(G.TechCur, 0, Mathf.Max(0, items.Count - 1));
        if (shownPage != G.TechPage)
        {
            shownPage = G.TechPage; shownCur = -1; scroll.ScrollVertical = 0;
            listTw?.Kill(); list.Modulate = new Color(1, 1, 1, 0); listTw = CreateTween(); listTw.TweenProperty(list, "modulate", Colors.White, .15f);       // переключение вкладки
        }
        for (int i = 0; i < items.Count; i++)
        {
            int ti = items[i]; var c = Card(i); c.Visible = true;
            c.Bind(ti, G.StateOfTech(ti), i == G.TechCur, Loc.T(Game.Techs[ti].Name), UpgradeIcons.ForTech(ti));
        }
        for (int i = items.Count; i < cards.Count; i++) cards[i].Visible = false;
        if (shownCur != G.TechCur && items.Count > 0) { shownCur = G.TechCur; CallDeferred(nameof(KeepVisible), G.TechCur); }
        if (G.TechLearned >= 0)
        {
            int k = items.IndexOf(G.TechLearned);
            if (k >= 0 && k < cards.Count) { cards[k].LearnFx(this); UiFx.Burst(this, UiFx.LocalPoint(this, details.IconNode, details.IconNode.Size / 2), new Color("#8ff0ff"), 16, 28, .6f); }
            G.TechLearned = -1;
        }
        details.Show(G, items.Count > 0 ? items[G.TechCur] : -1);
    }

    void KeepVisible(int idx) { if (idx >= 0 && idx < cards.Count && IsInstanceValid(cards[idx])) scroll.EnsureControlVisible(cards[idx]); }
}
