using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Состояние сетевого сеанса. Статическое, потому что пережитое перезагрузкой сцены (новая игра, вход клиента): транспорт и список участников не принадлежат одному Game.
/// Игровая логика обращается только к этому классу и к NetNode (RPC), а не к конкретному транспорту.</summary>
public static class NetService
{
    public static NetRole Role = NetRole.Offline;
    public static NetState State = NetState.Offline;
    public static string Status = "";                              // человекочитаемое состояние или причина ошибки
    public static INetTransport Transport;
    public static readonly Dictionary<long, NetPlayer> Players = new();
    public static string LocalName = "Игрок"; public static string VersionOverride;      // только для проверок несовместимой версии
    public static long LocalId = 1;                                // идентификатор своего узла в мультиплеере (1 — хост)

    // мир, выданный хостом клиенту при подключении
    public static int? WorldSeed; public static int WorldGen; public static Vector2 SpawnPos; public static bool WorldPending;
    public static byte[] PendingPoi, PendingAnc; public static int[] PendingTiles, PendingLamps;                   // снимок общего прогресса мира от хоста: применяется, когда мир клиента готов

    public static event Action<NetPlayer> PlayerJoined, PlayerLeft;
    public static event Action<string> Notice;                     // короткие сообщения для игрока
    public static event Action<string, string> Invite;             // приглашение (транспорт, цель): игра сначала предлагает выбрать героя, подключение выполняет она сама
    public static event Action Ended;                              // сеанс завершён (хост вышел, потеря связи, выход)

    public static bool Active => Role != NetRole.Offline && State != NetState.Failed;
    public static bool Online => State == NetState.Online;
    public static int Count => Players.Count;

    public static readonly Dictionary<string, Func<INetTransport>> Transports = new() { ["enet"] = () => new EnetTransport(), ["steam"] = () => SteamTransport.Shared };

    public static INetTransport Make(string id) => Transports.TryGetValue(id, out var f) ? f() : null;

    // ── запуск
    public static bool StartHost(string transportId, NetHostOptions o)
    {
        Leave(false); var t = Make(transportId); if (t == null) return Fail("Неизвестный транспорт");
        if (!t.IsAvailable(out var why)) return Fail(why);
        AutoName(t);
        Transport = t; Role = NetRole.Host; State = NetState.Connecting; Status = "Создание игры…"; Players.Clear(); WorldSeed = null;
        t.Failed += OnTransportFailed; t.Ready += OnHostReady; hostOpts = o;
        var err = t.StartHost(o); if (err != Error.Ok) return Fail(Status != "" && State == NetState.Failed ? Status : $"Не удалось создать игру ({err})");
        return true;
    }

    /// <summary>Если игрок не задал имя, берём имя из Steam, а без Steam — имя пользователя системы: иначе у всех участников было бы одинаковое «Игрок».</summary>
    static void AutoName(INetTransport t)
    {
        if (LocalName != "" && LocalName != "Игрок") return;
        string n = t is SteamTransport st ? st.LocalPersona : ""; if (n == "") n = OS.GetEnvironment("USERNAME"); if (n == "") n = OS.GetEnvironment("USER");
        if (n != "") LocalName = n.Length > 20 ? n.Substring(0, 20) : n;
    }

    static NetHostOptions hostOpts;
    static void OnHostReady() { Node?.AttachPeer(); }

    public static bool Join(string transportId, string target)
    {
        Leave(false); var t = Make(transportId); if (t == null) return Fail("Неизвестный транспорт");
        if (!t.IsAvailable(out var why)) return Fail(why);
        AutoName(t);
        Transport = t; Role = NetRole.Client; State = NetState.Connecting; Status = "Подключение…"; Players.Clear(); WorldSeed = null; WorldPending = false; PendingPoi = null; PendingAnc = null; PendingTiles = null; PendingLamps = null;
        t.Failed += OnTransportFailed; t.Ready += () => Node?.AttachPeer();
        var err = t.StartClient(target); if (err != Error.Ok) return Fail(State == NetState.Failed ? Status : $"Не удалось подключиться ({err})");
        return true;
    }

    static void OnTransportFailed(string why) => Fail(why);

    public static bool Fail(string why)
    {
        Status = why; State = NetState.Failed; Notice?.Invoke(why); GD.Print("[net] " + why);
        var t = Transport; Transport = null; t?.Close(); t?.ClearHandlers(); Role = NetRole.Offline; Players.Clear(); return false;
    }

    /// <summary>Выход из сеанса. notify — сообщить игре (возврат в меню и т.п.).</summary>
    public static void Leave(bool notify = true)
    {
        bool was = Role != NetRole.Offline;
        Node?.DetachPeer();
        var t = Transport; Transport = null; t?.Close(); t?.ClearHandlers();
        Role = NetRole.Offline; State = NetState.Offline; Players.Clear(); WorldSeed = null; WorldPending = false; PendingPoi = null; PendingAnc = null; PendingTiles = null; PendingLamps = null; Status = "";
        if (was && notify) Ended?.Invoke();
    }

    // ── вызывается NetNode
    public static NetNode Node;
    public static void RaiseJoined(NetPlayer p) { PlayerJoined?.Invoke(p); }
    public static void RaiseLeft(NetPlayer p) { PlayerLeft?.Invoke(p); }
    public static void RaiseNotice(string s) { Notice?.Invoke(s); }
    public static void RaiseEnded() { Ended?.Invoke(); }

    // ── Steam: приглашения из Overlay и аргумент запуска +connect_lobby
    static bool steamWatch;
    /// <summary>Однократно при старте игры: если Steam доступен, принимает приглашения друзей (Overlay) и аргумент +connect_lobby. Без Steam ничего не делает.</summary>
    public static void WatchSteamInvites()
    {
        if (steamWatch) return; steamWatch = true;
        var st = SteamTransport.Shared; if (!st.IsAvailable(out _)) return;
        st.JoinRequested += id => Invite?.Invoke("steam", id.ToString());
        var args = OS.GetCmdlineArgs();
        for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "+connect_lobby" && long.TryParse(args[i + 1], out var lobby)) Invite?.Invoke("steam", lobby.ToString());
    }

    /// <summary>Каждый кадр без активного сеанса: обработка обратных вызовов Steam (приглашения).</summary>
    public static void PollIdle(double dt) { if (Transport == null && steamWatch) SteamTransport.Shared.Poll(dt); }

    public static NetPlayer Local => Players.TryGetValue(LocalId, out var p) ? p : null;
}
