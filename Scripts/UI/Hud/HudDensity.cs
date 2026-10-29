using Godot;

namespace Lamplighter;

public enum HudMode { Normal, Minimal, Full }

/// <summary>Режим плотности HUD: обычный (основное), минимальный (здоровье, хотбар, одна цель), полный (всё для тестирования: все метки, числа и служебная строка). Переключается клавишей H и хранится в настройках.</summary>
public static class HudDensity
{
    public static HudMode Mode { get => (HudMode)Mathf.Clamp(Settings.HudMode, 0, 2); set { Settings.HudMode = (int)value; } }
    public static bool Minimal => Mode == HudMode.Minimal;
    public static bool Full => Mode == HudMode.Full;
    public static readonly string[] Names = { "Интерфейс: обычный", "Интерфейс: минимальный", "Интерфейс: полный" };

    public static HudMode Next() { Mode = (HudMode)(((int)Mode + 1) % 3); Settings.Save(); return Mode; }
}
