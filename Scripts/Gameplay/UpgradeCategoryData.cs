using Godot;

namespace Lamplighter;

/// <summary>Как открывается категория улучшений: сразу или после постоянного открытия биома.</summary>
public enum UpgradeUnlock { Always, BiomeDiscovered }

/// <summary>Категория окна улучшений (данные). Новая категория — это новый .tres в Resources/Upgrades/Categories; код интерфейса не меняется.
/// Page — номер страницы TechDef.Page, улучшения которой показывает категория; BiomeId — регион мира (World.RegionOf) для условия открытия.</summary>
[GlobalClass]
public partial class UpgradeCategoryData : Resource
{
    [Export] public string Id = "";
    [Export] public string Title = "";                 // русский исходный текст; перевод — lang/*.json
    [Export] public string Icon = "";                  // «item:19», «tile:13», «ui:ability_dash» — см. UpgradeIcons
    [Export] public int Order;
    [Export] public int Page;
    [Export] public int BiomeId = -1;
    [Export] public UpgradeUnlock Condition = UpgradeUnlock.Always;
}
