using Godot;
using System;

namespace Lamplighter;

/// <summary>Транспорт сетевой игры. Игровая сетевая логика (NetService/NetNode) знает только этот интерфейс и MultiplayerPeer, поэтому ENet, Steam и любой будущий
/// транспорт взаимозаменяемы без переписывания мультиплеера.</summary>
public interface INetTransport
{
    string Id { get; }
    string DisplayName { get; }
    /// <summary>Можно ли пользоваться транспортом сейчас; reason — почему нет (показывается игроку).</summary>
    bool IsAvailable(out string reason);
    MultiplayerPeer Peer { get; }
    /// <summary>Создать хост. Для транспортов с асинхронной подготовкой (лобби Steam) готовность сообщает событие Ready.</summary>
    Error StartHost(NetHostOptions options);
    /// <summary>Подключиться: для ENet — «адрес:порт», для Steam — идентификатор лобби.</summary>
    Error StartClient(string target);
    void Close();
    void Poll(double dt);
    /// <summary>Проверенный транспортом идентификатор пользователя участника (клиент не может его подменить). 0 — не поддерживается.</summary>
    ulong UserIdOf(long peerId);
    string UserNameOf(long peerId);
    /// <summary>Текст для приглашения друзей (адрес для ENet, идентификатор лобби для Steam) или пусто.</summary>
    string InviteInfo { get; }
    /// <summary>Отписывает обработчики событий (транспорт Steam один на процесс и переиспользуется между сеансами).</summary>
    void ClearHandlers();
    event Action Ready;
    event Action<string> Failed;
}
