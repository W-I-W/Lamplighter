using Godot;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Данные инструмента (Resources/Items/tool_*.tres), правятся в Inspector. Инструмент и добывает, и бьёт врагов — но слабее оружия сопоставимого уровня.
/// CombatDamage умножает базовый урон игрока (PlayerDamage), MiningPower — скорость добычи, Eff* — эффективность по группам ресурсов (0 — не подходит).</summary>
[GlobalClass]
public partial class ToolDef : Resource
{
    [Export] public string Id = "", Name = "", Desc = "";
    [Export] public int Icon = 23;                       // индекс значка в атласе предметов
    [Export] public float MiningPower = 1f;              // общая скорость добычи
    [Export] public float CombatDamage = 1f;             // урон врагам относительно PlayerDamage (не связан с добычей)
    [Export] public float AttackInterval = 1f;           // множитель паузы между ударами (больше — медленнее)
    [Export] public float AttackRange = 0f;              // добавка к дальности удара, пикселей
    [Export] public float EffWood = 1f, EffStone = 1f, EffOre = 1f, EffRubble = 1f;   // ResourceTypeEfficiency по группам: дерево, камень, руда, завалы
    [Export] public bool Craftable;                      // создаётся в ручном крафте (кирка есть всегда)

    public static readonly string[] Ids = { "pickaxe", "axe", "shovel", "hammer" };
    static Dictionary<string, ToolDef> cache;
    public static ToolDef Get(string id)
    {
        if (cache == null)
        {
            cache = new();
            foreach (var i in Ids) { var r = ResourceLoader.Load<ToolDef>($"res://Resources/Items/tool_{i}.tres"); if (r != null) cache[i] = r; }
        }
        return cache.TryGetValue(id, out var d) ? d : (cache.TryGetValue("pickaxe", out var p) ? p : new ToolDef { Id = "pickaxe", Name = "Кирка" });
    }

    /// <summary>Эффективность инструмента для типа тайла (0 — этим инструментом не добывается).</summary>
    public float Efficiency(Tl t) => t switch
    {
        Tl.Tree or Tl.DeadTree or Tl.Pine => EffWood,
        Tl.Rubble => EffRubble,
        Tl.Stone or Tl.Ruin or Tl.IceBlock or Tl.IceRuin => EffStone,
        _ => EffOre
    };
}
