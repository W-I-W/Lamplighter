using Godot;

namespace Lamplighter;

/// <summary>Внешний вид выделения интерактивных объектов (выбранный и под курсором). Настраивается в Resources/VisualConfigs/HighlightStyle.tres.</summary>
[GlobalClass]
public partial class HighlightStyle : Resource
{
    [Export] public Color SelectedColor = new("#ffe27a");
    [Export] public Color HoverColor = new("#bfe9ff");
    [Export] public float FadeSpeed = 9f;
    [Export] public float FillAlpha = .12f;
    [Export] public float HoverAlpha = .45f;
    [Export] public int LabelSize = 9;
    [Export] public float IconBob = 1.2f;
}
