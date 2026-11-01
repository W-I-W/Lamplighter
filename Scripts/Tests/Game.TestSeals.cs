using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Печати между регионами (LAMP_STAGE=seals): пока печати закрыты, из леса нельзя попасть ни в один следующий регион, обходных щелей у краёв прохода нет.</summary>
public partial class Game
{
    void BuildSealsScript()
    {
        int fails = 0; void Check(bool ok, string what) { if (!ok) fails++; GD.Print($"[check] {(ok ? "PASS" : "FAIL")} {what}"); }
        S(.3f, () =>
        {
            Menu = Mn.None;
            var seals = new[] { W.Seal1, W.Seal2, W.Seal3, W.Seal4 }; int[] bound = { World.MX0, World.AX0, World.IX0, World.HX0 };
            for (int stage = 0; stage < 4; stage++)
            {
                GD.Print($"[seals] seal {stage + 1} has {seals[stage].Count} tiles (14 or 28 as designed)");
                if (stage > 0) foreach (var t in seals[stage - 1]) W.Set(t.X, t.Y, Tl.None);              // предыдущие печати уже открыты
                var seen = new HashSet<int>(); var parent = new Dictionary<int, int>(); var q = new Queue<Vector2I>(); q.Enqueue(W.Spawn); seen.Add(World.Key(W.Spawn.X, W.Spawn.Y));
                Vector2I leak = new(-1, -1); int maxX = 0;
                while (q.Count > 0)
                {
                    var c = q.Dequeue(); maxX = Math.Max(maxX, c.X); if (c.X >= bound[stage] && leak.X < 0) leak = c;
                    foreach (var d in new[] { new Vector2I(1, 0), new Vector2I(-1, 0), new Vector2I(0, 1), new Vector2I(0, -1) })
                    {
                        var n = c + d; if (!World.In(n.X, n.Y)) continue; int k = World.Key(n.X, n.Y);
                        if (seen.Contains(k) || World.Blocks(W.Get(n.X, n.Y))) continue; seen.Add(k); parent[k] = World.Key(c.X, c.Y); q.Enqueue(n);
                    }
                }
                if (leak.X >= 0) { var sb = new System.Text.StringBuilder(); int k = World.Key(leak.X, leak.Y); for (int i = 0; i < 40 && parent.ContainsKey(k); i++) { sb.Append($"({k % World.Wd},{k / World.Wd}) "); k = parent[k]; } GD.Print($"[seals] stage {stage} leak path: " + sb); }
                if (leak.X >= 0 && stage == 99) for (int y = 40; y <= 90; y++) { var row = new System.Text.StringBuilder($"{y,3} "); for (int x = 480; x <= 520; x++) row.Append(W.Get(x, y) == Tl.Seal ? 'S' : World.Blocks(W.Get(x, y)) ? '#' : seen.Contains(World.Key(x, y)) ? 'o' : '.'); GD.Print("[map] " + row); }
                Check(leak.X < 0, $"seals: stage {stage}: the player cannot get past the seal (reachable up to x={maxX}, border {bound[stage]})");
            }
            GD.Print("[check] SUMMARY failures=" + fails); GetTree().Quit();
        });
    }
}
