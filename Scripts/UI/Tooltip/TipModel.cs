using Godot;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Lamplighter;

public enum TipKind { Action, Need, Gives, Info, Warn }

public readonly record struct TipSpan(string Text, TipRole Role);

/// <summary>Строка подсказки: действие (с клавишей), цена, награда, справка или предупреждение.</summary>
public sealed class TipRow
{
    public TipKind Kind; public string Key = ""; public bool Ok = true;     // Ok: для действия — доступно; для цены — хватает
    public readonly List<TipSpan> Spans = new();
}

/// <summary>Готовая к показу подсказка: ответ на «что можно сделать / что нужно / что получу».</summary>
public sealed class TipModel
{
    public string Title = "";
    public readonly List<TipRow> Rows = new();

    public bool HasAction { get { foreach (var r in Rows) if (r.Kind == TipKind.Action) return true; return false; } }
    /// <summary>Первое действие доступно (или действий нет).</summary>
    public bool PrimaryOk { get { foreach (var r in Rows) if (r.Kind == TipKind.Action) return r.Ok; return true; } }

    static readonly Regex KeyLine = new(@"^\[(\w{1,2})\]\s*(.*)$", RegexOptions.Compiled);

    /// <summary>Собирает подсказку из Ix: структурные строки (Ix.Action/Need/Gives…) берутся как есть, обычный текст разбирается эвристикой.</summary>
    public static TipModel From(Ix ix)
    {
        var m = new TipModel { Title = ix.Title ?? "" }; TipRow lastAction = null;
        foreach (var line in ix.Lines)
        {
            var rows = ix.Rich.TryGetValue(line, out var rich) ? rich : Parse(line);
            foreach (var r in rows)
            {
                if (r.Kind == TipKind.Action) lastAction = r;
                else if (!r.Ok && (r.Kind == TipKind.Need || r.Kind == TipKind.Warn) && lastAction != null) lastAction.Ok = false;
                m.Rows.Add(r);
            }
        }
        return m;
    }

    /// <summary>Запасной разбор обычных строк: «[E] Действие: пояснение» → действие + справка; «Не хватает: …» → красное предупреждение.</summary>
    static List<TipRow> Parse(string line)
    {
        var l = new List<TipRow>(); var k = KeyLine.Match(line);
        if (k.Success)
        {
            string rest = k.Groups[2].Value; int c = rest.IndexOf(": "); string tail = "";
            if (c > 0) { tail = rest[(c + 2)..]; rest = rest[..c]; }
            var a = new TipRow { Kind = TipKind.Action, Key = k.Groups[1].Value }; a.Spans.Add(new(rest, TipRole.Text)); l.Add(a);
            if (tail != "") { var i = new TipRow { Kind = TipKind.Info }; i.Spans.Add(new(tail, TipRole.Dim)); l.Add(i); }
            return l;
        }
        var r = new TipRow { Kind = TipKind.Info };
        if (line.StartsWith("Не хватает: ")) { r.Kind = TipKind.Warn; r.Ok = false; r.Spans.Add(new(line, TipRole.Bad)); }
        else if (line.StartsWith("Нет связи")) { r.Kind = TipKind.Warn; r.Spans.Add(new(line, TipRole.Bad)); }
        else if (line.StartsWith("Максимальный уровень")) r.Spans.Add(new(line, TipRole.Dim));
        else r.Spans.Add(new(line, TipRole.Text));
        l.Add(r); return l;
    }
}

/// <summary>Структурные подсказки: игровой код говорит, что это (действие, цена, награда), а не как это выглядит.
/// Каждый вызов добавляет и обычную строку в Lines (для тестов и запасного разбора), и структурную строку в Rich.</summary>
public sealed partial class Ix
{
    public readonly Dictionary<string, List<TipRow>> Rich = new();

    void Put(string plain, TipRow row) { Lines.Add(plain); Rich[plain] = new List<TipRow> { row }; }

    /// <summary>«[E] Улучшить маяк». Доступность выводится из следующих за ним строк цены.</summary>
    public Ix Action(string key, string text)
    {
        var r = new TipRow { Kind = TipKind.Action, Key = key }; r.Spans.Add(new(text, TipRole.Text)); Put($"[{key}] {text}", r); return this;
    }

    /// <summary>«Нужно: 1 Ядро света». Если чего-то не хватает — строка красная и показывает, сколько есть сейчас.</summary>
    public Ix Need(params (string name, int need, int have)[] items)
    {
        var r = new TipRow { Kind = TipKind.Need }; r.Spans.Add(new("Нужно: ", TipRole.Dim)); var plain = new List<string>(); bool first = true;
        foreach (var (name, need, have) in items)
        {
            bool ok = have >= need; r.Ok &= ok;
            if (!first) r.Spans.Add(new(", ", TipRole.Dim)); first = false;
            r.Spans.Add(new($"{need} ", ok ? TipRole.Ok : TipRole.Bad)); r.Spans.Add(new(name, TipRole.Resource));
            if (!ok) r.Spans.Add(new($" ({have})", TipRole.Bad));
            plain.Add($"{need} {name}" + (ok ? "" : $" (есть {have})"));
        }
        Put("Нужно: " + string.Join(", ", plain), r); return this;
    }

    /// <summary>«Откроет: Затопленные шахты». Роль задаёт цвет награды (по умолчанию — зона/открытие).</summary>
    public Ix Gives(string label, string text, TipRole role = TipRole.Zone)
    {
        var r = new TipRow { Kind = TipKind.Gives }; r.Spans.Add(new(label + ": ", TipRole.Dim)); r.Spans.Add(new(text, role)); Put($"{label}: {text}", r); return this;
    }

    public Ix Info(string text, TipRole role = TipRole.Dim)
    {
        var r = new TipRow { Kind = TipKind.Info }; r.Spans.Add(new(text, role)); Put(text, r); return this;
    }

    /// <summary>Красное предупреждение. blocks — оно же делает предыдущее действие недоступным.</summary>
    public Ix Warn(string text, bool blocks = false)
    {
        var r = new TipRow { Kind = TipKind.Warn, Ok = !blocks }; r.Spans.Add(new(text, TipRole.Bad)); Put(text, r); return this;
    }
}
