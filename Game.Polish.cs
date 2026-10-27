using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Мелкие эффекты: расходящиеся кольца (победа над стражем, улучшения, уровень), вспышка победы и искры производства.</summary>
public partial class Game
{
    sealed class Ring { public Vector2 P; public float T, Dur, R; public Color C; }
    readonly List<Ring> rings = new();
    public float VictoryT;
    float prodSparkT;

    public void AddRing(Vector2 p, float r, float dur, Color c)
    {
        rings.Add(new Ring { P = p, R = r, Dur = dur, C = c });
        if (rings.Count > 24) rings.RemoveAt(0);
    }

    void UpdatePolish(float dt)
    {
        for (int i = rings.Count - 1; i >= 0; i--) { rings[i].T += dt; if (rings[i].T >= rings[i].Dur) rings.RemoveAt(i); }
        VictoryT = Mathf.Max(0, VictoryT - dt);
        // дымок и искры над работающими зданиями рядом с игроком
        prodSparkT -= dt;
        if (prodSparkT <= 0)
        {
            prodSparkT = .35f;
            foreach (var b in Buildings)
            {
                if (b.Off || b.Status != 0 || !IsProducer(b.Type) || b.Pos.DistanceTo(PPos) > 260) continue;
                bool hot = b.Type == Tl.Forge || b.Type == Tl.Refinery;
                Parts.Add(new Particle { P = b.Pos + new Vector2(rng.RandfRange(-4, 4), -9), V = new Vector2(rng.RandfRange(-3, 3), -rng.RandfRange(8, 16)), Life = .9f, Max = .9f, C = hot ? new Color("#ff9a3a") : new Color(.7f, .72f, .8f, .8f), Size = hot ? 1.6f : 2f });
            }
        }
    }

    void DrawRings(EntityLayer c)
    {
        foreach (var r in rings)
        {
            float k = r.T / r.Dur, e = 1 - Mathf.Pow(1 - k, 3);
            c.DrawArc(r.P, r.R * e, 0, Mathf.Tau, 48, new Color(r.C.R, r.C.G, r.C.B, (1 - k) * .8f), 2f + 2f * (1 - k));
            c.DrawArc(r.P, r.R * e * .8f, 0, Mathf.Tau, 40, new Color(1, 1, 1, (1 - k) * .35f), 1f);
        }
    }

    /// <summary>Вспышка победы над стражем: кольца света, лучи и золотая вуаль на экране.</summary>
    void VictoryFx(Vector2 p, Color col)
    {
        VictoryT = 3f; AddRing(p, 220, 1.6f, col); AddRing(p, 140, 1.1f, new Color("#ffffff")); AddRing(PPos, 90, 1.3f, new Color("#ffe27a"));
        for (int i = 0; i < 32; i++) { float a = i * Mathf.Tau / 32; Parts.Add(new Particle { P = p, V = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 150, Life = 1.2f, Max = 1.2f, C = col, Size = 2.5f }); }
    }
}
