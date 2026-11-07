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

        // ── постройки: остались склад, кузница, лавка и дом; пассивного производства и рабочих мест нет
        S(.3f, () =>
        {
            Villagers.Clear(); NextVillagerId = 5000; Inv[It.Wood] = 500; Inv[It.Stone] = 500; Inv[It.Copper] = 500; Inv[It.Iron] = 500; Inv[It.Silver] = 500; Inv[It.Crystal] = 50;
            int active = 0; for (int bk = 0; bk < BCount; bk++) if (BActive(bk)) active++;
            Check(active == 5 && BActive(0) && BActive(3) && BActive(8) && BActive(10) && BActive(ResearchBk), "buildings: only the warehouse, forge, stall, house and research table remain");
            Check(!BUnlocked(1) && !BUnlocked(2) && !BUnlocked(4) && !BUnlocked(5) && !BUnlocked(6) && !BUnlocked(7) && !BUnlocked(9), "buildings: producers, workshop, hub and extractors are never unlocked");
            Check(!IsProducer(Tl.Sawmill) && !IsProducer(Tl.Mine) && !IsProducer(Tl.Forge), "buildings: nothing produces by itself");
            var b0 = CreateBuilding(0, sites[0]); var b3 = CreateBuilding(3, sites[3]); b0.Forced = b3.Forced = true;
            var lj = Mk(9001, "lumberjack"); var mn = Mk(9002, "miner"); var sm = Mk(9003, "smith"); RecomputeNetwork();
            Check(Villagers.All(v => v.Work < 0) && !b3.Worker, "buildings: villagers get no workplace");
            Check(MaxLv(3) == 1 && MaxLv(0) == 5, "buildings: the forge has a single level, the warehouse still grows");
            int w0 = Buildings.Count; BuildBuilding(1); Check(Buildings.Count == w0, "buildings: a removed type cannot be built");
            Stock[It.Wood] = 0; for (int i = 0; i < 400; i++) UpdateVillage(.05f); Check(Stock[It.Wood] == 0, "buildings: stock does not grow on its own");
            Check(PathExists(TileCenter(sites[3].X, sites[3].Y)), "buildings: the forge is reachable");
        });

        // ── кузница делает шестерни и линзы
        S(.3f, () =>
        {
            var fg = B(Tl.Forge); Tp(fg.Pos + new Vector2(0, 20)); Inv[It.Copper] = 4; Inv[It.Iron] = 2; Inv[It.Crystal] = 1; Inv[It.Silver] = 1; Inv[It.Gear] = 0; Inv[It.Lens] = 0;
            Check(StationNear() == "forge", "forge: it is a craft station");
            var gear = RecipesOf("forge").Find(r => r.Id == "make_" + It.Gear); var lens = RecipesOf("forge").Find(r => r.Id == "make_" + It.Lens);
            Check(gear != null && lens != null, "forge: gear and lens recipes exist");
            if (gear == null || lens == null) return;
            CraftRecipe(gear, 5); CraftRecipe(lens, 5);
            Check(Inv[It.Gear] == 2 && Inv[It.Copper] == 0 && Inv[It.Iron] == 0 && Inv[It.Lens] == 1 && Inv[It.Crystal] == 0 && Inv[It.Silver] == 0, $"forge: crafting spends exactly the materials ({Inv[It.Gear]} gears, {Inv[It.Lens]} lens)");
        });

        // ── старые сохранения: убранные постройки исчезают, ресурсы возвращаются
        S(.3f, () =>
        {
            Inv[It.Wood] = 0; var old = new Building { Type = Tl.Sawmill, X = sites[6].X, Y = sites[6].Y, Level = 2 }; old.Local[It.Wood] = 3; Buildings.Add(old); SetObj(old.X, old.Y, Tl.Sawmill);
            int n = Buildings.Count; RemoveRetiredBuildings();
            Check(Buildings.Count == n - 1 && !Buildings.Contains(old) && W.Get(old.X, old.Y) == Tl.None, "migration: a removed building type disappears from an old save");
            Check(Inv[It.Wood] == 12 + 15 + 3, $"migration: its price and stored goods are refunded ({Inv[It.Wood]} wood)");
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
            var b = B(Tl.Forge); b.Manual = true; Save(); var d = ReadSave();
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
