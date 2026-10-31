using Godot;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lamplighter;

/// <summary>Окно экипировки [C]: слева карточки предметов выбранной категории, в центре выбранный предмет с характеристиками, ячейками вставок и кнопками, справа описание, эффекты, сравнение
/// и выбор вставки. Окно ничего не решает: оно читает данные игры и вызывает те же команды, что и клавиши (мост — Game.EquipUI.cs). Клавиатура работает как раньше (WindowKey → EquipKey).
/// Сцены: Scenes/UI/EquipWindow.tscn, EquipCard.tscn, EquipSocket.tscn; стили — LanternTheme и UpgradeTheme.</summary>
public partial class EquipWindowUI : Control
{
    public Game G;
    static readonly Color Gold = new("#ffd23f"), Dim = new("#8a96b0"), Good = new("#9be07a"), Bad = new("#f06a5e"), Cyan = new("#8ff0ff"), Text = new("#d8e0f0");
    Control panel, listPanel, rightPanel, centerPanel; HBoxContainer tabsBox; VBoxContainer list, centerBox, rightBox; ScrollContainer scroll, rscroll; Label title, closeHint, foot;
    readonly List<Button> tabs = new(); readonly List<EquipCardUI> cards = new(); readonly List<EquipSocketUI> sockets = new();
    PackedScene cardScene, socketScene; bool wasOpen; string sig = ""; float rebuildT; int lastTab = -1; Tween openTw, bodyTw;
    static readonly string[] TabNames = { "Оружие", "Реликвии", "Создание", "Фонарь" };
    static readonly string[] TabQuestions = { "Что вы держите и что можно взять в руки", "Найденные реликвии и их бонусы", "Что можно создать прямо сейчас", "Ваш фонарь и его улучшения" };

    public override void _Ready()
    {
        ZIndex = 4040; panel = GetNode<Control>("%Panel"); listPanel = GetNode<Control>("%Scroll").GetParent<Control>(); rightPanel = GetNode<Control>("%RScroll").GetParent<Control>(); centerPanel = GetNode<Control>("%CenterBox").GetParent<Control>(); tabsBox = GetNode<HBoxContainer>("%Tabs"); list = GetNode<VBoxContainer>("%List"); scroll = GetNode<ScrollContainer>("%Scroll");
        centerBox = GetNode<VBoxContainer>("%CenterBox"); rightBox = GetNode<VBoxContainer>("%RightBox"); rscroll = GetNode<ScrollContainer>("%RScroll");
        title = GetNode<Label>("%Title"); closeHint = GetNode<Label>("%CloseHint"); foot = GetNode<Label>("%Foot");
        cardScene = GD.Load<PackedScene>("res://Scenes/UI/EquipCard.tscn"); socketScene = GD.Load<PackedScene>("res://Scenes/UI/EquipSocket.tscn");
    }

    public bool IsOver(Vector2 p) => Visible && panel.GetGlobalRect().HasPoint(p);

    // ───────────────────────── для проверок ─────────────────────────
    public int CardCount { get { int n = 0; foreach (var c in cards) if (c.Visible) n++; return n; } }
    public int SocketCount { get { int n = 0; foreach (var c in centerBox.GetChildren()) if (c is HBoxContainer h) foreach (var q in h.GetChildren()) if (q is EquipSocketUI) n++; return n; } }
    public void ClickTab(int i) => tabs[i].EmitSignal(BaseButton.SignalName.Pressed);
    public void ClickCard(int i) => cards[i].EmitSignal(Control.SignalName.GuiInput, Mb(false));
    public void DoubleClickCard(int i) => cards[i].EmitSignal(Control.SignalName.GuiInput, Mb(true));
    public void ClickSocket(int i, bool right = false) { int k = 0; foreach (var c in centerBox.GetChildren()) if (c is HBoxContainer h) foreach (var q in h.GetChildren()) if (q is EquipSocketUI so && k++ == i) so.EmitSignal(Control.SignalName.GuiInput, Mb(false, right)); }
    public bool PressButton(string startsWith) { foreach (var b in Buttons(this)) if (!b.Disabled && b.Visible && b.Text.StartsWith(T(startsWith))) { b.EmitSignal(BaseButton.SignalName.Pressed); return true; } return false; }
    public bool HasButton(string startsWith, bool enabled) { foreach (var b in Buttons(this)) if (b.Visible && b.Text.StartsWith(T(startsWith)) && b.Disabled != enabled) return true; return false; }
    public void ClickMod(int id, bool dbl) { foreach (var n in rightBox.GetChildren()) if (n is PanelContainer p && p.HasMeta("mod") && (int)p.GetMeta("mod") == id) p.EmitSignal(Control.SignalName.GuiInput, Mb(dbl)); }
    static InputEventMouseButton Mb(bool dbl, bool right = false) => new() { Pressed = true, ButtonIndex = right ? MouseButton.Right : MouseButton.Left, DoubleClick = dbl };
    static IEnumerable<Button> Buttons(Node n) { foreach (var c in n.GetChildren()) { if (c is Button b) yield return b; foreach (var d in Buttons(c)) yield return d; } }
    public void Force() { sig = ""; Refresh(); }
    /// <summary>Все видимые элементы центра и вкладок лежат внутри панели, а панель — внутри экрана.</summary>
    public bool Fits(Rect2 screen)
    {
        var pr = panel.GetGlobalRect(); if (!screen.Grow(1).Encloses(pr)) return false;
        foreach (var c in All(centerBox)) if (c is Control k && k.IsVisibleInTree() && !pr.Grow(1).Encloses(k.GetGlobalRect())) return false;
        foreach (var t in tabs) if (!pr.Grow(1).Encloses(t.GetGlobalRect())) return false;
        var cp = centerBox.GetParent<Control>().GetGlobalRect();
        foreach (var c in All(centerBox)) if (c is Label l && l.IsVisibleInTree() && l.Size.X > 0 && l.GetMinimumSize().X > l.Size.X + 1 && l.AutowrapMode == TextServer.AutowrapMode.Off && !l.ClipText) return false;
        return cp.Size.X > 0;
    }
    static IEnumerable<Node> All(Node n) { foreach (var c in n.GetChildren()) { yield return c; foreach (var d in All(c)) yield return d; } }
    public bool IconsCrisp { get { foreach (var n in All(this)) if (n is TextureRect t && t.Texture != null && t.TextureFilter != TextureFilterEnum.Nearest) return false; return true; } }

    // ───────────────────────── общие помощники ─────────────────────────
    static string T(string s) => Loc.T(s);

    Label Lbl(string text, Color? col = null, bool wrap = false, string variation = null, bool raw = false)
    {
        var l = new Label { Text = raw ? text : T(text), MouseFilter = MouseFilterEnum.Ignore }; if (variation != null) l.ThemeTypeVariation = variation;
        if (col.HasValue) l.AddThemeColorOverride("font_color", col.Value);
        if (wrap) { l.AutowrapMode = TextServer.AutowrapMode.WordSmart; l.SizeFlagsHorizontal = SizeFlags.ExpandFill; l.CustomMinimumSize = new Vector2(120, 0); }
        return l;
    }

    TextureRect Icon(Texture2D t, float size, Color? tint = null) => new TextureRect { Texture = t, CustomMinimumSize = new Vector2(size, size), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, TextureFilter = TextureFilterEnum.Nearest, Modulate = tint ?? Colors.White, MouseFilter = MouseFilterEnum.Ignore, SizeFlagsVertical = SizeFlags.ShrinkCenter };

    /// <summary>Строка характеристики: значок, название и значение; необязательная вторая строка — изменение по сравнению с надетым (зелёное — лучше, красное — хуже).</summary>
    Control Stat(Texture2D ic, string name, string value, Color? valCol = null, string delta = null, bool better = true)
    {
        var h = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; h.AddThemeConstantOverride("separation", 4);
        h.AddChild(Px.IconInline(ic, 12)); var n = Lbl(name, Dim); n.SizeFlagsHorizontal = SizeFlags.ExpandFill; h.AddChild(n);
        h.AddChild(Lbl(value, valCol ?? Text, false, null, true));
        if (delta != null) h.AddChild(Lbl(delta, better ? Good : Bad, false, null, true));
        return h;
    }

    Button Btn(string text, string hotkey, Action act, bool enabled = true, bool primary = false)
    {
        var b = new Button { Text = T(text) + (hotkey != null ? "  " + hotkey : ""), Disabled = !enabled, FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipText = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
        if (primary && enabled) b.AddThemeColorOverride("font_color", Gold);
        b.Pressed += () => act(); return b;
    }

    static void Clear(Node n) { foreach (var c in n.GetChildren()) { n.RemoveChild(c); c.QueueFree(); } }

    EquipCardUI Card(int i)
    {
        while (cards.Count <= i)
        {
            var c = cardScene.Instantiate<EquipCardUI>(); c.Picked += x => OnPick(x.Row); c.Activated += x => OnActivate(x.Row);
            list.AddChild(c); cards.Add(c);
        }
        return cards[i];
    }

    // ───────────────────────── кадр ─────────────────────────
    public override void _Process(double delta)
    {
        if (G == null || G.W == null) return;
        bool show = G.Window == Win.Equip && G.Menu == Mn.None; Visible = show;
        if (!show) { wasOpen = false; G.StationOpened = false; G.CraftMode = "hand"; return; }
        if (!wasOpen)
        {
            wasOpen = true; sig = ""; lastTab = -1; G.CraftResetSel(); panel.PivotOffset = panel.Size / 2; panel.Modulate = new Color(1, 1, 1, 0); panel.Scale = new Vector2(.96f, .96f);
            openTw?.Kill(); openTw = CreateTween().SetParallel(); openTw.TweenProperty(panel, "modulate", Colors.White, .12f); openTw.TweenProperty(panel, "scale", Vector2.One, .12f);   // короткое открытие
        }
        rebuildT -= (float)delta;
        string s = Signature(); if (s == sig || Input.IsMouseButtonPressed(MouseButton.Left)) return; sig = s; rebuildT = .25f; Refresh();      // перестраиваем только при изменении данных (иначе кнопки мигают под курсором) и не при зажатой мыши: иначе нажатие теряется
    }

    string Signature()
    {
        var sb = new StringBuilder(); sb.Append(G.WinTab).Append('|').Append(G.WinCur).Append('|').Append(G.EqSocket).Append('|').Append(G.EqPick).Append('|').Append(G.EqShowAll).Append('|').Append(G.CurW).Append('|').Append(G.CurTool).Append('|').Append(G.HotSel).Append('|').Append(Loc.Lang);
        foreach (var h in G.Hot) sb.Append(h).Append(','); foreach (var m in G.ModInv) sb.Append(m).Append(',');
        sb.Append(G.AxeLv).Append('|').Append(G.NearUpgrade()).Append('|').Append(G.OwnedRelics.Count).Append('|').Append(G.EquippedRelics.Count).Append('|').Append(G.CraftQty).Append('|').Append(G.CraftMode).Append(G.CraftOnlyReady).Append(G.StationOpened).Append('|').Append(G.HeroLampLv).Append('|').Append(G.Tier).Append('|').Append(G.Owned.Count);
        foreach (var o in G.Owned) sb.Append(o.Mods.Count).Append('.').Append(o.Lv).Append(';');
        for (int i = 0; i < It.Count; i++) if (i == It.Wood || i == It.Stone || i == It.Copper || i == It.Iron || i == It.Silver || i == It.Crystal) sb.Append(G.Inv[i]).Append(',');
        if (G.WinTab >= 2) for (int i = 0; i < It.Count; i++) sb.Append(G.Inv[i]).Append(',');
        return sb.ToString();
    }

    // ───────────────────────── действия мыши ─────────────────────────
    void OnPick(int row) { G.EqSelect(row); }

    void OnActivate(int row)
    {
        switch (G.WinTab)
        {
            case 0: G.EqEquipRow(row); Flash(centerBox); break;
            case 1: if (row < G.OwnedRelics.Count) { G.WinCur = row; G.EqToggleRelic(G.OwnedRelics[row]); Flash(centerBox); } break;
            case 2: G.EqCraftRow(row); Flash(centerBox); break;
            case 3: G.EqUpgradeLamp(); Flash(centerBox); break;
        }
    }

    void Flash(Control c) { var tw = CreateTween(); c.Modulate = new Color(1.5f, 1.4f, 1.1f); tw.TweenProperty(c, "modulate", Colors.White, .22f); }

    // ───────────────────────── перестроение ─────────────────────────
    void Refresh()
    {
        G.PokeHover();
        title.Text = T("Снаряжение"); closeHint.Text = T("Esc — закрыть"); foot.Text = T(TabQuestions[Mathf.Clamp(G.WinTab, 0, 3)]);
        RefreshTabs();
        Clear(centerBox); Clear(rightBox);
        switch (G.WinTab) { case 0: BuildWeapons(); break; case 1: BuildRelics(); break; case 2: BuildCraft(); break; default: BuildLantern(); break; }
        // пустые части не растягиваем: без списка (пустые реликвии) центр занимает его место, без сведений скрыта правая панель
        bool anyList = false; foreach (var cd in cards) if (cd.Visible) { anyList = true; break; }
        listPanel.Visible = anyList; rightPanel.Visible = rightBox.GetChildCount() > 0; centerPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizePanel();
        if (lastTab != G.WinTab) { lastTab = G.WinTab; scroll.ScrollVertical = 0; bodyTw?.Kill(); var body = scroll.GetParent().GetParent<Control>(); body.Modulate = new Color(1, 1, 1, 0); bodyTw = CreateTween(); bodyTw.TweenProperty(body, "modulate", Colors.White, .14f); }
    }

    void RefreshTabs()
    {
        var icons = new Texture2D[] { UiIcons.Item(UiIcons.Sword), UiIcons.Item(It.Relic), UiIcons.Tile(Art.Furnace), UiIcons.Tile(Art.Lantern) };
        while (tabs.Count < 4)
        {
            int i = tabs.Count; var b = new Button { FocusMode = FocusModeEnum.None, ToggleMode = false, ExpandIcon = false, IconAlignment = HorizontalAlignment.Left, TextureFilter = TextureFilterEnum.Nearest };
            b.Pressed += () => G.EqTab(i); tabsBox.AddChild(b); tabs.Add(b);
        }
        for (int i = 0; i < 4; i++)
        {
            var b = tabs[i]; b.Text = T(TabNames[i]); b.Icon = Px.Raised(icons[i], 2); bool act = i == G.WinTab;
            b.ThemeTypeVariation = act ? "TabSelected" : "Tab"; b.AddThemeColorOverride("font_color", act ? Gold : Dim); b.AddThemeColorOverride("font_hover_color", act ? Gold : Text);
        }
    }

    // ───────────────────────── вкладка «Оружие» ─────────────────────────
    int RowCount0 => G.ToolList().Count + G.Owned.Count;

    void BuildWeapons()
    {
        var tl = G.ToolList(); int nt = tl.Count, total = nt + G.Owned.Count; G.WinCur = Mathf.Clamp(G.WinCur, 0, Math.Max(0, total - 1));
        for (int i = 0; i < total; i++)
        {
            var c = Card(i); c.Visible = true; var r = G.EquipRef(i); int hs = r != null ? G.HotSlotOf(r) + 1 : 0; bool held = G.EqIsHeld(i);
            if (i < nt) { var td = ToolDef.Get(tl[i]); string nm = tl[i] == "pickaxe" ? Game.PickNames[G.Tier] : td.Name; c.Bind(i, UiIcons.Item(td.Icon), T(nm) + LvTag(G.ToolLv(tl[i])), Text, held ? T("Сейчас в руках") : hs > 0 ? $"{T("В хотбаре")}: {T("слот")} {hs}" : T("Инструмент"), held ? Good : hs > 0 ? Cyan : Dim, held, hs, i == G.WinCur); }
            else { var d = Db.Weapon(G.Owned[i - nt].Id); c.Bind(i, UiIcons.Item(G.HotIcon(Game.RefWeapon(d.Id))), T(d.Name) + LvTag(G.Owned[i - nt].Lv), Db.RarCol[(int)d.Rar], $"{T(Db.ClassNames[(int)d.Cls])} · {T(Db.RarName[(int)d.Rar])}", Dim, held, hs, i == G.WinCur); }
        }
        for (int i = total; i < cards.Count; i++) cards[i].Visible = false;
        if (G.WinCur < nt) BuildToolDetails(tl[G.WinCur]); else BuildWeaponDetails(G.WinCur - nt);
    }

    /// <summary>Назначение в хотбар: три пиктограммы-слота. Клик по слоту ставит сюда предмет, клик по слоту с этим предметом освобождает его.</summary>
    void BuildHotbarAssign(int row)
    {
        var r = G.EquipRef(row); if (r == null) return; int cur = G.HotSlotOf(r);
        centerBox.AddChild(Lbl("В хотбар", Dim, false, "DimLabel"));
        var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", 4); centerBox.AddChild(h);
        for (int s = 0; s < Game.HotCount; s++)
        {
            int slot = s; var occ = G.Hot[s]; var chip = new HotSlotChipUI { Slot = s, Mine = cur == s, Occupant = occ != null && G.HotValid(occ) ? UiIcons.Item(G.HotIcon(occ)) : null };
            chip.TooltipText = cur == s ? T("Сейчас в этом слоте. Клик — освободить") : occ != null && G.HotValid(occ) ? T("Заменит") + ": " + T(G.HotName(occ)) : T("Свободный слот");
            chip.Clicked = () => { if (cur == slot) G.EqHotClear(row); else G.EqHotAssign(row, slot); Flash(centerBox); }; h.AddChild(chip);
        }
    }

    static string LvTag(int lv) => lv > 0 ? " +" + lv : "";

    static string Level3(float v, float lo, float hi, string a, string b, string c) => v < lo ? T(a) : v < hi ? T(b) : T(c);

    void BuildToolDetails(string id)
    {
        int row = G.WinCur; var td = ToolDef.Get(id); string nm = id == "pickaxe" ? Game.PickNames[G.Tier] : td.Name; bool held = G.EqIsHeld(row); int slot = G.HotSlotOf(Game.RefTool(id));
        string status = held ? "Сейчас в руках" : slot >= 0 ? $"{T("В хотбаре")}: {T("слот")} {slot + 1}" : "Можно взять в руки";
        centerBox.AddChild(ItemDetailsCardUI.Make(UiIcons.Item(td.Icon), null, T(nm), Gold, T(status), held ? Good : slot >= 0 ? Cyan : Dim, T(td.Desc)));
        var ok = new List<string>(); if (td.EffWood > 0) ok.Add(T("дерево")); if (td.EffStone > 0) ok.Add(T("камень")); if (td.EffOre > 0) ok.Add(T("руда")); if (td.EffRubble > 0) ok.Add(T("завалы"));
        centerBox.AddChild(Stat(UiIcons.Item(UiIcons.Sword), "Урон", Level3(td.CombatDamage, .7f, 1.2f, "низкий", "обычный", "высокий")));
        centerBox.AddChild(Stat(UiIcons.Item(UiIcons.Pickaxe), "Добыча", Level3(td.MiningPower, 1.05f, 1.7f, "обычная", "быстрая", "очень быстрая"), Cyan));
        if (ok.Count > 0) centerBox.AddChild(Lbl($"{T("Подходит")}: {string.Join(", ", ok)}", Dim, true, null, true));
        if (!held) centerBox.AddChild(MainActionButtonUI.Make("Взять в руки", () => { G.EqEquipRow(row); Flash(centerBox); }, true));
        BuildHotbarAssign(row);
        if (G.Owned.Count == 0) rightBox.AddChild(EmptyStatePanelUI.Make(UiIcons.Item(UiIcons.Sword), 0, T("Пока только базовый инструмент"), T("Создайте оружие у печи или кузницы — оно появится здесь.")));
    }

    void BuildWeaponDetails(int idx)
    {
        if (idx < 0 || idx >= G.Owned.Count) return; var ow = G.Owned[idx]; var w = Db.Weapon(ow.Id); int row = G.ToolList().Count + idx; bool held = G.EqIsHeld(row); int slot = G.HotSlotOf(Game.RefWeapon(w.Id));
        string status = held ? "Сейчас в руках" : slot >= 0 ? $"{T("В хотбаре")}: {T("слот")} {slot + 1}" : "Можно взять в руки";
        centerBox.AddChild(ItemDetailsCardUI.Make(UiIcons.Item(G.HotIcon(Game.RefWeapon(w.Id))), null, T(w.Name), Db.RarCol[(int)w.Rar], T(status), held ? Good : slot >= 0 ? Cyan : Dim, $"{T(Db.ClassNames[(int)w.Cls])} · {T(Db.RarName[(int)w.Rar])}"));
        float dmg = G.WeaponDmg(w), rate = 1f / w.Cd; var cmp = held ? null : HeldWeaponDef();
        string D(float a, float b2, string fmt = "0.0#") { float d = a - b2; return Mathf.Abs(d) < .005f ? null : (d > 0 ? "+" : "") + d.ToString(fmt); }
        centerBox.AddChild(Stat(UiIcons.Item(UiIcons.Sword), "Урон", dmg.ToString("0.#"), Text, cmp == null ? null : D(dmg, G.WeaponDmg(cmp)), cmp == null || dmg >= G.WeaponDmg(cmp)));
        centerBox.AddChild(Stat(UiIcons.Item(UiIcons.Hammer), "Скорость", Level3(rate, .9f, 1.6f, "медленная", "обычная", "быстрая"), Text, cmp == null ? null : D(rate, 1f / cmp.Cd, "0.0"), cmp == null || rate >= 1f / cmp.Cd));
        if (w.Cls == WClass.Sword) centerBox.AddChild(Stat(UiIcons.Item(UiIcons.Pickaxe), "Дальность", Level3(w.Reach, 24f, 34f, "ближняя", "средняя", "дальняя")));
        else centerBox.AddChild(Stat(UiIcons.Item(UiIcons.Bow), "Дальность", T("стреляет издалека")));
        if (w.Slots > 0)
        {
            centerBox.AddChild(Stat(UiIcons.Item(It.Shard), "Вставки", $"{ow.Mods.Count}/{w.Slots}", Cyan));
            var sh = new HBoxContainer(); sh.AddThemeConstantOverride("separation", 4); centerBox.AddChild(sh);
            while (sockets.Count < w.Slots) sockets.Add(null);
            for (int s = 0; s < w.Slots; s++)
            {
                var so = socketScene.Instantiate<EquipSocketUI>(); sh.AddChild(so); int si = s;
                if (s < ow.Mods.Count) { var m = Db.Mods[ow.Mods[s]]; so.Bind(s, UiIcons.Item(Game.ModIcon(m.Id)), Game.ModTint(m.Id), G.EqSocket == s, $"{T(m.Name)}\n{T(m.Desc)}\n{T("ПКМ — снять")}"); }
                else so.Bind(s, null, Colors.White, G.EqSocket == s, T("Пустая ячейка: нажмите, чтобы выбрать вставку"));
                so.Picked += x => { G.EqSocket = x.Index; G.EqPick = -1; Sfx.Play("tick", -8, 1.5f); }; so.Removed += x => { if (x.Index < ow.Mods.Count && held) { x.Flash(Cyan); G.EqRemove(x.Index); } };
            }
        }
        if (!held) centerBox.AddChild(MainActionButtonUI.Make("Взять в руки", () => { G.EqEquipRow(row); Flash(centerBox); }, true));
        BuildHotbarAssign(row);
        // правая панель: описание, эффекты, выбор вставки
        rightBox.AddChild(Lbl(w.Desc, Text, true));
        if (held) { var eff = G.EffectsText(); if (eff != "") { rightBox.AddChild(Lbl("Бонусы", Gold, false, "GoldLabel")); rightBox.AddChild(Lbl(eff, Cyan, true)); } }
        if (w.Slots <= 0) return;
        if (!held) { rightBox.AddChild(Lbl("Чтобы менять вставки, возьмите оружие в руки.", Dim, true)); return; }
        BuildSocketPanel(ow, w);
    }

    WeaponDef HeldWeaponDef() => G.CurW >= 0 && G.CurW < G.Owned.Count ? Db.Weapon(G.Owned[G.CurW].Id) : null;

    void BuildSocketPanel(OwnedWeapon ow, WeaponDef w)
    {
        if (G.EqSocket >= 0 && G.EqSocket < ow.Mods.Count)
        {
            var m = Db.Mods[ow.Mods[G.EqSocket]];
            rightBox.AddChild(Lbl("Установлено", Gold, false, "GoldLabel")); rightBox.AddChild(ModRow(m, null, true)); rightBox.AddChild(Lbl(m.Desc, Text, true));
            int sl = G.EqSocket; rightBox.AddChild(Btn("Снять вставку", "Backspace", () => { G.EqRemove(sl); Flash(centerBox); }, true, true)); return;
        }
        if (G.EqSocket < 0 || G.EqSocket >= w.Slots) { rightBox.AddChild(Lbl(ow.Mods.Count < w.Slots ? "Нажмите на пустую ячейку, чтобы выбрать вставку." : "Все ячейки заняты. ПКМ по вставке — снять.", Dim, true)); return; }
        var hdr = new HBoxContainer(); hdr.AddChild(Lbl("Доступные вставки", Gold, false, "GoldLabel")); var sp = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill }; hdr.AddChild(sp); rightBox.AddChild(hdr);
        var all = new CheckButton { Text = T("Показать все"), ButtonPressed = G.EqShowAll, FocusMode = FocusModeEnum.None }; all.Toggled += on => G.EqShowAll = on; rightBox.AddChild(all);
        int shown = 0;
        for (int m = 0; m < Db.Mods.Length; m++)
        {
            int have = G.ModInv[m]; if (have <= 0 && !G.EqShowAll) continue; shown++; int id = m; bool sel = G.EqPick == m;
            var row = ModRow(Db.Mods[m], have, false, sel); row.SetMeta("mod", m);
            row.GuiInput += e => { if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left) { if (mb.DoubleClick && G.ModInv[id] > 0) { FlashSocket(); G.EqInstall(id); } else { G.EqPick = id; Sfx.Play("tick", -8, 1.4f); } } };
            rightBox.AddChild(row);
        }
        if (shown == 0) rightBox.AddChild(Lbl("Вставок нет. Создайте их у печи (вкладка «Создание») или нажмите «Показать все».", Dim, true));
        if (G.EqPick >= 0 && G.EqPick < Db.Mods.Length) { var pm = Db.Mods[G.EqPick]; rightBox.AddChild(Lbl(pm.Desc, Cyan, true)); int id2 = G.EqPick; bool can = G.ModInv[id2] > 0 && ow.Mods.Count < w.Slots; rightBox.AddChild(Btn("Установить", "1–7", () => { FlashSocket(); G.EqInstall(id2); Flash(centerBox); }, can, true)); }
    }

    void FlashSocket() { foreach (var n in centerBox.GetChildren()) if (n is HBoxContainer h) foreach (var c in h.GetChildren()) if (c is EquipSocketUI s) s.Flash(Cyan); }

    PanelContainer ModRow(ModDef m, int? have, bool plain, bool selected = false)
    {
        var p = new PanelContainer { MouseFilter = MouseFilterEnum.Stop, ThemeTypeVariation = selected ? "RowSelected" : "RowNormal" }; var h = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; h.AddThemeConstantOverride("separation", 4); p.AddChild(h);
        bool dim = have.HasValue && have.Value <= 0; h.AddChild(Px.IconInline(UiIcons.Item(Game.ModIcon(m.Id)), 16, dim ? Game.ModTint(m.Id) * new Color(1, 1, 1, .45f) : Game.ModTint(m.Id)));
        var n = Lbl(m.Name, dim ? Dim : Text); n.SizeFlagsHorizontal = SizeFlags.ExpandFill; h.AddChild(n);
        if (have.HasValue) h.AddChild(Lbl($"×{have.Value}", have.Value > 0 ? Gold : Dim));
        h.AddChild(Lbl(plain ? "" : "✓", Good)); return p;
    }

    // ───────────────────────── вкладка «Реликвии» ─────────────────────────
    void BuildRelics()
    {
        int n = G.OwnedRelics.Count; G.WinCur = Mathf.Clamp(G.WinCur, 0, Math.Max(0, n - 1));
        for (int i = 0; i < n; i++)
        {
            var d = Db.Relic(G.OwnedRelics[i]); var c = Card(i); c.Visible = true; if (d == null) { c.Visible = false; continue; }
            bool eqd = G.EquippedRelics.Contains(d.Id);
            c.Bind(i, UiIcons.Item(It.Relic), T(d.Title), Db.RarCol[d.Rarity], eqd ? T("Надета") : T(Db.RarName[d.Rarity]), eqd ? Good : Dim, eqd, 0, i == G.WinCur, false, d.Tint);
        }
        for (int i = n; i < cards.Count; i++) cards[i].Visible = false;
        if (n == 0)
        {
            // пустая вкладка должна выглядеть живой: три слота-силуэта и подсказка, где искать
            centerBox.AddChild(EmptyStatePanelUI.Make(UiIcons.Item(It.Relic), Math.Max(3, G.RelicSlots), T("У вас пока нет реликвий"), T("Их можно найти в сундуках, купить у торговцев или получить за опасные вылазки.")));
            return;
        }
        centerBox.AddChild(Lbl($"{T("Надето")}: {G.EquippedRelics.Count} {T("из")} {G.RelicSlots}", Dim, false, "DimLabel", true));
        var sh = new HBoxContainer(); sh.AddThemeConstantOverride("separation", 3); centerBox.AddChild(sh);
        for (int s = 0; s < G.RelicSlots; s++) { var sq = new PanelContainer { ThemeTypeVariation = s < G.EquippedRelics.Count ? "SlotOk" : "Slot", CustomMinimumSize = new Vector2(22, 22) }; if (s < G.EquippedRelics.Count) { var rd = Db.Relic(G.EquippedRelics[s]); var ti = Icon(UiIcons.Item(It.Relic), 14, rd?.Tint); ti.TooltipText = rd != null ? T(rd.Title) : ""; sq.AddChild(ti); sq.TooltipText = rd != null ? T(rd.Title) : ""; sq.MouseFilter = MouseFilterEnum.Stop; } sh.AddChild(sq); }
        var rel = Db.Relic(G.OwnedRelics[G.WinCur]); if (rel == null) return; bool eq = G.EquippedRelics.Contains(rel.Id); int cur = G.WinCur;
        centerBox.AddChild(ItemDetailsCardUI.Make(UiIcons.Item(It.Relic), rel.Tint, T(rel.Title), Db.RarCol[rel.Rarity], eq ? T("Сейчас экипировано") : T(Db.RarName[rel.Rarity]), eq ? Good : Dim));
        bool full = !eq && G.EquippedRelics.Count >= G.RelicSlots;
        centerBox.AddChild(MainActionButtonUI.Make(eq ? "Снять" : "Надеть", () => { G.EqToggleRelic(G.OwnedRelics[cur]); Flash(centerBox); }, eq || !full, full ? T("Все слоты заняты: сначала снимите другую реликвию.") : null));
        rightBox.AddChild(Lbl("Что даёт", Gold, false, "GoldLabel")); rightBox.AddChild(Lbl(rel.Desc, Text, true));
    }

    // ───────────────────────── вкладка «Создание» ─────────────────────────
    /// <summary>Создание — компактное окно: сверху режим (ручной крафт или станция) и фильтр, под ним сетка иконок и справа выбранный рецепт. Остальные вкладки используют полный размер.</summary>
    void SizePanel()
    {
        bool craft = G.WinTab == 2; float hw = craft ? 215 : 310, hh = craft ? 128 : 172;
        panel.OffsetLeft = -hw; panel.OffsetRight = hw; panel.OffsetTop = -hh; panel.OffsetBottom = hh;
    }

    public int RecipeCellCount => cells.Count;
    public void ClickRecipe(int i, bool dbl) { foreach (var c in cells) if (c.Index == i) c.EmitSignal(Control.SignalName.GuiInput, Mb(dbl)); }
    readonly List<RecipeCellUI> cells = new();

    Button Chip(string text, Texture2D icon, bool active, Action act)
    {
        var b = new Button { Text = T(text), Icon = icon != null ? Px.Raised(icon, 2) : null, FocusMode = FocusModeEnum.None, ExpandIcon = false, IconAlignment = HorizontalAlignment.Left, TextureFilter = TextureFilterEnum.Nearest, ThemeTypeVariation = active ? "TabSelected" : "Tab" };
        b.AddThemeColorOverride("font_color", active ? Gold : Dim); b.AddThemeColorOverride("font_hover_color", active ? Gold : Text); b.Pressed += () => act(); return b;
    }

    void BuildCraft()
    {
        foreach (var cd in cards) cd.Visible = false; cells.Clear();
        var rows = G.CraftRows(); int sel = G.CraftSync(rows);
        Texture2D GroupIcon(string g) => g == "hand" ? UiIcons.Item(UiIcons.Pickaxe) : g == "furnace" ? UiIcons.Tile(Art.Furnace) : UiIcons.Tile(Art.Forge);
        // режим и фильтр
        var top = new HBoxContainer(); top.AddThemeConstantOverride("separation", 2); centerBox.AddChild(top);
        var groups = G.CraftGroups(); string cg = G.CraftGroup();
        if (groups.Count > 1) foreach (var g in groups) { string gg = g; top.AddChild(Chip(Game.GroupName(g), GroupIcon(g), g == cg, () => G.EqCraftMode(gg))); }
        else top.AddChild(Lbl(Game.GroupName(cg), Gold, false, "GoldLabel"));
        top.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        top.AddChild(Chip("Все", null, !G.CraftOnlyReady, () => G.EqCraftFilter(false))); top.AddChild(Chip("Доступные", null, G.CraftOnlyReady, () => G.EqCraftFilter(true)));
        foot.Text = groups.Count > 1 ? T("Tab — режим · Enter — создать · двойной клик — создать") : T("Рецепты печи и кузницы открываются у станции [E]");
        if (rows.Count == 0)
        {
            centerBox.AddChild(new Control { CustomMinimumSize = new Vector2(0, 40) });
            var e = Lbl("Пока нет доступных рецептов", Dim, true); e.HorizontalAlignment = HorizontalAlignment.Center; centerBox.AddChild(e); return;
        }
        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; body.AddThemeConstantOverride("separation", 6); centerBox.AddChild(body);
        var gs = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(Game.CraftCols * (RecipeCellUI.Cell + 3) + 8, 150), SizeFlagsVertical = SizeFlags.ExpandFill }; body.AddChild(gs);
        var grid = new GridContainer { Columns = Game.CraftCols }; grid.AddThemeConstantOverride("h_separation", 3); grid.AddThemeConstantOverride("v_separation", 3); gs.AddChild(grid);
        for (int i = 0; i < rows.Count; i++)
        {
            var rc = rows[i].r; var cell = new RecipeCellUI(); grid.AddChild(cell); cells.Add(cell);
            cell.Bind(i, rc.Icon >= 0 ? UiIcons.Item(rc.Icon) : GroupIcon(rows[i].group), G.StateOf(rc), i == sel, T(rc.Title));
            cell.Picked += x => G.EqSelect(x); cell.Activated += x => { G.EqCraftRow(x); Flash(centerBox); };
        }
        if (sel >= Game.CraftCols * 2) CallDeferred(nameof(ScrollToCell), sel);
        BuildRecipeDetails(body, rows[sel].r, sel);
    }

    void ScrollToCell(int i) { foreach (var c in cells) if (IsInstanceValid(c) && c.Index == i && c.GetParent()?.GetParent() is ScrollContainer sc) sc.EnsureControlVisible(c); }

    void BuildRecipeDetails(Control body, Recipe r, int idx)
    {
        var v = new VBoxContainer { CustomMinimumSize = new Vector2(150, 0), SizeFlagsHorizontal = SizeFlags.ExpandFill }; v.AddThemeConstantOverride("separation", 4); body.AddChild(v);
        var st = G.StateOf(r); var bad = new Color("#f06a5e");
        string status = st == RecState.Owned ? "Уже есть" : st == RecState.Ready ? (r.Extra != null && r.Extra() > 0 ? $"{T("в очереди")} {r.Extra()}" : "Можно создать") : "Не хватает ресурсов";
        v.AddChild(ItemDetailsCardUI.Make(UiIcons.Item(r.Icon >= 0 ? r.Icon : UiIcons.Pickaxe), null, T(r.Title), Gold, T(status), st == RecState.Ready ? Good : st == RecState.Owned ? Dim : bad, T(r.Desc ?? "")));
        var cost = r.CostNow;
        if (cost != null) foreach (var c in cost) { int have = G.Inv[c[0]]; v.AddChild(Stat(UiIcons.Item(c[0]), It.Names[c[0]], $"{have}/{c[1]}", have >= c[1] ? Good : bad)); }
        v.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        if (r.Multi)
        {
            int max = G.MaxCraft(r);
            var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", 3); v.AddChild(h);
            h.AddChild(Btn("Создать 1", null, () => { G.EqCraftQty(idx, 1); Flash(centerBox); }, max >= 1, true));
            var b5 = Btn("×5", null, () => { G.EqCraftQty(idx, 5); Flash(centerBox); }, max >= 5); b5.SizeFlagsHorizontal = SizeFlags.Fill; b5.CustomMinimumSize = new Vector2(28, 0); h.AddChild(b5);
            var bm = Btn(max >= 2 ? $"{T("Макс")} ×{max}" : "Макс", null, () => { G.EqCraftQty(idx, max); Flash(centerBox); }, max >= 2); bm.SizeFlagsHorizontal = SizeFlags.Fill; bm.CustomMinimumSize = new Vector2(50, 0); h.AddChild(bm);
        }
        else v.AddChild(MainActionButtonUI.Make("Создать", () => { G.EqCraftRow(idx); Flash(centerBox); }, st == RecState.Ready, st == RecState.Owned ? T("Это уже у вас есть.") : null));
    }

    // ───────────────────────── вкладка «Фонарь» ─────────────────────────
    static readonly string[] LampStages = { "больше радиус света", "свет наносит урон врагам", "аура света вокруг героя" };
    static readonly string[] Roman = { "I", "II", "III" };

    void BuildLantern()
    {
        int lv = G.HeroLampLv;
        var c = Card(0); c.Visible = true; c.Bind(0, UiIcons.Tile(Art.Lantern), T("Фонарь героя"), new Color("#fff2b0"), $"{T("Ступень")} {lv} {T("из")} 3", Dim, true, 0, true);
        for (int i = 1; i < cards.Count; i++) cards[i].Visible = false; G.WinCur = 0;
        centerBox.AddChild(ItemDetailsCardUI.Make(UiIcons.Tile(Art.Lantern), null, T("Фонарь героя"), new Color("#fff2b0"), lv >= 3 ? T("Улучшен полностью") : $"{T("Ступень")} {lv} {T("из")} 3", lv >= 3 ? Good : Gold, T("Ваш личный свет: он идёт за вами в Тьму.")));
        var stepBar = new ProgressBar { MinValue = 0, MaxValue = 3, Value = lv, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 8), ThemeTypeVariation = "LightBar", MouseFilter = MouseFilterEnum.Ignore }; centerBox.AddChild(stepBar);
        if (G.HasHandLamp) centerBox.AddChild(Stat(UiIcons.Tile(Art.Lantern), "Радиус света", $"{(int)G.PlayerLightR}", Gold));
        else centerBox.AddChild(Lbl("Света пока нет: изучите «Искру света» в дереве навыков [T].", new Color("#ffb36a"), true));
        if (lv < 3)
        {
            var cost = Game.HeroCostFor(lv); bool bp = G.BpDone.Contains("bp_herolamp"); bool can = bp && G.Can(cost);
            string why = !bp ? T("Сначала найдите чертёж «Фонарь героя».") : !can ? T("Не хватает") + ": " + T(G.MissingText(cost)) : null;
            centerBox.AddChild(MainActionButtonUI.Make("Улучшить фонарь", () => { G.EqUpgradeLamp(); Flash(centerBox); }, can, why));
        }
        rightBox.AddChild(Lbl("Улучшения фонаря", Gold, false, "GoldLabel"));
        for (int i = 0; i < 3; i++)
        {
            bool done = i < lv, next = i == lv; var col = done ? Good : next ? Text : Dim;
            rightBox.AddChild(Lbl($"{(done ? "✓" : next ? ">" : "·")} {Roman[i]} — {T(LampStages[i])}", col, true, null, true));
        }
        if (lv < 3)
        {
            rightBox.AddChild(Lbl("Для следующей ступени", Gold, false, "GoldLabel"));
            foreach (var cc in Game.HeroCostFor(lv)) { int have = G.Inv[cc[0]]; rightBox.AddChild(Stat(UiIcons.Item(cc[0]), It.Names[cc[0]], $"{have}/{cc[1]}", have >= cc[1] ? Good : new Color("#ffb36a"))); }
        }
    }
}
