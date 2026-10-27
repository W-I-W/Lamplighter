using Godot;
using System;

namespace Lamplighter;

/// <summary>Единая точка запуска визуальных эффектов по игровым событиям. Параметры (частицы, кольцо, звук, тряска) — в Resources/VisualConfigs/Vfx/*.tres.</summary>
public partial class Game
{
    public void Fx(string id, Vector2 pos, Color? tint = null, float pitchMul = 1f)
    {
        var d = VisualCatalog.Get()?.Effect(id); if (d == null) return;
        var col = tint ?? d.Tint;
        if (d.Particles > 0) Burst(pos, col, d.Particles, d.Speed);
        if (d.RingRadius > 0) AddRing(pos, d.RingRadius, d.RingTime, tint ?? d.RingColor);
        if (d.Sfx != "") Sfx.Play(d.Sfx, d.SfxDb, d.SfxPitch * pitchMul);
        if (d.Shake > 0) Shake = Mathf.Max(Shake, d.Shake);
        if (d.Flash > 0) Flash = Mathf.Max(Flash, d.Flash);
    }
}
