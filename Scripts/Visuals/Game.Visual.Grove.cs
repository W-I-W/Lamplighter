using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Отрисовка Пожирателя Рощи: дерево (лист кадров 128×128 — покой, корни, споры, ветвь, удар, переход фазы, гибель), ядро поверх ствола,
/// корни из земли, удар ветвью, споры, «Голод Рощи», линии поглощения, заражённая и ожившая земля, листья и кора. Всё — PNG из Assets/Sprites/Bosses (tools/GraphicsGenerator/make_grove_devourer.py).</summary>
public partial class Game
{
    const string GroveDir = "res://Assets/Sprites/Bosses/";
    static Texture2D groveTex, groveCoreTex, groveRootTex, groveSporeTex, groveGroundTex, groveBitsTex, groveBranchTex, groveNodeTex;
    static Texture2D GT(ref Texture2D t, string n) => t ??= GD.Load<Texture2D>(GroveDir + n + ".png");
    public static readonly string[] GroveSprites = { "grove_devourer", "grove_core", "grove_root", "grove_spore", "grove_ground", "grove_bits", "grove_branch", "grove_node" };

    public const int GroveFrame = 128, GroveFrames = 21;              // кадры: 0–3 покой, 4–6 корни, 7–9 споры, 10–12 ветвь, 13 удар, 14–16 переход фазы, 17–20 гибель
    static readonly Vector2 GroveAnchor = new(64, 120);               // точка листа, стоящая на месте ствола (Enemy.Pos)
    public static readonly Vector2 GroveCoreOff = new(0, -54);        // ядро относительно Enemy.Pos

    /// <summary>Кадр листа по состоянию босса (то же у гостя: состояние приходит в снимке).</summary>
    public int GroveFrameOf(Enemy e)
    {
        int Loop(int a, int n, float fps) => a + (int)(Time * fps + e.Phase) % n;
        if (e.State == 9) return Loop(14, 3, 6f);
        if (e.VHit < .12f || e.Flash > .1f) return 13;
        return e.State switch { 1 or 5 => Loop(4, 3, 6f), 4 => Loop(4, 3, 3f), 2 => Loop(10, 3, 7f), 3 => Loop(7, 3, 6f), 7 => 13, _ => Loop(0, 4, 3f) };
    }

    void DrawGrove(EntityLayer c, Enemy e)
    {
        var tex = GT(ref groveTex, "grove_devourer"); if (tex == null) return;
        c.DrawSetTransform(e.Pos + new Vector2(0, 4), 0, new Vector2(1, .32f)); Gfx.Disc(c, Vector2.Zero, 46, new Color(0, 0, 0, .35f)); c.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        int f = GroveFrameOf(e);
        var shake = e.State == 9 ? new Vector2(Mathf.Round(Mathf.Sin(Time * 47f) * 1.5f), 0) : Vector2.Zero;
        Color mod = e.VHit < .09f ? new Color(1.3f, 1.2f, 1.2f) : Colors.White;
        if (e.Shield) mod *= new Color(.78f, .8f, .95f);
        if (e.T3 > 0) mod *= new Color(1.15f + .15f * Mathf.Sin(Time * 9f), .85f, .85f);
        var at = (e.Pos - GroveAnchor + shake).Round();
        c.DrawTextureRectRegion(tex, new Rect2(at, new Vector2(GroveFrame, GroveFrame)), new Rect2(f * GroveFrame, 0, GroveFrame, GroveFrame), mod);
        // ядро: закрыто корой (Сердце Тьмы), тлеет, открыто (уязвимо) или тянет силу
        var core = GT(ref groveCoreTex, "grove_core"); var cp = e.Pos + GroveCoreOff + shake;
        int cf = e.Shield ? 0 : (e.Over > 0 || e.State == 4 || e.State == 6 || e.State == 7) ? 2 : 1;
        Color cm = e.State == 4 ? new Color(.8f, 1.3f, .7f) : e.Over > 0 ? new Color(1.2f + .3f * Mathf.Sin(Time * 12f), 1.15f, .9f) : Colors.White;
        if (core != null) c.DrawTextureRectRegion(core, new Rect2((cp - new Vector2(12, 12)).Round(), new Vector2(24, 24)), new Rect2(cf * 24, 0, 24, 24), cm);
        if (e.Over > 0 && e.State != 4) Gfx.Ring(c, cp, 14 + 2 * Mathf.Sin(Time * 10f), 20, new Color(1f, .9f, .4f, .8f), 1.2f);
        if (e.Shield) Gfx.Ring(c, cp, 15, 20, new Color(.7f, .75f, 1f, .35f + .15f * Mathf.Sin(Time * 4f)), 1f);
    }

    void DrawGroveDeath(EntityLayer c, GroveDeathFx d)
    {
        var tex = GT(ref groveTex, "grove_devourer"); if (tex == null) return; float t = d.T;
        if (t > 3.4f) return;
        int f = 17 + Math.Min(3, (int)(t / 1.6f * 4));
        float a = t < 1.8f ? 1f : 1f - Mathf.Floor((t - 1.8f) / 1.6f * 6f) / 6f;             // пиксельное растворение ступенями
        var shake = t < 1.5f ? new Vector2(Mathf.Round(Mathf.Sin(t * 60f) * (1 + t)), 0) : Vector2.Zero;
        c.DrawTextureRectRegion(tex, new Rect2((d.Pos - GroveAnchor + shake).Round(), new Vector2(GroveFrame, GroveFrame)), new Rect2(f * GroveFrame, 0, GroveFrame, GroveFrame), new Color(1, 1, 1, a));
        var core = GT(ref groveCoreTex, "grove_core"); var cp = d.Pos + GroveCoreOff + shake;
        if (core != null && t < 1.5f) { bool blink = (int)(t * (6 + t * 10)) % 2 == 0; c.DrawTextureRectRegion(core, new Rect2((cp - new Vector2(12, 12)).Round(), new Vector2(24, 24)), new Rect2((blink ? 3 : 2) * 24, 0, 24, 24), blink ? new Color(1.6f, 1.5f, 1.2f) : Colors.White); }
        if (t > 1.3f && t < 2.4f) { float k = (t - 1.3f) / 1.1f; Gfx.Ring(c, cp, 10 + 160 * k, 48, new Color(1f, .95f, .7f, .8f * (1 - k)), 2f); }
    }

    /// <summary>Земля арены (под сущностями): заражённые пятна во время боя, ожившая трава после победы; предупреждения корней, ветви, спор и «Голода»; линии поглощения.</summary>
    void DrawGroveGround(EntityLayer c)
    {
        var ar = W.ArenaOf(1);
        if (ar != null && ar.Pos.DistanceTo(PPos) < 420 && (GroveInfect > .01f || GroveDeath != null))
        {
            var gt = GT(ref groveGroundTex, "grove_ground"); float R = (ar.Radius + .5f) * TS;
            float revive = GroveDeath == null ? -1 : Mathf.Clamp((GroveDeath.T - 1.6f) / 2.2f, 0, 1) * (R + 16);
            float ra = GroveDeath == null ? 0 : Mathf.Clamp((9f - GroveDeath.T) / 3f, 0, 1);
            for (int y = -ar.Radius; y <= ar.Radius; y++)
                for (int x = -ar.Radius; x <= ar.Radius; x++)
                {
                    uint h = (uint)((ar.Center.X + x) * 73856093) ^ (uint)((ar.Center.Y + y) * 19349663); h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                    if (h % 100 >= 46) continue;
                    var p = ar.Pos + new Vector2(x * TS + (int)(h >> 8) % 7 - 3, y * TS + (int)(h >> 12) % 7 - 3); float d = p.DistanceTo(ar.Pos);
                    if (d > R - 4) continue;
                    float edge = (int)(h >> 16) % 24 - 12;
                    if (revive >= 0 && d < revive) { if (ra > 0 && h % 3 != 0) c.DrawTextureRectRegion(gt, new Rect2((p - new Vector2(16, 16)).Round(), new Vector2(32, 32)), new Rect2(96, 0, 32, 32), new Color(1, 1, 1, ra)); continue; }
                    if (d < GroveInfect * R + edge) c.DrawTextureRectRegion(gt, new Rect2((p - new Vector2(16, 16)).Round(), new Vector2(32, 32)), new Rect2((int)(h % 3) * 32, 0, 32, 32), new Color(1, 1, 1, .85f));
                }
        }
        foreach (var r in GroveRoots) if (r.T < r.Warn)
        {
            float k = r.T / Mathf.Max(.01f, r.Warn); var rt = GT(ref groveRootTex, "grove_root");
            c.DrawTextureRectRegion(rt, new Rect2((r.Pos - new Vector2(12, 36)).Round(), new Vector2(24, 40)), new Rect2(0, 0, 24, 40), new Color(1, 1, 1, .35f + .65f * k));
            Gfx.Ring(c, r.Pos, r.R, 20, new Color(.75f, .45f, 1f, .45f + .45f * k), 1.2f); Gfx.Ring(c, r.Pos, r.R * (1 - k), 16, new Color(.9f, 1f, .6f, .6f), 1f);
        }
        foreach (var s in GroveSweeps)
        {
            if (s.T < s.Warn)
            {
                float k = s.T / Mathf.Max(.01f, s.Warn);
                Sector(c, s.C, s.R0, s.R1, s.Ang - s.Half, s.Ang + s.Half, new Color(.55f, .2f, .75f, .14f + .18f * k + .06f * Mathf.Sin(Time * 18f)));
                Gfx.Arc(c, s.C, s.R1, s.Ang - s.Half, s.Ang + s.Half, 24, new Color(.85f, .55f, 1f, .8f), 1.5f);
                Gfx.Arc(c, s.C, s.R0 + (s.R1 - s.R0) * k, s.Ang - s.Half, s.Ang + s.Half, 20, new Color(1f, .95f, .7f, .6f), 1f);
                Gfx.Line(c, s.C + Vector2.FromAngle(s.Ang - s.Half) * s.R0, s.C + Vector2.FromAngle(s.Ang - s.Half) * s.R1, new Color(.85f, .55f, 1f, .7f), 1f);
                Gfx.Line(c, s.C + Vector2.FromAngle(s.Ang + s.Half) * s.R0, s.C + Vector2.FromAngle(s.Ang + s.Half) * s.R1, new Color(.85f, .55f, 1f, .7f), 1f);
            }
        }
        foreach (var s in GroveSpores)
        {
            if (s.T < s.Warn) { float k = s.T / Mathf.Max(.01f, s.Warn); Gfx.Disc(c, s.Pos, s.R, new Color(.45f, .7f, .2f, .12f + .1f * Mathf.Sin(Time * 16f))); Gfx.Ring(c, s.Pos, s.R, 24, new Color(.7f, 1f, .35f, .8f), 1.3f); Gfx.Ring(c, s.Pos, s.R * (1 - k), 20, new Color(.95f, 1f, .7f, .6f), 1f); continue; }
            float fade = Mathf.Min(1f, (s.Warn + s.Dur - s.T) * 2f) * Mathf.Min(1f, (s.T - s.Warn) * 4f); var st = GT(ref groveSporeTex, "grove_spore");
            for (int i = 0; i < 3; i++)
            {
                float a = Time * (.6f + i * .2f) + i * 2.1f; var o = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * .6f) * s.R * .35f;
                int fr = (int)(Time * 4 + i) % 4; float sc = s.R / 16f * (i == 0 ? 1.1f : .8f);
                c.DrawTextureRectRegion(st, new Rect2((s.Pos + o - new Vector2(16, 16) * sc).Round(), new Vector2(32, 32) * sc), new Rect2(fr * 32, 0, 32, 32), new Color(1, 1, 1, .8f * fade));
            }
        }
        foreach (var h in GroveHungers)
        {
            float k = Mathf.Clamp(h.T / Mathf.Max(.01f, h.Warn), 0, 1); bool fired = h.T >= h.Warn; float step = Mathf.Tau / h.N;
            for (int i = 0; i < h.N; i++)
            {
                float a0 = h.Ang0 + i * step, a1 = a0 + step; bool safe = (h.Safe & (1 << i)) != 0;
                if (safe) { if (!fired) { Sector(c, h.C, 24, h.R, a0, a1, new Color(1f, .95f, .6f, .07f + .05f * k)); Gfx.Arc(c, h.C, h.R - 6, a0 + .05f, a1 - .05f, 10, new Color(1f, .95f, .6f, .7f), 1.5f); } continue; }
                if (!fired) { Sector(c, h.C, 24, h.R, a0, a1, new Color(.4f, .05f, .55f, .16f + .26f * k + .08f * Mathf.Sin(Time * 20f))); Gfx.Arc(c, h.C, (h.R - 24) * k + 24, a0, a1, 10, new Color(.9f, .5f, 1f, .7f), 1.2f); }
                else { float fa = 1f - (h.T - h.Warn) / .6f; Sector(c, h.C, 24, h.R, a0, a1, new Color(.85f, .3f, 1f, .5f * fa)); }
            }
        }
        if (GroveNodes.Length > 0)
        {
            var boss = Enemies.Find(x => x.Kind == EK.Warden); var nt = GT(ref groveNodeTex, "grove_node");
            var col = GroveAbsorbBuff ? new Color(1f, .45f, .3f) : new Color(.6f, 1f, .35f);
            foreach (var n in GroveNodes)
            {
                c.DrawTextureRectRegion(nt, new Rect2((n - new Vector2(8, 14)).Round(), new Vector2(16, 16)), new Rect2(((int)(Time * 4) % 2) * 16, 0, 16, 16), Colors.White);
                if (boss == null) continue; var to = boss.Pos + GroveCoreOff;
                Gfx.Line(c, n, to, new Color(col.R, col.G, col.B, .35f), 1f);
                for (int i = 0; i < 6; i++) { float q = (Time * .9f + i / 6f) % 1f; Gfx.Rect(c, new Rect2((n.Lerp(to, q) - Vector2.One).Round(), new Vector2(2, 2)), new Color(col.R, col.G, col.B, .9f)); }
            }
        }
    }

    /// <summary>Свет боя с Пожирателем: тусклое свечение заражённой рощи и ядра (видно дерево и землю арены), подсветка предупреждений корней, спор и «Голода», вспышка гибели.</summary>
    public IEnumerable<Vector4> GroveLights()
    {
        var boss = Enemies.Find(x => x.Kind == EK.Warden && x.Hp > 0); int n = 0;
        if (boss != null && boss.Pos.DistanceTo(CamCenter) < 600)
        {
            float k = .9f + .1f * Mathf.Sin(Time * 3f);
            yield return new Vector4(boss.Pos.X, boss.Pos.Y - 40, 120f * k + (boss.Over > 0 ? 30f : 0f), 0);
            if (GroveHungers.Count > 0) yield return new Vector4(boss.Pos.X, boss.Pos.Y, (GroveArenaR(boss) + 30f), 0);
        }
        foreach (var r in GroveRoots) if (r.T < r.Warn + .4f) { yield return new Vector4(r.Pos.X, r.Pos.Y, 26f, 0); if (++n > 10) break; }
        foreach (var s in GroveSpores) { yield return new Vector4(s.Pos.X, s.Pos.Y, s.R * 1.8f, 0); if (++n > 16) break; }
        foreach (var p in GroveNodes) yield return new Vector4(p.X, p.Y, 30f, 0);
        if (GroveDeath != null && GroveDeath.T < 6f) yield return new Vector4(GroveDeath.Pos.X, GroveDeath.Pos.Y - 40, 60f + Mathf.Min(GroveDeath.T, 3f) * 60f, GroveDeath.T > 1.4f && GroveDeath.T < 2.2f ? 1f : 0f);
    }

    /// <summary>Залитый сектор кольца r0..r1 от угла a0 до a1.</summary>
    static void Sector(CanvasItem c, Vector2 o, float r0, float r1, float a0, float a1, Color col)
    {
        int n = Math.Max(3, (int)((a1 - a0) / .12f)); var pts = new Vector2[(n + 1) * 2];
        for (int i = 0; i <= n; i++) { float a = a0 + (a1 - a0) * i / n; pts[i] = o + Vector2.FromAngle(a) * r1; pts[(n + 1) * 2 - 1 - i] = o + Vector2.FromAngle(a) * r0; }
        c.DrawColoredPolygon(pts, col);
    }

    /// <summary>Корни, вышедшие из земли, и ветвь в момент удара — по глубине вместе с сущностями.</summary>
    void AddGroveDraws(List<(float y, Action a)> list, EntityLayer c)
    {
        foreach (var r in GroveRoots) if (r.T >= r.Warn) { var rr = r; list.Add((r.Pos.Y, () => DrawGroveRoot(c, rr))); }
        foreach (var s in GroveSweeps) if (s.T >= s.Warn) { var ss = s; list.Add((s.C.Y + 2, () => DrawGroveBranch(c, ss))); }
        if (GroveDeath != null) { var d = GroveDeath; list.Add((d.Pos.Y, () => DrawGroveDeath(c, d))); }
    }

    void DrawGroveRoot(EntityLayer c, GroveRoot r)
    {
        float t = r.T - r.Warn; int f = t < .24f ? 1 + (int)(t / .08f) : t < .5f ? 4 : 5; float a = t > .5f ? Mathf.Clamp(1f - (t - .5f) / .25f, 0, 1) : 1f;
        c.DrawTextureRectRegion(GT(ref groveRootTex, "grove_root"), new Rect2((r.Pos - new Vector2(12, 36)).Round(), new Vector2(24, 40)), new Rect2(Math.Min(f, 5) * 24, 0, 24, 40), new Color(1, 1, 1, a));
    }

    void DrawGroveBranch(EntityLayer c, GroveSweep s)
    {
        float k = Mathf.Clamp((s.T - s.Warn) / .22f, 0, 1), fade = Mathf.Clamp(1f - (s.T - s.Warn - .22f) / .18f, 0, 1);
        float ang = s.Ang - s.Half + 2 * s.Half * k;
        var bt = GT(ref groveBranchTex, "grove_branch");
        c.DrawSetTransform(s.C, ang, new Vector2(s.R1 / 104f, 1f));
        c.DrawTextureRect(bt, new Rect2(0, -12, 104, 24), false, new Color(1, 1, 1, fade));
        c.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        if (k < 1) Gfx.Arc(c, s.C, s.R1 * .9f, s.Ang - s.Half, ang, 16, new Color(.85f, .6f, 1f, .5f * fade), 3f);
    }

    /// <summary>Листья, кора, споры и ростки — поверх сущностей.</summary>
    void DrawGroveBits(EntityLayer c)
    {
        if (GroveBits.Count == 0) return; var bt = GT(ref groveBitsTex, "grove_bits");
        foreach (var b in GroveBits)
        {
            int f = b.Kind * 4 + b.Frame % (b.Kind >= 2 ? 2 : 4) + (b.Kind == 3 ? 2 : 0); float a = Mathf.Clamp(b.Life / b.Max * 2f, 0, 1);
            if (b.Kind == 2) f = 8 + b.Frame % 2; else if (b.Kind == 3) f = 10 + b.Frame % 2;
            c.DrawSetTransform(b.P.Round(), b.Kind <= 1 ? b.Rot : 0, Vector2.One);
            c.DrawTextureRectRegion(bt, new Rect2(-4, -4, 8, 8), new Rect2(f * 8, 0, 8, 8), new Color(1, 1, 1, a));
        }
        c.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }
}
