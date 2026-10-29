namespace Lamplighter;

public enum SessionMode { None, Solo, Host, Guest }

/// <summary>Выбранные герой и мир текущего запуска. Статическое: переживает перезагрузку сцены (переход из главного меню в игру и обратно).
/// Solo/Host открывают файлы героя и мира; Guest — только героя (мир приходит от хоста). Mode == None — главное меню без игры, ничего не сохраняется.</summary>
public static class GameSession
{
    public static SessionMode Mode = SessionMode.None;
    public static string CharacterId, WorldId;
    public static Look GuestLook;                                     // внешность, которую гость отправляет хосту при рукопожатии
    public static string PendingTransport, PendingTarget;             // приглашение или ID лобби, ожидающие выбора героя

    public static bool Active => Mode != SessionMode.None;

    public static void Begin(SessionMode mode, string charId, string worldId) { Mode = mode; CharacterId = charId; WorldId = worldId; }

    public static void Clear() { Mode = SessionMode.None; CharacterId = null; WorldId = null; GuestLook = null; }
}
