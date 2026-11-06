using Godot;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Метки интереса и компас в мире. Обычный режим: только ближайшие метки, дальние скрыты, числа — лишь у самой ближней и у цели; метки плавно появляются и гаснут.
/// Минимальный режим — только стрелка к цели без чисел; полный — все метки с числами (как раньше).</summary>
public partial class Hud
{
    struct Mk { public Vector2 Pos; public Color Col; public string Name; public float Seed, Dist; public long Key; public int Icon; public bool Prio; }          // Icon: индекс тайла + 1 (0 — ромбик); так видно, как выглядит искомый предмет
    readonly Dictionary<long, float> markA = new();
    const int MarkNear = 3; const float MarkRangeTiles = 42f;
    float markT;

    /// <summary>Пока открыто любое окно (инвентарь, экипировка, навыки, карта), указатели и стрелки компаса не рисуются: они ложатся поверх интерфейса.</summary>
    bool PointersHidden => G.Window != Win.None || G.TechOpen || G.MapOpen;

    void DrawMarkers(Vector2 vs)
    {
        if (PointersHidden) return;
        float dt = Mathf.Min(.1f, (float)(G.UiTime - markT)); if (dt < 0) dt = 0; markT = (float)G.UiTime;
        var list = new List<Mk>();
        if (!HudDensity.Minimal)
        {
            foreach (var p in G.MarkerPois()) list.Add(new Mk { Pos = p.Pos, Col = Game.PoiColor(p.Kind), Name = Game.PoiName(p.Kind), Seed = p.Tile.X, Key = p.Tile.X * 100003L + p.Tile.Y, Icon = p.Kind == PK.TechCache ? Art.TechCache + 1 : 0 });
            foreach (var ev in G.Events) list.Add(new Mk { Pos = ev.Pos, Col = Game.EvColor(ev.Kind), Name = Game.EvName(ev.Kind), Seed = ev.Seed, Key = -1 - (long)ev.Seed });
        }
        if (G.NextGravePos() is Vector2 gp) list.Add(new Mk { Pos = gp, Col = new Color("#c8d0e8"), Name = "Могила", Seed = 1, Key = -900000000L, Prio = true });         // могила видна всегда, пока в ней лежат вещи
        if (G.Goals.Count > 0 && G.Goals[0].Target is Vector2 gt)                                                                                            // метка текущего задания: единственная «цель» на экране, показывается всегда
        {
            int at = list.FindIndex(q => q.Pos.DistanceTo(gt) < 12f);
            if (at >= 0) { var q = list[at]; q.Prio = true; list[at] = q; }                                                                                  // у цели уже есть своя метка (постамент, место): просто делаем её главной
            else list.Add(new Mk { Pos = gt, Col = new Color(.55f, 1f, .9f), Name = G.Goals[0].Title, Seed = 2, Key = -800000000L, Prio = true });
        }
        for (int i = 0; i < list.Count; i++) { var m = list[i]; m.Dist = m.Pos.DistanceTo(G.PPos) / 16f; list[i] = m; }
        list.Sort((a, b) => a.Prio != b.Prio ? (a.Prio ? -1 : 1) : a.Dist.CompareTo(b.Dist));
        int max = HudDensity.Full ? int.MaxValue : MarkNear; float range = HudDensity.Full ? 9999f : MarkRangeTiles;
        var seen = new HashSet<long>();
        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i]; bool want = i < max && (m.Dist <= range || m.Prio); seen.Add(m.Key);
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
            s = s.Round();
            if (m.Icon > 0) Tex(UiIcons.Tile(m.Icon - 1), new Rect2(s.X - 8, s.Y - 30 + Mathf.Round(Mathf.Sin(G.Time * 4f) * 1.5f), 16, 16), new Color(1, 1, 1, Mathf.Clamp(fade, 0, 1)));    // значок постамента над точкой
            Diamond(s, rank == 0 ? 3.5f : 2.5f, new Color(m.Col.R, m.Col.G, m.Col.B, pulse));
            if (rank == 0 || HudDensity.Full || m.Dist < 10) Txt(numbers ? $"{m.Name} · {(int)m.Dist}" : m.Name, s + new Vector2(-50, 14), 9, new Color(m.Col.R, m.Col.G, m.Col.B, (rank == 0 ? 1f : .6f) * fade), HorizontalAlignment.Center, 100);
        }
        else if ((rank == 0 || HudDensity.Full) && !m.Prio)          // у главных меток (задание, могила) стрелка одна: компас у героя, поэтому на краю экрана не дублируется
        {
            var c = new Vector2(Mathf.Clamp(s.X, 16, vs.X - 16), Mathf.Clamp(s.Y, 60, vs.Y - 60)); var dir = (s - vs * .5f).Normalized();
            ArrowMark(c, dir, new Color(m.Col.R, m.Col.G, m.Col.B, pulse));
            if (numbers) Txt($"{(int)m.Dist}", c - dir * 8 - new Vector2(6, -3), 9, new Color(m.Col.R, m.Col.G, m.Col.B, .8f * fade), HorizontalAlignment.Center, 12);
        }
    }

    /// <summary>Стрелки к древнему фонарю и к цели. Числа — только у цели при раскрытом списке целей ([G]) и в полном режиме.</summary>
    void DrawCompass()
    {
        if (PointersHidden) return;
        float pulse = .5f + .2f * Mathf.Sin(G.Time * 4f); bool nums = HudDensity.Full || (G.GameHudNode?.GoalsOpen ?? false);
        void One(Vector2 target, Color col, float radius, bool number, bool hideOnScreen = false)
        {
            if (hideOnScreen) { var sp = ToScreen(target); if (sp.X > 16 && sp.X < Size.X - 16 && sp.Y > 60 && sp.Y < Size.Y - 60) return; }          // цель на экране: видна метка, стрелка не нужна
            var d = target - G.PPos; float dist = d.Length(); if (dist < 70) return;
            var dir = d.Normalized(); var c = ToScreen(G.PPos) + dir * radius;
            ArrowMark(c, dir, new Color(col.R, col.G, col.B, pulse));
            if (number) Txt($"{(int)(dist / 16)}", c + dir * 10 - new Vector2(6, -3), 9, new Color(col.R, col.G, col.B, .8f), HorizontalAlignment.Center, 12);
        }
        if (G.NextGravePos() is Vector2 grave) One(grave, new Color("#c8d0e8"), 50, true, true);       // стрелка к могиле
        var anc = G.CompassTarget(); bool goalPoints = G.Goals.Count > 0 && G.Goals[0].Target != null;
        if (anc != null && !HudDensity.Minimal && !goalPoints) One(anc.Value, new Color(1f, .88f, .55f), 34, HudDensity.Full);      // пока есть задание с целью «идите/ищите», стрелка к фонарю отключена
        if (goalPoints) One(G.Goals[0].Target.Value, new Color(.55f, 1f, .9f), 42, nums, true);
    }
}
