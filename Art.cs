using Godot;
using System;

namespace Lamplighter;

/// <summary>Вся графика рисуется кодом: никаких внешних ассетов.</summary>
public static partial class Art
{
    // Индексы тайлов в атласе (колонки)
    public const int Dirt = 4, Tree = 5, Stone = 6, Copper = 7, Iron = 8, Lumen = 9,
        AncOff = 10, AncOn = 11, Furnace = 12, Lantern = 13, Cliff = 14, TileCount = 130;

    public static ImageTexture TileTex, PlayerTex, ShadeTex, WraithTex, IconTex;

    static Color H(string s) => Color.FromHtml(s);
    static readonly Color Clear = new(0, 0, 0, 0);

    sealed class Cv
    {
        readonly Image img; readonly int ox, oy;
        public Cv(Image i, int x, int y) { img = i; ox = x; oy = y; }
        public void P(int x, int y, Color c) { if (x >= 0 && y >= 0 && x < 16 && y < 16) img.SetPixel(ox + x, oy + y, c); }
        public Color G(int x, int y) => (x >= 0 && y >= 0 && x < 16 && y < 16) ? img.GetPixel(ox + x, oy + y) : Clear;
        public void R(int x, int y, int w, int h, Color c) { for (int j = 0; j < h; j++) for (int i = 0; i < w; i++) P(x + i, y + j, c); }
        public void E(float cx, float cy, float rx, float ry, Color c)
        {
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    float dx = (x + .5f - cx) / rx, dy = (y + .5f - cy) / ry;
                    if (dx * dx + dy * dy <= 1f) P(x, y, c);
                }
        }
        public void Outline(Color c)
        {
            var add = new System.Collections.Generic.List<Vector2I>();
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    if (G(x, y).A < .5f && (G(x - 1, y).A > .5f || G(x + 1, y).A > .5f || G(x, y - 1).A > .5f || G(x, y + 1).A > .5f))
                        add.Add(new Vector2I(x, y));
            foreach (var v in add) P(v.X, v.Y, c);
        }
        public void Speckle(Random r, int n, Color c, int x0 = 0, int y0 = 0, int x1 = 16, int y1 = 16, bool opaqueOnly = true)
        {
            for (int i = 0; i < n; i++)
            {
                int x = r.Next(x0, x1), y = r.Next(y0, y1);
                if (!opaqueOnly || G(x, y).A > .5f) P(x, y, c);
            }
        }
        public void Fill(Color c) => R(0, 0, 16, 16, c);
    }

    static Image NewImg(int w) { var i = Image.CreateEmpty(w, 16, false, Image.Format.Rgba8); i.Fill(Clear); return i; }

    public static void Build()
    {
        if (TileTex != null) return;
        BuildTiles(); BuildEntities(); BuildIcons(); ExtraEntities();
    }

    static void BuildTiles()
    {
        var img = NewImg(16 * TileCount);
        var r = new Random(1234);
        // трава: 4 варианта
        for (int v = 0; v < 4; v++)
        {
            var c = new Cv(img, v * 16, 0);
            c.Fill(H("#3f7a3a"));
            for (int i = 0; i < 256; i++)
            {
                double q = r.NextDouble();
                if (q < .10) c.P(i % 16, i / 16, H("#386d34"));
                else if (q < .18) c.P(i % 16, i / 16, H("#488740"));
            }
            for (int i = 0; i < 2 + v; i++)
            {
                int x = r.Next(1, 14), y = r.Next(2, 15);
                c.P(x, y, H("#66ab57")); c.P(x + 1, y - 1, H("#66ab57")); c.P(x + 2, y, H("#66ab57"));
            }
            if (v == 3) { c.P(r.Next(2, 14), r.Next(2, 14), H("#f2e27a")); c.P(r.Next(2, 14), r.Next(2, 14), H("#e9a0c9")); }
        }
        // земля
        var d = new Cv(img, Dirt * 16, 0);
        d.Fill(H("#6b4f33"));
        d.Speckle(r, 40, H("#7a5d3f")); d.Speckle(r, 30, H("#5a4129")); d.Speckle(r, 6, H("#8d6f4d"));

        // дерево
        var t = new Cv(img, Tree * 16, 0);
        t.R(7, 10, 3, 5, H("#6b4423")); t.R(7, 10, 1, 5, H("#4f3118"));
        t.E(8, 6.5f, 6.5f, 5.5f, H("#2d6b2f")); t.E(6.5f, 5.5f, 4.2f, 3.5f, H("#3b8a3a")); t.E(10, 4.5f, 3f, 2.5f, H("#4aa044"));
        t.Speckle(r, 10, H("#245a28"), 2, 3, 14, 11); t.Speckle(r, 8, H("#62bb5a"), 3, 2, 12, 8);
        t.Outline(H("#14281a"));

        // камень
        var s = new Cv(img, Stone * 16, 0);
        DrawRock(s, H("#8a8a96"), H("#b5b5c2"), H("#5e5e6c"));
        s.Outline(H("#2a2a33"));

        // медь
        var cu = new Cv(img, Copper * 16, 0);
        DrawRock(cu, H("#7c7478"), H("#a59ea2"), H("#575156"));
        foreach (var p in new[] { (5, 8), (9, 7), (8, 11), (11, 10), (6, 11) }) { cu.P(p.Item1, p.Item2, H("#e58a3d")); cu.P(p.Item1 + 1, p.Item2, H("#f2b062")); cu.P(p.Item1, p.Item2 + 1, H("#b8631f")); }
        cu.Outline(H("#2a2226"));

        // железо
        var fe = new Cv(img, Iron * 16, 0);
        DrawRock(fe, H("#5f6577"), H("#8a92a8"), H("#40445a"));
        foreach (var p in new[] { (5, 8), (9, 7), (8, 11), (11, 10), (6, 11) }) { fe.P(p.Item1, p.Item2, H("#cbd6e8")); fe.P(p.Item1 + 1, p.Item2, H("#e9f0fb")); fe.P(p.Item1, p.Item2 + 1, H("#8e9bb6")); fe.P(p.Item1 + 1, p.Item2 + 1, H("#b97a4c")); }
        fe.Outline(H("#1c1e2c"));

        // люмен (светящиеся кристаллы)
        var lu = new Cv(img, Lumen * 16, 0);
        DrawRock(lu, H("#364066"), H("#4f5b8c"), H("#232a47"));
        foreach (var p in new[] { (5, 4, 3), (9, 5, 4), (7, 7, 3), (11, 8, 3) })
        {
            for (int k = 0; k < p.Item3; k++) { lu.P(p.Item1, p.Item2 + 7 - k, H("#5fe4ff")); lu.P(p.Item1 + 1, p.Item2 + 7 - k, H("#a8f6ff")); }
            lu.P(p.Item1, p.Item2 + 7 - p.Item3, H("#e8ffff"));
        }
        lu.Outline(H("#10142b"));

        // древний фонарь (выкл/вкл)
        for (int on = 0; on < 2; on++)
        {
            var a = new Cv(img, (on == 0 ? AncOff : AncOn) * 16, 0);
            a.R(3, 13, 10, 2, H("#4a5060")); a.R(5, 11, 6, 2, H("#626a7e"));
            a.R(6, 5, 4, 7, H("#6e7790")); a.R(6, 5, 1, 7, H("#8e98b4")); a.R(9, 5, 1, 7, H("#555d72"));
            a.R(4, 2, 8, 3, H("#7d87a3")); a.R(5, 1, 6, 1, H("#96a0bd")); a.R(4, 4, 8, 1, H("#4d556a"));
            if (on == 0) { a.R(7, 6, 2, 4, H("#1b1e2b")); }
            else
            {
                a.R(7, 6, 2, 4, H("#ffd86b")); a.R(7, 7, 2, 2, H("#fffbe0"));
                a.P(3, 7, H("#ffe9a0")); a.P(12, 7, H("#ffe9a0")); a.P(8, 0, H("#ffe9a0"));
            }
            a.Outline(H("#14161f"));
        }

        // печь
        var f = new Cv(img, Furnace * 16, 0);
        f.R(2, 5, 12, 10, H("#74747f")); f.R(10, 1, 3, 5, H("#5d5d68")); f.R(2, 5, 12, 1, H("#9a9aa6"));
        f.R(2, 9, 12, 1, H("#5d5d68")); f.R(6, 5, 1, 4, H("#5d5d68")); f.R(10, 10, 1, 5, H("#5d5d68")); f.R(4, 10, 1, 5, H("#5d5d68"));
        f.R(5, 10, 6, 5, H("#14121a")); f.R(6, 11, 4, 3, H("#ff9a2e")); f.R(7, 11, 2, 2, H("#ffe27a")); f.P(8, 10, H("#ff9a2e"));
        f.Outline(H("#1b1b22"));

        // установленный фонарь
        var l = new Cv(img, Lantern * 16, 0);
        l.R(7, 7, 2, 8, H("#5a3d22")); l.R(5, 14, 6, 1, H("#3d2a17"));
        l.R(5, 2, 6, 6, H("#ffd86b")); l.R(7, 3, 2, 4, H("#fffbe0")); l.R(4, 1, 8, 2, H("#3a3f4d")); l.R(5, 7, 6, 1, H("#3a3f4d"));
        l.P(8, 0, H("#3a3f4d"));
        l.Outline(H("#1a140c"));

        // скала-граница
        var cl = new Cv(img, Cliff * 16, 0);
        cl.Fill(H("#2a2531"));
        cl.Speckle(r, 50, H("#352f3d")); cl.Speckle(r, 30, H("#1f1b25")); cl.Speckle(r, 10, H("#4a4358"));

        ExtraTiles(img, r);
        TileTex = ImageTexture.CreateFromImage(img);
    }

    static void DrawRock(Cv c, Color body, Color hi, Color lo)
    {
        c.E(8, 9.5f, 6.5f, 5.5f, body);
        c.E(6.5f, 7.5f, 3.2f, 2.2f, hi);
        for (int y = 9; y < 15; y++) for (int x = 9; x < 15; x++) if (c.G(x, y).A > .5f && x + y > 20) c.P(x, y, lo);
    }

    static void BuildEntities()
    {
        // игрок: 2 кадра ходьбы
        var img = NewImg(32);
        for (int f = 0; f < 2; f++)
        {
            var c = new Cv(img, f * 16, 0);
            c.E(8, 10.5f, 4.2f, 4.5f, H("#3b5ba5")); c.E(8, 11.5f, 3f, 3f, H("#4a6dc0"));
            c.E(8, 5.5f, 3.6f, 3.4f, H("#2f477f")); c.R(7, 5, 4, 3, H("#f0c8a0")); c.P(9, 6, H("#1a1a2a"));
            if (f == 0) { c.R(5, 14, 2, 2, H("#2a2f45")); c.R(9, 14, 2, 1, H("#2a2f45")); }
            else { c.R(5, 14, 2, 1, H("#2a2f45")); c.R(9, 14, 2, 2, H("#2a2f45")); }
            c.R(12, 9, 3, 4, H("#ffd86b")); c.P(13, 10, H("#fffbe0")); c.R(12, 8, 3, 1, H("#3a3f4d"));
            c.Outline(H("#10142a"));
        }
        PlayerTex = ImageTexture.CreateFromImage(img);

        // тень-великан
        img = NewImg(32);
        for (int f = 0; f < 2; f++)
        {
            var c = new Cv(img, f * 16, 0);
            c.E(8, 7.5f, 5.2f, 4.8f, H("#2b2040")); c.E(8, 11, 6.6f, 4f, H("#2b2040"));
            for (int x = 1; x < 15; x++) { if ((x + f) % 3 == 0) { c.P(x, 14, Clear); c.P(x, 15, Clear); } else if ((x + f) % 3 == 1) c.P(x, 15, H("#2b2040")); }
            c.Speckle(new Random(5 + f), 12, H("#3d2d5c"), 3, 3, 12, 10);
            c.R(5, 8, 2, 2, H("#ff6bd6")); c.R(9, 8, 2, 2, H("#ff6bd6")); c.P(5, 8, H("#ffffff")); c.P(9, 8, H("#ffffff"));
            c.R(6, 11, 4, 1, H("#0f0818"));
            c.Outline(H("#0c0714"));
        }
        ShadeTex = ImageTexture.CreateFromImage(img);

        // призрак
        img = NewImg(32);
        for (int f = 0; f < 2; f++)
        {
            var c = new Cv(img, f * 16, 0);
            c.E(8, 4.5f, 3.6f, 3.6f, H("#1d2f55"));
            for (int y = 7; y < 16; y++) { int w = 2 + (int)((y - 6) * .55f); c.R(8 - w, y, w * 2, 1, y % 2 == 0 ? H("#172648") : H("#1d2f55")); }
            for (int x = 3; x < 13; x++) if ((x + f) % 3 == 0) c.P(x, 15, Clear);
            c.R(6, 3, 4, 3, H("#050a18")); c.P(6, 4, H("#9ffcff")); c.P(9, 4, H("#9ffcff"));
            c.R(2, 9, 3, 1, H("#5ab8d9")); c.R(11, 9, 3, 1, H("#5ab8d9")); c.P(2, 10, H("#5ab8d9")); c.P(13, 10, H("#5ab8d9"));
            c.Outline(H("#060a1a"));
        }
        WraithTex = ImageTexture.CreateFromImage(img);
    }

    static void BuildIcons()
    {
        // 0 дерево 1 камень 2 медная руда 3 железная руда 4 осколок 5 медь 6 железо 7 кирка
        var img = NewImg(16 * 24);
        var r = new Random(77);
        var c = new Cv(img, 0, 0);
        c.R(2, 4, 12, 4, H("#8a5a2e")); c.R(2, 4, 12, 1, H("#a8743d")); c.R(1, 5, 1, 2, H("#d9b27c")); c.R(14, 5, 1, 2, H("#d9b27c"));
        c.R(3, 9, 11, 4, H("#7a4f28")); c.R(3, 9, 11, 1, H("#9a6a38")); c.R(2, 10, 1, 2, H("#d9b27c"));
        c.Outline(H("#2a1a0c"));
        c = new Cv(img, 16, 0); DrawRock(c, H("#8a8a96"), H("#b5b5c2"), H("#5e5e6c")); c.Outline(H("#2a2a33"));
        c = new Cv(img, 32, 0); DrawRock(c, H("#7c7478"), H("#a59ea2"), H("#575156"));
        foreach (var p in new[] { (5, 8), (9, 7), (8, 11), (11, 10) }) { c.P(p.Item1, p.Item2, H("#e58a3d")); c.P(p.Item1 + 1, p.Item2, H("#f2b062")); }
        c.Outline(H("#2a2226"));
        c = new Cv(img, 48, 0); DrawRock(c, H("#5f6577"), H("#8a92a8"), H("#40445a"));
        foreach (var p in new[] { (5, 8), (9, 7), (8, 11), (11, 10) }) { c.P(p.Item1, p.Item2, H("#cbd6e8")); c.P(p.Item1 + 1, p.Item2, H("#e9f0fb")); }
        c.Outline(H("#1c1e2c"));
        c = new Cv(img, 64, 0);
        for (int y = 2; y < 14; y++) { int w = y < 8 ? 1 + (y - 2) / 2 : 3 - (y - 8) / 2; c.R(8 - w, y, w * 2, 1, H("#5fe4ff")); }
        c.R(7, 4, 1, 6, H("#e8ffff")); c.Outline(H("#10305a"));
        c = new Cv(img, 80, 0);
        c.R(3, 6, 10, 5, H("#d9823b")); c.R(4, 5, 8, 1, H("#f2b062")); c.R(3, 11, 10, 1, H("#8a4a14")); c.R(5, 7, 4, 1, H("#f7c98a"));
        c.Outline(H("#3a1c06"));
        c = new Cv(img, 96, 0);
        c.R(3, 6, 10, 5, H("#a9b6cc")); c.R(4, 5, 8, 1, H("#dfe8f6")); c.R(3, 11, 10, 1, H("#6a7690")); c.R(5, 7, 4, 1, H("#ffffff"));
        c.Outline(H("#1c2438"));
        ExtraIcons(img);
        c = new Cv(img, 368, 0);
        c.R(7, 4, 2, 11, H("#8a5a2e")); c.R(2, 3, 12, 2, H("#cfd6e2")); c.R(1, 4, 2, 2, H("#cfd6e2")); c.R(13, 4, 2, 2, H("#cfd6e2")); c.R(3, 2, 10, 1, H("#ffffff"));
        c.Outline(H("#1a1a22"));
        IconTex = ImageTexture.CreateFromImage(img);
    }
}
