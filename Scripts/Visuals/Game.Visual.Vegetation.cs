using Godot;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Ветер и растительность: общий материал Assets/Materials/sway.tres (шейдер Assets/Shaders/Vegetation/sway.gdshader) качает только деревья и кустики,
/// остальные тайлы слоя не деформируются. Логика сообщает лишь о событиях (TileShake — удар по тайлу); ветер и амплитуды — в Resources/VisualConfigs/VisualFx.tres.</summary>
public partial class Game
{
    ShaderMaterial swayMat; Node2D foliageRoot; readonly List<(Vector2I tile, float age)> tileShakes = new();
    const int FoliageTuft = 130, FoliageBush = 131, FoliageFlowers = 132, FoliageTall = 133;

    void InitVegetation()
    {
        swayMat = GD.Load<ShaderMaterial>("res://Assets/Materials/sway.tres");
        var cfg = VisualFxConfig.Get(); var amp = new float[140];
        for (int i = 0; i < cfg.SwayTiles.Length && i < cfg.SwayAmp.Length; i++) if (cfg.SwayTiles[i] >= 0 && cfg.SwayTiles[i] < 140) amp[cfg.SwayTiles[i]] = cfg.SwayAmp[i];
        swayMat.SetShaderParameter("amp", amp);
    }

    /// <summary>Событие: по тайлу ударили (дерево встряхивается и плавно успокаивается).</summary>
    public void TileShake(int x, int y)
    {
        if (!TileSways(W.Get(x, y))) return;
        for (int i = 0; i < tileShakes.Count; i++) if (tileShakes[i].tile == new Vector2I(x, y)) { tileShakes[i] = (tileShakes[i].tile, 0f); return; }
        if (tileShakes.Count >= 8) tileShakes.RemoveAt(0);
        tileShakes.Add((new Vector2I(x, y), 0f));
    }

    bool TileSways(Tl t) => t == Tl.Tree || t == Tl.Pine || t == Tl.DeadTree;

    void UpdateVegetation(float dt)
    {
        if (swayMat == null) return;
        var cfg = VisualFxConfig.Get();
        var arr = new Vector4[8]; int n = 0;
        for (int i = tileShakes.Count - 1; i >= 0; i--)
        {
            var (t, age) = tileShakes[i]; age += dt; if (age >= cfg.HitShakeTime) { tileShakes.RemoveAt(i); continue; }
            tileShakes[i] = (t, age);
            float k = 1f - age / cfg.HitShakeTime; arr[n++] = new Vector4(t.X, t.Y, cfg.HitShakePx * k * k * Settings.Fx, t.X * 1.7f);
        }
        swayMat.SetShaderParameter("shakes", arr);
        swayMat.SetShaderParameter("time_s", Time);
        swayMat.SetShaderParameter("wind", cfg.Wind * Settings.Wind);
        swayMat.SetShaderParameter("player", PPos);
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
