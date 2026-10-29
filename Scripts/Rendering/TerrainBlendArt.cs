using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Рисует тайлы переходов и декора кодом из текущих PNG тайлов земли (Assets/Sprites/Tilesets) — перерисуйте тайл травы, земли и т.д., и переходы в игре пересоберутся сами.
/// Тайлы создаются лениво (при первом использовании) и дописываются в листы материалов; все пиксели либо непрозрачны, либо прозрачны. Раскладка листа — см. TerrainBlend.</summary>
public sealed class TerrainBlendArt
{
    static readonly float[] Amp = { 1f, 2f, 3.2f }, Jit = { .3f, .5f, .7f }, WidthPx = { 2.5f, 3.5f, 4.5f, 5.5f, 6.5f }, Band = { 0f, 1.6f, 3f };
    static readonly int[] DX = { 0, 1, 1, 1, 0, -1, -1, -1 }, DY = { -1, -1, 0, 1, 1, 1, 0, -1 };
    static readonly float[,] Bayer = BuildBayer();
    static readonly int[] MaskList = TerrainBlend.MaskList();
    // тайлы земли каждого материала (колонки атласа тайлов) — тот же порядок, что TerrainBlend.Names
    public static readonly int[][] FamilyTiles = { new[] { 0, 1, 2, 3 }, new[] { Art.Dirt }, new[] { 40, 41, 42 }, new[] { 44, 45, 46 }, new[] { Art.Scorch }, new[] { 104, 105 }, new[] { 101, 102, 103 }, new[] { 106, 107, 108 } };

    Color[][][] src;                                   // [материал][вариант тайла][y*16+x]
    readonly Image[] sheets = new Image[TerrainBlend.Sources]; readonly ImageTexture[] tex = new ImageTexture[TerrainBlend.Sources]; readonly bool[] dirty = new bool[TerrainBlend.Sources];
    readonly HashSet<int>[] done = new HashSet<int>[TerrainBlend.Sources];
    Image decor; ImageTexture decorTex; bool decorDirty; readonly HashSet<int> decorDone = new();
    readonly float[][][] prof = new float[TerrainBlend.Variants][][]; readonly float[][] n2 = new float[TerrainBlend.Variants][], jit = new float[TerrainBlend.Variants][], jit2 = new float[TerrainBlend.Variants][];
    Color[][] pal;                                     // [материал] base, dark, light, mid, accent

    public Texture2D Sheet(int m) => tex[m];
    public Texture2D DecorSheet => decorTex;
    public Image SheetImage(int m) => sheets[m];
    public Image DecorImage => decor;

    public TerrainBlendArt(Image tileAtlas)
    {
        for (int m = 0; m < TerrainBlend.Sources; m++)
        {
            sheets[m] = Image.CreateEmpty(TerrainBlend.Cols * 16, TerrainBlend.Rows * 16, false, Image.Format.Rgba8); tex[m] = ImageTexture.CreateFromImage(sheets[m]); done[m] = new HashSet<int>();
        }
        decor = Image.CreateEmpty(16 * TerrainBlend.DecorCols, 16 * TerrainBlend.Sources, false, Image.Format.Rgba8); decorTex = ImageTexture.CreateFromImage(decor);
        for (int v = 0; v < TerrainBlend.Variants; v++)
        {
            prof[v] = new float[8][]; for (int d = 0; d < 8; d++) prof[v][d] = Profile(v, d);
            n2[v] = Noise2(v); jit[v] = new float[256]; jit2[v] = new float[256];
            for (int i = 0; i < 256; i++) { jit[v][i] = TerrainBlend.Hash01(i & 15, i >> 4, v, 7) * 2 - 1; jit2[v][i] = TerrainBlend.Hash01(i & 15, i >> 4, v, 47) * 2 - 1; }
        }
        Reset(tileAtlas);
    }

    /// <summary>Перечитать тайлы земли (после перерисовки) и забыть всё нарисованное.</summary>
    public void Reset(Image tileAtlas)
    {
        src = new Color[TerrainBlend.Sources][][]; pal = new Color[TerrainBlend.Sources][];
        for (int m = 0; m < TerrainBlend.Sources; m++)
        {
            var ids = FamilyTiles[m]; src[m] = new Color[ids.Length][];
            for (int t = 0; t < ids.Length; t++)
            {
                var px = new Color[256];
                for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) { var c = tileAtlas.GetPixel(ids[t] * 16 + x, y); c.A = 1f; px[y * 16 + x] = c; }
                src[m][t] = px;
            }
            pal[m] = Palette(src[m]);
            sheets[m].Fill(new Color(0, 0, 0, 0)); done[m].Clear(); dirty[m] = true;
        }
        decor.Fill(new Color(0, 0, 0, 0)); decorDone.Clear(); decorDirty = true;
    }

    /// <summary>Загрузить в видеопамять нарисованное с прошлого вызова.</summary>
    public void Flush()
    {
        for (int m = 0; m < TerrainBlend.Sources; m++) if (dirty[m]) { tex[m].Update(sheets[m]); dirty[m] = false; }
        if (decorDirty) { decorTex.Update(decor); decorDirty = false; }
    }

    // ─────────────────────────── переходы ───────────────────────────
    public void Ensure(int m, int col, int row)
    {
        int key = row * TerrainBlend.Cols + col; if (!done[m].Add(key)) return;
        int v = row % TerrainBlend.Variants, dl = row / TerrainBlend.Variants % TerrainBlend.DitherLevels, wl = row / (TerrainBlend.Variants * TerrainBlend.DitherLevels);
        var s = src[m][v % src[m].Length]; var img = sheets[m]; int ox = col * 16, oy = row * 16;
        bool scatter = col >= TerrainBlend.ScatterCol;
        var mask = scatter ? 0 : MaskList[col]; var o = new bool[256];
        if (scatter) Scatter(o, col - TerrainBlend.ScatterCol, dl, v); else EdgeMask(o, mask, wl, dl, v);
        for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) if (o[y * 16 + x]) img.SetPixel(ox + x, oy + y, s[y * 16 + x]);
        dirty[m] = true;
    }

    void EdgeMask(bool[] o, int mask, int wl, int dl, int v)
    {
        int cls = v / 2; float a = Amp[cls], jt = Jit[cls], w = WidthPx[wl], band = Band[dl];
        for (int py = 0; py < 16; py++)
            for (int px = 0; px < 16; px++)
            {
                float cx = px + .5f, cy = py + .5f, e = 1e9f;
                for (int i = 0; i < 8; i++)
                {
                    if ((mask & (1 << i)) == 0) continue;
                    float x0 = DX[i] * 16, y0 = DY[i] * 16;
                    float ex = MathF.Max(MathF.Max(x0 - cx, cx - (x0 + 16)), 0), ey = MathF.Max(MathF.Max(y0 - cy, cy - (y0 + 16)), 0);
                    float d = MathF.Sqrt(ex * ex + ey * ey), p = DX[i] == 0 ? prof[v][i][px] : DY[i] == 0 ? prof[v][i][py] : n2[v][py * 16 + px] * .7f;
                    e = MathF.Min(e, d - a * p);
                }
                if (e > 1e8f) continue;
                e += jt * jit[v][py * 16 + px];
                if (band <= 0) o[py * 16 + px] = e < w;
                else o[py * 16 + px] = Math.Clamp(.5f + (w - e) / (2 * band), 0f, 1f) > Bayer[py & 3, px & 3] + jit2[v][py * 16 + px] * .07f;
            }
    }

    void Scatter(bool[] o, int k, int dl, int v)
    {
        float ln = MathF.Sqrt(DX[k] * DX[k] + DY[k] * DY[k]), ux = DX[k] / ln, uy = DY[k] / ln;
        int n = new[] { 2, 3, 5 }[dl] + v % 2, placed = 0;
        var shapes = new (int, int)[][] { new[] { (0, 0) }, new[] { (0, 0), (1, 0) }, new[] { (0, 0), (0, 1) }, new[] { (0, 0), (1, 0), (0, 1) }, new[] { (0, 0), (1, 0), (0, 1), (1, 1) } };
        float H(int t, int salt) => TerrainBlend.Hash01(k * 8 + dl, v * 64 + t, 5, salt);
        for (int t = 1; t <= 400 && placed < n; t++)
        {
            int x = (int)(H(t, 1) * 16), y = (int)(H(t, 2) * 16);
            float g = .5f + .5f * ((x - 7.5f) / 8 * ux + (y - 7.5f) / 8 * uy);
            if (H(t, 3) > g * g + .04f) continue;
            foreach (var (sx, sy) in shapes[(int)(H(t, 4) * shapes.Length)]) if (x + sx < 16 && y + sy < 16) o[(y + sy) * 16 + x + sx] = true;
            placed++;
        }
    }

    float[] Profile(int v, int d)
    {
        var c = new float[6]; for (int i = 0; i < 6; i++) c[i] = TerrainBlend.Hash01(v, d, i, 91) * 2 - 1;
        var r = new float[16];
        for (int i = 0; i < 16; i++)
        {
            float s = (i + .5f) / 16 * 5; int i0 = Math.Min((int)s, 4); float f = s - i0, k = (1 - MathF.Cos(MathF.PI * f)) / 2;
            r[i] = (c[i0] * (1 - k) + c[i0 + 1] * k) * (.25f + .75f * MathF.Sin(MathF.PI * (i + .5f) / 16));
        }
        return r;
    }

    float[] Noise2(int v)
    {
        var c = new float[36]; for (int i = 0; i < 36; i++) c[i] = TerrainBlend.Hash01(v, i, 5, 92) * 2 - 1;
        var r = new float[256];
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                float sx = (x + .5f) / 16 * 5, sy = (y + .5f) / 16 * 5; int ix = Math.Min((int)sx, 4), iy = Math.Min((int)sy, 4);
                float kx = (1 - MathF.Cos(MathF.PI * (sx - ix))) / 2, ky = (1 - MathF.Cos(MathF.PI * (sy - iy))) / 2;
                float a = c[iy * 6 + ix] * (1 - kx) + c[iy * 6 + ix + 1] * kx, b = c[(iy + 1) * 6 + ix] * (1 - kx) + c[(iy + 1) * 6 + ix + 1] * kx;
                r[y * 16 + x] = a * (1 - ky) + b * ky;
            }
        return r;
    }

    static float[,] BuildBayer()
    {
        int[,] b = { { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 } }; var r = new float[4, 4];
        for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++) r[y, x] = b[y, x] / 16f + 1f / 32f; return r;
    }

    // ─────────────────────────── декор ───────────────────────────
    static float Lum(Color c) => .3f * c.R + .59f * c.G + .11f * c.B;
    static float Sat(Color c) { float mx = MathF.Max(c.R, MathF.Max(c.G, c.B)), mn = MathF.Min(c.R, MathF.Min(c.G, c.B)); return mx <= 0 ? 0 : (mx - mn) / mx; }

    /// <summary>Палитра материала из его тайлов: основной, тёмный, светлый, второй по частоте и самый насыщенный (акцент).</summary>
    static Color[] Palette(Color[][] tiles)
    {
        var cnt = new Dictionary<uint, int>();
        foreach (var t in tiles) foreach (var c in t) { var k = c.ToRgba32(); cnt[k] = cnt.GetValueOrDefault(k) + 1; }
        var list = new List<uint>(cnt.Keys); list.Sort((a, b) => cnt[b].CompareTo(cnt[a]));
        Color C(uint k) => new Color(k);
        Color bas = C(list[0]), dark = bas, light = bas, mid = list.Count > 1 ? C(list[1]) : bas, acc = bas; float bs = -1;
        foreach (var k in list)
        {
            var c = C(k); if (Lum(c) < Lum(dark)) dark = c; if (Lum(c) > Lum(light)) light = c;
            float s = Sat(c) * (.4f + Lum(c)); if (s > bs) { bs = s; acc = c; }
        }
        return new[] { bas, dark, light, mid, acc };
    }

    /// <summary>Тайл декора: колонка kind*4 + вариант, строка — материал.</summary>
    public void EnsureDecor(int col, int row)
    {
        int key = row * TerrainBlend.DecorCols + col; if (!decorDone.Add(key)) return;
        int kind = col / 4, v = col % 4, r = row; string name = TerrainBlend.Names[row];
        var p = pal[row]; Color bas = p[0], dark = p[1], light = p[2], mid = p[3], acc = p[4];
        float S(int a, int b = 0) => TerrainBlend.Hash01(r * 8 + kind * 4 + v, a, b, 93);
        int ox = 3 + (int)(S(1) * 8), oy = 3 + (int)(S(2) * 8);
        void Put(int x, int y, Color c) { if (x >= 0 && x < 16 && y >= 0 && y < 16) decor.SetPixel(col * 16 + x, row * 16 + y, c); }
        if (kind == 0)                                                      // пятно: светлые и тёмные мазки той же палитры
        {
            int n = 3 + (int)(S(3) * 3);
            for (int i = 0; i < n; i++)
            {
                int x = ox + (int)((S(5, i) - .5f) * 4), y = oy + (int)((S(6, i) - .5f) * 3);
                Put(x, y, S(7, i) < .45f ? light : dark);
                if (S(8, i) < .5f) Put(x + 1, y, S(9, i) < .6f ? dark : mid);
            }
        }
        else if (name == "grass")                                           // травинки
        {
            for (int i = 0; i < 2 + v % 2; i++) { int x = ox + i * 2 - 1, y = oy + (int)(S(11, i) * 2); Put(x, y, light); Put(x, y + 1, mid == bas ? light : mid); Put(x, y + 2, dark); }
        }
        else if (name == "scorch" || name == "heart")                       // трещины/искры — самый насыщенный цвет материала
        {
            for (int i = 0; i < 2 + v % 3; i++)
            {
                int x = ox + (int)((S(21, i) - .5f) * 8), y = oy + (int)((S(22, i) - .5f) * 6); Put(x, y, acc);
                if (name == "scorch") Put(x + 1, y, acc.Lerp(light, .3f)); else if (i == 0) { Put(x + 1, y, acc.Darkened(.35f)); Put(x, y + 1, acc.Darkened(.35f)); }
            }
        }
        else if (name == "ice")                                             // осколки инея по диагонали
        {
            for (int i = 0; i < 2; i++) { int x = ox - 1 + i * 3, y = oy + i; Put(x, y, light); Put(x + 1, y - 1, mid.Lerp(light, .5f)); Put(x + 2, y - 2, light); }
        }
        else                                                                // камешки с тенью (земля, пещера, пепел, снег)
        {
            var tone = light.Lerp(mid, .15f);
            for (int i = 0; i < 1 + v % 3; i++) { int x = ox + i * 3 - 2, y = oy + i % 2; Put(x, y, tone); Put(x + 1, y, tone); Put(x, y + 1, dark); Put(x + 1, y + 1, dark); }
        }
        decorDirty = true;
    }
}
