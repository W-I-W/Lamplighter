using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Аудит мира (LAMP_STAGE=world): достижимость всех древних фонарей, боссов и находок на разных сиде.</summary>
public partial class Game
{
    void BuildWorldScript()
    {
        S(.2f, () =>
        {
            int fails = 0;
            foreach (int seed in SeedList())
            {
                var w = World.Generate(seed);
                foreach (var s in new[] { w.Seal1, w.Seal2, w.Seal3, w.Seal4 }) foreach (var t in s) w.Set(t.X, t.Y, Tl.None);
                foreach (var g in w.HeartGaps) foreach (var t in g) w.Set(t.X, t.Y, Tl.LightBridge);
                var reach = w.Reach(w.Spawn);
                int ok = 0, bad = 0, bosses = 0, badPoi = 0;
                var perReg = new int[5];
                foreach (var a in w.Ancients)
                {
                    var tt = a.Tile + new Vector2I(0, a.Boss ? 3 : 2);
                    if (reach[World.Key(tt.X, tt.Y)]) { ok++; if (a.Boss) bosses++; perReg[a.Region]++; } else { bad++; GD.Print($"[world] seed {seed}: ancient {a.Tile} region {a.Region} boss={a.Boss} UNREACHABLE"); }
                }
                foreach (var p in w.Pois)
                {
                    bool any = false; foreach (var d in World.D4) { var n = p.Tile + d; if (reach[World.Key(n.X, n.Y)]) any = true; }
                    if (!any && p.Variant == 0 && p.Kind != PK.Puzzle && p.Kind != PK.BridgeSite) { badPoi++; GD.Print($"[world] seed {seed}: poi {p.Kind} {p.Tile} unreachable"); }
                }
                // пустые зоны: наибольшее расстояние от проходимой клетки до ближайшего «значимого» объекта (по регионам)
                var pts = new List<Vector2I>(); foreach (var p in w.Pois) pts.Add(p.Tile); foreach (var an in w.Ancients) pts.Add(an.Tile);
                var gap = new int[5]; var big = new int[5];
                for (int y = 8; y < World.Ht - 8; y += 4) for (int x = 8; x < World.Wd - 8; x += 4)
                {
                    if (!reach[World.Key(x, y)]) continue; int reg = World.RegionOf(x); if (reg == 4) continue;
                    int best = 999; foreach (var q in pts) { int d = Math.Max(Math.Abs(q.X - x), Math.Abs(q.Y - y)); if (d < best) best = d; }
                    gap[reg] = Math.Max(gap[reg], best); if (best > 36) big[reg]++;
                }
                // ритм первых минут: ближайший фонарь леса и ближайшее место интереса от старта
                int nearA = 999, nearP = 999, cost1 = 0;
                foreach (var an in w.Ancients) if (an != w.Ancients[0] && an.Region == 0 && !an.Boss) { int d = Math.Max(Math.Abs(an.Tile.X - w.Spawn.X), Math.Abs(an.Tile.Y - w.Spawn.Y)); if (d < nearA) { nearA = d; cost1 = an.Cost; } }
                foreach (var p in w.Pois) if (p.Kind != PK.Lore) { int d = Math.Max(Math.Abs(p.Tile.X - w.Spawn.X), Math.Abs(p.Tile.Y - w.Spawn.Y)); if (d < nearP) nearP = d; }
                { var cs = new List<string>(); var c0 = w.Ancients[0].Tile; foreach (var an in w.Ancients) if (!an.Boss && an.Cost > 0) cs.Add($"r{an.Region}:{an.Cost}@{(int)new Vector2(an.Tile.X - c0.X, an.Tile.Y - c0.Y).Length()}"); Log($"seed {seed}: lantern costs [{string.Join(" ", cs)}]");
                  foreach (var an in w.Ancients) if (!an.Boss && an.Cost > 0 && an.Cost < 3) { Log($"seed {seed}: lantern too cheap ({an.Cost})"); fails++; } }
                if (nearA > 60 || nearA < 12 || nearP > 30) { Log($"seed {seed}: EARLY PACING nearestLantern={nearA} (cost {cost1}) nearestPoi={nearP}"); fails++; }
                Log($"seed {seed}: max gap to landmark per region=[{string.Join(",", gap)}] cells>36: [{string.Join(",", big)}]");
                Log($"seed {seed}: ancients ok={ok} bad={bad} bosses={bosses}/5 pois={w.Pois.Count} badPois={badPoi} perRegion=[{string.Join(",", perReg)}] arena={w.IceArena}");
                if (bad > 0 || bosses < 5 || badPoi > 0) fails++;
            }
            // чертежи: каждый должен продаваться в лавке/у торговца либо выдаваться фиксированными находками
            BeaconLevel = 6; var shop = new List<Offer>(); var saveEp = TradeEpoch; var sl = Buildings.Count;
            Buildings.Add(new Building { Type = Tl.Stall, Level = 3 });
            for (int ep = 0; ep < 6; ep++) { TradeEpoch = ep; shop.AddRange(BuildOffers(null)); }
            TradeEpoch = saveEp; Buildings.RemoveAt(Buildings.Count - 1);
            var trader = BuildOffers(new GEvent { Kind = "trader", Seed = 5 }); BpDone.Add("bp_stall");
            foreach (var bp in Db.Bps)
            {
                bool inShop = shop.Exists(o => o.Id == bp.Id) || trader.Exists(o => o.Id == bp.Id) || BpDone.Contains(bp.Id);
                int fixedTags = 0; foreach (var p in W.Pois) if (p.Tag != null && (p.Tag == "bp:" + bp.Id || p.Tag == "frag:" + bp.Id)) fixedTags++;
                if (!inShop && fixedTags == 0) { Log($"BLUEPRINT NOT GUARANTEED: {bp.Id}"); fails++; }
            }
            Log(fails == 0 ? "WORLD AUDIT PASSED" : $"WORLD AUDIT FAILED seeds={fails}");
        });
        static int[] SeedList() { var l = new List<int>(); for (int i = 1; i <= 40; i++) l.Add(i * 7919 % 100003); return l.ToArray(); }
}
}
