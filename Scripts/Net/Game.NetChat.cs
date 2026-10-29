using Godot;

namespace Lamplighter;

public partial class Game
{
    /// <summary>Игрок отправил сообщение из чата. В сети — через хоста (он проверяет отправителя и частоту), без сети — прямо в журнал.</summary>
    public string ChatName => PlayerName != "" ? PlayerName : Loc.T("Игрок");

    public void SendChat(string text)
    {
        text = GameNotificationService.Clean(text, GameNotificationService.MaxLen); if (text == "") return;
        if (NetService.Active && Net != null) Net.SendChat(text); else Notify.Chat(ChatName, text);
    }
}
