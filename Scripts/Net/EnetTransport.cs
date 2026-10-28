using Godot;
using System;

namespace Lamplighter;

/// <summary>Транспорт ENet: локальная сеть и разработка без Steam. Использует ту же игровую сетевую архитектуру, но НЕ подтверждает работу Steam P2P.</summary>
public sealed class EnetTransport : INetTransport
{
    ENetMultiplayerPeer peer; string invite = "";
    public string Id => "enet";
    public string DisplayName => "Локальная сеть (ENet)";
    public MultiplayerPeer Peer => peer;
    public string InviteInfo => invite;
    public event Action Ready;
    public event Action<string> Failed;

    public bool IsAvailable(out string reason) { reason = ""; return true; }

    public Error StartHost(NetHostOptions o)
    {
        peer = new ENetMultiplayerPeer(); var err = peer.CreateServer(o.Port, Math.Max(1, o.MaxPlayers - 1));
        if (err != Error.Ok) { Failed?.Invoke($"Не удалось открыть порт {o.Port} ({err})"); peer = null; return err; }
        invite = $"{o.Port}"; Ready?.Invoke(); return Error.Ok;
    }

    public Error StartClient(string target)
    {
        string host = target.Trim(); int port = NetConst.DefaultPort;
        int c = host.LastIndexOf(':'); if (c > 0 && int.TryParse(host.Substring(c + 1), out var p)) { port = p; host = host.Substring(0, c); }
        if (host == "") host = "127.0.0.1";
        peer = new ENetMultiplayerPeer(); var err = peer.CreateClient(host, port);
        if (err != Error.Ok) { Failed?.Invoke($"Не удалось подключиться к {host}:{port} ({err})"); peer = null; return err; }
        Ready?.Invoke(); return Error.Ok;
    }

    public void Close() { peer?.Close(); peer = null; invite = ""; }
    public void Poll(double dt) { }
    public void ClearHandlers() { Ready = null; Failed = null; }
    public ulong UserIdOf(long peerId) => 0;
    public string UserNameOf(long peerId) => "";
}
