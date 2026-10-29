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
        var f = new FlowUI { G = g, Kind = kind, Transport = transport ?? "", Target = target ?? "", FriendsOnly = g.NetFriendsOnly };
        f.Size = parent.Size; parent.AddChild(f); f.ZIndex = 4090; f.ShowCharacters(null); return f;
    }

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect); MouseFilter = MouseFilterEnum.Stop; TextureFilter = TextureFilterEnum.Nearest; Theme = Px.Theme;
        var bg = new TextureRect { Texture = GD.Load<Texture2D>("res://Assets/UI/Flow/flow_bg.png"), StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, MouseFilter = MouseFilterEnum.Ignore };
        bg.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(bg);
        var dim = new ColorRect { Color = new Color(.02f, .03f, .07f, .35f), MouseFilter = MouseFilterEnum.Ignore }; dim.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(dim);
        box = new Control { Size = new Vector2(BoxW, BoxH), MouseFilter = MouseFilterEnum.Ignore }; AddChild(box);
        fade = new ColorRect { Color = new Color(0, 0, 0, 0), MouseFilter = MouseFilterEnum.Ignore, ZIndex = 50 }; fade.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(fade);
        Resized += Layout; Layout();
    }

    void Layout() { if (box != null) box.Position = ((Size - box.Size) / 2).Floor(); }

    public string ModeLine => Kind switch
    {
        FlowKind.Solo => Loc.T("Одиночная игра"),
        FlowKind.Host => Loc.T("Сетевая игра · хост") + " · " + Loc.T(Transport == "steam" ? "Steam" : "локальная сеть"),
        _ => Loc.T("Подключение к игре") + (Target != "" ? " · " + Target : "")
    };

    public void Go(FlowScreen s)
    {
        cur?.QueueFree(); cur = s; s.Flow = this; box.AddChild(s); s.Size = new Vector2(BoxW, BoxH); Px.FadeIn(s, .2f, 6);
    }

    public void ShowCharacters(string select) => Go(new CharacterSelectionUI { SelectId = select ?? CharId });
    public void ShowCreateCharacter(bool first) => Go(new CharacterCreationUI { First = first });
    public void ShowWorlds(string select) => Go(new WorldSelectionUI { SelectId = select ?? WorldId });
    public void ShowCreateWorld() => Go(new WorldCreationUI());

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

    protected void Header(string title, string sub)
    {
        var t = Px.Label(title, Px.Gold, true, HorizontalAlignment.Center); t.Position = new Vector2(0, 0); t.Size = new Vector2(FlowUI.BoxW, 20); AddChild(t);
        var s = Px.Label(sub, Px.Dim, false, HorizontalAlignment.Center); s.Position = new Vector2(0, 21); s.Size = new Vector2(FlowUI.BoxW, 12); AddChild(s);
        note = Px.Label("", Px.Warn, false, HorizontalAlignment.Center); note.Position = new Vector2(0, 325); note.Size = new Vector2(FlowUI.BoxW, 12); AddChild(note);
    }

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
