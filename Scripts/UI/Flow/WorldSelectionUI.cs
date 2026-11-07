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
        BindSelectionLayout(Flow.ModeLine + " · " + (SaveManager.LoadCharacter(Flow.CharId)?.Name ?? ""), out cardBox, out detail, out empty);
        BindButton("Back", Back);
        ren = BindButton("Actions/Rename", AskRename);
        del = BindButton("Actions/Delete", AskDelete);
        BindButton("Actions/Create", Flow.ShowCreateWorld);
        play = BindButton("Actions/Play", PlaySel);
        if (Flow.Kind == FlowKind.Host)
        {
            play.Text = Loc.T("Создать лобби");
            play.GetNode<TextureRect>("Icon").Hide();
            play.GetNode<TextureRect>("LobbyIcon").Show();
        }
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
