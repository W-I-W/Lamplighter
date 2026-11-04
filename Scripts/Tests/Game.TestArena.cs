using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lamplighter;

/// <summary>Арены боссов (LAMP_STAGE=arena): на многих сидах стена замкнута, проходов 1–4, они достижимы со старта; бой начинается со сломанного тотема, ворота закрываются, при гибели всех внутри
/// всё сбрасывается, при победе ворота открываются, а сохранение не хранит ни закрытых ворот, ни сломанного тотема непобеждённого стража.</summary>
public partial class Game
{
    void BuildArenaScript()
    {
        int fails = 0; void Check(bool ok, string what) { if (!ok) fails++; GD.Print($"[check] {(ok ? "PASS" : "FAIL")} {what}"); }
        S(.3f, () =>
        {
            Menu = Mn.None; TestNoSpawn = true; MaxHp = Hp = 99999f;                  // страж бьёт по ожидающему игроку: смерть сбросила бы бой раньше проверки
            int bad = 0, seeds = 0;
            foreach (int seed in new[] { 1, 2, 3, 7, 11, 23, 42, 99, 123, 777 })
            {
                var w = World.Generate(seed); seeds++;
                if (w.Arenas.Count != 5) { bad++; GD.Print($"[arena] seed {seed}: {w.Arenas.Count} arenas"); continue; }
                foreach (var ar in w.Arenas)
                {
                    string tag = $"seed {seed} boss {ar.BossId}";
                    if (ar.Gates.Count < 1 || ar.Gates.Count > 4) { bad++; GD.Print($"[arena] {tag}: {ar.Gates.Count} gates"); }
                    if (w.Get(ar.Totem.X, ar.Totem.Y) != Tl.BossTotem) { bad++; GD.Print($"[arena] {tag}: no totem"); }
                    // закроем ворота и проверим, что изнутри наружу выйти нельзя; откроем — что со старта в арену можно попасть через проход
                    foreach (var g in ar.Gates) foreach (var t in g) w.Set(t.X, t.Y, Tl.BossGate);
                    int Flood(Vector2I from, out bool escaped)
                    {
                        var seen = new HashSet<int>(); var q = new Queue<Vector2I>(); q.Enqueue(from); seen.Add(World.Key(from.X, from.Y)); escaped = false;
                        while (q.Count > 0)
                        {
                            var c = q.Dequeue(); if (new Vector2(c.X - ar.Center.X, c.Y - ar.Center.Y).Length() > ar.Radius + 4) { escaped = true; break; }
                            foreach (var d in World.D4) { var n = c + d; if (!World.In(n.X, n.Y) || seen.Contains(World.Key(n.X, n.Y)) || World.Blocks(w.Get(n.X, n.Y))) continue; seen.Add(World.Key(n.X, n.Y)); q.Enqueue(n); }
                        }
                        return seen.Count;
                    }
                    var inside = ar.Center + new Vector2I(0, 3);
                    Flood(inside, out bool esc); if (esc) { bad++; GD.Print($"[arena] {tag}: the closed arena leaks"); }
                    foreach (var g in ar.Gates) foreach (var t in g) w.Set(t.X, t.Y, Tl.None);
                    bool reach = w.Reach(w.Spawn)[World.Key(inside.X, inside.Y)];
                    // в мире с открытыми печатями и мостами тоже должно быть достижимо: используем ту же страховку, что и генерация
                    if (!reach) { var saved = new List<(int, byte)>(); foreach (var s in new[] { w.Seal1, w.Seal2, w.Seal3, w.Seal4 }) foreach (var t in s) { saved.Add((World.Key(t.X, t.Y), w.Obj[World.Key(t.X, t.Y)])); w.Obj[World.Key(t.X, t.Y)] = (byte)Tl.None; } foreach (var gp in w.HeartGaps) foreach (var t in gp) { saved.Add((World.Key(t.X, t.Y), w.Obj[World.Key(t.X, t.Y)])); w.Obj[World.Key(t.X, t.Y)] = (byte)Tl.LightBridge; } reach = w.Reach(w.Spawn)[World.Key(inside.X, inside.Y)]; }
                    if (!reach) { bad++; GD.Print($"[arena] {tag}: the arena is not reachable from the start"); }
                }
            }
            Check(bad == 0, $"arena: {seeds} seeds, 5 arenas each: closed wall, 1–4 gates, reachable ({bad} problems)");

            // ── бой в живой игре ──
            var A = W.ArenaOf(1); Check(A != null && W.Get(A.Totem.X, A.Totem.Y) == Tl.BossTotem, "arena: the first guardian's totem stands in the world");
            Check(!Enemies.Exists(e => e.IsBoss), "arena: the guardian sleeps while the totem stands");
            Tp(TileCenter(A.Totem.X, A.Totem.Y + 2)); BossMask = 0; bossT = 0; UpdateBossSpawn(.3f);
            Check(!Enemies.Exists(e => e.IsBoss) && !ArenaIsLocked(1), "arena: standing next to the totem does not wake the guardian");
            BreakTile(A.Totem, Tl.BossTotem, false); bossT = 0; UpdateBossSpawn(.3f);
            Check(ArenaIsLocked(1) && Enemies.Exists(e => e.Kind == EK.Warden), "arena: breaking the totem wakes the guardian and locks the arena");
            Check(A.Gates.All(g => g.All(t => W.Get(t.X, t.Y) == Tl.BossGate)), "arena: every passage is sealed");
            Check(W.Obj.Length == ObjForSave().Length && A.Gates.All(g => g.All(t => ObjForSave()[World.Key(t.X, t.Y)] == (byte)Tl.None)) && ObjForSave()[World.Key(A.Totem.X, A.Totem.Y)] == (byte)Tl.BossTotem, "arena: the save keeps the gates open and the totem whole");
            var phase = Move(TileCenter(A.Gates[0][0].X, A.Gates[0][0].Y) + new Vector2(30, 0), new Vector2(-60, 0), 5f, true); Check(Collides(TileCenter(A.Gates[0][1].X, A.Gates[0][1].Y), 5f, true), "arena: a gate stops even a dash through walls");
        });
        S(4.5f, () =>
        {
            var A = W.ArenaOf(1); bossT = 0; UpdateBossSpawn(.3f);
            Check(ArenaIsLocked(1), "arena: the fight goes on while a player is inside");
            Tp(TileCenter(W.Spawn.X, W.Spawn.Y)); arenaT[1] = ArenaGrace; bossT = 0; UpdateBossSpawn(.3f);       // игрок «погиб» и возродился у маяка: внутри никого
            Check(!ArenaIsLocked(1) && !Enemies.Exists(e => e.IsBoss) && Boss == null, "arena: nobody inside — the guardian is reset");
            Check(W.Get(A.Totem.X, A.Totem.Y) == Tl.BossTotem && A.Gates.All(g => g.All(t => W.Get(t.X, t.Y) == Tl.None)), "arena: the totem is back and the passages are open after a defeat");
            Tp(TileCenter(A.Totem.X, A.Totem.Y + 2)); BreakTile(A.Totem, Tl.BossTotem, false); bossT = 0; UpdateBossSpawn(.3f);
            Check(ArenaIsLocked(1), "arena: the fight can be started again");
        });
        S(4.5f, () =>
        {
            var A = W.ArenaOf(1); var boss = Enemies.Find(e => e.Kind == EK.Warden);
            Check(boss != null, "arena: the guardian is back");
            boss.Hp = 0; Enemies.Remove(boss); KillEnemy(boss); bossT = 0; UpdateBossSpawn(.3f);
            Check(BossDown(1) && !ArenaIsLocked(1) && A.Gates.All(g => g.All(t => W.Get(t.X, t.Y) == Tl.None)), "arena: victory opens the passages");
            Check(W.Get(A.Totem.X, A.Totem.Y) == Tl.None, "arena: the totem stays broken after the victory");
            bossT = 0; UpdateBossSpawn(.3f); Check(!ArenaIsLocked(1) && !Enemies.Exists(e => e.IsBoss), "arena: a defeated guardian does not wake again");
            GD.Print("[check] SUMMARY failures=" + fails); GetTree().Quit();
        });
    }
}
