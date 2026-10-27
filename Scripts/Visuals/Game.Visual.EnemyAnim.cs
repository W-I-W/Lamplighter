using Godot;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Слой белых вспышек попадания: рисуется шейдером Assets/Shaders/Combat/flash_white.gdshader (аддитивный белый силуэт спрайта).</summary>
public partial class FlashLayer : Node2D
{
    public Game G;
    public override void _Draw() => G?.DrawEnemyFlash(this);
}

/// <summary>Выразительные атаки врагов: подготовка → атака → восстановление, следы рывков, частицы заклинаний, реакция на попадание и смерть.
/// Всё строится на уже существующих полях логики (Tele, Dash, Atk, Flash, State); тайминги и урон боя не менялись.
/// Логика сообщает только события (OnEnemyHit, OnEnemyDeath), остальное — этот визуальный компонент.</summary>
public partial class Game
{
    enum EStyle { None, Squash, Lunge, Shoot, Caster, Stomp, Bomb, Boss }

    static EStyle StyleOf(EK k) => k switch
    {
        EK.Shade or EK.Swarm or EK.Leech => EStyle.Squash,
        EK.Wraith or EK.Eel or EK.IceWolf or EK.Stalker => EStyle.Lunge,
        EK.Spitter => EStyle.Shoot,
        EK.Imp or EK.Spirit or EK.FrostPriest or EK.Mirage or EK.Weaver => EStyle.Caster,
        EK.Golem or EK.Beetle or EK.Sentinel => EStyle.Stomp,
        EK.Bomber => EStyle.Bomb,
        EK.Warden or EK.DepthWarden or EK.AshWarden or EK.WinterWarden or EK.Source => EStyle.Boss,
        _ => EStyle.None
    };

    static Color CasterColor(EK k) => k switch
    {
        EK.Imp => new Color("#ff9a2e"), EK.Spirit => new Color("#8ff0ff"), EK.FrostPriest => new Color("#bfe9ff"), EK.Mirage => new Color("#c080ff"), EK.Weaver => new Color("#b070ff"),
        EK.Warden => new Color("#ff7ad0"), EK.DepthWarden => new Color("#6ad0ff"), EK.AshWarden => new Color("#ff7a2e"), EK.WinterWarden => new Color("#d8f4ff"), EK.Source => new Color("#a050ff"), _ => new Color("#c0a0ff")
    };

    sealed class DeathAnim { public EK Kind; public Vector2 Pos; public int Face, Frame; public float Scale, T; public Color C; public EntityVisualDef Def; public bool Boss; }
    sealed class Spark { public Vector2 P, D; public float Len, T, Life; public Color C; }
    readonly List<DeathAnim> deaths = new(); readonly List<Spark> sparks = new();
    FlashLayer flashLayer; ShaderMaterial flashMat;

    void InitEnemyFx()
    {
        flashMat = GD.Load<ShaderMaterial>("res://Assets/Materials/flash_white.tres");
        flashLayer = new FlashLayer { G = this, Material = flashMat }; AddChild(flashLayer);
    }

    /// <summary>Профиль силы попадания: 0 — обычный враг, 1 — сильный, 2 — босс.</summary>
    static int HitClass(Enemy e) => e.IsBoss ? 2 : (e.Elite || e.Kind is EK.Golem or EK.Beetle or EK.Sentinel or EK.Weaver or EK.Stalker or EK.Mirage) ? 1 : 0;

    // ───────────────────────── события ─────────────────────────
    /// <summary>Событие логики: враг получил урон.</summary>
    public void OnEnemyHit(Enemy e, float dmg, Vector2 dir)
    {
        var cfg = VisualFxConfig.Get(); int hc = HitClass(e); bool crit = dmg >= .3f * e.MaxHp && !e.IsBoss;
        float mul = crit ? cfg.CritMul : 1f;
        e.VHit = 0; e.VHitDir = dir; e.VHitMul = mul;
        int n = Mathf.RoundToInt(cfg.HitParticles[hc] * mul * Settings.Particles);
        Impact(e.Pos + dir * -2f, dir, e.IsBoss ? new Color("#ffe9a0") : new Color("#fff0c0"), (crit ? 1.5f : 1f) * (hc == 2 ? 1.2f : 1f), n);
        Shake = Mathf.Max(Shake, 1.2f * cfg.HitCamShake[hc] * mul);
    }

    /// <summary>Событие логики: враг убит. Запускает анимацию смерти (враг уже убран из списка).</summary>
    public void OnEnemyDeath(Enemy e)
    {
        if (e.Kind == EK.Decoy) return;
        var vd = EnemyDef(e.Kind); if (vd.Sheet == null) return;
        float sc = (e.Elite ? 1.25f : 1f) * vd.Scale; var col = CasterColor(e.Kind);
        deaths.Add(new DeathAnim { Kind = e.Kind, Pos = e.Pos, Face = PPos.X < e.Pos.X ? -1 : 1, Frame = (int)(Time * vd.Fps + e.Phase) % vd.Frames, Scale = sc, Def = vd, C = SoulKind(e.Kind) ? new Color("#a060ff") : col, Boss = e.IsBoss });
        if (deaths.Count > 24) deaths.RemoveAt(0);
    }

    /// <summary>Пиксельный эффект удара: искры по направлению удара, крестообразная вспышка и частицы.</summary>
    public void Impact(Vector2 p, Vector2 dir, Color col, float strength, int particles)
    {
        if (Settings.Fx <= 0.01f) return;
        int rays = Mathf.Clamp((int)(4 * strength * Settings.Fx), 2, 9); var d0 = dir.LengthSquared() > .01f ? dir.Normalized() : Vector2.Right;
        for (int i = 0; i < rays; i++)
        {
            var d = d0.Rotated(rng.RandfRange(-.9f, .9f) * (i % 2 == 0 ? 1f : 1.6f));
            sparks.Add(new Spark { P = p, D = d, Len = rng.RandfRange(5f, 10f) * strength, Life = .16f, C = col });
        }
        sparks.Add(new Spark { P = p, D = Vector2.Zero, Len = 4f * strength, Life = .12f, C = Colors.White });     // вспышка-крест
        if (particles > 0) Burst(p, col, particles, 70f * strength);
        if (sparks.Count > 120) sparks.RemoveRange(0, sparks.Count - 120);
    }

    // ───────────────────────── обновление ─────────────────────────
    void UpdateEnemyVisuals(float dt)
    {
        var cfg = VisualFxConfig.Get();
        foreach (var e in Enemies)
        {
            e.VHit += dt; e.VAtk += dt;
            if (e.Tele > 0 && e.VPrevTele <= 0) e.VTele0 = e.Tele;
            bool attacked = e.Atk > e.VPrevAtk + .2f                                  // удар по игроку в ближнем бою
                || (e.VPrevTele > 0 && e.Tele <= 0 && e.Dash <= 0)                    // заклинание/выстрел в момент окончания подготовки
                || (e.Dash > 0 && e.VPrevDash <= 0);                                  // начало рывка
            if (attacked) e.VAtk = 0;
            e.VPrevAtk = e.Atk; e.VPrevTele = e.Tele; e.VPrevDash = e.Dash;
            var st = StyleOf(e.Kind);
            // след рывка: три последних положения
            if (st == EStyle.Lunge && e.Dash > 0) { e.VTrailT -= dt; if (e.VTrailT <= 0) { e.VTrailT = .035f; e.VTrail[2] = e.VTrail[1]; e.VTrail[1] = e.VTrail[0]; e.VTrail[0] = e.Pos; } }
            else if (e.VTrail[0] != Vector2.Zero && e.Dash <= 0 && e.VAtk > .25f) { e.VTrail[0] = e.VTrail[1] = e.VTrail[2] = Vector2.Zero; }
            // пиксельные частицы заклинателей и стрелков во время подготовки
            if (e.Tele > 0 && (st == EStyle.Caster || st == EStyle.Shoot) && Settings.Particles > .05f && rng.Randf() < dt * 40f * Settings.Particles)
            {
                float a = rng.Randf() * Mathf.Tau, r = rng.RandfRange(8f, 13f);
                Parts.Add(new Particle { P = e.Pos + Vector2.FromAngle(a) * r, V = -Vector2.FromAngle(a) * 30f + new Vector2(0, -8), Life = .35f, Max = .35f, C = CasterColor(e.Kind), Size = 1.5f });
            }
            if (st == EStyle.Bomb && e.State == 1 && rng.Randf() < dt * 25f) Parts.Add(new Particle { P = e.Pos + new Vector2(rng.RandfRange(-4, 4), -6), V = new Vector2(rng.RandfRange(-12, 12), -30), Life = .3f, Max = .3f, C = new Color("#ffb04a"), Size = 1.5f });
        }
        for (int i = deaths.Count - 1; i >= 0; i--)
        {
            var d = deaths[i]; bool first = d.T <= 0; d.T += dt;
            if (first && Settings.Particles > .05f) Burst(d.Pos, d.C, Mathf.RoundToInt((d.Boss ? 26 : 10) * Settings.Particles), d.Boss ? 110f : 70f);
            if (d.T > cfg.DeathTime * (d.Boss ? 2f : 1f)) deaths.RemoveAt(i);
        }
        for (int i = sparks.Count - 1; i >= 0; i--) { sparks[i].T += dt; if (sparks[i].T >= sparks[i].Life) sparks.RemoveAt(i); }
        flashLayer?.QueueRedraw();
    }

    // ───────────────────────── поза тела ─────────────────────────
    struct Pose { public Vector2 Off, Scale; public float Rot; }

    /// <summary>Поза спрайта врага для текущей стадии: подготовка (замах), атака (рывок, растяжение), восстановление (возврат формы) и встряска от попадания.</summary>
    Pose EnemyPose(Enemy e)
    {
        var p = new Pose { Scale = Vector2.One }; var st = StyleOf(e.Kind); var cfg = VisualFxConfig.Get();
        var to = PPos - e.Pos; var dir = to.LengthSquared() > .01f ? to.Normalized() : Vector2.Right;
        float prep = 0;
        if (e.Tele > 0 && e.VTele0 > .01f) prep = Mathf.Clamp(1f - e.Tele / e.VTele0, 0, 1);
        else if (st == EStyle.Bomb && e.State == 1) prep = Mathf.Clamp(1f - e.T1, 0, 1);
        else if (st == EStyle.Boss && (e.State == 1 || e.State == 4)) prep = .5f + .5f * Mathf.Sin(Time * 14f);
        float k = st == EStyle.Boss ? .55f : 1f;
        if (prep > 0)
        {
            float eased = prep * prep;
            switch (st)
            {
                case EStyle.Squash: p.Scale = new Vector2(1f + .30f * eased, 1f - .35f * eased); break;
                case EStyle.Lunge: p.Scale = new Vector2(1f + .12f * eased, 1f - .30f * eased); p.Off = -dir * 3.5f * eased; break;
                case EStyle.Shoot: p.Scale = new Vector2(1f - .08f * eased, 1f + .16f * eased); p.Off = -dir * 2.5f * eased; break;
                case EStyle.Caster: p.Scale = new Vector2(1f - .06f * eased + .05f * Mathf.Sin(Time * 40f), 1f + .12f * eased); p.Off = new Vector2(0, -3f * eased); break;
                case EStyle.Stomp: p.Scale = new Vector2(1f - .08f * eased, 1f + .22f * eased); p.Off = new Vector2(0, -2f * eased); break;
                case EStyle.Bomb: p.Scale = new Vector2(1f + .45f * eased, 1f + .45f * eased); p.Off = new Vector2(Mathf.Sin(Time * 50f) * .8f * eased, 0); break;
                case EStyle.Boss: p.Scale = new Vector2(1f + .05f * prep, 1f + .08f * prep); p.Off = new Vector2(0, -1.5f * prep); break;
            }
            p.Scale = Vector2.One + (p.Scale - Vector2.One) * k;
        }
        if (e.Dash > 0 || e.VAtk < .16f)
        {
            float u = e.Dash > 0 ? 1f : 1f - e.VAtk / .16f;
            switch (st)
            {
                case EStyle.Squash: p.Scale = new Vector2(1f - .2f * u, 1f + .35f * u); p.Off = dir * 4f * u; break;
                case EStyle.Lunge: p.Scale = new Vector2(1f + .45f * u, 1f - .25f * u); p.Off = dir * 3f * u; break;
                case EStyle.Shoot: p.Scale = new Vector2(1f + .15f * u, 1f - .1f * u); p.Off = -dir * 2.5f * u; break;
                case EStyle.Caster: p.Scale = new Vector2(1f + .1f * u, 1f + .2f * u); break;
                case EStyle.Stomp: p.Scale = new Vector2(1f + .22f * u, 1f - .3f * u); p.Off = dir * 2f * u; break;
                case EStyle.Boss: p.Scale = new Vector2(1f + .08f * u, 1f - .1f * u); break;
            }
        }
        else if (e.VAtk < .45f)
        {
            float u = 1f - (e.VAtk - .16f) / .29f; u *= u;
            switch (st)
            {
                case EStyle.Squash: p.Scale = new Vector2(1f + .2f * u, 1f - .25f * u); break;
                case EStyle.Lunge: p.Scale = new Vector2(1f + .1f * u, 1f - .12f * u); break;
                case EStyle.Stomp: p.Scale = new Vector2(1f + .12f * u, 1f - .18f * u); break;
                default: p.Scale = new Vector2(1f + .06f * u, 1f - .08f * u); break;
            }
        }
        float ft = cfg.HitFlashTime[HitClass(e)];
        if (e.VHit < .22f)
        {
            float d = 1f - e.VHit / .22f; float px = cfg.HitShakePxEnemy[HitClass(e)] * e.VHitMul * Settings.Fx;
            p.Off += e.VHitDir * px * d * d + new Vector2(Mathf.Sin(Time * 90f), Mathf.Cos(Time * 70f)) * px * .35f * d;
            if (e.VHit < ft) p.Scale = new Vector2(p.Scale.X * (1f - .06f * e.VHitMul), p.Scale.Y * (1f + .06f * e.VHitMul));
        }
        return p;
    }

    /// <summary>Общие параметры рисования врага для тела и слоя вспышки.</summary>
    bool EnemyDraw(Enemy e, out EntityVisualDef vd, out Vector2 at, out Vector2 scale, out int frame, out float fy)
    {
        vd = EnemyDef(e.Kind); at = Vector2.Zero; scale = Vector2.One; frame = 0; fy = 0;
        if (e.Hidden || vd.Sheet == null) return false;
        float sc = (e.Elite ? 1.25f : 1f) * vd.Scale; frame = (int)(Time * vd.Fps + e.Phase) % vd.Frames;
        float bob = Mathf.Sin(Time * 4 + e.Phase) * vd.Bob - vd.Lift; int face = e.Kind == EK.Beetle ? (e.Face.X < 0 ? -1 : 1) : (PPos.X < e.Pos.X ? -1 : 1);
        var pose = EnemyPose(e); fy = vd.Offset.Y + vd.FrameSize;                          // низ спрайта: растяжение и сжатие идут от «ног»
        at = e.Pos + new Vector2(0, bob + fy * sc) + pose.Off; scale = new Vector2(face * sc * pose.Scale.X, sc * pose.Scale.Y); return true;
    }

    public void DrawEnemyFlash(Node2D c)
    {
        if (Enemies == null) return; var cfg = VisualFxConfig.Get();
        foreach (var e in Enemies)
        {
            float ft = cfg.HitFlashTime[HitClass(e)]; bool fuse = e.Kind == EK.Bomber && e.State == 1 && ((int)(Time * 16) % 2 == 0);
            if (e.VHit >= ft && !fuse) continue;
            if (!EnemyDraw(e, out var vd, out var at, out var scale, out int f, out float fy)) continue;
            float a = e.VHit < ft ? (1f - e.VHit / ft * .6f) * Settings.Flash * .85f : .5f * Settings.Flash;
            c.DrawSetTransform(at, 0, scale);
            c.DrawTextureRectRegion(vd.Sheet, new Rect2(vd.Offset - new Vector2(0, fy), new Vector2(vd.FrameSize, vd.FrameSize)), new Rect2(f * vd.FrameSize, 0, vd.FrameSize, vd.FrameSize), new Color(1, 1, 1, a));
        }
        c.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }

    // ───────────────────────── смерть и искры ─────────────────────────
    void DrawDeath(EntityLayer c, DeathAnim d)
    {
        var cfg = VisualFxConfig.Get(); float life = cfg.DeathTime * (d.Boss ? 2f : 1f); float k = Mathf.Clamp(d.T / life, 0, 1); var vd = d.Def;
        float fy = vd.Offset.Y + vd.FrameSize; float sc = d.Scale;
        float step = Mathf.Floor(k * 6f) / 6f; float alpha = 1f - step;               // пиксельное растворение ступенями
        float sx = 1f + .25f * Mathf.Sin(k * Mathf.Pi) * (1f - k), sy = 1f - .65f * k * k;
        c.DrawSetTransform(d.Pos + new Vector2(0, fy * sc), 0, new Vector2(d.Face * sc * sx, sc * sy));
        var tint = k < .18f ? new Color(1.8f, 1.8f, 1.8f, alpha) : new Color(d.C.R * .5f + .5f, d.C.G * .5f + .5f, d.C.B * .5f + .5f, alpha);
        c.DrawTextureRectRegion(vd.Sheet, new Rect2(vd.Offset - new Vector2(0, fy), new Vector2(vd.FrameSize, vd.FrameSize)), new Rect2(d.Frame * vd.FrameSize, 0, vd.FrameSize, vd.FrameSize), tint);
        c.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        if (d.Boss && k < .9f && ((int)(Time * 20) % 3 == 0)) Gfx.Ring(c, d.Pos, 8 + 40 * k, 24, new Color(d.C.R, d.C.G, d.C.B, (1f - k) * .6f), 1f);
    }

    void DrawSparks(EntityLayer c)
    {
        foreach (var s in sparks)
        {
            float k = s.T / s.Life; var col = new Color(s.C.R, s.C.G, s.C.B, 1f - k * .8f);
            if (s.D == Vector2.Zero) { float l = s.Len * (1f - k * .5f); Gfx.Line(c, s.P + new Vector2(-l, 0), s.P + new Vector2(l, 0), col, 1f); Gfx.Line(c, s.P + new Vector2(0, -l), s.P + new Vector2(0, l), col, 1f); }
            else { var a = s.P + s.D * s.Len * k * .8f; var b = s.P + s.D * s.Len * (.3f + k); Gfx.Line(c, a, b, col, 1f); }
        }
    }

    /// <summary>След рывка: затухающие силуэты предыдущих положений.</summary>
    void DrawTrail(EntityLayer c, Enemy e)
    {
        var vd = EnemyDef(e.Kind); if (vd.Sheet == null) return; float sc = (e.Elite ? 1.25f : 1f) * vd.Scale; int face = PPos.X < e.Pos.X ? -1 : 1; float fy = vd.Offset.Y + vd.FrameSize;
        for (int i = 2; i >= 0; i--)
        {
            var p = e.VTrail[i]; if (p == Vector2.Zero) continue;
            var col = CasterColor(e.Kind); float a = .34f - .1f * i;
            c.DrawSetTransform(p + new Vector2(0, fy * sc), 0, new Vector2(face * sc * 1.2f, sc * .9f));
            c.DrawTextureRectRegion(vd.Sheet, new Rect2(vd.Offset - new Vector2(0, fy), new Vector2(vd.FrameSize, vd.FrameSize)), new Rect2(0, 0, vd.FrameSize, vd.FrameSize), new Color(col.R * .6f + .4f, col.G * .6f + .4f, col.B * .6f + .4f, a));
        }
        c.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }

    /// <summary>Направление выстрела: штриховая линия из точек на время подготовки.</summary>
    void DrawAim(EntityLayer c, Enemy e)
    {
        if (e.Tele <= 0 || StyleOf(e.Kind) != EStyle.Shoot) return;
        var d = (PPos - e.Pos).Normalized(); float k = e.VTele0 > .01f ? 1f - e.Tele / e.VTele0 : 0;
        for (int i = 0; i < 6; i++) { if (i * 7 > 40 + 30 * k) break; var a = e.Pos + d * (10 + i * 7); Gfx.Rect(c, new Rect2(a - new Vector2(.5f, .5f), new Vector2(2, 2)), new Color(1f, .6f, .35f, .25f + .5f * k)); }
    }
}
