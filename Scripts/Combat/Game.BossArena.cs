using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lamplighter;

/// <summary>Арены боссов. Бой начинается, когда ломают тотем стража: босс просыпается, проходы арены закрываются воротами, а все живые игроки собираются внутри (в сети гостей переносит хост).
/// Ворота открываются, когда босс побеждён, или когда внутри не осталось живых игроков: тогда босс и его помощники исчезают, тотем возвращается, и бой можно начать заново.
/// Решает хост (в одиночной игре — сам игрок), гости получают состояние арены от хоста (RpcArena) и ставят ворота у себя.</summary>
public partial class Game
{
    const int ArenaIdle = 0, ArenaLocked = 1, ArenaWon = 2;
    readonly Dictionary<int, int> arenaState = new();
    readonly Dictionary<int, float> arenaT = new();
    float arenaRepeatT;
    const float ArenaGrace = 4f;                                           // первые секунды боя позиции игроков ещё догоняют перенос

    public static EK BossKindOf(int id) => id == 1 ? EK.Warden : id == 2 ? EK.DepthWarden : id == 3 ? EK.AshWarden : id == 4 ? EK.WinterWarden : EK.Source;

    public bool ArenaIsLocked(int id) => arenaState.GetValueOrDefault(id) == ArenaLocked;
    public bool AnyArenaLocked { get { foreach (var v in arenaState.Values) if (v == ArenaLocked) return true; return false; } }

    /// <summary>Арена, внутри которой стоит точка (для замка боя), или null.</summary>
    public BossArena ArenaHolding(Vector2 p) { foreach (var a in W.Arenas) if (a.Contains(p, 16)) return a; return null; }

    void UpdateBossSpawn(float dt)
    {
        if (Boss != null && !Enemies.Contains(Boss)) Boss = null;
        if (NetClient) return;
        bossT -= dt; if (bossT > 0) return; float step = .3f; bossT = step;
        foreach (var ar in W.Arenas) ArenaTick(ar, step);
        arenaRepeatT -= step;
        if (arenaRepeatT <= 0) { arenaRepeatT = 2f; if (NetHost) foreach (var kv in arenaState) if (kv.Value == ArenaLocked) Net?.SendArena(kv.Key, ArenaLocked); }      // подключившийся посреди боя тоже увидит закрытые ворота
        if (Boss == null) foreach (var e in Enemies) if (e.IsBoss) { Boss = e; break; }
    }

    void ArenaTick(BossArena ar, float dt)
    {
        int id = ar.BossId; bool locked = ArenaIsLocked(id);
        if (BossDown(id)) { if (locked) ArenaUnlock(ar, true); return; }
        if (!locked) { if (W.Get(ar.Totem.X, ar.Totem.Y) == Tl.None) StartFight(ar); return; }           // тотем сломан — бой начался
        arenaT[id] = arenaT.GetValueOrDefault(id) + dt;
        if (arenaT[id] >= ArenaGrace && !AnyoneInside(ar)) LoseFight(ar);
    }

    bool AnyoneInside(BossArena ar)
    {
        if (Hp > 0 && ar.Contains(PPos, 8)) return true;
        foreach (var r in Remotes()) if (r.Hp > 0 && ar.Contains(r.Pos, 8)) return true;
        return false;
    }

    /// <summary>Точка внутри арены для переноса игрока: вокруг тотема, у каждого своя.</summary>
    Vector2 ArenaSpot(BossArena ar, int slot)
    {
        var tc = TileCenter(ar.Totem.X, ar.Totem.Y);
        var p = tc + Vector2.FromAngle(-Mathf.Pi / 2 + slot * 1.9f) * 30f;
        return NearestFree(p, 5f, false) ?? tc + new Vector2(0, -24);
    }

    void StartFight(BossArena ar)
    {
        int id = ar.BossId; var k = BossKindOf(id);
        arenaState[id] = ArenaLocked; arenaT[id] = 0;
        if (!Enemies.Exists(e => e.Kind == k))
        {
            if (id == 1) GroveReset(false);
            Boss = SpawnEnemy(id == 1 ? ar.Pos : ar.Pos + new Vector2(0, -36), k, null, DangerScale);          // Пожиратель Рощи растёт в самом центре арены
            Announce(EnemyName(k) + " пробудился!", id == 1 ? "Корни бьют из-под земли, ветви — по дуге. Не стойте на месте" : id == 2 ? "Вода и тьма — его стихия. Береги свет!" : id == 3 ? "Огонь и дым — его оружие" : id == 4 ? "Зеркальный щит, лёд и метель. Свет призм сломит его защиту" : id == 5 ? "Он поглощает свет. Ставьте фонари" : "Хранитель древнего фонаря. Победа даст силу света", new Color("#d58cff"), "boss");
            Shake = 4;
        }
        // собрать всех живых игроков: хост переносит себя и гостей, затем ворота закрываются
        int slot = 0;
        if (Hp > 0 && !ar.Contains(PPos, 0)) { PPos = ArenaSpot(ar, slot); pVel = Vector2.Zero; Window = Win.None; cam?.ResetSmoothing(); }
        slot++;
        foreach (var r in Remotes().ToList()) if (r.Hp > 0) { var spot = ArenaSpot(ar, slot++); Net?.SendTeleport(r.PeerId, spot); r.Pos = spot; r.Shown = spot; }
        SetGates(ar, true);
        Toast("Врата арены закрылись: победите стража");
        if (NetHost) Net?.SendArena(id, ArenaLocked);
    }

    /// <summary>Внутри не осталось живых игроков: страж и его помощники пропадают, тотем возвращается, ворота открыты.</summary>
    void LoseFight(BossArena ar)
    {
        int id = ar.BossId; var k = BossKindOf(id);
        Enemies.RemoveAll(e => e.Kind == k || ar.Contains(e.Pos, 48)); Projs.RemoveAll(p => ar.Contains(p.Pos, 48)); Boss = null;
        if (id == 4) ClearIceBlocks(); if (id == 5) ClearHeartTemps(); if (id == 1) GroveReset();
        arenaState[id] = ArenaIdle; SetGates(ar, false);
        if (W.Get(ar.Totem.X, ar.Totem.Y) == Tl.None) SetObj(ar.Totem.X, ar.Totem.Y, Tl.BossTotem);
        Announce("Страж восстановил силы", "Тотем снова цел: можно начать бой заново", new Color("#9aa6bf"), "error");
        if (NetHost) Net?.SendArena(id, ArenaIdle);
    }

    void ArenaUnlock(BossArena ar, bool won)
    {
        arenaState[ar.BossId] = ArenaIdle; SetGates(ar, false);
        if (NetHost) Net?.SendArena(ar.BossId, ArenaWon);
    }

    void SetGates(BossArena ar, bool closed)
    {
        foreach (var g in ar.Gates) foreach (var t in g)
        {
            var cur = W.Get(t.X, t.Y);
            if (closed ? cur == Tl.None : cur == Tl.BossGate) SetObj(t.X, t.Y, closed ? Tl.BossGate : Tl.None);
        }
        Sfx.Play(closed ? "boom" : "tick", -4, closed ? .7f : 1.2f);
    }

    /// <summary>Гость: состояние арены от хоста (ворота и тотем ставятся у себя).</summary>
    public void NetArena(int id, int state)
    {
        if (!NetClient || id < 1 || id > 5 || state < 0 || state > 2) return; var ar = W.ArenaOf(id); if (ar == null) return;
        bool was = ArenaIsLocked(id);
        if (state == ArenaLocked) { if (!was) { arenaState[id] = ArenaLocked; SetGates(ar, true); } return; }
        arenaState[id] = ArenaIdle; if (was) SetGates(ar, false);
        if (state == ArenaIdle && was && !BossDown(id) && W.Get(ar.Totem.X, ar.Totem.Y) == Tl.None) { SetObj(ar.Totem.X, ar.Totem.Y, Tl.BossTotem); Boss = null; if (id == 4) ClearIceBlocks(); if (id == 1) GroveReset(); }
    }

    /// <summary>Гость: хост собирает бойцов в арене.</summary>
    public void NetTeleport(float x, float y)
    {
        if (!NetClient || !float.IsFinite(x) || !float.IsFinite(y) || x < 0 || y < 0 || x > World.Wd * TS || y > World.Ht * TS) return;
        PPos = new Vector2(x, y); pVel = Vector2.Zero; Window = Win.None; cam?.ResetSmoothing();
    }

    /// <summary>Мир для записи: бой не сохраняется, поэтому ворота открыты, а тотем цел, если страж ещё не побеждён.</summary>
    byte[] ObjForSave()
    {
        byte[] o = W.Obj;
        foreach (var ar in W.Arenas)
        {
            bool fight = ArenaIsLocked(ar.BossId); if (!fight && !HasGateTiles(ar)) continue;
            if (o == W.Obj) o = (byte[])W.Obj.Clone();
            foreach (var g in ar.Gates) foreach (var t in g) if (o[World.Key(t.X, t.Y)] == (byte)Tl.BossGate) o[World.Key(t.X, t.Y)] = (byte)Tl.None;
            if (fight && !BossDown(ar.BossId)) o[World.Key(ar.Totem.X, ar.Totem.Y)] = (byte)Tl.BossTotem;
        }
        return o;
    }

    bool HasGateTiles(BossArena ar) { foreach (var g in ar.Gates) foreach (var t in g) if (W.Get(t.X, t.Y) == Tl.BossGate) return true; return false; }
}
