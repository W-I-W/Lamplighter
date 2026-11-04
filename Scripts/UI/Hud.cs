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
        DrawDrops(vs); DrawMarkers(vs); DrawStatusLabels(); DrawNetLabels(vs); DrawBossBar(vs); DrawFloaters(); DrawCompass(); DrawTooltip(vs);
        LayoutTop(vs);
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

    // ───────────────────────── уведомления ─────────────────────────
    // верхняя колонка: босс, разлом, испытание, лучезарность, затем баннеры и тосты — друг под другом, на одной оси
    protected float topCx, topMaxW, topRiftY, topTrialY, topSurgeY, topBannerY;
    Enemy FightBoss => G.BossInFight();                         // босс, с которым сражается кто-то из игроков: шкала видна всем, даже далеко от боя
    bool BossShown => FightBoss != null;

    void LayoutTop(Vector2 vs)
    {
        float y = 4, sl = 0, sr = vs.X;
        if (G.GameHudNode != null) (sl, sr) = G.GameHudNode.FreeSpan(0, 90, vs.X, G.UiScaleVp);
        topCx = (sl + sr) * .5f; topMaxW = Mathf.Max(80, sr - sl - 12);
        if (BossShown) y += PhaseCaptionShown ? 40 : 30;
        if (G.RiftOn) { topRiftY = y; y += 28; }
        if (G.TrialActive) { topTrialY = y; y += 18; }
        if (G.SurgeOn) { topSurgeY = y; y += 24; }
        topBannerY = y;
    }

    /// <summary>Подпись смены фазы под шкалой видна столько секунд.</summary>
    public const float PhaseCaption = 2.6f;
    bool PhaseCaptionShown { get { var b = FightBoss; var pc = Game.PhaseOf(b); return pc != null && pc.Index > 0 && G.Time - pc.ChangedAt < PhaseCaption; } }

    /// <summary>Общая шкала босса: название, «Фаза N / M», разделители на реальных порогах фаз (данные босса, BossPhaseBook), вспышка и подпись «Фаза II — Поглощение» при смене фазы.</summary>
    void DrawBossBar(Vector2 vs)
    {
        var b = FightBoss; if (b == null) return;      // полоса босса только пока он рядом (в сети она оставалась после смерти игрока)
        var pc = Game.PhaseOf(b); float w = 200, x = Mathf.Round(vs.X * .5f - w * .5f);      // по центру экрана
        float since = G.Time - pc.ChangedAt; float flash = pc.Index > 0 && since < .6f ? 1f - since / .6f : 0f;
        Panel(new Rect2(x - 6, 4, w + 12, 26), "PanelGold");
        Txt(Game.EnemyName(b.Kind), new Vector2(x, 14), 9, new Color("#f0c8ff"), HorizontalAlignment.Left, w);
        if (pc.Count > 1) Txt($"Фаза {pc.Number} / {pc.Count}", new Vector2(x, 14), 9, pc.Current.Tint.Lerp(Colors.White, .35f + .65f * flash), HorizontalAlignment.Right, w);
        var br = new Rect2(x, 18, w, 7);
        Bar(br, b.Hp / b.MaxHp, "HpBar");
        for (int i = 1; i < pc.Count; i++)                                                  // разделители на настоящих порогах, а не на равных частях
        {
            float px = Mathf.Round(x + w * pc.Phases[i].Threshold); bool passed = i <= pc.Index;
            Rect(new Rect2(px, br.Position.Y - 1, 1, br.Size.Y + 2), passed ? new Color(1f, 1f, 1f, .4f) : new Color(1f, .95f, .8f, .95f));
            Rect(new Rect2(px + 1, br.Position.Y - 1, 1, br.Size.Y + 2), new Color(0, 0, 0, .5f));
        }
        if (b.Shield) Rect(new Rect2(x, br.Position.Y, w, 2), new Color(.75f, .8f, 1f, .5f + .2f * Mathf.Sin(G.Time * 5f)));      // защита: урон по боссу слабый
        if (b.Over > 0) Rect(new Rect2(x, br.Position.Y + br.Size.Y - 2, w, 2), new Color(1f, .9f, .4f, .6f + .3f * Mathf.Sin(G.Time * 12f)));   // ядро/босс открыт
        if (flash > 0) { Rect(new Rect2(x - 6, 4, w + 12, 26), new Color(pc.Current.Tint.R, pc.Current.Tint.G, pc.Current.Tint.B, .45f * flash)); Rect(br, new Color(1, 1, 1, .8f * flash)); }
        if (PhaseCaptionShown)
        {
            float a = Mathf.Clamp((PhaseCaption - since) * 2f, 0, 1) * Mathf.Clamp(since * 6f, 0, 1);
            string cap = $"Фаза {BossPhaseBook.Roman(pc.Number)} — {pc.Current.Name}";
            var c = pc.Current.Tint.Lerp(Colors.White, .3f); c.A = a;
            Txt(cap, new Vector2(x, 40), 9, c, HorizontalAlignment.Center, w);
        }
    }

    /// <summary>Реликвии, лучезарность, испытание и события — в виде компактной полосы под шкалой босса/уведомлениями.</summary>
    void DrawStage4Hud(Vector2 vs)
    {
        float x = 6, y = (G.GameHudNode?.LeftBottom ?? 88) + 7;     // под панелью игрока и целью
        foreach (var id in HudDensity.Minimal ? new List<string>() : G.EquippedRelics)
        {
            var d = Db.Relic(id); if (d == null) continue;
            var c = new Vector2(x + 4, y); Diamond(c, 4f, d.Tint); Diamond(c, 4f, Db.RarCol[d.Rarity], true); x += 11;
        }
        foreach (var id in HudDensity.Minimal ? new List<string>() : G.TempRelics) { var d = Db.Relic(id); if (d == null) continue; var c = new Vector2(x + 4, y); Diamond(c, 4f, new Color(d.Tint.R, d.Tint.G, d.Tint.B, .6f + .4f * Mathf.Sin(G.Time * 8))); x += 11; }
        if (G.ActiveSyn.Count > 0 && !HudDensity.Minimal) Txt($"✦{G.ActiveSyn.Count}", new Vector2(x, y + 3), 9, Good);
        if (G.SurgeOn) { var rr = new Rect2(topCx - 60, topSurgeY, 120, 8); Bar(rr, G.Surge / 8f, "LightBar"); Txt("ЛУЧЕЗАРНОСТЬ", new Vector2(rr.Position.X, rr.Position.Y + 16), 9, Gold, HorizontalAlignment.Center, 120); }
        if (G.TrialActive) { var rr = new Rect2(topCx - 100, topTrialY, 200, 14); Panel(rr, "PanelGold"); Txt($"Испытание Тьмы · {(int)G.TrialLeft} с", new Vector2(rr.Position.X, rr.Position.Y + 10), 9, new Color("#e0b8ff"), HorizontalAlignment.Center, 200); }
        DrawEventTracker(vs);
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
