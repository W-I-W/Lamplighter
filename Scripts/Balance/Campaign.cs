using System;
using System.Collections.Generic;
using System.Linq;

namespace Lamplighter.Balance;

public enum Outcome { Won, Stuck, Timeout }

public static class Ms
{
    public static readonly string[] Names = { "furnace", "first_weapon", "tier1", "forest_lamps", "boss1", "beacon2", "tier2", "boss2", "beacon3", "tier3", "boss3", "beacon4", "boss4", "beacon5", "boss5_win" };
    public const int Furnace = 0, Weapon = 1, Tier1 = 2, Lamps = 3, Boss1 = 4, Beacon2 = 5, Tier2 = 6, Boss2 = 7, Beacon3 = 8, Tier3 = 9, Boss3 = 10, Beacon4 = 11, Boss4 = 12, Beacon5 = 13, Win = 14;
}

public sealed class RunResult
{
    public string Profile, Arch, StuckReason = ""; public ulong Seed; public Outcome Out; public float Hours; public int Deaths, Level, Kills, Tier, Beacon;
    public float[] Ms = Enumerable.Repeat(-1f, Balance.Ms.Names.Length).ToArray();
    public int[] DeathsRegion = new int[5], DeathsBoss = new int[5], BossAttempts = new int[5];
    public Dictionary<string, int> Killers = new();
    public int[] Gathered = new int[It.Count], EndInv = new int[It.Count], SkillLv; public string Weapon = ""; public int[] ModsOn = Array.Empty<int>(); public string[] Relics = Array.Empty<string>();
    public float TFight, TGather, TTravel, TRest, TCraft, TExplore;
    public float[] LevelAtHour = new float[12];
    public float[] StageSeconds = new float[5];
    public float[] LevelAtMs = new float[Balance.Ms.Names.Length];
    public Dictionary<string, int> Spawned = new();
}

/// <summary>Прохождение одного виртуального игрока: добыча, плавка, крафт, фонари, исследование, прокачка, боссы, смерти. Мир не воспроизводится, вместо него — параметры BalanceModel.A (см. отчёт).</summary>
public sealed class Campaign
{
    readonly BalanceModel M; readonly TuneM T; readonly Profile pr; readonly Rng r; SimPlayer p; readonly RunResult res = new(); readonly Archetype arch;
    int region;                                   // где герой сейчас
    float limit;                                  // предел времени, с
    int streak;                                   // смертей подряд на одном этапе
    readonly Dictionary<string, int> stageDeaths = new();
    public volatile bool Cancel;
    static readonly int[] OreOf = { It.CopperOre, It.IronOre, It.SilverOre };

    public Campaign(BalanceModel m, Profile prof, ulong seed)
    {
        M = m; T = m.T; pr = prof; r = new Rng(seed); p = new SimPlayer(m); limit = m.A.MaxHours * 3600f;
        res.Profile = prof.Name; res.Seed = seed;
        if (prof.PicksArchetype) arch = Profile.Archetypes[r.Int(0, Profile.Archetypes.Length - 1)];
        res.Arch = arch?.Name ?? (prof.Skills == SkillPolicy.Greedy ? "greedy" : prof.Skills == SkillPolicy.Random ? "random" : "cheapest");
    }

    /// <summary>Для стендовых проверок: кампания над уже собранным героем (только цены и оценки).</summary>
    public static float PriceSeconds(BalanceModel m, SimPlayer pl, Profile prof, int[][] cost) { var c = new Campaign(m, prof, 1, pl); return c.CostSeconds(cost); }
    Campaign(BalanceModel m, Profile prof, ulong seed, SimPlayer pl) : this(m, prof, seed) { p = pl; }

    // ───────────────────────── учёт времени ─────────────────────────
    void Tick(double s, ref float bucket) { p.Time += s; bucket += (float)s; StageClock(s); }
    void StageClock(double s) { res.StageSeconds[Math.Clamp(region, 0, 4)] += (float)s; }
    bool Over => p.Time > limit || Cancel;
    void Mark(int ms) { if (res.Ms[ms] < 0) { res.Ms[ms] = (float)(p.Time / 3600.0); res.LevelAtMs[ms] = p.Level; } }
    void Hourly() { int h = (int)(p.Time / 3600.0); for (int i = 0; i < res.LevelAtHour.Length; i++) if (i <= h && res.LevelAtHour[i] == 0) res.LevelAtHour[i] = p.Level; }

    // ───────────────────────── цены (в секундах труда) ─────────────────────────
    sealed class Src { public int Item; public float NMin, NMax, Xp, Need; public int Tier, Group; public int[] Regions; }
    static readonly Src[] Srcs =
    {
        new() { Item = It.Wood, NMin = 2, NMax = 3, Xp = 3, Need = 2.2f, Tier = 0, Group = 0, Regions = new[] { 0 } },
        new() { Item = It.Stone, NMin = 1, NMax = 2, Xp = 4, Need = 1.5f, Tier = 0, Group = 1, Regions = new[] { 0, 1, 2, 3 } },
        new() { Item = It.CopperOre, NMin = 1, NMax = 2, Xp = 6, Need = 1.8f, Tier = 0, Group = 2, Regions = new[] { 0, 1 } },
        new() { Item = It.IronOre, NMin = 1, NMax = 2, Xp = 10, Need = 2.6f, Tier = 1, Group = 2, Regions = new[] { 0, 1, 2, 3 } },
        new() { Item = It.Shard, NMin = 1, NMax = 2, Xp = 15, Need = 3.2f, Tier = 2, Group = 2, Regions = new[] { 0, 1 } },
        new() { Item = It.Aether, NMin = 1, NMax = 1, Xp = 25, Need = 4f, Tier = 2, Group = 2, Regions = new[] { 0, 1, 2, 3 } },
        new() { Item = It.SilverOre, NMin = 1, NMax = 2, Xp = 14, Need = 3f, Tier = 2, Group = 2, Regions = new[] { 1, 3 } },
        new() { Item = It.Crystal, NMin = 1, NMax = 2, Xp = 20, Need = 3.6f, Tier = 2, Group = 2, Regions = new[] { 1 } },
        new() { Item = It.Obsidian, NMin = 1, NMax = 1, Xp = 24, Need = 4.2f, Tier = 3, Group = 2, Regions = new[] { 2 } },
        new() { Item = It.Ember, NMin = 1, NMax = 2, Xp = 24, Need = 4f, Tier = 3, Group = 2, Regions = new[] { 2 } },
        new() { Item = It.Frost, NMin = 1, NMax = 2, Xp = 26, Need = 4f, Tier = 3, Group = 2, Regions = new[] { 3 } },
    };
    Src SrcOf(int item) { foreach (var s in Srcs) if (s.Item == item) return s; return null; }

    float ToolEff(Src s) => s.Group switch { 0 => (p.HasAxe && pr.UsesAxe) ? 1.9f : .7f, 1 => (p.HasAxe && pr.UsesAxe && false) ? .45f : 1f, _ => 1f };
    float TileSeconds(Src s) => s.Need * T.MineTimeMul / (p.MineSpeed * ToolEff(s)) / (1f + p.ChainLimit * M.A.ChainMineEff) + M.A.TravelPerTile * pr.GatherWaste;
    float ItemSeconds(int item)
    {
        if (item == It.Copper || item == It.Iron || item == It.Silver) return ItemSeconds(IngotOre(item)) + ItemSeconds(It.Wood) + .9f;
        if (item == It.Shard) { var s0 = SrcOf(It.Shard); return p.Tier >= 2 ? TileSeconds(s0) / 1.5f : 14f; }
        if (item == It.Relic) return 60f / Math.Max(.05f, M.A.PoiPerMinute) / .5f;
        var s = SrcOf(item); if (s == null) return 1e5f;
        if (p.Tier < s.Tier) return 1e4f;
        float perTile = Math.Max(1f, (s.NMin + s.NMax) / 2f * T.GatherMul) * (1f + p.ChainLimit * M.A.ChainMineEff);
        return TileSeconds(s) / Math.Max(.5f, perTile) * (1f + 0f);
    }
    static int IngotOre(int ing) => ing == It.Copper ? It.CopperOre : ing == It.Iron ? It.IronOre : It.SilverOre;
    float CostSeconds(int[][] cost) { if (cost == null) return 0; float t = 0; foreach (var c in cost) t += Math.Max(0, c[1] - p.Inv[c[0]]) * ItemSeconds(c[0]); return t; }
    bool Obtainable(int[][] cost) { if (cost == null) return true; foreach (var c in cost) { int it = c[0]; if (it == It.Relic || it == It.Core || it == It.Gear || it == It.Lens || it == It.AncCrystal || it == It.Coins || it == It.RiftShard) { if (p.Inv[it] < c[1]) return false; } else if (CostSeconds(new[] { c }) >= 1e4f) return false; } return true; }

    // ───────────────────────── время в мире: добыча и охота под угрозой ─────────────────────────
    float DarkShare(int reg)
    {
        float d0 = reg switch { 0 => M.A.DarkShare0, 1 => M.A.DarkShare1, 2 => M.A.DarkShare2, 3 => M.A.DarkShare3, _ => M.A.DarkShare4 };
        float lit = Math.Max(0f, 1f - d0 + M.A.LitShareGain * p.Lit[reg]);
        float rad = (1f + p.FarLampBonus) * (1f + p.Se(Tech.FireLens)); lit = Math.Min(.9f, lit * rad * rad);
        return 1f - lit;
    }

    /// <summary>Рабочее время seconds в регионе: тёмная доля идёт под появление врагов, светлая — безопасно.</summary>
    void Work(float seconds, int reg, ref float bucket, bool hunt = false)
    {
        if (seconds <= 0) return; region = reg;
        float dark = hunt ? seconds : seconds * DarkShare(reg), safe = seconds - dark;
        if (safe > 0) { Tick(safe, ref bucket); Regen(safe, true); }
        if (dark > 0) Exposure(dark, reg, hunt, ref bucket);
    }

    void Regen(float s, bool lit) { if (p.Hp < p.MaxHp) p.Hp = Math.Min(p.MaxHp, p.Hp + (lit ? 4f : T.RegenRate) * s); }

    EnemyM Pick(int reg, bool guardMode = false)
    {
        var list = M.Enemies.Where(e => e.Region == reg).ToArray(); var w = new float[list.Length]; float sum = 0;
        for (int i = 0; i < list.Length; i++)
        {
            var e = list[i]; float x = e.Weight;
            if (reg == 0) { if (e.Kind == "Spitter") x *= M.A.FarSpitterShare; if (e.Kind == "Swarm" && p.LitCount < 1) x = 0; if (e.Kind == "Bomber" && p.LitCount < 2) x = 0; }
            if (reg == 1) { if (e.Kind == "Eel") x *= M.A.WaterShare; if (e.Kind == "Swarm" && p.LitIn(1) < 1) x = 0; }
            w[i] = x; sum += x;
        }
        float rr = r.F() * sum; for (int i = 0; i < w.Length; i++) { rr -= w[i]; if (rr <= 0) return list[i]; }
        return list[0];
    }

    void Spawn(CombatSim cs, int reg)
    {
        var d = Pick(reg); int elites = 0; foreach (var f in cs.Foes) if (f.Alive && f.Elite) elites++;
        res.Spawned[d.Kind + "@" + reg] = res.Spawned.GetValueOrDefault(d.Kind + "@" + reg) + d.Group;
        for (int i = 0; i < d.Group; i++) cs.Foes.Add(cs.MakeFoe(d, p.DangerScale, elites, true, d.Group > 1 ? r.Range(130, 200) : -1f));
    }

    /// <summary>Тёмная сессия: пока герой трудится, раз в 2,2–3,8 с (Game.UpdateSpawning) у него появляется враг, если запас не исчерпан; бой идёт пока есть враги.</summary>
    void Exposure(float work, int reg, bool hunt, ref float bucket)
    {
        var cs = new CombatSim(p, pr, r, reg, false);
        float done = 0, spawnT = r.Range(1f, 3f);
        int baseCap = reg == 0 ? 3 + p.LitCount * 2 : 4 + p.LitIn(reg) * 2; int cap = Math.Min((int)(baseCap + T.SpawnCapBonus + (p.Level < T.FearLevel ? T.FearSpawn : 0)), 18);
        while (done < work && !Over)
        {
            if (!cs.AnyAlive)
            {
                if (p.Hp < pr.RestBelow * p.MaxHp) Rest();
                float step = Math.Min(spawnT, work - done); Tick(step, ref bucket); done += step; spawnT -= step; cs.Idle(step);
                if (done >= work) break;
            }
            else
            {
                bool contact = cs.AnyContact; cs.Step(); spawnT -= cs.Dt; res.TFight += cs.Dt; StageClock(cs.Dt);
                if (!contact) done += cs.Dt;
                if (cs.S.Died) { Die(cs.S.KilledBy, false, reg, 0); cs = new CombatSim(p, pr, r, reg, false); spawnT = 2f; continue; }
            }
            if (spawnT <= 0)
            {
                spawnT = r.Range(2.2f, 3.8f); if (cs.Alive < cap) Spawn(cs, reg);
            }
        }
        Hourly();
    }

    void Rest()
    {
        float t = M.A.RestTravel + Math.Max(0, p.MaxHp - p.Hp) / 4f; Tick(t, ref res.TRest); p.Hp = p.MaxHp;
    }

    void Die(string killer, bool boss, int reg, int bossId)
    {
        p.Deaths++; res.Deaths++; streak++;
        if (boss) res.DeathsBoss[bossId - 1]++; else res.DeathsRegion[Math.Clamp(reg, 0, 4)]++;
        res.Killers[killer ?? "env"] = res.Killers.GetValueOrDefault(killer ?? "env") + 1;
        p.Inv[It.Shard] -= Math.Min(25, p.Inv[It.Shard] / 4);
        string key = boss ? "boss" + bossId : "region" + reg; stageDeaths[key] = stageDeaths.GetValueOrDefault(key) + 1;
        if (stageDeaths[key] >= pr.QuitDeaths) throw new StuckException($"бросил игру: {stageDeaths[key]} смертей на этапе {key}");
        Tick(M.A.RespawnTravel, ref res.TTravel); p.Hp = p.MaxHp;
    }

    // ───────────────────────── добыча, плавка, получение ─────────────────────────
    int Yield(int item, float n)
    {
        if (item == It.Shard) return (int)n;
        float v = n * T.GatherMul; int i = (int)v; if (r.Chance(v - i)) i++; return Math.Max(1, i);
    }

    void Mine(int item, int target)
    {
        var s = SrcOf(item); int reg = region, home = region; if (Array.IndexOf(s.Regions, reg) < 0 || reg > p.Beacon - 1) reg = s.Regions.First(x => x <= p.Beacon - 1);
        if (reg != home) { Tick(M.A.RestTravel * 2f, ref res.TTravel); region = reg; }
        float secs = 0; int guard = 0;
        while (p.Inv[item] < target && guard++ < 5000)
        {
            float extra = 1f + p.ChainLimit * M.A.ChainMineEff; int tiles = (int)extra + (r.Chance(extra - (int)extra) ? 1 : 0);
            secs += TileSeconds(s) * (1f + 0f);
            for (int k = 0; k < tiles; k++) { int n = r.Int((int)s.NMin, (int)s.NMax); int y = Yield(item, n); p.Inv[item] += y; res.Gathered[item] += y; p.GiveXp(s.Xp); }
        }
        Work(secs, reg, ref res.TGather); region = home;
    }

    void Smelt(int ingot, int n)
    {
        int ore = IngotOre(ingot);
        Obtain(ore, p.Inv[ore] + n); Obtain(It.Wood, p.Inv[It.Wood] + n);
        p.Inv[ore] -= n; p.Inv[It.Wood] -= n; p.Inv[ingot] += n; p.GiveXp(2 * n); Tick(.9f * n, ref res.TCraft);
    }

    bool Obtain(int item, int need)
    {
        if (p.Inv[item] >= need) return true; if (Over) return false;
        int want = need + (int)Math.Ceiling(pr.WasteStock * Math.Max(0, need - p.Inv[item]));
        if (item == It.Copper || item == It.Iron || item == It.Silver) { if (p.Tier < SrcOf(IngotOre(item)).Tier) return false; Smelt(item, want - p.Inv[item]); return true; }
        if (item == It.Shard) return ObtainShards(want);
        var s = SrcOf(item); if (s == null || p.Tier < s.Tier || !s.Regions.Any(x => x <= p.Beacon - 1)) return false;
        if (item == It.Aether && p.LitCount < 1) return false; if (item == It.IronOre && p.Tier < 1) return false;
        Mine(item, want); return p.Inv[item] >= need;
    }

    bool ObtainShards(int want)
    {
        int guard = 0;
        while (p.Inv[It.Shard] < want && guard++ < 400 && !Over)
        {
            bool mine = p.Tier >= 2 && (region >= 1 || p.LitCount >= 2) && ItemSeconds(It.Shard) < 14f;
            if (mine) { Mine(It.Shard, Math.Min(want, p.Inv[It.Shard] + 4)); }
            else { int before = p.Inv[It.Shard]; Work(40f, Math.Min(region, p.Beacon - 1), ref res.TFight, true); }
        }
        return p.Inv[It.Shard] >= want;
    }

    bool Need(int[][] cost)
    {
        if (cost == null) return true;
        foreach (var c in cost) if (!Obtain(c[0], c[1])) return false;
        return p.Can(cost);
    }

    // ───────────────────────── оценка силы (для решений игрока) ─────────────────────────
    public static float EstDps(SimPlayer p, Profile pr)
    {
        var w = p.Cur; float dmg, cd; bool melee = w == null || w.Cls == (int)WClass.Sword;
        if (w == null) { dmg = p.PlayerDamage; cd = 1f; } else { dmg = p.WeaponDmg(w); cd = w.Cd * (melee ? 1f : 1f - p.Se(Tech.QuickDraw)); }
        float shots = 1f, mult = 1f;
        if (!melee) { int n = 1 + (int)p.E(Fxk.Multi); shots = n * (n > 1 ? .75f : 1f); mult *= 1f + p.Se(Tech.Marksman); mult *= 1f + .25f * (w.Pierce + (int)p.E(Fxk.Pierce) + (p.Has(Tech.Piercing) ? 1 : 0)); }
        else { mult *= 1f + .4f * (int)p.E(Fxk.Multi) + .55f * (float)Math.Ceiling(p.E(Fxk.Arc)); mult *= 1f + p.Se(Tech.SilverEdge) * .6f; }
        float dps = dmg * shots * mult / cd * pr.Uptime;
        dps *= 1f + .4f * (1 + (int)p.E(Fxk.Chain)) * (p.E(Fxk.Chain) > 0 ? .45f : 0f);
        float b = p.E(Fxk.Burn); if (b > 0 || p.Has(Tech.FlameEdge)) dps += 3f * Math.Min(1f, Math.Max(p.Se(Tech.FlameEdge) / cd, (.45f + .2f * b) * (2f + b) / cd));
        dps += p.AuraDps * (melee ? 1f : .3f);
        if (p.E(Fxk.Freeze) > 0 || p.Has(Tech.FrostEdge)) dps *= 1.08f;
        if (p.E(Fxk.Slow) > 0) dps *= 1.04f;
        if (p.Has(Tech.Spin) && melee) dps += p.PlayerDamage * 1.2f * p.SpinScale / 3f * .5f;
        return dps;
    }

    public static float Power(SimPlayer p, Profile pr)
    {
        float dps = EstDps(p, pr); float ehp = p.MaxHp + 12f * p.ReaperHeal * 5f + 4f * (1f - p.Se(Tech.Dash)) + (p.Has(Tech.HeatShield) ? 6f * p.Se(Tech.HeatShield) * p.MaxHp / 10f : 0f) + (p.Has(Tech.FrostArmor) ? 3f * p.Se(Tech.FrostArmor) * p.MaxHp / 10f : 0f);
        if (p.Has(Tech.Dash)) ehp *= 1f + .25f * (1f - p.Se(Tech.Dash));
        if (p.Has(Tech.Fireproof)) ehp *= 1.05f;
        return MathF.Sqrt(Math.Max(.01f, dps) * ehp);
    }

    // ───────────────────────── покупки и снаряжение ─────────────────────────
    int[][] NextPickCost() => p.Tier < 4 ? M.CostPick[p.Tier] : null;

    void Spend()
    {
        if (!p.HasFurnace) return;
        int guard = 0, bought = 1;
        while (bought > 0 && guard++ < 60 && !Over)
        {
            bought = 0;
            if (r.Chance(pr.SpendDelay)) break;
            // 1) инструмент
            var pc = NextPickCost();
            if (pc != null && region >= 0 && PickWorth() && (CostSeconds(pc) < 900f) && Obtainable(pc) && Need(pc)) { p.Pay(pc); p.Tier++; p.GiveXp(40 + 20 * p.Tier); MarkTier(); bought++; }
            // 2) оружие, вставки, реликвии
            if (CraftWeapons()) bought++;
            if (pr.BuysMods && CraftMods()) bought++;
            if (pr.OptimizesRelics) OptimizeRelics();
            // 3) навыки
            if (BuyNextSkill()) bought++;
        }
        Hourly();
    }

    bool PickWorth() => p.Tier switch { 0 => true, 1 => p.Beacon >= 1 && p.LitCount >= 1 || pr.Skills == SkillPolicy.Greedy, 2 => p.Beacon >= 2, 3 => p.Beacon >= 3, _ => false };
    void MarkTier() { if (p.Tier == 1) Mark(Ms.Tier1); if (p.Tier == 2) Mark(Ms.Tier2); if (p.Tier == 3) Mark(Ms.Tier3); }

    bool CraftWeapons()
    {
        bool any = false;
        var cands = new List<int>();
        for (int i = 0; i < M.Weapons.Length; i++)
        {
            var w = M.Weapons[i]; if (w.Unique || w.Cost == null || p.HasWeapon(w.Id)) continue; if (w.Bp != null && !p.Bp.Contains(w.Bp)) continue; cands.Add(i);
        }
        if (cands.Count > 0)
        {
            int pick = -1;
            if (pr.Skills == SkillPolicy.Random) pick = p.Owned.Count == 0 ? cands[r.Int(0, cands.Count - 1)] : -1;                    // новичок берёт что-то одно и редко меняет
            else if (arch != null) { var c2 = cands.Where(i => M.Weapons[i].Cls == (int)arch.Weapon).OrderByDescending(i => M.Weapons[i].Tier).ToList(); if (c2.Count > 0 && (p.Owned.Count == 0 || M.Weapons[c2[0]].Tier > BestTier((int)arch.Weapon))) pick = c2[0]; else if (p.Owned.Count == 0) pick = cands[0]; }
            else pick = cands.OrderByDescending(i => M.Weapons[i].Tier * 10 + M.Weapons[i].Dmg / M.Weapons[i].Cd).FirstOrDefault(i => M.Weapons[i].Tier > BestTier(-1) || p.Owned.Count == 0, -1);
            if (pick >= 0 && Obtainable(M.Weapons[pick].Cost) && CostSeconds(M.Weapons[pick].Cost) < 800f && NearForge() && Need(M.Weapons[pick].Cost))
            {
                p.Pay(M.Weapons[pick].Cost); p.GiveWeapon(pick); p.GiveXp(25); any = true; Tick(8, ref res.TCraft); if (res.Ms[Ms.Weapon] < 0) Mark(Ms.Weapon);
            }
        }
        EquipBest();
        return any;
    }
    bool NearForge() => p.HasFurnace || p.HasForge;
    int BestTier(int cls) { int t = 0; foreach (var o in p.Owned) { var w = M.Weapons[o.W]; if (cls < 0 || w.Cls == cls) t = Math.Max(t, w.Tier); } return t; }

    void EquipBest()
    {
        if (p.Owned.Count == 0) return; int best = -1; float bs = -1;
        for (int i = 0; i < p.Owned.Count; i++)
        {
            int cur = p.CurW; p.CurW = i; p.Recompute(); float s = EstDps(p, pr);
            if (arch != null && M.Weapons[p.Owned[i].W].Cls == (int)arch.Weapon) s *= 1.15f;          // привязка к сборке
            if (pr.Skills == SkillPolicy.Random) s = M.Weapons[p.Owned[i].W].Tier + r.F() * .5f;
            if (s > bs) { bs = s; best = i; } p.CurW = cur;
        }
        p.CurW = best; p.Recompute();
    }

    bool CraftMods()
    {
        if (p.Cur == null) return false; bool any = false;
        var ow = p.Owned[p.CurW]; var w = M.Weapons[ow.W];
        var order = arch != null ? arch.Mods.Select(i => i).ToList() : Enumerable.Range(0, M.Mods.Length).OrderBy(_ => r.F()).ToList();
        if (pr.Skills == SkillPolicy.Greedy) order = Enumerable.Range(0, M.Mods.Length).ToList();
        while (ow.Mods.Count < w.Slots)
        {
            int bestMod = -1; float bs = EstDps(p, pr) * 1.001f;
            foreach (int mi in order)
            {
                var md = M.Mods[mi]; if (p.ModInv[mi] <= 0 && (md.Bp != null && !p.Bp.Contains(md.Bp))) continue;
                if (p.ModInv[mi] <= 0 && (!Obtainable(md.Cost) || CostSeconds(md.Cost) > 500f)) continue;
                if (ow.Mods.Contains(mi) && md.Id != 3) continue;
                ow.Mods.Add(mi); p.Recompute(); float s = EstDps(p, pr); ow.Mods.RemoveAt(ow.Mods.Count - 1); p.Recompute();
                if (arch == null && pr.Skills != SkillPolicy.Greedy) { s = r.F() + 1f; }
                if (s > bs) { bs = s; bestMod = mi; }
                if (arch != null && s > EstDps(p, pr) * 1.001f) { bestMod = mi; break; }
            }
            if (bestMod < 0) break;
            if (p.ModInv[bestMod] <= 0) { if (!Need(M.Mods[bestMod].Cost)) break; p.Pay(M.Mods[bestMod].Cost); p.ModInv[bestMod]++; p.GiveXp(10); Tick(5, ref res.TCraft); }
            p.ModInv[bestMod]--; ow.Mods.Add(bestMod); p.Recompute(); any = true;
        }
        return any;
    }

    void OptimizeRelics()
    {
        if (p.RelicsOwned.Count <= p.RelicSlots) return;
        var perm = p.RelicsOwned.Where(id => M.Relics.First(x => x.Id == id).Permanent).ToList();
        var best = p.RelicsEq.ToList(); float bs = Power(p, pr);
        foreach (var id in perm) { if (best.Contains(id)) continue; for (int k = 0; k < best.Count; k++) { var saved = p.RelicsEq.ToList(); p.RelicsEq = best.ToList(); p.RelicsEq[k] = id; p.Recompute(); float s = Power(p, pr) * (arch != null && M.Relics.First(x => x.Id == id).Tags.Any(t => arch.Tags.Contains(t)) ? 1.03f : 1f); if (s > bs) { bs = s; best = p.RelicsEq.ToList(); } p.RelicsEq = saved; } }
        p.RelicsEq = best; p.Recompute();
    }

    bool BuyNextSkill()
    {
        var cand = new List<int>();
        for (int i = 0; i < M.Skills.Length; i++) { var s = M.Skills[i]; if (s.Reward || p.Sk[i] >= s.Max) continue; if (p.Sk[i] == 0 && !p.Unlocked(i)) continue; if (!p.ParentsOwned(i)) continue; cand.Add(i); }
        if (cand.Count == 0) return false;
        int pick = -1;
        switch (pr.Skills)
        {
            case SkillPolicy.Random:
                foreach (int i in cand.OrderBy(_ => r.F())) if (p.CanBuySkill(i, out _)) { pick = i; break; }
                break;
            case SkillPolicy.Cheapest:
                pick = cand.Where(i => p.CanBuySkill(i, out _) || CostSeconds(M.Skills[i].LevelCost(p.Sk[i] + 1, (float)M.CostMul(i))) < 240f).OrderBy(i => CostSeconds(M.Skills[i].LevelCost(p.Sk[i] + 1, (float)M.CostMul(i)))).FirstOrDefault(-1);
                break;
            case SkillPolicy.Archetype:
                foreach (var t in arch.Skills) { int i = (int)t; if (cand.Contains(i) && (p.CanBuySkill(i, out _) || CostSeconds(M.Skills[i].LevelCost(p.Sk[i] + 1, (float)M.CostMul(i))) < 700f)) { pick = i; break; } }
                if (pick < 0) pick = cand.Where(i => IsCombatSkill(i) && p.CanBuySkill(i, out _)).OrderBy(i => CostSeconds(M.Skills[i].LevelCost(p.Sk[i] + 1, (float)M.CostMul(i)))).FirstOrDefault(-1);
                break;
            case SkillPolicy.Greedy:
                float bestRatio = 0; float basePow = Power(p, pr);
                foreach (int i in cand)
                {
                    var cost = M.Skills[i].LevelCost(p.Sk[i] + 1, (float)M.CostMul(i)); float secs = CostSeconds(cost); if (secs > 900f || !Obtainable(cost)) continue;
                    p.Sk[i]++; float np = Power(p, pr); p.Sk[i]--; float gain = (np - basePow) / basePow;
                    if (p.Sk[i] == 0) gain += ChildValue(i) * .002f;
                    float ratio = gain / (30f + secs); if (ratio > bestRatio) { bestRatio = ratio; pick = i; }
                }
                break;
        }
        if (pick < 0) return false;
        var c = M.Skills[pick].LevelCost(p.Sk[pick] + 1, (float)M.CostMul(pick));
        if (!p.CanBuySkill(pick, out _)) { if (pr.Skills == SkillPolicy.Random) return false; if (CostSeconds(c) > 900f || !Obtainable(c) || !Need(c)) return false; }
        return p.BuySkill(pick);
    }

    static bool IsCombatSkill(int i) => (Tech)i is Tech.Dash or Tech.Reaper or Tech.Spin or Tech.Searing or Tech.SilverEdge or Tech.FlameEdge or Tech.FrostEdge or Tech.HeatShield or Tech.FrostArmor or Tech.Marksman or Tech.QuickDraw or Tech.Piercing or Tech.Overcharge or Tech.HeroLamp or Tech.Lens;
    float ChildValue(int i) => M.Skills.Count(s => s.ParentTech.Contains(i)) + (i == (int)Tech.Lens ? 1 : 0);

    // ───────────────────────── фонари, исследование ─────────────────────────
    int[] LampCosts(int reg) => reg == 0 ? M.LampCosts0 : reg == 1 ? M.LampCosts1 : reg == 2 ? M.LampCosts2 : M.LampCosts3;

    bool ClearLamp(int reg, int k)
    {
        var costs = LampCosts(reg); if (k >= costs.Length || Over) return false;
        if (!ObtainShards(p.Inv[It.Shard] < costs[k] ? costs[k] : 0)) return false;
        Work(M.A.LampTravel, reg, ref res.TTravel);
        if (r.Chance(M.A.GuardedLampChance)) Guards(reg);
        p.Inv[It.Shard] -= costs[k]; p.Lit[reg]++; p.GiveXp(60); Tick(10, ref res.TCraft);
        return true;
    }

    void Guards(int reg)
    {
        var cs = new CombatSim(p, pr, r, reg, false);
        for (int i = 0; i < (int)M.A.GuardCount; i++) cs.Foes.Add(cs.MakeFoe(Pick(reg), p.DangerScale, 0, false, r.Range(60, 120)));
        while (cs.AnyAlive && !cs.S.Died && !Over) { cs.Step(); res.TFight += cs.Dt; StageClock(cs.Dt); }
        if (cs.S.Died) Die(cs.S.KilledBy, false, reg, 0);
    }

    void Explore(float minutes, int reg)
    {
        if (minutes <= 0) return; int n = (int)Math.Round(minutes * M.A.PoiPerMinute); float t = 0;
        for (int i = 0; i < n && !Over; i++)
        {
            Work(60f / Math.Max(.05f, M.A.PoiPerMinute), reg, ref res.TExplore); p.PoisDone++; p.GiveXp(0);
            if (r.Chance(M.A.PoiFragChance)) { var ids = M.BlueprintIds; int bi = r.Int(0, ids.Length - 1); string id = ids[bi]; if (!p.Bp.Contains(id)) { p.Frag[id] = p.Frag.GetValueOrDefault(id) + 1; if (p.Frag[id] >= M.BlueprintNeed[bi]) p.Bp.Add(id); } }
            if (r.Chance(M.A.PoiRelicChance)) { var perm = M.Relics.Where(x => x.Permanent && !p.RelicsOwned.Contains(x.Id)).ToArray(); if (perm.Length > 0) p.GiveRelic(perm[r.Int(0, perm.Length - 1)].Id); }
            p.Inv[It.Relic] += r.Chance(.5f) ? 1 : 0;
            t += 1;
        }
        _ = t;
    }

    // ───────────────────────── боссы ─────────────────────────
    float BossRisk(BossM b)
    {
        float dps = EstDps(p, pr) * (b.NeedsLight ? (p.E(Fxk.Arc) > 0 ? .75f : .45f) : 1f);
        float ttk = b.Hp * T.At(T.BossHp, b.Id - 1) / Math.Max(.5f, dps);
        float incoming = b.MeleeDmg / b.MeleePeriod, ws = 0, ds = 0, ts = 0;
        foreach (var m in b.Moves) { if (m.Cooldown > 0) { incoming += m.Dmg / m.Cooldown * .6f; continue; } float w = m.W1; ws += w; ds += w * (m.Dmg + m.Ring * .5f); ts += w * (m.Windup + m.Gap + m.Post); }
        if (ts > 0) incoming += ds / ts;
        incoming *= 1f - Math.Min(.9f, pr.DodgeSkill * .8f);
        incoming *= T.At(T.BossDamage, b.Id - 1);
        float pool = p.MaxHp + T.RegenRate * ttk * .5f + (p.Has(Tech.Reaper) ? 0 : 0);
        return incoming * ttk / pool;
    }

    bool Ready(BossM b)
    {
        if (pr.SimulatedReadiness)
        {
            int win = 0, n = 8; var pc = p.Copy();
            for (int i = 0; i < n; i++) { var q = pc.Copy(); q.Hp = q.MaxHp; var cs = new CombatSim(q, pr, new Rng(r.Next()), b.Region, false); cs.Foes.Add(cs.MakeBoss(b)); var st = cs.Run(200f); if (!st.Died && !cs.AnyAlive) win++; }
            return win >= 5;
        }
        return BossRisk(b) <= pr.BossRisk;
    }

    bool FightBoss(BossM b)
    {
        res.BossAttempts[b.Id - 1]++; Rest();
        var cs = new CombatSim(p, pr, r, b.Region, false); cs.Foes.Add(cs.MakeBoss(b));
        while (cs.AnyAlive && !cs.S.Died && !Over) { cs.Step(); res.TFight += cs.Dt; StageClock(cs.Dt); }
        if (cs.S.Died) { Die(cs.S.KilledBy ?? b.Kind, true, b.Region, b.Id); return false; }
        if (cs.AnyAlive) return false;
        streak = 0; BossRewards(b.Id); return true;
    }

    void BossRewards(int id)
    {
        p.BossMask |= 1 << (id - 1); p.GiveXp(150 + 50 * id);
        string wid = id == 1 ? "sword_u" : id == 2 ? "bow_u" : id == 3 ? "staff_u" : id == 4 ? "staff_w" : null;
        if (wid != null) { int wi = Array.FindIndex(M.Weapons, w => w.Id == wid); if (wi >= 0) p.GiveWeapon(wi); }
        string rel = id == 1 ? "r_dawnprism" : id == 2 ? "r_winterheart" : id == 3 ? "r_emberheart" : id == 4 ? "r_prismcore" : null; if (rel != null) p.GiveRelic(rel);
        if (id < 5) { p.Inv[It.Core]++; p.Inv[It.Shard] += 6; p.Inv[It.Relic] += 2; }
        if (id == 1) { p.Sk[(int)Tech.Pulse] = 1; p.Inv[It.Aether] += 3; }
        if (id == 2) { p.Sk[(int)Tech.Beam] = 1; p.Inv[It.Crystal] += 4; p.Inv[It.Silver] += 3; }
        if (id == 3) { p.Sk[(int)Tech.Fireproof] = 1; p.Inv[It.Ember] += 4; p.Inv[It.Obsidian] += 3; }
        if (id == 4) { p.Sk[(int)Tech.Prism] = 1; p.Inv[It.Frost] += 6; p.Inv[It.Crystal] += 6; }
        p.Recompute(); EquipBest();
        Mark(id == 1 ? Ms.Boss1 : id == 2 ? Ms.Boss2 : id == 3 ? Ms.Boss3 : id == 4 ? Ms.Boss4 : Ms.Win);
    }

    // ───────────────────────── прохождение ─────────────────────────
    public RunResult Run()
    {
        try { Play(); }
        catch (StuckException e) { res.Out = Outcome.Stuck; res.StuckReason = e.Message; }
        res.Hours = (float)(p.Time / 3600.0); res.Level = p.Level; res.Kills = p.Kills; res.Tier = p.Tier; res.Beacon = p.Beacon;
        if (res.Out != Outcome.Won && Over) res.Out = Outcome.Timeout;
        res.SkillLv = (int[])p.Sk.Clone(); res.EndInv = (int[])p.Inv.Clone(); res.Weapon = p.Cur != null ? p.Cur.Id : "none";
        res.ModsOn = p.CurW >= 0 ? p.Owned[p.CurW].Mods.ToArray() : Array.Empty<int>(); res.Relics = p.RelicsEq.ToArray(); res.Gathered = (int[])res.Gathered.Clone();
        return res;
    }

    sealed class StuckException : Exception { public StuckException(string m) : base(m) { } }

    void Play()
    {
        Bootstrap();
        for (int reg = 0; reg < 5 && !Over; reg++)
        {
            region = reg;
            if (reg > 0) { if (p.Beacon < reg + 1) throw new StuckException("beacon not upgraded for region " + reg); }
            Spend();
            int wanted = Math.Min(pr.LampsWanted[reg], reg == 0 ? 5 : 3);
            for (int k = 0; k < wanted && !Over; k++) { if (!ClearLamp(reg, k)) break; if (k == 0 || k == wanted - 1) Spend(); }
            if (reg == 0) Mark(Ms.Lamps);
            Explore(pr.ExploreShare * 12f * (reg == 0 ? 1f : .7f), reg); Spend();
            var boss = M.Bosses[reg]; int tries = 0;
            while (!Over)
            {
                if (!Ready(boss)) { Grind(reg); Spend(); if (Over) break; if (++tries > 40) throw new StuckException($"cannot reach readiness for boss {boss.Id}"); continue; }
                if (FightBoss(boss)) break;
                if (streak >= pr.PanicDeaths) { Grind(reg); Spend(); streak = 0; }
                if (res.BossAttempts[boss.Id - 1] > 60) throw new StuckException($"boss {boss.Id}: 60 failed attempts");
            }
            if (Over) break;
            if (reg == 4) { res.Out = Outcome.Won; return; }
            if (pr.LightBossLamps && reg < 4) { p.Lit[reg]++; p.GiveXp(120); }
            p.Beacon++; p.Inv[It.Core]--; p.GiveXp(80);
            Mark(reg == 0 ? Ms.Beacon2 : reg == 1 ? Ms.Beacon3 : reg == 2 ? Ms.Beacon4 : Ms.Beacon5);
            Hourly();
        }
    }

    void Grind(int reg)
    {
        // охота ради опыта и осколков в регионе; длительность растёт с числом неудач
        float minutes = 6f + 3f * streak + 2f * res.BossAttempts[Math.Min(4, reg)];
        Work(minutes * 60f, reg, ref res.TFight, true);
    }

    void Bootstrap()
    {
        region = 0;
        Need(M.CostFurnace); p.Pay(M.CostFurnace); p.HasFurnace = true; p.GiveXp(20); Mark(Ms.Furnace);
        if (pr.UsesAxe) { var ac = new[] { new[] { It.Wood, 5 }, new[] { It.Stone, 3 } }; Need(ac); p.Pay(ac); p.HasAxe = true; }
        Need(M.CostLantern); p.Pay(M.CostLantern); p.HasLantern = true;
        var cs = new[] { new[] { It.Copper, 4 }, new[] { It.Wood, 3 } }; var sword = Array.FindIndex(M.Weapons, w => w.Id == "sword1");
        Need(M.Weapons[sword].Cost); p.Pay(M.Weapons[sword].Cost); p.GiveWeapon(sword); p.GiveXp(25); Mark(Ms.Weapon);
        _ = cs;
        var pc = M.CostPick[0]; Need(pc); p.Pay(pc); p.Tier = 1; p.GiveXp(60); Mark(Ms.Tier1);
        // кузница: нужен спасённый житель (лагерь-место) — в симуляторе после первых мест
        Explore(4f, 0);
        var fc = new[] { new[] { It.Stone, 15 }, new[] { It.Copper, 6 }, new[] { It.Iron, 3 } };
        if (p.Tier >= 1 && Need(fc)) { p.Pay(fc); p.HasForge = true; p.GiveXp(40); }
        p.Recompute(); Hourly();
    }
}
