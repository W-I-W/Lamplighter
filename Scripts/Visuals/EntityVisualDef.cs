using Godot;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Внешний вид существа: лист кадров (PNG) и параметры отображения. Меняется в редакторе, без правки кода.</summary>
[GlobalClass]
public partial class EntityVisualDef : Resource
{
    [Export] public string Id = "";
    [Export] public Texture2D Sheet;                  // горизонтальная лента кадров FrameSize×FrameSize
    [Export] public int Frames = 2;
    [Export] public int FrameSize = 16;
    [Export] public float Fps = 4f;                   // скорость смены кадров idle-анимации
    [Export] public float Scale = 1f;
    [Export] public Vector2 Offset = new(-8, -10);    // положение верхнего левого угла кадра относительно точки объекта
    [Export] public float Bob = 1.2f;                 // амплитуда покачивания
    [Export] public float Lift = 0f;                  // подъём над землёй (парящие существа)
    [Export] public float ShadowRadius = 5f;
}
