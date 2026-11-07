using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lamplighter;

/// <summary>Предмет на земле: общая стопка мира. Принадлежит хосту (в одиночной игре — самому игроку); клиенты показывают копию.</summary>
public sealed class WorldDrop { public int Id, Item, Count; public Vector2 Pos; public long Owner; public float Born; public Vector2 Vel; }          // Vel — только хост: «выпрыгивание» новой добычи, по сети не передаётся

/// <summary>Передача ресурсов между игроками, выбрасывание на землю и общий склад. Игрок сначала списывает у себя, потом просит хоста; хост проверяет отправителя, получателя, количество и дистанцию
/// и либо доставляет (RpcGrant), либо возвращает отправителю. Двойное нажатие видит уже уменьшенный запас, а разрыв связи в пути теряет предметы, но не дублирует их.
/// Подбор решает только хост: он убирает стопку и выдаёт её ближайшему игроку, поэтому один предмет не подобрать дважды.</summary>
public partial class Game
{
    public const float TransferRange = 112f, PickupRange = 14f, DropProtect = 2.0f;
    public const float MagnetRange = 19f, MagnetMin = 30f, MagnetMax = 85f, MagnetDelay = .45f;                  // слабый магнит: тянет только совсем рядом и медленно, после «выпрыгивания»
    public const int MaxMove = 999, MaxStack = 999;
    public readonly List<WorldDrop> Drops = new();
    int nextDropId = 1; bool dropsDirty; float dropSyncT, pickupRequestT; double dropClock;

    bool Authority => !NetClient;                                                    // одиночная игра или хост
    public long LocalPeer => NetService.Active ? NetService.LocalId : 1;
    public static bool Movable(int item) => item >= 0 && item < It.Count;
    public const int RelicBase = 1000;                                              // реликвия на земле: Item = RelicBase + номер в Db.Relics, Count = 1
    public static bool IsRelicDrop(int item) => item >= RelicBase && item < RelicBase + 400;
    public static RelicDef RelicOfDrop(int item) => IsRelicDrop(item) && item - RelicBase < Db.Relics.Count ? Db.Relics[item - RelicBase] : null;

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
        if (peer == LocalPeer) { AddInv(item, n, PPos); return; }
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

    /// <summary>Выбросить реликвию на землю: её может подобрать другой игрок. Надетая снимается.</summary>
    public bool TryDropRelic(string id)
    {
        int idx = Db.Relics.FindIndex(r => r.Id == id); if (idx < 0 || !OwnedRelics.Contains(id)) return false;
        OwnedRelics.Remove(id); EquippedRelics.Remove(id); RecomputeEffects(); WinCur = Math.Clamp(WinCur, 0, Math.Max(0, OwnedRelics.Count - 1));
        if (Authority) HostDrop(LocalPeer, RelicBase + idx, 1); else Net?.SendDropRelic(idx);
        Sfx.Play("tick", -4, .9f); Toast($"Выброшено: {Db.Relics[idx].Title}"); return true;
    }

    /// <summary>Удаление ресурса из инвентаря насовсем (без выпадения на землю).</summary>
    public bool TryDelete(int item, int n)
    {
        n = Math.Min(n, MaxMove); if (!Movable(item) || n < 1 || Inv[item] < n) return false;
        Inv[item] -= n; Toast($"Удалено: {Loc.T(It.Names[item])} ×{n}"); Sfx.Play("tick", -4, .7f); return true;
    }

    /// <summary>Хост: новая стопка рядом с игроком или добавление к существующей такой же в пределах двух клеток.</summary>
    public void HostDrop(long owner, int item, int n)
    {
        bool relic = IsRelicDrop(item) && RelicOfDrop(item) != null; if (relic) n = 1;
        if ((!Movable(item) && !relic) || n < 1 || n > MaxMove || PosOf(owner) is not Vector2 pp) return;
        HostDropAt(owner, item, n, pp + new Vector2(owner == LocalPeer ? Facing * 10f : 12f, 4f), Vector2.Zero);
    }

    /// <summary>Хост: стопка в заданной точке (добыча из блоков и с врагов: Owner = -1, подобрать может любой). vel — начальный «прыжок» стопки.</summary>
    public void HostDropAt(long owner, int item, int n, Vector2 pos, Vector2 vel)
    {
        bool relic = IsRelicDrop(item) && RelicOfDrop(item) != null; if (relic) n = 1;
        if ((!Movable(item) && !relic) || n < 1 || n > MaxStack) return;
        if (!relic) foreach (var d in Drops) if (d.Item == item && d.Pos.DistanceTo(pos) < 20f && d.Count + n <= MaxStack && (vel == Vector2.Zero || dropClock - d.Born > MagnetDelay)) { d.Count += n; d.Owner = owner; d.Born = (float)dropClock; dropsDirty = true; return; }       // добыча сливается только с уже улёгшейся стопкой
        Drops.Add(new WorldDrop { Id = nextDropId++, Item = item, Count = n, Pos = pos, Owner = owner, Born = (float)dropClock, Vel = vel }); dropsDirty = true;
    }

    /// <summary>Добытое из блока или выпавшее с врага падает на землю рядом и лежит, пока игрок не подберёт. Не хост (гость): возвращает false, и добыча выдаётся сразу, как раньше.</summary>
    bool SpawnLoot(int item, int n, Vector2 at)
    {
        if (!Authority || testing || !Movable(item) || n < 1) return false;
        while (n > 0) { int k = Math.Min(n, MaxStack); n -= k; HostDropAt(-1, item, k, at + new Vector2(rng.RandfRange(-3, 3), rng.RandfRange(-2, 3)), Vector2.FromAngle(rng.Randf() * Mathf.Tau) * rng.RandfRange(35, 65)); }
        return true;
    }

    public float DropAge(WorldDrop d) => (float)(dropClock - d.Born);

    void RequestNearbyDrop(float dt)
    {
        pickupRequestT -= dt;
        if (pickupRequestT > 0 || Net == null) return;
        pickupRequestT = .15f;
        foreach (var drop in Drops)
        {
            if (!Movable(drop.Item) || drop.Pos.DistanceTo(PPos) > PickupRange) continue;
            int count = Math.Min(drop.Count, Room(drop.Item));
            if (count == 0) { FullMsg(); continue; }
            Net.RequestPickup(drop.Id, count);
            break;
        }
    }

    internal WorldDrop TakeGuestPickup(long peer, int id, int requested)
    {
        var drop = Drops.Find(d => d.Id == id);
        if (drop == null || !Movable(drop.Item) || requested <= 0 ||
            PosOf(peer) is not Vector2 pos || pos.DistanceTo(drop.Pos) > PickupRange ||
            (peer == drop.Owner && DropAge(drop) < DropProtect)) return null;

        int count = Math.Min(requested, drop.Count);
        var taken = new WorldDrop { Id = drop.Id, Item = drop.Item, Count = count, Pos = drop.Pos, Owner = drop.Owner, Born = drop.Born };
        drop.Count -= count;
        if (drop.Count == 0) Drops.Remove(drop);
        dropsDirty = true;
        return taken;
    }

    internal int ReceiveGuestPickup(int item, int count)
    {
        int accepted = Math.Min(count, Room(item));
        Inv[item] += accepted;
        if (accepted < count) FullMsg();
        if (accepted > 0) Sfx.Play("pickup", -6, 1f + .04f * (item % 6));
        return count - accepted;
    }

    internal void RestoreGuestPickup(WorldDrop taken, int remainder)
    {
        var drop = Drops.Find(d => d.Id == taken.Id);
        if (drop != null)
        {
            int restored = Math.Min(remainder, MaxStack - drop.Count);
            drop.Count += restored;
            remainder -= restored;
            taken.Id = nextDropId++;
        }
        if (remainder > 0) { taken.Count = remainder; Drops.Add(taken); }
        dropsDirty = true;
    }

    /// <summary>Каждый кадр на хосте и в одиночной игре: подбор стопок игроками рядом и рассылка списка клиентам.</summary>
    void UpdateDrops(float dt)
    {
        dropClock += dt;
        if (!Authority) { RequestNearbyDrop(dt); return; }
        if (Drops.Count > 0)
        {
            var peers = new List<long> { LocalPeer }; if (NetService.Active) foreach (var p in NetService.Players.Values) if (p.Handshaken && p.PeerId != LocalPeer) peers.Add(p.PeerId);
            for (int i = Drops.Count - 1; i >= 0; i--)
            {
                var d = Drops[i]; if (d.Count <= 0) { Drops.RemoveAt(i); dropsDirty = true; continue; }
                if (d.Vel.LengthSquared() > 4f) { d.Pos += d.Vel * dt; d.Vel *= Mathf.Exp(-9f * dt); dropsDirty = true; }                       // «выпрыгивание» новой добычи
                else if (!IsRelicDrop(d.Item) && dropClock - d.Born > MagnetDelay)
                {
                    d.Vel = Vector2.Zero; Vector2? to = null; float bd = MagnetRange;
                    foreach (var peer in peers) { if (peer == d.Owner && dropClock - d.Born < DropProtect) continue; if (PosOf(peer) is Vector2 mp) { float md = mp.DistanceTo(d.Pos); if (md < bd && (peer != LocalPeer || Room(d.Item) > 0)) { bd = md; to = mp; } } }
                    if (to is Vector2 tp && bd > PickupRange * .5f) { float sp = MagnetMin + (MagnetMax - MagnetMin) * (1f - bd / MagnetRange); d.Pos += (tp - d.Pos).Normalized() * Math.Min(sp * dt, bd); dropsDirty = true; }
                }
                foreach (var peer in peers)
                {
                    if (peer == d.Owner && dropClock - d.Born < DropProtect) continue;        // выбросивший не подбирает свою стопку сразу
                    if (PosOf(peer) is not Vector2 pp || pp.DistanceTo(d.Pos) > PickupRange) continue;
                    if (IsRelicDrop(d.Item))
                    {
                        var rd = RelicOfDrop(d.Item); if (rd == null) { Drops.RemoveAt(i); dropsDirty = true; break; }
                        if (peer == LocalPeer && OwnedRelics.Contains(rd.Id)) continue;     // такая реликвия уже есть: остаётся лежать для другого игрока
                        Drops.RemoveAt(i); dropsDirty = true;
                        if (peer == LocalPeer) GiveRelic(rd.Id); else Net?.RpcId(peer, nameof(NetNode.RpcGrantRelic), d.Item - RelicBase);
                        Sfx.Play("pickup", -4); break;
                    }
                    if (peer != LocalPeer) continue;                                         // гость запрашивает только то количество, которое помещается у него
                    // В инвентаре нет места: стопка остаётся лежать (частично — берётся сколько влезает).
                    int room = Room(d.Item); if (room <= 0) { FullMsg(); continue; }
                    if (room < d.Count) { d.Count -= room; dropsDirty = true; Inv[d.Item] += room; Sfx.Play("pickup", -6, 1f + .04f * (d.Item % 6)); break; }
                    Drops.RemoveAt(i); dropsDirty = true;                                    // стопка убрана до выдачи: второй раз её не подобрать
                    GiveTo(peer, d.Item, d.Count); Sfx.Play("pickup", -6, 1f + .04f * (d.Item % 6));
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
        for (int i = 0; i < n; i++) if ((Movable(items[i]) || RelicOfDrop(items[i]) != null) && counts[i] > 0 && counts[i] <= MaxStack && float.IsFinite(xs[i]) && float.IsFinite(ys[i])) Drops.Add(new WorldDrop { Id = ids[i], Item = items[i], Count = counts[i], Pos = new Vector2(xs[i], ys[i]), Owner = -1 });
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
