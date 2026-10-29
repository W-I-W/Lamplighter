using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Lamplighter.Balance;

public sealed class SkillRow
{
    public string Skill, Category, Note; public int Level, MaxLevel; public double CostSec, Score, GainPct, GainSe, GainPer100s; public bool Significant; public int N;
}

public sealed class BossRow { public string Boss; public int Level; public double Win, Seconds, Damage; public int N; }

public sealed class WeaponRow { public string Weapon, Class; public int Tier; public double Dps, DmgPerKill, TimePerKill, Score, RelToTier; public string Note; }

/// <summary>Стенд: короткие контрольные схватки (3 врага региона) для эффективности каждого уровня навыка и баланса оружия. Общие случайные числа (одни и те же сиды у соседних уровней) уменьшают шум.</summary>
public static class Bench
{
    static readonly int[] StageLevel = { 8, 14, 20, 25, 29 };
    static readonly int[] StageTier = { 1, 2, 3, 3, 4 };

    public static SimPlayer Char(BalanceModel m, int stage, string weaponId = null, int[] levels = null)
    {
        var p = new SimPlayer(m) { Level = StageLevel[stage], Tier = StageTier[stage], Beacon = stage + 1, HasFurnace = true, HasForge = true, HasLantern = true };
        p.BossMask = (1 << stage) - 1; for (int i = 0; i <= stage && i < 4; i++) p.Lit[i] = i == 0 ? 5 : 3; p.PoisDone = 6; p.Kills = 100;
        weaponId ??= stage == 0 ? "sword1" : stage == 1 ? "sword2" : "sword3";
        int wi = Array.FindIndex(m.Weapons, w => w.Id == weaponId); p.GiveWeapon(wi); p.Hp = p.MaxHp;
        if (levels != null) for (int i = 0; i < levels.Length; i++) p.Sk[i] = levels[i];
        p.Recompute(); p.Hp = p.MaxHp; return p;
    }

    /// <summary>Схватки с тремя врагами региона. Метрика схватки: нанесённый урон на потерянное здоровье, E = урон / (чистые потери здоровья + 5); непрерывна, учитывает и силу атаки, и защиту.
    /// Возвращает метрики по схваткам (для парных разностей при общих случайных числах), средние потери здоровья и время на убийство.</summary>
    public static (double[] e, double dmg, double time) Fights(BalanceModel m, SimPlayer proto, int stage, int k, ulong seed)
    {
        var prof = Profile.Expert(); var e = new double[k]; double tt = 0, dd = 0;
        for (int i = 0; i < k; i++)
        {
            var p = proto.Copy(); p.Hp = p.MaxHp; var rng = new Rng(Rng.Mix(seed, (ulong)i + 1, (ulong)stage));
            var cs = new CombatSim(p, prof, rng, stage, false) { Dt = .05f };
            for (int j = 0; j < 3; j++)
            {
                var list = m.Enemies.Where(x => x.Region == stage && x.Kind != "Bomber").ToArray(); float sum = list.Sum(x => x.Weight), rr = rng.F() * sum; var d = list[0];
                for (int q = 0; q < list.Length; q++) { rr -= list[q].Weight; if (rr <= 0) { d = list[q]; break; } }
                for (int g = 0; g < d.Group; g++) cs.Foes.Add(cs.MakeFoe(d, p.DangerScale, 0, false, rng.Range(50, 110)));
            }
            var st = cs.Run(90f); double net = Math.Max(0, st.DamageTaken - st.Healed); double kl = Math.Max(1, st.Kills);
            e[i] = st.DamageDealt / (net + 5.0); dd += net / kl; tt += st.Time / kl;
        }
        return (e, dd / k, tt / k);
    }

    static int StageOf(BalanceModel m, int skill) { int pg = m.Skills[skill].Page; return pg switch { 0 => 1, 1 => 1, 2 => 2, _ => 3 }; }

    /// <summary>Вспомогательные навыки, без которых эффект навыка в схватке не проявится.</summary>
    static void Context(Tech t, int[] lv)
    {
        if (t is Tech.Lens or Tech.LensII) lv[(int)Tech.HeroLamp] = 1;            // радиус света важен для ауры, которая появляется со «звёздным фонарём»
        if (t == Tech.QuickDraw || t == Tech.Overcharge || t == Tech.Piercing) lv[(int)Tech.Marksman] = Math.Max(lv[(int)Tech.Marksman], 1);
    }

    public static List<SkillRow> Skills(BalanceModel m, int k, ulong seed, int threads = 0)
    {
        var rows = new List<SkillRow>(); var lk = new object(); var prof = Profile.Expert();
        var work = new List<int>(); for (int i = 0; i < m.Skills.Length; i++) if (!m.Skills[i].Reward) work.Add(i);
        Parallel.ForEach(work, new ParallelOptions { MaxDegreeOfParallelism = threads <= 0 ? Math.Max(1, System.Environment.ProcessorCount - 1) : threads }, i =>
        {
            var s = m.Skills[i]; var t = (Tech)i; string cat = Category(t, out string note); int stage = StageOf(m, i);
            string weap = t is Tech.Marksman or Tech.QuickDraw or Tech.Piercing or Tech.Overcharge ? "bow2" : null;
            int[] baseLv = new int[m.Skills.Length]; foreach (var (pt, pm) in s.ParentTech.Zip(s.ParentMin)) baseLv[pt] = pm; Context(t, baseLv);
            var local = new List<SkillRow>(); double[] prev = null;
            for (int lv = 0; lv <= s.Max; lv++)
            {
                var lvls = (int[])baseLv.Clone(); lvls[i] = lv; var p = Char(m, stage, weap, lvls);
                var (e, dmg, time) = Fights(m, p, stage, k, seed);
                if (lv > 0)
                {
                    double c2 = Campaign.PriceSeconds(m, Char(m, stage, weap, new int[m.Skills.Length]), prof, s.LevelCost(lv, (float)m.CostMul(i)));
                    double m0 = prev.Average(), gain = (e.Average() - m0) / m0 * 100.0;
                    var d = new double[k]; for (int q = 0; q < k; q++) d[q] = e[q] - prev[q]; double se = Math.Sqrt(Var(d) / k) / m0 * 100.0;
                    local.Add(new SkillRow { Skill = s.Id, Level = lv, MaxLevel = s.Max, Category = cat, Note = note, CostSec = c2, Score = e.Average(), GainPct = gain, GainSe = se, GainPer100s = c2 > 0 ? gain / c2 * 100.0 : double.NaN, Significant = Math.Abs(gain) > 2 * se, N = k });
                }
                prev = e;
            }
            lock (lk) rows.AddRange(local);
        });
        var order = m.Skills.Select(x => x.Id).ToList();
        return rows.OrderBy(r => order.IndexOf(r.Skill)).ThenBy(r => r.Level).ToList();
    }

    static double Var(double[] x) { if (x == null || x.Length < 2) return 0; double mu = x.Average(); return x.Sum(v => (v - mu) * (v - mu)) / (x.Length - 1); }

    /// <summary>Как измерять эффект навыка. Если схватка его не отражает, строка помечается и не участвует в сравнении эффективности.</summary>
    public static string Category(Tech t, out string note)
    {
        note = "";
        switch (t)
        {
            case Tech.Dash: case Tech.Reaper: case Tech.Spin: case Tech.Searing: case Tech.SilverEdge: case Tech.FlameEdge: case Tech.FrostEdge: case Tech.Marksman: case Tech.QuickDraw: case Tech.Piercing: case Tech.Overcharge: case Tech.HeroLamp: case Tech.Pulse: return "combat";
            case Tech.Lens: case Tech.LensII: note = "радиус света героя в коде влияет на ауру и на видимость в темноте; видимость не симулируется, боевая ценность — только радиус ауры"; return "combat-partial";
            case Tech.HeatShield: note = "щит от огня работает только против огненных атак, фоновый жар не симулируется"; return "combat-partial";
            case Tech.FrostArmor: note = "сокращает озноб после ледяных ударов, фоновый холод не симулируется"; return "combat-partial";
            case Tech.FarLanterns: case Tech.FarLanternsII: case Tech.FireLens: note = "увеличивает светлую долю добычи (допущение A.DarkShare*), в схватке не отражается"; return "safety";
            case Tech.WideSwing: note = "цепная добыча (допущение A.ChainMineEff)"; return "gathering";
            case Tech.Flare: note = "ракета: схватка со стационарным огнём не моделирует позицию"; return "combat-partial";
            default: note = "эффект не симулируется (деревня/здания, видимость, мосты и т. п.)"; return "not-simulated";
        }
    }

    // ───────────────────────── дуэли с боссами ─────────────────────────
    static readonly int[][] BossLevels = { new[] { 2, 5, 8, 11, 14, 17 }, new[] { 8, 11, 14, 17, 20, 23 }, new[] { 12, 15, 18, 21, 24, 27 }, new[] { 16, 19, 22, 25, 28, 31 }, new[] { 20, 23, 26, 29, 32, 35 } };

    public static List<BossRow> Bosses(BalanceModel m, int k, ulong seed)
    {
        var rows = new List<BossRow>(); var prof = Profile.Expert();
        for (int b = 0; b < 5; b++)
            foreach (int lv in BossLevels[b])
            {
                var proto = Char(m, b, b == 0 ? "sword2" : null); proto.Level = lv; proto.Sk[(int)Tech.Dash] = 3; proto.Sk[(int)Tech.Reaper] = 3; if (b >= 2) proto.Sk[(int)Tech.Searing] = 3; proto.Recompute();
                int wins = 0; double sec = 0, dmg = 0;
                for (int i = 0; i < k; i++)
                {
                    var p = proto.Copy(); p.Hp = p.MaxHp; var cs = new CombatSim(p, prof, new Rng(Rng.Mix(seed, (ulong)i + 1, (ulong)b * 100 + (ulong)lv)), b, false); cs.Foes.Add(cs.MakeBoss(m.Bosses[b]));
                    var st = cs.Run(300f); if (!st.Died && !cs.AnyAlive) wins++; sec += st.Time; dmg += st.DamageTaken;
                }
                rows.Add(new BossRow { Boss = m.Bosses[b].Kind, Level = lv, Win = (double)wins / k, Seconds = sec / k, Damage = dmg / k, N = k });
            }
        return rows;
    }

    // ───────────────────────── оружие ─────────────────────────
    public static List<WeaponRow> Weapons(BalanceModel m, int k, ulong seed)
    {
        var rows = new List<WeaponRow>(); var prof = Profile.Expert();
        foreach (var w in m.Weapons)
        {
            int stage = w.Tier == 1 ? 0 : w.Tier == 2 ? 1 : 2; var p = Char(m, stage, w.Id); var (e, dmg, time) = Fights(m, p, stage, k, seed);
            rows.Add(new WeaponRow { Weapon = w.Id, Class = ((WClass)w.Cls).ToString(), Tier = w.Tier, Dps = Campaign.EstDps(p, prof), DmgPerKill = dmg, TimePerKill = time, Score = e.Average(), Note = w.Unique ? "уникальное (награда босса)" : "" });
        }
        foreach (var g in rows.GroupBy(r => r.Tier)) { double med = Mx.Median(g.Select(r => r.Score).ToList()); foreach (var r in g) r.RelToTier = r.Score / med; }
        return rows;
    }

    /// <summary>Вставки: приращение к оценке для Серебряного клинка без вставок.</summary>
    public static List<(string mod, double gainPct, double costSec)> Mods(BalanceModel m, int k, ulong seed)
    {
        var res = new List<(string, double, double)>(); var prof = Profile.Expert();
        var b0 = Fights(m, Char(m, 2, "sword3"), 2, k, seed); double s0 = b0.e.Average();
        foreach (var md in m.Mods)
        {
            var p = Char(m, 2, "sword3"); p.Owned[p.CurW].Mods.Add(md.Id); p.Recompute(); var f = Fights(m, p, 2, k, seed);
            res.Add((md.Name, (f.e.Average() - s0) / s0 * 100.0, Campaign.PriceSeconds(m, Char(m, 2), prof, md.Cost)));
        }
        return res;
    }
}
