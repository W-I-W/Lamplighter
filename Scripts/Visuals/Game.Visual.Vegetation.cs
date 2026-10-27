using Godot;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Ветер и растительность: общий материал Assets/Materials/sway.tres (шейдер Assets/Shaders/Vegetation/sway.gdshader) качает только деревья и кустики,
/// остальные тайлы слоя не деформируются. Логика сообщает лишь о событиях (TileShake — удар по тайлу); ветер и амплитуды — в Resources/VisualConfigs/VisualFx.tres.</summary>
public partial class Game
{
    ShaderMaterial swayMat, waterMat; Node2D foliageRoot; readonly List<(Vector2I tile, float age, float amt)> tileShakes = new();
    const int FoliageTuft = 130, FoliageBush = 131, FoliageFlowers = 132, FoliageTall = 133;

    void InitVegetation()
    {
        swayMat = GD.Load<ShaderMaterial>("res://Assets/Materials/sway.tres");
        var cfg = VisualFxConfig.Get();
        // таблица по колонкам атласа: размах качания, верхний непрозрачный ряд спрайта, свечение (текстура-данные, а не массивы uniform)
        var data = Image.CreateEmpty(144, 1, false, Image.Format.Rgbaf); var tex = Art.TileTex?.GetImage();
        for (int c = 0; c < 144; c++)
        {
            float amp = 0, top = 0, glow = 0;
            for (int i = 0; i < cfg.SwayTiles.Length && i < cfg.SwayAmp.Length; i++) if (cfg.SwayTiles[i] == c) amp = cfg.SwayAmp[i];
            for (int i = 0; i < cfg.GlowTiles.Length && i < cfg.GlowAmount.Length; i++) if (cfg.GlowTiles[i] == c) glow = cfg.GlowAmount[i];
            if (tex != null && (c + 1) * 16 <= tex.GetWidth())
                for (int y = 0; y < 16; y++) { bool any = false; for (int x = 0; x < 16 && !any; x++) any = tex.GetPixel(c * 16 + x, y).A > .1f; if (any) { top = y; break; } }
            data.SetPixel(c, 0, new Color(amp, top, glow, 1f));
        }
        swayMat.SetShaderParameter("tile_data", ImageTexture.CreateFromImage(data));
        waterMat = GD.Load<ShaderMaterial>("res://Assets/Materials/water.tres"); waterMat.SetShaderParameter("water_col", Art.WaterG);
    }

    /// <summary>Событие: по тайлу ударили (дерево встряхивается и плавно успокаивается).</summary>
    public void TileShake(int x, int y)
    {
        var tt = W.Get(x, y); if (!TileSways(tt)) return; float amt = TileIsPlant(tt) ? 1f : .35f;
        for (int i = 0; i < tileShakes.Count; i++) if (tileShakes[i].tile == new Vector2I(x, y)) { tileShakes[i] = (tileShakes[i].tile, 0f, amt); return; }
        if (tileShakes.Count >= 8) tileShakes.RemoveAt(0);
        tileShakes.Add((new Vector2I(x, y), 0f, amt));
    }

    bool TileSways(Tl t) => t == Tl.Tree || t == Tl.Pine || t == Tl.DeadTree || t == Tl.Stone || t == Tl.Copper || t == Tl.Iron || t == Tl.Lumen || t == Tl.CrystalOre || t == Tl.SilverOre;
    bool TileIsPlant(Tl t) => t == Tl.Tree || t == Tl.Pine || t == Tl.DeadTree;

    void UpdateVegetation(float dt)
    {
        if (swayMat == null) return;
        var cfg = VisualFxConfig.Get();
        var arr = new Vector4[8]; int n = 0;
        for (int i = tileShakes.Count - 1; i >= 0; i--)
        {
            var (t, age, am) = tileShakes[i]; age += dt; if (age >= cfg.HitShakeTime) { tileShakes.RemoveAt(i); continue; }
            tileShakes[i] = (t, age, am);
            float k = 1f - age / cfg.HitShakeTime; arr[n++] = new Vector4(t.X, t.Y, cfg.HitShakePx * am * k * k * Settings.Fx, t.X * 1.7f);
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
                uint h = FoliageHash(wx, wy, W.Seed); float r = (h & 0xffff) / 65535f; if (r > dens) continue;
                float q = r / dens; int id = q < .52f ? FoliageTuft : q < .74f ? FoliageTall : q < .90f ? FoliageFlowers : FoliageBush;
                f.SetCell(new Vector2I(x, y), srcId, new Vector2I(id, 0));
            }
    }
}
