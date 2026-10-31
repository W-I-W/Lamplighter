using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Действия с ресурсом по ПКМ в инвентаре: передать игроку, выбросить, положить на склад, взять со склада. Компактное меню и окно количества («Дерево ×25 · Получатель · [1] [Половина] [Всё]»).
/// Окно только собирает запрос; решает игра (Game.TrySend/TryDrop/TryChest), в сети — хост. Клавиши: Esc — закрыть, Enter — подтвердить, ←/→ — получатель, 1/2/3 — 1, половина, всё.</summary>
public partial class ResourceActionUI : Control
{
    public Game G;
    enum Act { Send, Drop, Deposit, Withdraw }
    PanelContainer panel; VBoxContainer box; int item = -1, amount = 1, who; Act act; bool dialog;
    List<NetPlayer> peers = new();
    public bool Active => Visible;
    bool AmountFocused => amtEdit != null && GodotObject.IsInstanceValid(amtEdit) && amtEdit.HasFocus();
    public bool IsDialog => Visible && dialog;
    static readonly Color Gold = new("#ffd23f"), Dim = new("#8a96b0"), Good = new("#9be07a");

    public override void _Ready()
    {
        Theme = GD.Load<Theme>(Hud.ThemePath); MouseFilter = MouseFilterEnum.Stop; TextureFilter = TextureFilterEnum.Nearest; ZIndex = 4070; Visible = false;
        var dim = new ColorRect { Color = new Color(0, 0, .03f, .35f), MouseFilter = MouseFilterEnum.Ignore }; dim.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(dim);
        panel = new PanelContainer { ThemeTypeVariation = "PanelGold" }; AddChild(panel);
        var m = new MarginContainer(); foreach (var s in new[] { "left", "right", "top", "bottom" }) m.AddThemeConstantOverride("margin_" + s, 6); panel.AddChild(m);
        box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 4); m.AddChild(box);
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton mb && mb.Pressed && !new Rect2(panel.Position, panel.Size).HasPoint(mb.Position)) { Close(); AcceptEvent(); }       // клик мимо окна закрывает его
    }

    public void Close() { Visible = false; dialog = false; item = -1; }

    int Max() => Math.Min(Game.MaxMove, act == Act.Withdraw ? G.Stock[item] : G.Inv[item]);

    public void Open(int it)
    {
        item = it; dialog = false; peers = G.NearbyPlayers(); Build(); Visible = true;
    }

    public bool HasActions(int it)
    {
        bool wh = G.NearChestFor(G.PPos) && it != It.Coins;
        return G.Inv[it] > 0 || (wh && G.Stock[it] > 0);
    }

    void Row(string text, Action a) { box.AddChild(Px.Button(text, null, a, 150, 18)); }

    void Build()
    {
        foreach (var c in box.GetChildren()) { box.RemoveChild(c); c.QueueFree(); }
        string name = Loc.T(It.Names[item]); int have = G.Inv[item];
        var head = new HBoxContainer(); head.AddThemeConstantOverride("separation", 5); box.AddChild(head);
        head.AddChild(Px.IconInline(UiIcons.Item(item), 16));
        head.AddChild(Px.Label($"{name} ×{have}", Gold, false));
        bool wh = G.NearChestFor(G.PPos) && item != It.Coins;
        if (!dialog)
        {
            if (have > 0 && peers.Count > 0) Row("Передать игроку", () => Start(Act.Send));
            if (have > 0) Row("Выбросить", () => Start(Act.Drop));
            if (have > 0 && wh) Row("Положить на склад", () => Start(Act.Deposit));
            if (wh && G.Stock[item] > 0) Row("Взять со склада", () => Start(Act.Withdraw));
            return;
        }
        if (act == Act.Send && peers.Count > 0)
        {
            var r = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; r.AddThemeConstantOverride("separation", 4); box.AddChild(r);
            r.AddChild(Px.Button("", "left", () => Cycle(-1), 20, 18)); r.AddChild(Px.Label(Loc.T("Получатель") + ": " + peers[who % peers.Count].Name, Dim, false)); r.AddChild(Px.Button("", "right", () => Cycle(1), 20, 18));
        }
        int max = Math.Max(1, Max()); amount = Math.Clamp(amount, 1, max);
        string verb = act == Act.Send ? "передать" : act == Act.Drop ? "выбросить" : act == Act.Deposit ? "положить" : "взять";
        box.AddChild(Px.Label(Loc.T("Сколько") + " " + Loc.T(verb) + "?", Dim, false, HorizontalAlignment.Center));
        var q = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; q.AddThemeConstantOverride("separation", 3); box.AddChild(q);
        q.AddChild(Px.Button("1", null, () => SetAmount(1), 26, 18)); q.AddChild(Px.Button("Половина", null, () => SetAmount(Math.Max(1, max / 2)), 62, 18)); q.AddChild(Px.Button("Всё", null, () => SetAmount(max), 36, 18));
        var s = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; s.AddThemeConstantOverride("separation", 4); box.AddChild(s);
        s.AddChild(Px.Button("-", null, () => SetAmount(amount - 1), 20, 18));
        amtEdit = new LineEdit { Text = amount.ToString(), MaxLength = 4, CustomMinimumSize = new Vector2(46, 18), Alignment = HorizontalAlignment.Center, SelectAllOnFocus = true, TooltipText = Loc.T("Введите число или нажмите кнопку") }; Modal.Style(amtEdit); amtEdit.AddThemeFontSizeOverride("font_size", 9);
        amtEdit.TextChanged += t => { var digits = new string(System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Where(t, char.IsDigit))); if (digits != t) { int c = amtEdit.CaretColumn; amtEdit.Text = digits; amtEdit.CaretColumn = Math.Min(c, digits.Length); } amount = digits == "" ? 1 : Math.Clamp(int.TryParse(digits, out var v) ? v : 1, 1, max); amtMaxL.Text = $"/ {max}"; };
        amtEdit.TextSubmitted += _ => Confirm(); s.AddChild(amtEdit);
        amtMaxL = Px.Label($"/ {max}", Good, false); s.AddChild(amtMaxL);
        s.AddChild(Px.Button("+", null, () => SetAmount(amount + 1), 20, 18));
        string go = act == Act.Send ? "Передать" : act == Act.Drop ? "Выбросить" : act == Act.Deposit ? "Положить" : "Взять";
        var b = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; b.AddThemeConstantOverride("separation", 4); box.AddChild(b);
        b.AddChild(Px.Button(go, "check", Confirm, 104, 20)); b.AddChild(Px.Button("Отмена", null, Close, 60, 20));
    }

    LineEdit amtEdit; Label amtMaxL;
    /// <summary>Количество меняется на месте, без перестроения кнопок: курсор остаётся над той же кнопкой.</summary>
    void SetAmount(int n) { int max = Math.Max(1, Max()); amount = Math.Clamp(n, 1, max); if (amtEdit != null) { amtEdit.Text = amount.ToString(); amtEdit.CaretColumn = amtEdit.Text.Length; } }

    public void TypeForTest(string t) { if (amtEdit != null) { amtEdit.Text = t; amtEdit.EmitSignal(LineEdit.SignalName.TextChanged, t); } }
    public void DialogForTest(int a) => Start((Act)a);
    public int ForTestAmount => amount;
    void Start(Act a) { act = a; dialog = true; who = 0; amount = 1; Build(); }
    void Set(int n) => SetAmount(n);
    void Cycle(int d) { if (peers.Count > 0) { who = (who + d + peers.Count) % peers.Count; Build(); } }

    void Confirm()
    {
        if (item < 0) return; int n = Math.Clamp(amount, 1, Math.Max(1, Max()));
        switch (act)
        {
            case Act.Send: if (peers.Count > 0) G.TrySend(peers[who % peers.Count].PeerId, item, n); break;
            case Act.Drop: G.TryDrop(item, n); break;
            case Act.Deposit: G.TryChest(true, item, n); break;
            case Act.Withdraw: G.TryChest(false, item, n); break;
        }
        Close();
    }

    public override void _Input(InputEvent e)
    {
        if (!Visible || e is not InputEventKey k || !k.Pressed || k.Echo) return;
        var vp = GetViewport(); bool used = true;
        switch (k.Keycode)
        {
            case Key.Escape: Close(); break;
            case Key.Enter: case Key.KpEnter: if (dialog && !AmountFocused) Confirm(); else used = false; break;
            case Key.Left: if (dialog && act == Act.Send && !AmountFocused) Cycle(-1); else used = false; break;
            case Key.Right: if (dialog && act == Act.Send && !AmountFocused) Cycle(1); else used = false; break;
            case Key.Key1: if (dialog && !AmountFocused) Set(1); else used = false; break;
            case Key.Key2: if (dialog && !AmountFocused) Set(Math.Max(1, Max() / 2)); else used = false; break;
            case Key.Key3: if (dialog && !AmountFocused) Set(Math.Max(1, Max())); else used = false; break;
            default: used = false; break;
        }
        if (used) vp.SetInputAsHandled();
    }

    public override void _Process(double delta)
    {
        if (G == null) return;
        if (Visible && (G.Window != Win.Inventory || G.Menu != Mn.None)) Close();
        if (!Visible) return;
        var vs = Size; panel.ResetSize(); panel.Position = ((vs - panel.Size) / 2).Round();
    }
}
