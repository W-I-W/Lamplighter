using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Lamplighter;

/// <summary>Сетевая часть передачи ресурсов, выбрасывания и общего склада. Клиент только просит, решает хост (Game.HostTransfer/HostDrop/HostChest); хост проверяет отправителя по соединению,
/// ограничивает частоту и размеры, а предметы выдаёт единственным путём RpcGrant. Список стопок на земле хост присылает целиком.</summary>
public partial class NetNode
{
    readonly Dictionary<long, (double last, double winStart, int n)> moveRate = new();
    readonly Dictionary<long, (WorldDrop drop, double at)> pendingPickups = new();
    const double PickupReceiptTimeout = 5.0;          // квитанция не пришла (гость перезагружал сцену и RPC потерялся): запрос забывается, иначе гость больше никогда ничего не подберёт

    public void RequestPickup(int id, int count)
    {
        if (Multiplayer.MultiplayerPeer != null) RpcId(1, nameof(RpcRequestPickup), id, count);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcRequestPickup(int id, int count)
    {
        long peer = Multiplayer.GetRemoteSenderId();
        if (!Multiplayer.IsServer() || !NetService.Players.TryGetValue(peer, out var player) || !player.Handshaken || count <= 0 || count > Game.MaxStack) return;
        if (pendingPickups.TryGetValue(peer, out var pend))
        {
            if (now - pend.at < PickupReceiptTimeout) return;
            pendingPickups.Remove(peer);                // предметы считаются выданными: вернуть их на землю нельзя — гость мог их всё-таки получить (дюп)
        }
        var taken = G.TakeGuestPickup(peer, id, count);
        if (taken == null) return;
        pendingPickups.Add(peer, (taken, now));
        G.QueueNetSave();
        RpcId(peer, nameof(RpcPickupGrant), taken.Id, taken.Item, taken.Count);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcPickupGrant(int id, int item, int count)
    {
        if (Multiplayer.GetRemoteSenderId() != 1 || !Game.Movable(item) || count <= 0 || count > Game.MaxStack) return;
        int remainder = G.ReceiveGuestPickup(item, count);
        RpcId(1, nameof(RpcPickupReceipt), id, remainder);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcPickupReceipt(int id, int remainder)
    {
        long peer = Multiplayer.GetRemoteSenderId();
        if (!Multiplayer.IsServer() || !pendingPickups.TryGetValue(peer, out var pend) ||
            pend.drop.Id != id || remainder < 0 || remainder > pend.drop.Count) return;
        var taken = pend.drop; pendingPickups.Remove(peer);
        if (remainder > 0) G.RestoreGuestPickup(taken, remainder);
    }

    bool MoveRateOk(long id)
    {
        moveRate.TryGetValue(id, out var r); if (now - r.winStart >= 5) { r.winStart = now; r.n = 0; }
        if (now - r.last < .12 || r.n >= 30) { moveRate[id] = r; return false; }          // не чаще 8 раз в секунду и 30 за пять секунд
        r.last = now; r.n++; moveRate[id] = r; return true;
    }

    bool MoveSender(out long sender)
    {
        sender = Multiplayer.GetRemoteSenderId();
        return Multiplayer.IsServer() && NetService.Players.TryGetValue(sender, out var p) && p.Handshaken && MoveRateOk(sender);
    }

    // Гость уже списал предметы у себя: герой сохраняется ДО отправки. Иначе, если игра упадёт после того, как хост выдал предметы получателю, но до автосохранения,
    // старое сохранение гостя вернуло бы ему отданное (дюп). Упасть между сохранением и отправкой — значит потерять предметы, но не удвоить их.
    public void SendTransfer(long to, int item, int n) { if (Multiplayer.MultiplayerPeer == null) return; G?.Save(); RpcId(1, nameof(RpcTransfer), to, item, n); }
    public void SendDrop(int item, int n) { if (Multiplayer.MultiplayerPeer == null) return; G?.Save(); RpcId(1, nameof(RpcDrop), item, n); }
    public void SendDropRelic(int idx) { if (Multiplayer.MultiplayerPeer == null) return; G?.Save(); RpcId(1, nameof(RpcDropRelic), idx); }
    public void SendChest(bool deposit, int item, int n) { if (Multiplayer.MultiplayerPeer == null) return; if (deposit) G?.Save(); RpcId(1, nameof(RpcChest), deposit, item, n); }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcTransfer(long to, int item, int n)
    {
        if (!MoveSender(out var s)) { if (Multiplayer.IsServer()) RefundRejected(Multiplayer.GetRemoteSenderId(), item, n); return; }
        G.HostTransfer(s, to, item, n);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcDrop(int item, int n)
    {
        if (!MoveSender(out var s)) { if (Multiplayer.IsServer()) RefundRejected(Multiplayer.GetRemoteSenderId(), item, n); return; }
        if (!Game.Movable(item)) return;                                                   // реликвии и постройки на землю гость выбрасывает только своими командами, иначе можно было бы создать их из воздуха
        G.HostDrop(s, item, n);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcDropRelic(int idx)
    {
        if (idx < 0 || idx >= Db.Relics.Count) return;
        if (!MoveSender(out var s))
        {
            long r = Multiplayer.GetRemoteSenderId();                                  // гость уже снял реликвию у себя: при отказе она возвращается, а не пропадает
            if (Multiplayer.IsServer() && NetService.Players.TryGetValue(r, out var rp) && rp.Handshaken) RpcId(r, nameof(RpcGrantRelic), idx);
            return;
        }
        G.HostDrop(s, Game.RelicBase + idx, 1);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcGrantRelic(int idx)
    {
        if (Multiplayer.GetRemoteSenderId() != 1 || idx < 0 || idx >= Db.Relics.Count) return;
        G.GiveRelic(Db.Relics[idx].Id);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcChest(bool deposit, int item, int n)
    {
        if (!MoveSender(out var s)) { if (deposit && Multiplayer.IsServer()) RefundRejected(Multiplayer.GetRemoteSenderId(), item, n); return; }
        G.HostChest(s, deposit, item, n);
        G.QueueVillageSync();
    }

    /// <summary>Запрос отклонён до проверок (слишком часто, неизвестный отправитель): списанное у отправителя возвращается, если он известен хосту.</summary>
    void RefundRejected(long sender, int item, int n)
    {
        if (sender == 0 || !NetService.Players.TryGetValue(sender, out var p) || !p.Handshaken || !Game.Movable(item) || n < 1) return;
        var g = new int[It.Count]; g[item] = System.Math.Min(n, Game.MaxMove); RpcId(sender, nameof(RpcGrant), g);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcTransferNote(string text, int tone)
    {
        if (Multiplayer.GetRemoteSenderId() != 1) return;
        G.Notify.Post(MsgKind.System, (MsgTone)Mathf.Clamp(tone, 0, 5), text);
    }

    public void BroadcastDrops(long only)
    {
        if (!Multiplayer.IsServer()) return; var s = G.DropSnapshot();
        foreach (var t in NetService.Players.Values.Where(x => x.Handshaken && x.PeerId != 1 && (only == 0 || x.PeerId == only)))
            RpcId(t.PeerId, nameof(RpcDrops), s.ids, s.items, s.counts, s.xs, s.ys);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcDrops(int[] ids, int[] items, int[] counts, float[] xs, float[] ys)
    {
        if (Multiplayer.GetRemoteSenderId() != 1) return;
        G.ApplyDropSnapshot(ids, items, counts, xs, ys);
    }
}
