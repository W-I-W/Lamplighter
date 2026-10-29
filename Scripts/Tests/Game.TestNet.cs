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
        PlayerLook = ln.StartsWith("join") ? new Look { Skin = 5, Hair = 3, HairCol = 2, Cloth1 = 9, Cloth2 = 10, Variant = 2 } : new Look { Skin = 2, Hair = 6, HairCol = 1, Cloth1 = 3, Cloth2 = 4, Variant = 1 };      // у хоста и гостя разная внешность
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
        expectedClient = NearestFree(expectedClient, 5f, false) ?? expectedClient; expectedHost = NearestFree(expectedHost, 5f, false) ?? expectedHost;      // оба процесса считают одно и то же: точки не внутри камня (иначе игрок выталкивается)
        NetPlayer Other() => NetService.Players.Values.FirstOrDefault(p => p.PeerId != NetService.LocalId && p.Handshaken);
        S(.3f, () => { Menu = Mn.None; noHeal = true; Hp = MaxHp; Invuln = 99f; Enemies.Clear(); EventsEnabled = false; TestNoSpawn = true; });
        var chests = W.Pois.Where(p => p.Kind == PK.Chest).Take(3).ToList();
        void Open(Poi p) { p.State = 3; SetObj(p.Tile.X, p.Tile.Y, Tl.ChestOpen); }
        bool IsOpen(Poi p) => p.State == 3 && W.Get(p.Tile.X, p.Tile.Y) == Tl.ChestOpen;
        var ns = new System.Collections.Generic.List<Vector2I>(); Vector2I stoneT = new(-1, -1);
        S(.05f, () =>
        {
            for (int r = 11; r < 20 && ns.Count < 5; r++) for (int a = -r; a <= r && ns.Count < 5; a++) { var tt = new Vector2I(W.Spawn.X + a, W.Spawn.Y - r); if (W.GenObj[World.Key(tt.X, tt.Y)] == (byte)Tl.None && W.Ground[World.Key(tt.X, tt.Y)] != Art.WaterG && ns.All(q => Math.Abs(q.X - tt.X) >= 3)) ns.Add(tt); }
            for (int r = 3; r < 30 && stoneT.X < 0; r++) for (int a = -r; a <= r && stoneT.X < 0; a++) { var tt = new Vector2I(W.Spawn.X + a, W.Spawn.Y + r); if (W.GenObj[World.Key(tt.X, tt.Y)] == (byte)Tl.Stone && !ns.Contains(tt)) stoneT = tt; }
        });
        var surv = W.Pois.Find(p => p.Kind == PK.Survivor && p.Variant == 1 && World.RegionOf(p.Tile.X) == 0);
        Building Saw() => Buildings.Find(b => b.Type == Tl.Sawmill);
        if (host)
        {
            S(.3f, () => Check(NetService.Role == NetRole.Host && NetService.Online && NetService.LocalId == 1, "net host: the session is open and the host is peer 1"));
            if (expect == "reject")
            {
                WaitStep(14f, () => NetService.State == NetState.Online && NetService.Count > 1, ok => Check(!ok && NetService.Count == 1, "net host: a client with an incompatible version is not admitted"));
            }
            else
            {
                S(.2f, () => { Open(chests[0]); Inv[It.Shard] = 99; Activate(W.Ancients[1], 1); Check(IsOpen(chests[0]) && W.Ancients[1].Lit, "net host: progress made before the client joins (chest opened, lantern lit)"); });
                S(.2f, () => { SetObj(ns[0].X, ns[0].Y, Tl.Furnace); Placed.Add(World.Key(ns[0].X, ns[0].Y)); SetObj(ns[1].X, ns[1].Y, Tl.Lantern); Lamps.Add(new Lamp { X = ns[1].X, Y = ns[1].Y, Anim = 1 }); TestGoal = "TEST-GOAL"; });
                S(.2f, () =>
                {
                    var sites = new System.Collections.Generic.List<Vector2I>(); for (int r = 2; r < 9 && sites.Count < 2; r++) for (int a = -r; a <= r && sites.Count < 2; a++) { var t = new Vector2I(W.Spawn.X + a, W.Spawn.Y - r); if (sites.Count == 0 || Math.Abs(sites[0].X - t.X) >= 3) if (SafeTile(t.X, t.Y) && DecorFree(t.X, t.Y)) sites.Add(t); }
                    var wh = CreateBuilding(0, sites[0]); wh.Forced = true; var sw = CreateBuilding(1, sites[1]); sw.Forced = true; AddVillager(9501, TileCenter(W.Spawn.X, W.Spawn.Y + 2), "lumberjack", "test"); RecomputeNetwork();
                    Check(WorkerOf(sw)?.Id == 9501, "net host: a villager works in the sawmill before the client joins");
                });
                WaitStep(25f, () => NetService.Count >= 2 && Other() != null, ok => Check(ok, "net host: the client completed the handshake"));
                S(.2f, () => { var o = Other(); Check(o != null && o.Look.Skin == 5 && o.Look.Hair == 3 && o.Look.Cloth1 == 9 && o.Look.Variant == 2, "net host: the guest's appearance arrived with the handshake (validated by the host)"); });
                S(.3f, () => { Open(chests[1]); SetObj(ns[2].X, ns[2].Y, Tl.AmpCrystal); Placed.Add(World.Key(ns[2].X, ns[2].Y)); });
                WaitStep(20f, () => W.Get(ns[3].X, ns[3].Y) == Tl.Banner && Lamps.Any(l => l.X == ns[4].X && l.Y == ns[4].Y) && stoneT.X >= 0 && W.Get(stoneT.X, stoneT.Y) == Tl.None, ok => Check(ok, $"net host: the client's placed banner and lamp and a mined stone are visible here banner={W.Get(ns[3].X, ns[3].Y)} lamp={Lamps.Any(l => l.X == ns[4].X && l.Y == ns[4].Y)} stone={(stoneT.X >= 0 ? W.Get(stoneT.X, stoneT.Y).ToString() : "none")}"));
                WaitStep(20f, () => IsOpen(chests[2]), ok => Check(ok, "net host: a chest opened by the client is opened here too"));
                WaitStep(20f, () => Saw() != null && Saw().Manual && WorkerOf(Saw()) == null, ok => Check(ok, "net host: the client's worker removal command arrived and was applied"));
                WaitStep(20f, () => VillagerById(1000 + W.Pois.IndexOf(surv)) != null && surv.State == 3, ok => Check(ok, "net host: the client's rescue created the villager here (one, shared)"));
                S(.3f, () => Check(Villagers.Count(v => v.Id == 1000 + W.Pois.IndexOf(surv)) == 1, "net host: the rescued villager exists exactly once"));
                Vector2 arena = default; Enemy te = null;
                S(.3f, () => { arena = FindDarkArena(); Tp(arena); Enemies.Clear(); Projs.Clear(); });
                WaitStep(20f, () => Other() != null && Other().Pos.DistanceTo(arena + new Vector2(60, 0)) < 24f, ok => Check(ok, "net host: the client joined the arena"));
                S(.3f, () => { te = SpawnEnemy(arena + new Vector2(110, 0), EK.Shade, null, 1f); te.MaxHp = te.Hp = 400f; te.Cd = 0; });
                WaitStep(20f, () => te != null && te.Hp < 399f, ok => Check(ok, "net host: a hit made by the client reduced the host's enemy health (host-authoritative damage)"));
                S(.3f, () => Check(te != null && te.Target == Other()?.PeerId, $"net host: the enemy chose the nearest player, not the host (target {te?.Target})"));
                S(4f, () => { });
                S(.3f, () => { Enemies.Clear(); });
                Vector2 mpos = default;
                S(.3f, () => { mpos = TileCenter(W.Spawn.X + 2, W.Spawn.Y - 24); SpawnEvent("meteor", mpos); BeaconLevel = 3; });
                WaitStep(20f, () => { var mt = ToTile(mpos); return W.Get(mt.X, mt.Y) == Tl.MeteorOre; }, ok => Check(ok, "net host: the meteor falls and leaves its ore"));
                S(.3f, () => { Check(NetService.Count == 2, $"net host: two players in the roster ({NetService.Count})"); Check(Other()?.Name == "Клиент", $"net host: the client name was received ({Other()?.Name})"); });
                WaitStep(15f, () => Other() != null && Other().Pos.DistanceTo(expectedClient) < 3f, ok => Check(ok, $"net host: the client position reaches the host ({Other()?.Pos})"));
                S(.3f, () => { Check(Other() != null && Other().Light > 10f && Other().Light < 400f, $"net host: the client's light radius arrives ({Other()?.Light:0})"); Tp(expectedHost); GD.Print("[net] host moved"); Shot("net_host_view"); });
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
                WaitStep(10f, () => IsOpen(chests[0]) && W.Ancients[1].Lit, ok => Check(ok, "net client: progress made before joining arrives with the world snapshot (late join)"));
                WaitStep(10f, () => W.Get(ns[0].X, ns[0].Y) == Tl.Furnace && Placed.Contains(World.Key(ns[0].X, ns[0].Y)) && Lamps.Any(l => l.X == ns[1].X && l.Y == ns[1].Y), ok => Check(ok && HasFurnace, $"net client: objects placed by the host before joining (furnace, lamp) are shown and the furnace counts as built (late join) furnace={W.Get(ns[0].X, ns[0].Y)} placed={Placed.Contains(World.Key(ns[0].X, ns[0].Y))} lamp={Lamps.Any(l => l.X == ns[1].X && l.Y == ns[1].Y)} lamps={Lamps.Count}"));
                WaitStep(10f, () => Goals.Any(g => g.Title == "TEST-GOAL"), ok => Check(ok, "net client: the goal list is the host's (shared goals)"));
                WaitStep(10f, () => W.Get(ns[2].X, ns[2].Y) == Tl.AmpCrystal, ok => Check(ok, $"net client: an object placed by the host while online appears ({W.Get(ns[2].X, ns[2].Y)})"));
                S(.3f, () => { SetObj(ns[3].X, ns[3].Y, Tl.Banner); Placed.Add(World.Key(ns[3].X, ns[3].Y)); SetObj(ns[4].X, ns[4].Y, Tl.Lantern); Lamps.Add(new Lamp { X = ns[4].X, Y = ns[4].Y, Anim = 1 }); if (stoneT.X >= 0) SetObj(stoneT.X, stoneT.Y, Tl.None); });
                WaitStep(10f, () => IsOpen(chests[1]), ok => Check(ok, "net client: a chest opened by the host while online opens here too"));
                WaitStep(10f, () => VillagerById(9501) != null && Saw() != null && WorkerOf(Saw())?.Id == 9501, ok => Check(ok, "net client: the host's villager, sawmill and assignment arrive with the world (late join)"));
                WaitStep(10f, () => VillagerById(9501)?.State == VState.Working, ok => Check(ok, "net client: the villager's work state is shown (host-simulated)"));
                S(.3f, () => { Check(!IsOpen(chests[2]), "net client: untouched chest stays closed"); Open(chests[2]); ClearWorker(Saw()); Inv[It.Wood] = 20; Inv[It.Stone] = 10; Tp(surv.Pos + new Vector2(0, 30)); RescueSurvivor(surv); });
                WaitStep(10f, () => VillagerById(1000 + W.Pois.IndexOf(surv)) != null && Villagers.Count(v => v.Id == 1000 + W.Pois.IndexOf(surv)) == 1, ok => Check(ok, "net client: the rescued villager appears once for the client too"));
                Vector2 arena = default; float hit = 0;
                S(.3f, () => { arena = FindDarkArena(); Tp(arena + new Vector2(60, 0)); Hp = MaxHp; Invuln = 0; });
                WaitStep(25f, () => Enemies.Any(e => e.NetId > 0), ok => Check(ok, "net client: the host's enemy is shown on the client (no local AI)"));
                S(.3f, () => { Check(Enemies.All(e => e.NetId > 0), "net client: the client created no enemies of its own"); var e = Enemies.First(x => x.NetId > 0); Hurt(e, 8f, Vector2.Left, true); });
                WaitStep(25f, () => Hp < MaxHp - 1f, ok => Check(ok, $"net client: the damage decided by the host reaches the client ({MaxHp - Hp:0.0})"));
                S(.3f, () => { float drop = MaxHp - Hp; float one = 9f * Tune.T.EnemyDamage * FearMul; Check(Mathf.Abs(drop / one - Mathf.Round(drop / one)) < .06f, $"net client: damage arrives in whole hits of {one:0.00} (not doubled by the network): {drop:0.0}"); Invuln = 99f; });
                S(5f, () => { });
                WaitStep(20f, () => Events.Any(e => e.Kind == "meteor"), ok => Check(ok, "net client: the host's meteor event is shown (shared events)"));
                WaitStep(20f, () => { var mp = Events.FirstOrDefault(e => e.Kind == "meteor"); if (mp == null) return false; var mt = ToTile(mp.Pos); return W.Get(mt.X, mt.Y) == Tl.MeteorOre; }, ok => Check(ok, "net client: the meteor ore appeared for the client too"));
                WaitStep(15f, () => BeaconLevel >= 3, ok => Check(ok, "net client: the beacon level is shared"));
                S(1.5f, () => { });
                S(.3f, () =>
                {
                    Check(NetService.WorldSeed == W.Seed && W.Seed == 4242, $"net client: the client world uses the host seed ({W.Seed})");
                    Check(NetService.Players.ContainsKey(1) && NetService.Players[1].Name == "Хост", "net client: the host is in the roster with its name");
                    Check(NetService.Local != null && NetService.Local.Name == "Клиент", "net client: the roster contains this player");
                    Check(NetService.Players[1].Look.Skin == 2 && NetService.Players[1].Look.Hair == 6 && NetService.Players[1].Look.Variant == 1, "net client: the host's appearance arrived in the roster");
                    Tp(expectedClient);
                });
                WaitStep(20f, () => NetService.Players.TryGetValue(1, out var h) && h.Pos.DistanceTo(expectedHost) < 3f, ok => Check(ok, $"net client: the host position reaches the client ({(NetService.Players.TryGetValue(1, out var h2) ? h2.Pos : default)})"));
                S(1.0f, () => { Check(NetService.Players.TryGetValue(1, out var hh) && hh.Light > 10f && hh.Light < 400f, $"net client: the host's light radius arrives ({(NetService.Players.TryGetValue(1, out var h3) ? h3.Light : 0):0})"); Shot("net_client_view"); Check(Remotes().Count() == 1, "net client: one remote character is drawn"); });
                S(.3f, () => { NetService.Leave(false); Check(NetService.Role == NetRole.Offline, "net client: leaving the session works"); });
            }
        }
        S(.5f, () => { GD.Print($"[check] SUMMARY failures={fails}"); GD.Print("[check] NET DONE"); });
    }
}
