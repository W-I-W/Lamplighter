using Godot;
using System;
using System.Linq;

namespace Lamplighter;

/// <summary>Все числа боевой сложности (этап 10) в одном Resource: Resources/Combat/tuning.tres. Правятся в редакторе без перекомпиляции.
/// Индексы массивов биомов: 0 лес, 1 шахты, 2 пустоши, 3 монастырь, 4 Сердце Тьмы; боссов — номер босса − 1; кооператива — число игроков − 1.</summary>
[GlobalClass]
public partial class CombatTuning : Resource
{
    // сложность мира «Сложная» (множители поверх обычных; «Обычная» = 1)
    [Export] public float HardEnemyHp = 1.35f, HardEnemyDamage = 1.3f, HardBossHp = 1.3f, HardBossDamage = 1.2f;

    // обычные враги
    [Export] public float EnemyDamage = 1.6f;                 // урон ближних и дальних атак
    [Export] public float EnemyHp = 1.5f;
    [Export] public float EnemySpeed = 1.15f;
    [Export] public float EnemyAttackRate = 1.15f;             // как быстро восстанавливаются паузы между атаками
    [Export] public float EnemyAtkPause = 1.7f;                // во сколько раз длиннее паузы между атаками обычных врагов (боссов не касается): бьют реже
    [Export] public float EnemyMinHit = 1f;                    // нижний порог до сопротивлений; защита может уменьшить и это значение
    [Export] public float BareHandMul = .4f;                   // голые руки (инструмента нет): доля урона инструмента, заметно слабее меча
    [Export] public float FistDamageMul = .65f;                // инструменты уступают оружию своего этапа по урону
    [Export] public float SpawnDelayMul = 3.2f, SpawnCapMul = .4f;     // реже появляются (×3,2 к паузе) и меньше одновременно (×0,4 к лимиту): удары теперь тяжёлые, а врагов мало
    // бюджет опасности столкновений (в «стоимости» видов, см. EnemyBalance): по регионам — основа, прибавка за зажжённый фонарь региона и потолок
    [Export] public float[] EncounterBase = { 4f, 7f, 9f, 11f, 13f };
    [Export] public float[] EncounterMax = { 9f, 13f, 16f, 19f, 22f };
    [Export] public float EncounterPerLamp = 1f;
    [Export] public int MaxRangedNear = 2;                     // сколько дальнобойных врагов одновременно рядом с игроком
    [Export] public int SpawnCapBonus = 2;                     // дополнительные враги на территории
    [Export] public int MaxAttackers = 3;                      // одновременно готовящихся ближних атак
    [Export] public float ShadeWindup = .38f;                  // подготовка рывка тени
    // элитные
    [Export] public float[] EliteChance = { .05f, .07f, .1f, .13f, .15f };
    [Export] public int EliteMinLit = 1;                       // в лесу элитные появляются после первого зажжённого фонаря и вдали от маяка
    [Export] public int EliteMax = 2;                          // одновременно на территории
    [Export] public float EliteHp = 1.8f, EliteDamage = 1.25f;
    // боссы
    [Export] public float[] BossHp = { 1.7f, 1.6f, 1.55f, 1.5f, 1.45f };
    [Export] public float[] BossDamage = { 1.25f, 1.3f, 1.35f, 1.4f, 1.5f };
    [Export] public float BossLeashTiles = 36f;                // пока игрок ближе этого расстояния (в клетках) от места босса, бой идёт; бросив бой дальше, игрок теряет босса
    [Export] public float BossRegenDelay = 8f, BossRegenRate = .03f;   // босс начинает восстанавливаться только после стольких секунд без игрока и медленно (доля здоровья в секунду)
    [Export] public float BossWarnMul = .72f;                  // множитель времени подготовки атак боссов (кроме Рощи, у которой он свой): меньше — меньше времени увернуться
    [Export] public float BossTempo = .7f;                     // пауза между атаками босса (меньше — чаще): бои с боссами напряжённее, увернуться «на автомате» нельзя
    [Export] public float WinterVuln = 9f, SourceVuln = 10f;     // достаточно времени добежать и ударить после решения щита
    // игрок
    [Export] public float HitInvuln = .5f;
    [Export] public float RegenRate = .35f, RegenDelay = 5f;
    [Export] public float DashCooldown = 2.2f, DashInvuln = .2f;
    [Export] public float ReaperHeal = 3f;
    [Export] public int FearLevel = 5;                         // ниже этого уровня тень смертельна: урон по герою растёт на FearDamage за каждый недостающий уровень, врагов больше на FearSpawn
    [Export] public float FearDamage = 0f, FearSpawn = 2f;
    [Export] public float GatherMul = .65f, MineTimeMul = 1.35f, RegrowMul = .6f;         // добыча: множитель выпадения сырья, время добычи, скорость восстановления деревьев и руды
    [Export] public float ScarcityMul = 1f;                    // множитель минимального числа ресурсов рядом с игроком (появление при нехватке)
    [Export] public float TechCostMul = 2f;                    // множитель цены навыков [T]
    [Export] public float BareMineMul = .7f;                   // то же для голых рук: дерево ~2.6 с, камень ~1.2 с (кирка: камень ~0.9 с, топор быстрее по дереву)
    [Export] public float WeaponMineMul = .15f;                // скорость добычи блоков с оружием в руках относительно кирки
    [Export] public float XpMul = .6f, XpBase = 40f, XpLinear = 30f, XpQuad = 3f;        // опыт: множитель добычи и стоимость уровня (база + линейная + квадратичная часть)                   // лечение «Жнеца» за убийство (было 3)
    // кооператив: по числу игроков (1–4); не линейно
    [Export] public float[] CoopBossHp = { 1f, 1.6f, 2.1f, 2.6f };
    [Export] public float[] CoopCap = { 1f, 1.3f, 1.55f, 1.8f };
    [Export] public float[] CoopElite = { 1f, 1.3f, 1.6f, 2f };
    [Export] public float[] CoopBossTempo = { 1f, .95f, .88f, .8f };
    [Export] public int[] CoopMaxAttackers = { 3, 4, 5, 6 };

    public static float At(float[] a, int i) => a == null || a.Length == 0 ? 1f : a[Math.Clamp(i, 0, a.Length - 1)];
}

public static class Tune
{
    static CombatTuning t;
    public static CombatTuning T => t ??= ResourceLoader.Load<CombatTuning>("res://Resources/Combat/tuning.tres") ?? new CombatTuning();
    public static void Reload() { t = null; }
}
