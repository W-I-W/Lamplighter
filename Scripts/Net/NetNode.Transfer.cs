using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Lamplighter;

/// <summary>Сетевая часть передачи ресурсов, выбрасывания и общего склада. Клиент только просит, решает хост (Game.HostTransfer/HostDrop/HostChest); хост проверяет отправителя по соединению,
/// ограничивает частоту и размеры, а предметы выдаёт единственным путём RpcGrant. Список стопок на земле хост присылает целиком.</summary>
public partial class NetNode
{
    readonly Dictionary<long, (double last, double winStart, int n)> moveRate = new();

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

    public void SendTransfer(long to, int item, int n) { if (Multiplayer.MultiplayerPeer != null) RpcId(1, nameof(RpcTransfer), to, item, n); }
    public void SendDrop(int item, int n) { if (Multiplayer.MultiplayerPeer != null) RpcId(1, nameof(RpcDrop), item, n); }
    public void SendChest(bool deposit, int item, int n) { if (Multiplayer.MultiplayerPeer != null) RpcId(1, nameof(RpcChest), deposit, item, n); }

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
        G.HostDrop(s, item, n);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcChest(bool deposit, int item, int n)
    {
        if (!MoveSender(out var s)) { if (deposit && Multiplayer.IsServer()) RefundRejected(Multiplayer.GetRemoteSenderId(), item, n); return; }
        G.HostChest(s, deposit, item, n);
        BroadcastVillage();
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
