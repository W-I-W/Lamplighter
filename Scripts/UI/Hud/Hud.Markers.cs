using Godot;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Метки интереса и компас в мире. Обычный режим: только ближайшие метки, дальние скрыты, числа — лишь у самой ближней и у цели; метки плавно появляются и гаснут.
/// Минимальный режим — только стрелка к цели без чисел; полный — все метки с числами (как раньше).</summary>
public partial class Hud
{
    struct Mk { public Vector2 Pos; public Color Col; public string Name; public float Seed, Dist; public long Key; }
    readonly Dictionary<long, float> markA = new();
    const int MarkNear = 3; const float MarkRangeTiles = 42f;
    float markT;

    void DrawMarkers(Vector2 vs)
    {
        float dt = Mathf.Min(.1f, (float)(G.UiTime - markT)); if (dt < 0) dt = 0; markT = (float)G.UiTime;
        var list = new List<Mk>();
        if (!HudDensity.Minimal)
        {
            foreach (var p in G.MarkerPois()) list.Add(new Mk { Pos = p.Pos, Col = Game.PoiColor(p.Kind), Name = Game.PoiName(p.Kind), Seed = p.Tile.X, Key = p.Tile.X * 100003L + p.Tile.Y });
            foreach (var ev in G.Events) list.Add(new Mk { Pos = ev.Pos, Col = Game.EvColor(ev.Kind), Name = Game.EvName(ev.Kind), Seed = ev.Seed, Key = -1 - (long)ev.Seed });
        }
        for (int i = 0; i < list.Count; i++) { var m = list[i]; m.Dist = m.Pos.DistanceTo(G.PPos) / 16f; list[i] = m; }
        list.Sort((a, b) => a.Dist.CompareTo(b.Dist));
        int max = HudDensity.Full ? int.MaxValue : MarkNear; float range = HudDensity.Full ? 9999f : MarkRangeTiles;
        var seen = new HashSet<long>();
        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i]; bool want = i < max && m.Dist <= range; seen.Add(m.Key);
            markA.TryGetValue(m.Key, out float a); a = Mathf.MoveToward(a, want ? 1f : 0f, dt * 2.5f); markA[m.Key] = a;
            if (a > .01f) Marker(m, i == 0 || HudDensity.Full, i, a, vs);
        }
        foreach (var k in new List<long>(markA.Keys)) if (!seen.Contains(k)) { if (markA[k] <= .01f) markA.Remove(k); else markA[k] = Mathf.MoveToward(markA[k], 0, dt * 2.5f); }
        DrawCompass();
    }

    void Marker(Mk m, bool numbers, int rank, float fade, Vector2 vs)
    {
        var s = ToScreen(m.Pos); float pulse = (.65f + .35f * Mathf.Sin(G.Time * 3f + m.Seed)) * (rank == 0 ? 1f : .55f) * fade;
        bool inside = s.X > 16 && s.X < vs.X - 16 && s.Y > 60 && s.Y < vs.Y - 60;
        if (inside)
        {
            s = s.Round(); Diamond(s, rank == 0 ? 3.5f : 2.5f, new Color(m.Col.R, m.Col.G, m.Col.B, pulse));
            if (rank == 0 || HudDensity.Full || m.Dist < 10) Txt(numbers ? $"{m.Name} · {(int)m.Dist}" : m.Name, s + new Vector2(-50, 14), 9, new Color(m.Col.R, m.Col.G, m.Col.B, (rank == 0 ? 1f : .6f) * fade), HorizontalAlignment.Center, 100);
        }
        else if (rank == 0 || HudDensity.Full)
        {
            var c = new Vector2(Mathf.Clamp(s.X, 16, vs.X - 16), Mathf.Clamp(s.Y, 60, vs.Y - 60)); var dir = (s - vs * .5f).Normalized();
            ArrowMark(c, dir, new Color(m.Col.R, m.Col.G, m.Col.B, pulse));
            if (numbers) Txt($"{(int)m.Dist}", c - dir * 8 - new Vector2(6, -3), 9, new Color(m.Col.R, m.Col.G, m.Col.B, .8f * fade), HorizontalAlignment.Center, 12);
        }
    }

    /// <summary>Стрелки к древнему фонарю и к цели. Числа — только у цели при раскрытом списке целей ([G]) и в полном режиме.</summary>
    void DrawCompass()
    {
        float pulse = .5f + .2f * Mathf.Sin(G.Time * 4f); bool nums = HudDensity.Full || (G.GameHudNode?.GoalsOpen ?? false);
        void One(Vector2 target, Color col, float radius, bool number)
        {
            var d = target - G.PPos; float dist = d.Length(); if (dist < 70) return;
            var dir = d.Normalized(); var c = ToScreen(G.PPos) + dir * radius;
            ArrowMark(c, dir, new Color(col.R, col.G, col.B, pulse));
            if (number) Txt($"{(int)(dist / 16)}", c + dir * 10 - new Vector2(6, -3), 9, new Color(col.R, col.G, col.B, .8f), HorizontalAlignment.Center, 12);
        }
        var anc = G.CompassTarget();
        if (anc != null && !HudDensity.Minimal) One(anc.Value, new Color(1f, .88f, .55f), 34, HudDensity.Full);
        if (G.Goals.Count > 0 && G.Goals[0].Target != null && (anc == null || HudDensity.Minimal || G.Goals[0].Target.Value.DistanceTo(anc.Value) > 8))
            One(G.Goals[0].Target.Value, new Color(.55f, 1f, .9f), 42, nums);
    }
}
