using Godot;

namespace Lamplighter;

/// <summary>Данные навыка дерева развития (Resources/Skills/*.tres, генерируются tools/gen_skills.py). Имя, описание и условие открытия — в TechDef (Game.Tech.cs); здесь уровни, стоимость, положение и формула эффекта.
/// Текущий уровень в ресурсе не хранится: он лежит в прогрессе игрока (Game.SkillLv) и сохраняется в SaveData.
/// Parents — «id:уровень» (число или max): навык доступен, когда уровень родителя не ниже указанного.
/// Эффект на уровне L: EffBase + EffPer × L (на уровне 0 — ноль, кроме единицы abs). Единицы: % (+X%), cut (−X%), hp, s, dps, tiles, abs (абсолютный %).</summary>
[GlobalClass]
public partial class SkillDef : Resource
{
    [Export] public string Id = "";
    [Export] public int Page;
    [Export] public int MaxLevel = 1;
    [Export] public Vector2 Pos;
    [Export] public string Icon = "";
    [Export] public float[] CostCurve = { 1f };       // множитель базовой стоимости на каждый уровень (по длине MaxLevel)
    [Export] public string[] Parents = System.Array.Empty<string>();
    [Export] public string EffLabel = "";
    [Export] public string Unit = "";
    [Export] public float EffBase, EffPer;

    /// <summary>Эффект на уровне <paramref name="lv"/> в единицах навыка (доля для %, cut; прочее — как есть).</summary>
    public float Eff(int lv) => lv <= 0 && Unit != "abs" ? 0f : EffBase + EffPer * lv;

    /// <summary>Строка эффекта для окна: «+6%», «−12%», «+1,5 здор.», «3 с».</summary>
    public string EffText(int lv)
    {
        if (string.IsNullOrEmpty(Unit)) return "";
        float v = Eff(lv); string c = System.Globalization.CultureInfo.InvariantCulture.Name;
        string F(float x, string f) => x.ToString(f, System.Globalization.CultureInfo.InvariantCulture);
        return Unit switch
        {
            "%" => (v >= 0 ? "+" : "") + F(v * 100f, "0.#") + "%",
            "cut" => "-" + F(Mathf.Abs(v) * 100f, "0.#") + "%",
            "abs" => F(v * 100f, "0") + "%",
            "hp" => "+" + F(v, "0.0#"),
            "dps" => F(v, "0.0#"),
            "s" => F(v, "0.0#") + " " + Loc.T("с"),
            "tiles" => F(v, "0"),
            _ => F(v, "0.##")
        };
    }
}
