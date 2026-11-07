using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lamplighter;

/// <summary>Действия с ресурсом по ПКМ в инвентаре: передать игроку, удалить, положить на склад, взять со склада. Компактное меню и окно количества («Дерево ×25 · Получатель · [1] [Половина] [Всё]»).
/// Окно только собирает запрос; решает игра (Game.TrySend/TryDrop/TryChest), в сети — хост. Клавиши: Esc — закрыть, Enter — подтвердить, ←/→ — получатель, 1/2/3 — 1, половина, всё.</summary>
public partial class ResourceActionUI : Control
{
    public Game G;
    enum Act { Send, Drop, Deposit, Withdraw }
    PanelContainer panel;
    Control actions, quantity, recipient;
    readonly List<(Button Button, string Caption)> captions = new();
    string inputTip;
    TextureRect itemIcon;
    Label itemLabel, recipientLabel, prompt;
    Button drop, deposit, withdraw, confirm; int item = -1, amount = 1, who; Act act; bool dialog;
    List<NetPlayer> peers = new();
    public bool Active => Visible;
    bool AmountFocused => amtEdit.HasFocus();
    public bool IsDialog => Visible && dialog;

    public override void _Ready()
    {
        panel = GetNode<PanelContainer>("Panel");
        var content = panel.GetNode<VBoxContainer>("Margin/Content");
        itemIcon = content.GetNode<TextureRect>("Head/IconMargin/Icon");
        itemLabel = content.GetNode<Label>("Head/Item");
        actions = content.GetNode<Control>("Actions");
        quantity = content.GetNode<Control>("Quantity");
        recipient = quantity.GetNode<Control>("Recipient");
        recipientLabel = recipient.GetNode<Label>("Name");
        prompt = quantity.GetNode<Label>("Prompt");
        amtEdit = quantity.GetNode<LineEdit>("Amount/Input");
        amtMaxL = quantity.GetNode<Label>("Amount/Maximum");
        inputTip = amtEdit.TooltipText;
        amtEdit.TextChanged += AmountChanged;
        amtEdit.TextSubmitted += _ => Confirm();
        drop = Bind(actions, "Drop", () => Start(Act.Drop));
        deposit = Bind(actions, "Deposit", () => Start(Act.Deposit));
        withdraw = Bind(actions, "Withdraw", () => Start(Act.Withdraw));
        Bind(recipient, "Previous", () => Cycle(-1));
        Bind(recipient, "Next", () => Cycle(1));
        Bind(quantity, "Presets/One", () => SetAmount(1));
        Bind(quantity, "Presets/Half", () => SetAmount(Max() / 2));
        Bind(quantity, "Presets/All", () => SetAmount(Max()));
        Bind(quantity, "Amount/Less", () => SetAmount(amount - 1));
        Bind(quantity, "Amount/More", () => SetAmount(amount + 1));
        confirm = Bind(quantity, "Buttons/Confirm", Confirm);
        Bind(quantity, "Buttons/Cancel", Close);
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton mb && mb.Pressed && !new Rect2(panel.Position, panel.Size).HasPoint(mb.Position)) { Close(); AcceptEvent(); }       // клик мимо окна закрывает его
    }

    public void Close() { amtEdit.ReleaseFocus(); Visible = false; dialog = false; item = -1; }

    int Max() => Math.Min(Game.MaxMove, act == Act.Withdraw ? G.Stock[item] : G.Inv[item]);

    public void Open(int it)
    {
        foreach (var (button, caption) in captions) button.Text = Loc.T(caption);
        amtEdit.TooltipText = Loc.T(inputTip);
        item = it; dialog = false; peers = G.NearbyPlayers(); Build(); Visible = true;
    }

    public bool HasActions(int it)
    {
        bool wh = G.NearChestFor(G.PPos) && it != It.Coins;
        return G.Inv[it] > 0 || (wh && G.Stock[it] > 0);
    }

    Button Bind(Control parent, string path, Action action)
    {
        var button = parent.GetNode<Button>(path);
        captions.Add((button, button.Text));
        button.Pressed += () => { Sfx.Play("tick", -3, 1.3f); action(); };
        return button;
    }

    void Build()
    {
        G.PokeHover();
        int have = G.Inv[item];
        itemIcon.Texture = UiIcons.Item(item);
        itemLabel.Text = $"{Loc.T(It.Names[item])} ×{have}";
        bool nearStorage = G.NearChestFor(G.PPos) && item != It.Coins;
        actions.Visible = !dialog;
        quantity.Visible = dialog;
        drop.Visible = have > 0;
        deposit.Visible = have > 0 && nearStorage;
        withdraw.Visible = nearStorage && G.Stock[item] > 0;
        if (!dialog) return;

        recipient.Visible = act == Act.Send && peers.Count > 0;
        if (recipient.Visible) recipientLabel.Text = $"{Loc.T("Получатель")}: {peers[who].Name}";
        string verb = act == Act.Send ? "передать" : act == Act.Drop ? "выбросить" : act == Act.Deposit ? "положить" : "взять";
        prompt.Text = $"{Loc.T("Сколько")} {Loc.T(verb)}?";
        confirm.Text = Loc.T(act == Act.Send ? "Передать" : act == Act.Drop ? "Выбросить" : act == Act.Deposit ? "Положить" : "Взять");
        amtMaxL.Text = $"/ {Math.Max(1, Max())}";
        SetAmount(amount);
    }

    void AmountChanged(string text)
    {
        if (!dialog || item < 0) return;
        string digits = new(text.Where(char.IsDigit).ToArray());
        if (digits != text)
        {
            int caret = amtEdit.CaretColumn;
            amtEdit.Text = digits;
            amtEdit.CaretColumn = Math.Min(caret, digits.Length);
        }
        amount = Math.Clamp(int.TryParse(digits, out var value) ? value : 1, 1, Math.Max(1, Max()));
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
        panel.ResetSize();
    }
}
