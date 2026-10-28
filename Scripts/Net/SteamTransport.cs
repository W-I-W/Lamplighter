using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Транспорт Steam: лобби и P2P через Steam Datagram Relay с помощью GDExtension GodotSteam (класс SteamMultiplayerPeer и синглтон Steam).
/// Проект на C#, а официальных .NET-сборок GodotSteam нет, поэтому плагин вызывается динамически (ClassDB / Engine.GetSingleton) и не требует компиляционной зависимости:
/// без плагина транспорт честно сообщает «недоступно» и ничего не имитирует.
/// ВАЖНО: код написан по документации GodotSteam и НЕ проверен на живом Steam — для этого нужны плагин, AppID и два клиента Steam (см. docs/MULTIPLAYER.md).</summary>
public sealed class SteamTransport : INetTransport
{
    static SteamTransport shared; public static SteamTransport Shared => shared ??= new SteamTransport();     // Steam инициализируется один раз на процесс
    GodotObject steam; MultiplayerPeer peer; long lobbyId; bool wired, inited, hosting;
    public string Id => "steam";
    public string DisplayName => "Steam";
    public MultiplayerPeer Peer => peer;
    public string InviteInfo => lobbyId != 0 ? lobbyId.ToString() : "";
    public event Action Ready;
    public event Action<string> Failed;
    public event Action<long> JoinRequested;               // приглашение из Steam Overlay
    public event Action<long[]> LobbyList;                 // результат поиска лобби
    public long LobbyId => lobbyId;

    public bool IsAvailable(out string reason)
    {
        reason = "";
        if (!ClassDB.ClassExists("SteamMultiplayerPeer")) { reason = "Не найден плагин GodotSteam (нужен GDExtension с SteamMultiplayerPeer)"; return false; }
        if (!Engine.HasSingleton("Steam")) { reason = "Плагин GodotSteam не подключён (нет синглтона Steam)"; return false; }
        steam ??= Engine.GetSingleton("Steam");
        if (!inited)
        {
            // Запуск из редактора/exe не из Steam: steam_appid.txt ищется рядом с запущенным exe, а не в проекте. Без него Steam не знает AppID.
            // Для разработки подставляем тестовый AppID 480 (Spacewar); при запуске из Steam переменные уже заданы и ничего не меняется.
            if (OS.GetEnvironment("SteamAppId") == "" && OS.GetEnvironment("SteamGameId") == "" && !System.IO.File.Exists(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(OS.GetExecutablePath()) ?? "", "steam_appid.txt")))
            { OS.SetEnvironment("SteamAppId", NetConst.DevAppId); OS.SetEnvironment("SteamGameId", NetConst.DevAppId); GD.Print("[steam] AppID не задан — тестовый " + NetConst.DevAppId + " (Spacewar). В релизе нужен свой AppID."); }
            var r = steam.Call("steamInitEx");
            bool ok = true;
            if (r.VariantType == Variant.Type.Dictionary) { var d = r.AsGodotDictionary(); ok = d.ContainsKey("status") && (long)d["status"] == 0; if (!ok) reason = d.ContainsKey("verbal") ? d["verbal"].ToString() : "Steam не запущен"; }
            if (!ok) { if (reason == "") reason = "Steam не запущен или не удалось инициализировать"; return false; }
            inited = true; Wire(); steam.Call("initRelayNetworkAccess");            // доступ к сети ретрансляции Steam (SDR) нужен для P2P-сокетов
        }
        return true;
    }

    void Wire()
    {
        if (wired) return; wired = true;
        steam.Connect("lobby_created", Callable.From<long, long>(OnLobbyCreated));
        steam.Connect("lobby_joined", Callable.From<long, long, bool, long>(OnLobbyJoined));
        steam.Connect("join_requested", Callable.From<long, long>((lobby, _) => JoinRequested?.Invoke(lobby)));
        steam.Connect("lobby_match_list", Callable.From<Godot.Collections.Array>(arr => { var l = new List<long>(); foreach (var v in arr) l.Add((long)v); LobbyList?.Invoke(l.ToArray()); }));
    }

    Action later;                                          // действия, которые нельзя выполнять внутри обратного вызова Steam (создание сокета P2P)

    public void ClearHandlers() { Ready = null; Failed = null; }

    public void Poll(double dt)
    {
        if (!inited) return;
        var a = later; later = null; a?.Invoke();
        steam.Call("run_callbacks");
    }

    public Error StartHost(NetHostOptions o)
    {
        if (!IsAvailable(out var why)) { Failed?.Invoke(why); return Error.Unavailable; }
        hosting = true; steam.Call("createLobby", o.FriendsOnly ? 1 : 2, o.MaxPlayers);            // ELobbyType: 1 — только друзья, 2 — публичное; результат придёт в lobby_created
        return Error.Ok;
    }

    void OnLobbyCreated(long connect, long id)
    {
        if (connect != 1) { Failed?.Invoke($"Steam не смог создать лобби (код {connect})"); return; }
        lobbyId = id;
        steam.Call("setLobbyData", id, "game", "lanther"); steam.Call("setLobbyData", id, "version", Game.VersionStr); steam.Call("setLobbyData", id, "proto", NetConst.Protocol);
        later = () =>
        {
            peer = ClassDB.Instantiate("SteamMultiplayerPeer").As<MultiplayerPeer>();
            var r = peer.Call("create_host", 0); if (r.AsInt32() != 0) { peer = null; Failed?.Invoke($"Не удалось открыть сокет Steam P2P (код {r})"); return; }
            peer.Set("server_relay", true); Ready?.Invoke();
        };
    }

    public Error StartClient(string target)
    {
        if (!long.TryParse(target.Trim(), out var id)) { Failed?.Invoke("Неверный идентификатор лобби"); return Error.InvalidParameter; }
        if (!IsAvailable(out var why)) { Failed?.Invoke(why); return Error.Unavailable; }
        steam.Call("joinLobby", id); return Error.Ok;
    }

    void OnLobbyJoined(long id, long permissions, bool locked, long response)
    {
        if (hosting) return;                                    // создатель лобби тоже получает lobby_joined для своего лобби — это не вход клиента
        if (response != 1) { Failed?.Invoke(response == 5 ? "Лобби заполнено или недоступно" : $"Не удалось войти в лобби (код {response})"); return; }
        string ver = steam.Call("getLobbyData", id, "version").AsString(); string proto = steam.Call("getLobbyData", id, "proto").AsString();
        if (ver != Game.VersionStr || proto != NetConst.Protocol) { steam.Call("leaveLobby", id); Failed?.Invoke($"Несовместимая версия игры хоста ({ver})"); return; }
        long owner = steam.Call("getLobbyOwner", id).AsInt64(); lobbyId = id;
        later = () =>
        {
            peer = ClassDB.Instantiate("SteamMultiplayerPeer").As<MultiplayerPeer>();
            var r = peer.Call("create_client", owner, 0); if (r.AsInt32() != 0) { peer = null; Failed?.Invoke($"Не удалось подключиться к хосту Steam (код {r})"); return; }
            peer.Set("server_relay", true); Ready?.Invoke();
        };
    }

    /// <summary>Поиск открытых лобби Lanther; результат — событие LobbyList.</summary>
    public void RequestLobbies()
    {
        if (!IsAvailable(out _)) return;
        steam.Call("addRequestLobbyListStringFilter", "game", "lanther", 0); steam.Call("requestLobbyList");
    }

    public string LobbyInfo(long id) => steam.Call("getLobbyData", id, "version").AsString();
    public void OpenInviteDialog() { if (inited && lobbyId != 0) steam.Call("activateGameOverlayInviteDialog", lobbyId); }

    public void Close()
    {
        if (peer != null) peer.Call("close");
        if (inited && lobbyId != 0) steam.Call("leaveLobby", lobbyId);
        peer = null; lobbyId = 0; hosting = false;
    }

    public ulong UserIdOf(long peerId) => peer != null && peerId != 0 && ClassDB.ClassHasMethod("SteamMultiplayerPeer", "get_steam_id_for_peer_id") ? (ulong)peer.Call("get_steam_id_for_peer_id", peerId).AsInt64() : 0;
    public string UserNameOf(long peerId) { ulong id = UserIdOf(peerId); return id == 0 || !inited ? "" : steam.Call("getFriendPersonaName", (long)id).AsString(); }
}
