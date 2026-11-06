using System;

namespace Lamplighter;

/// <summary>Фиксированные боевые данные видов врагов (единственное место): здоровье, множитель урона относительно записанных в AI чисел атак, стоимость в бюджете столкновения и роль.
/// Ничто здесь не зависит от уровня героя, числа зажжённых фонарей, побеждённых боссов, региона или времени игры: один и тот же вид одинаков в любой момент.
/// Новые регионы опаснее за счёт других видов, а не множителей к старым.
/// Здоровье — «базовое» до общей настройки EnemyHp (tuning.tres); урон — множитель к числам атак из AI до общей настройки EnemyDamage.</summary>
public static class EnemyBalance
{
    public enum Role { Chaser, Fast, Ranged, Heavy, Support, Swarm, Bomb }

    public readonly record struct Def(float Hp, float Dmg, float Cost, Role Role);

    // Ориентир по урону — доля здоровья героя на этапе региона: слабый удар 5–12%, обычный 10–18%, сильный 20–30%.
    // Здоровье героя по CombatMath.BaseHealth: 60 на старте, 136 на 20-м уровне, предел 180 без реликвий и благословений.
    public static Def Of(EK k) => k switch
    {
        // тёмный лес
        EK.Shade => new(7f, 1f, 1f, Role.Chaser), EK.Wraith => new(3.5f, 1f, 2f, Role.Fast), EK.Spitter => new(4.5f, 1f, 2f, Role.Ranged),
        EK.Swarm => new(1.3f, 1f, .5f, Role.Swarm), EK.Bomber => new(5f, 1f, 2f, Role.Bomb),
        // затопленные шахты
        EK.Leech => new(5f, 1.6f, 1.5f, Role.Chaser), EK.Eel => new(7f, 1.8f, 2.5f, Role.Fast), EK.Beetle => new(15f, 1.7f, 3f, Role.Heavy),
        // пепельные пустоши
        EK.Imp => new(7f, 3f, 2f, Role.Ranged), EK.Spirit => new(8.5f, 3f, 2.5f, Role.Ranged), EK.Golem => new(31f, 2.6f, 3.5f, Role.Heavy),
        // ледяной монастырь
        EK.IceWolf => new(14f, 4f, 2.5f, Role.Fast), EK.FrostPriest => new(13f, 4.5f, 3f, Role.Support), EK.Sentinel => new(32f, 3.4f, 4f, Role.Heavy),
        // Сердце
        EK.Mirage => new(11f, 2f, 3f, Role.Ranged), EK.Weaver => new(16f, 5.5f, 3f, Role.Support), EK.Stalker => new(19f, 5f, 3f, Role.Fast),
        EK.Decoy => new(1f, 1f, 0f, Role.Chaser),
        // боссы: здоровье задаёт BossHp в tuning.tres
        EK.Warden => new(170f, 1f, 0f, Role.Heavy), EK.DepthWarden => new(260f, 1f, 0f, Role.Heavy), EK.AshWarden => new(300f, 1f, 0f, Role.Heavy),
        EK.WinterWarden => new(420f, 1f, 0f, Role.Heavy), EK.Source => new(560f, 1f, 0f, Role.Heavy),
        _ => new(300f, 1f, 0f, Role.Heavy)
    };

    public static float Hp(EK k) => Of(k).Hp;
    public static float Dmg(EK k) => Of(k).Dmg;
    public static float Cost(EK k) => Of(k).Cost;
    public static bool IsRanged(EK k) => Of(k).Role == Role.Ranged || Of(k).Role == Role.Support;
    public const float EliteCost = 3f;                                                    // элитный вариант добавляет к стоимости вида
}
