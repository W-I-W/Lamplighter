using Godot;

namespace Lamplighter;

/// <summary>Подсветка кнопок под неподвижным курсором. Godot обновляет «мышь над элементом» только по событию движения: если окно открылось или перестроилось под стоящим курсором, кнопка не подсвечивается,
/// пока мышь не шевельнётся. После таких изменений интерфейс просит PokeHover: через пару кадров, когда раскладка готова, игра отправляет событие движения в том же месте курсора.</summary>
public partial class Game
{
    float hoverPoke;

    /// <summary>Вызывать после открытия или перестроения интерфейса под курсором.</summary>
    public void PokeHover() { hoverPoke = .06f; }

    void UpdateHoverPoke(float dt)
    {
        if (hoverPoke <= 0) return; hoverPoke -= dt; if (hoverPoke > 0) return;
        var vp = GetViewport(); if (vp == null || testing || !DisplayServer.WindowIsFocused()) return;
        var p = vp.GetMousePosition(); if (!vp.GetVisibleRect().HasPoint(p)) return;
        vp.WarpMouse(p);
    }
}
