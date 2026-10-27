using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Ледяной монастырь: холод, скользкий лёд, ледяные существа и Хранитель Зимы.</summary>
public partial class Game
{
    public float ChillT;
    public HashSet<int> LitRec = new();
    public List<(Vector2I tile, float t, float life)> PendingBlocks = new();   // ледяные глыбы: предупреждение → появление
    public List<(Vector2I tile, float t)> TempBlocks = new();                  // существующие глыбы с временем жизни
    float prismT;

    public bool OnIce(Vector2 p)
    {
        var t = ToTile(p); if (!World.In(t.X, t.Y)) return false;
        int g = W.Ground[World.Key(t.X, t.Y)]; return g == Art.IceFloor || g == Art.IceFloor + 1;
    }

    public void Chill(float t)
    {
        if (Has(Tech.FrostArmor)) t *= .4f;
        ChillT = Mathf.Max(ChillT, t);
    }

    // ───────────────────────── призмы арены ─────────────────────────
    public int ArenaPrismsLit() { int n = TestPrisms; foreach (var p in W.ArenaPrisms) if (LitRec.Contains(World.Key(p.X, p.Y))) n++; return n; }

    void UpdateArenaPrisms()
    {
        foreach (var p in W.ArenaPrisms)
        {
            bool lit = LitRec.Contains(World.Key(p.X, p.Y)); var cur = W.Get(p.X, p.Y);
            if (lit && cur == Tl.Receiver) SetObj(p.X, p.Y, Tl.ReceiverOn); else if (!lit && cur == Tl.ReceiverOn) SetObj(p.X, p.Y, Tl.Receiver);
        }
    }

    void UpdateIceBlocks(float dt)
    {
        for (int i = PendingBlocks.Count - 1; i >= 0; i--)
        {
            var b = PendingBlocks[i]; b = (b.tile, b.t - dt, b.life); PendingBlocks[i] = b;
            if (b.t > 0) continue;
            PendingBlocks.RemoveAt(i);
            if (W.Get(b.tile.X, b.tile.Y) == Tl.None && ToTile(PPos) != b.tile)
            {
                SetObj(b.tile.X, b.tile.Y, Tl.IceBlock); TempBlocks.Add((b.tile, b.life));
                Burst(TileCenter(b.tile.X, b.tile.Y), new Color("#bfe9ff"), 8, 60); Sfx.Play("tick", -2, 1.8f);
            }
        }
        for (int i = TempBlocks.Count - 1; i >= 0; i--)
        {
            var b = TempBlocks[i]; b = (b.tile, b.t - dt); TempBlocks[i] = b;
            if (W.Get(b.tile.X, b.tile.Y) != Tl.IceBlock) { TempBlocks.RemoveAt(i); continue; }
            if (b.t <= 0) { SetObj(b.tile.X, b.tile.Y, Tl.None); Burst(TileCenter(b.tile.X, b.tile.Y), new Color("#e8faff"), 8, 50); TempBlocks.RemoveAt(i); }
        }
    }

    void ClearIceBlocks()
    {
        foreach (var b in TempBlocks) if (W.Get(b.tile.X, b.tile.Y) == Tl.IceBlock) SetObj(b.tile.X, b.tile.Y, Tl.None);
        TempBlocks.Clear(); PendingBlocks.Clear();
    }

    // ───────────────────────── ледяные существа ─────────────────────────
    Vector2 UpdateIceEnemy(Enemy e, float dt, Vector2 dir, float dist, Vector2 perp, float k)
    {
        Vector2 vel = Vector2.Zero;
        switch (e.Kind)
        {
            case EK.IceWolf:
                e.Cd -= dt;
                if (e.Tele > 0) { e.Tele -= dt; if (e.Tele <= 0) { e.Dash = .3f; e.DashDir = dir; } }
                else if (e.Dash > 0)
                {
                    e.Dash -= dt; vel = e.DashDir * 165f * k;
                    if (dist < 12 && e.Atk <= 0) { e.Atk = .9f; HurtPlayer(8f * e.Scale, -dir); Chill(2f); }
                }
                else
                {
                    vel = (dir + perp * Mathf.Sin(Time * 2.5f + e.Phase) * .6f).Normalized() * 60f * k;
                    if (e.Cd <= 0 && dist < 85) { e.Tele = .45f; e.Cd = rng.RandfRange(2.2f, 3.2f); }
                }
                break;
            case EK.FrostPriest:
                e.Cd -= dt;
                if (e.Tele > 0)
                {
                    e.Tele -= dt;
                    if (e.Tele <= 0) { AddZone(e.Mark, 22, 1.1f, 3.5f, 5f, false, false, true); Sfx.Play("flare", -5, .7f); e.Cd = rng.RandfRange(3f, 4.2f); }
                }
                else
                {
                    if (dist > 105) vel = dir * 38f; else if (dist < 70) vel = -dir * 38f; else vel = perp * Mathf.Sin(Time * 1.4f + e.Phase) * 18f;
                    vel *= k;
                    if (e.Cd <= 0 && dist < 160) { e.Tele = .5f; e.Mark = PPos; }
                }
                break;
            case EK.Sentinel:
                e.Cd -= dt;
                if (e.State == 0)
                {
                    vel = dir * 26f * k; if (dist < 38 && e.Cd <= 0) { e.State = 1; e.T1 = .9f; }
                }
                else
                {
                    e.T1 -= dt;
                    if (e.T1 <= 0)
                    {
                        e.State = 0; e.Cd = 2.4f; Shake = Mathf.Max(Shake, 3); Sfx.Play("boom", -4, 1.2f);
                        for (int i = 0; i < 16; i++) { float a = i * Mathf.Tau / 16; Parts.Add(new Particle { P = e.Pos, V = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 90, Life = .4f, Max = .4f, C = new Color("#bfe9ff"), Size = 2 }); }
                        if (dist < 44) { HurtPlayer(14f * e.Scale, -dir); Chill(2.5f); }
                    }
                }
                break;
            case EK.WinterWarden: vel = UpdateWinter(e, dt, dir, dist); break;
        }
        return vel;
    }

    Vector2 dirTo(Enemy e) => (PPos - e.Pos).Normalized();

    // ───────────────────────── Хранитель Зимы ─────────────────────────
    float reflectT;
    void UpdateReflect(float dt) { reflectT = Mathf.Max(0, reflectT - dt); }

    Vector2 UpdateWinter(Enemy e, float dt, Vector2 dir, float dist)
    {
        float hpf = e.Hp / e.MaxHp; int ph = hpf > .66f ? 1 : hpf > .33f ? 2 : 3; float mul = ph == 3 ? .65f : 1f;
        if ((int)e.Mark.X != ph)
        {
            e.Mark = new Vector2(ph, 0);
            if (ph == 2) Announce("Хранитель Зимы поднимает зеркальный щит!", "Зажгите призмы лучами: направьте свет на две из четырёх", new Color("#bfe9ff"), "boss");
            if (ph == 3) { Announce("Метель набирает силу!", "Теперь нужны три призмы. Лёд под ногами скользит", new Color("#e8faff"), "boss"); Shake = 5; }
        }
        int need = ph == 2 ? 2 : 3;
        if (ph == 1) { e.Shield = false; e.VulnT = 0; }
        else
        {
            if (e.VulnT > 0) { e.VulnT -= dt; if (e.VulnT <= 0) { e.Shield = true; Toast("Зеркальный щит восстановлен"); } }
            else if (!e.Shield && ph >= 2) e.Shield = true;
            if (e.Shield && ArenaPrismsLit() >= need) { e.Shield = false; e.VulnT = 6f; Announce("Щит разбит!", "Хранитель уязвим — бейте!", new Color("#ffe27a"), "ancient"); Burst(e.Pos, new Color("#ffffff"), 30, 150); Shake = 4; }
        }
        Vector2 vel = Vector2.Zero;
        switch (e.State)
        {
            case 0:
                {
                    var home = e.Home ?? e.Pos; var toHome = home - e.Pos;
                    vel = toHome.Length() > 11 * TS ? toHome.Normalized() * 30f : dir * (ph == 3 ? 36f : 28f);
                    e.T1 -= dt;
                    if (dist < 22 && e.Atk <= 0) { e.Atk = 1f; HurtPlayer(12f, -dir); Chill(2f); }
                    if (e.T1 <= 0)
                    {
                        float r = rng.Randf();
                        if (ph == 1) { if (r < .4f) { e.State = 1; e.T2 = .6f; } else if (r < .7f) { e.State = 2; e.T2 = .8f; } else { e.State = 3; e.T2 = .9f; e.DashDir = dir; } }
                        else if (ph == 2) { if (r < .3f) { e.State = 1; e.T2 = .6f; } else if (r < .55f) { e.State = 4; e.T2 = .7f; } else if (r < .75f) { e.State = 2; e.T2 = .8f; } else { e.State = 3; e.T2 = .9f; e.DashDir = dir; } }
                        else { if (r < .35f) { e.State = 4; e.T2 = .6f; } else if (r < .6f) { e.State = 1; e.T2 = .5f; } else if (r < .8f) { e.State = 3; e.T2 = .8f; e.DashDir = dir; } else { e.State = 2; e.T2 = .7f; } }
                    }
                    break;
                }
            case 1:     // ледяные глыбы на линиях света призм
                e.T2 -= dt;
                if (e.T2 <= 0)
                {
                    var prisms = new List<Vector2I>(W.ArenaPrisms); int n = ph == 3 ? 3 : 2;
                    for (int i = 0; i < n && prisms.Count > 0; i++)
                    {
                        int k = rng.RandiRange(0, prisms.Count - 1); var p = prisms[k]; prisms.RemoveAt(k);
                        var c = W.IceArena; var away = new Vector2I(Math.Sign(p.X - c.X), Math.Sign(p.Y - c.Y));
                        // линия луча идёт от эмиттера (дальше от центра) к призме: ставим глыбы за призмой и по линии
                        var side = away.X != 0 ? new Vector2I(0, 1) : new Vector2I(1, 0);
                        foreach (var off in new[] { 2, 4 }) foreach (var sg in new[] { 1, -1 })
                        {
                            var bt = p + side * (off * sg);
                            if (World.In(bt.X, bt.Y) && W.Get(bt.X, bt.Y) == Tl.None && W.Ground[World.Key(bt.X, bt.Y)] != Art.VoidG) { PendingBlocks.Add((bt, 1.1f, ph == 3 ? 20f : 14f)); break; }
                        }
                    }
                    Sfx.Play("flare", -4, .6f); e.State = 0; e.T1 = 2.4f * mul;
                }
                break;
            case 2:     // призыв ледяных существ
                e.T2 -= dt;
                if (e.T2 <= 0)
                {
                    for (int i = 0; i < (ph == 3 ? 3 : 2); i++) { var m = SpawnEnemy(e.Pos + new Vector2(rng.RandfRange(-26, 26), rng.RandfRange(-26, 26)), i == 0 && ph >= 2 ? EK.FrostPriest : EK.IceWolf, null, 1.2f); m.Home = e.Home; }
                    Burst(e.Pos, new Color("#bfe9ff"), 16, 90); Sfx.Play("boss", -4, 1.4f); e.State = 0; e.T1 = 3f * mul;
                }
                break;
            case 3:     // веер осколков
                e.T2 -= dt; e.DashDir = dir;
                if (e.T2 <= 0)
                {
                    int n = ph == 3 ? 7 : 5;
                    for (int i = 0; i < n; i++) { var d = dir.Rotated((i - (n - 1) / 2f) * .22f); Projs.Add(new Proj { Pos = e.Pos, Vel = d * 95f, Dmg = 7f, C = new Color("#9fe8ff"), Cold = true }); }
                    Sfx.Play("shoot", -2, 1.5f); e.State = 0; e.T1 = 2.2f * mul;
                }
                break;
            case 4:     // замораживание участков арены
                e.T2 -= dt;
                if (e.T2 <= 0)
                {
                    int n = ph == 3 ? 4 : 3;
                    for (int i = 0; i < n; i++) AddZone(PPos + Vector2.FromAngle(i * Mathf.Tau / n + rng.Randf()) * (i == 0 ? 0 : rng.RandfRange(30, 80)), 28, 1.5f + i * .2f, ph == 3 ? 7f : 5f, 6f, false, false, true);
                    Sfx.Play("pulse", -6, 1.3f); e.State = 0; e.T1 = 2.6f * mul;
                }
                break;
        }
        return vel;
    }

    void OnBossLeaveIce() { ClearIceBlocks(); }

    /// <summary>Свет, попавший в зеркальный щит, отражается осколком в игрока (предупреждение — вспышка).</summary>
    public void ReflectLight(Enemy e)
    {
        if (reflectT > 0) return; reflectT = .8f;
        var d = (PPos - e.Pos).Normalized();
        Projs.Add(new Proj { Pos = e.Pos + d * 16, Vel = d * 90f, Dmg = 6f, C = new Color("#ffffff"), Cold = e.Kind == EK.WinterWarden });
        Burst(e.Pos + d * 16, new Color("#ffffff"), 8, 90); Sfx.Play("tick", -2, 2.2f);
        Texts.Add(new FText { P = e.Pos + new Vector2(0, -26), S = "Отражение!", Life = .7f, C = new Color("#e8faff") });
    }
}
