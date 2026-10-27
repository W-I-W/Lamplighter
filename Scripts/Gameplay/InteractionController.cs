using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Поиск, выбор и устойчивость выбора интерактивных объектов. Знает только IInteractable — всю логику действий хранят сами объекты.
/// Правила: берётся только объект в радиусе Reach (до ближайшей точки его области), побеждает ближайший, равенство решают Priority и Key,
/// активная цель меняется лишь когда новая ближе на Margin пикселей или прежняя недоступна; клик мышью закрепляет цель, пока она доступна.</summary>
public sealed class InteractionController
{
    public const float Reach = 36f, Margin = 6f, HoverPad = 2f;
    public IInteractable Selected { get; private set; }
    public IInteractable Hover { get; private set; }
    public bool Pinned { get; private set; }
    string selKey, pinKey;

    /// <summary>Расстояние от точки до ближайшей точки прямоугольника (0, если внутри).</summary>
    public static float Distance(Vector2 p, Rect2 r)
    {
        float dx = Mathf.Max(Mathf.Max(r.Position.X - p.X, 0), p.X - r.End.X), dy = Mathf.Max(Mathf.Max(r.Position.Y - p.Y, 0), p.Y - r.End.Y);
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    public void Reset() { Selected = null; Hover = null; selKey = null; pinKey = null; Pinned = false; }

    /// <param name="enabled">false — режим строительства/окна/меню: выбор отключён.</param>
    public void Update(Vector2 player, Vector2? mouseWorld, bool mouseOverUi, List<IInteractable> all, bool enabled)
    {
        if (!enabled) { Selected = null; Hover = null; selKey = null; return; }
        var inReach = new List<(IInteractable c, float d)>();
        foreach (var c in all) { float d = Distance(player, c.Bounds); if (d <= Reach) inReach.Add((c, d)); }
        inReach.Sort((a, b) =>
        {
            int r = Math.Round(a.d * 2).CompareTo(Math.Round(b.d * 2)); if (r != 0) return r;
            r = a.c.Priority.CompareTo(b.c.Priority); return r != 0 ? r : string.CompareOrdinal(a.c.Key, b.c.Key);
        });
        // наведение мыши
        Hover = null;
        if (mouseWorld.HasValue && !mouseOverUi)
        {
            float bd = float.MaxValue;
            foreach (var (c, _) in inReach)
                if (c.Bounds.Grow(HoverPad).HasPoint(mouseWorld.Value)) { float dd = mouseWorld.Value.DistanceTo(c.Bounds.GetCenter()); if (dd < bd - .01f) { bd = dd; Hover = c; } }
        }
        // закреплённая кликом цель
        IInteractable pinned = null;
        if (pinKey != null) foreach (var (c, _) in inReach) if (c.Key == pinKey) { pinned = c; break; }
        if (pinned == null) { pinKey = null; Pinned = false; }
        if (pinned != null) { Selected = pinned; selKey = pinned.Key; Pinned = true; return; }
        // автоматический выбор с гистерезисом
        if (inReach.Count == 0) { Selected = null; selKey = null; return; }
        var best = inReach[0];
        if (selKey != null)
            foreach (var (c, d) in inReach)
                if (c.Key == selKey) { if (best.c.Key != selKey && best.d >= d - Margin) best = (c, d); break; }
        Selected = best.c; selKey = best.c.Key;
    }

    /// <summary>ЛКМ: закрепить наведённый объект; клик в пустоту снимает закрепление.</summary>
    public bool Click()
    {
        if (Hover != null) { pinKey = Hover.Key; return true; }
        pinKey = null; return false;
    }
}
