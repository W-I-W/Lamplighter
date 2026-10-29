using Godot;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lamplighter;

/// <summary>Вид сообщения: ChatMessage — игрок, SystemMessage — служебное, WorldEvent — событие мира, ImportantNotification — важное (дополнительно показывает короткий баннер).</summary>
public enum MsgKind { Chat, System, World, Important }

/// <summary>Цвет по смыслу: белый — игроки, золотой — рецепты, постройки, улучшения, зелёный — успех, голубой — исследование, красный — предупреждения, серый — служебное.</summary>
public enum MsgTone { Chat, Gold, Good, Cyan, Warn, Info }

public sealed class GameMessage
{
    public int Id; public MsgKind Kind; public MsgTone Tone; public string Sender = ""; public string Text = ""; public string Icon = "";
    public int Priority; public float Time; public int Repeat = 1;
    public bool Shared;                       // событие общего мира или сообщение игрока: уходит остальным участникам игры
    public bool Silent;                       // из истории при входе: только в журнале, без показа в углу
}

/// <summary>Единый журнал уведомлений и чата: все обычные сообщения игры (бывшие «тосты» и баннеры) попадают сюда, а не в отдельные панели. История — последние 100 сообщений сессии, на диск не пишется.
/// Работает без сети и без Steam; сетевую рассылку делает Game через событие Broadcast.</summary>
public sealed class GameNotificationService
{
    public const int MaxHistory = 100, MaxLen = 200;
    public readonly List<GameMessage> History = new();
    public Action<GameMessage> Broadcast;                 // хост: разослать общее сообщение клиентам
    public event Action<GameMessage> Added;
    public float Clock;                                   // секунды игрового времени (стоит, пока открыто окно или меню)
    int nextId;

    public void Tick(float dt) => Clock += dt;

    public GameMessage Post(MsgKind kind, MsgTone tone, string text, string sender = null, int priority = 0, string icon = null, bool shared = false, bool silent = false)
    {
        text = Clean(text, MaxLen); if (text == "") return null;
        var last = History.Count > 0 ? History[^1] : null;
        if (last != null && !silent && last.Kind == kind && last.Tone == tone && last.Text == text && (last.Sender ?? "") == (sender ?? "") && Clock - last.Time < 3f)
        { last.Repeat++; last.Time = Clock; Added?.Invoke(last); return last; }          // одинаковые подряд сливаются: «текст ×3»
        var m = new GameMessage { Id = ++nextId, Kind = kind, Tone = tone, Text = text, Sender = sender ?? "", Priority = priority, Icon = icon ?? "", Shared = shared || kind == MsgKind.Chat, Silent = silent, Time = silent ? Clock - 600f : Clock };
        History.Add(m); if (History.Count > MaxHistory) History.RemoveAt(0);
        Added?.Invoke(m); if (shared && !silent) Broadcast?.Invoke(m);
        return m;
    }

    public GameMessage Chat(string sender, string text, bool silent = false) => Post(MsgKind.Chat, MsgTone.Chat, text, sender, 0, null, false, silent);
    public GameMessage System(string text, MsgTone tone = MsgTone.Info) => Post(MsgKind.System, tone, text);
    public GameMessage World(string text, MsgTone tone = MsgTone.Cyan, bool shared = false) => Post(MsgKind.World, tone, text, null, 0, null, shared);
    public GameMessage Important(string text, MsgTone tone = MsgTone.Gold) => Post(MsgKind.Important, tone, text, null, 2);

    /// <summary>Последние общие сообщения для игрока, который входит в игру.</summary>
    public List<GameMessage> RecentShared(int n) { var l = new List<GameMessage>(); for (int i = History.Count - 1; i >= 0 && l.Count < n; i--) if (History[i].Shared) l.Insert(0, History[i]); return l; }

    /// <summary>Чистит текст: без управляющих символов, один пробел между словами, не длиннее max. Разметка не разбирается (при показе «[» экранируется), команд в тексте нет.</summary>
    public static string Clean(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(); bool sp = false;
        foreach (var ch in s)
        {
            if (char.IsControl(ch) || ch == '​' || ch == '‮') { sp = sb.Length > 0; continue; }
            if (char.IsWhiteSpace(ch)) { sp = sb.Length > 0; continue; }
            if (sp) { sb.Append(' '); sp = false; }
            sb.Append(ch); if (sb.Length >= max) break;
        }
        return sb.ToString().Trim();
    }

    public static Color ToneColor(MsgTone t) => t switch
    {
        MsgTone.Chat => new Color("#eef2ff"), MsgTone.Gold => new Color("#ffd23f"), MsgTone.Good => new Color("#9be07a"),
        MsgTone.Cyan => new Color("#8ff0ff"), MsgTone.Warn => new Color("#f06a5e"), _ => new Color("#9aa6bf")
    };

    static readonly string[] NameCols = { "#7ad7ff", "#ffb36a", "#b9f08a", "#e49bff", "#ffe27a", "#ff8f9c", "#8fe8c8", "#a9b8ff" };
    /// <summary>Имя игрока — свой устойчивый цвет по имени (не белый, чтобы отличать от текста).</summary>
    public static Color NameColor(string name) { int h = 17; foreach (var c in name ?? "") h = unchecked(h * 31 + c); return new Color(NameCols[Math.Abs(h % NameCols.Length)]); }
}
