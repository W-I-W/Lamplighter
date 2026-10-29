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
    Theme theme; Font font; WindowView wv; MenuView mv;

    static readonly Color Dim = new("#8a96b0"), Good = new("#9be07a"), Warn = new("#ffb36a"), Gold = new("#ffd23f"), Cyan = new("#8ff0ff"), Text = new("#d8e0f0"), Bad = new("#f06a5e");
    readonly List<Rect2> panelRects = new();

    public override void _Ready()
    {
        ProcessPriority = 1000;      // после игры и камеры: подписи над персонажами берут уже обновлённые позиции, иначе отстают на кадр и дрожат при ходьбе
        theme = GD.Load<Theme>(ThemePath); font = theme.DefaultFont ?? ThemeDB.FallbackFont; Theme = theme;
        wv = GD.Load<PackedScene>("res://Scenes/UI/Window.tscn").Instantiate<WindowView>(); wv.G = G; wv.ZIndex = 4050; AddChild(wv);
        mv = GD.Load<PackedScene>("res://Scenes/UI/Menu.tscn").Instantiate<MenuView>(); mv.G = G; mv.ZIndex = 4060; AddChild(mv);
    }

    // ───────────────────────── примитивы ─────────────────────────
    /// <summary>Текст пиксельным шрифтом. size ≥ 14 — двойной размер (целочисленное масштабирование). y — линия текста (верх строки + 7).</summary>
    void Txt(string s, Vector2 p, int size = 9, Color? c = null, HorizontalAlignment al = HorizontalAlignment.Left, float w = -1)
    {
        Lbl(Loc.T(s), p, size >= 14, c ?? Text, al, w);
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

    /// <summary>Панель только для показа (уведомления, полоса босса, список событий): клик сквозь неё идёт в мир. blocks — панель перехватывает мышь.
    /// Раньше любая такая панель считалась интерфейсом, и атака левой кнопкой «пропадала», если курсор был под уведомлением (в сети их особенно много).</summary>
    void Panel(Rect2 r, string variation = "PanelContainer", bool blocks = false)
    {
        r = new Rect2(r.Position.Round(), r.Size.Round()); if (blocks) panelRects.Add(r);
        Sty(r, variation);
    }

    void Bar(Rect2 r, float f, string variation)
    {
        var n = Take(bars, ref nBar);
        if (n.ThemeTypeVariation != variation) n.ThemeTypeVariation = variation;
        n.ShowPercentage = false; n.MinValue = 0; n.MaxValue = 1; n.Value = Mathf.Clamp(f, 0, 1);
        n.Position = r.Position.Round(); n.Size = r.Size.Round(); n.Modulate = curMod;
    }

    public int RowHit(Vector2 p) => wv.RowHit(p);
    public int TabHit(Vector2 p) => wv.TabHit(p);

    Vector2 ToScreen(Vector2 w) => ((w - G.CamCenter) * Game.Zoom + G.ViewSize * .5f) / G.UiScaleVp;

    /// <summary>Находится ли точка (логические координаты) над панелью интерфейса.</summary>
    public bool IsOverUi(Vector2 logical) { if (wv != null && wv.IsOver(logical) || mv != null && mv.IsOver(logical)) return true; foreach (var r in panelRects) if (r.HasPoint(logical)) return true; return G.GameHudNode != null && G.GameHudNode.IsOver(logical, G.UiScaleVp); }

    public override void _Process(double delta)
    {
        if (G == null || theme == null) return;
        BeginFrame(); Compose(); EndFrame();
    }

    void Compose()
    {
        panelRects.Clear();
        var vs = Size; if (vs.X < 100 || vs.Y < 60) return;
        if (G.Menu == Mn.Main || G.Menu == Mn.Flow) { DrawMenu(vs); FinishViews(); return; }
        if (G.LightBlockT > 0) Rect(new Rect2(Vector2.Zero, vs), new Color(0, 0, .05f, .25f));
        if (G.VictoryT > 0) { float vk = Mathf.Clamp(G.VictoryT / 3f, 0, 1); Rect(new Rect2(Vector2.Zero, vs), new Color(1f, .9f, .5f, .22f * vk * vk)); }
        DrawMarkers(vs); DrawEventMarkers(vs); DrawStatusLabels(); DrawNetLabels(vs); DrawBossBar(vs); DrawFloaters(); DrawCompass(); DrawTooltip(vs);
        LayoutTop(vs);
        float y = DrawBanners(vs); DrawToasts(vs, y);
        if (G.LightBlockT > 0) Txt($"СВЕТ ПОГАШЕН · {G.LightBlockT:F0} с", new Vector2(0, vs.Y * .5f - 40), 14, new Color("#7ac8ff"), HorizontalAlignment.Center, vs.X);
        DrawStage4Hud(vs);
        DrawEndgameHud(vs);
        if (G.Menu != Mn.None) DrawMenu(vs);
        else if (G.FinStage >= 1) DrawFinale(vs);
        else if (G.MapOpen) DrawMap(vs);
        else DrawWindows(vs);
        FinishViews();
        if (G.Hp / G.MaxHp < .3f) Rect(new Rect2(Vector2.Zero, vs), new Color(.6f, 0, 0, .12f + .06f * Mathf.Sin(G.Time * 6f)));
    }

    // ───────────────────────── мир: маркеры, компас, числа ─────────────────────────
    void DrawFloaters()
    {
        foreach (var t in G.Texts)
        {
            var p = ToScreen(t.P); var col = t.C; col.A = Mathf.Clamp(t.Life * 2, 0, 1);
            var ts = Loc.T(t.S); p = new Vector2(Mathf.Round(p.X), Mathf.Round(p.Y));
            Lbl(ts, p - new Vector2(40, 0), false, col, HorizontalAlignment.Center, 80);
        }
    }

    void Arrow(Vector2 center, Vector2 dir, Color c)
    {
        ArrowMark(center, dir, c);
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
            Diamond(s, 3.5f, new Color(col.R, col.G, col.B, pulse));
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
    // верхняя колонка: босс, разлом, испытание, лучезарность, затем баннеры и тосты — друг под другом, на одной оси
    protected float topCx, topMaxW, topRiftY, topTrialY, topSurgeY, topBannerY;
    bool BossShown => G.Boss != null && G.Boss.Pos.DistanceTo(G.PPos) <= Tune.T.BossLeashTiles * 16f;

    void LayoutTop(Vector2 vs)
    {
        float y = 4, sl = 0, sr = vs.X;
        if (G.GameHudNode != null) (sl, sr) = G.GameHudNode.FreeSpan(0, 90, vs.X, G.UiScaleVp);
        topCx = (sl + sr) * .5f; topMaxW = Mathf.Max(80, sr - sl - 12);
        if (BossShown) y += 28;
        if (G.RiftOn) { topRiftY = y; y += 28; }
        if (G.TrialActive) { topTrialY = y; y += 18; }
        if (G.SurgeOn) { topSurgeY = y; y += 24; }
        topBannerY = y;
    }

    void DrawBossBar(Vector2 vs)
    {
        var b = G.Boss; if (b == null || !BossShown) return;      // полоса босса только пока он рядом (в сети она оставалась после смерти игрока)
        float w = 200, x = topCx - w * .5f;
        Panel(new Rect2(x - 6, 4, w + 12, 24), "PanelGold");
        Txt(Game.EnemyName(b.Kind), new Vector2(x, 14), 9, new Color("#f0c8ff"), HorizontalAlignment.Center, w);
        Bar(new Rect2(x, 17, w, 7), b.Hp / b.MaxHp, "HpBar");
    }

    float DrawBanners(Vector2 vs)
    {
        float y = topBannerY;
        if (G.Banners.Count == 0) return y;
        var b = G.Banners[0];                                   // уведомления идут по очереди, ни одно не теряется
        float a = Mathf.Clamp(b.Life * 1.6f, 0, 1) * Mathf.Clamp(b.Age * 6f, 0, 1), slide = Mathf.Clamp(b.Age * 8f, 0, 1);
        float w = Mathf.Min(topMaxW, Mathf.Max(TextW(b.Title), TextW(b.Sub)) + 20);
        var r = new Rect2(Mathf.Round(topCx - w * .5f), Mathf.Round(y - (1 - slide) * 6), w, 24);
        var old = curMod; curMod = new Color(1, 1, 1, a);
        Panel(r, "PanelGold");
        Txt(b.Title, new Vector2(r.Position.X, r.Position.Y + 10), 9, new Color(b.C.R, b.C.G, b.C.B), HorizontalAlignment.Center, w);
        { string sub = Loc.T(b.Sub); RichLine(sub, new Vector2(r.Position.X, r.Position.Y + 20), w, Text, TextFx.Typed(sub, b.Age, 70f), true); }
        curMod = old;
        return y + 28;
    }

    void DrawToasts(Vector2 vs, float y)
    {
        y += 8; int from = Mathf.Max(0, G.Toasts.Count - 3);
        float cx = topCx, maxW = topMaxW - 12;   // та же ось, что у баннеров: между боковыми панелями
        for (int i = from; i < G.Toasts.Count; i++)
        {
            var t = G.Toasts[i]; float a = Mathf.Clamp(t.Life, 0, 1);
            var lines = new List<string>(); string line = "";
            foreach (var word in Loc.T(t.S).Split(' '))
            {
                string n = line == "" ? word : line + " " + word;
                if (TextW(n) > maxW && line != "") { lines.Add(line); line = word; } else line = n;
            }
            if (line != "") lines.Add(line);
            float w = 0; foreach (var l in lines) w = Mathf.Max(w, TextW(l));
            var old = curMod; curMod = new Color(1, 1, 1, a);
            Panel(new Rect2(Mathf.Round(cx - w * .5f - 5), y - 8, w + 10, 4 + lines.Count * 10), "PanelDark");
            int typed = (int)(t.Age * 90f), off = 0;
            for (int k = 0; k < lines.Count; k++)
            {
                int vis = Mathf.Clamp(typed - off, 0, lines[k].Length); off += lines[k].Length + 1;
                RichLine(lines[k], new Vector2(cx - w * .5f, y + 1 + k * 10), w + 4, Text, vis >= lines[k].Length ? -1 : vis);
            }
            curMod = old; y += 6 + lines.Count * 10;
        }
    }

    /// <summary>Реликвии, лучезарность, испытание и события — в виде компактной полосы под шкалой босса/уведомлениями.</summary>
    void DrawStage4Hud(Vector2 vs)
    {
        float x = 6, y = 92;                                     // под панелями здоровья и цели
        foreach (var id in G.EquippedRelics)
        {
            var d = Db.Relic(id); if (d == null) continue;
            var c = new Vector2(x + 4, y); Diamond(c, 4f, d.Tint); Diamond(c, 4f, Db.RarCol[d.Rarity], true); x += 11;
        }
        foreach (var id in G.TempRelics) { var d = Db.Relic(id); if (d == null) continue; var c = new Vector2(x + 4, y); Diamond(c, 4f, new Color(d.Tint.R, d.Tint.G, d.Tint.B, .6f + .4f * Mathf.Sin(G.Time * 8))); x += 11; }
        if (G.ActiveSyn.Count > 0) Txt($"✦{G.ActiveSyn.Count}", new Vector2(x, y + 3), 9, Good);
        if (G.SurgeOn) { var rr = new Rect2(topCx - 60, topSurgeY, 120, 8); Bar(rr, G.Surge / 8f, "LightBar"); Txt("ЛУЧЕЗАРНОСТЬ", new Vector2(rr.Position.X, rr.Position.Y + 16), 9, Gold, HorizontalAlignment.Center, 120); }
        if (G.TrialActive) { var rr = new Rect2(topCx - 100, topTrialY, 200, 14); Panel(rr, "PanelGold"); Txt($"Испытание Тьмы · {(int)G.TrialLeft} с", new Vector2(rr.Position.X, rr.Position.Y + 10), 9, new Color("#e0b8ff"), HorizontalAlignment.Center, 200); }
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
        var a = G.selA; var old = curMod; curMod = new Color(1, 1, 1, a);
        string key = G.selHasE ? "E" : G.selHasU ? "U" : "";
        if (key != "")
        {
            Txt("[" + key + "]", new Vector2(p.X - TextW("[" + key + "]") / 2, p.Y - 2), 9, Gold); p.Y -= 12;
        }
        string t = Loc.T(G.selTitle); float w = TextW(t) + 8;
        Sty(new Rect2(Mathf.Round(p.X - w / 2), p.Y - 10, w, 12), "PanelDark");
        Txt(t, new Vector2(Mathf.Round(p.X - w / 2) + 4, p.Y - 1), 9, Text);
        curMod = old;
    }

    /// <summary>Подпись состояния (нет ресурсов, склад полон…) над постройкой рядом с игроком.</summary>
    /// <summary>Сеть: имена и здоровье союзников над персонажами и компактный индикатор соединения (точка и число игроков).</summary>
    void DrawNetLabels(Vector2 vs)
    {
        if (!NetService.Active) return;
        foreach (var p in NetService.Players.Values)
        {
            if (!p.Handshaken || p.PeerId == NetService.LocalId) continue;
            // ник и здоровье союзника видны, только когда он рядом (до 5 клеток) и на него наведён курсор
            if (p.Shown.DistanceTo(G.PPos) > 80f || G.Aim.DistanceTo(p.Shown + new Vector2(0, -4)) > 14f) continue;
            var sp = ToScreen(p.Shown + new Vector2(0, -17));
            Txt(p.Name, sp + new Vector2(-45, -9), 9, new Color("#c9f0b0"), HorizontalAlignment.Center, 90);
            float w = 16f, k = Mathf.Clamp(p.Hp / Mathf.Max(1f, p.MaxHp), 0, 1);
            Rect(new Rect2(Mathf.Round(sp.X - w / 2), Mathf.Round(sp.Y + 1), w, 2), new Color(0, 0, 0, .7f));
            Rect(new Rect2(Mathf.Round(sp.X - w / 2), Mathf.Round(sp.Y + 1), Mathf.Round(w * k), 2), k > .35f ? new Color("#9be07a") : new Color("#f06a5e"));
        }
        var col = NetService.State == NetState.Online ? new Color("#9be07a") : NetService.State == NetState.Failed ? new Color("#f06a5e") : new Color("#8ff0ff");
        Txt($"● {NetService.Count}/{NetConst.MaxPlayers}", new Vector2(vs.X - 56, vs.Y - 14), 9, col, HorizontalAlignment.Right, 48);
    }

    void DrawStatusLabels()
    {
        foreach (var b in G.Buildings)
        {
            if (!Game.IsProducer(b.Type) || b.Status == 0 || b.Pos.DistanceTo(G.PPos) >= 70) continue;
            var col = b.Status == 4 ? new Color(.7f, .72f, .8f) : b.Status == 2 ? new Color("#ffb04a") : new Color("#ff6a5a");
            var p = ToScreen(b.Pos + new Vector2(0, -15 - 1.5f * Mathf.Sin(G.Time * 5f)));
            Txt(Game.StatusText(b.Status), p + new Vector2(-30, -4), 9, col, HorizontalAlignment.Center, 60);
        }
    }

    /// <summary>Скрывает вид, который в этом кадре не использовался, и закрывает кадр у использованного.</summary>
    void FinishViews()
    {
        if (wv.Started) { wv.End(); wv.Started = false; } else if (wv.Visible) wv.Hide2();
        if (mv.Started) { mv.End(); mv.Started = false; } else if (mv.Visible) mv.Visible = false;
    }
}
