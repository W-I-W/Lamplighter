using Godot;
using System;

namespace Lamplighter;

public enum NetRole { Offline, Host, Client }
public enum NetState { Offline, Connecting, Handshaking, Online, Failed }

/// <summary>Параметры создания игры хостом. FriendsOnly — только друзья (для Steam-лобби), иначе публичное лобби.</summary>
public sealed class NetHostOptions
{
    public int MaxPlayers = NetConst.MaxPlayers; public bool FriendsOnly = true; public int Port = NetConst.DefaultPort;
}

public static class NetConst
{
    public const int MaxPlayers = 4;                       // включая хоста
    public const int DefaultPort = 24865;
    public const string DevAppId = "480";                   // Spacewar: только для разработки, пока нет своего AppID
    public const string Protocol = "lanther-net-1";        // версия сетевого протокола; меняется при несовместимых изменениях сообщений
    public const int GenVersion = 1;                       // версия алгоритма генерации мира: клиент обязан генерировать тот же мир
    public const float StateHz = 15f;                      // частота отправки позиции
    public const float HandshakeTimeout = 6f;
}

/// <summary>Участник сеанса. UserId — идентификатор, подтверждённый транспортом (для Steam — Steam ID, полученный от Steam, а не от клиента); 0 — транспорт его не даёт.</summary>
public sealed class NetPlayer
{
    public long PeerId; public ulong UserId; public string Name = "";
    public Vector2 Pos, Shown; public int Face = 1; public float Hp = 100, MaxHp = 100; public byte Flags;   // Flags: бит0 — рывок, бит1 — замах
    public double LastRecv, Joined; public bool Handshaken;
    public float Walk; public Vector2 PrevShown;
}
