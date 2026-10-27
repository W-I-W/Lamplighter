using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Экономический аудит (LAMP_STAGE=econ): торговые петли, неиспользуемые ресурсы, стоимость улучшений.</summary>
public partial class Game
{
    void BuildEconScript()
    {
        S(.2f, () =>
        {
            int fails = 0;
            for (int i = 0; i < Demand.Length; i++) Demand[i] = 1f;
            // 1) цикл «купить сырьё → переработать → продать» не должен давать прибыль (дерево для плавки ≈ 1 ед. = цена дерева)
            int Buy(int it) => BuyPrice(it); int Sell(int it) => SellPrice(it);
            var loops = new List<(string n, int cost, int income)>
            {
                ("copper ore->ingot", Buy(It.CopperOre) + Sell(It.Wood), Sell(It.Copper)),
                ("iron ore->ingot", Buy(It.IronOre) + Sell(It.Wood), Sell(It.Iron)),
                ("silver ore->ingot", Buy(It.SilverOre) + Sell(It.Wood), Sell(It.Silver)),
                ("gear", 2 * Buy(It.Copper) + Buy(It.Iron), Sell(It.Gear)),
                ("lens", Buy(It.Crystal) + Buy(It.Silver), Sell(It.Lens)),
            };
            foreach (var (n, cost, income) in loops) { bool ok = income <= cost; if (!ok) fails++; Log($"trade loop {n}: cost={cost} sell={income} -> {(ok ? "OK" : "EXPLOIT")}"); }
            // 2) ресурсы, не встречающиеся ни в одной цене
            var used = new HashSet<int>();
            void Scan(int[][] c) { if (c != null) foreach (var x in c) used.Add(x[0]); }
            foreach (var t in Techs) Scan(t.Cost);
            for (int bk = 0; bk < BCount; bk++) for (int lv = 1; lv <= 5; lv++) { try { Scan(BCost(bk, lv)); } catch { } }
            foreach (var w in Db.Weapons) Scan(w.Cost);
            foreach (var l in Legends) Scan(l.Cost);
            foreach (var c in CostPick) Scan(c); foreach (var c in LampCost) Scan(c);
            for (int i = 0; i < 3; i++) Scan(HeroCostFor(i));
            Scan(AmpCostArr); Scan(BarrierCostArr); Scan(MirrorCost); Scan(MachineCost); Scan(BridgeCost); Scan(CostFurnace); Scan(CostLantern);
            for (int it = 0; it < It.Count; it++) if (!used.Contains(it)) Log($"resource never spent in recipes: {It.Names[it]}");
            // 3) совокупная стоимость всех построек и технологий по ресурсам
            var total = new int[It.Count];
            foreach (var t in Techs) if (t.Cost != null) foreach (var x in t.Cost) total[x[0]] += x[1];
            for (int bk = 0; bk < BCount; bk++) for (int lv = 1; lv <= MaxLv(bk); lv++) foreach (var x in BCost(bk, lv)) total[x[0]] += x[1];
            var sb = new List<string>(); for (int it = 0; it < It.Count; it++) if (total[it] > 0) sb.Add($"{It.Ab[it]}={total[it]}");
            Log("total tech+building cost: " + string.Join(" ", sb));
            Log(fails == 0 ? "ECON AUDIT PASSED" : $"ECON AUDIT FAILED {fails}");
        });
    }
}
