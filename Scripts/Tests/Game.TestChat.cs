using Godot;
using System;
using System.Linq;

namespace Lamplighter;

/// <summary>Чат и журнал (LAMP_STAGE=chat): единый журнал вместо панелей, дедупликация, очистка текста, Enter/Esc, блокировка игрового ввода, история, настройки. Скриншоты — по состояниям.</summary>
public partial class Game
{
    static void Press(Key k, bool pressed) => Input.ParseInputEvent(new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = pressed });

    void BuildChatScript()
    {
        int fails = 0; void Check(bool ok, string what) { if (!ok) fails++; GD.Print($"[check] {(ok ? "PASS" : "FAIL")} {what}"); }
        Vector2 p0 = default;
        S(.3f, () =>
        {
            Notify.History.Clear(); Settings.ShowChat = true; Settings.ShowNotes = true; Settings.ChatSecs = 10f; Settings.ChatSize = 0;
            Announce("Открыта постройка: Жилой дом", "подсказка", new Color("#d9b27c"), "tech");
            Toast("WASD — движение · ЛКМ — атака и добыча", MsgTone.Cyan);
            Announce("Древний фонарь активирован", "", new Color("#ffe9a0"), "ancient");
            Fail("Не хватает: дерево");
            Notify.Chat("Damir", "Нашёл босса! [b]жирный[/b] [color=red]x[/color]");
            for (int i = 0; i < 5; i++) Toast("Игра сохранена");
            Check(Notify.History.Count == 6, $"chat: six different messages, the repeats merged ({Notify.History.Count})");
            Check(Notify.History[^1].Repeat == 5, "chat: identical messages are merged into one with a counter");
            Check(Notify.History.Exists(m => m.Text.StartsWith("Открыта постройка")), "chat: ordinary notifications go to the log, not to banner panels");
            Announce("Хранитель Тёмного леса повержен!", "победа", new Color("#ffe9a0"), "ancient");
            Check(Notify.History[^1].Kind == MsgKind.Important && Notify.History[^1].Text.Contains("победа"), "chat: an important victory is a line in the chat, with its short note");
            Check(GameNotificationService.Clean("  a\u0007b \n\n c  ", 200) == "a b c" && GameNotificationService.Clean(new string('x', 500), 200).Length == 200, "chat: control characters are stripped and the length is limited to 200");
            Check(Notify.Chat("X", "   ") == null, "chat: an empty message is not accepted");
            for (int i = 0; i < 130; i++) Notify.System("Сообщение " + i);
            Check(Notify.History.Count == GameNotificationService.MaxHistory, "chat: only the last 100 messages are kept");
            Notify.Chat("Damir", "Нашёл босса! [b]жирный[/b] [color=red]x[/color]");
        });
        S(.6f, () => Shot("chat1_corner"));
        S(.1f, () => { p0 = PPos; Press(Key.Enter, true); Press(Key.Enter, false); });
        S(.4f, () =>
        {
            Check(ChatOpen, "chat: Enter opens the input");
            Press(Key.W, true); Press(Key.Space, true);
        });
        S(.6f, () =>
        {
            Check(PPos.DistanceTo(p0) < .5f, $"chat: the hero does not move while typing ({PPos.DistanceTo(p0):F2})");
            Press(Key.W, false); Press(Key.Space, false);
            Shot("chat2_open");
            Press(Key.Escape, true); Press(Key.Escape, false);
        });
        S(.3f, () =>
        {
            Check(!ChatOpen && Window == Win.None && Menu == Mn.None, "chat: Esc closes only the chat (no menu opens)");
            Notify.History.Clear(); int n0 = 0; SendChat("  Привет всем  "); SendChat("   ");
            Check(Notify.History.Count == n0 + 1 && Notify.History[^1].Text == "Привет всем" && Notify.History[^1].Sender == ChatName, "chat: solo message is posted with the player's name, the empty one is not sent");
            Settings.ChatSize = 1; Shot("chat3_large");
            Settings.ChatSize = 0; Settings.ShowNotes = false;
        });
        S(.3f, () => { Shot("chat4_chat_only"); Settings.ShowNotes = true; });
        S(.1f, () => { Notify.Clock += 30f; });
        S(.3f, () => { Check(!Chat.IsOpen, "chat: closed again"); Shot("chat5_faded"); });
        S(.1f, () => { Log("chat done"); GD.Print("[check] SUMMARY failures=" + fails); GD.Print("CHAT DONE"); });
    }
}
