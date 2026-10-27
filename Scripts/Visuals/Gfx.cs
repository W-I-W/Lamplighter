using Godot;

namespace Lamplighter;

/// <summary>Эффекты мира собираются только из PNG в Assets/Sprites/Vfx (px, line, disc, arrow, crack_N): код лишь размещает и красит текстуры.
/// Заменив PNG своим рисунком, вы меняете вид всех полосок, колец, лучей и частиц.</summary>
public static class Gfx
{
    const string Dir = "res://Assets/Sprites/Vfx/";
    static Texture2D px, line, disc, arrow; static readonly Texture2D[] cracks = new Texture2D[4];
    static Texture2D Px => px ??= GD.Load<Texture2D>(Dir + "px.png");
    static Texture2D LineT => line ??= GD.Load<Texture2D>(Dir + "line.png");
    static Texture2D DiscT => disc ??= GD.Load<Texture2D>(Dir + "disc.png");
    static Texture2D ArrowT => arrow ??= GD.Load<Texture2D>(Dir + "arrow.png");
    public static Texture2D Crack(int i) => cracks[i] ??= GD.Load<Texture2D>(Dir + $"crack_{i}.png");

    public static void Rect(CanvasItem c, Rect2 r, Color col) => c.DrawTextureRect(Px, r, false, col);

    public static void Outline(CanvasItem c, Rect2 r, Color col, float w = 1f)
    {
        Rect(c, new Rect2(r.Position, new Vector2(r.Size.X, w)), col);
        Rect(c, new Rect2(r.Position.X, r.End.Y - w, r.Size.X, w), col);
        Rect(c, new Rect2(r.Position.X, r.Position.Y + w, w, r.Size.Y - 2 * w), col);
        Rect(c, new Rect2(r.End.X - w, r.Position.Y + w, w, r.Size.Y - 2 * w), col);
    }

    public static void Line(CanvasItem c, Vector2 a, Vector2 b, Color col, float w = 1f)
    {
        var d = b - a; float len = d.Length(); if (len < .01f) return;
        c.DrawSetTransform(a, d.Angle(), Vector2.One);
        c.DrawTextureRect(LineT, new Rect2(0, -w * .5f, len, w), false, col);
        c.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }

    public static void Disc(CanvasItem c, Vector2 p, float r, Color col) => c.DrawTextureRect(DiscT, new Rect2(p - new Vector2(r, r), new Vector2(r * 2, r * 2)), false, col);

    /// <summary>Дуга из коротких отрезков line.png; полный круг — a0=0, a1=Tau.</summary>
    public static void Arc(CanvasItem c, Vector2 p, float r, float a0, float a1, int segs, Color col, float w = 1f)
    {
        segs = Mathf.Max(3, segs); var prev = p + Vector2.FromAngle(a0) * r;
        for (int i = 1; i <= segs; i++) { var pt = p + Vector2.FromAngle(Mathf.Lerp(a0, a1, i / (float)segs)) * r; Line(c, prev, pt, col, w); prev = pt; }
    }
    public static void Ring(CanvasItem c, Vector2 p, float r, int segs, Color col, float w = 1f) => Arc(c, p, r, 0, Mathf.Tau, segs, col, w);

    public static void Triangle(CanvasItem c, Vector2 tip, Vector2 dir, float size, Color col)
    {
        c.DrawSetTransform(tip, dir.Angle(), Vector2.One);
        c.DrawTextureRect(ArrowT, new Rect2(-size, -size * .5f, size, size), false, col);
        c.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }

    /// <summary>Полоска прогресса над объектом: тёмная подложка + заливка нужного цвета.</summary>
    public static void Bar(CanvasItem c, Vector2 pos, float w, float h, float f, Color fill, float backAlpha = .7f)
    {
        Rect(c, new Rect2(pos, new Vector2(w, h)), new Color(0, 0, 0, backAlpha));
        Rect(c, new Rect2(pos, new Vector2(w * Mathf.Clamp(f, 0, 1), h)), fill);
    }

    /// <summary>Трещины на блоке: кадр crack_N из PNG по прогрессу добычи (0..1).</summary>
    public static void CrackOverlay(CanvasItem c, Vector2 center, float k)
        => c.DrawTextureRect(Crack(Mathf.Clamp((int)(k * 4), 0, 3)), new Rect2(center - new Vector2(8, 8), new Vector2(16, 16)), false);
}
