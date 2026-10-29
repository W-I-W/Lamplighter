using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Профессия жителя (этап 9). Все числа — в .tres (Resources/Villagers), код ничего не знает о конкретных профессиях: новая профессия — новый файл.
/// Работник не производит ресурсы сам: он меняет скорость уже существующего здания (Game.UpdateVillage), поэтому двойного начисления нет.</summary>
[GlobalClass]
public partial class ProfDef : Resource
{
    [Export] public string Id = "";
    [Export] public string Title = "";                 // русский текст; перевод — lang/en.json
    [Export] public string Building = "";              // Tl рабочего здания ("Sawmill"); пусто — рабочего места пока нет
    [Export] public float Rate = 1.35f;                // множитель скорости здания, если у работника своя профессия (без работника здание идёт на 0,6, с чужим — на 1)
    [Export] public string Anim = "chop";              // chop, pick, hammer, water, inspect — вид рабочей анимации и звука
    [Export] public string Sprite = "";                // id EntityVisualDef (лист из 4 кадров: стоит, идёт, работа 1, работа 2)
    [Export] public int Order;                         // очерёдность: спасённые жители получают первую профессию, которой ещё нет в деревне
    [Export] public bool Enabled = true;               // выключенные профессии пока не раздаются (нет здания)
    [Export] public Color Accent = new Color("#ffd86b");
    [Export] public string[] Lines = new string[0];    // реплики о работе (перевод по строкам)

    public Tl BuildingTile => Building != "" && Enum.TryParse<Tl>(Building, out var t) ? t : Tl.None;
}

public static class ProfDefs
{
    public const string Dir = "res://Resources/Villagers";
    static List<ProfDef> all;

    public static IReadOnlyList<ProfDef> All
    {
        get
        {
            if (all != null) return all;
            all = new List<ProfDef>();
            using var d = DirAccess.Open(Dir); if (d == null) return all;
            var names = new List<string>(d.GetFiles()); names.Sort(StringComparer.Ordinal);
            foreach (var n in names)
            {
                var f = n.EndsWith(".remap") ? n.Substring(0, n.Length - 6) : n; if (!f.EndsWith(".tres")) continue;
                var def = ResourceLoader.Load<ProfDef>($"{Dir}/{f}"); if (def != null && def.Id != "") all.Add(def);
            }
            all.Sort((a, b) => a.Order.CompareTo(b.Order));
            return all;
        }
    }

    public static ProfDef Of(string id) { if (string.IsNullOrEmpty(id)) return null; foreach (var d in All) if (d.Id == id) return d; return null; }
    public static ProfDef ForBuilding(Tl t) { foreach (var d in All) if (d.Enabled && d.BuildingTile == t && t != Tl.None) return d; return null; }
}
