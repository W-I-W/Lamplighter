using Godot;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Разбор строк-иконок из UpgradeIconSet и UpgradeCategoryData: «item:N», «tile:N», «ui:имя».</summary>
public static class UpgradeIcons
{
    static readonly Dictionary<string, Texture2D> cache = new();

    public static Texture2D Get(string spec)
    {
        if (string.IsNullOrEmpty(spec)) return null;
        if (cache.TryGetValue(spec, out var t)) return t;
        int c = spec.IndexOf(':'); if (c < 0) return null;
        string kind = spec.Substring(0, c), arg = spec.Substring(c + 1);
        t = kind switch
        {
            "item" => int.TryParse(arg, out var i) ? UiIcons.Item(i) : null,
            "tile" => int.TryParse(arg, out var j) ? UiIcons.Tile(j) : null,
            "ui" => ResourceLoader.Exists($"res://Assets/UI/{arg}.png") ? GD.Load<Texture2D>($"res://Assets/UI/{arg}.png") : null,
            _ => null
        };
        if (t != null) cache[spec] = t; return t;
    }

    /// <summary>Иконка навыка 32×32 из SkillDef (Assets/UI/Skills); без неё — запасная из набора UpgradeIconSet.</summary>
    public static Texture2D ForTech(int tech)
    {
        if (tech < 0 || tech >= Game.Techs.Length) return null; string path = Game.SkillOf(tech).Icon;
        if (!string.IsNullOrEmpty(path)) { if (cache.TryGetValue(path, out var c)) return c; if (ResourceLoader.Exists(path)) { var t = GD.Load<Texture2D>(path); if (t != null) { cache[path] = t; return t; } } }
        var s = UpgradeIconSet.Get().Tech; return tech < s.Length ? Get(s[tech]) : null;
    }
}
