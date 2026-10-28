using Godot;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Слой атмосферных частиц биомов: рисуется выше Тьмы, поэтому светлячки, угли и снег видны и в темноте.</summary>
public partial class AmbientLayer : Node2D
{
    public Game G;
    public override void _Draw() => G?.DrawAmbient(this);
}

/// <summary>Живой мир: эффекты фонарей и зданий, атмосфера биомов. Все счётчики ограничены, частицы создаются только рядом с камерой, интенсивность задаёт Settings.Particles.</summary>
public partial class Game
{
    sealed class AmbP { public Vector2 P, V; public float Life, Max, Phase, Size; public int Kind; public Color C; }
    sealed class Emitter { public Vector2 P; public float T, Dur, Rate; public Color C; }
    readonly List<AmbP> ambient = new(); readonly List<Emitter> emitters = new(); AmbientLayer ambientLayer; float ambAcc, lampAcc, bldAcc;
    const int AmbCap = 90;

    void InitAmbient()
    {
        var al = new CanvasLayer { Layer = 6 }; AddChild(al);
        ambientLayer = new AmbientLayer { G = this }; al.AddChild(ambientLayer);
    }

    // ───────────────────────── события фонарей ─────────────────────────
    /// <summary>Событие: зажжён древний фонарь — вспышка у фонаря, волна света, золотые частицы; Тьма отступает по существующей анимации a.Anim.</summary>
    public void OnAncientLit(Ancient a, bool beacon = false)
    {
        float r = Mathf.Max(a.RadiusTiles * TS, 60f);
        AddRing(a.Pos, 22f, .25f, Colors.White);                                   // вспышка у самого фонаря
        AddRing(a.Pos, r, beacon ? 2.2f : 1.5f, new Color("#ffe27a"));            // волна света расходится
        if (beacon) AddRing(a.Pos, r * .6f, 1.6f, new Color("#fff2b0"));
        Burst(a.Pos, new Color("#ffd86b"), Mathf.RoundToInt((beacon ? 40 : 24) * Settings.Particles), beacon ? 120f : 90f);
        emitters.Add(new Emitter { P = a.Pos, Dur = beacon ? 4f : 2.6f, Rate = beacon ? 30f : 18f, C = new Color("#ffd86b") });     // золотые частицы поднимаются вверх
    }

    /// <summary>Событие: поставлен обычный фонарь.</summary>
    public void OnLampLit(Vector2 pos, int kind)
    {
        var c = kind == 2 ? new Color("#bfe9ff") : kind == 1 ? new Color("#ff9a5a") : new Color("#ffd86b");
        AddRing(pos, kind == 0 ? 40f : 52f, .5f, c); Burst(pos + new Vector2(0, -4), c, Mathf.RoundToInt(10 * Settings.Particles), 50f);
    }

    // ───────────────────────── обновление ─────────────────────────
    void UpdateWorldLife(float dt)
    {
        TickDemolish(dt);
        StationFlashT = Mathf.Max(0, StationFlashT - dt);
        if (cam == null || W == null) return;
        var cc = CamCenter; float far = 260f; float sp = Settings.Particles;
        // золотые эмиттеры
        for (int i = emitters.Count - 1; i >= 0; i--)
        {
            var em = emitters[i]; em.T += dt; if (em.T > em.Dur) { emitters.RemoveAt(i); continue; }
            if (Parts.Count < VisualFxConfig.Get().MaxParticles && rng.Randf() < em.Rate * dt * sp)
                Parts.Add(new Particle { P = em.P + new Vector2(rng.RandfRange(-10, 10), rng.RandfRange(-4, 8)), V = new Vector2(rng.RandfRange(-6, 6), -rng.RandfRange(18, 40)), Life = rng.RandfRange(.8f, 1.5f), Max = 1.5f, C = em.C, Size = 1.5f });
        }
        // огонь фонарей: мелкие языки пламени и искры; у древних фонарей — золотая пыль
        lampAcc += dt;
        if (lampAcc > .12f && sp > .05f)
        {
            lampAcc = 0;
            foreach (var l in Lamps)
            {
                if (l.Pos.DistanceSquaredTo(cc) > far * far || Parts.Count > VisualFxConfig.Get().MaxParticles) continue;
                if (rng.Randf() < .55f * sp) Parts.Add(new Particle { P = l.Pos + new Vector2(rng.RandfRange(-1.5f, 1.5f), -6), V = new Vector2(rng.RandfRange(-4, 4), -rng.RandfRange(10, 22)), Life = .35f, Max = .35f, C = l.Kind == 2 ? new Color("#bfe9ff") : rng.Randf() < .5f ? new Color("#ffb04a") : new Color("#ffe27a"), Size = 1f });
            }
            foreach (var a in W.Ancients)
                if (a.Lit && a.Pos.DistanceSquaredTo(cc) < far * far && Parts.Count < VisualFxConfig.Get().MaxParticles && rng.Randf() < .5f * sp)
                    Parts.Add(new Particle { P = a.Pos + new Vector2(rng.RandfRange(-6, 6), -8), V = new Vector2(rng.RandfRange(-5, 5), -rng.RandfRange(12, 26)), Life = .9f, Max = .9f, C = new Color("#ffd86b"), Size = 1f });
        }
        // производственные здания
        bldAcc += dt;
        if (bldAcc > .25f && sp > .05f)
        {
            bldAcc = 0;
            foreach (var b in Buildings)
            {
                if (b.Pos.DistanceSquaredTo(cc) > far * far || Parts.Count > VisualFxConfig.Get().MaxParticles) continue;
                bool working = b.Status == 0 && !b.Off;
                switch (b.Type)
                {
                    case Tl.Forge when working:
                        if (rng.Randf() < .9f * sp) Parts.Add(new Particle { P = b.Pos + new Vector2(rng.RandfRange(-4, 4), -4), V = new Vector2(rng.RandfRange(-22, 22), -rng.RandfRange(25, 55)), Life = .45f, Max = .45f, C = new Color("#ffb04a"), Size = 1f });
                        if (rng.Randf() < .5f * sp) Parts.Add(new Particle { P = b.Pos + new Vector2(5, -9), V = new Vector2(rng.RandfRange(-3, 3), -14), Life = 1f, Max = 1f, C = new Color(.55f, .55f, .6f), Size = 2f });
                        break;
                    case Tl.Furnace:
                        if (rng.Randf() < .45f * sp) Parts.Add(new Particle { P = b.Pos + new Vector2(rng.RandfRange(-2, 2), -9), V = new Vector2(rng.RandfRange(-3, 3), -13), Life = 1.1f, Max = 1.1f, C = new Color(.5f, .5f, .55f), Size = 2f });
                        break;
                    case Tl.Sawmill when working:
                        if (rng.Randf() < .7f * sp) Parts.Add(new Particle { P = b.Pos + new Vector2(rng.RandfRange(-4, 4), 0), V = new Vector2(rng.RandfRange(-16, 16), -rng.RandfRange(8, 22)), Life = .4f, Max = .4f, C = new Color("#d9b27c"), Size = 1f });
                        break;
                    case Tl.Mine or Tl.Drill when working:
                        if (rng.Randf() < .6f * sp) Parts.Add(new Particle { P = b.Pos + new Vector2(rng.RandfRange(-5, 5), 2), V = new Vector2(rng.RandfRange(-10, 10), -rng.RandfRange(6, 16)), Life = .6f, Max = .6f, C = new Color(.62f, .6f, .58f), Size = 1.5f });
                        break;
                    case Tl.Collector or Tl.Refinery when working:
                        if (rng.Randf() < .5f * sp) Parts.Add(new Particle { P = b.Pos + new Vector2(rng.RandfRange(-3, 3), -8), V = new Vector2(rng.RandfRange(-3, 3), -16), Life = 1f, Max = 1f, C = new Color(.8f, .9f, 1f), Size = 1.5f });
                        break;
                }
            }
        }
        UpdateAmbient(dt, cc);
    }

    /// <summary>Атмосфера текущего биома рядом с камерой: листья и светлячки, капли, пепел и угли, снег, частицы Тьмы.</summary>
    void UpdateAmbient(float dt, Vector2 cc)
    {
        float sp = Settings.Particles * Settings.Fx; int reg = RegionAt(cc); var half = ViewSize / Zoom * .5f + new Vector2(24, 24);
        for (int i = ambient.Count - 1; i >= 0; i--)
        {
            var p = ambient[i]; p.Life -= dt; p.Phase += dt;
            p.P += p.V * dt;
            switch (p.Kind)
            {
                case 0: p.P += new Vector2(Mathf.Sin(p.Phase * 2.2f) * 10f * dt, 0); break;                          // лист: покачивание при падении
                case 1: p.V = new Vector2(Mathf.Sin(p.Phase * 1.3f), Mathf.Cos(p.Phase * 1.7f)) * 6f; break;          // светлячок: блуждание
                case 6: p.P += new Vector2(Mathf.Sin(p.Phase * 1.5f) * 6f * dt, 0); break;                           // снег: лёгкий дрейф
                case 7: p.V = new Vector2(Mathf.Sin(p.Phase * .9f) * 5f, -6f); break;                                // частицы Тьмы: плавное всплытие
            }
            if (p.Life <= 0 || Mathf.Abs(p.P.X - cc.X) > half.X + 40 || Mathf.Abs(p.P.Y - cc.Y) > half.Y + 40) ambient.RemoveAt(i);
        }
        if (sp <= .02f || FinStage >= 1) return;
        ambAcc += dt; if (ambAcc < .08f) return; float step = ambAcc; ambAcc = 0;
        int Count(int kind) { int n = 0; foreach (var p in ambient) if (p.Kind == kind) n++; return n; }
        void Add(int kind, Vector2 pos, Vector2 vel, float life, float size, Color c) { if (ambient.Count < AmbCap) ambient.Add(new AmbP { Kind = kind, P = pos, V = vel, Life = life, Max = life, Size = size, C = c, Phase = rng.Randf() * 6.28f }); }
        Vector2 Rand() => cc + new Vector2(rng.RandfRange(-half.X, half.X), rng.RandfRange(-half.Y, half.Y));
        Vector2 Top() => cc + new Vector2(rng.RandfRange(-half.X, half.X), -half.Y);
        switch (reg)
        {
            case 0:
                if (rng.Randf() < 1.1f * step * sp) Add(0, Top(), new Vector2(rng.RandfRange(-4, 4), rng.RandfRange(10, 18)), 12f, 1.5f, rng.Randf() < .5f ? new Color("#5aa840") : new Color("#c98a3a"));
                if (Count(1) < 10 * sp && rng.Randf() < 1.2f * step) Add(1, Rand(), Vector2.Zero, rng.RandfRange(6f, 11f), 1f, new Color("#e8ff7a"));
                if (rng.Randf() < 2.2f * step * sp) Add(2, Rand(), new Vector2(rng.RandfRange(3, 8), rng.RandfRange(-4, 2)), 5f, 1f, new Color(1f, 1f, .85f, .6f));
                break;
            case 1:
                if (rng.Randf() < 1.6f * step * sp) Add(3, Rand(), new Vector2(0, rng.RandfRange(55, 85)), rng.RandfRange(.5f, 1f), 1f, new Color("#8fd0ff"));
                if (Count(2) < 14 * sp && rng.Randf() < 1.5f * step) Add(2, Rand(), new Vector2(rng.RandfRange(-2, 2), rng.RandfRange(-3, 0)), 5f, 1f, new Color(.7f, .85f, 1f, .5f));
                break;
            case 2:
                if (rng.Randf() < 3f * step * sp) Add(4, Rand() + new Vector2(-30, -20), new Vector2(rng.RandfRange(14, 26), rng.RandfRange(3, 10)), 7f, 1.5f, new Color(.55f, .52f, .5f));
                if (Count(5) < 18 * sp && rng.Randf() < 2f * step) Add(5, Rand() + new Vector2(0, 30), new Vector2(rng.RandfRange(-4, 4), -rng.RandfRange(10, 22)), rng.RandfRange(2f, 4f), 1f, new Color("#ff8a3a"));
                break;
            case 3:
                if (rng.Randf() < 3.2f * step * sp) Add(6, Top(), new Vector2(rng.RandfRange(-5, 5), rng.RandfRange(9, 16)), 14f, rng.Randf() < .25f ? 2f : 1f, new Color(.93f, .97f, 1f));
                if (Count(2) < 10 * sp && rng.Randf() < 1.2f * step) Add(2, Rand(), new Vector2(rng.RandfRange(-3, 3), rng.RandfRange(-2, 2)), 4f, 1f, new Color(.75f, .92f, 1f, .7f));
                break;
            case 4:
                if (Count(7) < 24 * sp && rng.Randf() < 2f * step) Add(7, Rand(), new Vector2(0, -6), rng.RandfRange(5f, 9f), rng.Randf() < .3f ? 2f : 1f, new Color(.62f, .28f, .9f));
                break;
        }
    }

    public void DrawAmbient(Node2D c)
    {
        if (cam == null || ambient.Count == 0) return;
        var cc = CamCenter; var half = ViewSize * .5f;
        foreach (var p in ambient)
        {
            float a = Mathf.Clamp(Mathf.Min(p.Life, p.Max - p.Life > .4f ? 1f : (p.Max - p.Life) * 2.5f), 0, 1);
            if (p.Kind == 1) a *= .5f + .5f * Mathf.Sin(p.Phase * 5f);                                           // светлячок мерцает
            if (p.Kind == 2) a *= .6f;
            var s = ((p.P - cc).Floor() * Zoom) + half; float sz = Mathf.Max(1f, Mathf.Round(p.Size * Zoom * .6f));
            c.DrawTextureRect(GD.Load<Texture2D>("res://Assets/Sprites/Vfx/px.png"), new Rect2(s, new Vector2(sz, sz)), false, new Color(p.C.R, p.C.G, p.C.B, a * p.C.A));
            if (p.Kind == 1 && a > .6f) c.DrawTextureRect(GD.Load<Texture2D>("res://Assets/Sprites/Vfx/px.png"), new Rect2(s - new Vector2(sz, sz), new Vector2(sz * 3, sz * 3)), false, new Color(p.C.R, p.C.G, p.C.B, a * .12f));
        }
    }
}
