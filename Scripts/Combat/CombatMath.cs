using System;

namespace Lamplighter;

/// <summary>Чистые формулы, общие для игры и симулятора.</summary>
public static class CombatMath
{
    public const int HealthModel = 1;
    public static float BaseHealth(int level)
    {
        int n = Math.Max(0, level - 1);
        return 60f + 4f * Math.Min(n, 20) + 2f * Math.Clamp(n - 20, 0, 20);
    }
    public static float MigrateBaseHealth(float oldMax, int level) => Math.Max(60f, oldMax - (60f + 10f * Math.Max(0, level - 1)) + BaseHealth(level));
    public const float RangedRecoveryTime = .28f, RangedMoveMul = .55f, RangedKnockbackMul = .35f;
    // Level supplements equipment; it cannot turn a starter weapon into an endgame weapon.
    public static float LevelDamageBonus(int level)
    {
        int n = Math.Max(0, level - 1);
        return .25f * Math.Min(n, 20) + .1f * Math.Clamp(n - 20, 0, 20);
    }
    public const float OrbSplashMul = .35f;
    public static float RangeFalloff(float flightFraction) => 1f - .6f * Math.Clamp((flightFraction - .3f) / .7f, 0f, 1f);
}
