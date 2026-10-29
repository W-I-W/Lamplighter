using Godot;

namespace Lamplighter;

/// <summary>Трекер события: одна строка под блоком региона — ближайшее активное событие. Подробности (расстояние) — по [G]. Без событий блока нет; в минимальном режиме скрыт.</summary>
public partial class Hud
{
    void DrawEventTracker(Vector2 vs)
    {
        if (G.Events.Count == 0 || G.Window != Win.None || HudDensity.Minimal) return;
        GEvent best = null; float bd = float.MaxValue; foreach (var e in G.Events) { float d = e.Pos.DistanceTo(G.PPos); if (d < bd) { bd = d; best = e; } }
        if (best == null) return;
        bool more = HudDensity.Full || (G.GameHudNode?.GoalsOpen ?? false);
        string t = Loc.T("Событие") + ": " + Loc.T(Game.EvName(best.Kind)) + (more ? $" · {(int)(bd / 16)}" : "");
        float w = TextW(t) + 18, y = (G.GameHudNode?.RightBottom ?? 4) + 3;
        var r = new Rect2(vs.X - 4 - w, y, w, 14); Panel(r, "PanelDark");
        Diamond(new Vector2(r.Position.X + 7, r.Position.Y + 7), 3f, Game.EvColor(best.Kind));
        Txt(t, new Vector2(r.Position.X + 13, r.Position.Y + 10), 9, new Color(.78f, .82f, .9f));
    }
}
