using Godot;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Ветер и растительность: общий материал Assets/Materials/sway.tres (шейдер Assets/Shaders/Vegetation/sway.gdshader) качает только деревья и кустики,
/// остальные тайлы слоя не деформируются. Логика сообщает лишь о событиях (TileShake — удар по тайлу); ветер и амплитуды — в Resources/VisualConfigs/VisualFx.tres.</summary>
public partial class Game
{
    ShaderMaterial swayMat, waterMat; Node2D foliageRoot; readonly List<(Vector2I tile, float age, float amt, int kind)> tileShakes = new();
    const int FoliageTuft = 130, FoliageBush = 131, FoliageFlowers = 132, FoliageTall = 133, FoliageStick = 178, FoliagePebble = 179;

    /// <summary>Палки и камушки на стартовой поляне: растут как кусты (ломаются за несколько ударов, даже голой рукой) и дают первое дерево и камень без кирки. Чистая функция координат и зерна мира.</summary>
    public static int StartForage(int wx, int wy, int seed, Vector2I spawn)
    {
        int dx = wx - spawn.X, dy = wy - spawn.Y, d2 = dx * dx + dy * dy; if (d2 < 9 || d2 > 14 * 14) return 0;
        uint h = FoliageHash(wx, wy, seed + 4242); if ((h & 0xff) >= 22) return 0;
        return ((h >> 8) & 1) == 0 ? FoliageStick : FoliagePebble;
    }

    void InitVegetation()
    {
        swayMat = GD.Load<ShaderMaterial>("res://Assets/Materials/sway.tres");
        var cfg = VisualFxConfig.Get();
        // таблица по колонкам атласа: размах качания, верхний непрозрачный ряд спрайта, свечение (текстура-данные, а не массивы uniform)
        var data = Image.CreateEmpty(256, 1, false, Image.Format.Rgbaf); var tex = Art.TileTex?.GetImage();
        for (int c = 0; c < 256; c++)
        {
            float amp = 0, top = 0, glow = 0, pose = 0;
            for (int i = 0; i < cfg.SwayTiles.Length && i < cfg.SwayAmp.Length; i++) if (cfg.SwayTiles[i] == c) { amp = cfg.SwayAmp[i]; if (cfg.UsePoses && i < cfg.PoseBase.Length) pose = cfg.PoseBase[i]; }
            for (int i = 0; i < cfg.GlowTiles.Length && i < cfg.GlowAmount.Length; i++) if (cfg.GlowTiles[i] == c) glow = cfg.GlowAmount[i];
            if (tex != null && (c + 1) * 16 <= tex.GetWidth())
                for (int y = 0; y < 16; y++) { bool any = false; for (int x = 0; x < 16 && !any; x++) any = tex.GetPixel(c * 16 + x, y).A > .1f; if (any) { top = y; break; } }
            data.SetPixel(c, 0, new Color(amp, top, glow, pose));
        }
        swayMat.SetShaderParameter("tile_data", ImageTexture.CreateFromImage(data));
        swayMat.SetShaderParameter("tree_cut", Art.TreeCut);
        waterMat = GD.Load<ShaderMaterial>("res://Assets/Materials/water.tres"); waterMat.SetShaderParameter("water_col", Art.WaterG);
    }

    /// <summary>Событие: по тайлу ударили (дерево встряхивается и плавно успокаивается).</summary>
    public void TileShake(int x, int y, float mul = 1f)
    {
        var tt = W.Get(x, y); if (!TileSways(tt)) return; float amt = (TileIsPlant(tt) ? 1f : .8f) * mul;
        AddShake(new Vector2I(x, y), amt, TileIsPlant(tt) ? 2 : 0);                    // дерево качается целиком (один тайл), блок — сам
    }

    void AddShake(Vector2I t, float amt, int kind)
    {
        for (int i = 0; i < tileShakes.Count; i++) if (tileShakes[i].tile == t && tileShakes[i].kind == kind) { tileShakes[i] = (t, 0f, amt, kind); return; }
        if (tileShakes.Count >= 16) tileShakes.RemoveAt(0);
        tileShakes.Add((t, 0f, amt, kind));
    }

    bool TileSways(Tl t) => t != Tl.None;                                           // качаются от удара любые блоки: деревья кроной, остальные целиком
    bool TileIsPlant(Tl t) => t == Tl.Tree || t == Tl.Pine || t == Tl.DeadTree;

    void UpdateVegetation(float dt)
    {
        if (swayMat == null) return;
        UpdateTreeOcclusion();
        var cfg = VisualFxConfig.Get();
        var arr = new Vector4[16]; int n = 0;
        for (int i = tileShakes.Count - 1; i >= 0; i--)
        {
            var (t, age, am, kd) = tileShakes[i]; age += dt; if (age >= cfg.HitShakeTime) { tileShakes.RemoveAt(i); continue; }
            tileShakes[i] = (t, age, am, kd);
            float k = 1f - age / cfg.HitShakeTime; arr[n++] = new Vector4(t.X, t.Y, cfg.HitShakePx * am * k * k * Settings.Fx, (t.X * 1.7f) % 50f + kd * 100f);
        }
        swayMat.SetShaderParameter("shakes", arr);
        swayMat.SetShaderParameter("time_s", Time);
        swayMat.SetShaderParameter("wind", cfg.Wind * Settings.Wind);
        swayMat.SetShaderParameter("player", PPos);
        waterMat.SetShaderParameter("time_s", Time); waterMat.SetShaderParameter("strength", Settings.Fx);
    }

    static uint FoliageHash(int x, int y, int seed)
    {
        uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(seed * 83492791); h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15; return h;
    }

    /// <summary>Декоративная трава и кусты на свободных клетках травы леса: только внешний вид, ни на что не влияют и в сохранение не попадают.</summary>
    void FillFoliage(TileMapLayer f, int cx, int cy)
    {
        float dens = VisualFxConfig.Get().FoliageDensity; if (dens <= 0) return;
        for (int y = 0; y < CS; y++)
            for (int x = 0; x < CS; x++)
            {
                int wx = cx * CS + x, wy = cy * CS + y, k = wy * World.Wd + wx;
                if (World.RegionOf(wx) != 0 || W.Ground[k] > 3 || W.Obj[k] != (byte)Tl.None) continue;
                if (CutBush.Contains(k)) continue;
                int fg = StartForage(wx, wy, W.Seed, W.Spawn); if (fg != 0) { f.SetCell(new Vector2I(x, y), srcId, new Vector2I(fg, 0)); continue; }
                uint h = FoliageHash(wx, wy, W.Seed); float r = (h & 0xffff) / 65535f; if (r > dens) continue;
                float q = r / dens; int id = q < .52f ? FoliageTuft : q < .74f ? FoliageTall : q < .90f ? FoliageFlowers : FoliageBush;
                f.SetCell(new Vector2I(x, y), srcId, new Vector2I(id, 0));
            }
    }
}
