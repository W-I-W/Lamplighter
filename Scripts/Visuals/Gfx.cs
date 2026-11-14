using Godot;

namespace Lamplighter;

/// <summary>Эффекты мира собираются только из PNG в Assets/Sprites/Vfx (px, line, disc, arrow): код лишь размещает и красит текстуры.
/// Заменив PNG своим рисунком, вы меняете вид всех полосок, колец, лучей и частиц.</summary>
public static class Gfx
{
    const string Dir = "res://Assets/Sprites/Vfx/";
    static Texture2D px, line, disc, arrow;
    static Texture2D Px => px ??= GD.Load<Texture2D>(Dir + "px.png");
    static Texture2D LineT => line ??= GD.Load<Texture2D>(Dir + "line.png");
    static Texture2D DiscT => disc ??= GD.Load<Texture2D>(Dir + "disc.png");
    static Texture2D ArrowT => arrow ??= GD.Load<Texture2D>(Dir + "arrow.png");

    public static void Rect(CanvasItem c, Rect2 r, Color col) => c.DrawTextureRect(Px, r, false, col);

    // ───────────── мягкие частицы: fx_*.png (tools/GraphicsGenerator/make_vfx_soft.py) рисуются со сглаживанием и mip-уровнями,
    // поэтому даже частица в 1–2 пикселя мира выглядит круглой, а не квадратиком. Проект по умолчанию рисует «ближайшим пикселем»,
    // фильтр задаётся самой текстурой через CanvasTexture.
    static readonly System.Collections.Generic.Dictionary<string, Texture2D> soft = new();
    static Texture2D Soft(string n)
    {
        if (soft.TryGetValue(n, out var t)) return t;
        var src = GD.Load<Texture2D>(Dir + n + ".png"); Texture2D res = src;
        if (src != null)
        {
            var img = src.GetImage(); if (img.IsCompressed()) img.Decompress(); img.Convert(Image.Format.Rgba8); img.GenerateMipmaps();
            res = new CanvasTexture { DiffuseTexture = ImageTexture.CreateFromImage(img), TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps };
        }
        soft[n] = res; return res;
    }
    public static Texture2D SoftTex => Soft("fx_soft");
    public static Texture2D DotTex => Soft("fx_dot");
    public static Texture2D SlashTex => Soft("fx_slash");
    public static Texture2D RingTex => Soft("fx_ring");
    public static Texture2D SplatTex => Soft("fx_splat");

    /// <summary>Круглое мягкое свечение радиуса r.</summary>
    public static void Glow(CanvasItem c, Vector2 p, float r, Color col) => c.DrawTextureRect(Soft("fx_soft"), new Rect2(p - new Vector2(r, r), new Vector2(r * 2, r * 2)), false, col);
    /// <summary>Мягкое овальное свечение с полуосями rx, ry (рябь воды, отсвет на земле).</summary>
    public static void GlowOval(CanvasItem c, Vector2 p, float rx, float ry, Color col) => c.DrawTextureRect(Soft("fx_soft"), new Rect2(p - new Vector2(rx, ry), new Vector2(rx * 2, ry * 2)), false, col);
    /// <summary>Круглая точка радиуса r: плотное ядро, мягкий край.</summary>
    public static void Dot(CanvasItem c, Vector2 p, float r, Color col) => c.DrawTextureRect(Soft("fx_dot"), new Rect2(p - new Vector2(r, r), new Vector2(r * 2, r * 2)), false, col);
    /// <summary>Четырёхлучевая звезда с ореолом, полуразмер r.</summary>
    public static void Star(CanvasItem c, Vector2 p, float r, Color col) => c.DrawTextureRect(Soft("fx_star"), new Rect2(p - new Vector2(r, r), new Vector2(r * 2, r * 2)), false, col);
    /// <summary>Клуб дыма радиуса r, повёрнутый на rot.</summary>
    public static void Puff(CanvasItem c, Vector2 p, float r, float rot, Color col) => Sprite(c, Soft("fx_puff"), p, new Vector2(r * 2, r * 2), rot, col);
    /// <summary>Осколок размера s, повёрнутый на rot.</summary>
    public static void Shard(CanvasItem c, Vector2 p, float s, float rot, Color col) => Sprite(c, Soft("fx_shard"), p, new Vector2(s, s), rot, col);
    /// <summary>Штрих искры от хвоста a к головке b толщиной w.</summary>
    public static void Streak(CanvasItem c, Vector2 a, Vector2 b, Color col, float w = 1.5f)
    {
        var d = b - a; float len = d.Length(); if (len < .01f) return;
        c.DrawSetTransform(a, d.Angle(), Vector2.One);
        c.DrawTextureRect(Soft("fx_spark"), new Rect2(0, -w, len, w * 2), false, col);
        c.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }
    /// <summary>Вертикальный луч: основание в p, высота h, ширина w.</summary>
    public static void Ray(CanvasItem c, Vector2 p, float h, float w, Color col) => c.DrawTextureRect(Soft("fx_ray"), new Rect2(p.X - w * .5f, p.Y - h, w, h), false, col);

    static void Sprite(CanvasItem c, Texture2D t, Vector2 p, Vector2 size, float rot, Color col)
    {
        c.DrawSetTransform(p, rot, Vector2.One);
        c.DrawTextureRect(t, new Rect2(-size * .5f, size), false, col);
        c.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }

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

}
