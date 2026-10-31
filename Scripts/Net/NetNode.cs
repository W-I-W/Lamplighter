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
            if (NetService.Players.TryGetValue(1, out var me)) { me.Name = NetService.LocalName; me.Look = G?.PlayerLook ?? Game.DefaultLook(); }      // после перезагрузки сцены (выбор героя и мира) запись хоста обновляется
            else NetService.Players[1] = new NetPlayer { PeerId = 1, Name = NetService.LocalName, Handshaken = true, UserId = t.UserIdOf(1), Look = G?.PlayerLook ?? Game.DefaultLook(), Hp = G?.Hp ?? 100, MaxHp = G?.MaxHp ?? 100, Pos = G?.PPos ?? default, Shown = G?.PPos ?? default };
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
        if (NetService.Players.Remove(id, out var p)) { if (p.Handshaken) { chatRate.Remove(id); G.Notify.Post(MsgKind.World, MsgTone.Info, string.Format(Loc.T("{0} покинул игру"), p.Name), null, 0, null, true); NetService.RaiseLeft(p); BroadcastRoster(); } }
    }

    void OnConnectedToServer()
    {
        NetService.LocalId = Multiplayer.GetUniqueId(); NetService.State = NetState.Handshaking; NetService.Status = "Проверка версии…";
        RpcId(1, nameof(RpcHello), NetConst.Protocol, NetService.VersionOverride ?? Game.VersionStr, NetConst.GenVersion, NetService.LocalName, (GameSession.GuestLook ?? G?.PlayerLook ?? Game.DefaultLook()).ToInts());
    }

    void OnConnectionFailed() { NetService.Fail("Не удалось подключиться к хосту"); DetachPeer(); }

    void OnServerDisconnected()
    {
        NetService.RaiseNotice("Соединение с хостом потеряно");
        NetService.Leave(); DetachPeer();
    }

    // ───────────────────────── рукопожатие ─────────────────────────
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcHello(string protocol, string version, int gen, string name, int[] look)
    {
        if (!Multiplayer.IsServer()) return;
        long id = Multiplayer.GetRemoteSenderId();
        if (!NetService.Players.TryGetValue(id, out var p) || p.Handshaken) return;
        string why = null;
        if (protocol != NetConst.Protocol || version != Game.VersionStr) why = $"Несовместимая версия игры (у хоста {Game.VersionStr})";
        else if (gen != NetConst.GenVersion) why = "Несовместимая версия генерации мира";
        else if (NetService.Players.Values.Count(x => x.Handshaken) >= NetConst.MaxPlayers) why = "Игра заполнена";
        if (why != null) { RpcId(id, nameof(RpcReject), why); GetTree().CreateTimer(.4).Timeout += () => Multiplayer.MultiplayerPeer?.DisconnectPeer((int)id); NetService.Players.Remove(id); return; }
        var verified = NetService.Transport?.UserNameOf(id) ?? "";             // имя из Steam проверено транспортом и не подменяется клиентом
        p.Name = CleanName(verified != "" ? verified : name, id);
        string baseName = p.Name; for (int dup = 2; NetService.Players.Values.Any(x => x != p && x.Handshaken && x.Name == p.Name); dup++) p.Name = $"{baseName} {dup}";     // у двух игроков не бывает одного имени
        p.Look = Look.FromInts(look); p.Handshaken = true; p.LastRecv = now; p.Hp = 100; p.MaxHp = 100; p.Pos = p.Shown = G.PPos;
        RpcId(id, nameof(RpcWelcome), G.W.Seed, NetConst.GenVersion, (long)id, G.PPos.X, G.PPos.Y, BuildRoster());
        RpcId(id, nameof(RpcWorldState), G.NetSnapshotPoi(), G.NetSnapshotAnc());
        BroadcastVillage(id); BroadcastDrops(id);
        RpcId(id, nameof(RpcStructs), G.NetStructTiles(), G.NetStructLamps());
        BroadcastEvents(id);
        RpcId(id, nameof(RpcProgress), G.BeaconLevel, G.BossMask);
        SendChatHistory(id); G.Notify.Post(MsgKind.World, MsgTone.Good, string.Format(Loc.T("{0} присоединился к игре"), p.Name), null, 0, null, true); NetService.RaiseJoined(p); BroadcastRoster();
    }

    static string CleanName(string n, long id)
    {
        // пиксельный шрифт знает ASCII и основную кириллицу: всё остальное из имени Steam (комбинируемые знаки, греческие и редкие символы, эмодзи) убирается, иначе вместо букв рисовались квадраты с шестнадцатеричными кодами
        n = (n ?? "").Trim();
        var chars = n.Where(c => (c >= ' ' && c <= '~' && c != '[' && c != ']') || (c >= 0x410 && c <= 0x44F) || c == 0x401 || c == 0x451).ToArray(); n = new string(chars).Trim();
        if (n.Length > 16) n = n.Substring(0, 16).Trim();
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

    // ───────────────────────── общий прогресс мира ─────────────────────────
    /// <summary>Хост → вошедший клиент: полный снимок мест интереса и фонарей. Хранится до готовности мира клиента (сцена может перезагрузиться с seed хоста).</summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcWorldState(byte[] poi, byte[] anc) { if (Multiplayer.GetRemoteSenderId() != 1) return; NetService.PendingPoi = poi; NetService.PendingAnc = anc; }

    readonly Dictionary<long, (double start, int n)> wrate = new();
    bool WorldRateOk(long id) { wrate.TryGetValue(id, out var r); if (now - r.start >= 1) r = (now, 0); r.n++; wrate[id] = r; return r.n <= 400; }

    public void SendPoi(int i, int state, int flags, int tile)
    {
        if (Multiplayer.MultiplayerPeer == null) return;
        if (Multiplayer.IsServer()) Rpc(nameof(RpcPoi), i, state, flags, tile); else RpcId(1, nameof(RpcPoi), i, state, flags, tile);
    }

    public void SendAnc(int i, int bits)
    {
        if (Multiplayer.MultiplayerPeer == null) return;
        if (Multiplayer.IsServer()) Rpc(nameof(RpcAnc), i, bits); else RpcId(1, nameof(RpcAnc), i, bits);
    }

    /// <summary>Изменение места интереса: клиент → хост (проверка и пересылка остальным) и хост → клиенты.</summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcPoi(int idx, int state, int flags, int tile)
    {
        long sender = Multiplayer.GetRemoteSenderId();
        if (Multiplayer.IsServer())
        {
            if (!NetService.Players.TryGetValue(sender, out var p) || !p.Handshaken || !WorldRateOk(sender) || !G.NetApplyPoi(idx, state, flags, tile)) return;
            foreach (var t in NetService.Players.Values.Where(x => x.Handshaken && x.PeerId != 1 && x.PeerId != sender)) RpcId(t.PeerId, nameof(RpcPoi), idx, state, flags, tile);
        }
        else if (sender == 1) G.NetApplyPoi(idx, state, flags, tile);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcAnc(int idx, int bits)
    {
        long sender = Multiplayer.GetRemoteSenderId();
        if (Multiplayer.IsServer())
        {
            if (!NetService.Players.TryGetValue(sender, out var p) || !p.Handshaken || !WorldRateOk(sender) || !G.NetApplyAnc(idx, bits)) return;
            foreach (var t in NetService.Players.Values.Where(x => x.Handshaken && x.PeerId != 1 && x.PeerId != sender)) RpcId(t.PeerId, nameof(RpcAnc), idx, bits);
        }
        else if (sender == 1) G.NetApplyAnc(idx, bits);
    }

    // ───────────────────────── деревня: жители, здания, склад ─────────────────────────
    // Хост — единственный хозяин жителей, зданий и производства. Клиенты получают снимок (при входе, при изменении и раз в несколько секунд),
    // позиции жителей приходят 5 раз в секунду и сглаживаются; свои действия клиенты отправляют хосту командами.
    public void BroadcastVillage(long only = 0)
    {
        if (!Multiplayer.IsServer()) return;
        var vills = G.NetVillRoster(); var blds = G.NetBldRoster(); var stock = G.NetStock();
        if (only != 0) RpcId(only, nameof(RpcVillage), vills, blds, stock); else Rpc(nameof(RpcVillage), vills, blds, stock);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcVillage(Godot.Collections.Array vills, Godot.Collections.Array blds, int[] stock) { if (Multiplayer.GetRemoteSenderId() == 1) G.NetApplyVillage(vills, blds, stock); }

    public void SendVillPos(Godot.Collections.Array<float> data) { if (Multiplayer.IsServer() && data.Count > 0) Rpc(nameof(RpcVillPos), data); }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    public void RpcVillPos(Godot.Collections.Array<float> data) { if (Multiplayer.GetRemoteSenderId() == 1) G.NetApplyVillPos(data); }

    public void SendRescue(int idx) { if (Multiplayer.MultiplayerPeer != null) RpcId(1, nameof(RpcRescue), idx); }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcRescue(int idx)
    {
        if (!Multiplayer.IsServer()) return; long s = Multiplayer.GetRemoteSenderId();
        if (!NetService.Players.TryGetValue(s, out var p) || !p.Handshaken || !WorldRateOk(s)) return;
        if (G.NetRescue(idx)) BroadcastVillage();
    }

    public void SendAssign(int key, int manual, int vid) { if (Multiplayer.MultiplayerPeer != null) RpcId(1, nameof(RpcAssign), key, manual, vid); }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcAssign(int key, int manual, int vid)
    {
        if (!Multiplayer.IsServer()) return; long s = Multiplayer.GetRemoteSenderId();
        if (!NetService.Players.TryGetValue(s, out var p) || !p.Handshaken || !WorldRateOk(s)) return;
        if (G.NetApplyAssign(key, manual, vid)) BroadcastVillage();
    }

    /// <summary>Команды строительства от клиента: 1 построить (тип, x, y), 2 улучшить, 3 разобрать, 4 режим, 5 приоритет, 6 пауза, 7 забрать ресурсы.</summary>
    public void SendBuildCmd(int op, int a, int b, int c) { if (Multiplayer.MultiplayerPeer != null) RpcId(1, nameof(RpcBuildCmd), op, a, b, c); }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcBuildCmd(int op, int a, int b, int c)
    {
        if (!Multiplayer.IsServer()) return; long s = Multiplayer.GetRemoteSenderId();
        if (!NetService.Players.TryGetValue(s, out var p) || !p.Handshaken || !WorldRateOk(s)) return;
        var grant = G.NetBuildCmd(op, a, b, c);
        if (grant != null) RpcId(s, nameof(RpcGrant), grant);
        BroadcastVillage();
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcGrant(int[] items) { if (Multiplayer.GetRemoteSenderId() == 1) G.NetGrant(items); }

    // ───────────────────────── звуки и эффекты ─────────────────────────
    public void SendSfx(int idx, float db, float pitch, float x, float y) { if (Multiplayer.MultiplayerPeer == null) return; if (Multiplayer.IsServer()) Rpc(nameof(RpcSfx), idx, db, pitch, x, y); else RpcId(1, nameof(RpcSfx), idx, db, pitch, x, y); }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    public void RpcSfx(int idx, float db, float pitch, float x, float y)
    {
        long sender = Multiplayer.GetRemoteSenderId();
        if (Multiplayer.IsServer())
        {
            if (!NetService.Players.TryGetValue(sender, out var p) || !p.Handshaken || !WorldRateOk(sender)) return;
            G.NetSfxIn(idx, db, pitch, x, y);
            foreach (var o in NetService.Players.Values.Where(q => q.Handshaken && q.PeerId != 1 && q.PeerId != sender)) RpcId(o.PeerId, nameof(RpcSfx), idx, db, pitch, x, y);
        }
        else if (sender == 1) G.NetSfxIn(idx, db, pitch, x, y);
    }

    public void SendBossDown(int id, Vector2 pos) { if (Multiplayer.IsServer()) Rpc(nameof(RpcBossDown), id, pos.X, pos.Y); }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcBossDown(int id, float x, float y) { if (Multiplayer.GetRemoteSenderId() == 1 && float.IsFinite(x) && float.IsFinite(y)) G.NetBossDown(id, new Vector2(x, y)); }

    // ───────────────────────── общий прогресс ─────────────────────────
    public void SendProgress(int beacon, int boss) { if (Multiplayer.MultiplayerPeer == null) return; if (Multiplayer.IsServer()) Rpc(nameof(RpcProgress), beacon, boss); else RpcId(1, nameof(RpcProgress), beacon, boss); }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcProgress(int beacon, int boss)
    {
        long sender = Multiplayer.GetRemoteSenderId();
        if (Multiplayer.IsServer())
        {
            if (!NetService.Players.TryGetValue(sender, out var p) || !p.Handshaken || !WorldRateOk(sender) || !G.NetApplyProgress(beacon, boss)) return;
            foreach (var o in NetService.Players.Values.Where(q => q.Handshaken && q.PeerId != 1 && q.PeerId != sender)) RpcId(o.PeerId, nameof(RpcProgress), G.BeaconLevel, G.BossMask);
        }
        else if (sender == 1) G.NetApplyProgress(beacon, boss);
    }

    // ───────────────────────── события мира ─────────────────────────
    public void BroadcastEvents(long only = 0)
    {
        if (!Multiplayer.IsServer()) return; var a = G.NetEventRoster();
        if (only != 0) RpcId(only, nameof(RpcEvents), a); else Rpc(nameof(RpcEvents), a);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcEvents(Godot.Collections.Array a) { if (Multiplayer.GetRemoteSenderId() == 1) G.NetApplyEvents(a); }

    public void SendEventAct(int seed) { if (Multiplayer.MultiplayerPeer != null) RpcId(1, nameof(RpcEventAct), seed); }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcEventAct(int seed)
    {
        if (!Multiplayer.IsServer()) return; long s = Multiplayer.GetRemoteSenderId();
        if (!NetService.Players.TryGetValue(s, out var p) || !p.Handshaken || !WorldRateOk(s)) return;
        if (G.NetEventAct(seed)) BroadcastEvents();
    }

    // ───────────────────────── общие постройки игроков и цели ─────────────────────────
    public void SendTile(int x, int y, int t, int placed) { if (Multiplayer.MultiplayerPeer == null) return; if (Multiplayer.IsServer()) Rpc(nameof(RpcTile), x, y, t, placed); else RpcId(1, nameof(RpcTile), x, y, t, placed); }
    public void SendLamp(int op, int x, int y, int kind, int level, int dir) { if (Multiplayer.MultiplayerPeer == null) return; if (Multiplayer.IsServer()) Rpc(nameof(RpcLamp), op, x, y, kind, level, dir); else RpcId(1, nameof(RpcLamp), op, x, y, kind, level, dir); }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcTile(int x, int y, int t, int placed)
    {
        long sender = Multiplayer.GetRemoteSenderId();
        if (Multiplayer.IsServer())
        {
            if (!NetService.Players.TryGetValue(sender, out var p) || !p.Handshaken || !WorldRateOk(sender) || !G.NetApplyTile(x, y, t, placed)) return;
            foreach (var o in NetService.Players.Values.Where(q => q.Handshaken && q.PeerId != 1 && q.PeerId != sender)) RpcId(o.PeerId, nameof(RpcTile), x, y, t, placed);
        }
        else if (sender == 1) G.NetApplyTile(x, y, t, placed);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcLamp(int op, int x, int y, int kind, int level, int dir)
    {
        long sender = Multiplayer.GetRemoteSenderId();
        if (Multiplayer.IsServer())
        {
            if (!NetService.Players.TryGetValue(sender, out var p) || !p.Handshaken || !WorldRateOk(sender) || !G.NetApplyLamp(op, x, y, kind, level, dir)) return;
            foreach (var o in NetService.Players.Values.Where(q => q.Handshaken && q.PeerId != 1 && q.PeerId != sender)) RpcId(o.PeerId, nameof(RpcLamp), op, x, y, kind, level, dir);
        }
        else if (sender == 1) G.NetApplyLamp(op, x, y, kind, level, dir);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcStructs(int[] tiles, int[] lamps) { if (Multiplayer.GetRemoteSenderId() == 1) { NetService.PendingTiles = tiles; NetService.PendingLamps = lamps; } }

    // ───────────────────────── бой ─────────────────────────
    public void SendEnemies(Godot.Collections.Array<float> d) { if (Multiplayer.IsServer() && d.Count > 0) Rpc(nameof(RpcEnemies), d); }
    public void SendProjs(Godot.Collections.Array<float> d) { if (Multiplayer.IsServer()) Rpc(nameof(RpcProjs), d); }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    public void RpcEnemies(Godot.Collections.Array<float> d) { if (Multiplayer.GetRemoteSenderId() == 1) G.NetApplyEnemies(d); }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    public void RpcProjs(Godot.Collections.Array<float> d) { if (Multiplayer.GetRemoteSenderId() == 1) G.NetApplyProjs(d); }

    public void SendEnemyHit(int id, float dmg, Vector2 dir, bool melee) { if (Multiplayer.MultiplayerPeer != null) RpcId(1, nameof(RpcEnemyHit), id, dmg, dir.X, dir.Y, melee); }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcEnemyHit(int id, float dmg, float dx, float dy, bool melee)
    {
        if (!Multiplayer.IsServer()) return; long s = Multiplayer.GetRemoteSenderId();
        if (!NetService.Players.TryGetValue(s, out var p) || !p.Handshaken || !WorldRateOk(s)) return;
        G.NetEnemyHit(s, id, dmg, new Vector2(dx, dy), melee);
    }

    public void SendHurt(long peer, float dmg, Vector2 kb, bool fire) { if (Multiplayer.IsServer() && peer != 1) RpcId(peer, nameof(RpcHurtMe), dmg, kb.X, kb.Y, fire); }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcHurtMe(float dmg, float kx, float ky, bool fire) { if (Multiplayer.GetRemoteSenderId() == 1) G.NetHurtMe(dmg, new Vector2(kx, ky), fire); }

    Godot.Collections.Array BuildRoster()
    {
        var a = new Godot.Collections.Array();
        foreach (var p in NetService.Players.Values.Where(x => x.Handshaken)) a.Add(new Godot.Collections.Dictionary { ["id"] = p.PeerId, ["name"] = p.Name, ["user"] = (long)p.UserId, ["look"] = p.Look.ToInts() });
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
            p.Name = (string)d["name"]; p.UserId = (ulong)(long)d["user"]; p.Handshaken = true; if (d.ContainsKey("look")) p.Look = Look.FromInts(d["look"].AsInt32Array());
        }
        foreach (var id in NetService.Players.Keys.ToList()) if (!ids.Contains(id)) { var p = NetService.Players[id]; NetService.Players.Remove(id); NetService.RaiseLeft(p); if (id != NetService.LocalId) NetService.RaiseNotice($"{p.Name} отключился"); }
    }

    // ───────────────────────── состояние персонажей ─────────────────────────
    /// <summary>Клиент → хост: собственное состояние (позиция, взгляд, здоровье, флаги). Ненадёжная упорядоченная доставка: потеря пакета не страшна.</summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    public void RpcState(float x, float y, int face, float hp, float maxHp, int flags, float light, int held, float aim)
    {
        if (!Multiplayer.IsServer()) return;
        long id = Multiplayer.GetRemoteSenderId();
        if (!NetService.Players.TryGetValue(id, out var p) || !p.Handshaken) return;
        if (!RateOk(id)) return;                                                         // ограничение частоты запросов
        if (!float.IsFinite(x) || !float.IsFinite(y)) return;
        float maxX = World.Wd * 16f, maxY = World.Ht * 16f; x = Mathf.Clamp(x, 0, maxX); y = Mathf.Clamp(y, 0, maxY);
        double dt = Math.Max(.02, now - p.LastRecv); var np = new Vector2(x, y);
        if (p.LastRecv > 0 && p.Pos.DistanceTo(np) / dt > 1200f) { return; }             // слишком быстрое перемещение — состояние отвергается (позиция остаётся прежней)
        p.Pos = np; p.Face = face < 0 ? -1 : 1; p.MaxHp = Mathf.Clamp(float.IsFinite(maxHp) ? maxHp : 100, 1, 1000); p.Hp = Mathf.Clamp(float.IsFinite(hp) ? hp : p.MaxHp, 0, p.MaxHp); p.Flags = (byte)(flags & 3); p.Light = float.IsFinite(light) ? Mathf.Clamp(light, 20f, 400f) : 72f; p.Held = held; p.AimAng = float.IsFinite(aim) ? aim : 0f; p.LastRecv = now;
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
        for (int i = 0; i + 9 < data.Count; i += 10)
        {
            long id = (long)data[i]; if (id == NetService.LocalId) continue;
            if (!NetService.Players.TryGetValue(id, out var p)) continue;
            bool first = p.LastRecv <= 0; p.Pos = new Vector2(data[i + 1], data[i + 2]); p.Face = (int)data[i + 3]; p.Hp = data[i + 4]; p.MaxHp = data[i + 5]; p.Flags = (byte)(int)data[i + 6]; p.Light = data[i + 7]; p.Held = (int)data[i + 8]; p.AimAng = data[i + 9]; p.LastRecv = now;
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
            RpcId(1, nameof(RpcState), G.PPos.X, G.PPos.Y, G.Facing, G.Hp, G.MaxHp, G.NetFlags(), G.PlayerLightR, G.HeldIcon(), (G.Aim - G.PPos).Angle());
        else if (NetService.Role == NetRole.Host && Multiplayer.IsServer())
        {
            if (local != null) { local.Pos = G.PPos; local.Shown = G.PPos; local.Face = G.Facing; local.Hp = G.Hp; local.MaxHp = G.MaxHp; local.Flags = (byte)G.NetFlags(); local.Light = G.PlayerLightR; local.Held = G.HeldIcon(); local.AimAng = (G.Aim - G.PPos).Angle(); }
            foreach (var p in NetService.Players.Values.Where(x => x.PeerId != 1 && !x.Handshaken && now - x.Joined > NetConst.HandshakeTimeout).ToList())
                Multiplayer.MultiplayerPeer?.DisconnectPeer((int)p.PeerId);                 // не прошёл рукопожатие вовремя
            foreach (var target in NetService.Players.Values.Where(x => x.PeerId != 1 && x.Handshaken))
            {
                var data = new Godot.Collections.Array<float>();
                foreach (var p in NetService.Players.Values.Where(x => x.Handshaken && x.PeerId != target.PeerId))
                { data.Add(p.PeerId); data.Add(p.Pos.X); data.Add(p.Pos.Y); data.Add(p.Face); data.Add(p.Hp); data.Add(p.MaxHp); data.Add(p.Flags); data.Add(p.Light); data.Add(p.Held); data.Add(p.AimAng); }
                if (data.Count > 0) RpcId(target.PeerId, nameof(RpcSnapshot), data);
            }
        }
    }
}
