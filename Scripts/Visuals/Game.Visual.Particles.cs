using Godot;

namespace Lamplighter;

/// <summary>Частицы эффектов: виды (точка со шлейфом, искра, дым, осколок, звёздочка, вспышка, луч), их отрисовка и готовые «слои» эффектов —
/// искры по направлению, осколки с гравитацией и отскоком, клубы пыли, мерцающие звёздочки, вспышка, столб лучей.
/// Физика (гравитация, сопротивление, вращение, рост, пол) — в Game.UpdateFx. Количество умножается на Settings.Particles и ограничено VisualFxConfig.MaxParticles.
/// Всё рисуется теми же PNG-примитивами (Gfx): px, line, disc.</summary>
public partial class Game
{
    /// <summary>Число частиц с учётом настройки игрока: при включённых частицах — не меньше одной.</summary>
    int PN(float n) => n <= 0 ? 0 : Mathf.Max(Settings.Particles > .05f ? 1 : 0, Mathf.RoundToInt(n * Settings.Particles));
    bool PartsFull => Parts.Count > VisualFxConfig.Get().MaxParticles;

    /// <summary>Оттенок: k &gt; 0 — к белому, k &lt; 0 — к чёрному. Прозрачность исходного цвета сохраняется.</summary>
    public static Color Tone(Color c, float k)
    {
        var r = k >= 0 ? c.Lerp(Colors.White, k) : c.Lerp(Colors.Black, -k); r.A = c.A; return r;
    }

    /// <summary>Искры конусом по направлению dir: светлые штрихи, которые гравитация загибает вниз.</summary>
    public void Sparks(Vector2 p, Vector2 dir, Color c, int n, float speed, float spread = .9f)
    {
        n = PN(n); var d0 = dir.LengthSquared() > .01f ? dir.Normalized() : Vector2.Up;
        for (int i = 0; i < n && !PartsFull; i++)
        {
            var d = d0.Rotated(rng.RandfRange(-spread, spread)); float life = rng.RandfRange(.18f, .4f);
            Parts.Add(new Particle { P = p, V = d * rng.RandfRange(.45f, 1f) * speed, Life = life, Max = life, C = Tone(c, rng.RandfRange(.15f, .65f)), Kind = Particle.Spark, Size = 1f, Grav = 150f, Drag = 2.5f });
        }
    }

    /// <summary>Осколки: вылетают вверх и в стороны, вращаются, падают и раз-другой отскакивают от «земли» чуть ниже точки p.</summary>
    public void Debris(Vector2 p, Color c, int n, float speed = 70f)
    {
        n = PN(n);
        for (int i = 0; i < n && !PartsFull; i++)
        {
            float life = rng.RandfRange(.45f, .8f);
            Parts.Add(new Particle
            {
                P = p + new Vector2(rng.RandfRange(-3, 3), rng.RandfRange(-3, 2)), V = new Vector2(rng.RandfRange(-1f, 1f) * speed * .75f, -rng.RandfRange(.5f, 1f) * speed),
                Life = life, Max = life, C = Tone(c, rng.RandfRange(-.35f, .25f)), Kind = Particle.Shard, Size = rng.RandfRange(1.8f, 3.2f), Grav = 330f, Drag = .8f,
                Rot = rng.Randf() * Mathf.Tau, Spin = rng.RandfRange(-14f, 14f), Floor = p.Y + rng.RandfRange(3f, 8f),
            });
        }
    }

    /// <summary>Клубы пыли или дыма: кружки, которые расплываются и тают.</summary>
    public void Puff(Vector2 p, Color c, int n, float spread = 6f, float rise = 10f, float alpha = .6f)
    {
        n = PN(n);
        for (int i = 0; i < n && !PartsFull; i++)
        {
            float life = rng.RandfRange(.35f, .7f);
            Parts.Add(new Particle
            {
                P = p + new Vector2(rng.RandfRange(-spread, spread) * .5f, rng.RandfRange(-2, 2)), V = new Vector2(rng.RandfRange(-1f, 1f) * spread * 2.5f, -rng.RandfRange(.3f, 1f) * rise),
                Life = life, Max = life, C = Tone(c, rng.RandfRange(-.1f, .2f)), Kind = Particle.Smoke, Size = rng.RandfRange(1.4f, 2.4f), Grow = rng.RandfRange(4f, 8f), Drag = 3f, Alpha = alpha,
            });
        }
    }

    /// <summary>Мерцающие четырёхлучевые звёздочки в круге radius, медленно всплывают.</summary>
    public void Sparkle(Vector2 p, Color c, int n, float radius, float rise = 18f)
    {
        n = PN(n);
        for (int i = 0; i < n && !PartsFull; i++)
        {
            float a = rng.Randf() * Mathf.Tau, life = rng.RandfRange(.4f, .9f);
            Parts.Add(new Particle
            {
                P = p + Vector2.FromAngle(a) * radius * Mathf.Sqrt(rng.Randf()), V = new Vector2(rng.RandfRange(-4, 4), -rng.RandfRange(.4f, 1f) * rise),
                Life = life, Max = life, C = Tone(c, rng.RandfRange(0f, .6f)), Kind = Particle.Star, Size = rng.RandfRange(1.5f, 3f), Drag = .5f,
            });
        }
    }

    /// <summary>Короткая вспышка: светлый круг, который быстро раздувается и гаснет.</summary>
    public void FlashAt(Vector2 p, Color c, float r, float life = .12f)
    {
        if (Settings.Fx <= .01f) return;
        Parts.Add(new Particle { P = p, Life = life, Max = life, C = c, Kind = Particle.Glow, Size = r * .45f, Grow = r * .55f / life, Drag = 0 });
    }

    /// <summary>Столб света: вертикальные лучи, поднимающиеся из полосы шириной 2*width.</summary>
    public void Rays(Vector2 p, Color c, int n, float width, float height)
    {
        n = PN(n);
        for (int i = 0; i < n && !PartsFull; i++)
        {
            float life = rng.RandfRange(.4f, .8f);
            Parts.Add(new Particle { P = p + new Vector2(rng.RandfRange(-width, width), rng.RandfRange(0, 4)), V = new Vector2(0, -rng.RandfRange(10, 30)), Life = life, Max = life, C = Tone(c, rng.RandfRange(0f, .4f)), Kind = Particle.Ray, Size = rng.RandfRange(.5f, 1f) * height, Drag = 0 });
        }
    }

    // ───────────────────────── отрисовка ─────────────────────────
    void DrawParticles(EntityLayer c)
    {
        foreach (var p in Parts)
        {
            float k = Mathf.Clamp(p.Life / p.Max, 0, 1);                      // 1 — только родилась, 0 — исчезает
            var col = p.C; col.A = Mathf.Clamp(k * 1.5f, 0, 1) * p.Alpha;
            switch (p.Kind)
            {
                case Particle.Spark:
                {
                    float sp = p.V.Length(); var d = sp > .5f ? p.V / sp : Vector2.Right; float len = Mathf.Clamp(sp * .06f, 2.5f, 11f) * (.4f + .6f * k);
                    Gfx.Glow(c, p.P, 2.2f, new Color(col.R, col.G, col.B, col.A * .35f));                      // ореол головки
                    Gfx.Streak(c, p.P - d * len, p.P + d * .8f, col, .9f + .5f * p.Size);
                    Gfx.Dot(c, p.P, .9f, new Color(1, 1, 1, col.A));                                           // раскалённая головка
                    break;
                }
                case Particle.Smoke:
                    Gfx.Puff(c, p.P, Mathf.Max(1f, p.Size * 1.3f), p.Rot + p.Life * .8f, new Color(col.R, col.G, col.B, col.A * .6f));
                    break;
                case Particle.Shard:
                    Gfx.Shard(c, p.P, Mathf.Max(1.5f, p.Size * 1.25f), p.Rot, col);
                    break;
                case Particle.Star:
                {
                    float l = p.Size * (.6f + 1.1f * Mathf.Sin((1 - k) * Mathf.Pi));  // вспыхивает к середине жизни и гаснет
                    Gfx.Star(c, p.P, l * 1.6f, col);
                    Gfx.Star(c, p.P, l * .9f, new Color(1, 1, 1, col.A * .8f));
                    break;
                }
                case Particle.Glow:
                {
                    float r = Mathf.Max(.5f, p.Size) * 1.6f;
                    Gfx.Glow(c, p.P, r, new Color(col.R, col.G, col.B, col.A * .6f));
                    Gfx.Glow(c, p.P, r * .5f, new Color(1, 1, 1, col.A * .8f));
                    break;
                }
                case Particle.Ray:
                {
                    float h = p.Size * (.4f + .6f * Mathf.Sin((1 - k) * Mathf.Pi));
                    Gfx.Ray(c, p.P, h, 5f, new Color(col.R, col.G, col.B, col.A * .55f));
                    Gfx.Ray(c, p.P, h * .9f, 2f, new Color(1, 1, 1, col.A * .8f));
                    break;
                }
                default:
                {
                    float s = p.Size * (k < .35f ? .55f + k / .35f * .45f : 1f);                 // к концу жизни точка сжимается
                    float sp2 = p.V.LengthSquared();
                    if (sp2 > 900f)                                                                // быстрая точка тянет за собой шлейф
                    {
                        float sp = Mathf.Sqrt(sp2); var d = p.V / sp;
                        Gfx.Streak(c, p.P - d * Mathf.Min(sp * .04f, 6f), p.P, new Color(col.R, col.G, col.B, col.A * .55f), Mathf.Max(.7f, s * .45f));
                    }
                    Gfx.Glow(c, p.P, s * 1.4f, new Color(col.R, col.G, col.B, col.A * .3f));     // мягкий ореол
                    Gfx.Dot(c, p.P, s * .6f, col);
                    break;
                }
            }
        }
    }

    /// <summary>Ударная волна: яркий передний фронт, широкий тающий шлейф позади, белое эхо внутри и блики, бегущие по кольцу.</summary>
    void DrawRings(EntityLayer c)
    {
        foreach (var r in rings)
        {
            float k = r.T / r.Dur, e = 1 - Mathf.Pow(1 - k, 3), fade = 1 - k, rad = r.R * e;
            int segs = Mathf.Clamp((int)(rad * .6f) + 12, 12, 56);
            Gfx.Ring(c, r.P, Mathf.Max(0, rad - 2f - 3f * fade), segs, new Color(r.C.R, r.C.G, r.C.B, fade * .2f), 3f + 5f * fade);     // шлейф
            Gfx.Ring(c, r.P, rad, segs, new Color(r.C.R, r.C.G, r.C.B, fade * .85f), 1.5f + 2f * fade);                               // фронт
            Gfx.Ring(c, r.P, rad * .72f, Mathf.Max(12, segs - 8), new Color(1, 1, 1, fade * fade * .35f), 1f);                        // эхо
            if (rad < 10f) continue;
            int glints = rad > 60f ? 10 : 6;
            for (int i = 0; i < glints; i++)
            {
                var gp = r.P + Vector2.FromAngle(r.Seed + i * Mathf.Tau / glints + k * 1.2f) * rad;
                Gfx.Star(c, gp, 3f + 4f * fade, new Color(1, 1, 1, fade * .9f));
            }
        }
    }
}
