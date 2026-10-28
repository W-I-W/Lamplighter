using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lamplighter;

/// <summary>Сетевой узел игры: RPC и связь с Godot High-Level Multiplayer. Хост — единственный авторитет: принимает рукопожатие, проверяет версию, число игроков и частоту запросов,
/// ведёт список участников и рассылает позиции. Клиенты шлют только свой ввод/состояние. Транспорт (ENet, Steam) подключается как MultiplayerPeer через NetService.</summary>
public partial class NetNode : Node
{
    public Game G;
    double now, snapT, sendT; readonly Dictionary<long, (double start, int n)> rate = new();
    const int MaxStatePerSec = 40;

    public override void _EnterTree()
    {
        NetService.Node = this;
        Multiplayer.PeerConnected += OnPeerConnected; Multiplayer.PeerDisconnected += OnPeerDisconnected;
        Multiplayer.ConnectedToServer += OnConnectedToServer; Multiplayer.ConnectionFailed += OnConnectionFailed; Multiplayer.ServerDisconnected += OnServerDisconnected;
        if (NetService.Transport?.Peer != null && Multiplayer.MultiplayerPeer != NetService.Transport.Peer) AttachPeer();      // перезагрузка сцены: сеанс продолжается
    }

    public override void _ExitTree()
    {
        Multiplayer.PeerConnected -= OnPeerConnected; Multiplayer.PeerDisconnected -= OnPeerDisconnected;
        Multiplayer.ConnectedToServer -= OnConnectedToServer; Multiplayer.ConnectionFailed -= OnConnectionFailed; Multiplayer.ServerDisconnected -= OnServerDisconnected;
        if (NetService.Node == this) NetService.Node = null;
    }

    public void AttachPeer()
    {
        var t = NetService.Transport; if (t?.Peer == null) return;
        Multiplayer.MultiplayerPeer = t.Peer;
        if (NetService.Role == NetRole.Host)
        {
            NetService.LocalId = 1; NetService.State = NetState.Online; NetService.Status = "Игра открыта";
            if (!NetService.Players.ContainsKey(1)) NetService.Players[1] = new NetPlayer { PeerId = 1, Name = NetService.LocalName, Handshaken = true, UserId = t.UserIdOf(1), Hp = G?.Hp ?? 100, MaxHp = G?.MaxHp ?? 100, Pos = G?.PPos ?? default, Shown = G?.PPos ?? default };
        }
    }

    public void DetachPeer() { try { Multiplayer.MultiplayerPeer = new OfflineMultiplayerPeer(); } catch { } }

    // ───────────────────────── события сети ─────────────────────────
    void OnPeerConnected(long id)
    {
        if (!Multiplayer.IsServer()) return;
        NetService.Players[id] = new NetPlayer { PeerId = id, Joined = now, Name = "…", UserId = NetService.Transport?.UserIdOf(id) ?? 0 };     // ждём рукопожатие
    }

    void OnPeerDisconnected(long id)
    {
        if (!Multiplayer.IsServer()) return;
        rate.Remove(id);
        if (NetService.Players.Remove(id, out var p)) { if (p.Handshaken) { NetService.RaiseNotice($"{p.Name} отключился"); NetService.RaiseLeft(p); BroadcastRoster(); } }
    }

    void OnConnectedToServer()
    {
        NetService.LocalId = Multiplayer.GetUniqueId(); NetService.State = NetState.Handshaking; NetService.Status = "Проверка версии…";
        RpcId(1, nameof(RpcHello), NetConst.Protocol, NetService.VersionOverride ?? Game.VersionStr, NetConst.GenVersion, NetService.LocalName);
    }

    void OnConnectionFailed() { NetService.Fail("Не удалось подключиться к хосту"); DetachPeer(); }

    void OnServerDisconnected()
    {
        NetService.RaiseNotice("Соединение с хостом потеряно");
        NetService.Leave(); DetachPeer();
    }

    // ───────────────────────── рукопожатие ─────────────────────────
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcHello(string protocol, string version, int gen, string name)
    {
        if (!Multiplayer.IsServer()) return;
        long id = Multiplayer.GetRemoteSenderId();
        if (!NetService.Players.TryGetValue(id, out var p) || p.Handshaken) return;
        string why = null;
        if (protocol != NetConst.Protocol || version != Game.VersionStr) why = $"Несовместимая версия игры (у хоста {Game.VersionStr})";
        else if (gen != NetConst.GenVersion) why = "Несовместимая версия генерации мира";
        else if (NetService.Players.Values.Count(x => x.Handshaken) >= NetConst.MaxPlayers) why = "Игра заполнена";
        if (why != null) { RpcId(id, nameof(RpcReject), why); GetTree().CreateTimer(.4).Timeout += () => Multiplayer.MultiplayerPeer?.DisconnectPeer((int)id); NetService.Players.Remove(id); return; }
        p.Name = CleanName(name, id); p.Handshaken = true; p.LastRecv = now; p.Hp = 100; p.MaxHp = 100; p.Pos = p.Shown = G.PPos;
        RpcId(id, nameof(RpcWelcome), G.W.Seed, NetConst.GenVersion, (long)id, G.PPos.X, G.PPos.Y, BuildRoster());
        NetService.RaiseNotice($"{p.Name} присоединился"); NetService.RaiseJoined(p); BroadcastRoster();
    }

    static string CleanName(string n, long id)
    {
        n = (n ?? "").Trim(); if (n.Length > 20) n = n.Substring(0, 20);
        var chars = n.Where(c => !char.IsControl(c) && c != '[' && c != ']').ToArray(); n = new string(chars);
        return n == "" ? $"Игрок {id % 1000}" : n;
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcReject(string reason) { NetService.Fail(reason); DetachPeer(); }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcWelcome(int seed, int gen, long yourId, float hx, float hy, Godot.Collections.Array roster)
    {
        NetService.LocalId = yourId; NetService.WorldSeed = seed; NetService.WorldGen = gen; NetService.SpawnPos = new Vector2(hx, hy); NetService.WorldPending = true;
        NetService.State = NetState.Online; NetService.Status = "Подключено";
        ApplyRoster(roster); NetService.RaiseNotice("Подключено к игре");
    }

    Godot.Collections.Array BuildRoster()
    {
        var a = new Godot.Collections.Array();
        foreach (var p in NetService.Players.Values.Where(x => x.Handshaken)) a.Add(new Godot.Collections.Dictionary { ["id"] = p.PeerId, ["name"] = p.Name, ["user"] = (long)p.UserId });
        return a;
    }

    void BroadcastRoster() { if (Multiplayer.IsServer()) Rpc(nameof(RpcRoster), BuildRoster()); }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcRoster(Godot.Collections.Array roster) { ApplyRoster(roster); }

    void ApplyRoster(Godot.Collections.Array roster)
    {
        var ids = new HashSet<long>();
        foreach (var v in roster)
        {
            var d = v.AsGodotDictionary(); long id = (long)d["id"]; ids.Add(id);
            if (!NetService.Players.TryGetValue(id, out var p)) { p = new NetPlayer { PeerId = id, Handshaken = true, Joined = now }; NetService.Players[id] = p; if (id != NetService.LocalId) NetService.RaiseJoined(p); }
            p.Name = (string)d["name"]; p.UserId = (ulong)(long)d["user"]; p.Handshaken = true;
        }
        foreach (var id in NetService.Players.Keys.ToList()) if (!ids.Contains(id)) { var p = NetService.Players[id]; NetService.Players.Remove(id); NetService.RaiseLeft(p); if (id != NetService.LocalId) NetService.RaiseNotice($"{p.Name} отключился"); }
    }

    // ───────────────────────── состояние персонажей ─────────────────────────
    /// <summary>Клиент → хост: собственное состояние (позиция, взгляд, здоровье, флаги). Ненадёжная упорядоченная доставка: потеря пакета не страшна.</summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    public void RpcState(float x, float y, int face, float hp, float maxHp, int flags)
    {
        if (!Multiplayer.IsServer()) return;
        long id = Multiplayer.GetRemoteSenderId();
        if (!NetService.Players.TryGetValue(id, out var p) || !p.Handshaken) return;
        if (!RateOk(id)) return;                                                         // ограничение частоты запросов
        if (!float.IsFinite(x) || !float.IsFinite(y)) return;
        float maxX = World.Wd * 16f, maxY = World.Ht * 16f; x = Mathf.Clamp(x, 0, maxX); y = Mathf.Clamp(y, 0, maxY);
        double dt = Math.Max(.02, now - p.LastRecv); var np = new Vector2(x, y);
        if (p.LastRecv > 0 && p.Pos.DistanceTo(np) / dt > 1200f) { return; }             // слишком быстрое перемещение — состояние отвергается (позиция остаётся прежней)
        p.Pos = np; p.Face = face < 0 ? -1 : 1; p.MaxHp = Mathf.Clamp(float.IsFinite(maxHp) ? maxHp : 100, 1, 1000); p.Hp = Mathf.Clamp(float.IsFinite(hp) ? hp : p.MaxHp, 0, p.MaxHp); p.Flags = (byte)(flags & 3); p.LastRecv = now;
    }

    bool RateOk(long id)
    {
        rate.TryGetValue(id, out var r); if (now - r.start >= 1) r = (now, 0);
        r.n++; rate[id] = r; return r.n <= MaxStatePerSec;
    }

    /// <summary>Хост → клиент: снимок позиций остальных участников (без самого получателя).</summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    public void RpcSnapshot(Godot.Collections.Array<float> data)
    {
        for (int i = 0; i + 6 < data.Count; i += 7)
        {
            long id = (long)data[i]; if (id == NetService.LocalId) continue;
            if (!NetService.Players.TryGetValue(id, out var p)) continue;
            bool first = p.LastRecv <= 0; p.Pos = new Vector2(data[i + 1], data[i + 2]); p.Face = (int)data[i + 3]; p.Hp = data[i + 4]; p.MaxHp = data[i + 5]; p.Flags = (byte)(int)data[i + 6]; p.LastRecv = now;
            if (first) p.Shown = p.Pos;
        }
    }

    // ───────────────────────── цикл ─────────────────────────
    public override void _Process(double delta)
    {
        now += delta;
        if (NetService.Transport != null) NetService.Transport.Poll(delta); else NetService.PollIdle(delta);
        if (NetService.Role == NetRole.Offline || G == null) return;
        // игрок: интерполяция удалённых персонажей
        foreach (var p in NetService.Players.Values)
        {
            if (p.PeerId == NetService.LocalId) continue;
            p.PrevShown = p.Shown; p.Shown = p.Shown.Lerp(p.Pos, 1f - Mathf.Exp(-14f * (float)delta));
            if (p.Shown.DistanceTo(p.Pos) > 160f) p.Shown = p.Pos;                          // телепорт/возрождение — без «скольжения»
            if (p.Shown.DistanceSquaredTo(p.PrevShown) > .02f) p.Walk += (float)delta * 9f;
        }
        sendT -= (float)delta; if (sendT > 0) return; sendT = 1f / NetConst.StateHz;
        var local = NetService.Local;
        if (NetService.Role == NetRole.Client && NetService.State == NetState.Online && Multiplayer.MultiplayerPeer != null && Multiplayer.GetUniqueId() != 1)
            RpcId(1, nameof(RpcState), G.PPos.X, G.PPos.Y, G.Facing, G.Hp, G.MaxHp, G.NetFlags());
        else if (NetService.Role == NetRole.Host && Multiplayer.IsServer())
        {
            if (local != null) { local.Pos = G.PPos; local.Shown = G.PPos; local.Face = G.Facing; local.Hp = G.Hp; local.MaxHp = G.MaxHp; local.Flags = (byte)G.NetFlags(); }
            foreach (var p in NetService.Players.Values.Where(x => x.PeerId != 1 && !x.Handshaken && now - x.Joined > NetConst.HandshakeTimeout).ToList())
                Multiplayer.MultiplayerPeer?.DisconnectPeer((int)p.PeerId);                 // не прошёл рукопожатие вовремя
            foreach (var target in NetService.Players.Values.Where(x => x.PeerId != 1 && x.Handshaken))
            {
                var data = new Godot.Collections.Array<float>();
                foreach (var p in NetService.Players.Values.Where(x => x.Handshaken && x.PeerId != target.PeerId))
                { data.Add(p.PeerId); data.Add(p.Pos.X); data.Add(p.Pos.Y); data.Add(p.Face); data.Add(p.Hp); data.Add(p.MaxHp); data.Add(p.Flags); }
                if (data.Count > 0) RpcId(target.PeerId, nameof(RpcSnapshot), data);
            }
        }
    }
}
