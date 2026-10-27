using Godot;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Lamplighter;

/// <summary>Локализация: исходные тексты — русские; английский берётся из res://lang/en.json (точные строки и шаблоны с {0}, {1}…).
/// Все тексты интерфейса проходят через Loc.T при отрисовке, поэтому в коде не нужно разбрасывать ключи.</summary>
public static class Loc
{
    public static string Lang = "ru";
    static readonly Dictionary<string, string> exact = new();
    static readonly List<(Regex re, string tpl, int n)> pats = new();
    static readonly Dictionary<string, string> cache = new();
    static readonly List<string> prefixes = new(), suffixes = new();
    public static readonly HashSet<string> Missing = new();
    public static bool LogMissing;

    public static void SetLang(string l)
    {
        Lang = l; cache.Clear(); exact.Clear(); pats.Clear(); prefixes.Clear(); suffixes.Clear();
        if (l == "ru") return;
        var path = $"res://lang/{l}.json";
        if (!FileAccess.FileExists(path)) return;
        try
        {
            var d = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(FileAccess.GetFileAsString(path));
            foreach (var kv in d)
            {
                if (string.IsNullOrEmpty(kv.Value)) continue;
                if (Regex.IsMatch(kv.Key, @"\{\d+\}"))
                {
                    var esc = Regex.Escape(kv.Key); int n = 0;
                    var re = Regex.Replace(esc, @"\\\{(\d+)\}", m => { n++; return "(?<g" + m.Groups[1].Value + ">.+?)"; });
                    pats.Add((new Regex("^" + re + "$", RegexOptions.Singleline | RegexOptions.Compiled), kv.Value, n));
                }
                else
                {
                    exact[kv.Key] = kv.Value;
                    if (kv.Key.Length > 2 && (kv.Key.StartsWith(" ") || kv.Key.StartsWith(","))) suffixes.Add(kv.Key);
                    else if (kv.Key.Length > 2 && (kv.Key.EndsWith(" ") || kv.Key.EndsWith(":"))) prefixes.Add(kv.Key);
                }
            }
            prefixes.Sort((a, b) => b.Length.CompareTo(a.Length)); suffixes.Sort((a, b) => b.Length.CompareTo(a.Length));
            // более длинные (специфичные) шаблоны проверяются раньше
            pats.Sort((a, b) => b.re.ToString().Length.CompareTo(a.re.ToString().Length));
        }
        catch (Exception e) { GD.PushWarning("lang load failed: " + e.Message); }
    }

    static readonly Regex Run = new(@"[А-Яа-яЁё][А-Яа-яЁё .,'«»…—\-:!?]*[А-Яа-яЁё.»]|[А-Яа-яЁё]", RegexOptions.Compiled);
    static readonly Regex Bullet = new(@"^((?:[▶•★☆✓●◆○▮ ]+|\[[^\]]{1,2}\] ))(.+)$", RegexOptions.Compiled | RegexOptions.Singleline);
    static bool HasCyr(string s) { foreach (var c in s) if (c >= 'А' && c <= 'я' || c == 'ё' || c == 'Ё') return true; return false; }

    public static string T(string s)
    {
        if (Lang == "ru" || string.IsNullOrEmpty(s) || !HasCyr(s)) return s;
        if (cache.TryGetValue(s, out var c)) return c;
        string r = Translate(s);
        if (cache.Count > 6000) cache.Clear();
        cache[s] = r; return r;
    }

    static string Translate(string s)
    {
        if (exact.TryGetValue(s, out var e)) return e;
        foreach (var (re, tpl, n) in pats)
        {
            var m = re.Match(s); if (!m.Success) continue;
            string r = tpl;
            foreach (Group g in m.Groups) if (g.Name.StartsWith("g")) r = r.Replace("{" + g.Name.Substring(1) + "}", T(g.Value));
            return r;
        }
        // маркеры списков и значки в начале строки
        var bm = Bullet.Match(s); if (bm.Success) return bm.Groups[1].Value + T(bm.Groups[2].Value);
        // строка, собранная из «хвоста»/«головы» + динамической части
        foreach (var suf in suffixes) if (s.Length > suf.Length && s.EndsWith(suf)) return T(s.Substring(0, s.Length - suf.Length)) + exact[suf];
        foreach (var pre in prefixes) if (s.Length > pre.Length && s.StartsWith(pre)) return exact[pre] + T(s.Substring(pre.Length));
        // многострочные и составные тексты: переводим по частям, разделённым " · " или переводом строки
        foreach (var sep in new[] { "\n", " · ", " — ", ": " })
        {
            if (!s.Contains(sep)) continue;
            var parts = s.Split(sep); bool any = false; var o = new string[parts.Length];
            for (int i = 0; i < parts.Length; i++) { o[i] = T(parts[i]); if (o[i] != parts[i]) any = true; }
            if (any) return string.Join(sep, o);
        }
        // последний шанс: переводим отдельные русские фрагменты внутри строки (числа, скобки и т.п. остаются)
        string r2 = Run.Replace(s, m => exact.TryGetValue(m.Value, out var t) ? t : m.Value);
        if (LogMissing && HasCyr(r2)) Missing.Add(s);
        return r2;
    }
}
