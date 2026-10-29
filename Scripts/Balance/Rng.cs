using System;

namespace Lamplighter.Balance;

/// <summary>Детерминированный генератор (SplitMix64 + xoshiro-подобное смешивание): без Godot, без глобального состояния, один экземпляр на прогон.</summary>
public sealed class Rng
{
    ulong s;
    public Rng(ulong seed) { s = seed == 0 ? 0x9E3779B97F4A7C15UL : seed; Next(); Next(); }

    /// <summary>Сид прогона из общего сида, номера профиля и номера прогона — не зависит от порядка и числа потоков.</summary>
    public static ulong Mix(ulong a, ulong b, ulong c = 0, ulong d = 0)
    {
        ulong x = a * 0x9E3779B97F4A7C15UL ^ (b + 0x632BE59BD9B4E019UL) * 0xBF58476D1CE4E5B9UL ^ (c + 0x1234567UL) * 0x94D049BB133111EBUL ^ (d + 0x7F4A7C15UL) * 0xD6E8FEB86659FD93UL;
        x ^= x >> 30; x *= 0xBF58476D1CE4E5B9UL; x ^= x >> 27; x *= 0x94D049BB133111EBUL; x ^= x >> 31; return x;
    }

    public ulong Next()
    {
        ulong z = (s += 0x9E3779B97F4A7C15UL);
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL; z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL; return z ^ (z >> 31);
    }

    public float F() => (Next() >> 40) * (1f / (1UL << 24));                 // [0,1)
    public float Range(float a, float b) => a + (b - a) * F();
    public int Int(int a, int b) => a + (int)(F() * (b - a + 1)) % (b - a + 1);   // включительно
    public bool Chance(float p) => F() < p;
    public float Normal(float mean, float sd)
    {
        float u1 = Math.Max(1e-6f, F()), u2 = F();
        return mean + sd * MathF.Sqrt(-2f * MathF.Log(u1)) * MathF.Cos(6.2831853f * u2);
    }
}
