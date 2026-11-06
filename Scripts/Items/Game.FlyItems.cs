using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Летящий к игроку значок добытого ресурса. Только визуальный слой: предмет уже выдан в инвентарь (Give), поэтому сеть, сохранения и крафт не затронуты.</summary>
public sealed class FlyItem { public int Item; public Vector2 P, V; public float T, Delay; }

public partial class Game
{
    public readonly List<FlyItem> Flies = new();
    const int MaxFlies = 48;
    float flySfxCd;

    /// <summary>Значки подпрыгивают из места добычи, затем притягиваются к игроку. На стопку — не больше 4 значков.</summary>
    void SpawnFlies(int item, int n, Vector2 at)
    {
        if (Settings.Fx <= 0f || Flies.Count >= MaxFlies) return;
        int k = Math.Clamp(n, 1, 4);
        for (int i = 0; i < k; i++)
        {
            float a = rng.RandfRange(0, Mathf.Tau), sp = rng.RandfRange(40, 85);
            Flies.Add(new FlyItem { Item = item, P = at, V = Vector2.FromAngle(a) * sp + new Vector2(0, -30), Delay = .22f + .05f * i });
        }
    }

    /// <summary>Эффект установки блока или здания: облачко пыли, кольцо, «толчок» тайла и лёгкая тряска.</summary>
    void PlaceFx(Vector2I tile, Color? dust = null, float power = 1f)
    {
        Diag.Mark("first_build");
        var c = TileCenter(tile.X, tile.Y); var col = dust ?? new Color("#d9b27c");
        Burst(c + new Vector2(0, 4), col, (int)(10 * power) + 4, 45f * power);
        AddRing(c, 14f * power + 6f, .35f, new Color(col.R, col.G, col.B, .8f));
        TileShake(tile.X, tile.Y, 2f * power);
        Shake = Mathf.Max(Shake, .5f * power);
    }

    void UpdateFlies(float dt)
    {
        flySfxCd -= dt;
        for (int i = Flies.Count - 1; i >= 0; i--)
        {
            var f = Flies[i]; f.T += dt;
            if (f.T < f.Delay) { f.P += f.V * dt; f.V = f.V.Lerp(Vector2.Zero, 1f - Mathf.Exp(-9f * dt)); continue; }   // фаза «выпрыгивания»: быстро гаснет
            var to = PPos + new Vector2(0, -4) - f.P; float d = to.Length();
            float speed = 110f + 700f * Mathf.Min(1f, (f.T - f.Delay) / .35f);                                          // разгон: чем дольше летит, тем быстрее
            if (d <= speed * dt + 4f)
            {
                Flies.RemoveAt(i);
                if (flySfxCd <= 0) { flySfxCd = .05f; Sfx.Play("pickup", -9, 1.1f + .04f * (f.Item % 6)); }
                continue;
            }
            f.P += to / d * speed * dt;
        }
    }
}
