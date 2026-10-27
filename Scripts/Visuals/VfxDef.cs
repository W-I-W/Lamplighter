using Godot;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Визуальное событие: частицы, кольцо, звук, тряска и вспышка. Запускается по имени: Game.Fx("tile_break", pos, color).</summary>
[GlobalClass]
public partial class VfxDef : Resource
{
    [Export] public string Id = "";
    [Export] public int Particles = 0;
    [Export] public float Speed = 60f;
    [Export] public float ParticleSize = 1.5f;
    [Export] public Color Tint = new(1, 1, 1, 1);     // цвет по умолчанию (можно переопределить при вызове)
    [Export] public float RingRadius = 0f;
    [Export] public float RingTime = .6f;
    [Export] public Color RingColor = new(1f, .89f, .48f);
    [Export] public string Sfx = "";
    [Export] public float SfxDb = 0f;
    [Export] public float SfxPitch = 1f;
    [Export] public float Shake = 0f;
    [Export] public float Flash = 0f;
}
