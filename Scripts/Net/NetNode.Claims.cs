using Godot;
using System.Linq;

namespace Lamplighter;

/// <summary>Заявки гостя на разовую добычу мира: разбитый блок (руда, дерево, камень, куст) и место интереса с наградой (сундук, святилище, обелиск, тайник, шпиль).
/// Гость не берёт такую добычу сам: он спрашивает хоста, и хост отвечает «да» только первому — блок или место в этот момент забирается у всех. Без этого два игрока,
/// ударившие один блок или открывшие один сундук в пределах задержки синхронизации, получали добычу оба (дюп).</summary>
public partial class NetNode
{
    public void SendMineClaim(int x, int y, int code) { if (Multiplayer.MultiplayerPeer != null) RpcId(1, nameof(RpcMineClaim), x, y, code); }
    public void SendPoiClaim(int idx) { if (Multiplayer.MultiplayerPeer != null) RpcId(1, nameof(RpcPoiClaim), idx); }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcMineClaim(int x, int y, int code)
    {
        if (!Multiplayer.IsServer()) return; long s = Multiplayer.GetRemoteSenderId();
        if (!NetService.Players.TryGetValue(s, out var p) || !p.Handshaken || !WorldRateOk(s)) return;
        bool ok = G.HostMineClaim(s, x, y, code);
        RpcId(s, nameof(RpcMineResult), x, y, code, ok);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcMineResult(int x, int y, int code, bool ok) { if (Multiplayer.GetRemoteSenderId() == 1) G.NetMineResult(x, y, code, ok); }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcPoiClaim(int idx)
    {
        if (!Multiplayer.IsServer()) return; long s = Multiplayer.GetRemoteSenderId();
        if (!NetService.Players.TryGetValue(s, out var p) || !p.Handshaken || !WorldRateOk(s)) return;
        RpcId(s, nameof(RpcPoiResult), idx, G.HostPoiClaim(s, idx));
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcPoiResult(int idx, bool ok) { if (Multiplayer.GetRemoteSenderId() == 1) G.NetPoiResult(idx, ok); }

    /// <summary>Хост: изменение клетки, принятое не через общий слепок (срубленный куст), — всем гостям, кроме того, кто его сделал.</summary>
    public void RelayTile(long except, int x, int y, int t, int placed)
    {
        if (!Multiplayer.IsServer()) return;
        foreach (var o in NetService.Players.Values.Where(q => q.Handshaken && q.PeerId != 1 && q.PeerId != except)) RpcId(o.PeerId, nameof(RpcTile), x, y, t, placed);
    }

    /// <summary>Хост: вернуть одному гостю настоящее состояние клетки (его заявка отклонена, а у него клетка уже пустая).</summary>
    public void SendTileTo(long peer, int x, int y, int t, int placed) { if (Multiplayer.IsServer() && peer != 1) RpcId(peer, nameof(RpcTile), x, y, t, placed); }
}
