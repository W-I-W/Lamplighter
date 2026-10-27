using Godot;
using System.Text;
using System.Text.RegularExpressions;

namespace Lamplighter;

/// <summary>Раскраска игрового текста (BBCode для RichTextLabel): свет — золотой с пульсацией, Тьма — фиолетовая с пульсацией, герой — голубой,
/// опасность — красная, клавиши в скобках — золотые. Работает для русского и английского текста.</summary>
public static class TextFx
{
    static readonly Regex Rx = new(
        @"(?<key>\[[^\[\]]{1,9}\])|(?<level>Уровень \d+|Level \d+|level \d+)|(?<good>\+\d+(?:[.,]\d+)?%?|выросл\w+|повыс\w+|restored|increased)|(?<hero>[Фф]онарщик\w*|lamplighter)|(?<dark>Источник\w* Тьмы|Тьм\w+|погасил\w*|Source of Darkness|Darkness|extinguished)|(?<bad>опасн\w*|смерт\w*|враг\w*|монстр\w*|danger\w*|enem(?:y|ies)|deadly)|(?<hp>здоров\w+|health)|(?<stat>добыч\w+|урон\w*|damage|mining|скорост\w+|speed)|(?<light>Сеть Маяков|Центральн\w+ маяк\w*|ядер света|ядр\w+|свет\w*|фонар\w+|маяк\w*|Beacon Network|Central Beacon|cores? of light|light|lanterns?|beacon\w*)|(?<num>\d+(?:/\d+)?)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    static string Esc(string s) => s.Replace("[", "[lb]");

    public static string Colorize(string t)
    {
        var sb = new StringBuilder(); int last = 0;
        foreach (Match m in Rx.Matches(t))
        {
            sb.Append(Esc(t.Substring(last, m.Index - last))); last = m.Index + m.Length; string v = Esc(m.Value);
            if (m.Groups["key"].Success) sb.Append($"[color=#ffe08a]{v}[/color]");
            else if (m.Groups["hero"].Success) sb.Append($"[color=#8ff0ff]{v}[/color]");
            else if (m.Groups["dark"].Success) sb.Append($"[pulse freq=1.2 color=#c070ff55 ease=-2][color=#c070ff]{v}[/color][/pulse]");
            else if (m.Groups["level"].Success) sb.Append($"[pulse freq=1.4 color=#ffffff77 ease=-2][color=#ffd86b]{v}[/color][/pulse]");
            else if (m.Groups["good"].Success) sb.Append($"[color=#9be07a]{v}[/color]");
            else if (m.Groups["hp"].Success) sb.Append($"[color=#f06a5e]{v}[/color]");
            else if (m.Groups["stat"].Success) sb.Append($"[color=#ffb36a]{v}[/color]");
            else if (m.Groups["num"].Success) sb.Append($"[color=#ffd86b]{v}[/color]");
            else if (m.Groups["bad"].Success) sb.Append($"[color=#f06a5e]{v}[/color]");
            else sb.Append($"[pulse freq=1.6 color=#ffe08a66 ease=-2][color=#ffe08a]{v}[/color][/pulse]");
        }
        sb.Append(Esc(t.Substring(last))); return sb.ToString();
    }

    /// <summary>Печатная машинка: сколько символов показано через age секунд при cps символах в секунду (-1 — все).</summary>
    public static int Typed(string plain, float age, float cps) { int n = (int)(age * cps); return n >= plain.Length ? -1 : n; }

    /// <summary>Подготавливает RichTextLabel: цвет по умолчанию, раскраска и печать. Сброс печати при смене текста.</summary>
    public static void Apply(RichTextLabel l, string plain, Color baseCol, float cps, bool center = false)
    {
        l.BbcodeEnabled = true; l.ScrollActive = false; l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        string bb = Colorize(plain); if (center) bb = "[center]" + bb + "[/center]";
        if (l.Text != bb) l.Text = bb;
        if (!l.HasThemeColorOverride("default_color") || l.GetThemeColor("default_color") != baseCol) l.AddThemeColorOverride("default_color", baseCol);
        string last = l.HasMeta("k") ? (string)l.GetMeta("k") : null;
        if (last != plain) { l.SetMeta("k", plain); l.SetMeta("t", Time.GetTicksMsec() / 1000.0); }
        l.VisibleCharacters = cps <= 0 ? -1 : Typed(plain, (float)(Time.GetTicksMsec() / 1000.0 - (double)l.GetMeta("t")), cps);
    }
}
