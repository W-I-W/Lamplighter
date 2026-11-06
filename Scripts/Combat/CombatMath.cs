using System;

namespace Lamplighter;

/// <summary>Чистые формулы, общие для игры и симулятора.</summary>
public static class CombatMath
{
    public static float RangeFalloff(float flightFraction) => 1f - .6f * Math.Clamp((flightFraction - .3f) / .7f, 0f, 1f);
}
