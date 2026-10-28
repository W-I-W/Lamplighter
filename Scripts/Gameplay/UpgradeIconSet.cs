using Godot;

namespace Lamplighter;

/// <summary>Иконки улучшений как данные: Resources/Upgrades/UpgradeIcons.tres хранит строку на каждое улучшение (в порядке enum Tech).
/// Строка — «item:N» (иконка предмета), «tile:N» (тайл атласа) или «ui:имя» (Assets/UI/имя.png).</summary>
[GlobalClass]
public partial class UpgradeIconSet : Resource
{
    public const string Path = "res://Resources/Upgrades/UpgradeIcons.tres";
    [Export] public string[] Tech = System.Array.Empty<string>();
    static UpgradeIconSet inst;
    public static UpgradeIconSet Get() => inst ??= ResourceLoader.Exists(Path) ? GD.Load<UpgradeIconSet>(Path) : new UpgradeIconSet();
}
