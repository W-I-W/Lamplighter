using Godot;
using System;
using System.Collections.Generic;
using System.IO;

namespace Lamplighter;

/// <summary>Отладочный автотест: LAMP_AUTOTEST=папка — скриптованный прогон; LAMP_LOAD=1 — проверка загрузки; LAMP_STAGE=ash — тест пустошей.</summary>
public partial class Game
{
    string testDir; bool testing;
    Vector2? aimOverride;
    List<(float wait, Action act)> script; int si; float sw = 1.2f;

    void Shot(string name)
    {
        var img = GetViewport().GetTexture().GetImage();
        img.SavePng(Path.Combine(testDir, name + ".png"));
        GD.Print($"[test] shot {name}");
    }

    void Tp(Vector2 p) { PPos = p; cam.ResetSmoothing(); Enemies.Clear(); Projs.Clear(); }
    void TestAim(Vector2 off) { aimOverride = Aim = PPos + off; }
    void Log(string s) => GD.Print($"[test t={Time:F1}] {s}");

    /// <summary>Свободная клетка рядом с игроком (для постройки).</summary>
    Vector2 FreeAdj(bool needLit = false)
    {
        foreach (var d in new[] { new Vector2(24, 0), new Vector2(-24, 0), new Vector2(0, 24), new Vector2(0, -24), new Vector2(24, 24), new Vector2(-24, 24), new Vector2(24, -24), new Vector2(-24, -24), new Vector2(32, 0), new Vector2(-32, 0) })
        {
            var t = ToTile(PPos + d);
            if (W.Get(t.X, t.Y) == Tl.None && W.Ground[World.Key(t.X, t.Y)] != Art.WaterG && ToTile(PPos) != t && (!needLit || IsLit(TileCenter(t.X, t.Y)))) return d;
        }
        return new Vector2(24, 0);
    }

    bool noHeal;
    void AutoTest(float dt)
    {
        if (OS.GetEnvironment("LAMP_STAGE") == "genres") { Db.SaveDefaults(); GetTree().Quit(); return; }
        if (OS.GetEnvironment("LAMP_STAGE") == "makeold") { MakeOldSave(); GetTree().Quit(); return; }
        if (OS.GetEnvironment("LAMP_STAGE") == "makev4") { BeaconLevel = 4; Save(); GetTree().Quit(); return; }
        if (!noHeal) Hp = MaxHp;
        if (OS.GetEnvironment("LAMP_LOAD") != "" && OS.GetEnvironment("LAMP_STAGE") != "reload" && OS.GetEnvironment("LAMP_STAGE") != "savesmoke" && OS.GetEnvironment("LAMP_STAGE") != "interactload" && OS.GetEnvironment("LAMP_STAGE") != "ui")
        {
            sw -= dt; if (sw > 0) return;
            if (si == 0)
            {
                Log($"LOAD: buildings={Buildings.Count} levels=[{string.Join(",", Buildings.ConvertAll(b => b.Level))}] villagers={Villagers.Count} tech={TechMask} bossMask={BossMask} beacon={BeaconLevel} lit={LitCount}/{LitIn(1)}/{LitIn(2)} done={PoisDone} puzzles={PuzzlesSolved} lamps={Lamps.Count} kinds=[{string.Join(",", Lamps.ConvertAll(l => l.Kind))}] machines={MachinesRestored} weapons=[{string.Join(",", Owned.ConvertAll(o => o.Id + ":" + o.Mods.Count))}] cur={CurW} relics={OwnedRelics.Count}/{EquippedRelics.Count} bps={BpDone.Count} frag={Frag.Count} mods=[{string.Join(",", ModInv)}] hero={HeroLampLv} epoch={TradeEpoch} bought={Bought.Count} extraV={ExtraVillagers} found={Found.Count} syn={ActiveSyn.Count} sealOpen={W.Get(125, 64) == Tl.None} seal3={W.Get(W.Seal3[0].X, W.Seal3[0].Y) == Tl.None} seal4={W.Get(W.Seal4[0].X, W.Seal4[0].Y) == Tl.None} expl={Array.FindAll(Expl, b => b != 0).Length} inv=[{string.Join(",", Inv)}] tier={Tier} lvl={Level}");
                Shot("load"); si++; sw = .8f;
            }
            else GetTree().Quit();
            return;
        }
        if (script == null) { script = new(); BuildScript(); }
        sw -= dt; if (sw > 0) return;
        if (si >= script.Count) { foreach (var m in Loc.Missing) GD.Print("[loc-missing] " + m.Replace((char)10, (char)126)); GD.Print("[test] SCRIPT DONE"); Save(); GetTree().Quit(); return; }
        var s = script[si++]; try { s.act(); } catch (Exception e) { GD.Print("[test] EXCEPTION: " + e); }
        sw = s.wait;
    }

    /// <summary>Создаёт сохранение формата этапа 2 (лес 128x128), чтобы проверить миграцию.</summary>
    void MakeOldSave()
    {
        var old = new byte[World.N * World.N];
        for (int y = 0; y < World.N; y++) for (int x = 0; x < World.N; x++) old[y * World.N + x] = W.Obj[y * World.Wd + x];
        var d = new SaveData
        {
            Version = 2, Seed = W.Seed, Obj = Convert.ToBase64String(old), Lit = new bool[7], Px = 1000, Py = 1000, Hp = 90, MaxHp = 90, Xp = 10, Level = 7, Tier = 2, Tech = 0b1111, Kills = 30, Boss = true,
            Inv = new int[9] { 50, 40, 3, 2, 9, 7, 6, 1, 2 }, Stock = new int[9], BT = new int[] { (int)Tl.Warehouse }, BX = new int[] { 66 }, BY = new int[] { 66 }, BL = new int[] { 2 },
            PoiState = new byte[35]
        };
        d.Lit[1] = true; d.Lit[2] = true; d.Lit[6] = true; for (int i = 0; i < 6; i++) d.PoiState[i] = 3 | 16;
        W.Obj[0] = W.Obj[0];
        System.IO.File.WriteAllText(SavePath, System.Text.Json.JsonSerializer.Serialize(d, new System.Text.Json.JsonSerializerOptions { IncludeFields = true }));
        Log("old-format save written");
    }

    void S(float wait, Action a) => script.Add((wait, a));

    void BuildScript()
    {
        if (OS.GetEnvironment("LAMP_STAGE") == "ash") { BuildAshScript(); return; }
        if (OS.GetEnvironment("LAMP_STAGE") == "s4") { BuildS4Script(); return; }
        if (OS.GetEnvironment("LAMP_STAGE") == "s4b") { BuildS4bScript(); return; }
        if (OS.GetEnvironment("LAMP_STAGE") == "perf") { BuildPerfScript(); return; }
        if (OS.GetEnvironment("LAMP_STAGE") == "world") { BuildWorldScript(); return; }
        if (OS.GetEnvironment("LAMP_STAGE") == "ice") { BuildIceScript(); return; }
        if (OS.GetEnvironment("LAMP_STAGE") == "heart") { BuildHeartScript(); return; }
        if (OS.GetEnvironment("LAMP_STAGE") == "end") { BuildEndScript(); return; }
        if (OS.GetEnvironment("LAMP_STAGE") == "menu") { BuildMenuScript(); return; }
        if (OS.GetEnvironment("LAMP_STAGE") == "loc") { BuildLocScript(); return; }
        if (OS.GetEnvironment("LAMP_STAGE") == "econ") { BuildEconScript(); return; }
        if (OS.GetEnvironment("LAMP_STAGE") == "ui") { BuildUiScript(); return; }
        if (OS.GetEnvironment("LAMP_STAGE") == "fx") { BuildFxScript(); return; }
        if (OS.GetEnvironment("LAMP_STAGE") == "veg") { BuildVegScript(); return; }
        if (OS.GetEnvironment("LAMP_STAGE") == "items") { BuildItemsScript(); return; }
        if (OS.GetEnvironment("LAMP_STAGE") == "trees") { BuildTreesScript(); return; }
        if (OS.GetEnvironment("LAMP_STAGE") == "upgrades") { BuildUpgradesScript(); return; }
        if (OS.GetEnvironment("LAMP_STAGE") == "interact") { BuildInteractScript(); return; }
        if (OS.GetEnvironment("LAMP_STAGE") == "interactload") { BuildInteractLoadScript(); return; }
        if (OS.GetEnvironment("LAMP_STAGE") == "texcheck") { BuildTexCheckScript(); return; }
        if (OS.GetEnvironment("LAMP_STAGE") == "soak") { BuildSoakScript(); return; }
        if (OS.GetEnvironment("LAMP_STAGE") == "combat") { BuildCombatScript(); return; }
        if (OS.GetEnvironment("LAMP_STAGE") == "mksave") { BuildMkSaveScript(); return; }
        if (OS.GetEnvironment("LAMP_STAGE") == "savesmoke") { BuildSaveSmokeScript(); return; }
        if (OS.GetEnvironment("LAMP_STAGE") == "final") { BuildFinalScript(); return; }
        if (OS.GetEnvironment("LAMP_STAGE") == "reload") { BuildReloadScript(); return; }
        var spawn = TileCenter(W.Spawn.X, W.Spawn.Y);
        var ent = new Vector2I(World.MX0 + 8, 64);
        Puzzle pz0 = null;
        S(.3f, () => Shot("1_start"));
        S(.1f, () =>
        {
            var reach = W.Reach(ent); int ok = 0, bad = 0;
            foreach (var a in W.Ancients) if (a.Region == 1) { var tt = a.Tile + new Vector2I(0, a.Boss ? 3 : 2); if (reach[World.Key(tt.X, tt.Y)]) ok++; else bad++; }
            Log($"mines ancients reachable={ok} unreachable={bad}; pois={W.Pois.Count} puzzles={W.Puzzles.Count} emitters={W.Emitters.Count}");
            var fr = W.Reach(W.Spawn); int fok = 0, fbad = 0;
            foreach (var a in W.Ancients) if (a.Region == 0 && a.Tile.Y != 64) { var tt = a.Tile + new Vector2I(0, a.Boss ? 3 : 2); if (fr[World.Key(tt.X, tt.Y)]) fok++; else fbad++; }
            Log($"forest ancients reachable={fok} unreachable={fbad}; gate front reachable={fr[World.Key(120, 64)]}");
            int badPoi = 0;
            foreach (var p in W.Pois)
            {
                if (World.RegionOf(p.Tile.X) != 1) continue;
                bool any = false; foreach (var d in World.D4) { var n = p.Tile + d; if (reach[World.Key(n.X, n.Y)]) any = true; }
                if (!any && p.Variant == 0 && p.Kind != PK.Puzzle && p.Kind != PK.BridgeSite) badPoi++;
            }
            Log($"mines pois (outside prefabs) unreachable={badPoi}");
        });
        // ── ядро, маяк, печать ──
        S(.5f, () =>
        {
            BossMask |= 1; Inv[It.Core] = 2; Inv[It.Wood] = 200; Inv[It.Stone] = 200; Inv[It.Copper] = 40; Inv[It.Iron] = 60; Inv[It.Shard] = 60; Inv[It.Silver] = 40; Inv[It.Crystal] = 40; Inv[It.Relic] = 8; Inv[It.Aether] = 10; Inv[It.Obsidian] = 10; Inv[It.Ember] = 10;
            Tp(W.Ancients[0].Pos + new Vector2(0, 30));
        });
        S(.4f, () => { Cur = FindIx(); Log("beacon ix: " + string.Join(" | ", Cur.Lines)); Cur.E?.Invoke(); Log($"beacon level={BeaconLevel} sealOpen={W.Get(125, 64) == Tl.None} core={Inv[It.Core]}"); });
        S(2.5f, () => Shot("2_beacon_up"));
        // ── вход в шахты ──
        S(1.8f, () => { Tp(TileCenter(ent.X, ent.Y)); });
        S(1.5f, () => Shot("3_mines_entry"));
        S(.2f, () =>
        {
            Vector2? wtr = null;
            for (int y = 40; y < 90 && wtr == null; y++) for (int x = 135; x < 200; x++) if (W.Ground[World.Key(x, y)] == Art.WaterG && W.Get(x, y) == Tl.None) { wtr = TileCenter(x, y); break; }
            if (wtr != null) { Tp(wtr.Value); Log($"in water={WaterAt(PPos)} lightR={PlayerLightR:F0}"); }
            else Log("no water found");
        });
        S(.6f, () =>
        {
            SpawnEnemy(PPos + new Vector2(30, -10), EK.Eel).Age = 1; SpawnEnemy(PPos + new Vector2(-30, 10), EK.Leech).Age = 1;
            SpawnEnemy(PPos + new Vector2(0, 40), EK.Beetle).Age = 1;
        });
        S(1.4f, () => { Shot("4_mines_enemies"); Log($"enemies={Enemies.Count} hidden={Enemies.FindAll(e => e.Hidden).Count} leechT={LeechT:F1}"); });
        S(.1f, () =>
        {
            var bt = Enemies.Find(e => e.Kind == EK.Beetle); if (bt == null) { Log("beetle gone"); return; }
            float hp0 = bt.Hp; bt.Face = (PPos - bt.Pos).Normalized(); Hurt(bt, 4, (bt.Pos - PPos).Normalized(), true); float front = hp0 - bt.Hp;
            bt.Face = -(PPos - bt.Pos).Normalized(); hp0 = bt.Hp; Hurt(bt, 4, (bt.Pos - PPos).Normalized(), true); float back = hp0 - bt.Hp;
            Log($"beetle dmg front={front:F2} back={back:F2}");
        });
        // ── загадка света ──
        S(.1f, () =>
        {
            Enemies.Clear(); pz0 = W.Puzzles[0]; Tp(TileCenter(pz0.Mirrors[0].X, pz0.Mirrors[0].Y) + new Vector2(0, 20));
            Log($"puzzle0 state={pz0.Poi.State} receivers={pz0.Receivers.Count} mirrors={pz0.Mirrors.Count} doors={pz0.Doors.Count}");
        });
        S(.5f, () =>
        {
            Shot("5_puzzle_before");
            int n = pz0.Mirrors.Count; bool solved = false;
            for (int mask = 0; mask < (1 << n) && !solved; mask++)
            {
                for (int i = 0; i < n; i++) SetObj(pz0.Mirrors[i].X, pz0.Mirrors[i].Y, ((mask >> i) & 1) == 1 ? Tl.Mirror2 : Tl.Mirror1);
                puzzleT = 0; UpdateBeams(.016f);
                if (pz0.Poi.State == 3) { solved = true; Log($"puzzle solved with mask={mask}"); }
            }
            if (!solved) Log("PUZZLE UNSOLVABLE");
        });
        S(.8f, () =>
        {
            Shot("6_puzzle_solved");
            int opened = 0; foreach (var d in pz0.Doors) if (W.Get(d.X, d.Y) == Tl.None) opened++;
            Log($"doors opened={opened}/{pz0.Doors.Count}");
            foreach (var p in W.Pois) if (p.Kind == PK.Chest && p.Variant == 1 && p.Pos.DistanceTo(TileCenter(pz0.Receivers[0].X, pz0.Receivers[0].Y)) < 12 * TS && W.Get(p.Tile.X, p.Tile.Y) == Tl.Chest) { int c0 = Inv[It.Crystal]; OpenChest(p); Log($"vault chest loot crystal+{Inv[It.Crystal] - c0}"); break; }
        });
        // ── технологии света ──
        S(.2f, () =>
        {
            TechPage = 1;
            W.Ancients[7].Lit = true; W.Ancients[7].Anim = 1;
            for (int i = 0; i < 7; i++) BuyTech(i);
            Log($"mines techs bought: mask={Convert.ToString(TechMask, 2)} optics={Has(Tech.Optics)} combat={Has(Tech.CombatLamp)}");
        });
        S(.4f, () =>
        {
            Tp(W.Ancients[7].Pos + new Vector2(0, 40));
            TestAim(FreeAdj(true)); BuildLamp(2); Log($"crystal lamp built: lamps={Lamps.Count}");
        });
        S(.3f, () =>
        {
            var l = Lamps[Lamps.Count - 1]; l.Anim = 1; l.Dir = 0;
            SpawnEnemy(l.Pos + new Vector2(60, 0), EK.Shade).Age = 1; Enemies[0].Hp = 50;
            TestAim(FreeAdj(true)); BuildLamp(1);
            TestAim(new Vector2(0, 24)); BuildMirror();
        });
        S(1.2f, () => { Shot("7_lanterns_beam"); Log($"beams={Beams.Count} enemyHp={(Enemies.Count > 0 ? Enemies[0].Hp : -1):F1}"); });
        S(.2f, () => { var l = Lamps.Find(x => x.Kind == 2); int lv = l.Level; Inv[It.Copper] += 10; Inv[It.Shard] += 10; UpgradeLamp(l); UpgradeLamp(l); Log($"lamp level {lv}->{l.Level} rad={LampRad(l):F0}"); });
        // ── мост, механизм, завал ──
        S(.2f, () =>
        {
            var bs = W.Pois.Find(p => p.Kind == PK.BridgeSite); Tp(bs.Pos + new Vector2(-40, 0)); aimOverride = null;
            var reach = W.Reach(new Vector2I(bs.Tile.X - 3, bs.Tile.Y)); int cs = 0;
            foreach (var p in W.Pois) if (p.Kind == PK.Chest && p.Pos.DistanceTo(bs.Pos) < 10 * TS && reach[World.Key(p.Tile.X, p.Tile.Y + 1)]) cs++;
            Log($"bridge: chest reachable before repair={cs}");
            RepairBridge(bs.Tile.X, bs.Tile.Y);
            reach = W.Reach(new Vector2I(bs.Tile.X - 3, bs.Tile.Y)); cs = 0;
            foreach (var p in W.Pois) if (p.Kind == PK.Chest && p.Pos.DistanceTo(bs.Pos) < 10 * TS && (reach[World.Key(p.Tile.X, p.Tile.Y + 1)] || reach[World.Key(p.Tile.X - 1, p.Tile.Y)] || reach[World.Key(p.Tile.X + 1, p.Tile.Y)])) cs++;
            Log($"bridge: chest reachable after repair={cs} state={bs.State}");
        });
        S(.6f, () => Shot("8_bridge"));
        S(.2f, () =>
        {
            var wk = W.Pois.Find(p => p.Kind == PK.Workshop); Tp(wk.Pos + new Vector2(0, 30));
            RestoreMachine(wk.Tile.X, wk.Tile.Y, wk); Log($"machines restored={MachinesRestored} prodMul={ProdMul:F2}");
        });
        S(.4f, () => Shot("9_workshop"));
        S(.2f, () =>
        {
            int rub = 0; Vector2I rt = default;
            for (int y = 0; y < World.Ht && rub == 0; y++) for (int x = 130; x < 250; x++) if (W.Get(x, y) == Tl.Rubble) { rub++; rt = new Vector2I(x, y); break; }
            Log($"rubble found={rub}");
            if (rub > 0) { Tp(TileCenter(rt.X, rt.Y) + new Vector2(0, 20)); BreakTile(rt, Tl.Rubble, false); Log($"rubble broken -> {W.Get(rt.X, rt.Y)}"); }
        });
        // ── кирка и жилы ──
        S(.3f, () =>
        {
            Tp(spawn + new Vector2(30, 0)); HasFurnace = true;
            Inv[It.Iron] = 20; Inv[It.Silver] = 20; Inv[It.Crystal] = 20; Tier = 2;
            var t = ToTile(PPos + new Vector2(0, 24)); SetObj(t.X, t.Y, Tl.Furnace);
            UpgradePick(); Log($"pick tier={Tier} chain={ChainLimit}");
            var o = ToTile(PPos + new Vector2(-70, 0));
            for (int i = 0; i < 6; i++) SetObj(o.X + i, o.Y, Tl.CrystalOre);
            BreakTile(o, Tl.CrystalOre, true);
            int left = 0; for (int i = 0; i < 6; i++) if (W.Get(o.X + i, o.Y) == Tl.CrystalOre) left++;
            Log($"vein chain: remaining {left}/6 (expect 2)");
        });
        // ── автобур ──
        S(.3f, () =>
        {
            var a = W.Ancients[7]; Tp(a.Pos + new Vector2(0, 40)); a.Lit = true; a.Anim = 1;
            if (!HasBuilding(Tl.Warehouse)) { TestAim(FreeAdj(true)); BuildBuilding(0); }
            TechPage = 1; for (int i = 0; i < 7; i++) BuyTech(i);
            TestAim(FreeAdj(true)); BuildBuilding(4);
            Log($"drill built={HasBuilding(Tl.Drill)}");
            Stock[It.SilverOre] = 0; Stock[It.Crystal] = 0;
            for (int i = 0; i < 400; i++) UpdateVillage(.2f);
            Log($"drill output after 80s: silverOre={Stock[It.SilverOre]} crystal={Stock[It.Crystal]}");
        });
        S(.8f, () => Shot("10_drill"));
        // ── босс глубин ──
        S(.2f, () => { var bl = BossLantern(2); Tp(bl.Pos + new Vector2(0, 120)); aimOverride = null; Log($"boss lantern at {bl.Tile} region={bl.Region}"); });
        S(2f, () => { Log($"boss alive={Boss != null} kind={Boss?.Kind} hp={Boss?.Hp}"); Shot("11_boss"); if (Boss != null) { Boss.State = 1; Boss.T2 = 1.5f; } });
        S(.8f, () => { Log($"boss hidden={Boss?.Hidden}"); Shot("12_boss_dive"); if (Boss != null) { Boss.State = 6; Boss.T2 = .1f; Boss.Hp = Boss.MaxHp * .5f; } });
        S(1.5f, () => { Log($"lightBlockT={LightBlockT:F1} playerR={PlayerLightR:F0}"); Shot("13_boss_dark"); });
        S(.2f, () => { LightBlockT = 0; if (Boss != null) { Boss.State = 4; Boss.T2 = .2f; Boss.Hp = Boss.MaxHp * .3f; } });
        S(1.0f, () => { Shot("14_boss_tide"); if (Boss != null) { Boss.State = 5; Boss.T2 = .1f; } });
        S(1.0f, () => { Log($"summons: eels={Enemies.FindAll(e => e.Kind == EK.Eel).Count} leeches={Enemies.FindAll(e => e.Kind == EK.Leech).Count}"); if (Boss != null) { Boss.State = 3; Hurt(Boss, 9999, Vector2.Right); } });
        S(.8f, () => { Log($"boss2 defeated={BossDown(2)} beam={Has(Tech.Beam)} cores={Inv[It.Core]}"); });
        S(.2f, () => { beamTest = true; TestAim(new Vector2(80, 0)); });
        S(.5f, () => { Log($"player beam: on={BeamOn} beams={Beams.Count} energy={BeamE:F0}"); Shot("15_player_beam"); beamTest = false; aimOverride = null; });
        S(.2f, () =>
        {
            var bl = BossLantern(2); Tp(bl.Pos + new Vector2(0, 30)); Cur = FindIx(); Cur.E?.Invoke(); Log($"boss lantern lit={bl.Lit}");
            Tp(W.Ancients[0].Pos + new Vector2(0, 30)); Cur = FindIx(); Cur.E?.Invoke();
            Log($"beacon level={BeaconLevel} seal2Open={W.Get(252, 64) == Tl.None}");
        });
        S(.8f, () => Shot("16_boss_lit_map"));
        S(.2f, () => { MapOpen = true; for (int i = 0; i < Expl.Length; i += 3) Expl[i] = 1; });
        S(.6f, () => { Shot("17_map"); MapOpen = false; });
        S(.2f, () => { TechOpen = true; TechPage = 1; });
        S(.5f, () => { Shot("18_tech_mines"); TechOpen = false; });
    }
}
