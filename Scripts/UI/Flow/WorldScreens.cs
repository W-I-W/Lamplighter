using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Выбор мира: карточки с миниатюрой карты, seed, сложностью, временем и исследованием. Хост дополнительно выбирает тип лобби. Удаление мира не затрагивает героев.</summary>
public partial class WorldSelectionUI : FlowScreen
{
    public string SelectId;
    readonly List<WorldInfo> list = new(); readonly Dictionary<string, Card> cards = new();
    VBoxContainer cardBox; Control detail; Button play, del, ren; Label empty; string sel; Button lobbyBtn;

    public override void _Ready()
    {
        base._Ready(); Header("Выбор мира", Flow.ModeLine + " · " + (SaveManager.LoadCharacter(Flow.CharId)?.Name ?? ""));
        var lp = Px.Panel(); lp.Position = new Vector2(0, 38); lp.Size = new Vector2(270, 246); AddChild(lp);
        var sc = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever, Size = new Vector2(258, 234), Position = new Vector2(6, 6) }; lp.AddChild(new Control()); lp.GetChild<Control>(0).AddChild(sc);
        cardBox = new VBoxContainer(); cardBox.AddThemeConstantOverride("separation", 4); cardBox.CustomMinimumSize = new Vector2(250, 0); sc.AddChild(cardBox);
        empty = Px.Label("Миров пока нет.\nСоздайте первый мир: он сохранит постройки, фонари и жителей независимо от героев.", Px.Dim, false, HorizontalAlignment.Center, true); empty.Position = new Vector2(14, 90); empty.Size = new Vector2(240, 60); lp.AddChild(empty);
        var dp = Px.Panel(); dp.Position = new Vector2(280, 38); dp.Size = new Vector2(340, 246); AddChild(dp); detail = new Control { Size = dp.Size }; dp.AddChild(detail);

        AddChild(Px.Button("Назад", null, Back, 90).Also(b => b.Position = new Vector2(0, 294)));
        var r = Row(100, 294, 520, BoxContainer.AlignmentMode.End); AddChild(r);
        ren = Px.Button("Переименовать", "edit", AskRename, 130); r.AddChild(ren);
        del = Px.Button("Удалить", "trash", AskDelete, 92); r.AddChild(del);
        r.AddChild(Px.Button("Создать мир", "plus", () => Flow.ShowCreateWorld(), 120));
        play = Px.Button(Flow.Kind == FlowKind.Host ? "Создать лобби" : "Играть", Flow.Kind == FlowKind.Host ? "lobby" : "play", PlaySel, 130); r.AddChild(play);
        Reload(SelectId);
        if (list.Count == 0) CallDeferred(nameof(AutoCreate));
    }

    void AutoCreate() { if (list.Count == 0) Flow.ShowCreateWorld(); }

    void Reload(string select)
    {
        foreach (var c in cardBox.GetChildren()) c.QueueFree(); cards.Clear(); list.Clear(); list.AddRange(SaveManager.ListWorlds());
        empty.Visible = list.Count == 0;
        foreach (var wi in list)
        {
            var card = new Card { Key = wi.Id, CustomMinimumSize = new Vector2(250, 50) }; var id = wi.Id;
            card.Clicked = () => Select(id); card.Activated = () => { Select(id); PlaySel(); };
            var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", 6); card.AddChild(Pad(h, 4));
            h.AddChild(Thumb(wi.Id, new Vector2(80, 16)));
            var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 1); v.SizeFlagsHorizontal = SizeFlags.ExpandFill; h.AddChild(v);
            v.AddChild(Px.Label(wi.Name, Px.Gold));
            v.AddChild(Px.Label($"Seed {wi.Seed} · {Px.DiffName(wi.Difficulty)}", wi.Difficulty == 1 ? Px.Warn : Px.Text));
            v.AddChild(Px.Label(Px.Time(wi.PlaySecs) + " · " + string.Format(Loc.T("карта {0}%"), (int)(SaveManager.ExploredFrac(wi) * 100)), Px.Dim));
            cardBox.AddChild(card); cards[wi.Id] = card;
        }
        sel = null; if (list.Count > 0) Select(select != null && cards.ContainsKey(select) ? select : list[0].Id, true); else BuildDetail();
        UpdateButtons();
    }

    internal static Control Thumb(string id, Vector2 size)
    {
        var tex = SaveManager.LoadThumb(id); var fr = Px.Panel("Slot"); var tr = new TextureRect { Texture = tex ?? Px.Icon("world"), CustomMinimumSize = size, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = tex != null ? TextureRect.StretchModeEnum.Scale : TextureRect.StretchModeEnum.KeepAspectCentered, TextureFilter = TextureFilterEnum.Nearest, MouseFilter = MouseFilterEnum.Ignore };
        fr.AddChild(Pad(tr, 1)); fr.MouseFilter = MouseFilterEnum.Ignore; return fr;
    }

    WorldInfo Sel => list.Find(w => w.Id == sel);

    void Select(string id, bool quiet = false)
    {
        if (sel == id) return; sel = id; Flow.WorldId = id; foreach (var kv in cards) kv.Value.Selected = kv.Key == id; BuildDetail(); UpdateButtons();
        if (!quiet && detail.GetChildCount() > 0) Px.Pop(detail.GetChild<Control>(0), .02f);
    }

    void UpdateButtons() { bool has = sel != null; play.Disabled = !has; del.Disabled = !has; ren.Disabled = !has; }

    void BuildDetail()
    {
        foreach (var c in detail.GetChildren()) c.QueueFree(); var w = Sel;
        if (w == null) { var l = Px.Label("Выберите мир или создайте новый", Px.Dim, false, HorizontalAlignment.Center, true); l.Position = new Vector2(20, 100); l.Size = new Vector2(300, 40); detail.AddChild(l); return; }
        var root = new Control { Size = detail.Size, MouseFilter = MouseFilterEnum.Ignore }; detail.AddChild(root);
        var th = Thumb(w.Id, new Vector2(280, 56)); th.Position = new Vector2(10, 10); root.AddChild(th);      // целиком внутри рамки панели
        var nm = Px.Label(w.Name, Px.Gold, true, HorizontalAlignment.Left); nm.Position = new Vector2(10, 90); nm.Size = new Vector2(320, 20); root.AddChild(nm);
        var v = new VBoxContainer { Position = new Vector2(10, 114), Size = new Vector2(320, 120) }; v.AddThemeConstantOverride("separation", 2); root.AddChild(v);
        void Stat(string icon, string text, Color c) { var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", 4); h.AddChild(new TextureRect { Texture = Px.Icon(icon), CustomMinimumSize = new Vector2(12, 12), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore }); h.AddChild(Px.Label(text, c)); v.AddChild(h); }
        Stat("dice", "Seed: " + w.Seed, Px.Text);
        Stat(w.Difficulty == 1 ? "skull" : "shield", Loc.T("Сложность: ") + Px.DiffName(w.Difficulty), w.Difficulty == 1 ? Px.Warn : Px.Text);
        Stat("clock", Loc.T("Время игры: ") + Px.Time(w.PlaySecs), Px.Text);
        Stat("world", string.Format(Loc.T("Исследовано: {0}% · маяк {1}/6"), (int)(SaveManager.ExploredFrac(w) * 100), w.Beacon), Px.Text);
        Stat("lobby", string.Format(Loc.T("Зажжено древних фонарей: {0}"), w.Lit) + (w.Finished ? " · " + Loc.T("пройден") : ""), w.Finished ? Px.Good : Px.Text);
        if (Flow.Kind == FlowKind.Host)
        {
            v.AddChild(new HSeparator());
            var r = new HBoxContainer(); r.AddThemeConstantOverride("separation", 6); v.AddChild(r);
            if (Flow.Transport == "steam")
            {
                lobbyBtn = Px.Button(LobbyText(), "users", () => { Flow.FriendsOnly = !Flow.FriendsOnly; lobbyBtn.Text = LobbyText(); }, 190); r.AddChild(lobbyBtn);
            }
            r.AddChild(Px.Label(string.Format(Loc.T("до {0} игроков"), NetConst.MaxPlayers), Px.Dim));
        }
    }

    string LobbyText() => Loc.T(Flow.FriendsOnly ? "Лобби: только друзья" : "Лобби: публичное");

    void PlaySel()
    {
        if (sel == null) { Sfx.Play("error"); return; }
        var w = SaveManager.LoadWorld(sel); if (w == null) { Toast("Файл мира повреждён"); Reload(null); return; }
        Flow.PlayWorld(sel);
    }

    void AskRename()
    {
        var w = Sel; if (w == null) return; var id = w.Id;
        Modal.Prompt(this, "Переименовать мир", "Новое название мира:", w.Name, "Сохранить", n =>
        {
            var save = SaveManager.LoadWorld(id); if (save == null) return; save.Name = SaveManager.CleanName(n, save.Name); SaveManager.SaveWorld(save); Toast("Мир переименован", true); Reload(id);
        });
    }

    void AskDelete()
    {
        var w = Sel; if (w == null) return;
        if (GameSession.Active && GameSession.WorldId == w.Id) { Toast("Нельзя удалить мир активной игровой сессии"); return; }
        var id = w.Id;
        Modal.Ask(this, "Удалить мир?", string.Format(Loc.T("Мир «{0}» будет удалён навсегда вместе с постройками и фонарями. Герои останутся."), w.Name), "Удалить", () =>
        {
            SaveManager.DeleteWorld(id); if (Flow.WorldId == id) Flow.WorldId = null; Toast("Мир удалён", true); Reload(null);
            if (list.Count == 0) CallDeferred(nameof(AutoCreate));
        });
    }

    public override void Back() => Flow.ShowCharacters(null);

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
        if (k == Key.F2) { AskRename(); return true; }
        if (k == Key.N) { Flow.ShowCreateWorld(); return true; }
        if (k == Key.Escape) { Back(); return true; }
        return false;
    }
}

/// <summary>Создание мира: название, сложность и свёрнутые дополнительные настройки (seed). Пустой seed выбирается случайно. После создания мир сразу запускается с выбранным героем.</summary>
public partial class WorldCreationUI : FlowScreen
{
    static readonly string[] Names = { "Тёмный лес", "Новое начало", "Мир друзей", "Долина фонарей", "Ночная застава", "Пепельный край", "Огни севера", "Тихая гавань" };
    static readonly RandomNumberGenerator Rng = new();
    LineEdit name, seed; int difficulty; Button bNormal, bHard, bMore; Control adv; PanelContainer panel; bool open;

    public override void _Ready()
    {
        base._Ready(); Rng.Randomize(); Header("", "");
        panel = Px.Panel("PanelGold"); panel.CustomMinimumSize = new Vector2(300, 0); AddChild(panel);
        var m = new MarginContainer(); foreach (var s in new[] { "left", "right", "top", "bottom" }) m.AddThemeConstantOverride("margin_" + s, 10); panel.AddChild(m);
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 6); m.AddChild(v);
        v.AddChild(Px.Label("Новый мир", Px.Gold, true, HorizontalAlignment.Center));
        name = new LineEdit { Text = Loc.T(Names[Rng.RandiRange(0, Names.Length - 1)]), MaxLength = 20, CustomMinimumSize = new Vector2(0, 20), PlaceholderText = Loc.T("Название мира") }; Modal.Style(name); v.AddChild(name);
        name.TextSubmitted += _ => Create();
        var dr = new HBoxContainer(); dr.AddThemeConstantOverride("separation", 6); v.AddChild(dr);
        bNormal = Px.Button("Обычная", "shield", () => SetDiff(0), 0, 26); bHard = Px.Button("Сложная", "skull", () => SetDiff(1), 0, 26);
        foreach (var b in new[] { bNormal, bHard }) { b.SizeFlagsHorizontal = SizeFlags.ExpandFill; dr.AddChild(b); }
        bNormal.TooltipText = Loc.T("Сбалансированное приключение"); bHard.TooltipText = Loc.T("Более опасные враги и боссы");
        SetDiff(0, true);
        bMore = Px.Button("+ Дополнительные настройки", null, ToggleMore, 0, 18); bMore.SizeFlagsHorizontal = SizeFlags.ExpandFill; v.AddChild(bMore);
        adv = new HBoxContainer { Visible = false }; ((HBoxContainer)adv).AddThemeConstantOverride("separation", 4); v.AddChild(adv);
        seed = new LineEdit { MaxLength = 18, CustomMinimumSize = new Vector2(0, 20), SizeFlagsHorizontal = SizeFlags.ExpandFill, PlaceholderText = Loc.T("Seed: случайный") }; Modal.Style(seed); adv.AddChild(seed);
        seed.TextSubmitted += _ => Create();
        adv.AddChild(Px.Button("", "dice", () => { seed.Text = NewSeed().ToString(); Px.Pop(seed, .04f); }, 26, 20));
        var br = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; br.AddThemeConstantOverride("separation", 8); v.AddChild(br);
        br.AddChild(Px.Button("Назад", null, Back, 80, 24));
        var cb = Px.Button("Создать мир", "check", Create, 150, 24); cb.AddThemeColorOverride("font_color", Px.Gold); cb.AddThemeColorOverride("font_hover_color", new Color("#fff2b0")); br.AddChild(cb);
        CallDeferred(nameof(Place));
    }

    void Place() { panel.ResetSize(); panel.Position = new Vector2((FlowUI.BoxW - panel.Size.X) / 2, (FlowUI.BoxH - panel.Size.Y) / 2 - 4).Floor(); }

    void ToggleMore()
    {
        open = !open; adv.Visible = open; bMore.Text = Loc.T(open ? "- Дополнительные настройки" : "+ Дополнительные настройки"); Sfx.Play("tick", -3, 1.3f); Place(); CallDeferred(nameof(Place));
    }

    static int NewSeed() => (int)(Rng.Randi() % 1000000);

    /// <summary>Число используется как есть, слово даёт устойчивый seed (одно и то же слово — один и тот же мир). Пустая строка — 0 (вызывающий выбирает случайный).</summary>
    public static int ParseSeed(string s)
    {
        s = (s ?? "").Trim(); if (s == "") return 0;
        if (int.TryParse(s, out var n) && n >= 0) return n % 2000000000;
        int h = 17; foreach (var ch in s) h = unchecked(h * 31 + ch); return Math.Abs(h % 2000000000);
    }

    void SetDiff(int d, bool quiet = false)
    {
        difficulty = d; bNormal.ThemeTypeVariation = d == 0 ? "ButtonSelected" : ""; bHard.ThemeTypeVariation = d == 1 ? "ButtonSelected" : "";
        if (!quiet) Sfx.Play("tick", -3, 1.3f);
    }

    bool creating;
    void Create()
    {
        if (creating) return; var n = SaveManager.CleanName(name.Text, ""); if (n == "") { Sfx.Play("error"); Px.Shake(name); Toast("Введите название мира"); return; }
        string st = seed.Text.Trim(); int s = st == "" ? NewSeed() : ParseSeed(st);
        creating = true; var w = SaveManager.CreateWorld(n, s, difficulty);
        Flow.WorldId = w.Id; Flow.PlayWorld(w.Id);
    }

    public override bool OnKey(Key k)
    {
        if (k == Key.Escape) { Back(); return true; }
        if (k == Key.Enter || k == Key.KpEnter) { Create(); return true; }
        if (k == Key.Left || k == Key.A) { SetDiff(0); return true; }
        if (k == Key.Right || k == Key.D) { SetDiff(1); return true; }
        return false;
    }

    public override void Back() { if (SaveManager.ListWorlds().Count == 0) { Flow.ShowCharacters(null); return; } Flow.ShowWorlds(null); }
}
