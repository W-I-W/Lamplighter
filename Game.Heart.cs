using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Сердце Тьмы: ложные огни, ловушки, загадки мостов, иллюзорные враги и финальный босс — Источник Тьмы.</summary>
public partial class Game
{
    public Dictionary<int, float> PylonT = new();                       // ключ плитки → оставшееся время свечения
    readonly Dictionary<int, float> falseArm = new();                   // ложные огни: время до всплеска (после приближения игрока)
    readonly Dictionary<int, float> trapCd = new();
    public List<(Vector2I tile, float t, float life)> VoidPending = new();
    public List<(Vector2I tile, float t)> VoidTemp = new();
    public List<(Vector2I tile, float life, float arm)> ArenaFalse = new();
    public bool InDark => !(IsLit(PPos) || Surge > 0);                  // защищён ли игрок светом (лампы или импульс [F])

    public int PylonsOn() { int n = 0; foreach (var p in W.ArenaPylons) if (PylonT.GetValueOrDefault(World.Key(p.X, p.Y)) > 0) n++; return n; }
    void DrainPylons() { foreach (var p in W.ArenaPylons) PylonT[World.Key(p.X, p.Y)] = 0; }

    public void DarkDot(float amt)
    {
        if (!InDark) return;
        Hp -= amt; regenDelay = 3f;
        if (rng.Randf() < .12f) Burst(PPos, new Color("#7a3aff"), 2, 40);
        if (Hp <= 0) Die();
    }

    // ───────────────────────── мир Сердца ─────────────────────────
    void UpdateHeart(float dt)
    {
        UpdateVoid(dt);
        if (RegionAt(PPos) != 4 && ArenaFalse.Count == 0) return;
        // ложные огни: приближение — предупреждение — всплеск (его можно избежать, отойдя)
        foreach (var t in W.FalseLights)
        {
            if (W.Get(t.X, t.Y) != Tl.FalseLantern) continue;
            var c = TileCenter(t.X, t.Y); float d = c.DistanceTo(PPos); int k = World.Key(t.X, t.Y);
            if (d < 3.2f * TS) { falseArm[k] = falseArm.GetValueOrDefault(k, .9f) - dt; if (falseArm[k] <= 0) FalseBurst(t); }
            else if (falseArm.ContainsKey(k)) falseArm.Remove(k);
        }
        // ловушки-руны
        foreach (var t in W.Traps)
        {
            int k = World.Key(t.X, t.Y); float cd = trapCd.GetValueOrDefault(k) - dt; trapCd[k] = cd;
            if (cd <= 0 && TileCenter(t.X, t.Y).DistanceTo(PPos) < 9)
            {
                trapCd[k] = 7f; AddZone(TileCenter(t.X, t.Y), 34, .9f, 3.5f, 9f, false, false, false, true);
                Toast("Тенистая руна! Уйдите или вспыхните светом [F]"); Sfx.Play("flare", -4, .6f);
            }
        }
        // ложные фонари босса
        for (int i = ArenaFalse.Count - 1; i >= 0; i--)
        {
            var f = ArenaFalse[i]; var c = TileCenter(f.tile.X, f.tile.Y);
            f.life -= dt; if (c.DistanceTo(PPos) < 1.6f * TS) f.arm -= dt; else f.arm = Mathf.Min(.6f, f.arm + dt);
            ArenaFalse[i] = f;
            if (f.arm <= 0) { ArenaFalse.RemoveAt(i); FalseBurst(f.tile, true); }
            else if (f.life <= 0 || W.Get(f.tile.X, f.tile.Y) != Tl.FalseLantern) { if (W.Get(f.tile.X, f.tile.Y) == Tl.FalseLantern) SetObj(f.tile.X, f.tile.Y, Tl.None); ArenaFalse.RemoveAt(i); }
        }
    }

    void FalseBurst(Vector2I t, bool boss = false)
    {
        var c = TileCenter(t.X, t.Y); falseArm.Remove(World.Key(t.X, t.Y));
        SetObj(t.X, t.Y, Tl.None);
        Burst(c, new Color("#c070ff"), 24, 120); Sfx.Play("boom", -4, 1.4f); Shake = Mathf.Max(Shake, 3);
        if (c.DistanceTo(PPos) < 40 && InDark) { HurtPlayer(boss ? 10f : 7f, (PPos - c).Normalized()); }
        if (!boss)
        {
            LightBlockT = Mathf.Max(LightBlockT, 1.5f);
            for (int i = 0; i < 2; i++) { var d = SpawnEnemy(c + Vector2.FromAngle(rng.Randf() * Mathf.Tau) * 14, EK.Decoy, null, 1f); d.T1 = 9f; }
            Toast("Ложный свет! Настоящий горит ровно, без мерцания");
        }
    }

    void UpdateVoid(float dt)
    {
        for (int i = VoidPending.Count - 1; i >= 0; i--)
        {
            var b = VoidPending[i]; b = (b.tile, b.t - dt, b.life); VoidPending[i] = b;
            if (b.t > 0) continue;
            VoidPending.RemoveAt(i);
            if (W.Get(b.tile.X, b.tile.Y) == Tl.None && ToTile(PPos) != b.tile)
            {
                bool occ = false; foreach (var e in Enemies) if (ToTile(e.Pos) == b.tile && !e.IsBoss) occ = true;
                if (occ) continue;
                SetObj(b.tile.X, b.tile.Y, Tl.Void); VoidTemp.Add((b.tile, b.life)); Burst(TileCenter(b.tile.X, b.tile.Y), new Color("#5a2aa0"), 6, 40);
            }
        }
        for (int i = VoidTemp.Count - 1; i >= 0; i--)
        {
            var b = VoidTemp[i]; b = (b.tile, b.t - dt); VoidTemp[i] = b;
            if (W.Get(b.tile.X, b.tile.Y) != Tl.Void) { VoidTemp.RemoveAt(i); continue; }
            if (b.t <= 0) { SetObj(b.tile.X, b.tile.Y, Tl.None); Burst(TileCenter(b.tile.X, b.tile.Y), new Color("#c070ff"), 5, 40); VoidTemp.RemoveAt(i); }
        }
    }

    void ClearHeartTemps()
    {
        foreach (var b in VoidTemp) if (W.Get(b.tile.X, b.tile.Y) == Tl.Void) SetObj(b.tile.X, b.tile.Y, Tl.None);
        foreach (var f in ArenaFalse) if (W.Get(f.tile.X, f.tile.Y) == Tl.FalseLantern) SetObj(f.tile.X, f.tile.Y, Tl.None);
        VoidTemp.Clear(); VoidPending.Clear(); ArenaFalse.Clear(); DrainPylons();
    }

    /// <summary>Свет древних светил: от луча или касания [E]. Вызывается из UpdateBeams.</summary>
    void UpdateSourceDevices(HashSet<int> rec, float dt)
    {
        foreach (var p in W.ArenaPylons)
        {
            int k = World.Key(p.X, p.Y); float t = PylonT.GetValueOrDefault(k);
            if (rec.Contains(k)) t = Mathf.Max(t, 1.5f);
            t = Mathf.Max(0, t - dt); PylonT[k] = t;
            var cur = W.Get(p.X, p.Y);
            if (t > 0 && cur == Tl.Pylon) { SetObj(p.X, p.Y, Tl.PylonOn); Sfx.Play("tick", -3, 1.6f); }
            else if (t <= 0 && cur == Tl.PylonOn) SetObj(p.X, p.Y, Tl.Pylon);
        }
    }

    // ───────────────────────── взаимодействие ─────────────────────────
    bool HeartIx(int x, int y, Tl t, Ix ix)
    {
        W.ByTile.TryGetValue(World.Key(x, y), out var p);
        switch (t)
        {
            case Tl.Tablet:
                if (p == null) return false;
                ix.Lines.Add(LoreRead(p) ? "[E] Перечитать записку" : "[E] Прочесть записку"); ix.E = () => ReadLore(p); return true;
            case Tl.FalseLantern:
                foreach (var rd in W.Riddles)
                {
                    int i = rd.Lanterns.IndexOf(new Vector2I(x, y)); if (i < 0) continue;
                    if (rd.Poi.State >= 3) { ix.Lines.Add("Настоящий свет указал путь"); return true; }
                    ix.Lines.Add("Огонёк · " + (rd.Real == i ? "горит ровно" : "мерцает")); ix.Lines.Add("[E] Коснуться света"); ix.E = () => TouchLantern(rd, i); return true;
                }
                ix.Lines.Add("Ложный свет: мерцает и не греет. Держитесь подальше"); return true;
            case Tl.Pylon:
                ix.Lines.Add("Древнее светило: оживает от луча или касания");
                ix.Lines.Add("[E] Пробудить светило"); ix.E = () => { PylonT[World.Key(x, y)] = 18f; Burst(TileCenter(x, y), new Color("#ffe27a"), 16, 90); Sfx.Play("tech", -2, 1.2f); }; return true;
            case Tl.PylonOn: ix.Lines.Add($"Светило горит ({PylonT.GetValueOrDefault(World.Key(x, y)):0} с)"); return true;
        }
        return false;
    }

    void TouchLantern(Riddle rd, int i)
    {
        var t = rd.Lanterns[i];
        if (rd.Real == i)
        {
            rd.Poi.State = 3; rd.Poi.Seen = true;
            foreach (var g in rd.Gap) { SetObj(g.X, g.Y, Tl.LightBridge); Burst(TileCenter(g.X, g.Y), new Color("#ffe27a"), 8, 60); }
            foreach (var l in rd.Lanterns) if (W.Get(l.X, l.Y) == Tl.FalseLantern) Burst(TileCenter(l.X, l.Y), new Color("#ffe27a"), 10, 70);
            GiveXp(80); Flash = .3f; Shake = 3; Sfx.Play("ancient");
            Announce("Настоящий свет найден!", "Мост света соединил острова", new Color("#ffe27a"), "ancient"); Save();
        }
        else FalseBurst(t);
    }

    public int LoreMask;
    bool LoreRead(Poi p) => LoreIndex(p) is int i && (LoreMask & (1 << i)) != 0;
    static int? LoreIndex(Poi p) => p.Tag != null && p.Tag.StartsWith("lore:") && int.TryParse(p.Tag.Substring(5), out int i) && i >= 0 && i < Story.Lore.Length ? i : null;

    void ReadLore(Poi p)
    {
        if (LoreIndex(p) is not int i) return;
        var (title, text) = Story.Lore[i];
        if ((LoreMask & (1 << i)) == 0) { LoreMask |= 1 << i; GiveXp(20); Found.Add("Записка: " + title); if (Found.Count > 60) Found.RemoveAt(0); }
        p.State = 3; p.Seen = true; OpenStory(title, text, $"Записка {i + 1} из {Story.Lore.Length}");
    }
    public int LoreCount { get { int n = 0; for (int i = 0; i < Story.Lore.Length; i++) if ((LoreMask & (1 << i)) != 0) n++; return n; } }

    // ───────────────────────── враги Сердца ─────────────────────────
    Vector2 UpdateHeartEnemy(Enemy e, float dt, Vector2 dir, float dist, Vector2 perp, float k)
    {
        Vector2 vel = Vector2.Zero;
        switch (e.Kind)
        {
            case EK.Mirage:
                e.Cd -= dt;
                if (e.Tele > 0)
                {
                    e.Tele -= dt;
                    if (e.Tele <= 0)
                    {
                        if (e.State == 1)
                        {
                            int alive = 0; foreach (var o in Enemies) if (o.Kind == EK.Decoy) alive++;
                            for (int i = 0; i < 2 && alive < 6; i++) { var d = SpawnEnemy(e.Pos + Vector2.FromAngle(rng.Randf() * Mathf.Tau) * 16, EK.Decoy, null, 1f); d.T1 = 9f; alive++; }
                            Burst(e.Pos, new Color("#c070ff"), 12, 70);
                        }
                        else for (int i = -1; i <= 1; i++) Projs.Add(new Proj { Pos = e.Pos, Vel = (e.Mark - e.Pos).Normalized().Rotated(i * .22f) * 85f, Dmg = 7f * e.Scale, C = new Color("#a05aff"), Dark = true });
                        e.Cd = rng.RandfRange(2.6f, 3.6f);
                    }
                }
                else
                {
                    if (dist > 125) vel = dir * 40f; else if (dist < 80) vel = -dir * 42f; else vel = perp * Mathf.Sin(Time * 1.6f + e.Phase) * 22f;
                    vel *= k;
                    if (e.Cd <= 0 && dist < 190) { e.Tele = .6f; e.Mark = PPos; e.State = rng.Randf() < .45f ? 1 : 0; }
                }
                break;
            case EK.Decoy:
                e.T1 -= dt; if (e.T1 <= 0) { e.Hp = 0; Burst(e.Pos, new Color("#c070ff"), 6, 40); break; }
                vel = (dir + perp * Mathf.Sin(Time * 5f + e.Phase) * .8f).Normalized() * 36f * k;
                if (dist < 10) { e.Hp = 0; LightBlockT = Mathf.Max(LightBlockT, 1.2f); Burst(e.Pos, new Color("#c070ff"), 10, 70); Toast("Иллюзия вспыхнула и ослепила"); }
                break;
            case EK.Weaver:
                e.Cd -= dt;
                if (e.Tele > 0)
                {
                    e.Tele -= dt;
                    if (e.Tele <= 0) { AddZone(e.Mark, 30, 1.1f, 4.5f, 8f, false, false, false, true); AddZone(e.Mark + Vector2.FromAngle(rng.Randf() * Mathf.Tau) * 40, 24, 1.3f, 4.5f, 8f, false, false, false, true); e.Cd = rng.RandfRange(3.4f, 4.6f); Sfx.Play("flare", -5, .6f); }
                }
                else
                {
                    if (dist > 140) vel = dir * 36f; else if (dist < 95) vel = -dir * 36f; else vel = perp * Mathf.Sin(Time * 1.2f + e.Phase) * 16f;
                    vel *= k;
                    if (e.Cd <= 0 && dist < 200) { e.Tele = .6f; e.Mark = PPos; }
                    if (dist < 13 && e.Atk <= 0) { e.Atk = 1f; HurtPlayer(6f * e.Scale, -dir); }
                }
                break;
            case EK.Stalker:
                e.Cd -= dt;
                if (e.Tele > 0)
                {
                    e.Tele -= dt;
                    if (e.Tele <= 0)
                    {
                        var tt = ToTile(e.Mark);
                        if (W.Get(tt.X, tt.Y) == Tl.None) { Burst(e.Pos, new Color("#7a3aff"), 8, 60); e.Pos = e.Mark; Burst(e.Pos, new Color("#7a3aff"), 8, 60); e.Dash = .25f; e.DashDir = (PPos - e.Pos).Normalized(); }
                        e.Cd = rng.RandfRange(3f, 4.2f);
                    }
                }
                else if (e.Dash > 0)
                {
                    e.Dash -= dt; vel = e.DashDir * 150f * k;
                    if (dist < 12 && e.Atk <= 0) { e.Atk = .9f; HurtPlayer(10f * e.Scale, -dir); }
                }
                else
                {
                    vel = (dir + perp * Mathf.Sin(Time * 3f + e.Phase) * .4f).Normalized() * (InDark ? 68f : 40f) * k;
                    if (e.Cd <= 0 && dist > 55 && dist < 170) { e.Tele = .6f; var behind = PPos - LastMoveDir.Normalized() * 30; e.Mark = behind.LengthSquared() > 1 ? behind : PPos + perp * 30; }
                    if (dist < 12 && e.Atk <= 0) { e.Atk = .9f; HurtPlayer(10f * e.Scale, -dir); }
                }
                break;
            case EK.Source: vel = UpdateSource(e, dt, dir, dist); break;
        }
        return vel;
    }

    // ───────────────────────── Источник Тьмы ─────────────────────────
    Vector2 UpdateSource(Enemy e, float dt, Vector2 dir, float dist)
    {
        float hpf = e.Hp / e.MaxHp; int ph = hpf > .66f ? 1 : hpf > .33f ? 2 : 3; float mul = ph == 3 ? .62f : ph == 2 ? .85f : 1f;
        if ((int)e.Mark.X != ph)
        {
            e.Mark = new Vector2(ph, 0); e.State = 0; e.T1 = 2f; DrainPylons();
            if (ph == 2) { e.Shield = true; Announce("Источник искажает мир!", "Он закрылся тьмой. Пробудите два древних светила [E] у краёв арены — свет сломит защиту", new Color("#d58cff"), "boss"); Shake = 6; }
            if (ph == 3) { e.Shield = true; Announce("Последний свет!", "Нужны три светила. Не доверяйте мерцающим огням — настоящие горят ровно", new Color("#ffe27a"), "boss"); Shake = 8; }
        }
        if (ph >= 2)
        {
            int need = ph == 2 ? 2 : 3;
            if (e.VulnT > 0) { e.VulnT -= dt; if (e.VulnT <= 0) { e.Shield = true; Toast("Источник вновь поглощает свет"); } }
            else if (!e.Shield) e.Shield = true;
            if (e.Shield && PylonsOn() >= need)
            {
                e.Shield = false; e.VulnT = ph == 2 ? 7f : 9f; DrainPylons();
                Announce("Защита сломлена!", "Источник уязвим — бейте обычным оружием!", new Color("#ffe27a"), "ancient"); Burst(e.Pos, new Color("#ffffff"), 40, 170); Shake = 6; Flash = .3f;
            }
            if (e.Shield) e.Hp = Mathf.Max(e.Hp, e.MaxHp * (ph == 2 ? .335f : .02f));
        }
        else e.Shield = false;

        Vector2 vel = Vector2.Zero; var perp = new Vector2(-dir.Y, dir.X);
        switch (e.State)
        {
            case 0:
                {
                    var home = e.Home ?? e.Pos; var toHome = home - e.Pos;
                    float spd = ph == 3 ? 38f : 28f;
                    vel = toHome.Length() > 12 * TS ? toHome.Normalized() * 34f : dist > 120 ? dir * spd : dist < 70 ? -dir * spd : perp * Mathf.Sin(Time * .9f) * spd * .6f;
                    e.T1 -= dt;
                    if (dist < 22 && e.Atk <= 0) { e.Atk = 1.2f; HurtPlayer(14f, -dir); }
                    if (e.T1 <= 0)
                    {
                        float r = rng.Randf();
                        if (ph == 1) { e.State = r < .3f ? 1 : r < .6f ? 2 : r < .8f ? 3 : 4; }
                        else if (ph == 2) { e.State = r < .25f ? 5 : r < .5f ? 6 : r < .7f ? 7 : r < .85f ? 2 : 3; }
                        else { e.State = r < .2f ? 1 : r < .4f ? 5 : r < .65f ? 2 : r < .8f ? 4 : r < .9f ? 3 : 7; }
                        e.T2 = e.State switch { 1 => .9f, 2 => .75f, 3 => .8f, 4 => .7f, 5 => 1.6f, 6 => .8f, _ => .8f } * (ph == 3 ? .85f : 1f);
                        e.DashDir = dir;
                        if (e.State == 5) PlaceVoidRing(ph);
                    }
                    break;
                }
            case 1:     // поглощение света: кольцо вокруг босса
                e.T2 -= dt;
                if (e.T2 <= 0)
                {
                    float R = ph == 3 ? 115f : 95f;
                    for (int i = 0; i < 24; i++) { float a = i * Mathf.Tau / 24; Parts.Add(new Particle { P = e.Pos + Vector2.FromAngle(a) * R, V = -Vector2.FromAngle(a) * 60, Life = .5f, Max = .5f, C = new Color("#7a3aff"), Size = 2 }); }
                    if (dist < R) { if (InDark) { HurtPlayer(9f, -dir); LightBlockT = Mathf.Max(LightBlockT, 3.5f); } else Texts.Add(new FText { P = PPos + new Vector2(0, -16), S = "Свет защитил!", Life = 1f, C = new Color("#ffe27a") }); }
                    Sfx.Play("pulse", -3, .6f); Shake = 3; e.State = 0; e.T1 = 2.4f * mul;
                }
                break;
            case 2:     // залп теневых снарядов (рассеиваются в свете фонарей)
                e.T2 -= dt; e.DashDir = dir;
                if (e.T2 <= 0)
                {
                    int n = ph == 3 ? 7 : 5;
                    for (int i = 0; i < n; i++) Projs.Add(new Proj { Pos = e.Pos, Vel = dir.Rotated((i - (n - 1) / 2f) * .21f) * 85f, Dmg = 7f, C = new Color("#a05aff"), Dark = true, Life = 3.5f });
                    Sfx.Play("shoot", -2, .7f); e.State = 0; e.T1 = 2.2f * mul;
                }
                break;
            case 3:     // призыв
                e.T2 -= dt;
                if (e.T2 <= 0)
                {
                    int alive = 0; foreach (var o in Enemies) if (!o.IsBoss) alive++;
                    var kinds = ph == 1 ? new[] { EK.Shade, EK.Shade } : ph == 2 ? new[] { EK.Mirage, EK.Weaver } : new[] { EK.Stalker, EK.Stalker, EK.Weaver };
                    foreach (var kd in kinds) if (alive++ < 6) { var m = SpawnEnemy(e.Pos + Vector2.FromAngle(rng.Randf() * Mathf.Tau) * 28, kd, null, 1.1f); m.Home = e.Home; }
                    Burst(e.Pos, new Color("#7a3aff"), 18, 100); Sfx.Play("boss", -4, .8f); e.State = 0; e.T1 = 3.4f * mul;
                }
                break;
            case 4:     // тёмные зоны: безопасны при свете ламп или вспышке [F]
                e.T2 -= dt;
                if (e.T2 <= 0)
                {
                    for (int i = 0; i < 3; i++) AddZone(PPos + (i == 0 ? Vector2.Zero : Vector2.FromAngle(rng.Randf() * Mathf.Tau) * rng.RandfRange(40, 85)), 30, 1.2f + i * .25f, 5f, 9f, false, false, false, true);
                    Sfx.Play("pulse", -5, .7f); e.State = 0; e.T1 = 2.6f * mul;
                }
                break;
            case 5:     // искажение: часть арены обращается в пустоту
                e.T2 -= dt;
                if (e.T2 <= 0) { Sfx.Play("pulse", -3, .5f); Shake = 4; e.State = 0; e.T1 = 3.2f * mul; }
                break;
            case 6:     // ложные фонари
                e.T2 -= dt;
                if (e.T2 <= 0)
                {
                    var c = W.HeartArena;
                    for (int n = 0, tries = 0; n < 3 && tries < 40; tries++)
                    {
                        var t = c + new Vector2I(rng.RandiRange(-9, 9), rng.RandiRange(-9, 9)); float rr = (t - c).Length();
                        if (rr < 4 || rr > 10 || W.Get(t.X, t.Y) != Tl.None || (TileCenter(t.X, t.Y)).DistanceTo(PPos) < 40) continue;
                        bool nearP = false; foreach (var p in W.ArenaPylons) if ((p - t).Length() < 3f) nearP = true; if (nearP) continue;
                        SetObj(t.X, t.Y, Tl.FalseLantern); ArenaFalse.Add((t, 16f, .6f)); n++;
                    }
                    Burst(e.Pos, new Color("#ffe27a"), 14, 80); Sfx.Play("flare", -4, 1.4f); Toast("Ложные огни! Настоящие светила — по краям арены");
                    e.State = 0; e.T1 = 3.4f * mul;
                }
                break;
            case 7:     // огонь или лёд (чередуются)
                e.T2 -= dt;
                if (e.T2 <= 0)
                {
                    bool cold = rng.Randf() < .5f;
                    for (int i = 0; i < 3; i++) AddZone(PPos + (i == 0 ? Vector2.Zero : Vector2.FromAngle(rng.Randf() * Mathf.Tau) * rng.RandfRange(35, 80)), 26, 1.3f + i * .2f, 4f, 11f, false, false, cold);
                    Sfx.Play("flare", -5, cold ? 1.3f : .7f); e.State = 0; e.T1 = 2.6f * mul;
                }
                break;
        }
        return vel;
    }

    void PlaceVoidRing(int ph)
    {
        var c = W.HeartArena; int q = rng.RandiRange(0, 3); float a0 = q * Mathf.Pi / 2;
        for (int y = -10; y <= 10; y++) for (int x = -10; x <= 10; x++)
        {
            float rr = Mathf.Sqrt(x * x + y * y); if (rr < 4.5f || rr > (ph == 3 ? 10.5f : 9.5f)) continue;
            float ang = Mathf.Atan2(y, x); float da = Mathf.PosMod(ang - a0 + Mathf.Pi, Mathf.Tau) - Mathf.Pi; if (da < 0 || da > Mathf.Pi / 2 + (ph == 3 ? .5f : 0f)) continue;
            var t = c + new Vector2I(x, y); bool nearP = false; foreach (var p in W.ArenaPylons) if ((p - t).Length() < 2.5f) nearP = true;
            if (!nearP && W.Get(t.X, t.Y) == Tl.None && ToTile(PPos) != t) VoidPending.Add((t, 1.4f, ph == 3 ? 9f : 7f));
        }
        Toast("Мир искажается — уйдите с тёмных клеток!");
                }

    // ───────────────────────── рисование ─────────────────────────
    void DrawHeartTelegraph(EntityLayer c, Enemy e)
    {
        if (e.Kind == EK.Mirage && e.Tele > 0) c.DrawLine(e.Pos, e.Pos + (e.Mark - e.Pos).Normalized() * 80, new Color(.7f, .4f, 1f, .6f), 1.5f);
        if (e.Kind == EK.Weaver && e.Tele > 0) { c.DrawArc(e.Mark, 30, 0, Mathf.Tau, 24, new Color(.6f, .3f, 1f, .8f), 1.5f); }
        if (e.Kind == EK.Stalker && e.Tele > 0) { c.DrawArc(e.Mark, 12, 0, Mathf.Tau, 16, new Color(.7f, .4f, 1f, .9f), 1.5f); c.DrawLine(e.Pos, e.Mark, new Color(.7f, .4f, 1f, .25f), 1f); }
        if (e.Kind == EK.Decoy) { float a = .5f + .3f * Mathf.Sin(Time * 14f); c.DrawArc(e.Pos, 7, 0, Mathf.Tau, 12, N("Reveal") > 0 ? new Color(1f, .4f, .4f, .9f) : new Color(.8f, .6f, 1f, a), 1f); }
        if (e.Kind != EK.Source) return;
        if (e.Shield) { float p = .6f + .4f * Mathf.Sin(Time * 5f); c.DrawArc(e.Pos, 28, 0, Mathf.Tau, 32, new Color(.7f, .4f, 1f, .5f * p), 2.5f); }
        if (e.VulnT > 0) c.DrawArc(e.Pos, 22, 0, Mathf.Tau, 24, new Color(1f, .9f, .4f, .8f), 1.5f);
        float k = e.State == 0 ? 0 : 1 - Mathf.Clamp(e.T2 / (e.State == 5 ? 1.6f : .9f), 0, 1);
        switch (e.State)
        {
            case 1: { float R = (int)(e.Mark.X) == 3 ? 115f : 95f; c.DrawArc(e.Pos, R, 0, Mathf.Tau, 48, new Color(.7f, .4f, 1f, .35f + .4f * k), 1.5f); c.DrawArc(e.Pos, R * (1 - k), 0, Mathf.Tau, 36, new Color(1, 1, 1, .5f), 1f); break; }
            case 2: c.DrawLine(e.Pos, e.Pos + e.DashDir * 150, new Color(.7f, .4f, 1f, .3f + .5f * k), 2f); break;
            case 3: case 4: case 6: case 7: c.DrawArc(e.Pos, 14 + 14 * k, 0, Mathf.Tau, 20, new Color(.9f, .6f, 1f, .8f), 1.5f); break;
            case 5: c.DrawArc(e.Pos, 20, 0, Mathf.Tau, 24, new Color(.6f, .2f, 1f, .6f + .3f * Mathf.Sin(Time * 14f)), 2f); break;
        }
    }

    /// <summary>Глобальные элементы Сердца: руны, предупреждения пустоты, огни, лучи светил к боссу.</summary>
    void DrawHeartWorld(EntityLayer c)
    {
        if (RegionAt(PPos) != 4 && VoidPending.Count == 0) return;
        foreach (var t in W.Traps)
        {
            var p = TileCenter(t.X, t.Y); float d = p.DistanceTo(PPos); float rv = N("Reveal") > 0 ? 1.7f : 1f; if (d > PlayerLightR * .9f * rv) continue;
            float a = Mathf.Clamp(1.2f - d / (PlayerLightR * .9f * rv), 0, 1) * (.5f + .3f * Mathf.Sin(Time * 4f + t.X));
            c.DrawArc(p, 6, 0, Mathf.Tau, 12, new Color(.7f, .3f, 1f, a), 1f); c.DrawLine(p + new Vector2(-4, 0), p + new Vector2(4, 0), new Color(.7f, .3f, 1f, a), 1f); c.DrawLine(p + new Vector2(0, -4), p + new Vector2(0, 4), new Color(.7f, .3f, 1f, a), 1f);
        }
        foreach (var t in W.FalseLights)
            if (falseArm.TryGetValue(World.Key(t.X, t.Y), out float arm)) { var p = TileCenter(t.X, t.Y); float k = 1 - arm / .9f; c.DrawArc(p, 26, 0, Mathf.Tau, 24, new Color(1f, .5f, 1f, .5f + .4f * k), 1.5f); c.DrawArc(p, 26 * (1 - k), 0, Mathf.Tau, 20, new Color(1, 1, 1, .5f), 1f); }
        if (N("Reveal") > 0) foreach (var rd in W.Riddles) if (rd.Poi.State < 3) { var rp = TileCenter(rd.Lanterns[rd.Real].X, rd.Lanterns[rd.Real].Y); if (rp.DistanceTo(PPos) < 260) c.DrawArc(rp, 14 + 2 * Mathf.Sin(Time * 4f), 0, Mathf.Tau, 20, new Color(1f, .9f, .4f, .8f), 1.2f); }
        foreach (var f in ArenaFalse) { var p = TileCenter(f.tile.X, f.tile.Y); if (f.arm < .6f) c.DrawArc(p, 18, 0, Mathf.Tau, 20, new Color(1f, .4f, 1f, .8f), 1.5f); }
        foreach (var b in VoidPending) { var p = TileCenter(b.tile.X, b.tile.Y); float k = 1 - b.t / 1.4f; c.DrawRect(new Rect2(p - new Vector2(8, 8), new Vector2(16, 16)), new Color(.5f, .1f, 1f, .15f + .35f * k), true); c.DrawRect(new Rect2(p - new Vector2(8, 8), new Vector2(16, 16)), new Color(.8f, .5f, 1f, .7f), false, 1f); }
        if (Boss != null && Boss.Kind == EK.Source && Boss.Shield)
            foreach (var p in W.ArenaPylons) if (PylonT.GetValueOrDefault(World.Key(p.X, p.Y)) > 0) { var pc = TileCenter(p.X, p.Y); c.DrawLine(pc, Boss.Pos, new Color(1f, .9f, .5f, .25f), 4f); c.DrawLine(pc, Boss.Pos, new Color(1f, .95f, .8f, .8f), 1.2f); }
    }

    /// <summary>Источники света Сердца для шейдера: ложные огни мерцают, настоящий — горит ровно.</summary>
    IEnumerable<Vector4> HeartLights()
    {
        if (RegionAt(PPos) != 4) yield break;
        int n = 0; var cc = CamCenter;
        foreach (var rd in W.Riddles)
            for (int i = 0; i < rd.Lanterns.Count; i++)
            {
                var t = rd.Lanterns[i]; if (W.Get(t.X, t.Y) != Tl.FalseLantern) continue; var p = TileCenter(t.X, t.Y); if (p.DistanceTo(cc) > 520) continue;
                float f = rd.Poi.State >= 3 ? 1f : rd.Real == i ? (N("Reveal") > 0 ? 1.35f : 1f) : 1f + .45f * Mathf.Sin(Time * (13f + i * 3.1f) + i) * Mathf.Sin(Time * 5f + i * 2f);
                yield return new Vector4(p.X, p.Y, 58 * Mathf.Max(.3f, f), 0); if (++n > 10) yield break;
            }
        foreach (var t in W.FalseLights)
        {
            if (W.Get(t.X, t.Y) != Tl.FalseLantern) continue; var p = TileCenter(t.X, t.Y); if (p.DistanceTo(cc) > 520) continue;
            float f = 1f + .5f * Mathf.Sin(Time * 15f + t.X) * Mathf.Sin(Time * 6f + t.Y);
            yield return new Vector4(p.X, p.Y, 50 * Mathf.Max(.3f, f), 0); if (++n > 14) yield break;
        }
        foreach (var f in ArenaFalse) { var p = TileCenter(f.tile.X, f.tile.Y); float fl = 1f + .5f * Mathf.Sin(Time * 15f + f.tile.X); yield return new Vector4(p.X, p.Y, 46 * Mathf.Max(.3f, fl), 0); }
        foreach (var p0 in W.ArenaPylons) if (PylonT.GetValueOrDefault(World.Key(p0.X, p0.Y)) > 0) { var p = TileCenter(p0.X, p0.Y); yield return new Vector4(p.X, p.Y, 70, 0); }
    }

    // ───────────────────────── победа ─────────────────────────
    void FinalBossKilled(Enemy e)
    {
        BossMask |= 16; if (Boss == e) Boss = null;
        ClearHeartTemps();
        Give(It.Core, 1, e.Pos + new Vector2(0, -20)); Give(It.Shard, 12, e.Pos); Give(It.Relic, 3, e.Pos + new Vector2(0, -8)); Give(It.AncCrystal, 3, e.Pos + new Vector2(10, -8));
        GiveRelic("r_voidlens"); Found.Add("Победа над Источником Тьмы"); GiveXp(900);
        Flash = 1f; Shake = 10; Burst(e.Pos, new Color("#ffe9a0"), 80, 220); VictoryFx(e.Pos, new Color("#ffe9a0"));
        Announce("Источник Тьмы повержен!", "Пятое ядро света добыто. Вернитесь к Центральному маяку и зажгите его окончательно", new Color("#ffe9a0"), "ancient");
        Save();
    }
}
