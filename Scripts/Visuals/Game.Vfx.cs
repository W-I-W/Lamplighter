using Godot;
using System;

namespace Lamplighter;

/// <summary>Единая точка запуска визуальных эффектов по игровым событиям. Параметры (частицы, кольцо, звук, тряска) — в Resources/VisualConfigs/Vfx/*.tres.</summary>
public partial class Game
{
    public void Fx(string id, Vector2 pos, Color? tint = null, float pitchMul = 1f, string sfx = null)
    {
        var d = VisualCatalog.Get()?.Effect(id); if (d == null) return;
        var col = tint ?? d.Tint;
        if (d.Particles > 0) Burst(pos, col, d.Particles, d.Speed);
        if (d.RingRadius > 0) AddRing(pos, d.RingRadius, d.RingTime, tint ?? d.RingColor);
        if (sfx != null) Sfx.Play(sfx, d.SfxDb, pitchMul);
        else if (d.Sfx != "") Sfx.Play(d.Sfx, d.SfxDb, d.SfxPitch * pitchMul);
        if (d.Shake > 0) Shake = Mathf.Max(Shake, d.Shake);
        if (d.Flash > 0) Flash = Mathf.Max(Flash, d.Flash);
        if (d.Layers == "") return;
        foreach (var layer in d.Layers.Split(','))
            switch (layer.Trim())
            {
                case "sparks": Sparks(pos, Vector2.Up, col, 5, d.Speed * 1.8f); break;
                case "debris": Debris(pos, col, 7, d.Speed); break;
                case "puff": Puff(pos + new Vector2(0, 3), Tone(col, .35f), 5); break;
                case "sparkle": Sparkle(pos, col, 9, Mathf.Max(9f, d.RingRadius * .4f)); break;
                case "rays": Rays(pos + new Vector2(0, 6), col, 8, 7f, 28f); break;
                case "flash": FlashAt(pos, Tone(col, .6f), 9f); break;
                case "ring2": if (d.RingRadius > 0) AddRing(pos, d.RingRadius * .55f, d.RingTime * .7f, Colors.White); break;
            }
    }
}
