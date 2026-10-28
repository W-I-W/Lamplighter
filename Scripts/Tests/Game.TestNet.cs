using Godot;
using System;
using System.Linq;

namespace Lamplighter;

/// <summary>Проверки сетевого слоя на ENet двумя процессами (LAMP_STAGE=nethost / netjoin). Это проверка игровой сетевой архитектуры, а НЕ Steam P2P.</summary>
public partial class Game
{
    int netWait;

    /// <summary>Запуск сеанса из переменных окружения (только проверки): LAMP_NET=host | join:адрес[:порт], LAMP_NET_PORT, LAMP_NET_NAME, LAMP_NET_FAKEVER.</summary>
    void StartNetFromEnv()
    {
        string ln = OS.GetEnvironment("LAMP_NET"); if (!testing || ln == "") return;
        int port = int.TryParse(OS.GetEnvironment("LAMP_NET_PORT"), out var pp) ? pp : NetConst.DefaultPort;
        string nm = OS.GetEnvironment("LAMP_NET_NAME"); NetService.LocalName = nm != "" ? nm : ln.StartsWith("join") ? "Клиент" : "Хост";
        string fv = OS.GetEnvironment("LAMP_NET_FAKEVER"); NetService.VersionOverride = fv != "" ? fv : null;
        if (ln == "host") NetService.StartHost("enet", new NetHostOptions { Port = port });
        else if (ln == "hoststeam") NetService.StartHost("steam", new NetHostOptions { FriendsOnly = true });
        else if (ln.StartsWith("join:")) { string t = ln.Substring(5); if (!t.Contains(':')) t += ":" + port; NetService.Join("enet", t); }
    }

    /// <summary>Шаг, который повторяется (через si--), пока условие не выполнится или не истечёт время; затем выполняется onDone(успех).</summary>
    void WaitStep(float timeout, Func<bool> cond, Action<bool> onDone)
    {
        float waited = 0;
        S(.3f, () => { waited += .3f; bool ok = cond(); if (ok || waited >= timeout) onDone(ok); else si--; });
    }

    /// <summary>Снимки сетевых страниц меню (русский и английский текст).</summary>
    void BuildNetMenuScript()
    {
        S(.3f, () => { OpenMenu(Mn.Multi); }); S(.4f, () => Shot("netmenu_multi"));
        S(.1f, () => { NetAddr = "192.168.0.15:24865"; OpenMenu(Mn.NetJoin); NetService.Status = "Подключение…"; }); S(.4f, () => Shot("netmenu_join"));
        S(.1f, () => { NetService.Status = ""; OpenMenu(Mn.NetInfo); }); S(.4f, () => Shot("netmenu_info_offline"));
        S(.1f, () => { NetService.Role = NetRole.Host; NetService.State = NetState.Online; NetService.Players[1] = new NetPlayer { PeerId = 1, Name = "Host", Handshaken = true }; NetService.Players[2] = new NetPlayer { PeerId = 2, Name = "Friend", Handshaken = true }; NetService.Transport = NetService.Make("enet"); NetService.Status = "Игра открыта"; });
        S(.4f, () => Shot("netmenu_info_host"));
        S(.1f, () => { NetService.Players.Clear(); NetService.Transport = null; NetService.Role = NetRole.Offline; NetService.State = NetState.Offline; foreach (var m in Loc.Missing) GD.Print("[loc-missing] " + m); GD.Print("[check] NETMENU DONE"); });
    }

    /// <summary>Проверка настоящего Steam на этой машине (LAMP_STAGE=steamprobe): доступность GodotSteam, инициализация, создание лобби и SteamMultiplayerPeer.
    /// Нужен запущенный Steam и steam_appid.txt (для разработки — тестовый 480). Подключение второго клиента этот тест НЕ проверяет.</summary>
    void BuildSteamProbeScript()
    {
        var t = new SteamTransport(); string why = ""; bool lobby = false; string fail = "";
        S(.3f, () =>
        {
            bool ok = t.IsAvailable(out why); GD.Print($"[steam] available={ok} reason={why}");
            t.Ready += () => lobby = true; t.Failed += f => fail = f;
        });
        S(.2f, () =>
        {
            foreach (bool relayFirst in new[] { false, true })
            {
                var pr = ClassDB.Instantiate("SteamMultiplayerPeer").As<MultiplayerPeer>(); if (relayFirst) pr.Set("server_relay", true);
                var r = pr.Call("create_host", 0); if (!relayFirst) pr.Set("server_relay", true);
                GD.Print($"[steam] create_host relayFirst={relayFirst} -> {r} id={pr.GetUniqueId()} isServerFlag={pr.IsServerRelaySupported()} status={pr.GetConnectionStatus()}"); pr.Call("close");
            }
        });
        S(.2f, () => { var err = t.StartHost(new NetHostOptions { FriendsOnly = true }); GD.Print($"[steam] StartHost -> {err}"); });
        WaitStep(20f, () => { t.Poll(.3); return lobby || fail != ""; }, ok => GD.Print($"[steam] lobbyReady={lobby} lobbyId={t.LobbyId} peer={(t.Peer != null ? t.Peer.GetClass() : "null")} failed='{fail}'"));
        S(.2f, () =>
        {
            var names = new System.Collections.Generic.List<string>(); foreach (var m in ClassDB.ClassGetMethodList("SteamMultiplayerPeer", true)) names.Add((string)m["name"]);
            GD.Print("[steam] peer methods: " + string.Join(",", names)); GD.Print("[steam] parent=" + ClassDB.GetParentClass("SteamMultiplayerPeer"));
            var props = new System.Collections.Generic.List<string>(); foreach (var m in ClassDB.ClassGetPropertyList("SteamMultiplayerPeer", true)) props.Add((string)m["name"]); GD.Print("[steam] peer props: " + string.Join(",", props));
        });
        S(.3f, () => { t.Close(); GD.Print("[steam] PROBE DONE"); });
    }

    /// <summary>Один процесс: хост через настоящий Steam (лобби + SteamMultiplayerPeer как MultiplayerPeer игры). Если Steam недоступен — этап пропускается с причиной.
    /// Подключение второго клиента Steam этим тестом НЕ проверяется.</summary>
    void BuildNetSteamScript()
    {
        int fails = 0;
        void Check(bool ok, string what) { if (!ok) fails++; GD.Print($"[check] {(ok ? "PASS" : "FAIL")} {what}"); }
        S(.3f, () => { Menu = Mn.None; noHeal = true; EventsEnabled = false; });
        if (!NetService.Make("steam").IsAvailable(out var why))
        {
            S(.2f, () => { GD.Print($"[check] SKIP steam is not available here: {why}"); GD.Print("[check] SUMMARY failures=0"); GD.Print("[check] NETSTEAM DONE"); });
            return;
        }
        WaitStep(25f, () => NetService.Online, ok => Check(ok, $"steam host: the lobby was created and the session is online ({NetService.Status})"));
        S(.3f, () =>
        {
            Check(NetService.Role == NetRole.Host && NetService.LocalId == 1 && Multiplayer.IsServer(), "steam host: this game is the multiplayer server (peer 1)");
            Check(NetService.Transport is SteamTransport st && st.LobbyId != 0 && st.InviteInfo != "", $"steam host: a Steam lobby exists (id {NetService.Transport?.InviteInfo})");
            Check(NetService.Transport?.Peer != null && NetService.Transport.Peer.GetClass() == "SteamMultiplayerPeer", "steam host: the transport peer is SteamMultiplayerPeer");
            var pr = Multiplayer.MultiplayerPeer; GD.Print($"[steam] mp peer={pr?.GetClass()} id={pr?.GetUniqueId()} status={pr?.GetConnectionStatus()} isServer={Multiplayer.IsServer()} api uid={Multiplayer.GetUniqueId()}");
            GD.Print($"[steam] host Steam ID = {NetService.Local?.UserId}");
            NetService.Leave(false); Check(NetService.Role == NetRole.Offline, "steam host: leaving closes the lobby");
        });
        S(.5f, () => { GD.Print($"[check] SUMMARY failures={fails}"); GD.Print("[check] NETSTEAM DONE"); });
    }

    void BuildNetScript(bool host)
    {
        int fails = 0; string expect = OS.GetEnvironment("LAMP_NET_EXPECT");
        void Check(bool ok, string what) { if (!ok) fails++; GD.Print($"[check] {(ok ? "PASS" : "FAIL")} {what}"); }
        var expectedClient = new Vector2((W.Spawn.X + .5f) * TS + 64, (W.Spawn.Y + .5f) * TS + 32);
        var expectedHost = new Vector2((W.Spawn.X + .5f) * TS - 64, (W.Spawn.Y + .5f) * TS);
        NetPlayer Other() => NetService.Players.Values.FirstOrDefault(p => p.PeerId != NetService.LocalId && p.Handshaken);
        S(.3f, () => { Menu = Mn.None; noHeal = true; Hp = MaxHp; Invuln = 99f; Enemies.Clear(); EventsEnabled = false; });
        if (host)
        {
            S(.3f, () => Check(NetService.Role == NetRole.Host && NetService.Online && NetService.LocalId == 1, "net host: the session is open and the host is peer 1"));
            if (expect == "reject")
            {
                WaitStep(14f, () => NetService.State == NetState.Online && NetService.Count > 1, ok => Check(!ok && NetService.Count == 1, "net host: a client with an incompatible version is not admitted"));
            }
            else
            {
                WaitStep(25f, () => NetService.Count >= 2 && Other() != null, ok => Check(ok, "net host: the client completed the handshake"));
                S(.3f, () => { Check(NetService.Count == 2, $"net host: two players in the roster ({NetService.Count})"); Check(Other()?.Name == "Клиент", $"net host: the client name was received ({Other()?.Name})"); });
                WaitStep(15f, () => Other() != null && Other().Pos.DistanceTo(expectedClient) < 3f, ok => Check(ok, $"net host: the client position reaches the host ({Other()?.Pos})"));
                S(.3f, () => { Tp(expectedHost); GD.Print("[net] host moved"); Shot("net_host_view"); });
                WaitStep(20f, () => NetService.Count == 1, ok => Check(ok, "net host: the client leaving is noticed"));
            }
        }
        else
        {
            if (expect == "reject")
            {
                WaitStep(14f, () => NetService.State == NetState.Failed, ok => Check(ok && NetService.Status.Contains("версия"), $"net client: an incompatible version is rejected ({NetService.Status})"));
            }
            else
            {
                WaitStep(25f, () => NetService.Online && NetService.Local != null && !NetService.WorldPending, ok => Check(ok, "net client: connected and received the world"));
                S(.3f, () =>
                {
                    Check(NetService.WorldSeed == W.Seed && W.Seed == 4242, $"net client: the client world uses the host seed ({W.Seed})");
                    Check(NetService.Players.ContainsKey(1) && NetService.Players[1].Name == "Хост", "net client: the host is in the roster with its name");
                    Check(NetService.Local != null && NetService.Local.Name == "Клиент", "net client: the roster contains this player");
                    Tp(expectedClient);
                });
                WaitStep(20f, () => NetService.Players.TryGetValue(1, out var h) && h.Pos.DistanceTo(expectedHost) < 3f, ok => Check(ok, $"net client: the host position reaches the client ({(NetService.Players.TryGetValue(1, out var h2) ? h2.Pos : default)})"));
                S(1.0f, () => { Shot("net_client_view"); Check(Remotes().Count() == 1, "net client: one remote character is drawn"); });
                S(.3f, () => { NetService.Leave(false); Check(NetService.Role == NetRole.Offline, "net client: leaving the session works"); });
            }
        }
        S(.5f, () => { GD.Print($"[check] SUMMARY failures={fails}"); GD.Print("[check] NET DONE"); });
    }
}
