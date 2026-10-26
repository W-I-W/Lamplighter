using Godot;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Компактный HUD, рисуется одним Control.</summary>
public partial class Hud : Control
{
    public Game G;
    Font font = ThemeDB.FallbackFont;

    static readonly Color Panel = new(0.05f, 0.05f, 0.09f, 0.80f);
    static readonly Color Edge = new(1, 1, 1, 0.14f);
    static readonly Color Dim = new(0.75f, 0.78f, 0.85f, 0.85f);
    static readonly Color Good = new("#9dff8a");
    static readonly Color Warn = new("#ffb36a");
    static readonly Color Gold = new("#ffe27a");

    void Txt(string s, Vector2 p, int size = 14, Color? c = null, HorizontalAlignment al = HorizontalAlignment.Left, float w = -1)
    {
        DrawString(font, p + new Vector2(1, 1), s, al, w, size, new Color(0, 0, 0, .7f));
        DrawString(font, p, s, al, w, size, c ?? Colors.White);
    }

    void Box(Rect2 r, Color? border = null) { DrawRect(r, Panel); DrawRect(r, border ?? Edge, false, 1); }

    void Bar(Rect2 r, float f, Color c)
    {
        DrawRect(r, new Color(0, 0, 0, .6f));
        DrawRect(new Rect2(r.Position, new Vector2(r.Size.X * Mathf.Clamp(f, 0, 1), r.Size.Y)), c);
    }

    Vector2 ToScreen(Vector2 w) => (w - G.CamCenter) * Game.Zoom + G.ViewSize * .5f;
    float TextW(string s, int size) => font.GetStringSize(s, HorizontalAlignment.Left, -1, size).X;

    public override void _Draw()
    {
        if (G == null) return;
        Size = G.ViewSize; Position = Vector2.Zero;
        var vs = G.ViewSize;

        if (G.LightBlockT > 0) DrawRect(new Rect2(Vector2.Zero, vs), new Color(0, 0, .05f, .25f));
        DrawMarkers(vs);
        DrawEventMarkers(vs);
        DrawStatus();
        DrawGoals();
        DrawTopRight(vs);
        DrawBossBar(vs);
        DrawInventory(vs);
        DrawAbilities(vs);
        DrawBuildPanel(vs);
        DrawFloaters();
        DrawCompass();
        DrawPrompt(vs);
        float y = DrawBanners(vs);
        DrawToasts(vs, y);
        if (G.LightBlockT > 0) Txt($"СВЕТ ПОГАШЕН · {G.LightBlockT:F0} с", new Vector2(0, vs.Y * .5f - 70), 20, new Color("#7ac8ff"), HorizontalAlignment.Center, vs.X);
        DrawStage4Hud(vs);
        if (G.TechOpen) DrawTech(vs);
        if (G.MapOpen) DrawMap(vs);
        DrawWindows(vs);
        if (G.Hp / G.MaxHp < .3f) DrawRect(new Rect2(Vector2.Zero, vs), new Color(.6f, 0, 0, .12f + .06f * Mathf.Sin(G.Time * 6f)));
    }

    // ───────────────────────── блоки ─────────────────────────
    void DrawStatus()
    {
        Box(new Rect2(10, 10, 232, 58));
        Txt($"Ур. {G.Level}", new Vector2(18, 28), 15, Gold);
        Txt(G.CurDef != null ? G.CurDef.Name : Game.PickNames[G.Tier], new Vector2(18, 28), 12, G.CurDef != null ? Db.RarCol[(int)G.CurDef.Rar] : Dim, HorizontalAlignment.Right, 216);
        Bar(new Rect2(18, 34, 216, 12), G.Hp / G.MaxHp, G.Hp / G.MaxHp < .3f ? new Color("#e03c3c") : new Color("#c8423f"));
        Txt($"{(int)G.Hp}/{(int)G.MaxHp}", new Vector2(18, 45), 11, Colors.White, HorizontalAlignment.Center, 216);
        Bar(new Rect2(18, 53, 216, 6), G.Xp / G.XpNeed, new Color("#7ad45a"));
    }

    void DrawGoals()
    {
        var goals = G.Goals;
        int n = Mathf.Min(3, goals.Count);
        float h = 24 + Mathf.Max(1, n) * 38;
        Box(new Rect2(10, 76, 300, h));
        Txt("Ближайшие цели", new Vector2(18, 92), 11, Dim);
        if (n == 0) { Txt("Все цели выполнены! Развивайте деревню.", new Vector2(18, 116), 13, Good); return; }
        for (int i = 0; i < n; i++)
        {
            float y = 112 + i * 38;
            Txt((i == 0 ? "▶ " : "• ") + goals[i].Title, new Vector2(18, y), i == 0 ? 14 : 13, i == 0 ? Gold : Colors.White);
            Txt(goals[i].Needs, new Vector2(30, y + 16), 11, i == 0 ? new Color("#d8f0ff") : Dim, HorizontalAlignment.Left, 276);
        }
    }

    void DrawTopRight(Vector2 vs)
    {
        int reg = G.RegionAt(G.PPos);
        Box(new Rect2(vs.X - 220, 10, 210, 44));
        string line = reg == 0 ? $"Фонари: {G.LitCount}/5" : $"Фонари: {G.LitIn(reg)}/3";
        Txt(Game.RegionNames[reg], new Vector2(vs.X - 212, 27), 12, reg == 1 ? new Color("#7ac8ff") : reg == 2 ? new Color("#ff9a5a") : new Color("#7ad45a"));
        Txt(line, new Vector2(vs.X - 212, 46), 13, new Color("#ffe9a0"));
        Txt($"Маяк {G.BeaconLevel} · [M]", new Vector2(vs.X - 212, 46), 11, Dim, HorizontalAlignment.Right, 194);

        if (G.Buildings.Count == 0 && G.Villagers.Count == 0) return;
        float y = 60;
        Box(new Rect2(vs.X - 220, y, 210, 62));
        Txt($"Деревня · жителей: {G.Villagers.Count}", new Vector2(vs.X - 212, y + 16), 12, new Color("#d9b27c"));
        float x = vs.X - 212; int shown = 0;
        for (int i = 0; i < It.Count && shown < 6; i++)
        {
            if (G.Stock[i] <= 0) continue;
            DrawTextureRectRegion(Art.IconTex, new Rect2(x, y + 22, 16, 16), new Rect2(i * 16, 0, 16, 16));
            Txt(G.Stock[i].ToString(), new Vector2(x + 17, y + 35), 11);
            x += 33; shown++;
        }
        if (shown == 0) Txt("склад пуст — рабочие трудятся", new Vector2(vs.X - 212, y + 36), 11, Dim);
        Txt($"склад {G.StockTotal}/{G.StockCap} · E забрать · ×{G.ProdMul:F2}", new Vector2(vs.X - 212, y + 56), 9, new Color(1, 1, 1, .5f));
    }

    void DrawBossBar(Vector2 vs)
    {
        var b = G.Boss; if (b == null) return;
        float w = 420, x = vs.X * .5f - w * .5f;
        Box(new Rect2(x - 8, 8, w + 16, 40), new Color("#d58cff"));
        Txt(Game.EnemyName(b.Kind), new Vector2(x, 26), 14, new Color("#f0c8ff"), HorizontalAlignment.Center, w);
        Bar(new Rect2(x, 32, w, 10), b.Hp / b.MaxHp, b.Kind == EK.DepthWarden ? new Color("#3d9be0") : b.Kind == EK.AshWarden ? new Color("#e0662a") : new Color("#b04ae0"));
    }

    void DrawInventory(Vector2 vs)
    {
        float ix = 10, iy = vs.Y - 52, sw = 40;
        Box(new Rect2(ix, iy, It.Count * sw + 8, 44));
        for (int i = 0; i < It.Count; i++)
        {
            var p = new Vector2(ix + 5 + i * sw, iy + 4);
            DrawTextureRectRegion(Art.IconTex, new Rect2(p, new Vector2(22, 22)), new Rect2(i * 16, 0, 16, 16), G.Inv[i] > 0 ? Colors.White : new Color(1, 1, 1, .25f));
            Txt(G.Inv[i].ToString(), p + new Vector2(19, 26), 10, G.Inv[i] > 0 ? Colors.White : Dim);
        }
    }

    void DrawAbilities(Vector2 vs)
    {
        var list = new List<(string key, string name, float cd, float max, bool own)>
        {
            ("Shift", "Рывок", G.DashCd, 1.6f, G.Has(Tech.Dash)),
            ("ПКМ", "Круг", G.SpinCd, 3f, G.Has(Tech.Spin)),
            ("Q", "Ракета", G.FlareCd, 8f, G.Has(Tech.Flare)),
            ("F", "Импульс", G.PulseCd, 12f, G.Has(Tech.Pulse)),
        };
        float x = 10, y = vs.Y - 96;
        foreach (var a in list)
        {
            if (!a.own) continue;
            var r = new Rect2(x, y, 58, 36); Box(r, a.cd <= 0 ? Good : Edge);
            if (a.cd > 0) DrawRect(new Rect2(r.Position, new Vector2(58 * (a.cd / a.max), 36)), new Color(0, 0, 0, .55f));
            Txt(a.key, new Vector2(x, y + 14), 11, Gold, HorizontalAlignment.Center, 58);
            Txt(a.name, new Vector2(x, y + 29), 11, Colors.White, HorizontalAlignment.Center, 58);
            x += 64;
        }
        if (G.Has(Tech.Beam))
        {
            var r = new Rect2(x, y, 92, 36); Box(r, G.BeamOn ? new Color("#ffffff") : Edge);
            Bar(new Rect2(x + 4, y + 24, 84, 8), G.BeamE / 100f, G.beamLockPublic ? new Color("#7a7f8c") : new Color("#bfe9ff"));
            Txt("R · Прожектор", new Vector2(x, y + 15), 11, Gold, HorizontalAlignment.Center, 92);
        }
    }

    void DrawBuildPanel(Vector2 vs)
    {
        float w = 350, h = 270, cx = vs.X - w - 10, cy = vs.Y - h - 10;
        Box(new Rect2(cx, cy, w, h));
        Txt("Строить   [B] все · [C] экип. · [J] журнал · [P] произв.", new Vector2(cx + 8, cy + 16), 11, Dim);
        float y = cy + 38;
        void Line(string key, string name, string right, int state)
        {
            var col = state == 2 ? Good : state == 1 ? Warn : state == 3 ? new Color("#8ff0ff") : new Color(.55f, .57f, .63f);
            Txt($"[{key}] {name}", new Vector2(cx + 8, y), 13, col);
            Txt(right, new Vector2(cx + 8, y), 11, state == 0 ? new Color(.6f, .6f, .66f) : col, HorizontalAlignment.Right, w - 16);
            y += 19;
        }
        Line("1", "Печь", Game.CostStr(Game.FurnaceCost), G.Can(Game.FurnaceCost) ? 2 : 1);
        Line("2", "Фонарь", Game.CostStr(Game.LanternCost), G.Can(Game.LanternCost) ? 2 : 1);
        var pc = G.NextPickCost;
        string pn = G.Tier < 4 ? Game.PickNames[G.Tier + 1] : "";
        if (pc == null) Line("3", "Кирка", "максимум", 3);
        else if (!G.HasFurnace) Line("3", pn, "нужна печь", 0);
        else Line("3", pn, Game.CostStr(pc), G.Can(pc) ? 2 : 1);
        var types = new[] { Tl.Warehouse, Tl.Sawmill, Tl.Mine, Tl.Forge, Tl.Drill, Tl.Collector };
        string[] keys = { "4", "5", "6", "7", "Z", "X" };
        for (int bk = 0; bk < 6; bk++)
        {
            string nm = Game.BNames[bk];
            if (G.HasBuilding(types[bk])) Line(keys[bk], $"{nm} ур.{LevelOf(types[bk])}", "построено · [U] улучшить", 3);
            else if (!G.BUnlocked(bk)) Line(keys[bk], nm, "нужно: " + Game.BLock(bk), 0);
            else Line(keys[bk], nm, Game.CostStr(Game.BCost(bk, 1)), G.Can(Game.BCost(bk, 1)) ? 2 : 1);
        }
        if (G.Has(Tech.CombatLamp)) Line("8", "Боевой фонарь", Game.CostStr(Game.LampCostOf(1)), G.Can(Game.LampCostOf(1)) ? 2 : 1); else Line("8", "Боевой фонарь", "нужно: улучшение [T]", 0);
        if (G.Has(Tech.Optics))
        {
            Line("9", "Кристальный фонарь", Game.CostStr(Game.LampCostOf(2)), G.Can(Game.LampCostOf(2)) ? 2 : 1);
            Line("0", "Зеркало", Game.CostStr(Game.MirrorCostArr), G.Can(Game.MirrorCostArr) ? 2 : 1);
        }
        else { Line("9", "Кристальный фонарь", "нужно: оптика [T]", 0); Line("0", "Зеркало", "нужно: оптика [T]", 0); }
    }

    int LevelOf(Tl t) { foreach (var b in G.Buildings) if (b.Type == t) return b.Level; return 1; }

    void DrawFloaters()
    {
        foreach (var t in G.Texts)
        {
            var p = ToScreen(t.P); var col = t.C; col.A = Mathf.Clamp(t.Life * 2, 0, 1);
            DrawString(font, p - new Vector2(59, -1), t.S, HorizontalAlignment.Center, 120, 13, new Color(0, 0, 0, col.A * .8f));
            DrawString(font, p - new Vector2(60, 0), t.S, HorizontalAlignment.Center, 120, 13, col);
        }
    }

    void Arrow(Vector2 center, Vector2 dir, Color c)
    {
        var perp = new Vector2(-dir.Y, dir.X);
        DrawColoredPolygon(new[] { center + dir * 9, center - dir * 4 + perp * 6, center - dir * 4 - perp * 6 }, c);
    }

    void DrawCompass()
    {
        float pulse = .75f + .25f * Mathf.Sin(G.Time * 4f);
        void One(Vector2 target, Color col, float radius)
        {
            var d = target - G.PPos; float dist = d.Length(); if (dist < 70) return;
            var dir = d.Normalized(); var c = ToScreen(G.PPos) + dir * radius;
            Arrow(c, dir, new Color(col.R, col.G, col.B, pulse));
            Txt($"{(int)(dist / 16)}", c + dir * 20 - new Vector2(10, -4), 11, col, HorizontalAlignment.Center, 20);
        }
        var anc = G.CompassTarget();
        if (anc != null) One(anc.Value, new Color(1f, .88f, .55f), 62);
        if (G.Goals.Count > 0 && G.Goals[0].Target != null && (anc == null || G.Goals[0].Target.Value.DistanceTo(anc.Value) > 8))
            One(G.Goals[0].Target.Value, new Color(.55f, 1f, .9f), 78);
    }

    void DrawMarkers(Vector2 vs)
    {
        foreach (var p in G.MarkerPois())
        {
            var s = ToScreen(p.Pos); var col = Game.PoiColor(p.Kind);
            bool inside = s.X > 30 && s.X < vs.X - 30 && s.Y > 110 && s.Y < vs.Y - 110;
            float dist = p.Pos.DistanceTo(G.PPos) / 16;
            float pulse = .65f + .35f * Mathf.Sin(G.Time * 3f + p.Tile.X);
            if (inside)
            {
                DrawColoredPolygon(new[] { s + new Vector2(0, -8), s + new Vector2(6, 0), s + new Vector2(0, 8), s + new Vector2(-6, 0) }, new Color(col.R, col.G, col.B, pulse));
                Txt($"{Game.PoiName(p.Kind)} · {(int)dist}", s + new Vector2(-70, 24), 11, col, HorizontalAlignment.Center, 140);
            }
            else
            {
                var c = new Vector2(Mathf.Clamp(s.X, 30, vs.X - 30), Mathf.Clamp(s.Y, 110, vs.Y - 110));
                var dir = (s - vs * .5f).Normalized();
                Arrow(c, dir, new Color(col.R, col.G, col.B, pulse));
                Txt($"{(int)dist}", c - dir * 14 - new Vector2(10, -4), 10, col, HorizontalAlignment.Center, 20);
            }
        }
    }

    void DrawPrompt(Vector2 vs)
    {
        var lines = G.Cur.Lines; if (lines.Count == 0) return;
        float w = 0; foreach (var l in lines) w = Mathf.Max(w, TextW(l, 14));
        float h = lines.Count * 20 + 12;
        float x = vs.X * .5f - w * .5f - 12, y = vs.Y - 112 - h;
        Box(new Rect2(x, y, w + 24, h));
        for (int i = 0; i < lines.Count; i++)
            Txt(lines[i], new Vector2(x + 12, y + 20 + i * 20), 14, i == 0 || lines[i].StartsWith("[") ? Gold : Dim);
    }

    float DrawBanners(Vector2 vs)
    {
        float y = G.Boss != null ? 64 : 56;
        foreach (var b in G.Banners)
        {
            float a = Mathf.Clamp(b.Life * 1.5f, 0, 1), slide = Mathf.Clamp((5f - b.Life) * 6f, 0, 1);
            float w = Mathf.Max(TextW(b.Title, 20), TextW(b.Sub, 13)) + 36;
            var r = new Rect2(vs.X * .5f - w * .5f, y - (1 - slide) * 12, w, 50);
            DrawRect(r, new Color(.04f, .04f, .08f, .88f * a));
            DrawRect(r, new Color(b.C.R, b.C.G, b.C.B, .8f * a), false, 2);
            DrawRect(new Rect2(r.Position, new Vector2(4, 50)), new Color(b.C.R, b.C.G, b.C.B, a));
            Txt(b.Title, new Vector2(r.Position.X, r.Position.Y + 22), 20, new Color(b.C.R, b.C.G, b.C.B, a), HorizontalAlignment.Center, w);
            Txt(b.Sub, new Vector2(r.Position.X, r.Position.Y + 41), 13, new Color(1, 1, 1, a), HorizontalAlignment.Center, w);
            y += 58;
        }
        return y;
    }

    void DrawToasts(Vector2 vs, float y)
    {
        y += 14;
        foreach (var t in G.Toasts)
        {
            float a = Mathf.Clamp(t.Life, 0, 1);
            float w = TextW(t.S, 14);
            DrawRect(new Rect2(vs.X * .5f - w * .5f - 10, y - 15, w + 20, 24), new Color(0.05f, 0.05f, 0.09f, .8f * a));
            Txt(t.S, new Vector2(vs.X * .5f - w * .5f, y + 2), 14, new Color(1, 1, 1, a));
            y += 28;
        }
    }

    void DrawTech(Vector2 vs)
    {
        var all = Game.PageList(G.TechPage);
        float w = 700, rowH = 38, h = 96 + all.Count * rowH;
        var r = new Rect2(vs.X * .5f - w * .5f, vs.Y * .5f - h * .5f - 10, w, h);
        DrawRect(new Rect2(Vector2.Zero, vs), new Color(0, 0, 0, .45f));
        Box(r, new Color("#8ff0ff"));
        Txt("Улучшения · цифра — изучить · ←/→ вкладка · T/Esc — закрыть", new Vector2(r.Position.X + 16, r.Position.Y + 22), 13, new Color("#8ff0ff"));
        for (int pg = 0; pg < 3; pg++)
        {
            var tr = new Rect2(r.Position.X + 16 + pg * 150, r.Position.Y + 32, 144, 24);
            DrawRect(tr, pg == G.TechPage ? new Color(.2f, .4f, .5f, .9f) : new Color(.1f, .12f, .18f, .9f));
            DrawRect(tr, pg == G.TechPage ? new Color("#8ff0ff") : Edge, false, 1);
            Txt(Game.TechPages[pg], new Vector2(tr.Position.X, tr.Position.Y + 17), 13, pg == G.TechPage ? Colors.White : Dim, HorizontalAlignment.Center, 144);
        }
        Txt($"Изучено: {G.TechCount}/{Game.Techs.Length}", new Vector2(r.Position.X, r.Position.Y + 22), 12, Dim, HorizontalAlignment.Right, w - 16);
        int num = 0;
        for (int k = 0; k < all.Count; k++)
        {
            int i = all[k]; var t = Game.Techs[i]; float y = r.Position.Y + 66 + k * rowH;
            bool own = G.Has((Tech)i), ok = t.Ok(G), can = ok && !t.Reward && G.Can(t.Cost);
            var col = own ? new Color("#8ff0ff") : can ? Good : ok ? Warn : new Color(.55f, .57f, .63f);
            string key = t.Reward ? "[★]" : $"[{++num}]";
            Txt($"{key} {t.Name}", new Vector2(r.Position.X + 16, y + 14), 14, col);
            Txt(t.Desc, new Vector2(r.Position.X + 34, y + 29), 11, own ? Dim : new Color(.8f, .82f, .9f));
            string right = own ? "✓ изучено" : !ok ? "нужно: " + t.Req : t.Reward ? "награда" : Game.CostStr(t.Cost);
            Txt(right, new Vector2(r.Position.X, y + 14), 12, col, HorizontalAlignment.Right, w - 16);
            if (ok && !own && !t.Reward && !can) Txt(G.NeedStr(t.Cost), new Vector2(r.Position.X, y + 29), 10, Dim, HorizontalAlignment.Right, w - 16);
        }
    }

    void DrawMap(Vector2 vs)
    {
        float s = 2.6f; float mw = World.Wd * s, mh = World.Ht * s;
        var o = new Vector2(vs.X * .5f - mw * .5f, vs.Y * .5f - mh * .5f - 6);
        DrawRect(new Rect2(Vector2.Zero, vs), new Color(0, 0, 0, .6f));
        Box(new Rect2(o - new Vector2(10, 40), new Vector2(mw + 20, mh + 76)), new Color("#8ff0ff"));
        Txt("Карта · M/Esc — закрыть", new Vector2(o.X, o.Y - 18), 14, new Color("#8ff0ff"));
        DrawRect(new Rect2(o, new Vector2(mw, mh)), new Color(.03f, .04f, .07f));
        DrawTextureRect(G.MapTex, new Rect2(o, new Vector2(mw, mh)), false);
        // границы и названия биомов
        for (int i = 1; i < 3; i++) DrawLine(o + new Vector2(i * 128 * s, 0), o + new Vector2(i * 128 * s, mh), new Color(1, 1, 1, .25f), 1);
        for (int i = 0; i < 3; i++) Txt(Game.RegionNames[i], o + new Vector2(i * 128 * s, mh + 16), 12, i == 1 ? new Color("#7ac8ff") : i == 2 ? new Color("#ff9a5a") : new Color("#7ad45a"), HorizontalAlignment.Center, 128 * s);
        // освобождённые территории — тёплое свечение
        foreach (var a in G.W.Ancients)
            if (a.Lit) DrawCircle(o + a.Pos / 16f * s, G.AncientR(a) / 16f * s, new Color(1f, .85f, .4f, .18f));
        foreach (var l in G.Lamps) DrawCircle(o + l.Pos / 16f * s, 5 * s, new Color(1f, .85f, .4f, .16f));
        // печати
        foreach (var seal in new[] { G.W.Seal1, G.W.Seal2 })
            if (seal.Count > 0 && G.W.Get(seal[0].X, seal[0].Y) == Tl.Seal)
                DrawRect(new Rect2(o + new Vector2(seal[0].X, seal[0].Y - 1) * s, new Vector2(2 * s, 9 * s)), new Color("#d58cff"));
        // фонари
        foreach (var a in G.W.Ancients)
        {
            var p = o + a.Pos / 16f * s;
            bool known = a.Lit || G.IsExplored(a.Pos);
            if (!known) continue;
            if (a.Boss) { DrawColoredPolygon(new[] { p + new Vector2(0, -6), p + new Vector2(5, 0), p + new Vector2(0, 6), p + new Vector2(-5, 0) }, a.Lit ? Gold : new Color("#d58cff")); }
            else DrawCircle(p, 3.5f, a.Lit ? Gold : Colors.White);
        }
        foreach (var p in G.W.Pois)
        {
            if (!(p.Seen || p.Revealed) || p.State >= 3) continue;
            DrawCircle(o + p.Pos / 16f * s, 2.2f, Game.PoiColor(p.Kind));
        }
        foreach (var b in G.Buildings) DrawRect(new Rect2(o + b.Pos / 16f * s - new Vector2(2, 2), new Vector2(4, 4)), new Color("#d9b27c"));
        foreach (var ev in G.Events) { var ep = o + ev.Pos / 16f * s; var ec = Game.EvColor(ev.Kind); DrawColoredPolygon(new[] { ep + new Vector2(0, -5), ep + new Vector2(4, 0), ep + new Vector2(0, 5), ep + new Vector2(-4, 0) }, ec); }
        var pp = o + G.PPos / 16f * s; float blink = Mathf.Sin(G.Time * 6f) > 0 ? 1f : .5f;
        DrawCircle(pp, 4.5f, new Color(0, 0, 0, .8f)); DrawCircle(pp, 3f, new Color(.4f, 1f, .5f, blink));
        Txt("● вы   ◆ страж   ○ фонарь (золото — горит)   • места   ▮ печать", new Vector2(o.X, o.Y + mh + 34), 11, Dim);
    }
}
