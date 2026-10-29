using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Многоуровневые навыки: уровни хранятся в прогрессе игрока (SkillLv, у каждого игрока свои), конфигурация — в SkillDef (Resources/Skills), расчёт эффектов — здесь.
/// Has(навык) означает «уровень ≥ 1», поэтому условия открытия построек и способностей остались прежними. Бонусы считаются от базовой характеристики и сохранённых уровней,
/// а не накапливаются в состоянии: повторная загрузка или пересчёт ничего не удваивает.</summary>
public partial class Game
{
    public int[] SkillLv = new int[Techs.Length];
    public int TechSel = -1;                                                     // выбранный навык (индекс Tech) в окне улучшений
    public readonly Dictionary<int, Vector3> TechView = new();                   // положение дерева по категориям: x, y — смещение, z — масштаб (шаг)
    public event Action<int, int> SkillChanged;                                  // (навык, новый уровень)

    /// <summary>Битовая маска «изучено» для старых сохранений и тестов: ставит уровень 1 для новых битов, снимает уровень для сброшенных. Вторые ступени в маске не представлены.</summary>
    public int TechMask
    {
        get { int m = 0; for (int i = 0; i < 32 && i < SkillLv.Length; i++) if (SkillLv[i] > 0) m |= 1 << i; return m; }
        set { for (int i = 0; i < 32 && i < SkillLv.Length; i++) { bool on = ((value >> i) & 1) == 1; if (on && SkillLv[i] == 0) SkillLv[i] = 1; else if (!on) SkillLv[i] = 0; } }
    }

    public bool Has(Tech t) => SkillLv[(int)t] > 0;
    public int Lv(Tech t) => SkillLv[(int)t];

    // ───────────────────────── данные ─────────────────────────
    static SkillDef[] skillDefs;
    static readonly SkillDef Fallback = new();

    public static SkillDef SkillOf(int i)
    {
        if (skillDefs == null)
        {
            skillDefs = new SkillDef[Techs.Length]; var byId = new Dictionary<string, SkillDef>();
            foreach (var f in DirAccess.GetFilesAt("res://Resources/Skills"))
            {
                string name = f.EndsWith(".remap") ? f.Substring(0, f.Length - 6) : f; if (!name.EndsWith(".tres")) continue;
                var d = ResourceLoader.Load<SkillDef>("res://Resources/Skills/" + name); if (d != null && d.Id != "") byId[d.Id] = d;
            }
            for (int k = 0; k < Techs.Length; k++) { skillDefs[k] = byId.TryGetValue(((Tech)k).ToString(), out var d) ? d : new SkillDef { Id = ((Tech)k).ToString(), Page = Techs[k].Page }; Techs[k].S = skillDefs[k]; }
        }
        return skillDefs[i];
    }

    public static int SkillMax(int i) => Math.Max(1, SkillOf(i).MaxLevel);
    float E(Tech t) => SkillOf((int)t).Eff(SkillLv[(int)t]);                         // эффект навыка на текущем уровне

    // ───────────────────────── зависимости ─────────────────────────
    /// <summary>Родители навыка: (индекс, требуемый уровень). «max» — максимальный уровень родителя.</summary>
    public static List<(int tech, int min)> ParentReqs(int i)
    {
        var l = new List<(int, int)>();
        foreach (var s in SkillOf(i).Parents)
        {
            int c = s.IndexOf(':'); string id = c < 0 ? s : s.Substring(0, c), lv = c < 0 ? "1" : s.Substring(c + 1);
            if (!Enum.TryParse<Tech>(id, out var t)) continue;
            l.Add(((int)t, lv == "max" ? SkillMax((int)t) : int.Parse(lv)));
        }
        return l;
    }

    public static Tech[] ParentsOf(int i) { var l = ParentReqs(i); var r = new Tech[l.Count]; for (int k = 0; k < r.Length; k++) r[k] = (Tech)l[k].tech; return r; }
    public bool ParentsOwned(int i) { foreach (var (t, m) in ParentReqs(i)) if (SkillLv[t] < m) return false; return true; }
    public string MissingParents(int i)
    {
        var l = new List<string>(); foreach (var (t, m) in ParentReqs(i)) if (SkillLv[t] < m) l.Add($"{Loc.T(Techs[t].Name)} {m}/{SkillMax(t)}");
        return string.Join(", ", l);
    }

    /// <summary>Состояние связи родитель → навык: 2 — требование выполнено (золото), 1 — родитель развивается (голубой), 0 — закрыто (серый).</summary>
    public int LinkState(int parent, int child)
    {
        foreach (var (t, m) in ParentReqs(child)) if (t == parent) return SkillLv[t] >= m ? 2 : SkillLv[t] > 0 ? 1 : 0;
        return 0;
    }

    // ───────────────────────── стоимость ─────────────────────────
    /// <summary>Цена покупки уровня <paramref name="level"/> (1…max): базовая цена навыка × SkillDef.CostCurve[level−1]; у каждого ресурса не меньше 1, если кривая не ноль.</summary>
    public static int[][] LevelCost(int i, int level)
    {
        var d = SkillOf(i); var b = Techs[i].Cost; if (b == null) return C();
        float k = d.CostCurve != null && level >= 1 && level <= d.CostCurve.Length ? d.CostCurve[level - 1] : 1f;
        if (k <= 0) return C();
        var r = new int[b.Length][]; for (int n = 0; n < b.Length; n++) r[n] = new[] { b[n][0], b[n][1] <= 0 ? 0 : Math.Max(1, (int)Math.Ceiling(b[n][1] * k)) };
        return r;
    }

    // ───────────────────────── состояние и покупка ─────────────────────────
    /// <summary>Owned — максимальный уровень. Иначе Available/Missing — по стоимости следующего уровня, Locked — условия или родители не выполнены.</summary>
    public TechState StateOfTech(int i)
    {
        int lv = SkillLv[i], mx = SkillMax(i); if (lv >= mx) return TechState.Owned;
        if (lv == 0 && (Techs[i].Reward || !ParentsOwned(i) || !Techs[i].Ok(this))) return TechState.Locked;
        return Can(LevelCost(i, lv + 1)) ? TechState.Available : TechState.Missing;
    }

    /// <summary>Покупка одного уровня навыка (кнопка, Enter): ровно +1, цена уровня, все условия проверяются здесь же.</summary>
    public bool LearnTech(int i)
    {
        var t = Techs[i]; int lv = SkillLv[i], mx = SkillMax(i);
        if (lv >= mx) { Toast("Максимальный уровень"); return false; }
        if (lv == 0 && t.Reward) { Fail("Это награда: её даёт победа над стражем"); return false; }
        if (!ParentsOwned(i)) { Fail("Сначала изучите: " + MissingParents(i)); return false; }
        if (lv == 0 && !t.Ok(this)) { Fail("Пока недоступно: " + t.Req); return false; }
        var cost = LevelCost(i, lv + 1); if (!Can(cost)) { Fail("Не хватает: " + NeedStr(cost)); return false; }
        Pay(cost); SetSkillLevel(i, lv + 1); GiveXp(lv == 0 ? 30 : 8);
        Burst(PPos, new Color("#8ff0ff"), lv == 0 ? 20 : 10, 100);
        if (lv == 0) Announce("Изучено: " + t.Name, t.Desc, new Color("#8ff0ff"), "tech");
        else if (lv + 1 == mx) Announce("Максимальный уровень: " + t.Name, "Открыты связанные навыки", new Color("#ffd23f"), "tech");
        else { Sfx.Play("upgrade", -4, 1f + .06f * lv); Toast($"{t.Name}: {lv + 1}/{mx}"); }
        TechLearned = i; TechLearnT = .6f; Save(); return true;
    }

    /// <summary>Единственное место, где меняется уровень (покупка, награда, загрузка, миграция).</summary>
    public void SetSkillLevel(int i, int lv)
    {
        lv = Math.Clamp(lv, 0, SkillMax(i)); if (SkillLv[i] == lv) return; SkillLv[i] = lv; SkillChanged?.Invoke(i, lv); RecomputeEffects();
    }

    // ───────────────────────── сохранение и миграция ─────────────────────────
    public (string[] k, int[] v) SaveSkills()
    {
        var k = new List<string>(); var v = new List<int>(); for (int i = 0; i < SkillLv.Length; i++) if (SkillLv[i] > 0) { k.Add(((Tech)i).ToString()); v.Add(SkillLv[i]); }
        return (k.ToArray(), v.ToArray());
    }

    /// <summary>Загрузка уровней. Нового формата нет (старое сохранение) — миграция из маски: способности (рывок, круговой удар, ракета, импульс) получают уровень 1, то есть ту силу, что была,
    /// а числовые улучшения — максимальный уровень, чтобы суммарный бонус остался прежним (прежнее +30% света = Линза I и II на максимуме). Ресурсы за это не возвращаются и не списываются.</summary>
    public void LoadSkills(string[] keys, int[] vals, int legacyMask)
    {
        Array.Clear(SkillLv);
        if (keys != null && vals != null)
        {
            for (int n = 0; n < keys.Length && n < vals.Length; n++) if (Enum.TryParse<Tech>(keys[n], out var t) && (int)t < SkillLv.Length) SkillLv[(int)t] = Math.Clamp(vals[n], 0, SkillMax((int)t));
            return;
        }
        for (int i = 0; i < 32; i++) if (((legacyMask >> i) & 1) == 1) SkillLv[i] = LegacyLevel(i);
        if (SkillLv[(int)Tech.Lens] > 0) SkillLv[(int)Tech.LensII] = SkillMax((int)Tech.LensII);
        if (SkillLv[(int)Tech.FarLanterns] > 0) SkillLv[(int)Tech.FarLanternsII] = SkillMax((int)Tech.FarLanternsII);
    }

    static int LegacyLevel(int i) { var t = (Tech)i; return t == Tech.Dash || t == Tech.Spin || t == Tech.Flare || t == Tech.Pulse || Techs[i].Reward ? 1 : SkillMax(i); }

    // ───────────────────────── расчёт эффектов ─────────────────────────
    public float LensBonus => E(Tech.Lens) + E(Tech.LensII);
    public float FarLampBonus => E(Tech.FarLanterns) + E(Tech.FarLanternsII);
    public float DashCooldown => Tune.T.DashCooldown * (1f - E(Tech.Dash));
    public float SpinScale => 1f + E(Tech.Spin);
    public float FlareDuration => 10f + E(Tech.Flare);
    public float PulseCooldown => 12f * (1f - E(Tech.Pulse));
    public float RegenPerSec => Tune.T.RegenRate + E(Tech.Regen);                    // здоровье в секунду вне света (после RegenDelay без урона)
    public float ReaperHeal => E(Tech.Reaper) * Tune.T.ReaperHeal / 1.5f;
    public float SightTiles => E(Tech.Sight);
    public float HeatTaken => 1f - E(Tech.HeatShield);
    public float ColdTaken => 1f - E(Tech.FrostArmor);
    public float WaterSpeed => E(Tech.Boots);
    /// <summary>Урон света по врагам вокруг героя в секунду: лучшее из трёх источников (Жгучий свет, Звёздный фонарь, улучшение фонаря героя).</summary>
    public float AuraDps
    {
        get
        {
            float a = Has(Tech.Searing) ? E(Tech.Searing) : 0f, b = Has(Tech.HeroLamp) ? 2.5f + .5f * (Lv(Tech.HeroLamp) - 1) : 0f, c = HeroLampLv >= 2 ? 2.5f : 0f;
            return Math.Max(a, Math.Max(b, c));
        }
    }
}
