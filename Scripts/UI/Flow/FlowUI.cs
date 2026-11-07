using Godot;
using System;

namespace Lamplighter;

public enum FlowKind { Solo, Host, Guest }

/// <summary>Экран запуска игры: выбор героя → (для одиночной игры и хоста) выбор мира → старт. Гость выбирает только героя и подключается к миру хоста.
/// Рисуется поверх игры внутри масштабируемого интерфейса (логическая сетка 640×360, пиксельные рамки, nearest).</summary>
public partial class FlowUI : Control
{
    public const float BoxW = 620, BoxH = 340;
    public Game G; public FlowKind Kind; public string Transport = "", Target = ""; public bool FriendsOnly = true;
    public string CharId, WorldId;
    Control box; FlowScreen cur; ColorRect fade; bool starting;

    public static FlowUI Open(Game g, Control parent, FlowKind kind, string transport, string target)
    {
        var flow = GD.Load<PackedScene>("res://Scenes/UI/Flow.tscn").Instantiate<FlowUI>();
        flow.G = g;
        flow.Kind = kind;
        flow.Transport = transport ?? "";
        flow.Target = target ?? "";
        flow.FriendsOnly = g.NetFriendsOnly;
        parent.AddChild(flow);
        flow.ShowCharacters(null);
        return flow;
    }

    public override void _Ready()
    {
        box = GetNode<Control>("Screen");
        fade = GetNode<ColorRect>("Fade");
        Resized += FitScreen;
        box.Resized += FitScreen;
        FitScreen();
    }

    void FitScreen()
    {
        if (box.Size.X <= 0 || box.Size.Y <= 0) return;
        float fit = Mathf.Min(1f, Mathf.Min(Size.X / box.Size.X, Size.Y / box.Size.Y));
        box.PivotOffset = box.Size * .5f;
        box.Scale = Vector2.One * fit;
    }

    public string ModeLine => Kind switch
    {
        FlowKind.Solo => Loc.T("Одиночная игра"),
        FlowKind.Host => Loc.T("Сетевая игра · хост") + " · " + Loc.T(Transport == "steam" ? "Steam" : "локальная сеть"),
        _ => Loc.T("Подключение к игре") + (Target != "" ? " · " + Target : "")
    };

    public void Go(FlowScreen s)
    {
        G?.PokeHover();
        cur?.QueueFree(); cur = s; s.Flow = this; box.AddChild(s); s.Size = box.Size; Px.FadeIn(s, .2f, 6);
    }

    public void ShowCharacters(string select)
    {
        var screen = GD.Load<PackedScene>("res://Scenes/UI/CharacterSelection.tscn").Instantiate<CharacterSelectionUI>();
        screen.SelectId = select ?? CharId;
        Go(screen);
    }
    public void ShowCreateCharacter(bool first)
    {
        var screen = GD.Load<PackedScene>("res://Scenes/UI/CharacterCreation.tscn").Instantiate<CharacterCreationUI>();
        screen.First = first;
        Go(screen);
    }
    public void ShowWorlds(string select)
    {
        var screen = GD.Load<PackedScene>("res://Scenes/UI/WorldSelection.tscn").Instantiate<WorldSelectionUI>();
        screen.SelectId = select ?? WorldId;
        Go(screen);
    }
    public void ShowCreateWorld() => Go(GD.Load<PackedScene>("res://Scenes/UI/WorldCreation.tscn").Instantiate<WorldCreationUI>());

    /// <summary>Выход из экрана выбора обратно в меню, из которого он был открыт.</summary>
    public void Leave()
    {
        if (starting) return;
        var back = Kind == FlowKind.Solo ? Mn.Main : Kind == FlowKind.Host ? Mn.Multi : (GameSession.Mode == SessionMode.None ? Mn.Multi : Mn.NetJoin);
        if (Kind == FlowKind.Guest) { GameSession.PendingTarget = null; if (Transport == "") back = Mn.Main; }
        QueueFree(); G.FlowClosed(back);
    }

    /// <summary>Герой выбран: одиночная игра и хост идут к выбору мира, гость сразу подключается.</summary>
    public void PickCharacter(string id)
    {
        CharId = id;
        if (Kind != FlowKind.Guest) { ShowWorlds(null); return; }
        starting = true; Intro(() =>
        {
            if (G.StartGuestSession(id, Transport, Target)) { QueueFree(); G.FlowClosed(Mn.NetJoin); }
            else { starting = false; fade.Color = new Color(0, 0, 0, 0); Cur?.Toast(NetService.Status); }
        });
    }

    public FlowScreen Cur => cur;

    /// <summary>Мир выбран: старт одиночной игры или создание лобби.</summary>
    public void PlayWorld(string worldId)
    {
        WorldId = worldId; starting = true;
        Intro(() =>
        {
            if (Kind == FlowKind.Solo) G.StartSolo(CharId, worldId);
            else if (!G.StartHostSession(CharId, worldId, Transport, FriendsOnly)) { starting = false; fade.Color = new Color(0, 0, 0, 0); Cur?.Toast(NetService.Status); }
        });
    }

    /// <summary>Короткая анимация начала игры: вспышка света фонаря и затемнение.</summary>
    void Intro(Action then)
    {
        Sfx.Play("upgrade", -3, 1.2f);
        var t = CreateTween(); fade.Color = new Color(1f, .85f, .5f, 0);
        t.TweenProperty(fade, "color", new Color(1f, .85f, .5f, .55f), .12f); t.TweenProperty(fade, "color", new Color(0, 0, 0, 1), .32f); t.TweenCallback(Callable.From(then));
    }

    public override void _Input(InputEvent e)
    {
        if (starting || e is not InputEventKey k || !k.Pressed || k.Echo) return;
        if (GetViewport().GuiGetFocusOwner() is LineEdit) { if (k.Keycode == Key.Escape) { GetViewport().GuiGetFocusOwner().ReleaseFocus(); GetViewport().SetInputAsHandled(); } return; }
        if (cur != null && cur.OnKey(k.Keycode)) GetViewport().SetInputAsHandled();
    }
}

/// <summary>Общая основа экранов выбора: заголовок, подсказка, всплывающее сообщение.</summary>
public abstract partial class FlowScreen : Control
{
    public FlowUI Flow; Label note;

    public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; }
    public virtual bool OnKey(Key k) => false;

    protected void BindSelectionLayout(string subtitle, out VBoxContainer cards, out Control details, out Label empty)
    {
        var title = GetNode<Label>("Title");
        title.Text = Loc.T(title.Text);
        GetNode<Label>("Subtitle").Text = subtitle;
        BindNotice(GetNode<Label>("Notice"));
        cards = GetNode<VBoxContainer>("List/Content/Scroll/Cards");
        details = GetNode<Control>("Details/Content");
        empty = GetNode<Label>("List/Empty");
        empty.Text = Loc.T(empty.Text);
    }

    protected Button BindButton(string path, Action action)
    {
        var button = GetNode<Button>(path);
        button.Text = Loc.T(button.Text);
        button.Pressed += () => { Sfx.Play("tick", -3, 1.3f); action(); };
        return button;
    }

    protected void BindNotice(Label notice) => note = notice;

    public void Toast(string msg, bool good = false)
    {
        if (note == null) return; note.Text = Loc.T(msg ?? ""); note.Modulate = good ? Px.Good : Px.Warn; Sfx.Play(good ? "pickup" : "error", -4);
        var t = CreateTween(); t.TweenInterval(2.6f); t.TweenCallback(Callable.From(() => { if (GodotObject.IsInstanceValid(note)) note.Text = ""; }));
    }

    protected static HBoxContainer Row(float x, float y, float w, BoxContainer.AlignmentMode al = BoxContainer.AlignmentMode.Begin, int sep = 6)
    {
        var h = new HBoxContainer { Position = new Vector2(x, y), Size = new Vector2(w, 24), Alignment = al }; h.AddThemeConstantOverride("separation", sep); return h;
    }

    protected static MarginContainer Pad(Control child, int m = 6)
    {
        var mc = new MarginContainer(); foreach (var s in new[] { "left", "right", "top", "bottom" }) mc.AddThemeConstantOverride("margin_" + s, m); mc.AddChild(child); return mc;
    }

    public virtual void Back() => Flow.Leave();
}
