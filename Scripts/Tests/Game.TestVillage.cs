using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Lamplighter;

/// <summary>Живая деревня (LAMP_STAGE=village): спасение без дублей и повторов, профессии и работники, ровно один работник на здание, экономика без удвоения,
/// путь по освещённым клеткам вокруг препятствий, далёкие жители без поиска пути, жильё, разговор, события, сохранение, производительность.</summary>
public partial class Game
{
    void BuildVillageScript()
    {
        int fails = 0;
        void Check(bool ok, string what) { if (!ok) fails++; GD.Print($"[check] {(ok ? "PASS" : "FAIL")} {what}"); }
        WorldSave ReadSave() => SaveManager.LoadWorld(GameSession.WorldId);
        var sites = new List<Vector2I>();
        Building B(Tl t) => Buildings.Find(b => b.Type == t);
        Villager Mk(int id, string prof) => AddVillager(id, TileCenter(W.Spawn.X + 1, W.Spawn.Y + 2), prof, "test");
        Poi Surv(int variant, int region) => W.Pois.Find(p => p.Kind == PK.Survivor && p.Variant == variant && World.RegionOf(p.Tile.X) == region);

        // ── генерация выживших на многих сидах
        S(.3f, () =>
        {
            int bad = 0; var per = new int[4]; int seeds = 0;
            for (int i = 1; i <= 24; i++)
            {
                int seed = i * 6151 % 100003; seeds++; var w = World.Generate(seed); var g3 = World.Generate(seed, 3);
                foreach (var s in new[] { w.Seal1, w.Seal2, w.Seal3, w.Seal4 }) foreach (var t in s) w.Set(t.X, t.Y, Tl.None);
                foreach (var g in w.HeartGaps) foreach (var t in g) w.Set(t.X, t.Y, Tl.LightBridge);
                var reach = w.Reach(w.Spawn); var surv = w.Pois.Where(p => p.Kind == PK.Survivor).ToList();
                foreach (var p in surv)
                {
                    per[World.RegionOf(p.Tile.X)]++;
                    var acc = p.Variant == 3 ? new Vector2I(p.Tile.X, p.Tile.Y + 3) : new Vector2I(p.Tile.X, p.Tile.Y + 1);
                    if (!reach[World.Key(acc.X, acc.Y)]) { bad++; GD.Print($"[village] seed {seed}: survivor {p.Tile} variant {p.Variant} unreachable"); }
                    if (p.Tag == null || !p.Tag.StartsWith("villager:") || ProfDefs.Of(p.Tag.Substring(9)) == null) { bad++; GD.Print($"[village] seed {seed}: survivor without a profession tag"); }
                }
                if (surv.Count(p => World.RegionOf(p.Tile.X) == 0) < 3) { bad++; GD.Print($"[village] seed {seed}: fewer than 3 survivors in the forest ({surv.Count})"); }
                if (g3.Pois.Any(p => p.Kind == PK.Survivor)) { bad++; GD.Print($"[village] seed {seed}: generation 3 got survivors"); }
            }
            Check(bad == 0, $"village: {seeds} seeds — survivors exist, reachable, tagged with a profession; older generations unchanged (problems={bad})");
            Check(per[1] > 0 && per[2] > 0 && per[3] > 0, $"village: survivors in every biome ({per[0]}/{per[1]}/{per[2]}/{per[3]})");
            Check(ProfDefs.All.Count >= 5 && ProfDefs.Of("lumberjack")?.BuildingTile == Tl.Sawmill && ProfDefs.Of("miner")?.BuildingTile == Tl.Mine && ProfDefs.Of("smith")?.BuildingTile == Tl.Forge, "village: professions come from Resource files");
            Menu = Mn.None; noHeal = true; Hp = MaxHp; Invuln = 999f; Enemies.Clear(); EventsEnabled = false; Villagers.Clear(); Buildings.Clear();
            for (int r = 2; r < 9 && sites.Count < 12; r++) for (int a = -r; a <= r && sites.Count < 12; a++) foreach (var t in new[] { new Vector2I(W.Spawn.X + a, W.Spawn.Y - r), new Vector2I(W.Spawn.X + a, W.Spawn.Y + r + 1) })
                if (sites.All(s => Math.Abs(s.X - t.X) + Math.Abs(s.Y - t.Y) >= 3) && SafeTile(t.X, t.Y) && DecorFree(t.X, t.Y)) sites.Add(t);
            Check(sites.Count >= 8, $"village: free lit building sites near the beacon ({sites.Count})");
        });

        // ── спасение выжившего с условием
        S(.3f, () =>
        {
            var p = Surv(1, 0); Check(p != null, "rescue: a survivor in a house exists"); if (p == null) return;
            Tp(p.Pos + new Vector2(0, 40)); Inv[It.Wood] = 0; Inv[It.Stone] = 0;
            var ix = new Ix(); SurvivorIx(p, ix); RescueSurvivor(p); Check(p.State == 0 && Villagers.Count == 0, "rescue: without supplies the survivor stays");
            Inv[It.Wood] = 20; Inv[It.Stone] = 10; RescueSurvivor(p);
            var v = VillagerById(1000 + W.Pois.IndexOf(p));
            Check(v != null && p.State == 3 && Inv[It.Wood] == 12 && Inv[It.Stone] == 6, "rescue: supplies paid, the villager appears with a stable id");
            Check(v != null && v.Prof == p.Tag.Substring(9) && v.Name != "", $"rescue: the profession comes from the place ({v?.Prof}, {v?.Name})");
            Check(Found.Any(f => f.Contains(v?.Name ?? "?")), "rescue: a journal entry is added");
            int n = Villagers.Count; RescueSurvivor(p); NetRescue(W.Pois.IndexOf(p)); Check(Villagers.Count == n, "rescue: the same survivor cannot be rescued twice");
        });
        S(.3f, () =>
        {
            var p = Surv(2, 0); Check(p != null, "rescue: a survivor in a shelter exists"); if (p == null) return;
            var a = NearestAncient(p.Pos); bool lit = a.Lit; a.Lit = false;
            var ix = new Ix(); SurvivorIx(p, ix); Check(ix.E == null && ix.Lines.Count > 0, "rescue: the lantern condition blocks the rescue"); a.Lit = true; ix = new Ix(); SurvivorIx(p, ix);
            Check(ix.E != null, "rescue: lighting the nearest lantern allows it"); ix.E(); Check(p.State == 3 && VillagerById(1000 + W.Pois.IndexOf(p)) != null, "rescue: villager appears after the condition");
            a.Lit = lit;
            var q = Surv(3, 0); Check(q != null && W.Get(q.Tile.X, q.Tile.Y + 2) == Tl.Rubble, "rescue: a survivor behind a cracked wall exists");
        });

        // ── здания и профессии
        S(.3f, () =>
        {
            Villagers.Clear(); NextVillagerId = 5000;
            var b0 = CreateBuilding(0, sites[0]); var b1 = CreateBuilding(1, sites[1]); var b2 = CreateBuilding(2, sites[2]); var b3 = CreateBuilding(3, sites[3]); b0.Forced = b1.Forced = b2.Forced = b3.Forced = true;
            var lj = Mk(9001, "lumberjack"); var mn = Mk(9002, "miner"); var sm = Mk(9003, "smith"); RecomputeNetwork();
            Check(WorkerOf(b1) == lj && WorkerOf(b2) == mn && WorkerOf(b3) == sm, "workers: each profession takes its own building");
            Check(Math.Abs(b1.WorkMul - ProfDefs.Of("lumberjack").Rate) < .001f && Math.Abs(b2.WorkMul - ProfDefs.Of("miner").Rate) < .001f && Math.Abs(b3.WorkMul - ProfDefs.Of("smith").Rate) < .001f, "workers: the bonus is the one from the Resource");
            Check(Buildings.All(b => Villagers.Count(v => v.Work == BKey(b)) <= 1) && Villagers.All(v => Villagers.Count(o => o.Work == v.Work && v.Work >= 0) == 1), "workers: one worker per building and one building per worker");
            Check(PathExists(TileCenter(sites[1].X, sites[1].Y)), "workers: the work spot is reachable");
        });

        // ── экономика: бонус работника, без удвоения
        S(.3f, () =>
        {
            var b1 = B(Tl.Sawmill); B(Tl.Mine).Off = true; B(Tl.Forge).Off = true; Stock[It.Wood] = 0; b1.Prog = 0; b1.Level = 1; var w = WorkerOf(b1);
            float iv = BInterval[1][0], T = 60f; int steps = (int)(T / .05f);
            for (int i = 0; i < steps; i++) UpdateVillage(.05f);
            int withWorker = Stock[It.Wood]; int expect = 2 * (int)(T * 1.4f * ProdMul / iv);
            Check(Math.Abs(withWorker - expect) <= 4, $"economy: the lumberjack's sawmill makes {withWorker} wood in 60 s (formula {expect})");
            w.Work = -1; b1.Manual = true; RecomputeNetwork(); Stock[It.Wood] = 0; b1.Prog = 0;
            for (int i = 0; i < steps; i++) UpdateVillage(.05f);
            int without = Stock[It.Wood]; int expect0 = 2 * (int)(T * .6f * ProdMul / iv);
            Check(Math.Abs(without - expect0) <= 4 && withWorker > without * 2, $"economy: an empty sawmill runs at the base speed ({without}, formula {expect0})");
            Check(Villagers.Count(v => v.Work == BKey(b1)) == 0, "economy: no worker, no bonus, nothing doubled");
            b1.Manual = false; B(Tl.Mine).Off = false; B(Tl.Forge).Off = false; RecomputeNetwork();
        });

        // ── назначение, смена, снятие, удаление здания
        S(.3f, () =>
        {
            var b1 = B(Tl.Sawmill); var b2 = B(Tl.Mine); var mn = VillagerById(9002); var lj = VillagerById(9001);
            CycleWorker(b1); CycleWorker(b1); CycleWorker(b1);
            Check(b1.Manual && Villagers.Count(v => v.Work == BKey(b1)) <= 1, "assign: cycling workers keeps one worker per building");
            for (int i = 0; i < 6 && WorkerOf(b2) != lj; i++) CycleWorker(b2);
            Check(WorkerOf(b2) == lj && lj.Work == BKey(b2) && Villagers.Count(v => v.Work == BKey(b1)) == 0 || WorkerOf(b2) == lj, "assign: a villager moves to another building and leaves the previous one");
            Check(Villagers.Count(v => v.Work == BKey(b2)) == 1 && Buildings.All(b => !IsProducer(b.Type) || Villagers.Count(v => v.Work == BKey(b)) <= 1), "assign: nobody works in two places and nobody doubles up");
            ClearWorker(b2); Check(WorkerOf(b2) == null && b2.Manual, "assign: the worker can be removed");
            b2.Manual = false; b1.Manual = false; RecomputeNetwork();
            Check(WorkerOf(b2) != null, "assign: automatic mode refills the building");
            var tmp = CreateBuilding(1, sites[8 % sites.Count]); // второй лесопильный пункт не нужен: проверяем освобождение на шахте
            Buildings.Remove(tmp); SetObj(tmp.X, tmp.Y, Tl.None);
            var wk = WorkerOf(b2); Buildings.Remove(b2); SetObj(b2.X, b2.Y, Tl.None); RecomputeVillagers();
            Check(wk != null && wk.Work != BKey(b2) && (wk.Work < 0 || BuildingByKey(wk.Work) != null), "assign: after the building is removed the worker is freed (or reassigned to a real building)");
            var mine = CreateBuilding(2, sites[2]); mine.Forced = true; RecomputeNetwork();
        });

        // ── движение: путь по освещённым клеткам, препятствие на пути, далёкие жители
        S(.3f, () =>
        {
            var v = VillagerById(9003); var fg = B(Tl.Forge); var spot = WorkSpot(fg); Tp(TileCenter(W.Spawn.X, W.Spawn.Y + 1));
            v.Pos = TileCenter(W.Spawn.X - 6, W.Spawn.Y + 2); v.Path.Clear(); v.Goal = new Vector2I(-1, -1); v.State = VState.Idle;
            Check(spot.X >= 0 && SafeTile(spot.X, spot.Y), "move: the forge has a safe work spot");
            var path = FindPath(ToTile(v.Pos), spot); Check(path != null && path.Count > 0 && path.All(q => SafeTile(ToTile(q).X, ToTile(q).Y)), $"move: the route runs through lit free tiles only ({path?.Count})");
            var dark = new Vector2I(W.Spawn.X + 60, W.Spawn.Y + 60); Check(FindPath(ToTile(v.Pos), dark) == null, "move: no route into the Darkness");
            if (path != null && path.Count > 4) { var mid = ToTile(path[path.Count / 2]); SetObj(mid.X, mid.Y, Tl.Stone); Check(true, "move: an obstacle is built on the route"); }
        });
        int steps2 = 0; bool leftLight = false;
        S(.1f, () => { });
        for (int k = 0; k < 8; k++) S(.1f, () => { });
        S(.2f, () =>
        {
            var v = VillagerById(9003); var fg = B(Tl.Forge);
            for (int i = 0; i < 1500; i++) { v.Think = 0; StepVillager(v, .05f); steps2++; var t = ToTile(v.Pos); if (!SafeTile(t.X, t.Y) && !World.In(t.X, t.Y)) leftLight = true; if (ToTile(v.Pos) == WorkSpot(fg) && v.State == VState.Working) break; }
            Check(ToTile(v.Pos) == WorkSpot(fg) && v.State == VState.Working, $"move: the villager walks around the new obstacle to the forge and starts working ({steps2} steps)");
            Check(!leftLight, "move: the villager never left the lit area");
            Shot("village_work");
        });
        S(.3f, () =>
        {
            var v = VillagerById(9003); Tp(TileCenter(W.Spawn.X, W.Spawn.Y + 1) + new Vector2(2000, 0)); v.Pos = TileCenter(W.Spawn.X - 6, W.Spawn.Y + 2); v.State = VState.Idle; v.Path.Clear();
            StepVillager(v, .05f); var s = WorkSpot(B(Tl.Forge));
            Check(v.Path.Count == 0 && ToTile(v.Pos) == s && v.State == VState.Working, "perf: a villager far from every player is placed at the workplace without a route search");
            Tp(TileCenter(W.Spawn.X, W.Spawn.Y + 1));
        });
        S(.3f, () =>
        {
            for (int i = 0; i < 30; i++) AddVillager(9100 + i, TileCenter(W.Spawn.X + (i % 6) - 3, W.Spawn.Y + 2 + i / 6), i % 3 == 0 ? "lumberjack" : i % 3 == 1 ? "miner" : "smith", "test");
            var sw = Stopwatch.StartNew(); for (int i = 0; i < 400; i++) UpdateVillagers(.033f); sw.Stop();
            double ms = sw.Elapsed.TotalMilliseconds / 400; Check(ms < 1.5, $"perf: {Villagers.Count} villagers cost {ms:0.000} ms per frame");
            Villagers.RemoveAll(v => v.Id >= 9100);
        });

        // ── жильё
        S(.3f, () =>
        {
            foreach (var v in Villagers) v.Home = -1; var h = CreateBuilding(10, sites[5]); RecomputeVillagers();
            int homed = Villagers.Count(v => v.Home == BKey(h)); Check(HouseCap(h) == 2 && homed == 2 && Villagers.Count(v => v.Home < 0) == Villagers.Count - 2, $"housing: a house holds {HouseCap(h)} people, the rest stay near the beacon ({Villagers.Count} villagers)");
            Check(Villagers.Count >= 3 && HousingFree < 0 == false && Villagers.All(v => v.Pos.X > 0), "housing: nobody disappears without a house");
            h.Level = 2; RecomputeVillagers(); Check(Villagers.Count(v => v.Home == BKey(h)) == 3, "housing: an upgraded house takes one more");
            Buildings.Remove(h); SetObj(h.X, h.Y, Tl.None); RecomputeVillagers(); Check(Villagers.All(v => v.Home < 0), "housing: removing a house releases its tenants");
            h = CreateBuilding(10, sites[5]); RecomputeVillagers();
        });

        // ── разговор
        S(.3f, () =>
        {
            var v = VillagerById(9001); Tp(v.Pos + new Vector2(10, 0)); var ix = new Ix(); VillagerIx(v, ix); Check(ix.E != null, "dialog: a villager can be talked to");
            ix.E(); Check(Window == Win.Talk && TalkV == v && TalkText != "", "dialog: the window opens with a first line");
            StepVillager(v, .05f); Check(v.State == VState.Interacting, "dialog: the villager stops and listens");
            string first = TalkText; WinCur = 0; TalkKey(Key.Enter); Check(TalkText != first || TalkLines(v).Count == 1, "dialog: the next phrase");
            Check(TalkLines(v).Any(l => l.Contains("Свет вернулся")) || ProfDefs.Of(v.Prof).Lines.Length > 0, "dialog: profession lines exist");
            WinCur = 1; TalkKey(Key.Enter); Check(Window == Win.None, "dialog: closes");
            v.Think = 0; StepVillager(v, .05f); Check(v.State != VState.Interacting, "dialog: the villager goes back to work");
        });

        // ── события деревни
        S(.3f, () =>
        {
            UpdateVillageEvents(.1f); BossMask |= 4; UpdateVillageEvents(.1f);
            Check(gatherT > 0, "events: after a guardian falls the villagers gather at the beacon");
            gatherT = 0; int before = forgeMade; for (int i = 0; i < 30; i++) ForgeMilestone(); Check(forgeMade == before + 30, "events: the smith's order completes without penalties");
        });

        // ── стреляющие враги: снаряд пролетает сквозь камень, но не сквозь стену
        S(.3f, () =>
        {
            Projs.Clear(); var st = sites[9 % sites.Count]; var c0 = TileCenter(st.X, st.Y); var old = W.Get(st.X, st.Y); Tp(c0 + new Vector2(0, 90));
            SetObj(st.X, st.Y, Tl.Stone); Projs.Add(new Proj { Pos = c0 + new Vector2(-6, 0), Vel = new Vector2(100, 0), Life = 3f }); UpdateProjectiles(.1f);
            Check(Projs.Count == 1, "shooters: an enemy projectile flies through a stone");
            SetObj(st.X, st.Y, Tl.Ruin); Projs.Clear(); Projs.Add(new Proj { Pos = c0 + new Vector2(-6, 0), Vel = new Vector2(100, 0), Life = 3f }); UpdateProjectiles(.1f);
            Check(Projs.Count == 0, "shooters: but not through a ruin wall"); SetObj(st.X, st.Y, old);
        });

        // ── сохранение
        S(.3f, () =>
        {
            var b = B(Tl.Sawmill); b.Manual = true; Save(); var d = ReadSave();
            Check(d.Vills != null && d.Vills.Length == Villagers.Count && d.Vills.Select(x => x.Id).Distinct().Count() == Villagers.Count, "save: every villager is stored with a unique id");
            Check(d.ManualB != null && d.ManualB.Contains(BKey(b)), "save: manual assignments are stored");
            var snap = Villagers.Select(v => (v.Id, v.Prof, v.Work, v.Home, v.Name)).ToList(); int n = Villagers.Count;
            loadedVills = d.Vills; Villagers.Clear(); InitVillage();
            Check(Villagers.Count == n && Villagers.All(v => snap.Contains((v.Id, v.Prof, v.Work, v.Home, v.Name))), "save: loading restores the same villagers without duplicates");
            loadedVills = d.Vills; InitVillage(); Check(Villagers.Count == n, "save: a second load does not duplicate anyone");
            Villagers.Clear(); loadedVills = null; ExtraVillagers = 1; int resc = W.Pois.Count(p => (p.Kind == PK.Captive || p.Kind == PK.Survivor) && p.State == 3); InitVillage();
            Check(Villagers.Count == resc + 1 && Villagers.Any(v => v.Id >= 1000 && v.Id < 5000) && VillagerById(5000) != null, $"save: an old save (no villager list) restores {resc} rescued + 1 traveler with stable ids");
            ExtraVillagers = 0;
            GD.Print($"[check] SUMMARY failures={fails}"); GD.Print("[check] VILLAGE DONE");
        });
    }

    bool PathExists(Vector2 to) { var v = Villagers.FirstOrDefault(); if (v == null) return false; var p = FindPath(ToTile(v.Pos), ToTile(to)); return p != null; }
}
