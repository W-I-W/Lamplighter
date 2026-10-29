using Godot;

namespace Lamplighter;

/// <summary>Индикатор уровней навыка: ряд квадратиков, заполненные — золотые. Рисуется вручную без сглаживания.</summary>
public partial class SkillPipsUI : Control
{
    public int Max = 1, Level; public Color Fill = new("#ffd23f"), Empty = new("#2b3552");
    public override void _Draw()
    {
        int n = Max; float w = n == 1 ? 6 : 5, gap = 1; float total = n * w + (n - 1) * gap; float x0 = Mathf.Round((Size.X - total) / 2f), y = Mathf.Round((Size.Y - 4) / 2f);
        for (int i = 0; i < n; i++) DrawRect(new Rect2(x0 + i * (w + gap), y, w, 4), i < Level ? Fill : Empty);
    }
    public void Set(int max, int lv, Color fill) { if (max == Max && lv == Level && fill == Fill) return; Max = max; Level = lv; Fill = fill; QueueRedraw(); }
}
