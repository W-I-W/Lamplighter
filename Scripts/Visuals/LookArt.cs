using Godot;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Отрисовка героя из PNG-слоёв (Assets/Sprites/Player/Look, см. tools/GraphicsGenerator/make_look_art.py): контур, кожа, одежда 1 и 2, детали, волосы.
/// Тонируемые слои нарисованы в оттенках серого и умножаются на цвет палитры Look, поэтому внешность меняется без новых текстур во время игры.</summary>
public static class LookArt
{
    const string Dir = "res://Assets/Sprites/Player/Look/";
    static readonly Dictionary<string, Texture2D> cache = new();

    static Texture2D T(string n)
    {
        if (cache.TryGetValue(n, out var t)) return t;
        t = GD.Load<Texture2D>(Dir + n + ".png"); cache[n] = t; return t;
    }

    public static bool Available() => T("skin") != null && T("detail_v0") != null;

    static readonly Color OutlineCol = new(16 / 255f, 20 / 255f, 42 / 255f);

    /// <summary>Контур считается в коде по общему силуэту слоёв (кожа, одежда, детали, лампа, волосы) выбранной комбинации «фасон × причёска» и кэшируется:
    /// новые причёски и фасоны не требуют отдельных листов контура.</summary>
    static Texture2D Outline(Look l)
    {
        string key = $"outline_v{l.Variant}_h{l.Hair}";
        if (cache.TryGetValue(key, out var cached)) return cached;
        var layers = new[] { "skin", $"cloth1_v{l.Variant}", $"cloth2_v{l.Variant}", $"detail_v{l.Variant}", $"lamp_v{l.Variant}", $"hair_{l.Hair}" };
        Image sil = null;
        foreach (var n in layers)
        {
            var img = T(n)?.GetImage(); if (img == null) continue;
            if (img.IsCompressed()) img.Decompress();
            img.Convert(Image.Format.Rgba8);
            if (sil == null) { sil = Image.CreateEmpty(img.GetWidth(), img.GetHeight(), false, Image.Format.Rgba8); }
            if (img.GetSize() != sil.GetSize()) continue;
            for (int y = 0; y < img.GetHeight(); y++)
                for (int x = 0; x < img.GetWidth(); x++)
                    if (img.GetPixel(x, y).A > .01f) sil.SetPixel(x, y, Colors.White);
        }
        if (sil == null) return null;
        int w = sil.GetWidth(), h = sil.GetHeight(), cw = 16;                // кадры лежат в ряд по 16 px: контур не должен переходить через границу кадра
        var res = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (sil.GetPixel(x, y).A > 0) continue;
                int fx = x / cw * cw;
                bool near = (x - 1 >= fx && sil.GetPixel(x - 1, y).A > 0) || (x + 1 < fx + cw && x + 1 < w && sil.GetPixel(x + 1, y).A > 0)
                    || (y > 0 && sil.GetPixel(x, y - 1).A > 0) || (y + 1 < h && sil.GetPixel(x, y + 1).A > 0);
                if (near) res.SetPixel(x, y, OutlineCol);
            }
        var tex = ImageTexture.CreateFromImage(res); cache[key] = tex; return tex;
    }

    /// <summary>Рисует кадр героя (frame 0/1) в клетке 16×16 с левым верхним углом pos; height &lt; 16 обрезает низ (стоит в воде). scale — для крупного предпросмотра в интерфейсе.</summary>
    public static void Draw(CanvasItem c, Look l, int frame, Vector2 pos, Color mod, float height = 16, float scale = 1, bool lamp = true)
    {
        l ??= new Look(); l.Clamp();
        float h = Mathf.Clamp(height, 1, 16); var src = new Rect2(frame * 16, 0, 16, h); var dst = new Rect2(pos, new Vector2(16, h) * scale);
        void L(string n, Color col) { var t = T(n); if (t != null) c.DrawTextureRectRegion(t, dst, src, col * mod); }
        var ol = Outline(l); if (ol != null) c.DrawTextureRectRegion(ol, dst, src, Colors.White * mod);
        L("skin", Look.SkinCols[l.Skin]);
        L($"cloth1_v{l.Variant}", Look.ClothCols[l.Cloth1]);
        L($"cloth2_v{l.Variant}", Look.ClothCols[l.Cloth2]);
        L($"detail_v{l.Variant}", Colors.White);
        L(lamp ? $"lamp_v{l.Variant}" : $"lampoff_v{l.Variant}", Colors.White);      // лампа горит (есть у героя) или потухла и не светится
        L($"hair_{l.Hair}", Look.HairCols[l.HairCol]);
    }
}
