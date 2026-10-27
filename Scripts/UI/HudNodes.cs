using Godot;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Набор узлов-«кирпичей» для игровых окон: Label, PanelContainer, ProgressBar, ColorRect и TextureRect из пулов.
/// Ничего не рисуется вручную: каждый кадр окна раскладывают по уже существующим узлам, стили берутся из темы Resources/UI/LanternTheme.tres,
/// картинки — из PNG. Порядок вызовов задаёт порядок наложения (ZIndex).</summary>
public partial class Hud
{
    readonly List<Label> labels = new(); readonly List<ColorRect> rects = new(); readonly List<PanelContainer> panels = new();
    readonly List<ProgressBar> bars = new(); readonly List<TextureRect> texs = new();
    int nLabel, nRect, nPanel, nBar, nTex, seq; Color curMod = Colors.White;
    static Texture2D arrowTex, diamondTex, diamondLineTex, discTex, lineTex;
    static Texture2D Vfx(ref Texture2D t, string n) => t ??= GD.Load<Texture2D>("res://Assets/Sprites/Vfx/" + n + ".png");

    void BeginFrame() { nLabel = nRect = nPanel = nBar = nTex = seq = 0; curMod = Colors.White; }

    void EndFrame()
    {
        for (int i = nLabel; i < labels.Count; i++) labels[i].Visible = false;
        for (int i = nRect; i < rects.Count; i++) rects[i].Visible = false;
        for (int i = nPanel; i < panels.Count; i++) panels[i].Visible = false;
        for (int i = nBar; i < bars.Count; i++) bars[i].Visible = false;
        for (int i = nTex; i < texs.Count; i++) texs[i].Visible = false;
    }

    T Take<T>(List<T> list, ref int used) where T : Control, new()
    {
        T n;
        if (used < list.Count) n = list[used];
        else { n = new T { MouseFilter = MouseFilterEnum.Ignore }; list.Add(n); AddChild(n); }
        used++; n.Visible = true; n.ZIndex = Mathf.Min(seq++, 4000); return n;
    }

    /// <summary>Заливка прямоугольника цветом (затемнение, подложки, точки карты).</summary>
    void Rect(Rect2 r, Color c) { var n = Take(rects, ref nRect); n.Position = r.Position; n.Size = r.Size; n.Color = c * curMod; }

    /// <summary>Панель/кнопка/вкладка из темы по имени вариации (PanelContainer — стандартная панель).</summary>
    void Sty(Rect2 r, string variation)
    {
        var n = Take(panels, ref nPanel); string v = variation == "PanelContainer" ? "" : variation;
        if (n.ThemeTypeVariation != v) n.ThemeTypeVariation = v;
        n.Position = r.Position; n.Size = r.Size; n.Modulate = curMod;
    }

    /// <summary>Текстура (PNG/атлас) в прямоугольнике; rot — поворот вокруг центра.</summary>
    void Tex(Texture2D t, Rect2 r, Color? c = null, float rot = 0)
    {
        var n = Take(texs, ref nTex);
        if (n.Texture != t) n.Texture = t;
        n.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize; n.StretchMode = TextureRect.StretchModeEnum.Scale;
        n.Position = r.Position; n.Size = r.Size; n.PivotOffset = r.Size * .5f; n.Rotation = rot; n.Modulate = (c ?? Colors.White) * curMod;
    }

    void Diamond(Vector2 c, float half, Color col, bool outline = false)
        => Tex(outline ? Vfx(ref diamondLineTex, "diamond_line") : Vfx(ref diamondTex, "diamond"), new Rect2(c - new Vector2(half, half), new Vector2(half * 2 + 1, half * 2 + 1)), col);

    void Disc(Vector2 c, float r, Color col) => Tex(Vfx(ref discTex, "disc"), new Rect2(c - new Vector2(r, r), new Vector2(r * 2, r * 2)), col);

    void ArrowMark(Vector2 center, Vector2 dir, Color col)
        => Tex(Vfx(ref arrowTex, "arrow"), new Rect2(center + dir * 1.5f - new Vector2(4, 3.5f), new Vector2(8, 7)), col, dir.Angle());

    void LineUi(Vector2 a, Vector2 b, Color col, float w = 1f)
    {
        var d = b - a; float len = d.Length(); if (len < .01f) return;
        Tex(Vfx(ref lineTex, "line"), new Rect2(a - new Vector2(0, w * .5f), new Vector2(len, w)), col, d.Angle());
        texs[nTex - 1].PivotOffset = new Vector2(0, w * .5f);
    }

    /// <summary>Подпись пиксельным шрифтом из темы (PlainLabel / BigLabel). Цвет — через Modulate, тень берётся из темы.</summary>
    Label Lbl(string s, Vector2 p, bool big, Color c, HorizontalAlignment al, float w)
    {
        var n = Take(labels, ref nLabel);
        n.ClipText = true; n.AutowrapMode = TextServer.AutowrapMode.Off;
        string v = big ? "BigLabel" : "PlainLabel"; if (n.ThemeTypeVariation != v) n.ThemeTypeVariation = v;
        if (n.Text != s) n.Text = s;
        if (n.HorizontalAlignment != al) n.HorizontalAlignment = al;
        float tw = TextW(s, big ? 14 : 9) + 3, asc = big ? 14 : 7, width = w > 0 ? w : tw, x = p.X;
        if (tw > width) { if (al == HorizontalAlignment.Center) x -= (tw - width) * .5f; else if (al == HorizontalAlignment.Right) x -= tw - width; width = tw; }   // текст не обрезается
        n.Position = new Vector2(Mathf.Round(x), Mathf.Round(p.Y) - asc); n.Size = new Vector2(width, big ? 22 : 11); n.Modulate = c * curMod;
        return n;
    }
}
