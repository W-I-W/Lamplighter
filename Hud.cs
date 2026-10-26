using Godot;

namespace Lamplighter;

/// <summary>Компактный HUD, рисуется одним Control.</summary>
public partial class Hud : Control
{
    public Game G;
    Font font = ThemeDB.FallbackFont;

    static readonly Color Panel = new(0.05f, 0.05f, 0.09f, 0.78f);
    static readonly Color Edge = new(1, 1, 1, 0.14f);
    static readonly Color Dim = new(0.75f, 0.78f, 0.85f, 0.85f);

    void Txt(string s, Vector2 p, int size = 14, Color? c = null, HorizontalAlignment al = HorizontalAlignment.Left, float w = -1)
    {
        DrawString(font, p + new Vector2(1, 1), s, al, w, size, new Color(0, 0, 0, .7f));
        DrawString(font, p, s, al, w, size, c ?? Colors.White);
    }

    void Box(Rect2 r) { DrawRect(r, Panel); DrawRect(r, Edge, false, 1); }

    void Bar(Rect2 r, float f, Color c)
    {
        DrawRect(r, new Color(0, 0, 0, .6f));
        DrawRect(new Rect2(r.Position, new Vector2(r.Size.X * Mathf.Clamp(f, 0, 1), r.Size.Y)), c);
    }

    void Icon(int idx, Vector2 p, float s) => DrawTextureRectRegion(Art.IconTex, new Rect2(p, new Vector2(s, s)), new Rect2(idx * 16, 0, 16, 16));

    Vector2 ToScreen(Vector2 w) => (w - G.CamCenter) * Game.Zoom + G.ViewSize * .5f;

    public override void _Draw()
    {
        if (G == null) return;
        Size = G.ViewSize; Position = Vector2.Zero;
        var vs = G.ViewSize;

        // здоровье / опыт
        Box(new Rect2(10, 10, 232, 58));
        Txt($"Ур. {G.Level}", new Vector2(18, 28), 15, new Color("#ffe27a"));
        Txt($"{Game.PickNames[G.Tier]}", new Vector2(18, 28), 12, Dim, HorizontalAlignment.Right, 216);
        Bar(new Rect2(18, 34, 216, 12), G.Hp / G.MaxHp, G.Hp / G.MaxHp < .3f ? new Color("#e03c3c") : new Color("#c8423f"));
        Txt($"{(int)G.Hp}/{(int)G.MaxHp}", new Vector2(18, 45), 11, Colors.White, HorizontalAlignment.Center, 216);
        Bar(new Rect2(18, 53, 216, 6), G.Xp / G.XpNeed, new Color("#7ad45a"));

        // территории
        Box(new Rect2(vs.X - 160, 10, 150, 30));
        Txt($"Фонари: {G.LitCount}/5", new Vector2(vs.X - 152, 31), 15, new Color("#ffe9a0"));

        // цель
        var obj = G.Objective();
        var sz = font.GetStringSize(obj, HorizontalAlignment.Left, -1, 13);
        Box(new Rect2(vs.X * .5f - sz.X * .5f - 10, 10, sz.X + 20, 26));
        Txt(obj, new Vector2(vs.X * .5f - sz.X * .5f, 28), 13, new Color("#d8f0ff"));

        // инвентарь
        float ix = 10, iy = vs.Y - 52;
        Box(new Rect2(ix, iy, 7 * 62 + 8, 44));
        for (int i = 0; i < It.Count; i++)
        {
            var p = new Vector2(ix + 8 + i * 62, iy + 6);
            DrawTextureRectRegion(Art.IconTex, new Rect2(p, new Vector2(28, 28)), new Rect2(i * 16, 0, 16, 16), G.Inv[i] > 0 ? Colors.White : new Color(1, 1, 1, .3f));
            Txt(G.Inv[i].ToString(), p + new Vector2(30, 22), 14, G.Inv[i] > 0 ? Colors.White : Dim);
        }

        // крафт
        float cx = vs.X - 292, cy = vs.Y - 108;
        Box(new Rect2(cx, cy, 282, 98));
        Txt("Крафт (курсор на свободную клетку)", new Vector2(cx + 8, cy + 16), 11, Dim);
        CraftLine(cx + 8, cy + 36, "1", "Печь", "10 кам. 5 дер.", G.Can(Game.FurnaceCost), It.Stone);
        CraftLine(cx + 8, cy + 56, "2", "Фонарь", "4 дер. 2 мед.", G.Can(Game.LanternCost), It.Copper);
        var pc = G.NextPickCost;
        if (pc != null)
        {
            string name = G.Tier == 0 ? "Медная кирка" : "Железная кирка";
            string cost = G.Tier == 0 ? "5 мед. 5 дер." : "6 жел. 3 оск.";
            CraftLine(cx + 8, cy + 76, "3", name + " (у печи)", cost, G.Can(pc) && G.HasFurnace, 7);
        }
        else Txt("Кирка улучшена до максимума", new Vector2(cx + 8, cy + 82), 12, new Color("#7ad45a"));

        // управление
        Txt("WASD — ход · ЛКМ/Пробел — бить и добывать · E — действие · F5 сохр. · F9 новая игра", new Vector2(0, vs.Y - 6), 11, new Color(1, 1, 1, .45f), HorizontalAlignment.Center, vs.X);

        // всплывающие числа (мир → экран)
        foreach (var t in G.Texts)
        {
            var p = ToScreen(t.P); var col = t.C; col.A = Mathf.Clamp(t.Life * 2, 0, 1);
            DrawString(font, p - new Vector2(59, -1), t.S, HorizontalAlignment.Center, 120, 13, new Color(0, 0, 0, col.A * .8f));
            DrawString(font, p - new Vector2(60, 0), t.S, HorizontalAlignment.Center, 120, 13, col);
        }

        // компас на ближайший древний фонарь
        var tgt = G.CompassTarget();
        if (tgt != null)
        {
            var d = tgt.Value - G.PPos; float dist = d.Length();
            if (dist > 70)
            {
                var dir = d.Normalized(); var c = ToScreen(G.PPos) + dir * 62f;
                var perp = new Vector2(-dir.Y, dir.X);
                float pulse = .75f + .25f * Mathf.Sin(G.Time * 4f);
                DrawColoredPolygon(new[] { c + dir * 9, c - dir * 4 + perp * 6, c - dir * 4 - perp * 6 }, new Color(1f, .85f, .4f, pulse));
                Txt($"{(int)(dist / 16)}", c + dir * 20 - new Vector2(10, -4), 11, new Color(1f, .9f, .6f, .9f), HorizontalAlignment.Center, 20);
            }
        }

        // подсказка взаимодействия
        if (G.Prompt != "")
        {
            var s = font.GetStringSize(G.Prompt, HorizontalAlignment.Left, -1, 15);
            var p = new Vector2(vs.X * .5f, vs.Y * .5f + 70);
            Box(new Rect2(p.X - s.X * .5f - 10, p.Y - 18, s.X + 20, 28));
            Txt(G.Prompt, new Vector2(p.X - s.X * .5f, p.Y + 2), 15, new Color("#ffe9a0"));
        }

        // сообщения
        float ty = 56;
        foreach (var t in G.Toasts)
        {
            float a = Mathf.Clamp(t.Life, 0, 1);
            var s = font.GetStringSize(t.S, HorizontalAlignment.Left, -1, 14);
            DrawRect(new Rect2(vs.X * .5f - s.X * .5f - 10, ty - 15, s.X + 20, 24), new Color(0.05f, 0.05f, 0.09f, .8f * a));
            Txt(t.S, new Vector2(vs.X * .5f - s.X * .5f, ty + 2), 14, new Color(1, 1, 1, a));
            ty += 28;
        }

        // кровавая виньетка при низком здоровье
        if (G.Hp / G.MaxHp < .3f) DrawRect(new Rect2(Vector2.Zero, vs), new Color(.6f, 0, 0, .12f + .06f * Mathf.Sin(G.Time * 6f)));
    }

    void CraftLine(float x, float y, string key, string name, string cost, bool ok, int icon)
    {
        var col = ok ? new Color("#9dff8a") : new Color(.62f, .64f, .7f);
        Txt($"[{key}] {name}", new Vector2(x, y), 13, col);
        Txt(cost, new Vector2(x, y), 12, ok ? col : Dim, HorizontalAlignment.Right, 266);
    }
}
