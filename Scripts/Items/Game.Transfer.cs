using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lamplighter;

/// <summary>Предмет на земле: общая стопка мира. Принадлежит хосту (в одиночной игре — самому игроку); клиенты показывают копию.</summary>
public sealed class WorldDrop { public int Id, Item, Count; public Vector2 Pos; public long Owner; public float Born; }

/// <summary>Передача ресурсов между игроками, выбрасывание на землю и общий склад. Игрок сначала списывает у себя, потом просит хоста; хост проверяет отправителя, получателя, количество и дистанцию
/// и либо доставляет (RpcGrant), либо возвращает отправителю. Двойное нажатие видит уже уменьшенный запас, а разрыв связи в пути теряет предметы, но не дублирует их.
/// Подбор решает только хост: он убирает стопку и выдаёт её ближайшему игроку, поэтому один предмет не подобрать дважды.</summary>
public partial class Game
{
    public const float TransferRange = 112f, PickupRange = 14f, DropProtect = 2.0f;
    public const int MaxMove = 999, MaxStack = 999;
    public readonly List<WorldDrop> Drops = new();
    int nextDropId = 1; bool dropsDirty; float dropSyncT; double dropClock;

    bool Authority => !NetClient;                                                    // одиночная игра или хост
    public long LocalPeer => NetService.Active ? NetService.LocalId : 1;
    public static bool Movable(int item) => item >= 0 && item < It.Count;

    /// <summary>Позиция игрока по номеру соединения (хост знает всех).</summary>
    Vector2? PosOf(long peer)
    {
        if (peer == LocalPeer) return PPos;
        return NetService.Players.TryGetValue(peer, out var p) && p.Handshaken ? p.Pos : null;
    }

    public List<NetPlayer> NearbyPlayers()
    {
        var l = new List<NetPlayer>(); if (!NetService.Active) return l;
        foreach (var p in NetService.Players.Values) if (p.Handshaken && p.PeerId != LocalPeer && p.Shown.DistanceTo(PPos) <= TransferRange) l.Add(p);
        return l;
    }

    public bool NearChestFor(Vector2 pos) { foreach (var b in Buildings) if (b.Type == Tl.Warehouse && b.Pos.DistanceTo(pos) < 26 * TS) return true; return false; }

    /// <summary>Выдать ресурсы игроку: локально (я — получатель) или через хоста — только хост выдаёт предметы по сети.</summary>
    void GiveTo(long peer, int item, int n)
    {
        if (n <= 0 || !Movable(item)) return;
        if (peer == LocalPeer) { Inv[item] += n; return; }
        if (Net == null) return; var g = new int[It.Count]; g[item] = Math.Min(n, MaxMove); Net.RpcId(peer, nameof(NetNode.RpcGrant), g);
    }

    void NoteTo(long peer, string text, MsgTone tone)
    {
        if (peer == LocalPeer) Notify.Post(MsgKind.System, tone, text); else Net?.RpcId(peer, nameof(NetNode.RpcTransferNote), text, (int)tone);
    }

    string Nm(long peer) => peer == LocalPeer ? ChatName : NetService.Players.TryGetValue(peer, out var p) ? p.Name : "?";

    // ───────────────────────── прямая передача ─────────────────────────
    /// <summary>Передать n штук ресурса игроку toPeer. Возвращает false, если запрос не принят на месте (нет предметов, далеко, не тот игрок).</summary>
    public bool TrySend(long toPeer, int item, int n)
    {
        n = Math.Min(n, MaxMove); if (!Movable(item) || n < 1 || Inv[item] < n || toPeer == LocalPeer || PosOf(toPeer) is not Vector2 tp || tp.DistanceTo(PPos) > TransferRange) { Fail("Передача невозможна"); return false; }
        Inv[item] -= n;
        if (Authority) HostTransfer(LocalPeer, toPeer, item, n); else Net?.SendTransfer(toPeer, item, n);
        return true;
    }

    /// <summary>Решает хост. Атомарно: либо получатель получил n штук, либо отправитель получил их обратно.</summary>
    public void HostTransfer(long from, long to, int item, int n)
    {
        bool ok = Movable(item) && n >= 1 && n <= MaxMove && from != to && PosOf(from) is Vector2 fp && PosOf(to) is Vector2 tp && fp.DistanceTo(tp) <= TransferRange * 1.5f;
        if (!ok) { GiveTo(from, item, Movable(item) ? Math.Clamp(n, 0, MaxMove) : 0); NoteTo(from, Loc.T("Передача отклонена, предметы возвращены"), MsgTone.Warn); return; }
        GiveTo(to, item, n);
        string name = Loc.T(It.Names[item]);
        NoteTo(from, string.Format(Loc.T("Передано игроку {0}: {1} ×{2}"), Nm(to), name, n), MsgTone.Good);
        NoteTo(to, string.Format(Loc.T("{0} передал вам: {1} ×{2}"), Nm(from), name, n), MsgTone.Good);
        Sfx.Play("pickup", -4, 1.2f);
    }

    // ───────────────────────── предметы на земле ─────────────────────────
    public bool TryDrop(int item, int n)
    {
        n = Math.Min(n, MaxMove); if (!Movable(item) || n < 1 || Inv[item] < n) return false;
        Inv[item] -= n;
        if (Authority) HostDrop(LocalPeer, item, n); else Net?.SendDrop(item, n);
        Sfx.Play("tick", -4, .9f);
        return true;
    }

    /// <summary>Хост: новая стопка рядом с игроком или добавление к существующей такой же в пределах двух клеток.</summary>
    public void HostDrop(long owner, int item, int n)
    {
        if (!Movable(item) || n < 1 || n > MaxMove || PosOf(owner) is not Vector2 pp) return;
        var pos = pp + new Vector2(owner == LocalPeer ? Facing * 10f : 12f, 4f);
        foreach (var d in Drops) if (d.Item == item && d.Pos.DistanceTo(pos) < 20f && d.Count + n <= MaxStack) { d.Count += n; d.Owner = owner; d.Born = (float)dropClock; dropsDirty = true; return; }
        Drops.Add(new WorldDrop { Id = nextDropId++, Item = item, Count = n, Pos = pos, Owner = owner, Born = (float)dropClock }); dropsDirty = true;
    }

    /// <summary>Каждый кадр на хосте и в одиночной игре: подбор стопок игроками рядом и рассылка списка клиентам.</summary>
    void UpdateDrops(float dt)
    {
        dropClock += dt;
        if (Authority && Drops.Count > 0)
        {
            var peers = new List<long> { LocalPeer }; if (NetService.Active) foreach (var p in NetService.Players.Values) if (p.Handshaken && p.PeerId != LocalPeer) peers.Add(p.PeerId);
            for (int i = Drops.Count - 1; i >= 0; i--)
            {
                var d = Drops[i]; if (d.Count <= 0) { Drops.RemoveAt(i); dropsDirty = true; continue; }
                foreach (var peer in peers)
                {
                    if (peer == d.Owner && dropClock - d.Born < DropProtect) continue;        // выбросивший не подбирает свою стопку сразу
                    if (PosOf(peer) is not Vector2 pp || pp.DistanceTo(d.Pos) > PickupRange) continue;
                    Drops.RemoveAt(i); dropsDirty = true;                                    // стопка убрана до выдачи: второй раз её не подобрать
                    GiveTo(peer, d.Item, d.Count); NoteTo(peer, string.Format(Loc.T("Подобрано: {0} ×{1}"), Loc.T(It.Names[d.Item]), d.Count), MsgTone.Good); Sfx.Play("pickup", -4);
                    break;
                }
            }
        }
        dropSyncT -= dt;
        if (dropsDirty && dropSyncT <= 0 && NetService.Active && NetService.Role == NetRole.Host) { dropSyncT = .12f; dropsDirty = false; Net?.BroadcastDrops(0); }
        else if (dropsDirty && !NetService.Active) dropsDirty = false;
    }

    public (int[] ids, int[] items, int[] counts, float[] xs, float[] ys) DropSnapshot()
        => (Drops.Select(d => d.Id).ToArray(), Drops.Select(d => d.Item).ToArray(), Drops.Select(d => d.Count).ToArray(), Drops.Select(d => d.Pos.X).ToArray(), Drops.Select(d => d.Pos.Y).ToArray());

    /// <summary>Клиент: заменить список стопок присланным хостом (значения проверяются).</summary>
    public void ApplyDropSnapshot(int[] ids, int[] items, int[] counts, float[] xs, float[] ys)
    {
        if (ids == null || items == null || counts == null || xs == null || ys == null) return;
        int n = Math.Min(Math.Min(ids.Length, items.Length), Math.Min(counts.Length, Math.Min(xs.Length, ys.Length))); n = Math.Min(n, 200);
        Drops.Clear();
        for (int i = 0; i < n; i++) if (Movable(items[i]) && counts[i] > 0 && counts[i] <= MaxStack && float.IsFinite(xs[i]) && float.IsFinite(ys[i])) Drops.Add(new WorldDrop { Id = ids[i], Item = items[i], Count = counts[i], Pos = new Vector2(xs[i], ys[i]), Owner = -1 });
    }

    // ───────────────────────── общий склад ─────────────────────────
    /// <summary>Положить в склад (deposit) или взять из него. Склад принадлежит миру: меняет его только хост, все участники видят одно количество.</summary>
    public bool TryChest(bool deposit, int item, int n)
    {
        n = Math.Min(n, MaxMove); if (!Movable(item) || item == It.Coins || n < 1 || !NearChestFor(PPos)) { Fail("Склад далеко"); return false; }
        if (deposit) { if (Inv[item] < n) return false; Inv[item] -= n; }
        else if (Authority && Stock[item] < 1) return false;
        if (Authority) HostChest(LocalPeer, deposit, item, n); else Net?.SendChest(deposit, item, n);
        return true;
    }

    public void HostChest(long who, bool deposit, int item, int n)
    {
        bool ok = Movable(item) && item != It.Coins && n >= 1 && n <= MaxMove && PosOf(who) is Vector2 pp && NearChestFor(pp);
        if (!ok) { if (deposit) GiveTo(who, item, Movable(item) ? Math.Clamp(n, 0, MaxMove) : 0); return; }
        if (deposit) { int add = Math.Min(n, Math.Max(0, StockCap - Stock[item])); Stock[item] += add; if (add < n) { GiveTo(who, item, n - add); NoteTo(who, Loc.T("Склад полон"), MsgTone.Warn); } }
        else { int take = Math.Min(n, Stock[item]); if (take > 0) { Stock[item] -= take; GiveTo(who, item, take); } }
        netT = 0;                                                                         // состояние склада сразу уходит клиентам
    }
}
