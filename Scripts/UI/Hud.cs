using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Слой интерфейса поверх мира в логических пикселях (640×360 при масштабе ×2/×3): маркеры, уведомления, шкала босса и игровые окна.
/// Основной HUD — сцена GameHud.tscn; здесь всё рисуется пиксельным шрифтом и текстурными рамками из темы Resources/UI/LanternTheme.tres.</summary>
public partial class Hud : Control
{
    public Game G;
    public const string ThemePath = "res://Resources/UI/LanternTheme.tres";
    Theme theme; Font font;

    static readonly Color Dim = new("#8a96b0"), Good = new("#9be07a"), Warn = new("#ffb36a"), Gold = new("#ffe08a"), Cyan = new("#8ff0ff"), Text = new("#d8e0f0"), Bad = new("#f06a5e");
    readonly List<Rect2> panelRects = new();

    public override void _Ready()
    {
        theme = GD.Load<Theme>(ThemePath); font = theme.DefaultFont ?? ThemeDB.FallbackFont;
    }

    // ───────────────────────── примитивы ─────────────────────────
    /// <summary>Текст пиксельным шрифтом. size ≥ 14 — двойной размер (целочисленное масштабирование). y — линия текста (верх строки + 7).</summary>
    void Txt(string s, Vector2 p, int size = 9, Color? c = null, HorizontalAlignment al = HorizontalAlignment.Left, float w = -1)
    {
        s = Loc.T(s); int fs = size >= 14 ? 18 : 9; p = new Vector2(Mathf.Round(p.X), Mathf.Round(p.Y));
        DrawString(font, p + new Vector2(1, 1), s, al, w, fs, new Color("#090c14"));
        DrawString(font, p, s, al, w, fs, c ?? Text);
    }

    float TextW(string s, int size = 9) => font.GetStringSize(Loc.T(s), HorizontalAlignment.Left, -1, size >= 14 ? 18 : 9).X;

    /// <summary>Перенос по словам: рисует абзац, возвращает y следующей строки. Строка = 10 px.</summary>
    float Para(string s, float x, float y, float w, Color? c = null, int maxLines = 99)
    {
        s = Loc.T(s); var words = s.Split(' '); string line = ""; int n = 0;
        foreach (var word in words)
        {
            string t = line == "" ? word : line + " " + word;
            if (TextW(t) > w && line != "") { Txt(line, new Vector2(x, y), 9, c); y += 10; line = word; if (++n >= maxLines) return y; } else line = t;
        }
        if (line != "") { Txt(line, new Vector2(x, y), 9, c); y += 10; }
        return y;
    }

    void Panel(Rect2 r, string variation = "PanelContainer")
    {
        r = new Rect2(r.Position.Round(), r.Size.Round()); panelRects.Add(r);
        theme.GetStylebox("panel", variation).Draw(GetCanvasItem(), r);
    }

    void Bar(Rect2 r, float f, string variation)
    {
        theme.GetStylebox("background", "ProgressBar").Draw(GetCanvasItem(), r);
        float w = Mathf.Round((r.Size.X - 2) * Mathf.Clamp(f, 0, 1));
        if (w > 0) theme.GetStylebox("fill", variation).Draw(GetCanvasItem(), new Rect2(r.Position + new Vector2(1, 1), new Vector2(w, r.Size.Y - 2)));
    }

    /// <summary>Окно: рамка, заголовок и подсказка клавиш внизу. Возвращает внутреннюю область.</summary>
    Rect2 Frame(Rect2 r, string title, string footer = null, Color? accent = null)
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0, 0, .03f, .55f));
        Panel(r, "PanelContainer");
        Txt(title, new Vector2(r.Position.X + 8, r.Position.Y + 14), 9, accent ?? Gold);
        theme.GetStylebox("panel", "PanelDark").Draw(GetCanvasItem(), new Rect2(r.Position.X + 6, r.Position.Y + 19, r.Size.X - 12, 3));
        if (footer != null) Txt(footer, new Vector2(r.Position.X + 8, r.End.Y - 6), 9, Dim, HorizontalAlignment.Left, r.Size.X - 16);
        return new Rect2(r.Position.X + 8, r.Position.Y + 26, r.Size.X - 16, r.Size.Y - 26 - 14);
    }

    /// <summary>Ряд вкладок; возвращает y под ними.</summary>
    float Tabs(float x, float y, string[] names, int cur)
    {
        foreach (var t in new[] { 0 }) { }
        for (int i = 0; i < names.Length; i++)
        {
            float w = TextW(names[i]) + 10; var r = new Rect2(x, y, w, 12);
            theme.GetStylebox(i == cur ? "normal" : "normal", i == cur ? "TabSelected" : "Tab").Draw(GetCanvasItem(), r);
            Txt(names[i], new Vector2(x + 5, y + 9), 9, i == cur ? Gold : Dim);
            x += w + 2;
        }
        return y + 16;
    }

    /// <summary>Строка списка с курсором. Возвращает y следующей строки.</summary>
    float Row(Rect2 a, float y, bool selected, string left, string right, Color col, float h = 11)
    {
        if (selected) { theme.GetStylebox("normal", "ButtonSelected").Draw(GetCanvasItem(), new Rect2(a.Position.X - 2, y - 8, a.Size.X + 4, h)); }
        Txt(left, new Vector2(a.Position.X + 2, y), 9, col, HorizontalAlignment.Left, a.Size.X - (right != null ? TextW(right) + 6 : 0));
        if (right != null) Txt(right, new Vector2(a.Position.X, y), 9, selected ? Gold : col, HorizontalAlignment.Right, a.Size.X - 2);
        return y + h;
    }

    int Scroll(int cur, int visible) => Mathf.Max(0, cur - visible + 1);
    Vector2 ToScreen(Vector2 w) => ((w - G.CamCenter) * Game.Zoom + G.ViewSize * .5f) / G.UiScaleVp;

    /// <summary>Находится ли точка (логические координаты) над панелью интерфейса.</summary>
    public bool IsOverUi(Vector2 logical) { foreach (var r in panelRects) if (r.HasPoint(logical)) return true; return G.GameHudNode != null && G.GameHudNode.IsOver(logical); }

    public override void _Draw()
    {
        if (G == null || theme == null) return;
        panelRects.Clear();
        var vs = Size; if (vs.X < 100 || vs.Y < 60) return;
        if (G.Menu == Mn.Main) { DrawMenu(vs); return; }
        if (G.LightBlockT > 0) DrawRect(new Rect2(Vector2.Zero, vs), new Color(0, 0, .05f, .25f));
        if (G.VictoryT > 0) { float vk = Mathf.Clamp(G.VictoryT / 3f, 0, 1); DrawRect(new Rect2(Vector2.Zero, vs), new Color(1f, .9f, .5f, .22f * vk * vk)); }
        DrawMarkers(vs); DrawEventMarkers(vs); DrawBossBar(vs); DrawFloaters(); DrawCompass(); DrawSelectionLabel();
        float y = DrawBanners(vs); DrawToasts(vs, y);
        if (G.LightBlockT > 0) Txt($"СВЕТ ПОГАШЕН · {G.LightBlockT:F0} с", new Vector2(0, vs.Y * .5f - 40), 14, new Color("#7ac8ff"), HorizontalAlignment.Center, vs.X);
        DrawStage4Hud(vs);
        if (G.TechOpen) DrawTech(vs);
        if (G.MapOpen) DrawMap(vs);
        DrawWindows(vs); DrawEndgameHud(vs); DrawFinale(vs); DrawMenu(vs);
        if (G.Hp / G.MaxHp < .3f) DrawRect(new Rect2(Vector2.Zero, vs), new Color(.6f, 0, 0, .12f + .06f * Mathf.Sin(G.Time * 6f)));
    }

    // ───────────────────────── мир: маркеры, компас, числа ─────────────────────────
    void DrawFloaters()
    {
        foreach (var t in G.Texts)
        {
            var p = ToScreen(t.P); var col = t.C; col.A = Mathf.Clamp(t.Life * 2, 0, 1);
            var ts = Loc.T(t.S); p = new Vector2(Mathf.Round(p.X), Mathf.Round(p.Y));
            DrawString(font, p - new Vector2(39, -1), ts, HorizontalAlignment.Center, 80, 9, new Color(0.035f, 0.047f, 0.078f, col.A));
            DrawString(font, p - new Vector2(40, 0), ts, HorizontalAlignment.Center, 80, 9, col);
        }
    }

    void Arrow(Vector2 center, Vector2 dir, Color c)
    {
        var perp = new Vector2(-dir.Y, dir.X);
        DrawColoredPolygon(new[] { center + dir * 5, center - dir * 2 + perp * 3, center - dir * 2 - perp * 3 }, c);
    }

    void DrawCompass()
    {
        float pulse = .75f + .25f * Mathf.Sin(G.Time * 4f);
        void One(Vector2 target, Color col, float radius)
        {
            var d = target - G.PPos; float dist = d.Length(); if (dist < 70) return;
            var dir = d.Normalized(); var c = ToScreen(G.PPos) + dir * radius;
            Arrow(c, dir, new Color(col.R, col.G, col.B, pulse));
            Txt($"{(int)(dist / 16)}", c + dir * 10 - new Vector2(6, -3), 9, col, HorizontalAlignment.Center, 12);
        }
        var anc = G.CompassTarget();
        if (anc != null) One(anc.Value, new Color(1f, .88f, .55f), 34);
        if (G.Goals.Count > 0 && G.Goals[0].Target != null && (anc == null || G.Goals[0].Target.Value.DistanceTo(anc.Value) > 8))
            One(G.Goals[0].Target.Value, new Color(.55f, 1f, .9f), 42);
    }

    void Marker(Vector2 wp, Color col, string name, float seed, Vector2 vs)
    {
        var s = ToScreen(wp); float dist = wp.DistanceTo(G.PPos) / 16; float pulse = .65f + .35f * Mathf.Sin(G.Time * 3f + seed);
        bool inside = s.X > 16 && s.X < vs.X - 16 && s.Y > 60 && s.Y < vs.Y - 60;
        if (inside)
        {
            s = s.Round();
            DrawColoredPolygon(new[] { s + new Vector2(0, -4), s + new Vector2(3, 0), s + new Vector2(0, 4), s + new Vector2(-3, 0) }, new Color(col.R, col.G, col.B, pulse));
            Txt($"{name} · {(int)dist}", s + new Vector2(-50, 14), 9, col, HorizontalAlignment.Center, 100);
        }
        else
        {
            var c = new Vector2(Mathf.Clamp(s.X, 16, vs.X - 16), Mathf.Clamp(s.Y, 60, vs.Y - 60)); var dir = (s - vs * .5f).Normalized();
            Arrow(c, dir, new Color(col.R, col.G, col.B, pulse)); Txt($"{(int)dist}", c - dir * 8 - new Vector2(6, -3), 9, col, HorizontalAlignment.Center, 12);
        }
    }

    void DrawMarkers(Vector2 vs) { foreach (var p in G.MarkerPois()) Marker(p.Pos, Game.PoiColor(p.Kind), Game.PoiName(p.Kind), p.Tile.X, vs); }
    void DrawEventMarkers(Vector2 vs) { foreach (var ev in G.Events) Marker(ev.Pos, Game.EvColor(ev.Kind), Game.EvName(ev.Kind), ev.Seed, vs); }

    // ───────────────────────── уведомления ─────────────────────────
    void DrawBossBar(Vector2 vs)
    {
        var b = G.Boss; if (b == null) return;
        float w = 200, x = vs.X * .5f - w * .5f;
        Panel(new Rect2(x - 6, 4, w + 12, 24), "PanelGold");
        Txt(Game.EnemyName(b.Kind), new Vector2(x, 14), 9, new Color("#f0c8ff"), HorizontalAlignment.Center, w);
        Bar(new Rect2(x, 17, w, 7), b.Hp / b.MaxHp, "HpBar");
    }

    float DrawBanners(Vector2 vs)
    {
        float y = G.Boss != null ? 34 : 22;
        if (G.Banners.Count == 0) return y;
        var b = G.Banners[G.Banners.Count - 1];                 // показываем только последнее уведомление
        float a = Mathf.Clamp(b.Life * 1.6f, 0, 1), slide = Mathf.Clamp((3.5f - b.Life) * 8f, 0, 1);
        float w = Mathf.Min(vs.X - 20, Mathf.Max(TextW(b.Title), TextW(b.Sub)) + 20);
        var r = new Rect2(Mathf.Round(vs.X * .5f - w * .5f), Mathf.Round(y - (1 - slide) * 6), w, 24);
        var old = Modulate; Modulate = new Color(1, 1, 1, a);
        Panel(r, "PanelGold");
        Txt(b.Title, new Vector2(r.Position.X, r.Position.Y + 10), 9, new Color(b.C.R, b.C.G, b.C.B), HorizontalAlignment.Center, w);
        Txt(b.Sub, new Vector2(r.Position.X, r.Position.Y + 20), 9, Text, HorizontalAlignment.Center, w);
        Modulate = old;
        return y + 28;
    }

    void DrawToasts(Vector2 vs, float y)
    {
        y += 8; int from = Mathf.Max(0, G.Toasts.Count - 3);
        for (int i = from; i < G.Toasts.Count; i++)
        {
            var t = G.Toasts[i]; float a = Mathf.Clamp(t.Life, 0, 1);
            float w = Mathf.Min(TextW(t.S), vs.X - 30);
            var old = Modulate; Modulate = new Color(1, 1, 1, a);
            Panel(new Rect2(Mathf.Round(vs.X * .5f - w * .5f - 5), y - 8, w + 10, 13), "PanelDark");
            Txt(t.S, new Vector2(vs.X * .5f - w * .5f, y + 1), 9, Text);
            Modulate = old; y += 15;
        }
    }

    /// <summary>Реликвии, лучезарность, испытание и события — в виде компактной полосы под шкалой босса/уведомлениями.</summary>
    void DrawStage4Hud(Vector2 vs)
    {
        float x = 6, y = 92;                                     // под панелями здоровья и цели
        foreach (var id in G.EquippedRelics)
        {
            var d = Db.Relic(id); if (d == null) continue;
            var c = new Vector2(x + 4, y); DrawColoredPolygon(new[] { c + new Vector2(0, -4), c + new Vector2(4, 0), c + new Vector2(0, 4), c + new Vector2(-4, 0) }, d.Tint);
            DrawPolyline(new[] { c + new Vector2(0, -4), c + new Vector2(4, 0), c + new Vector2(0, 4), c + new Vector2(-4, 0), c + new Vector2(0, -4) }, Db.RarCol[d.Rarity], 1f); x += 11;
        }
        foreach (var id in G.TempRelics) { var d = Db.Relic(id); if (d == null) continue; var c = new Vector2(x + 4, y); DrawColoredPolygon(new[] { c + new Vector2(0, -4), c + new Vector2(4, 0), c + new Vector2(0, 4), c + new Vector2(-4, 0) }, new Color(d.Tint.R, d.Tint.G, d.Tint.B, .6f + .4f * Mathf.Sin(G.Time * 8))); x += 11; }
        if (G.ActiveSyn.Count > 0) Txt($"✦{G.ActiveSyn.Count}", new Vector2(x, y + 3), 9, Good);
        if (G.SurgeOn) { var rr = new Rect2(vs.X * .5f - 60, 30, 120, 8); Bar(rr, G.Surge / 8f, "LightBar"); Txt("ЛУЧЕЗАРНОСТЬ", new Vector2(rr.Position.X, rr.Position.Y + 16), 9, Gold, HorizontalAlignment.Center, 120); }
        if (G.TrialActive) { var rr = new Rect2(vs.X * .5f - 100, G.Boss != null ? 34 : 4, 200, 14); Panel(rr, "PanelGold"); Txt($"Испытание Тьмы · {(int)G.TrialLeft} с", new Vector2(rr.Position.X, rr.Position.Y + 10), 9, new Color("#e0b8ff"), HorizontalAlignment.Center, 200); }
        if (G.Events.Count > 0 && G.Window == Win.None)
        {
            float ey = 92; var r = new Rect2(vs.X - 126, ey, 122, 8 + G.Events.Count * 10 + 6); Panel(r, "PanelDark");
            for (int i = 0; i < G.Events.Count; i++) Txt($"◆ {Game.EvName(G.Events[i].Kind)} · {(int)(G.Events[i].Pos.DistanceTo(G.PPos) / 16)}", new Vector2(r.Position.X + 6, r.Position.Y + 12 + i * 10), 9, Game.EvColor(G.Events[i].Kind), HorizontalAlignment.Left, 110);
        }
    }

    /// <summary>Название и значок действия над выбранным объектом — в экранных координатах, пиксельным шрифтом.</summary>
    void DrawSelectionLabel()
    {
        if (G.selA <= .02f || G.Window != Win.None) return;
        var p = ToScreen(new Vector2(G.selRect.GetCenter().X, G.selRect.Position.Y)).Round(); p.Y -= 4 + Mathf.Round(Mathf.Sin(G.UiTime * 5f));
        var a = G.selA; var old = Modulate; Modulate = new Color(1, 1, 1, a);
        string key = G.selHasE ? "E" : G.selHasU ? "U" : "";
        if (key != "")
        {
            theme.GetStylebox("panel", "Keycap").Draw(GetCanvasItem(), new Rect2(p.X - 6, p.Y - 10, 12, 11));
            Txt(key, new Vector2(p.X - 3, p.Y - 2), 9, Gold); p.Y -= 12;
        }
        string t = Loc.T(G.selTitle); float w = TextW(t) + 8;
        theme.GetStylebox("panel", "PanelDark").Draw(GetCanvasItem(), new Rect2(Mathf.Round(p.X - w / 2), p.Y - 10, w, 12));
        Txt(t, new Vector2(Mathf.Round(p.X - w / 2) + 4, p.Y - 1), 9, Text);
        Modulate = old;
    }
}
