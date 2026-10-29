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

    public static bool Available() => T("skin") != null && T("detail") != null;

    /// <summary>Рисует кадр героя (frame 0/1) в клетке 16×16 с левым верхним углом pos; height &lt; 16 обрезает низ (стоит в воде). scale — для крупного предпросмотра в интерфейсе.</summary>
    public static void Draw(CanvasItem c, Look l, int frame, Vector2 pos, Color mod, float height = 16, float scale = 1)
    {
        l ??= new Look(); l.Clamp();
        float h = Mathf.Clamp(height, 1, 16); var src = new Rect2(frame * 16, 0, 16, h); var dst = new Rect2(pos, new Vector2(16, h) * scale);
        void L(string n, Color col) { var t = T(n); if (t != null) c.DrawTextureRectRegion(t, dst, src, col * mod); }
        L($"outline_v{l.Variant}_h{l.Hair}", Colors.White);
        L("skin", Look.SkinCols[l.Skin]);
        L($"cloth1_v{l.Variant}", Look.ClothCols[l.Cloth1]);
        L($"cloth2_v{l.Variant}", Look.ClothCols[l.Cloth2]);
        L("detail", Colors.White);
        L($"hair_{l.Hair}", Look.HairCols[l.HairCol]);
    }
}
