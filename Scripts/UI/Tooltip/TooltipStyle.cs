using Godot;

namespace Lamplighter;

/// <summary>Смысловая роль фрагмента подсказки: цвет подбирается по роли, а не задаётся в игровом коде.</summary>
public enum TipRole { Text, Key, Resource, Zone, Ok, Bad, Dim }

/// <summary>Внешний вид и анимация подсказок (tooltip). Настраивается в Resources/UI/TooltipStyle.tres.
/// Игровые классы описывают только смысл (действие, цена, награда); цвета, рамки, отступы и тайминги живут здесь.</summary>
[GlobalClass]
public partial class TooltipStyle : Resource
{
    public const string Path = "res://Resources/UI/TooltipStyle.tres";

    // палитра
    [Export] public Color TextColor = new("#eef2fa");
    [Export] public Color KeyColor = new("#ffd23f");
    [Export] public Color ResourceColor = new("#ffd23f");
    [Export] public Color ZoneColor = new("#8ff0ff");
    [Export] public Color OkColor = new("#9be07a");
    [Export] public Color BadColor = new("#f06a5e");
    [Export] public Color DimColor = new("#8a96b0");

    // рамки панели из темы (Resources/UI/LanternTheme.tres)
    [Export] public string PanelAvailable = "PanelGold";
    [Export] public string PanelLocked = "PanelDark";
    [Export] public string KeycapStyle = "Keycap";

    // раскладка, логические пиксели
    [Export] public int LineHeight = 11;
    [Export] public int PadX = 7;
    [Export] public int MaxWidth = 230;
    [Export] public int MinWidth = 84;
    [Export] public int Gap = 6;
    [Export] public int TopMargin = 58;                 // выше — зона баннеров и уведомлений

    // анимация
    [Export] public float FadeIn = .12f;
    [Export] public float FadeOut = .18f;
    [Export] public int SlidePx = 4;
    [Export] public float KeyPulseSpeed = 6f;
    [Export] public float AlertPulseSpeed = 5f;
    [Export] public float FlashTime = .3f;

    public Color Color(TipRole r) => r switch
    {
        TipRole.Key => KeyColor, TipRole.Resource => ResourceColor, TipRole.Zone => ZoneColor,
        TipRole.Ok => OkColor, TipRole.Bad => BadColor, TipRole.Dim => DimColor, _ => TextColor
    };
}
