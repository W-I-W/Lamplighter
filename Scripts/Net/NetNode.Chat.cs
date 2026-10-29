using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Lamplighter;

/// <summary>Чат по сети: клиент отправляет текст хосту, хост определяет отправителя по соединению (имя из своей записи, не от клиента), проверяет частоту и длину и рассылает всем рукопожатым игрокам.
/// Общие события мира (кто вошёл или вышел) хост рассылает так же. Текст никогда не разбирается как разметка или команда. Отдельного лобби для чата нет — тот же канал игры.</summary>
public partial class NetNode
{
    readonly Dictionary<long, (double last, double winStart, int n)> chatRate = new();
    const int ChatHistoryOnJoin = 20;

    /// <summary>Отправить своё сообщение: хост обрабатывает сразу, клиент — через хоста (на экране оно появится, когда хост его разошлёт, поэтому дублей нет).</summary>
    public void SendChat(string text)
    {
        if (Multiplayer.MultiplayerPeer == null) return;
        if (Multiplayer.IsServer()) HostChat(1, text); else RpcId(1, nameof(RpcChat), text);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcChat(string text)
    {
        if (!Multiplayer.IsServer()) return;
        HostChat(Multiplayer.GetRemoteSenderId(), text);
    }

    bool ChatRateOk(long id)
    {
        chatRate.TryGetValue(id, out var r); if (now - r.winStart >= 10) { r.winStart = now; r.n = 0; }
        if (now - r.last < .5 || r.n >= 6) { chatRate[id] = r; return false; }          // не чаще раза в полсекунды и шести сообщений за десять секунд
        r.last = now; r.n++; chatRate[id] = r; return true;
    }

    void HostChat(long id, string text)
    {
        if (!NetService.Players.TryGetValue(id, out var p) || !p.Handshaken) return;
        text = GameNotificationService.Clean(text, GameNotificationService.MaxLen); if (text == "" || !ChatRateOk(id)) return;
        var m = G.Notify.Chat(p.Name, text); if (m == null) return;                            // у хоста сообщение появляется здесь, у остальных — после рассылки
        BroadcastChat(m, 0);
    }

    /// <summary>Хост → клиенты: одно общее сообщение (чат или событие мира). only != 0 — одному игроку.</summary>
    public void BroadcastChat(GameMessage m, long only)
    {
        if (!Multiplayer.IsServer()) return;
        foreach (var t in NetService.Players.Values.Where(x => x.Handshaken && x.PeerId != 1 && (only == 0 || x.PeerId == only)))
            RpcId(t.PeerId, nameof(RpcChatLine), (int)m.Kind, (int)m.Tone, m.Sender, m.Text);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcChatLine(int kind, int tone, string sender, string text)
    {
        if (Multiplayer.GetRemoteSenderId() != 1) return;
        ApplyChatLine(kind, tone, sender, text, false);
    }

    void ApplyChatLine(int kind, int tone, string sender, string text, bool silent)
    {
        var k = kind == (int)MsgKind.Chat ? MsgKind.Chat : MsgKind.World;                    // от хоста принимаются только чат и события мира
        var t = k == MsgKind.Chat ? MsgTone.Chat : (MsgTone)Mathf.Clamp(tone, 0, 5);
        sender = GameNotificationService.Clean(sender, 24);
        if (k == MsgKind.Chat) G.Notify.Chat(sender == "" ? "?" : sender, text, silent); else G.Notify.Post(MsgKind.World, t, text, null, 0, null, false, silent);
    }

    /// <summary>Вошедшему игроку — последние 20 общих сообщений (только журнал, в углу не показываются).</summary>
    public void SendChatHistory(long id)
    {
        var l = G.Notify.RecentShared(ChatHistoryOnJoin); if (l.Count == 0) return;
        RpcId(id, nameof(RpcChatHistory), l.Select(m => (int)m.Kind * 10 + (int)m.Tone).ToArray(), l.Select(m => m.Sender).ToArray(), l.Select(m => m.Text).ToArray());
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcChatHistory(int[] kinds, string[] senders, string[] texts)
    {
        if (Multiplayer.GetRemoteSenderId() != 1 || kinds == null || senders == null || texts == null) return;
        int n = System.Math.Min(ChatHistoryOnJoin, System.Math.Min(kinds.Length, System.Math.Min(senders.Length, texts.Length)));
        for (int i = 0; i < n; i++) ApplyChatLine(kinds[i] / 10, kinds[i] % 10, senders[i], texts[i], true);
    }
}
