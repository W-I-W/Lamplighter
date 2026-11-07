using Godot;
using System;

namespace Lamplighter;

/// <summary>Общие детали экранов выбора героя и мира: цвета Lanther, кнопки и надписи из темы LanternTheme (пиксельный шрифт, тёмно-синие панели, золотые акценты), короткие анимации.</summary>
public static class Px
{
    public static readonly Color Gold = new("#ffd23f"), Text = new("#d8e0f0"), Dim = new("#8a96b0"), Good = new("#9be07a"), Bad = new("#f06b5e"), Warn = new("#ffb36b"), Cyan = new("#8ff0ff");
    static Theme theme;
    public static Theme Theme => theme ??= GD.Load<Theme>("res://Resources/UI/LanternTheme.tres");
    public static Texture2D Icon(string n) => GD.Load<Texture2D>($"res://Assets/UI/Flow/icon_{n}.png");

    public static Label Label(string s, Color? c = null, bool big = false, HorizontalAlignment al = HorizontalAlignment.Left, bool wrap = false)
    {
        var l = new Label { Text = Loc.T(s), HorizontalAlignment = al, MouseFilter = Control.MouseFilterEnum.Ignore, ThemeTypeVariation = big ? "BigLabel" : "PlainLabel", Modulate = c ?? Text };
        if (wrap) { l.AutowrapMode = TextServer.AutowrapMode.WordSmart; l.CustomMinimumSize = new Vector2(10, 0); }
        return l;
    }

    public static Button Button(string text, string icon, Action act, float w = 0, float h = 22)
    {
        var b = new Button { Text = Loc.T(text), FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(w, h) };
        if (icon != null) b.AddChild(new TextureRect { Texture = Px.Icon(icon), Position = new Vector2(5, (h - 16) / 2 + 1), Size = new Vector2(16, 16), MouseFilter = Control.MouseFilterEnum.Ignore });      // значок слева, текст по центру всей кнопки
        b.Pressed += () => { Sfx.Play("tick", -3, 1.3f); act?.Invoke(); };
        return b;
    }

    /// <summary>Значок в строке рядом с текстом: пиксельный шрифт сидит в строке высоко, поэтому значок, центрованный по строке, выглядит ниже текста. Нижний отступ в 2 px поднимает его на один пиксель интерфейса.</summary>
    static readonly System.Collections.Generic.Dictionary<(Texture2D, int), Texture2D> raised = new();
    /// <summary>Значок с прозрачными строками снизу: в кнопке (значок по центру содержимого) он поднимается на rows/2 пикселей к середине строки шрифта.</summary>
    public static Texture2D Raised(Texture2D t, int rows)
    {
        if (t == null) return null; if (raised.TryGetValue((t, rows), out var r)) return r;
        var src = t.GetImage(); if (src == null) return t; if (src.IsCompressed()) src.Decompress(); src.Convert(Image.Format.Rgba8);
        var img = Image.CreateEmpty(src.GetWidth(), src.GetHeight() + rows, false, Image.Format.Rgba8); img.BlitRect(src, new Rect2I(0, 0, src.GetWidth(), src.GetHeight()), Vector2I.Zero);
        return raised[(t, rows)] = ImageTexture.CreateFromImage(img);
    }

    public static Control IconInline(Texture2D tex, float size, Color? mod = null)
    {
        var m = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter }; m.AddThemeConstantOverride("margin_bottom", 2);
        m.AddChild(new TextureRect { Texture = tex, Modulate = mod ?? Colors.White, CustomMinimumSize = new Vector2(size, size), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, TextureFilter = CanvasItem.TextureFilterEnum.Nearest, MouseFilter = Control.MouseFilterEnum.Ignore });
        return m;
    }

    public static PanelContainer Panel(string variation = "PanelDark") => new() { ThemeTypeVariation = variation };

    public static string Time(float secs) { string h = Loc.T("ч"), m = Loc.T("м"); return secs < 3600 ? $"{(int)(secs / 60)}{m}" : $"{(int)(secs / 3600)}{h} {(int)(secs % 3600 / 60)}{m}"; }
    public static string DiffName(int d) => Loc.T(d == 1 ? "Сложная" : "Обычная");

    // ───────────────────────── короткие анимации ─────────────────────────
    public static void Pop(Control c, float amount = .12f)
    {
        if (!GodotObject.IsInstanceValid(c)) return; c.PivotOffset = c.Size / 2; var t = c.CreateTween(); c.Scale = Vector2.One;
        t.TweenProperty(c, "scale", Vector2.One * (1 + amount), .07f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        t.TweenProperty(c, "scale", Vector2.One, .1f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
    }

    public static void FadeIn(Control c, float dur = .22f, float slide = 8)
    {
        c.Modulate = new Color(1, 1, 1, 0); var t = c.CreateTween().SetParallel();
        t.TweenProperty(c, "modulate:a", 1f, dur);
        if (slide != 0) { var pos = c.Position; c.Position = pos + new Vector2(0, slide); t.TweenProperty(c, "position", pos, dur).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out); }
    }

    public static void Shake(Control c)
    {
        var p = c.Position; var t = c.CreateTween();
        for (int i = 0; i < 3; i++) { t.TweenProperty(c, "position:x", p.X + (i % 2 == 0 ? 3 : -3), .04f); }
        t.TweenProperty(c, "position:x", p.X, .04f);
    }
}

/// <summary>Герой из PNG-слоёв в увеличенном виде. Ожидание — лёгкое покачивание, ходьба — смена кадров шага; Flip — взгляд влево.</summary>
public partial class LookView : Control
{
    public Look Look = Game.DefaultLook(); public bool Walking, Flip, Lamp; float zoom = 4, t;
    public float Zoom { get => zoom; set { zoom = value; CustomMinimumSize = new Vector2(16, 16) * zoom; Size = CustomMinimumSize; } }

    public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; TextureFilter = TextureFilterEnum.Nearest; }
    public override void _Process(double delta) { t += (float)delta; QueueRedraw(); }

    public override void _Draw()
    {
        int frame = Walking ? (int)(t * 6) % 2 : 0; float bob = Walking ? (frame == 1 ? -1 : 0) : 0;      // покой — дыхание из кадров Idle
        float z = zoom;
        if (Flip) DrawSetTransform(new Vector2(16 * z, 0), 0, new Vector2(-1, 1));
        LookArt.Draw(this, Look, frame, new Vector2(0, bob * z), Colors.White, 16, z, Lamp, Walking);
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }
}

/// <summary>Сцена под героем в редакторе: ночное небо, тёплое свечение фонаря, трава под ногами.</summary>
public partial class StageView : Control
{
    public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; TextureFilter = TextureFilterEnum.Nearest; ClipContents = true; }

    public override void _Draw()
    {
        var sz = Size; DrawRect(new Rect2(Vector2.Zero, sz), new Color("#101a36"));
        for (int i = 0; i < 5; i++) DrawRect(new Rect2(0, sz.Y * (.2f * i), sz.X, sz.Y * .2f), new Color(.06f + .018f * i, .09f + .02f * i, .2f + .03f * i, 1));
        foreach (var st in new[] { (.12f, .1f), (.3f, .22f), (.55f, .08f), (.78f, .18f), (.9f, .06f), (.2f, .38f), (.7f, .34f) }) DrawRect(new Rect2(Mathf.Floor(sz.X * st.Item1), Mathf.Floor(sz.Y * st.Item2), 2, 2), new Color(.7f, .78f, 1f, .75f));
        float z = Zoom, gy = FloorY; var g = new Color("#3f7a3a"); var d = new Color("#4a3426");
        DrawRect(new Rect2(0, gy, sz.X, sz.Y - gy), d);
        for (int i = 0; i < (int)(sz.X / (4 * z)) + 1; i++) for (int k = 0; k < 4; k++) if ((i * 7 + k * 3) % 5 == 0) DrawRect(new Rect2(i * 4 * z + (k % 2) * 2 * z, gy + (3 + k * 3) * z, z * 2, z), d.Darkened(.25f));
        DrawRect(new Rect2(0, gy, sz.X, 4 * z), g); DrawRect(new Rect2(0, gy, sz.X, z), g.Lightened(.3f)); DrawRect(new Rect2(0, gy + 3 * z, sz.X, z), g.Darkened(.25f));
        QueueRedraw();
    }

    public float Zoom = 8, FloorY = 150;
}

/// <summary>Образец цвета палитры в пиксельной рамке слота; выбранный подсвечен золотой рамкой.</summary>
public partial class Swatch : Control
{
    public Color Col; public bool Selected; public Action Clicked;

    public override void _Ready() { CustomMinimumSize = new Vector2(20, 20); MouseFilter = MouseFilterEnum.Stop; MouseDefaultCursorShape = CursorShape.PointingHand; }

    public override void _Draw()
    {
        var sb = Px.Theme.GetStylebox("panel", Selected ? "SlotSelected" : "Slot"); if (sb != null) DrawStyleBox(sb, new Rect2(Vector2.Zero, Size));
        DrawRect(new Rect2(3, 3, Size.X - 6, Size.Y - 6), Col);
        DrawRect(new Rect2(3, 3, Size.X - 6, 1), Col.Lightened(.35f)); DrawRect(new Rect2(3, Size.Y - 4, Size.X - 6, 1), Col.Darkened(.35f));
    }

    public override void _GuiInput(InputEvent e) { if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left) { Clicked?.Invoke(); AcceptEvent(); } }
}

/// <summary>Карточка списка (герой или мир): пиксельная рамка, выбранная — золотая; клик выбирает, двойной клик запускает.</summary>
public partial class Card : PanelContainer
{
    public Action Clicked, Activated; bool selected; public string Key = "";

    public Card() { ThemeTypeVariation = "PanelDark"; MouseFilter = MouseFilterEnum.Stop; MouseDefaultCursorShape = CursorShape.PointingHand; }

    public bool Selected
    {
        get => selected;
        set { if (value == selected) return; selected = value; ThemeTypeVariation = value ? "PanelGold" : "PanelDark"; if (value) Px.Pop(this, .03f); }
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
        {
            if (mb.DoubleClick) Activated?.Invoke(); else { Sfx.Play("tick", -5, 1.4f); Clicked?.Invoke(); }
            AcceptEvent();
        }
    }
}
