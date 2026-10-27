using Godot;

namespace Lamplighter;

/// <summary>Настройки «живого мира»: ветер, качание растений, реакция на попадания и т.д. Ресурс Resources/VisualConfigs/VisualFx.tres, правится в Inspector.
/// Игровые настройки пользователя (Settings.Wind, Settings.Particles, Settings.Flash, Settings.Fx, Settings.Shake) умножаются на эти значения.</summary>
[GlobalClass]
public partial class VisualFxConfig : Resource
{
    public const string Path = "res://Resources/VisualConfigs/VisualFx.tres";
    // ветер и растительность
    [Export] public int[] SwayTiles = { 5, 111, 79, 130, 131, 132, 133 };          // колонки атласа тайлов, которые качаются (деревья 5/111/79, трава и кусты 130–133)
    [Export] public float[] SwayAmp = { 2.0f, 1.6f, 1.0f, 2.6f, 1.5f, 2.8f, 3.6f };   // амплитуда у верхушки, пикселей
    [Export] public int[] GlowTiles = { 9, 22, 50, 51, 100, 110 };               // редкие ресурсы: Люмен, Эфир, Кристалл, Серебро, Метеорит, Морозная руда
    [Export] public float[] GlowAmount = { .35f, .35f, .3f, .15f, .4f, .3f };
    [Export] public bool UsePoses = true;                    // качание кадрами из PNG (Environment/134..161_*_p*.png); false — плавный сдвиг шейдером
    [Export] public int[] PoseBase = { 134, 138, 142, 146, 150, 154, 158 };   // первая колонка поз для каждого тайла из SwayTiles (порядок: сильно влево, влево, вправо, сильно вправо)
    [Export] public float Wind = 1f;
    [Export] public float HitShakePx = 2.4f;               // встряска дерева при ударе
    [Export] public float HitShakeTime = .5f;
    [Export] public float FoliageDensity = .17f;            // доля свободных клеток травы с кустиками и цветами
    [Export] public float FoliageActiveRadius = 640f;       // дальше от камеры растительность не обновляется (экономия)

    // попадания: профили для обычных врагов [0], сильных противников [1] и боссов [2]
    [Export] public float[] HitFlashTime = { .09f, .11f, .13f };         // белая вспышка спрайта, секунд
    [Export] public float[] HitShakePxEnemy = { 1.8f, 1.1f, .5f };       // встряска врага, пикселей
    [Export] public int[] HitParticles = { 6, 9, 12 };                   // пиксельные искры
    [Export] public float[] HitCamShake = { 1.0f, .8f, .4f };            // вклад в тряску камеры (умножается на Settings.Shake)
    [Export] public float CritMul = 1.6f;                                // усиление эффекта при критическом попадании (удар ≥ 30% здоровья)
    [Export] public float DeathTime = .5f;
    [Export] public int MaxParticles = 700;

    static VisualFxConfig inst;
    public static VisualFxConfig Get() => inst ??= (ResourceLoader.Exists(Path) ? ResourceLoader.Load<VisualFxConfig>(Path) : null) ?? new VisualFxConfig();
}
