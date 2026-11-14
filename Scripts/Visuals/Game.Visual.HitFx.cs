using Godot;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Эффект попадания по врагу: серп удара по направлению атаки (ближний бой), ударная волна у земли, искры по направлению удара,
/// брызги цвета самого врага, которые падают и оставляют пятна на земле, вспышка в точке удара; при сильном ударе — линии удара и звёздочки.
/// Только внешний вид: урон, отбрасывание и тайминги боя не меняются. Интенсивность — Settings.Fx и Settings.Particles.
/// Текстуры: Assets/Sprites/Vfx/fx_slash, fx_ring, fx_splat (tools/GraphicsGenerator/make_vfx_soft.py).</summary>
/// <summary>Слой серпов и линий удара: поверх белой вспышки попадания (FlashLayer), иначе вспышка закрывает серп.</summary>
public partial class HitTopLayer : Node2D
{
    public Game G;
    public override void _Draw() => G?.DrawHitTop(this);
}

public partial class Game
{
    HitTopLayer hitTopLayer;
    sealed class HitSlash { public Vector2 P, Dir; public float T, Life, Size, Tilt; public Color C; }
    sealed class HitWave { public Vector2 P; public float T, Life, R; public Color C; }
    sealed class HitSplat { public Vector2 P; public float T, Life, Size, Rot; public Color C; }
    sealed class HitLine { public Vector2 P, D; public float T, Life, Len; }
    readonly List<HitSlash> hitSlashes = new(); readonly List<HitWave> hitWaves = new(); readonly List<HitSplat> hitSplats = new(); readonly List<HitLine> hitLines = new();
    int slashFlip;
    const int SplatCap = 48;

    /// <summary>Цвет «крови» врага: брызги, пятна, волна. Подобран под палитру спрайтов.</summary>
    public static Color BloodColor(EK k) => k switch
    {
        EK.Shade or EK.Swarm => new Color("#8a6ad0"), EK.Wraith => new Color("#6a90e0"), EK.Spitter => new Color("#9ad050"), EK.Bomber => new Color("#e05040"),
        EK.Eel => new Color("#4cc8c0"), EK.Leech => new Color("#6a5aa8"), EK.Beetle => new Color("#7aa4d8"), EK.DepthWarden => new Color("#4a9ae8"),
        EK.Imp => new Color("#ff8a2e"), EK.Golem => new Color("#ff9a2e"), EK.Spirit => new Color("#d8d0c8"), EK.AshWarden => new Color("#ff7a2a"),
        EK.IceWolf or EK.FrostPriest or EK.Sentinel or EK.WinterWarden => new Color("#aee6ff"),
        EK.Mirage or EK.Decoy => new Color("#c890f4"), EK.Weaver => new Color("#a070e0"), EK.Stalker => new Color("#8ff0ff"), EK.Source => new Color("#b070ff"),
        EK.Warden => new Color("#ff7ad0"), _ => new Color("#c070ff")
    };

    /// <summary>Создаёт все слои эффекта попадания. crit — сильный удар (≥30% здоровья), hc — класс врага (0 обычный, 1 сильный, 2 босс).</summary>
    void HitFx(Enemy e, Vector2 dir, bool melee, bool crit, int hc)
    {
        if (Settings.Fx <= .01f) return;
        var d = dir.LengthSquared() > .01f ? dir.Normalized() : (e.Pos - PPos).LengthSquared() > .01f ? (e.Pos - PPos).Normalized() : Vector2.Right;
        var blood = BloodColor(e.Kind); float sc = hc == 2 ? 1.7f : e.Elite ? 1.25f : hc == 1 ? 1.15f : 1f;
        var body = e.Pos + new Vector2(0, -4f * sc); var hit = body - d * 3f * sc;              // точка удара — на стороне, обращённой к атакующему

        if (melee) hitSlashes.Add(new HitSlash { P = body, Dir = d, Life = crit ? .22f : .17f, Size = (crit ? 34f : 26f) * sc, Tilt = (slashFlip++ % 2 == 0 ? 1f : -1f) * .45f, C = crit ? new Color("#ffb83a") : new Color("#ffe08a") });      // тёплый серп с белой сердцевиной: читается и на белой вспышке врага
        hitWaves.Add(new HitWave { P = e.Pos + new Vector2(0, 2), Life = crit ? .34f : .22f, R = (crit ? 20f : 12f) * sc, C = crit ? new Color("#ffe27a") : Tone(blood, .45f) });
        if (crit) hitWaves.Add(new HitWave { P = e.Pos + new Vector2(0, 2), Life = .24f, R = 11f * sc, C = Colors.White });

        Sparks(hit, d, crit ? new Color("#ffe9a0") : new Color("#fff4dc"), crit ? 10 : 5, crit ? 210f : 150f, .55f);
        FlashAt(hit, Tone(blood, .6f), (crit ? 11f : 6.5f) * sc, .09f);

        // брызги цвета врага: летят по направлению удара, падают и оставляют пятно на земле
        int n = PN((crit ? 9 : 5) * (hc == 2 ? 1.5f : 1f));
        for (int i = 0; i < n && !PartsFull; i++)
        {
            float life = rng.RandfRange(.5f, .8f);
            Parts.Add(new Particle
            {
                P = hit + new Vector2(rng.RandfRange(-2, 2), rng.RandfRange(-2, 2)), V = d.Rotated(rng.RandfRange(-.75f, .75f)) * rng.RandfRange(40f, 115f) * sc + new Vector2(0, -rng.RandfRange(25f, 70f)),
                Life = life, Max = life, C = Tone(blood, rng.RandfRange(-.25f, .2f)), Size = rng.RandfRange(1.4f, 2.4f), Grav = 340f, Drag = 1.2f,
                Floor = e.Pos.Y + rng.RandfRange(1f, 8f), Kind = Particle.Drop,
            });
        }

        if (crit)
        {
            for (int i = 0; i < 7; i++)                                                             // линии удара веером от точки попадания
                hitLines.Add(new HitLine { P = body, D = d.Rotated(rng.RandfRange(-1.1f, 1.1f)), Life = rng.RandfRange(.12f, .18f), Len = rng.RandfRange(10f, 18f) * sc });
            Sparkle(body, new Color("#fff2b0"), 4, 8f * sc, 14f);
        }
    }

    /// <summary>Капля долетела до земли: пятно, которое медленно тает (не на воде).</summary>
    void AddSplat(Vector2 p, Color c, float size)
    {
        if (WaterAt(p)) return;
        hitSplats.Add(new HitSplat { P = p, C = c, Size = size * rng.RandfRange(2.2f, 3.2f), Rot = rng.Randf() * Mathf.Tau, Life = rng.RandfRange(2.2f, 3.2f) });
        if (hitSplats.Count > SplatCap) hitSplats.RemoveAt(0);
    }

    void UpdateHitFx(float dt)
    {
        for (int i = hitSlashes.Count - 1; i >= 0; i--) { hitSlashes[i].T += dt; if (hitSlashes[i].T >= hitSlashes[i].Life) hitSlashes.RemoveAt(i); }
        for (int i = hitWaves.Count - 1; i >= 0; i--) { hitWaves[i].T += dt; if (hitWaves[i].T >= hitWaves[i].Life) hitWaves.RemoveAt(i); }
        for (int i = hitSplats.Count - 1; i >= 0; i--) { hitSplats[i].T += dt; if (hitSplats[i].T >= hitSplats[i].Life) hitSplats.RemoveAt(i); }
        for (int i = hitLines.Count - 1; i >= 0; i--) { hitLines[i].T += dt; if (hitLines[i].T >= hitLines[i].Life) hitLines.RemoveAt(i); }
    }

    /// <summary>Слои у земли (под существами): пятна брызг и ударные волны.</summary>
    void DrawHitGround(EntityLayer c)
    {
        foreach (var s in hitSplats)
        {
            float a = Mathf.Clamp((s.Life - s.T) / 1f, 0, 1) * Mathf.Clamp(s.T / .06f, 0, 1) * .6f;
            float grow = .7f + .3f * Mathf.Clamp(s.T / .12f, 0, 1);
            c.DrawSetTransform(s.P, s.Rot, new Vector2(1f, .7f));                                     // пятно лежит на земле: сплюснуто по вертикали
            c.DrawTextureRect(Gfx.SplatTex, new Rect2(-s.Size * grow * .5f, -s.Size * grow * .5f, s.Size * grow, s.Size * grow), false, new Color(s.C.R * .8f, s.C.G * .8f, s.C.B * .8f, a));
        }
        c.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        foreach (var w in hitWaves)
        {
            float k = w.T / w.Life, e = 1 - Mathf.Pow(1 - k, 3), r = Mathf.Max(1f, w.R * e);
            var col = new Color(w.C.R, w.C.G, w.C.B, (1 - k) * .9f);
            c.DrawTextureRect(Gfx.RingTex, new Rect2(w.P - new Vector2(r, r * .55f), new Vector2(r * 2, r * 1.1f)), false, col);
        }
    }

    /// <summary>Слои поверх существ: серпы удара и линии удара.</summary>
    public void DrawHitTop(CanvasItem c)
    {
        foreach (var s in hitSlashes)
        {
            float k = s.T / s.Life, a = 1f - k * k;
            float grow = .65f + .55f * (1 - Mathf.Pow(1 - k, 2));                                       // серп раскрывается и тут же гаснет
            var p = s.P + s.Dir * (k * 7f - 5f);                                                       // и проходит сквозь врага по направлению удара
            float ang = s.Dir.Angle() + s.Tilt * (1 - k);
            float sz = s.Size * grow;
            c.DrawSetTransform(p, ang, Vector2.One);
            c.DrawTextureRect(Gfx.SlashTex, new Rect2(-sz * .6f, -sz * .55f, sz * 1.1f, sz * 1.1f), false, new Color(s.C.R * .6f, s.C.G * .45f, s.C.B * .3f, a * .5f));   // тёмная кайма — контраст на светлом
            c.DrawTextureRect(Gfx.SlashTex, new Rect2(-sz * .55f, -sz * .5f, sz, sz), false, new Color(s.C.R, s.C.G, s.C.B, a));
            c.DrawTextureRect(Gfx.SlashTex, new Rect2(-sz * .45f, -sz * .36f, sz * .8f, sz * .72f), false, new Color(1, 1, 1, a));
            c.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
            Gfx.Glow(c, p + s.Dir * sz * .35f, sz * .3f, new Color(s.C.R, s.C.G, s.C.B, a * .35f));
        }
        foreach (var l in hitLines)
        {
            float k = l.T / l.Life;
            var a = l.P + l.D * (5f + l.Len * k); var b = l.P + l.D * (5f + l.Len * (k + .55f));
            Gfx.Streak(c, a, b, new Color(1f, .97f, .85f, 1f - k), 1.4f);
        }
    }
}
