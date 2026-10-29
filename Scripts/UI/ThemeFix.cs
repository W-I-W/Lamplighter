using Godot;

namespace Lamplighter;

/// <summary>Пиксельный шрифт сидит в строке высоко, поэтому подпись на кнопках казалась сдвинутой вверх. Один раз после загрузки темы опускаем содержимое всех стилей кнопок на два пикселя интерфейса:
/// верхний отступ больше, нижний меньше. Меняется только копия темы в памяти, файл темы не трогается.</summary>
public static class ThemeFix
{
    static bool done;
    static readonly string[] States = { "normal", "hover", "pressed", "disabled", "focus", "hover_pressed" };

    public static void Apply(Theme t, float shift = 2f)
    {
        if (done || t == null) return; done = true;
        foreach (var type in t.GetStyleboxTypeList())
        {
            if (!t.HasStylebox("pressed", type) || !t.HasStylebox("normal", type)) continue;          // только кнопки и вкладки
            foreach (var st in States)
            {
                if (!t.HasStylebox(st, type)) continue;
                var sb = t.GetStylebox(st, type).Duplicate() as StyleBox; if (sb == null) continue;
                float top = sb.GetContentMargin(Side.Top), bottom = sb.GetContentMargin(Side.Bottom);
                float cut = Mathf.Min(bottom, shift); sb.ContentMarginBottom = bottom - cut; sb.ContentMarginTop = top + 2f * shift - cut;      // центр содержимого опускается на shift
                t.SetStylebox(st, type, sb);
            }
        }
    }
}
