using Godot;
using System;

namespace Lamplighter;

/// <summary>Правила визуального смешивания поверхностей: какой материал «заступает» на соседние клетки, какой тайл листа перехода выбрать (листы рисует TerrainBlendArt кодом из PNG тайлов земли).
/// Чистые функции от массива Ground, Seed и мировых координат — порядок загрузки чанков не влияет на результат. Игровых данных не меняет.
/// Лист материала: 55 колонок (47 канонических масок 8 соседей + 8 направлений «вкраплений») × 90 строк = (уровень ширины 0..4 · 3 + уровень dithering 0..2) · 6 + вариант.</summary>
public static class TerrainBlend
{
    public const int Cols = 55, ScatterCol = 47, Variants = 6, DitherLevels = 3, WidthLevels = 5, Rows = WidthLevels * DitherLevels * Variants;
    /// <summary>Порядок совпадает с TerrainBlendArt.FamilyTiles. 8 — вода, 9 — раскалённый пепел (опасные: принимают лишь узкий край и сами не заступают).</summary>
    public static readonly string[] Names = { "grass", "dirt", "cave", "ash", "scorch", "ice", "snow", "heart" };
    public const int Sources = 8, Water = 8, HotAsh = 9, DecorCols = 8;
    static readonly int[] Rank = { 2, 1, 3, 4, 5, 6, 7, 8, 0, 0 };
    static readonly int[] DX = { 0, 1, 1, 1, 0, -1, -1, -1 }, DY = { -1, -1, 0, 1, 1, 1, 0, -1 };   // бит i: N, NE, E, SE, S, SW, W, NW
    static readonly sbyte[] FamOf = new sbyte[256];
    static readonly byte[] CanonIdx = new byte[256];
    public static readonly int MaskCount;

    static TerrainBlend()
    {
        for (int i = 0; i < 256; i++) FamOf[i] = -1;
        void Set(sbyte f, params int[] ids) { foreach (var i in ids) FamOf[i] = f; }
        Set(0, 0, 1, 2, 3); Set(1, Art.Dirt); Set(2, 40, 41, 42); Set(3, 44, 45, 46); Set(4, Art.Scorch); Set(5, 104, 105); Set(6, 101, 102, 103); Set(7, 106, 107, 108);
        Set(Water, Art.WaterG); Set(HotAsh, Art.HotAsh);
        int n = 0; for (int m = 0; m < 256; m++) if (Canon(m) == m) CanonIdx[m] = (byte)n++;
        MaskCount = n;
    }

    /// <summary>Угловой бит нужен, только если оба прилегающих боковых бита не установлены (иначе угол уже покрыт краем).</summary>
    public static int Canon(int m)
    {
        if ((m & 2) != 0 && (m & 5) != 0) m &= ~2;
        if ((m & 8) != 0 && (m & 20) != 0) m &= ~8;
        if ((m & 32) != 0 && (m & 80) != 0) m &= ~32;
        if ((m & 128) != 0 && (m & 65) != 0) m &= ~128;
        return m;
    }
    /// <summary>Канонические маски по возрастанию — индекс = колонка листа.</summary>
    public static int[] MaskList() { var l = new System.Collections.Generic.List<int>(); for (int m = 0; m < 256; m++) if (Canon(m) == m) l.Add(m); return l.ToArray(); }
    public static int MaskColumn(int mask) => CanonIdx[Canon(mask)];
    public static int FamilyOfGround(int groundTile) => groundTile >= 0 && groundTile < 256 ? FamOf[groundTile] : -1;

    public struct Params { public int Seed; public float WidthF, WidthJitter, Dither, Rough, Scatter, Decor; public bool On; }
    public static Params From(TerrainBlendConfig c, int worldSeed) => new Params
    {
        On = c.Enabled, Seed = worldSeed + c.SeedSalt, WidthF = Math.Clamp(c.BlendWidth, 0f, 4f), WidthJitter = c.WidthJitter,
        Dither = Math.Clamp(c.DitherIntensity, 0f, 1f), Rough = Math.Clamp(c.Roughness, 0f, 1f), Scatter = Math.Clamp(c.ScatterDensity, 0f, 1f), Decor = Math.Clamp(c.DecorDensity, 0f, 1f)
    };

    public struct Layer { public int Mat, Col, Row; public bool Any => Mat >= 0; }
    public struct Result { public Layer Under, Over; }

    // ─────────────────────────── хэш и шум по мировым координатам ───────────────────────────
    public static uint Hash(int x, int y, int seed, int salt)
    {
        unchecked
        {
            uint h = (uint)x * 0x9E3779B1u ^ (uint)y * 0x85EBCA77u ^ (uint)seed * 0xC2B2AE3Du ^ (uint)salt * 0x27D4EB2Fu;
            h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15; return h;
        }
    }
    public static float Hash01(int x, int y, int seed, int salt) => (Hash(x, y, seed, salt) & 0xffffff) / 16777215f;

    /// <summary>Гладкий шум решётки с шагом cell клеток, значения −1..1.</summary>
    public static float Value(int x, int y, int seed, int salt, int cell)
    {
        int gx = FloorDiv(x, cell), gy = FloorDiv(y, cell);
        float fx = (x - gx * cell) / (float)cell, fy = (y - gy * cell) / (float)cell;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        float a = Hash01(gx, gy, seed, salt), b = Hash01(gx + 1, gy, seed, salt), c = Hash01(gx, gy + 1, seed, salt), d = Hash01(gx + 1, gy + 1, seed, salt);
        return (a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy) * 2f - 1f;
    }
    static int FloorDiv(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);

    static int Fam(byte[] g, int x, int y) => World.In(x, y) ? FamOf[g[y * World.Wd + x]] : -1;

    // ─────────────────────────── переходы ───────────────────────────
    /// <summary>Тайлы перехода для клетки: до двух материалов (нижний слой — слабее по рангу) либо россыпь вкраплений. Зависит только от g, p и координат.</summary>
    public static Result Cell(byte[] g, int x, int y, in Params p)
    {
        var res = new Result { Under = new Layer { Mat = -1 }, Over = new Layer { Mat = -1 } };
        if (!p.On) return res;
        int own = Fam(g, x, y); if (own < 0) return res;
        bool hazard = own >= Sources;
        Span<int> masks = stackalloc int[Sources];
        for (int i = 0; i < 8; i++)
        {
            int f = Fam(g, x + DX[i], y + DY[i]);
            if (f >= 0 && f < Sources && f != own && Rank[f] > Rank[own]) masks[f] |= 1 << i;
        }
        int hi = -1, lo = -1;
        for (int m = 0; m < Sources; m++)
        {
            if (masks[m] == 0) continue;
            if (hi < 0 || Rank[m] > Rank[hi]) { lo = hi; hi = m; } else if (lo < 0 || Rank[m] > Rank[lo]) lo = m;
        }
        // уровни ширины и dithering — функция клетки (низкочастотный шум), а не материала: соседние клетки отличаются не более чем на уровень
        int wl = 0, dl = 0;
        if (!hazard)
        {
            wl = Math.Clamp((int)MathF.Round(p.WidthF + p.WidthJitter * Value(x, y, p.Seed, 11, 5)), 0, WidthLevels - 1);
            dl = Math.Clamp((int)MathF.Round(p.Dither * 2f + (p.Dither > 0 ? Value(x, y, p.Seed, 12, 4) * .6f : 0f)), 0, DitherLevels - 1);
        }
        if (hi >= 0)
        {
            if (lo >= 0) { res.Under = Make(p, x, y, lo, masks[lo], 0, wl, dl, hazard); res.Over = Make(p, x, y, hi, masks[hi], 1, wl, dl, hazard); } else res.Under = Make(p, x, y, hi, masks[hi], 0, wl, dl, hazard);
            return res;
        }
        if (hazard || p.Scatter <= 0) return res;
        // вкрапления: клетка не касается более сильного материала, но её сосед касается — россыпь пятен, гуще в сторону границы
        int best = -1;
        Span<float> sx = stackalloc float[Sources]; Span<float> sy = stackalloc float[Sources]; Span<bool> seen = stackalloc bool[Sources];
        for (int i = 0; i < 8; i++)
        {
            int qx = x + DX[i], qy = y + DY[i], qf = Fam(g, qx, qy); if (qf < 0) continue;
            for (int j = 0; j < 8; j++)
            {
                int f2 = Fam(g, qx + DX[j], qy + DY[j]);
                if (f2 < 0 || f2 >= Sources || f2 == qf || f2 == own || Rank[f2] <= Rank[qf] || Rank[f2] <= Rank[own]) continue;
                seen[f2] = true; sx[f2] += DX[i]; sy[f2] += DY[i];
            }
        }
        for (int m = 0; m < Sources; m++) if (seen[m] && (best < 0 || Rank[m] > Rank[best])) best = m;
        if (best < 0) return res;
        float vx = sx[best], vy = sy[best];
        float chance = p.Scatter * (.35f + .65f * (Value(x, y, p.Seed, 13, 3) * .5f + .5f));
        if (Hash01(x, y, p.Seed, 14) >= chance) return res;
        if (vx == 0 && vy == 0) vx = 1;
        int dir = (((int)MathF.Round(MathF.Atan2(vy, vx) / (MathF.PI / 4f)) + 2) % 8 + 8) % 8;
        res.Under = new Layer { Mat = best, Col = ScatterCol + dir, Row = (wl * DitherLevels + dl) * Variants + (int)(Hash(x, y, p.Seed, 15) % Variants) };
        return res;
    }

    static Layer Make(Params p, int x, int y, int mat, int mask, int salt, int wl, int dl, bool hazard)
    {
        int cls = 0;
        if (!hazard)
        {
            float u = Hash01(x, y, p.Seed, 20 + salt), r = p.Rough, p0 = 1f - r, p2 = r * r;
            cls = u < p0 ? 0 : u < p0 + p2 ? 2 : 1;
        }
        int v = cls * 2 + (int)(Hash(x, y, p.Seed, 30 + salt) & 1);
        return new Layer { Mat = mat, Col = CanonIdx[Canon(mask)], Row = (wl * DitherLevels + dl) * Variants + v };
    }

    // ─────────────────────────── декоративный слой ───────────────────────────
    /// <summary>Декор (травинки, камешки, угольки…) для свободной клетки: колонка (вид·4 + вариант) и строка (материал) листа decor.png, либо (−1,−1).
    /// У границ материалов часто выпадает «акцент» соседнего материала (камешки на траве у земли, травинки на земле у травы).</summary>
    public static Vector2I Decor(byte[] g, int x, int y, in Params p)
    {
        var none = new Vector2I(-1, -1);
        if (!p.On || p.Decor <= 0) return none;
        int own = Fam(g, x, y); if (own < 0 || own >= Sources) return none;
        int other = -1;
        for (int i = 0; i < 8; i++) { int f = Fam(g, x + DX[i], y + DY[i]); if (f >= 0 && f < Sources && f != own && (other < 0 || Rank[f] > Rank[other])) other = f; }
        float dens = p.Decor * (other >= 0 ? 2.5f : 1f) * (.55f + .45f * (Value(x, y, p.Seed, 40, 6) * .5f + .5f));
        if (Hash01(x, y, p.Seed, 41) >= dens) return none;
        int v = (int)(Hash(x, y, p.Seed, 42) & 3), row = own, kind = Hash01(x, y, p.Seed, 43) < .55f ? 0 : 1;
        if (other >= 0 && Hash01(x, y, p.Seed, 44) < .6f) { row = other; kind = 1; }
        return new Vector2I(kind * 4 + v, row);
    }
}
