using Godot;
using System;

namespace Lamplighter;

/// <summary>Подсветка интерактивных объектов: контур выбранного, мягкий контур под курсором, значок действия и название. Плавное появление/исчезновение.
/// Параметры — в Resources/VisualConfigs/HighlightStyle.tres. Логику выбора не содержит: читает InteractionController.</summary>
public partial class Game
{
    public float selA, hovA; public Rect2 selRect, hovRect; public string selTitle = ""; public bool selHasE, selHasU;
    HighlightStyle hl;

    HighlightStyle Style => hl ??= VisualCatalog.Get()?.Highlight ?? new HighlightStyle();

    void UpdateSelectionVisual(float dt)
    {
        var s = Interact.Selected; var h = Interact.Hover;
        DetailT = Mathf.Max(0, DetailT - dt); float k = Style.FadeSpeed * dt;
        if (s != null) { selRect = s.Bounds; selTitle = s.Title; selHasE = Cur.E != null; selHasU = Cur.U != null; selA = Mathf.Min(1, selA + k); } else selA = Mathf.Max(0, selA - k);
        if (h != null && h != s) { hovRect = h.Bounds; hovA = Mathf.Min(1, hovA + k); } else hovA = Mathf.Max(0, hovA - k);
    }

    void DrawSelection(EntityLayer c)
    {
        var st = Style;
        if (hovA > .01f)
        {
            var col = st.HoverColor; col.A = st.HoverAlpha * hovA;
            c.DrawRect(hovRect.Grow(1), col, false, 1f);
        }
        if (selA > .01f)
        {
            var col = st.SelectedColor; var r = selRect.Grow(1.5f);
            c.DrawRect(r, new Color(col.R, col.G, col.B, st.FillAlpha * selA), true);
            c.DrawRect(r.Grow(1), new Color(col.R, col.G, col.B, .25f * selA), false, 2f);
            c.DrawRect(r, new Color(col.R, col.G, col.B, selA), false, 1f);
        }
    }

    /// <summary>Предпросмотр постройки под курсором, пока открыто меню строительства: зелёный — можно ставить, красный — нельзя.</summary>
    void DrawBuildGhost(EntityLayer c)
    {
        if (Window != Win.Build) return;
        var list = BuildEntriesFiltered(); if (WinCur >= list.Count) return; var e = list[WinCur]; if (e.Tile < 0) return;
        var t = ToTile(Aim); var center = TileCenter(t.X, t.Y);
        bool ok = e.Unlocked && W.Get(t.X, t.Y) == Tl.None && PPos.DistanceTo(center) <= 56f && ToTile(PPos) != t && W.Ground[World.Key(t.X, t.Y)] != Art.WaterG;
        var col = ok ? new Color(.45f, 1f, .45f, .75f) : new Color(1f, .4f, .35f, .6f);
        c.DrawTextureRectRegion(Art.TileTex, new Rect2(center - new Vector2(8, 8), new Vector2(16, 16)), new Rect2(e.Tile * 16, 0, 16, 16), col);
        c.DrawRect(new Rect2(center - new Vector2(8, 8), new Vector2(16, 16)), new Color(col.R, col.G, col.B, .9f), false, 1f);
    }
}
