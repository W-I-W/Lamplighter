using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lamplighter;

/// <summary>Бой в кооперативе. Хост — единственный хозяин врагов: он ведёт их ИИ, выбирает цели среди всех игроков, считает урон и смерть, а клиентам шлёт снимок врагов и снарядов
/// (10 раз в секунду, без декоративных частиц). Клиент не запускает ИИ: он сглаживает присланные позиции, а свои попадания отправляет хосту. Урон по игроку считает хост и шлёт
/// один раз тому, кого ударили; уклонение (неуязвимость рывка) учитывает сам игрок, повторная доставка ничего не удваивает, потому что у игрока есть неуязвимость после удара.</summary>
public partial class Game
{
    int nextEnemyId; float netCombatT; long aiRemote;               // aiRemote — пир, по которому сейчас «бьёт» враг (0 — хост)
    readonly Dictionary<int, Enemy> proxies = new();

    // ───────────────────────── выбор цели (хост) ─────────────────────────
    /// <summary>Враг выбирает игрока по расстоянию с гистерезисом: цель держится 2–3 секунды, ближайшие уже атакуемые игроки получают штраф, чтобы атаки распределялись.</summary>
    void ChooseTarget(Enemy e, float dt, Vector2 local)
    {
        aiRemote = 0; e.RetargetT -= dt; if (!NetHost) return;
        var cands = new List<(long id, Vector2 pos)> { (1, local) };
        foreach (var r in Remotes()) if (r.Hp > 0) cands.Add((r.PeerId, r.Pos));
        if (cands.Count == 1) { e.Target = 1; return; }
        if (e.RetargetT <= 0 || !cands.Exists(c => c.id == e.Target))
        {
            e.RetargetT = 2f + rng.Randf(); long best = e.Target; float bs = float.MaxValue;
            foreach (var c in cands)
            {
                float d = e.Pos.DistanceTo(c.pos) - (c.id == e.Target ? 40 : 0) - (c.id == e.LastHitBy ? 30 : 0);
                foreach (var o in Enemies) if (o != e && o.Target == c.id && !o.IsBoss) d += 12;
                if (d < bs) { bs = d; best = c.id; }
            }
            e.Target = best;
        }
        var tc = cands.Find(c => c.id == e.Target); if (e.Target != 1) { PPos = tc.pos; aiRemote = e.Target; }
    }

    /// <summary>Расстояние до ближайшего игрока (для удаления далёких врагов и ограничений обновления).</summary>
    float NearestPlayerDist(Vector2 p)
    {
        float d = p.DistanceTo(PPos); if (NetHost) foreach (var r in Remotes()) d = Mathf.Min(d, p.DistanceTo(r.Pos)); return d;
    }

    // ───────────────────────── снимки ─────────────────────────
    public void NetCombatTick(float dt)
    {
        if (!NetHost || Net == null) return;
        netCombatT -= dt; if (netCombatT > 0) return; netCombatT = .1f;
        var d = new Godot.Collections.Array<float>(); int n = 0;
        foreach (var e in Enemies)
        {
            if (e.Hidden && !e.IsBoss) { }
            if (NearestPlayerDist(e.Pos) > 520 && !e.IsBoss) continue; if (++n > 60) break;
            d.Add(e.NetId); d.Add((int)e.Kind); d.Add(e.Pos.X); d.Add(e.Pos.Y); d.Add(e.Hp); d.Add(e.MaxHp); d.Add(e.Tele); d.Add(e.Dash); d.Add(e.State);
            d.Add(e.Atk > 0 ? 1 : 0); d.Add(e.Elite ? e.EMod + 1 : 0); d.Add(e.Face.X < 0 ? -1 : 1); d.Add(e.Hidden ? 1 : 0);
        }
        Net.SendEnemies(d);
        var p = new Godot.Collections.Array<float>(); int m = 0;
        foreach (var pr in Projs) { if (++m > 80) break; p.Add(pr.Pos.X); p.Add(pr.Pos.Y); p.Add(pr.Vel.X); p.Add(pr.Vel.Y); p.Add((pr.Fire ? 1 : 0) | (pr.Zone ? 2 : 0) | (pr.Cold ? 4 : 0) | (pr.Dark ? 8 : 0)); p.Add(pr.C.R); p.Add(pr.C.G); p.Add(pr.C.B); }
        Net.SendProjs(p);
    }

    public void NetApplyEnemies(Godot.Collections.Array<float> d)
    {
        if (!NetClient) return;
        for (int i = 0; i + 12 < d.Count; i += 13)
        {
            int id = (int)d[i], kind = (int)d[i + 1]; if (id <= 0 || !Enum.IsDefined(typeof(EK), kind)) continue;
            if (!proxies.TryGetValue(id, out var e)) { e = new Enemy { Kind = (EK)kind, NetId = id, Pos = new Vector2(d[i + 2], d[i + 3]) }; e.NetPos = e.Pos; proxies[id] = e; Enemies.Add(e); }
            e.NetPos = new Vector2(d[i + 2], d[i + 3]);
            float hp = d[i + 4]; if (hp < e.Hp - .05f) { e.Flash = .15f; e.VHit = 0; }
            e.Hp = hp; e.MaxHp = Mathf.Max(1f, d[i + 5]); e.Tele = d[i + 6]; e.Dash = d[i + 7]; e.State = (int)d[i + 8]; e.Atk = d[i + 9] > 0 ? .3f : 0; int em = (int)d[i + 10]; e.Elite = em > 0; e.EMod = Math.Max(0, em - 1);
            e.Face = new Vector2(d[i + 11], 0); e.Hidden = d[i + 12] > 0; e.SeenT = 0;
        }
    }

    public void NetApplyProjs(Godot.Collections.Array<float> d)
    {
        if (!NetClient) return; Projs.Clear();
        for (int i = 0; i + 7 < d.Count; i += 8)
        {
            int f = (int)d[i + 4]; Projs.Add(new Proj { Pos = new Vector2(d[i], d[i + 1]), Vel = new Vector2(d[i + 2], d[i + 3]), Life = .4f, Fire = (f & 1) != 0, Zone = (f & 2) != 0, Cold = (f & 4) != 0, Dark = (f & 8) != 0, C = new Color(d[i + 5], d[i + 6], d[i + 7]), Dmg = 0 });
        }
    }

    /// <summary>Босс, у которого в радиусе боя есть хотя бы один живой игрок (я или любой другой). По нему рисуется шкала здоровья у всех участников.</summary>
    public Enemy BossInFight()
    {
        float r = Tune.T.BossLeashTiles * TS;
        foreach (var e in Enemies)
        {
            if (!e.IsBoss || e.Hp <= 0) continue;
            if (e.Pos.DistanceTo(PPos) <= r) return e;
            if (NetService.Active) foreach (var p in NetService.Players.Values) if (p.Handshaken && p.PeerId != LocalPeer && p.Hp > 0 && p.Shown.DistanceTo(e.Pos) <= r) return e;
        }
        return null;
    }

    /// <summary>Клиент: показ врагов хоста без ИИ.</summary>
    void ClientEnemies(float dt)
    {
        Boss = null;
        for (int i = Enemies.Count - 1; i >= 0; i--)
        {
            var e = Enemies[i]; e.Age += dt; e.SeenT += dt; e.Flash = Mathf.Max(0, e.Flash - dt); e.Atk = Mathf.Max(0, e.Atk - dt);
            if (e.SeenT > .7f) { proxies.Remove(e.NetId); Enemies.RemoveAt(i); continue; }
            var d = e.NetPos - e.Pos; if (d.Length() > 120) e.Pos = e.NetPos; else e.Pos = e.Pos.Lerp(e.NetPos, 1f - Mathf.Exp(-14f * dt));
            if (e.IsBoss && e.Pos.DistanceTo(PPos) < Tune.T.BossLeashTiles * TS) Boss ??= e;
        }
    }

    // ───────────────────────── урон ─────────────────────────
    /// <summary>Хост: участник попал по врагу. Урон ограничен и считается на хосте.</summary>
    public void NetEnemyHit(long sender, int id, float dmg, Vector2 dir, bool melee)
    {
        if (!float.IsFinite(dmg) || dmg <= 0) return; dmg = Mathf.Min(dmg, 60f);
        foreach (var e in Enemies) if (e.NetId == id) { e.LastHitBy = sender; Hurt(e, dmg, dir.LengthSquared() > .01f ? dir.Normalized() : Vector2.Right, melee); return; }
    }

    /// <summary>Клиент: враг ударил именно вас (решение хоста).</summary>
    public void NetHurtMe(float dmg, Vector2 kb, bool fire)
    {
        if (!float.IsFinite(dmg) || dmg <= 0) return; long saved = aiRemote; aiRemote = 0; float c = ctxDmg; ctxDmg = 1f;
        HurtPlayer(Mathf.Min(dmg, 80f), kb, fire); aiRemote = saved; ctxDmg = c;
    }
}
