using Godot;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Вид места интереса, которое добавляет «исследовательская» генерация (World.Explore). Новое место этого вида — новый .tres в Resources/Pois, код генерации не меняется.
/// Графика тайловая (Assets/Sprites): место собирается из существующих тайлов, отдельные сцены не нужны.</summary>
[GlobalClass]
public partial class PoiDef : Resource
{
    [Export] public string Id = "";
    [Export] public string Title = "";                 // русский исходный текст; перевод — lang/*.json
    [Export] public string Hint = "";
    [Export] public int Kind;                          // PK
    [Export] public int Variant;                       // Poi.Variant: отличает вариант вида (8 — тёмный лагерь, 9 — сокровищница)
    [Export] public int MinGen = 1;                    // с какой версии генерации (World.GenV) место добавляется: старые миры не меняются
    [Export] public int Region;                       // биом (World.RegionOf), в котором размещается
    [Export] public int Count = 1;                     // сколько мест на мир (в одном биоме)
    [Export] public int MinFromSpawn = 14;             // расстояние от маяка, в клетках
    [Export] public int MaxFromSpawn = 56;
    [Export] public int Clear = 3;                     // радиус очищаемой площадки
    [Export] public int Spacing = 14;                  // минимальное расстояние до других мест
    [Export] public int Guards;                        // сколько стражей
    [Export] public int Tier = 1;                      // ценность награды 0..3 (для дальнего леса растёт с расстоянием)
    [Export] public string Reward = "";                // награда по тегам (как у находок): "relic:id;frag:id;item:N:K"; пусто — бросок по таблице тира
    [Export] public bool Unique;                       // награда выдаётся один раз и не возобновляется
    [Export] public float SilhouetteR;                 // видимость силуэта сквозь тьму, в пикселях (0 — не видно)
    [Export] public Color MapColor = new Color("#ffd86b");
}

/// <summary>Каталог видов мест интереса. Загружается из Resources/Pois один раз.</summary>
public static class PoiDefs
{
    public const string Dir = "res://Resources/Pois";
    static List<PoiDef> all;

    public static IReadOnlyList<PoiDef> All
    {
        get
        {
            if (all != null) return all;
            all = new List<PoiDef>();
            using var d = DirAccess.Open(Dir); if (d == null) return all;
            var names = new List<string>(d.GetFiles()); names.Sort(System.StringComparer.Ordinal);
            foreach (var n in names)
            {
                var f = n.EndsWith(".remap") ? n.Substring(0, n.Length - 6) : n; if (!f.EndsWith(".tres")) continue;
                var def = ResourceLoader.Load<PoiDef>($"{Dir}/{f}"); if (def != null && def.Id != "") all.Add(def);
            }
            return all;
        }
    }

    public static PoiDef Of(PK k, int variant = -1) { foreach (var d in All) if (d.Kind == (int)k) return d; return null; }
}
