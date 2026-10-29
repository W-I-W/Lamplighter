using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Lamplighter.Balance;

public sealed class OwnedW { public int W; public List<int> Mods = new(); }

/// <summary>Состояние героя в симуляции. Формулы — копии формул Game (имя метода указано рядом); соответствие проверяет команда validate на настоящем Game.</summary>
public sealed class SimPlayer
{
    public readonly BalanceModel M; readonly TuneM T;
    public int Level = 1; public float Xp, Hp = 60; public int Tier, Kills, BossMask, Beacon = 1, PoisDone;
    public int[] Lit = new int[5];
    public bool HasFurnace, HasForge, HasLantern, HasAxe;
    public int[] Inv = new int[It.Count]; public int[] Sk;
    public List<OwnedW> Owned = new(); public int CurW = -1; public int[] ModInv = new int[7];
    public HashSet<string> Bp = new(); public Dictionary<string, int> Frag = new();
    public List<string> RelicsOwned = new(), RelicsEq = new();
    public double Time; public int Deaths; public float DashCd, SpinCd, FlareCd, PulseCd;
    public float[] EffP = new float[(int)Fxk.Count]; public Dictionary<string, float> Num = new(); public HashSet<string> Flags = new(); public Dictionary<string, int> Tags = new();

    public SimPlayer(BalanceModel m) { M = m; T = m.T; Sk = new int[m.Skills.Length]; Hp = MaxHp; }

    // ───────────────────────── характеристики ─────────────────────────
    public float MaxHp => 60f + 10f * (Level - 1);                                                         // Game.GiveXp: +10 за уровень
    public float XpNeed => T.XpBase + T.XpLinear * Level + T.XpQuad * Level * Level;                       // Game.XpNeed
    public float N(string k) => Num.TryGetValue(k, out var v) ? v : 0f;
    public float E(Fxk k) => EffP[(int)k];
    public float Se(Tech t) => M.Skills[(int)t].Eff(Sk[(int)t]);                                            // Game.E(Tech)
    public int Lv(Tech t) => Sk[(int)t];
    public bool Has(Tech t) => Sk[(int)t] > 0;
    public int LitCount { get { int n = 0; foreach (var x in Lit) n += x; return n; } }
    public int LitIn(int r) => Lit[r];
    public bool BossDown(int id) => ((BossMask >> (id - 1)) & 1) == 1;
    public int BossCount { get { int n = 0, m = BossMask; while (m != 0) { n += m & 1; m >>= 1; } return n; } }
    public int RelicSlots => 3 + Math.Min(3, BossCount);                                                    // Game.RelicSlots
    public float FearMul => Level >= T.FearLevel ? 1f : 1f + T.FearDamage * (T.FearLevel - Level);                  // Game.FearMul
    public float PlayerDamage => (2f + 2f * Tier + .4f * (Level - 1)) * (1f + N("Dmg"));                    // Game.PlayerDamage
    public float WeaponDmg(WeaponM w) => (w.Dmg + .35f * (Level - 1)) * (1f + N("Dmg"));                    // Game.WeaponDmg
    public float MineSpeed => (1f + .7f * Tier) * (1f + .06f * (Level - 1)) * (1f + N("MineSpd"));          // Game.MineSpeed
    public float LensBonus => Se(Tech.Lens) + Se(Tech.LensII);
    public float FarLampBonus => Se(Tech.FarLanterns) + Se(Tech.FarLanternsII);
    public float LightR => (72f + 8 * Tier + 2 * Level) * (1f + LensBonus) * (1f + Se(Tech.HeroLamp)) * (1f + N("HeroRadius"));   // Game.PlayerLightR (без фонаря героя и «лучезарности»)
    public float DashCooldown => T.DashCooldown * (1f - Se(Tech.Dash));
    public float SpinScale => 1f + Se(Tech.Spin);
    public float FlareDuration => 10f + Se(Tech.Flare);
    public float PulseCooldown => 12f * (1f - Se(Tech.Pulse));
    public float ReaperHeal => Se(Tech.Reaper) * T.ReaperHeal / 1.5f;
    public float HeatTaken => 1f - Se(Tech.HeatShield);
    public float ColdTaken => 1f - Se(Tech.FrostArmor);
    public float AuraDps { get { float a = Has(Tech.Searing) ? Se(Tech.Searing) : 0f, b = Has(Tech.HeroLamp) ? 2.5f + .5f * (Lv(Tech.HeroLamp) - 1) : 0f; return Math.Max(a, b); } }
    public float DangerScale => 1f + .18f * LitCount + .12f * (Lit[1] + Lit[2] + Lit[3]) + (BossDown(1) ? .1f : 0) + (BossDown(2) ? .1f : 0) + (BossDown(3) ? .1f : 0) + (BossDown(4) ? .1f : 0);   // Game.DangerScale
    public int ChainLimit => (Tier >= 4 ? 5 : Tier >= 3 ? 3 : Has(Tech.WideSwing) ? 1 : 0) + (int)N("MineChain");
    public WeaponM Cur => CurW >= 0 && CurW < Owned.Count ? M.Weapons[Owned[CurW].W] : null;
    public bool HasWeapon(string id) { foreach (var o in Owned) if (M.Weapons[o.W].Id == id) return true; return false; }
    public bool HasRanged() { foreach (var o in Owned) if (M.Weapons[o.W].Cls != (int)WClass.Sword) return true; return false; }

    // ───────────────────────── опыт и уровни ─────────────────────────
    public void GiveXp(float x, bool raw = false)
    {
        Xp += raw ? x : x * T.XpMul;
        while (Xp >= XpNeed) { Xp -= XpNeed; Level++; Hp = MaxHp; }
    }

    // ───────────────────────── ресурсы ─────────────────────────
    public bool Can(int[][] cost) { if (cost == null) return true; foreach (var c in cost) if (Inv[c[0]] < c[1]) return false; return true; }
    public void Pay(int[][] cost) { if (cost == null) return; foreach (var c in cost) Inv[c[0]] -= c[1]; }
    public int Missing(int[][] cost, int item) { if (cost == null) return 0; foreach (var c in cost) if (c[0] == item) return Math.Max(0, c[1] - Inv[item]); return 0; }

    // ───────────────────────── навыки ─────────────────────────
    public bool ParentsOwned(int i) { var s = M.Skills[i]; for (int k = 0; k < s.ParentTech.Length; k++) if (Sk[s.ParentTech[k]] < s.ParentMin[k]) return false; return true; }

    /// <summary>Условие открытия (TechDef.Ok): переписано по таблице Game.Techs, в тестах сверяется с числом навыков.</summary>
    public bool Unlocked(int i)
    {
        switch ((Tech)i)
        {
            case Tech.Lens: return HasFurnace;
            case Tech.Dash: return Tier >= 1;
            case Tech.Reaper: return Kills >= 8;
            case Tech.WideSwing: return HasForge;
            case Tech.FarLanterns: return HasLantern;
            case Tech.Spin: return LitCount >= 1;
            case Tech.Searing: return LitCount >= 2;
            case Tech.Flare: return PoisDone >= 2;
            case Tech.Sight: return PoisDone >= 5;
            case Tech.Pulse: return BossDown(1);
            case Tech.SilverEdge: case Tech.CombatLamp: case Tech.LensII: case Tech.FarLanternsII: return LitIn(1) >= 1;
            case Tech.Optics: return Inv[It.Crystal] >= 1 || Tier >= 3;
            case Tech.HeroLamp: return Has(Tech.Lens);
            case Tech.SilverGears: return HasForge;
            case Tech.AutoDrill: return LitIn(1) >= 1 && HasForge;
            case Tech.Boots: return Beacon >= 2;
            case Tech.Beam: return BossDown(2);
            case Tech.FlameEdge: case Tech.HeatShield: case Tech.Collector: return Beacon >= 3;
            case Tech.ForgeHeat: return Beacon >= 3 && HasForge;
            case Tech.FireLens: return LitIn(2) >= 1;
            case Tech.Fireproof: return BossDown(3);
            case Tech.Prism: return BossDown(4);
            case Tech.SpikeBoots: case Tech.FrostEdge: case Tech.FrostArmor: return Beacon >= 4;
            case Tech.Marksman: return HasRanged();
            case Tech.QuickDraw: return HasRanged() && Kills >= 30;
            case Tech.Piercing: return Has(Tech.Marksman);
            case Tech.Overcharge: return Has(Tech.Marksman) && LitCount >= 2;
            default: return false;
        }
    }

    public bool CanBuySkill(int i, out int[][] cost)
    {
        cost = null; var s = M.Skills[i]; int lv = Sk[i]; if (lv >= s.Max) return false;
        if (lv == 0 && (s.Reward || !Unlocked(i))) return false; if (!ParentsOwned(i)) return false;
        cost = s.LevelCost(lv + 1, (float)M.CostMul(i)); return Can(cost);
    }

    public bool BuySkill(int i)
    {
        if (!CanBuySkill(i, out var cost)) return false; Pay(cost); Sk[i]++; GiveXp(Sk[i] == 1 ? 30 : 8); Recompute(); return true;
    }

    // ───────────────────────── эффекты оружия, реликвий, синергий ─────────────────────────
    public void Recompute()
    {
        Array.Clear(EffP); Num.Clear(); Flags.Clear(); Tags.Clear();
        void Tag(string t) { if (!string.IsNullOrEmpty(t)) Tags[t] = Tags.GetValueOrDefault(t) + 1; }
        var w = Cur;
        if (w != null)
        {
            for (int i = 0; i < w.FxK.Length; i++) EffP[(int)Enum.Parse<Fxk>(w.FxK[i])] += w.FxP[i];
            foreach (var t in w.Tags ?? Array.Empty<string>()) Tag(t);
            foreach (var mi in Owned[CurW].Mods) { var md = M.Mods[mi]; for (int i = 0; i < md.FxK.Length; i++) EffP[(int)Enum.Parse<Fxk>(md.FxK[i])] += md.FxP[i]; Tag(md.Tag); }
        }
        foreach (var id in RelicsEq) { var r = M.Relics.FirstOrDefault(x => x.Id == id); if (r == null) continue; foreach (var t in r.Tags) Tag(t); foreach (var e in r.Effects) AddEffect(e); }
        foreach (var s in M.Syns)
        {
            bool ok = true; foreach (var rq in s.Requires) if (!SynReq(rq)) { ok = false; break; }
            if (ok) foreach (var g in s.Grants) AddEffect(g);
        }
    }

    void AddEffect(string eff)
    {
        var p = eff.Split(':'); if (p.Length < 2) return;
        if (p[0] == "Flag") { Flags.Add(p[1]); return; }
        if (!float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return;
        if (Enum.TryParse<Fxk>(p[0], out var k)) EffP[(int)k] += v; else Num[p[0]] = N(p[0]) + v;
    }

    bool SynReq(string cond)                                                                                // Game.SynReq
    {
        if (cond.StartsWith("tag:"))
        {
            var s = cond.Substring(4); int ge = s.IndexOf(">="); string tag = ge >= 0 ? s.Substring(0, ge) : s; int need = ge >= 0 ? int.Parse(s.Substring(ge + 2)) : 1;
            return Tags.GetValueOrDefault(tag) >= need;
        }
        if (cond.StartsWith("class:")) { var w = Cur; return w != null && (w.Cls == (int)WClass.Sword ? "sword" : w.Cls == (int)WClass.Bow ? "bow" : w.Cls == (int)WClass.Staff ? "staff" : "pick") == cond.Substring(6); }
        if (cond.StartsWith("ability:"))
            return cond.Substring(8) switch { "pulse" => Has(Tech.Pulse), "dash" => Has(Tech.Dash), "spin" => Has(Tech.Spin), "flare" => Has(Tech.Flare), "beam" => Has(Tech.Beam), "lamp" => HasLantern || Has(Tech.HeroLamp), _ => false };
        return false;
    }

    public void GiveWeapon(int wi) { foreach (var o in Owned) if (o.W == wi) return; Owned.Add(new OwnedW { W = wi }); if (CurW < 0) CurW = Owned.Count - 1; Recompute(); }
    public void Equip(int ownedIdx) { CurW = ownedIdx; Recompute(); }

    public void GiveRelic(string id, bool quiet = true)
    {
        if (RelicsOwned.Contains(id)) { Inv[It.Coins] += 20; return; }
        RelicsOwned.Add(id); var r = M.Relics.FirstOrDefault(x => x.Id == id);
        if (r != null && r.Permanent && RelicsEq.Count < RelicSlots) RelicsEq.Add(id);
        Recompute();
    }

    public SimPlayer Copy()
    {
        var c = (SimPlayer)MemberwiseClone(); c.Lit = (int[])Lit.Clone(); c.Inv = (int[])Inv.Clone(); c.Sk = (int[])Sk.Clone(); c.ModInv = (int[])ModInv.Clone();
        c.Owned = Owned.Select(o => new OwnedW { W = o.W, Mods = new List<int>(o.Mods) }).ToList(); c.Bp = new HashSet<string>(Bp); c.Frag = new Dictionary<string, int>(Frag);
        c.RelicsOwned = new List<string>(RelicsOwned); c.RelicsEq = new List<string>(RelicsEq); c.EffP = (float[])EffP.Clone(); c.Num = new Dictionary<string, float>(Num);
        c.Flags = new HashSet<string>(Flags); c.Tags = new Dictionary<string, int>(Tags); return c;
    }
}
