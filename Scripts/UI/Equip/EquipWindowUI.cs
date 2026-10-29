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
    Control panel; HBoxContainer tabsBox; VBoxContainer list, centerBox, rightBox; ScrollContainer scroll, rscroll; Label title, closeHint, foot;
    readonly List<Button> tabs = new(); readonly List<EquipCardUI> cards = new(); readonly List<EquipSocketUI> sockets = new();
    PackedScene cardScene, socketScene; bool wasOpen; string sig = ""; float rebuildT; int lastTab = -1; Tween openTw, bodyTw;
    static readonly string[] TabNames = { "Оружие", "Реликвии", "Создание", "Фонарь" };

    public override void _Ready()
    {
        ZIndex = 4040; panel = GetNode<Control>("%Panel"); tabsBox = GetNode<HBoxContainer>("%Tabs"); list = GetNode<VBoxContainer>("%List"); scroll = GetNode<ScrollContainer>("%Scroll");
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
        h.AddChild(Icon(ic, 12)); var n = Lbl(name, Dim); n.SizeFlagsHorizontal = SizeFlags.ExpandFill; h.AddChild(n);
        h.AddChild(Lbl(value, valCol ?? Text));
        if (delta != null) h.AddChild(Lbl(delta, better ? Good : Bad));
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
        if (!show) { wasOpen = false; return; }
        if (!wasOpen)
        {
            wasOpen = true; sig = ""; lastTab = -1; panel.PivotOffset = panel.Size / 2; panel.Modulate = new Color(1, 1, 1, 0); panel.Scale = new Vector2(.96f, .96f);
            openTw?.Kill(); openTw = CreateTween().SetParallel(); openTw.TweenProperty(panel, "modulate", Colors.White, .12f); openTw.TweenProperty(panel, "scale", Vector2.One, .12f);   // короткое открытие
        }
        rebuildT -= (float)delta;
        string s = Signature(); if (s == sig && rebuildT > 0) return; sig = s; rebuildT = .25f; Refresh();
    }

    string Signature()
    {
        var sb = new StringBuilder(); sb.Append(G.WinTab).Append('|').Append(G.WinCur).Append('|').Append(G.EqSocket).Append('|').Append(G.EqPick).Append('|').Append(G.EqShowAll).Append('|').Append(G.CurW).Append('|').Append(G.CurTool).Append('|').Append(G.HotSel).Append('|').Append(Loc.Lang);
        foreach (var h in G.Hot) sb.Append(h).Append(','); foreach (var m in G.ModInv) sb.Append(m).Append(',');
        sb.Append(G.OwnedRelics.Count).Append('|').Append(G.EquippedRelics.Count).Append('|').Append(G.CraftQty).Append('|').Append(G.CraftFold.Count).Append('|').Append(G.HeroLampLv).Append('|').Append(G.Tier).Append('|').Append(G.Owned.Count);
        foreach (var o in G.Owned) sb.Append(o.Mods.Count).Append('.');
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
        title.Text = T("Экипировка"); closeHint.Text = T("Esc — закрыть"); foot.Text = T("←/→ вкладки · ↑/↓ выбор · двойной щелчок — главное действие");
        RefreshTabs();
        Clear(centerBox); Clear(rightBox);
        switch (G.WinTab) { case 0: BuildWeapons(); break; case 1: BuildRelics(); break; case 2: BuildCraft(); break; default: BuildLantern(); break; }
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
            var b = tabs[i]; b.Text = T(TabNames[i]); b.Icon = icons[i]; bool act = i == G.WinTab;
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
            if (i < nt) { var td = ToolDef.Get(tl[i]); string nm = tl[i] == "pickaxe" ? Game.PickNames[G.Tier] : td.Name; c.Bind(i, UiIcons.Item(td.Icon), T(nm), Text, T("Инструмент"), Dim, held, hs, i == G.WinCur); }
            else { var d = Db.Weapon(G.Owned[i - nt].Id); c.Bind(i, UiIcons.Item(G.HotIcon(Game.RefWeapon(d.Id))), T(d.Name), Db.RarCol[(int)d.Rar], $"{T(Db.ClassNames[(int)d.Cls])} · {T(Db.RarName[(int)d.Rar])}", Dim, held, hs, i == G.WinCur); }
        }
        for (int i = total; i < cards.Count; i++) cards[i].Visible = false;
        if (G.WinCur < nt) BuildToolDetails(tl[G.WinCur]); else BuildWeaponDetails(G.WinCur - nt);
    }

    void BuildHotbarAssign(int row)
    {
        var r = G.EquipRef(row); if (r == null) return; int cur = G.HotSlotOf(r);
        centerBox.AddChild(Lbl("Назначить в слот хотбара", Dim, false, "DimLabel"));
        var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", 3); centerBox.AddChild(h);
        for (int s = 0; s < Game.HotCount; s++)
        {
            int slot = s; var b = new Button { Text = (s + 1).ToString(), FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(34, 0), SizeFlagsHorizontal = SizeFlags.ExpandFill };
            if (cur == s) { b.AddThemeColorOverride("font_color", Gold); b.Text = "[" + (s + 1) + "]"; b.TooltipText = T("Сейчас в этом слоте"); }
            else { var occ = G.Hot[s]; b.TooltipText = occ != null ? T("Заменит") + ": " + T(G.HotName(occ)) : T("Свободный слот"); }
            b.Pressed += () => { G.EqHotAssign(row, slot); Flash(centerBox); }; h.AddChild(b);
        }
        centerBox.AddChild(Lbl("Shift + 1–3", Dim, false, "DimLabel"));
    }

    void BuildToolDetails(string id)
    {
        int row = G.WinCur; var td = ToolDef.Get(id); string nm = id == "pickaxe" ? Game.PickNames[G.Tier] : td.Name; bool held = G.EqIsHeld(row);
        var head = new HBoxContainer(); head.AddThemeConstantOverride("separation", 6); head.AddChild(Icon(UiIcons.Item(td.Icon), 32));
        var tt = new VBoxContainer(); tt.AddChild(Lbl(nm, Gold, true, "GoldLabel")); tt.AddChild(Lbl(held ? "Надето" : "Инструмент", held ? Good : Dim)); head.AddChild(tt); centerBox.AddChild(head);
        centerBox.AddChild(Stat(UiIcons.Item(UiIcons.Sword), "Урон врагам", $"{Mathf.RoundToInt(td.CombatDamage * 100)}%"));
        centerBox.AddChild(Stat(UiIcons.Item(UiIcons.Pickaxe), "Добыча", $"×{td.MiningPower:0.#}", Cyan));
        centerBox.AddChild(Btn(held ? "Уже в руках" : "Экипировать", "Enter", () => { G.EqEquipRow(row); Flash(centerBox); }, !held, true));
        if (G.HotSlotOf(Game.RefTool(id)) >= 0) centerBox.AddChild(Btn("Убрать из хотбара", "X", () => G.EqHotClear(row)));
        BuildHotbarAssign(row);
        rightBox.AddChild(Lbl(td.Desc, Text, true));
        rightBox.AddChild(Lbl("Эффективность", Gold, false, "GoldLabel"));
        foreach (var (n, v) in new[] { ("Дерево", td.EffWood), ("Камень", td.EffStone), ("Руда", td.EffOre), ("Завалы", td.EffRubble) }) rightBox.AddChild(Stat(UiIcons.Item(UiIcons.Pickaxe), n, v <= 0 ? "—" : $"×{v:0.#}", v <= 0 ? Dim : Text));
    }

    void BuildWeaponDetails(int idx)
    {
        if (idx < 0 || idx >= G.Owned.Count) return; var ow = G.Owned[idx]; var w = Db.Weapon(ow.Id); int row = G.ToolList().Count + idx; bool held = G.EqIsHeld(row);
        var head = new HBoxContainer(); head.AddThemeConstantOverride("separation", 6); head.AddChild(Icon(UiIcons.Item(G.HotIcon(Game.RefWeapon(w.Id))), 32, null));
        var tt = new VBoxContainer(); tt.AddChild(Lbl(w.Name, Db.RarCol[(int)w.Rar], true)); tt.AddChild(Lbl($"{T(Db.ClassNames[(int)w.Cls])} · {T(Db.RarName[(int)w.Rar])}", Dim)); head.AddChild(tt); centerBox.AddChild(head);
        float dmg = G.WeaponDmg(w), rate = 1f / w.Cd; var cmp = held ? null : HeldWeaponDef();
        string D(float a, float b2, string fmt = "0.0#") { float d = a - b2; return Mathf.Abs(d) < .005f ? null : (d > 0 ? "+" : "") + d.ToString(fmt); }
        centerBox.AddChild(Stat(UiIcons.Item(UiIcons.Sword), "Урон", dmg.ToString("0.#"), Text, cmp == null ? null : D(dmg, G.WeaponDmg(cmp)), cmp == null || dmg >= G.WeaponDmg(cmp)));
        centerBox.AddChild(Stat(UiIcons.Item(UiIcons.Hammer), "Скорость", $"{rate:0.0}/{T("с")}", Text, cmp == null ? null : D(rate, 1f / cmp.Cd, "0.0"), cmp == null || rate >= 1f / cmp.Cd));
        if (w.Cls == WClass.Sword) centerBox.AddChild(Stat(UiIcons.Item(UiIcons.Pickaxe), "Дальность", $"{w.Reach:0}", Text, cmp != null && cmp.Cls == WClass.Sword ? D(w.Reach, cmp.Reach, "0") : null, cmp == null || w.Reach >= cmp.Reach));
        else centerBox.AddChild(Stat(UiIcons.Item(UiIcons.Bow), "Скорость снаряда", $"{w.Speed:0}", Text, cmp != null && cmp.Cls != WClass.Sword ? D(w.Speed, cmp.Speed, "0") : null, cmp == null || w.Speed >= cmp.Speed));
        centerBox.AddChild(Stat(UiIcons.Item(It.Shard), "Слоты вставок", $"{ow.Mods.Count}/{w.Slots}", Cyan));
        // ячейки вставок
        var sh = new HBoxContainer(); sh.AddThemeConstantOverride("separation", 4); centerBox.AddChild(sh);
        while (sockets.Count < w.Slots) sockets.Add(null);
        for (int s = 0; s < w.Slots; s++)
        {
            var so = socketScene.Instantiate<EquipSocketUI>(); sh.AddChild(so); int si = s;
            if (s < ow.Mods.Count) { var m = Db.Mods[ow.Mods[s]]; so.Bind(s, UiIcons.Item(Game.ModIcon(m.Id)), Game.ModTint(m.Id), G.EqSocket == s, $"{T(m.Name)}\n{T(m.Desc)}\n{T("ПКМ — снять")}"); }
            else so.Bind(s, null, Colors.White, G.EqSocket == s, T("Пустая ячейка: нажмите, чтобы выбрать вставку"));
            so.Picked += x => { G.EqSocket = x.Index; G.EqPick = -1; Sfx.Play("tick", -8, 1.5f); }; so.Removed += x => { if (x.Index < ow.Mods.Count && held) { x.Flash(Cyan); G.EqRemove(x.Index); } };
        }
        centerBox.AddChild(Btn(held ? "Уже в руках" : "Экипировать", "Enter", () => { G.EqEquipRow(row); Flash(centerBox); }, !held, true));
        if (G.HotSlotOf(Game.RefWeapon(w.Id)) >= 0) centerBox.AddChild(Btn("Убрать из хотбара", "X", () => G.EqHotClear(row)));
        BuildHotbarAssign(row);
        // правая панель: описание, эффекты, выбор вставки
        rightBox.AddChild(Lbl(w.Desc, Text, true));
        if (held) { var eff = G.EffectsText(); if (eff != "") { rightBox.AddChild(Lbl("Эффекты", Gold, false, "GoldLabel")); rightBox.AddChild(Lbl(eff, Cyan, true)); } }
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
        bool dim = have.HasValue && have.Value <= 0; h.AddChild(Icon(UiIcons.Item(Game.ModIcon(m.Id)), 16, dim ? Game.ModTint(m.Id) * new Color(1, 1, 1, .45f) : Game.ModTint(m.Id)));
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
            c.Bind(i, UiIcons.Item(It.Relic), T(d.Title), Db.RarCol[d.Rarity], T(Db.RarName[d.Rarity]), Dim, G.EquippedRelics.Contains(d.Id), 0, i == G.WinCur, false, d.Tint);
        }
        for (int i = n; i < cards.Count; i++) cards[i].Visible = false;
        centerBox.AddChild(Lbl($"{T("Слоты")}: {G.EquippedRelics.Count}/{G.RelicSlots}", Dim, false, "DimLabel", true));
        var sh = new HBoxContainer(); sh.AddThemeConstantOverride("separation", 3); centerBox.AddChild(sh);
        for (int s = 0; s < G.RelicSlots; s++) { var sq = new PanelContainer { ThemeTypeVariation = s < G.EquippedRelics.Count ? "SlotOk" : "Slot", CustomMinimumSize = new Vector2(20, 20) }; if (s < G.EquippedRelics.Count) { var rd = Db.Relic(G.EquippedRelics[s]); var ti = Icon(UiIcons.Item(It.Relic), 14, rd?.Tint); ti.TooltipText = rd != null ? T(rd.Title) : ""; sq.AddChild(ti); sq.TooltipText = rd != null ? T(rd.Title) : ""; sq.MouseFilter = MouseFilterEnum.Stop; } sh.AddChild(sq); }
        if (n == 0) { centerBox.AddChild(Lbl("Реликвий нет: ищите в сундуках и у торговцев", Dim, true)); return; }
        var rel = Db.Relic(G.OwnedRelics[G.WinCur]); if (rel == null) return; bool eq = G.EquippedRelics.Contains(rel.Id); int cur = G.WinCur;
        var head = new HBoxContainer(); head.AddThemeConstantOverride("separation", 6); head.AddChild(Icon(UiIcons.Item(It.Relic), 32, rel.Tint));
        var tt = new VBoxContainer(); tt.AddChild(Lbl(rel.Title, Db.RarCol[rel.Rarity], true)); tt.AddChild(Lbl(Db.RarName[rel.Rarity], Dim)); head.AddChild(tt); centerBox.AddChild(head);
        bool full = !eq && G.EquippedRelics.Count >= G.RelicSlots;
        centerBox.AddChild(Btn(eq ? "Снять" : "Надеть", "Enter", () => { G.EqToggleRelic(G.OwnedRelics[cur]); Flash(centerBox); }, eq || !full, true));
        if (full) centerBox.AddChild(Lbl("Все слоты заняты: сначала снимите другую реликвию.", Bad, true));
        rightBox.AddChild(Lbl(rel.Desc, Text, true));
        rightBox.AddChild(Lbl("Свойства", Gold, false, "GoldLabel"));
        rightBox.AddChild(Lbl(rel.Tags.Length > 0 ? string.Join(", ", rel.Tags) : "без тегов", Cyan, true));
        if (eq) { var el = new List<string>(); foreach (var e in rel.Effects) el.Add(e); if (el.Count > 0) { rightBox.AddChild(Lbl("Активный эффект", Gold, false, "GoldLabel")); rightBox.AddChild(Lbl(string.Join(", ", el), Good, true)); } }
        var tc = new List<string>(); foreach (var kv in G.TagCount) tc.Add($"{kv.Key} ×{kv.Value}");
        if (tc.Count > 0) { rightBox.AddChild(Lbl("Теги надетых", Gold, false, "GoldLabel")); rightBox.AddChild(Lbl(string.Join(", ", tc), Dim, true)); }
    }

    // ───────────────────────── вкладка «Создание» ─────────────────────────
    void BuildCraft()
    {
        var rows = G.CraftRows(); G.WinCur = Mathf.Clamp(G.WinCur, 0, Math.Max(0, rows.Count - 1));
        Texture2D GroupIcon(string g) => g == "hand" ? UiIcons.Item(UiIcons.Pickaxe) : g == "furnace" ? UiIcons.Tile(Art.Furnace) : UiIcons.Tile(Art.Forge);
        for (int i = 0; i < rows.Count; i++)
        {
            var (grp, rec) = rows[i]; var c = Card(i); c.Visible = true;
            if (rec == null) { bool fold = G.CraftFold.Contains(grp); c.Bind(i, GroupIcon(grp), (fold ? "+ " : "- ") + T(Game.GroupName(grp)), Gold, $"{G.RecipesOf(grp).Count}", Dim, false, 0, i == G.WinCur); continue; }
            var st = G.StateOf(rec); string sub = st == RecState.Owned ? T("есть") : st == RecState.Missing ? T("не хватает") : (rec.Extra != null && rec.Extra() > 0 ? $"{T("в очереди")} {rec.Extra()}" : T("можно создать"));
            c.Bind(i, rec.Icon >= 0 ? UiIcons.Item(rec.Icon) : GroupIcon(grp), T(rec.Name), st == RecState.Ready ? Good : st == RecState.Owned ? Dim : Text, sub, st == RecState.Missing ? Bad : Dim, st == RecState.Owned, 0, i == G.WinCur, st == RecState.Owned);
        }
        for (int i = rows.Count; i < cards.Count; i++) cards[i].Visible = false;
        if (rows.Count == 0) return; var (sg, sr) = rows[G.WinCur]; int cur = G.WinCur;
        if (sr == null) { centerBox.AddChild(Lbl(Game.GroupName(sg), Gold, true, "GoldLabel")); centerBox.AddChild(Lbl($"{T("Рецептов")}: {G.RecipesOf(sg).Count}", Dim, false, null, true)); centerBox.AddChild(Btn(G.CraftFold.Contains(sg) ? "Развернуть" : "Свернуть", "Enter", () => G.EqCraftRow(cur), true, true)); return; }
        var stt = G.StateOf(sr);
        var head = new HBoxContainer(); head.AddThemeConstantOverride("separation", 6); head.AddChild(Icon(sr.Icon >= 0 ? UiIcons.Item(sr.Icon) : GroupIcon(sg), 32));
        var tt = new VBoxContainer(); tt.AddChild(Lbl(sr.Name, stt == RecState.Ready ? Good : Gold, true)); tt.AddChild(Lbl(stt == RecState.Owned ? "Уже есть" : stt == RecState.Ready ? "Можно создать" : "Не хватает материалов", stt == RecState.Missing ? Bad : Dim)); head.AddChild(tt); centerBox.AddChild(head);
        var cost = sr.CostNow; if (cost != null) { centerBox.AddChild(Lbl("Нужно", Dim, false, "DimLabel")); foreach (var c in cost) { int have = G.Inv[c[0]]; centerBox.AddChild(Stat(UiIcons.Item(c[0]), It.Names[c[0]], $"{have}/{c[1]}", have >= c[1] ? Good : Bad)); } }
        if (sr.Multi) centerBox.AddChild(Btn($"{T("Количество")}: ×{(Game.QtySteps[G.CraftQty] == int.MaxValue ? T("макс.") + $" ({Math.Max(1, G.MaxCraft(sr))})" : Game.QtySteps[G.CraftQty].ToString())}", "Q", () => G.EqCycleQty()));
        centerBox.AddChild(Btn("Создать", "Enter", () => { G.EqCraftRow(cur); Flash(centerBox); }, stt == RecState.Ready, true));
        rightBox.AddChild(Lbl(sr.Desc ?? "", Text, true)); rightBox.AddChild(Lbl(sg == "hand" ? "Создаётся в любом месте." : "Нужна печь или кузница рядом.", Dim, true));
    }

    // ───────────────────────── вкладка «Фонарь» ─────────────────────────
    void BuildLantern()
    {
        var c = Card(0); c.Visible = true; c.Bind(0, UiIcons.Tile(Art.Lantern), T("Фонарь героя"), new Color("#fff2b0"), $"{T("Уровень")} {G.HeroLampLv}/3", Dim, true, 0, true);
        for (int i = 1; i < cards.Count; i++) cards[i].Visible = false; G.WinCur = 0;
        var head = new HBoxContainer(); head.AddThemeConstantOverride("separation", 6); head.AddChild(Icon(UiIcons.Tile(Art.Lantern), 32));
        var tt = new VBoxContainer(); tt.AddChild(Lbl("Фонарь героя", new Color("#fff2b0"), true)); tt.AddChild(Lbl($"{T("Уровень")} {G.HeroLampLv}/3", Gold)); head.AddChild(tt); centerBox.AddChild(head);
        centerBox.AddChild(Stat(UiIcons.Tile(Art.Lantern), "Радиус света", $"{(int)G.PlayerLightR} px", Gold));
        var bar = new ProgressBar { MinValue = 0, MaxValue = 320, Value = G.PlayerLightR, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 8), ThemeTypeVariation = "LightBar", MouseFilter = MouseFilterEnum.Ignore }; centerBox.AddChild(bar);
        if (G.HeroLampLv < 3)
        {
            var cost = Game.HeroCostFor(G.HeroLampLv); bool bp = G.BpDone.Contains("bp_herolamp");
            centerBox.AddChild(Lbl("Следующий уровень", Dim, false, "DimLabel")); foreach (var cc in cost) { int have = G.Inv[cc[0]]; centerBox.AddChild(Stat(UiIcons.Item(cc[0]), It.Names[cc[0]], $"{have}/{cc[1]}", have >= cc[1] ? Good : Bad)); }
            centerBox.AddChild(Btn("Улучшить", "Enter", () => { G.EqUpgradeLamp(); Flash(centerBox); }, bp && G.Can(cost), true));
            if (!bp) centerBox.AddChild(Lbl("Нужен чертёж «Фонарь героя» (журнал [J])", Bad, true));
        }
        else centerBox.AddChild(Lbl("Максимальный уровень", Good));
        rightBox.AddChild(Lbl("Улучшения", Gold, false, "GoldLabel"));
        string[] ups = { "Уровень 1: +12% радиуса света", "Уровень 2: свет жжёт врагов", "Уровень 3: аура света вокруг героя" };
        for (int i = 0; i < 3; i++) rightBox.AddChild(Lbl((i < G.HeroLampLv ? "✓ " : "· ") + T(ups[i]), i < G.HeroLampLv ? Good : Dim, true));
        int cry = 0; foreach (var l in G.Lamps) if (l.Kind == 2) cry++;
        rightBox.AddChild(Lbl($"{T("Фонарей")}: {G.Lamps.Count} ({T("кристальных")} {cry}) · {T("кристаллы +12% (до 3)")}", Dim, true, null, true));
    }
}
