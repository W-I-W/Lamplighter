using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Выбор героя: карточки слева, справа — крупный спрайт, экипировка и характеристики выбранного героя. Нельзя начать игру без героя.</summary>
public partial class CharacterSelectionUI : FlowScreen
{
    public string SelectId;
    readonly List<CharInfo> list = new(); readonly Dictionary<string, Card> cards = new();
    VBoxContainer cardBox; Control detail; Button play, del; Label empty; string sel; CharacterSave selSave; bool walking;

    public override void _Ready()
    {
        base._Ready(); Header("Выбор героя", Flow.ModeLine);
        var lp = Px.Panel(); lp.Position = new Vector2(0, 38); lp.Size = new Vector2(270, 246); AddChild(lp);
        var sc = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever, Size = new Vector2(258, 234), Position = new Vector2(6, 6) }; lp.AddChild(new Control()); lp.GetChild<Control>(0).AddChild(sc);
        cardBox = new VBoxContainer(); cardBox.AddThemeConstantOverride("separation", 4); cardBox.CustomMinimumSize = new Vector2(250, 0); sc.AddChild(cardBox);
        empty = Px.Label("Героев пока нет.\nСоздайте первого — он сохранит оружие, предметы и навыки во всех мирах.", Px.Dim, false, HorizontalAlignment.Center, true); empty.Position = new Vector2(14, 90); empty.Size = new Vector2(240, 60); lp.AddChild(empty);
        var dp = Px.Panel(); dp.Position = new Vector2(280, 38); dp.Size = new Vector2(340, 246); AddChild(dp); detail = new Control { Size = dp.Size }; dp.AddChild(detail);

        AddChild(Px.Button("Назад", null, Back, 90).Also(b => b.Position = new Vector2(0, 294)));
        var r = Row(260, 294, 360, BoxContainer.AlignmentMode.End); AddChild(r);
        del = Px.Button("Удалить", "trash", AskDelete, 92); r.AddChild(del);
        r.AddChild(Px.Button("Создать героя", "plus", () => Flow.ShowCreateCharacter(false), 130));
        play = Px.Button(Flow.Kind == FlowKind.Guest ? "Подключиться" : "Играть", "play", PlaySel, 120); r.AddChild(play);
        Reload(SelectId);
        if (list.Count == 0) CallDeferred(nameof(AutoCreate));
    }

    void AutoCreate() { if (list.Count == 0) Flow.ShowCreateCharacter(true); }

    void Reload(string select)
    {
        foreach (var c in cardBox.GetChildren()) c.QueueFree(); cards.Clear(); list.Clear(); list.AddRange(SaveManager.ListCharacters());
        empty.Visible = list.Count == 0;
        foreach (var ci in list)
        {
            var card = new Card { Key = ci.Id, CustomMinimumSize = new Vector2(250, 50) }; var id = ci.Id;
            card.Clicked = () => Select(id); card.Activated = () => { Select(id); PlaySel(); };
            var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", 6); card.AddChild(Pad(h, 4));
            var slot = Px.Panel("Slot"); slot.CustomMinimumSize = new Vector2(38, 38); var lv = new LookView { Look = ci.Look, Lamp = ci.HasLamp, Zoom = 2, Position = new Vector2(3, 3) }; var holder = new Control { CustomMinimumSize = new Vector2(30, 30), MouseFilter = MouseFilterEnum.Ignore }; holder.AddChild(lv); slot.AddChild(Pad(holder, 1)); h.AddChild(slot);
            var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 1); v.SizeFlagsHorizontal = SizeFlags.ExpandFill; h.AddChild(v);
            v.AddChild(Px.Label(ci.Name, Px.Gold));
            v.AddChild(Px.Label(string.Format(Loc.T("ур. {0} · здор. {1} · навыки {2}"), ci.Level, Mathf.RoundToInt(ci.MaxHp), ci.SkillPts), Px.Text));
            v.AddChild(Px.Label(WeaponLine(ci) + " · " + Px.Time(ci.PlaySecs), Px.Dim));
            cardBox.AddChild(card); cards[ci.Id] = card;
        }
        sel = null; if (list.Count > 0) Select(select != null && cards.ContainsKey(select) ? select : list[0].Id, true);
        else { selSave = null; BuildDetail(); }
        UpdateButtons();
    }

    static string WeaponLine(CharInfo ci)
    {
        if (ci.Weapon != "") return Db.Weapon(ci.Weapon)?.Name is string n ? Loc.T(n) : ci.Weapon;
        return Loc.T(ToolDef.Get(ci.Tool)?.Name ?? "Кирка");
    }

    void Select(string id, bool quiet = false)
    {
        if (sel == id) return; sel = id; Flow.CharId = id; foreach (var kv in cards) kv.Value.Selected = kv.Key == id;
        selSave = SaveManager.LoadCharacter(id); BuildDetail(); UpdateButtons();
        if (!quiet && detail.GetChildCount() > 0) Px.Pop(detail.GetChild<Control>(0), .02f);
    }

    void UpdateButtons() { play.Disabled = sel == null; del.Disabled = sel == null; }

    void BuildDetail()
    {
        foreach (var c in detail.GetChildren()) c.QueueFree();
        if (selSave == null) { var l = Px.Label("Выберите героя или создайте нового", Px.Dim, false, HorizontalAlignment.Center, true); l.Position = new Vector2(20, 100); l.Size = new Vector2(300, 40); detail.AddChild(l); return; }
        var c0 = selSave; var root = new Control { Size = detail.Size, MouseFilter = MouseFilterEnum.Ignore }; detail.AddChild(root);
        var stage = new StageView { Position = new Vector2(8, 8), Size = new Vector2(126, 170), Zoom = 6, FloorY = 96 + 0 }; root.AddChild(stage);
        var lv = new LookView { Look = c0.Look ?? new Look(), Lamp = HasLampOf(c0), Zoom = 6, Walking = walking, Position = new Vector2(15, 20) }; stage.AddChild(lv);
        var nm = Px.Label(c0.Name, Px.Gold, true, HorizontalAlignment.Center); nm.Position = new Vector2(8, 182); nm.Size = new Vector2(126, 20); root.AddChild(nm);
        var wb = Px.Button(walking ? "Ходьба" : "Покой", "walk", null, 126, 20); wb.Position = new Vector2(8, 206); wb.Pressed += () => { walking = !walking; lv.Walking = walking; wb.Text = Loc.T(walking ? "Ходьба" : "Покой"); }; root.AddChild(wb);

        var v = new VBoxContainer { Position = new Vector2(144, 8), Size = new Vector2(188, 230) }; v.AddThemeConstantOverride("separation", 2); root.AddChild(v);
        float need = Tune.T.XpBase + Tune.T.XpLinear * c0.Level + Tune.T.XpQuad * c0.Level * c0.Level;
        v.AddChild(Px.Label(string.Format(Loc.T("Уровень героя {0}"), c0.Level), Px.Gold));
        var bar = new ProgressBar { ThemeTypeVariation = "XpBar", MinValue = 0, MaxValue = 1, Value = Mathf.Clamp(c0.Xp / Mathf.Max(1, need), 0, 1), ShowPercentage = false, CustomMinimumSize = new Vector2(150, 6) }; v.AddChild(bar);
        int skills = 0; if (c0.SkV != null) foreach (var s in c0.SkV) skills += s;
        Stat(v, "heart", string.Format(Loc.T("Здоровье: {0}"), Mathf.RoundToInt(c0.MaxHp)));
        Stat(v, "sword", string.Format(Loc.T("Изучено навыков: {0}"), skills));
        Stat(v, "skull", string.Format(Loc.T("Побеждено врагов: {0}"), c0.Kills));
        Stat(v, "clock", string.Format(Loc.T("Время игры: {0}"), Px.Time(c0.PlaySecs)));
        Stat(v, "world", string.Format(Loc.T("Миров посещено: {0}"), c0.Worlds?.Count ?? 0));
        v.AddChild(new HSeparator());
        v.AddChild(Px.Label("Экипировка", Px.Cyan));
        var tool = ToolDef.Get(c0.CurTool ?? "pickaxe"); v.AddChild(Px.Label("• " + Loc.T(tool?.Name ?? "Кирка"), Px.Text));
        if (c0.WId != null && c0.WId.Length > 0) { var cw = Db.Weapon(c0.WId[Math.Clamp(c0.CurW, 0, c0.WId.Length - 1)]); if (cw != null) v.AddChild(Px.Label("• " + Loc.T(cw.Name) + (c0.WId.Length > 1 ? $" (+{c0.WId.Length - 1})" : ""), Px.Good)); }
        else v.AddChild(Px.Label("• оружия пока нет", Px.Dim));
        int shown = 0; if (c0.Equipped != null) foreach (var rid in c0.Equipped) { if (shown++ >= 3) break; v.AddChild(Px.Label("◆ " + (Db.Relic(rid)?.Title ?? rid), Px.Cyan)); }
        if (shown == 0) v.AddChild(Px.Label("◆ реликвий нет", Px.Dim));
    }

    static void Stat(VBoxContainer v, string icon, string text)
    {
        var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", 4); var ic = new TextureRect { Texture = Px.Icon(icon), CustomMinimumSize = new Vector2(12, 12), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore };
        h.AddChild(ic); h.AddChild(Px.Label(text, Px.Text)); v.AddChild(h);
    }

    void PlaySel() { if (sel == null) { Sfx.Play("error"); return; } Flow.PickCharacter(sel); }

    void AskDelete()
    {
        if (sel == null) return;
        if (GameSession.Active && GameSession.CharacterId == sel) { Toast("Нельзя удалить героя активной игровой сессии"); return; }
        var name = selSave?.Name ?? ""; var id = sel;
        Modal.Ask(this, "Удалить героя?", string.Format(Loc.T("«{0}» будет удалён навсегда: предметы, оружие и навыки. Миры не пострадают."), name), "Удалить", () =>
        {
            SaveManager.DeleteCharacter(id); if (Flow.CharId == id) Flow.CharId = null; Toast("Герой удалён", true); Reload(null);
            if (list.Count == 0) CallDeferred(nameof(AutoCreate));
        });
    }

    public override bool OnKey(Key k)
    {
        if (list.Count > 0 && (k == Key.Up || k == Key.Down || k == Key.W || k == Key.S))
        {
            int i = Math.Max(0, list.FindIndex(c => c.Id == sel)); i = Math.Clamp(i + (k == Key.Up || k == Key.W ? -1 : 1), 0, list.Count - 1); Select(list[i].Id);
            if (cards.TryGetValue(list[i].Id, out var cd) && cd.GetParent()?.GetParent() is ScrollContainer sc) sc.EnsureControlVisible(cd);
            return true;
        }
        if (k == Key.Enter || k == Key.KpEnter) { PlaySel(); return true; }
        if (k == Key.Delete) { AskDelete(); return true; }
        if (k == Key.N) { Flow.ShowCreateCharacter(false); return true; }
        if (k == Key.Escape) { Back(); return true; }
        return false;
    }
    internal static bool HasLampOf(CharacterSave c) { if (c == null) return false; if (c.HeroLamp > 0) return true; if (c.SkK != null && c.SkV != null) for (int i = 0; i < c.SkK.Length && i < c.SkV.Length; i++) if (c.SkK[i] == "Lens" && c.SkV[i] > 0) return true; return false; }
}

/// <summary>Создание героя: имя, цвет кожи, причёска, цвет волос, два цвета одежды и фасон. Предпросмотр меняется сразу; внешность не влияет на характеристики.</summary>
public partial class CharacterCreationUI : FlowScreen
{
    public bool First;
    static readonly string[] Names = { "Алекс", "Марк", "Лена", "Ника", "Тимур", "Эльза", "Рэй", "Мира", "Кай", "Фрея", "Орион", "Вера" };
    readonly Look look = Game.DefaultLook(); LineEdit name; LookView view; Label hairName, varName; Control stage;
    readonly List<Swatch> skin = new(), hairC = new(), cl1 = new(), cl2 = new(); bool walking, flip;
    static readonly RandomNumberGenerator Rng = new();

    public override void _Ready()
    {
        base._Ready(); Rng.Randomize(); Header(First ? "Ваш первый герой" : "Новый герой", Flow.ModeLine);
        var lp = Px.Panel(); lp.Position = new Vector2(0, 38); lp.Size = new Vector2(250, 246); AddChild(lp);
        stage = new StageView { Position = new Vector2(6, 6), Size = new Vector2(238, 188), Zoom = 8, FloorY = 134 }; lp.AddChild(new Control()); lp.GetChild<Control>(0).AddChild(stage);
        view = new LookView { Look = look, Zoom = 8, Position = new Vector2(55, 6) }; stage.AddChild(view);
        var r = Row(8, 200, 234, BoxContainer.AlignmentMode.Center, 4); lp.GetChild<Control>(0).AddChild(r);
        r.AddChild(Px.Button("Покой", "walk", null, 76).Also(b => b.Pressed += () => { walking = !walking; view.Walking = walking; b.Text = Loc.T(walking ? "Ходьба" : "Покой"); Px.Pop(view, .06f); }));
        r.AddChild(Px.Button("Взгляд", "flip", () => { flip = !flip; view.Flip = flip; Px.Pop(view, .06f); }, 76));
        r.AddChild(Px.Button("Случайно", "dice", Randomize, 76));
        var hint = Px.Label("Внешность — только для красоты", Px.Dim, false, HorizontalAlignment.Center); hint.Position = new Vector2(0, 226); hint.Size = new Vector2(250, 12); lp.GetChild<Control>(0).AddChild(hint);

        var rp = Px.Panel(); rp.Position = new Vector2(260, 38); rp.Size = new Vector2(360, 246); AddChild(rp);
        var v = new VBoxContainer { Position = new Vector2(8, 8), Size = new Vector2(344, 230) }; v.AddThemeConstantOverride("separation", 5); rp.AddChild(new Control()); rp.GetChild<Control>(0).AddChild(v);
        name = new LineEdit { Text = Loc.T(Names[Rng.RandiRange(0, Names.Length - 1)]), MaxLength = 16, CustomMinimumSize = new Vector2(180, 20), PlaceholderText = Loc.T("Имя героя") }; Modal.Style(name);
        name.TextChanged += _ => { }; var nr = LabeledRow("Имя"); nr.AddChild(name); nr.AddChild(Px.Button("", "dice", () => { name.Text = Loc.T(Names[Rng.RandiRange(0, Names.Length - 1)]); Px.Pop(name, .04f); }, 24, 20));
        v.AddChild(nr.GetParent());
        AddSwatches(v, "Кожа", Look.SkinCols, skin, i => look.Skin = i, () => look.Skin);
        hairName = Px.Label("", Px.Gold, false, HorizontalAlignment.Center); AddCycler(v, "Причёска", hairName, d => { look.Hair = (look.Hair + d + Look.HairN) % Look.HairN; });
        AddSwatches(v, "Цвет волос", Look.HairCols, hairC, i => look.HairCol = i, () => look.HairCol);
        AddSwatches(v, "Одежда", Look.ClothCols, cl1, i => look.Cloth1 = i, () => look.Cloth1);
        AddSwatches(v, "Доп. цвет", Look.ClothCols, cl2, i => look.Cloth2 = i, () => look.Cloth2);
        varName = Px.Label("", Px.Gold, false, HorizontalAlignment.Center); AddCycler(v, "Фасон", varName, d => { look.Variant = (look.Variant + d + Look.VariantN) % Look.VariantN; });
        Refresh();

        AddChild(Px.Button("Назад", null, Back, 90).Also(b => b.Position = new Vector2(0, 294)));
        var br = Row(360, 294, 260, BoxContainer.AlignmentMode.End); AddChild(br); br.AddChild(Px.Button("Создать героя", "check", Create, 150));
    }

    HBoxContainer LabeledRow(string label)
    {
        var outer = new HBoxContainer(); outer.AddThemeConstantOverride("separation", 6); var l = Px.Label(label, Px.Dim); l.CustomMinimumSize = new Vector2(64, 0); outer.AddChild(l);
        var inner = new HBoxContainer(); inner.AddThemeConstantOverride("separation", 3); outer.AddChild(inner); return inner;
    }

    void AddSwatches(VBoxContainer v, string label, Color[] cols, List<Swatch> store, Action<int> set, Func<int> get)
    {
        var inner = LabeledRow(label);
        for (int i = 0; i < cols.Length; i++)
        {
            int idx = i; var sw = new Swatch { Col = cols[i], Clicked = () => { set(idx); Refresh(); Sfx.Play("tick", -5, 1.5f); Px.Pop(view, .05f); } };
            inner.AddChild(sw); store.Add(sw);
        }
        v.AddChild(inner.GetParent());
    }

    void AddCycler(VBoxContainer v, string label, Label value, Action<int> step)
    {
        var inner = LabeledRow(label); value.CustomMinimumSize = new Vector2(130, 0);
        inner.AddChild(Px.Button("", "left", () => { step(-1); Refresh(); Px.Pop(view, .05f); }, 26, 20));
        inner.AddChild(value);
        inner.AddChild(Px.Button("", "right", () => { step(1); Refresh(); Px.Pop(view, .05f); }, 26, 20));
        v.AddChild(inner.GetParent());
    }

    void Refresh()
    {
        look.Clamp(); view.Look = look;
        for (int i = 0; i < skin.Count; i++) skin[i].Selected = i == look.Skin; for (int i = 0; i < hairC.Count; i++) hairC[i].Selected = i == look.HairCol;
        for (int i = 0; i < cl1.Count; i++) cl1[i].Selected = i == look.Cloth1; for (int i = 0; i < cl2.Count; i++) cl2[i].Selected = i == look.Cloth2;
        hairName.Text = Loc.T(Look.HairNames[look.Hair]); varName.Text = Loc.T(Look.VariantNames[look.Variant]);
        foreach (var s in skin) s.QueueRedraw(); foreach (var s in hairC) s.QueueRedraw(); foreach (var s in cl1) s.QueueRedraw(); foreach (var s in cl2) s.QueueRedraw();
    }

    void Randomize() { var r = Look.Random(Rng); look.Skin = r.Skin; look.Hair = r.Hair; look.HairCol = r.HairCol; look.Cloth1 = r.Cloth1; look.Cloth2 = r.Cloth2; look.Variant = r.Variant; Refresh(); Px.Pop(view, .1f); Sfx.Play("tick", -3, 1.6f); }

    bool creating;
    void Create()
    {
        if (creating) return; var n = SaveManager.CleanName(name.Text, ""); if (n == "") { Sfx.Play("error"); Px.Shake(name); name.GrabFocus(); return; }
        creating = true; var c = SaveManager.CreateCharacter(n, look);
        Sfx.Play("upgrade", -3, 1.3f); view.Walking = false; var t = CreateTween(); var p0 = view.Position;
        t.TweenProperty(view, "position:y", p0.Y - 18, .14f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out); t.TweenProperty(view, "position:y", p0.Y, .14f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        t.TweenInterval(.15f); t.TweenCallback(Callable.From(() => Flow.ShowCharacters(c.Id)));
    }

    public override bool OnKey(Key k)
    {
        if (k == Key.Escape) { Back(); return true; }
        if (k == Key.Enter || k == Key.KpEnter) { Create(); return true; }
        return false;
    }


    public override void Back()
    {
        if (SaveManager.ListCharacters().Count == 0) { Flow.Leave(); return; }          // героев нет: вернуться можно только в меню
        Flow.ShowCharacters(null);
    }
}
