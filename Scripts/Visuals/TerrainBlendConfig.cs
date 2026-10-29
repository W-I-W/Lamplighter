using Godot;

namespace Lamplighter;

/// <summary>Настройки визуального смешивания поверхностей (переходы трава/земля/снег/пепел…): Resources/VisualConfigs/TerrainBlend.tres, правится в Inspector.
/// Только внешний вид: коллизии, свойства поверхности, ресурсы и логика не затрагиваются. Результат зависит только от Seed мира и координат клетки.</summary>
[GlobalClass]
public partial class TerrainBlendConfig : Resource
{
    public const string Path = "res://Resources/VisualConfigs/TerrainBlend.tres";
    [Export] public bool Enabled = true;
    /// <summary>Ширина смешивания: уровни 0..4 = 2.5, 3.5, 4.5, 5.5, 6.5 пикселей заступа более «сильного» материала на соседнюю клетку (дробные значения округляются).</summary>
    [Export(PropertyHint.Range, "0,4,0.1")] public float BlendWidth = 2f;
    /// <summary>Размах изменения ширины по клеткам (в уровнях): низкочастотный шум делает полосу перехода то уже, то шире.</summary>
    [Export(PropertyHint.Range, "0,2,0.05")] public float WidthJitter = 1f;
    /// <summary>Интенсивность dithering: 0 — резкий край, 0.5 — мягкая шашечная зона 3 px, 1 — широкая зона 6 px с вкраплениями.</summary>
    [Export(PropertyHint.Range, "0,1,0.05")] public float DitherIntensity = .5f;
    /// <summary>Неровность границ: доля клеток с «рваным» контуром (0 — спокойные края, 1 — сильно изрезанные).</summary>
    [Export(PropertyHint.Range, "0,1,0.05")] public float Roughness = .55f;
    /// <summary>Плотность пятен-вкраплений соседнего материала вблизи границы (клетки через одну от края).</summary>
    [Export(PropertyHint.Range, "0,1,0.05")] public float ScatterDensity = .6f;
    /// <summary>Плотность декоративного слоя (травинки, камешки, угольки): доля свободных клеток; у границ материалов в 2.5 раза больше.</summary>
    [Export(PropertyHint.Range, "0,0.5,0.01")] public float DecorDensity = .12f;
    /// <summary>Добавка к Seed мира: меняет раскладку шума, не меняя мир.</summary>
    [Export] public int SeedSalt = 0;

    static TerrainBlendConfig inst;
    public static TerrainBlendConfig Get() => inst ??= (ResourceLoader.Exists(Path) ? ResourceLoader.Load<TerrainBlendConfig>(Path) : null) ?? new TerrainBlendConfig();
    public static void Override(TerrainBlendConfig c) => inst = c;
}
