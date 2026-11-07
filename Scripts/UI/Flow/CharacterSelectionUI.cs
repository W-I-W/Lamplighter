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
        BindSelectionLayout(Flow.ModeLine, out cardBox, out detail, out empty);
        BindButton("Back", Back);
        del = BindButton("Actions/Delete", AskDelete);
        BindButton("Actions/Create", () => Flow.ShowCreateCharacter(false));
        play = BindButton("Actions/Play", PlaySel);
        if (Flow.Kind == FlowKind.Guest) play.Text = Loc.T("Подключиться");
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
        if (string.IsNullOrEmpty(ci.Tool)) return Loc.T("Пусто");
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
        if (!string.IsNullOrEmpty(c0.CurTool)) { var tool = ToolDef.Get(c0.CurTool); v.AddChild(Px.Label("• " + Loc.T(tool?.Name ?? "Кирка"), Px.Text)); }
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
    internal static bool HasLampOf(CharacterSave c) { if (c == null) return false; if (c.SkK != null && c.SkV != null) for (int i = 0; i < c.SkK.Length && i < c.SkV.Length; i++) if (c.SkK[i] == "Spark" && c.SkV[i] > 0) return true; return false; }
}
