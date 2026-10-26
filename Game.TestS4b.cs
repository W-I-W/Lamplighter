using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

public partial class Game
{
    Poi FirstPoi(PK k, int region = -1) => W.Pois.Find(p => p.Kind == k && (region < 0 || World.RegionOf(p.Tile.X) == region));

    void KillAllEv(GEvent ev) { foreach (var e in new List<Enemy>(Enemies)) if (e.Ev == ev) { e.Hp = 0; } }

    void BuildS4bScript()
    {
        var spawn = TileCenter(W.Spawn.X, W.Spawn.Y);
        GEvent evx = null;
        S(.3f, () =>
        {
            for (int i = 0; i < Inv.Length; i++) Inv[i] = 80; Inv[It.Coins] = 1000; Inv[It.Core] = 0;
            Tp(spawn + new Vector2(0, 40)); HasFurnace = true;
            int[] cnt = new int[16]; foreach (var p in W.Pois) cnt[(int)p.Kind]++;
            Log($"pois={W.Pois.Count} techCache={cnt[(int)PK.TechCache]} spire={cnt[(int)PK.Spire]} altar={cnt[(int)PK.Altar]} secret={cnt[(int)PK.Secret]} relics={Db.Relics.Count} synergies={Db.Synergies.Count}");
            var starts = new[] { W.Spawn, new Vector2I(World.MX0 + 8, 64), new Vector2I(World.AX0 + 8, 64) }; int bad = 0, tot = 0;
            for (int reg = 0; reg < 3; reg++)
            {
                var rch = W.Reach(starts[reg]);
                foreach (var p in W.Pois)
                {
                    if (World.RegionOf(p.Tile.X) != reg || !(p.Kind == PK.TechCache || p.Kind == PK.Spire || p.Kind == PK.Altar || p.Kind == PK.Secret || (p.Kind == PK.Chest && p.Variant >= 3))) continue;
                    tot++; bool any = false; foreach (var d in World.D4) { var n = p.Tile + d; if (rch[World.Key(n.X, n.Y)] || (p.Kind == PK.Altar && false)) any = true; }
                    if (!any) { bad++; Log($"UNREACHABLE {p.Kind} v{p.Variant} at {p.Tile}"); }
                }
            }
            Log($"new finds reachable: {tot - bad}/{tot}");
        });
        // ── забытая технология, шпиль, тайник ──
        S(.3f, () =>
        {
            var tc = FirstPoi(PK.TechCache, 0); Tp(tc.Pos + new Vector2(0, 24)); Cur = FindIx(); Log("tc ix: " + string.Join(" | ", Cur.Lines)); Cur.E?.Invoke();
            Log($"after cache: bp_hub={BpDone.Contains("bp_hub")} frag refinery={Frag.GetValueOrDefault("bp_refinery")} state={tc.State}");
            var sp = FirstPoi(PK.Spire, 0); Tp(sp.Pos + new Vector2(0, 24)); int a0 = Inv[It.AncCrystal]; Cur = FindIx(); Cur.E?.Invoke(); Log($"spire: anc crystal +{Inv[It.AncCrystal] - a0}");
        });
        S(.3f, () =>
        {
            var sc = FirstPoi(PK.Secret, 0); Tp(sc.Pos + new Vector2(0, 30));
            Log($"secret hidden: tile={W.Get(sc.Tile.X, sc.Tile.Y)} visible-in-tilemap=false");
            Beacons.Add(new Beacon { Pos = PPos + new Vector2(0, -30), Dur = 5, R = 72, Burn = 0 });
        });
        S(.4f, () =>
        {
            var sc = FirstPoi(PK.Secret, 0); Log($"secret revealed by flare: tile={W.Get(sc.Tile.X, sc.Tile.Y)}");
            Cur = FindIx(); Cur.E?.Invoke(); Log($"secret chest opened: relics={string.Join(",", OwnedRelics)}");
            Shot("s4b_1_secret");
        });
        // ── алтарь лучей ──
        S(.3f, () =>
        {
            var alt = FirstPoi(PK.Altar, 1); var a = alt.Tile; Tp(TileCenter(a.X, a.Y + 5));
            var chest = W.Pois.Find(p => p.Kind == PK.Chest && p.Variant == 7 && p.Pos.DistanceTo(alt.Pos) < 5 * TS);
            Cur = FindIx(); Log("chest locked: " + string.Join(" | ", Cur.Lines));
            foreach (var (o, d) in new[] { (new Vector2I(-8, 0), 0), (new Vector2I(8, 0), 2), (new Vector2I(0, -8), 1) })
            { var lp = a + o; SetObj(lp.X, lp.Y, Tl.CrystalLantern); Lamps.Add(new Lamp { X = lp.X, Y = lp.Y, Kind = 2, Dir = d, Anim = 1, Level = 1 }); }
            puzzleT = 0; UpdateBeams(.05f); puzzleT = 0; UpdateBeams(.05f);
            Log($"altar state={alt.State} tile={W.Get(a.X, a.Y)}");
            Cur = FindIx(); Log("chest now: " + string.Join(" | ", Cur.Lines)); int c0 = Owned.Count;
            OpenChest(chest); Log($"treasury: weapon storm_u={HasWeapon("storm_u")} weapons+{Owned.Count - c0}");
        });
        S(.5f, () => Shot("s4b_2_altar"));
        // ── события ──
        S(.3f, () =>
        {
            Tp(spawn + new Vector2(0, 40)); EventsEnabled = true;
            // защита от появления внутри построек
            SetObj(W.Spawn.X + 3, W.Spawn.Y + 3, Tl.Warehouse); Buildings.Add(new Building { Type = Tl.Warehouse, X = W.Spawn.X + 3, Y = W.Spawn.Y + 3 });
            int near = 0, found = 0; var bpos = TileCenter(W.Spawn.X + 3, W.Spawn.Y + 3);
            for (int i = 0; i < 300; i++) { var p = FindSpot(2, 14, false, 1, false); if (p != null) { found++; if (p.Value.DistanceTo(bpos) < 100) near++; } }
            Log($"event spots near building: {near}/{found} (expect 0)");
            Buildings.Clear(); SetObj(W.Spawn.X + 3, W.Spawn.Y + 3, Tl.None); EventsEnabled = false;
            evx = SpawnEvent("elite", PPos + new Vector2(180, 0)); Log($"elite spawned={evx != null} enemies={Enemies.FindAll(e => e.Ev == evx).Count} elite={Enemies.Exists(e => e.Elite)}");
        });
        S(.4f, () => { Shot("s4b_3_elite"); int c0 = Inv[It.Coins]; KillAllEv(evx); for (int i = 0; i < 3; i++) UpdateEnemies(.05f); UpdateEvents(.1f); Log($"elite killed: coins+{Inv[It.Coins] - c0} events left={Events.Count} mods={string.Join(",", ModInv)}"); });
        S(.2f, () =>
        {
            Enemies.Clear(); evx = SpawnEvent("caravan", PPos + new Vector2(-170, 0));
            Log($"caravan guards={Enemies.FindAll(e => e.Ev == evx).Count} carrier={Enemies.Exists(e => e.Carrier)}");
            foreach (var e in Enemies) if (e.Carrier) e.Hp = 0; for (int i = 0; i < 3; i++) UpdateEnemies(.05f); UpdateEvents(.1f); Log($"caravan carrier killed: events left={Events.Count}");
        });
        S(.2f, () =>
        {
            Enemies.Clear(); Lamps.Clear(); var p0 = PPos + new Vector2(150, 40); evx = SpawnEvent("lamp", p0); int lamps0 = Lamps.Count;
            for (int w = 0; w < 5; w++) { evx.WaveT = 0; evx.Pos = p0; UpdateEvents(.1f); foreach (var e in new List<Enemy>(Enemies)) if (e.Ev == evx) e.Hp = 0; for (int i = 0; i < 2; i++) UpdateEnemies(.05f); }
            UpdateEvents(.1f); Log($"lamp saved: wave={evx.Wave} lamps {lamps0}->{Lamps.Count} done={evx.Done || !Events.Contains(evx)}");
        });
        S(.2f, () =>
        {
            Enemies.Clear(); evx = SpawnEvent("meteor", PPos + new Vector2(0, -160)); for (int i = 0; i < 8; i++) UpdateEvents(.5f);
            var t = ToTile(evx.Pos); Log($"meteor tile={W.Get(t.X, t.Y)} state={evx.State}"); Tp(evx.Pos + new Vector2(0, 20)); Tier = 1;
            int a0 = Inv[It.AncCrystal]; BreakTile(t, Tl.MeteorOre, false); UpdateEvents(.5f); Log($"meteor mined: anc+{Inv[It.AncCrystal] - a0} events left={Events.Count}");
        });
        S(.2f, () =>
        {
            Enemies.Clear(); BossMask |= 1; evx = SpawnEvent("rift", PPos + new Vector2(-150, 60)); Tp(evx.Pos + new Vector2(0, 20));
            Cur = FindIx(); Log("rift ix: " + string.Join(" | ", Cur.Lines)); Cur.E?.Invoke();
            Log($"trial active={TrialActive} temp relics={string.Join(",", TempRelics)} burn={E(Fxk.Burn):F1}");
        });
        S(.4f, () => Shot("s4b_4_rift"));
        S(.2f, () =>
        {
            for (int w = 0; w < 4; w++) { evx.WaveT = 0; UpdateEvents(.1f); Log($"wave {evx.Wave} enemies={Enemies.FindAll(e => e.Ev == evx).Count}"); KillAllEv(evx); for (int i = 0; i < 2; i++) UpdateEnemies(.05f); }
            UpdateEvents(.1f); Log($"trial result: active={TrialActive} temp relics={TempRelics.Count} owned permanent unchanged={OwnedRelics.Count} bossMask={BossMask}");
        });
        S(.2f, () =>
        {
            Enemies.Clear(); evx = SpawnEvent("traveler", spawn + new Vector2(150, 0)); Tp(evx.Npc + new Vector2(0, 20)); Cur = FindIx(); Cur.E?.Invoke();
            Log($"traveler escort state={evx.State}"); Tp(spawn + new Vector2(0, 30)); int v0 = Villagers.Count; for (int i = 0; i < 120; i++) { UpdateEvents(.1f); PPos = spawn + new Vector2(0, 30); }
            Log($"traveler saved: villagers {v0}->{Villagers.Count} events left={Events.Count}");
        });
        S(.2f, () =>
        {
            Enemies.Clear(); evx = SpawnEvent("trader", spawn + new Vector2(30, 30)); Tp(spawn + new Vector2(30, 50)); Cur = FindIx(); Log("trader ix: " + string.Join(" | ", Cur.Lines)); Cur.E?.Invoke();
            Log($"wanderer offers={CurOffers.Count} window={Window}");
        });
        S(.4f, () => { WinCur = 0; Shot("s4b_5_trader"); });
        // ── экономика без эксплойтов ──
        S(.2f, () =>
        {
            CloseWindows(); for (int i = 0; i < Demand.Length; i++) Demand[i] = 1f; Inv[It.Wood] = 100; Inv[It.Coins] = 0;
            float spread = BuyPrice(It.Wood) - SellPrice(It.Wood); Log($"wood buy={BuyPrice(It.Wood)} sell={SellPrice(It.Wood)} spread={spread}");
            SellItem(It.Wood, 60); Log($"sold 60 wood: coins={Inv[It.Coins]} demand={Demand[0]:F2} next sell price={SellPrice(It.Wood)}");
            int c = Inv[It.Coins]; int cost = 0; for (int i = 0; i < 60; i++) cost += BuyPrice(It.Wood);
            Log($"re-buy 60 wood would cost {cost} vs earned {c} => profit {c - cost} (must be <0)");
            foreach (int it in new[] { It.Iron, It.Silver, It.Crystal, It.Shard, It.Gear, It.Lens, It.AncCrystal, It.Obsidian }) if (BuyPrice(it) <= SellPrice(it)) Log($"ARBITRAGE on {It.Names[it]}");
            for (int i = 0; i < Demand.Length; i++) Demand[i] = 1f;
        });
        // ── лавка и сеть зданий ──
        S(.2f, () =>
        {
            Enemies.Clear(); Events.Clear(); Tp(spawn + new Vector2(0, 40)); Buildings.Clear(); Stock = new int[It.Count];
            Log($"stall unlocked={BUnlocked(8)} hub={BUnlocked(7)} refinery={BUnlocked(6)}"); UnlockBp("bp_refinery", false); UnlockBp("bp_hub", false);
            TestAim(FreeAdj(true)); BuildBuilding(0);
            Villagers.Clear(); for (int i = 0; i < 2; i++) Villagers.Add(new Villager { Pos = PPos });
            var wh = Buildings[0]; var far = TileCenter(wh.X + 30, wh.Y);      // вне радиуса склада (14 клеток)
            var mine = new Building { Type = Tl.Mine, X = wh.X + 30, Y = wh.Y }; Buildings.Add(mine); netT = 0; RecomputeNetwork(); Log($"mine at 30 tiles: linked={mine.Linked}");
            Stock[It.Wood] = 0; for (int i = 0; i < 200; i++) UpdateVillage(.2f); Log($"unlinked mine local={LocalTotal(mine)} stock stone={Stock[It.Stone]}");
            var h1 = new Building { Type = Tl.Hub, X = wh.X + 14, Y = wh.Y }; var h2 = new Building { Type = Tl.Hub, X = wh.X + 26, Y = wh.Y }; Buildings.Add(h1); Buildings.Add(h2); netT = 0; RecomputeNetwork();
            Log($"after 2 hubs: mine linked={mine.Linked} h1={h1.Linked} h2={h2.Linked}");
            mine.Mode = 1; Stock = new int[It.Count]; for (int i = 0; i < 400; i++) UpdateVillage(.2f); Log($"mine mode=Stone: stone={Stock[It.Stone]} copperOre={Stock[It.CopperOre]} (expect copperOre 0)");
            Buildings.Remove(mine);
        });
        S(.2f, () =>
        {
            var wh = Buildings[0]; Buildings.RemoveAll(b => b.Type == Tl.Hub);
            var rf = new Building { Type = Tl.Refinery, X = wh.X + 2, Y = wh.Y + 2 }; var fg = new Building { Type = Tl.Forge, X = wh.X + 3, Y = wh.Y - 2 }; Buildings.Add(rf); Buildings.Add(fg); netT = 0; RecomputeNetwork();
            Stock = new int[It.Count]; Stock[It.CopperOre] = 10; Stock[It.IronOre] = 6; Stock[It.Wood] = 30; Stock[It.Crystal] = 5; Stock[It.SilverOre] = 4;
            for (int i = 0; i < 1500; i++) UpdateVillage(.2f);
            Log($"chain: gears={Stock[It.Gear]} lenses={Stock[It.Lens]} copper={Stock[It.Copper]} iron={Stock[It.Iron]} silver={Stock[It.Silver]}");
            rf.Prio = 2; fg.Prio = 0; netT = 0; RecomputeNetwork(); Log($"order first={order[0].Type} (expect Forge)"); rf.Off = true; Stock = new int[It.Count]; Stock[It.Copper] = 10; Stock[It.Iron] = 5; for (int i = 0; i < 100; i++) UpdateVillage(.2f); Log($"paused refinery: gears={Stock[It.Gear]} (expect 0)"); rf.Off = false;
        });
        // ── свет: усилители, барьеры, фонарь героя ──
        S(.2f, () =>
        {
            Tp(spawn + new Vector2(0, 50)); BpDone.Add("bp_amp"); BpDone.Add("bp_barrier"); BpDone.Add("bp_herolamp");
            Lamps.Clear(); var lt = ToTile(PPos + new Vector2(30, 0)); SetObj(lt.X, lt.Y, Tl.Lantern); var lamp = new Lamp { X = lt.X, Y = lt.Y, Anim = 1 }; Lamps.Add(lamp);
            float r0 = LampRad(lamp); for (int i = 1; i <= 3; i++) SetObj(lt.X + i, lt.Y + 1, Tl.AmpCrystal); RecomputeLightTech(); Log($"lamp radius {r0:F0} -> {LampRad(lamp):F0} amps={lamp.Amp}");
            // барьер
            var a = ToTile(PPos + new Vector2(-60, -40)); for (int yy = a.Y - 1; yy <= a.Y + 8; yy++) for (int xx = a.X - 7; xx <= a.X + 7; xx++) SetObj(xx, yy, Tl.None); SetObj(a.X, a.Y, Tl.BarrierPost); SetObj(a.X, a.Y + 6, Tl.BarrierPost); Posts.Clear(); Posts.Add(a); Posts.Add(new Vector2I(a.X, a.Y + 6)); RecomputeLightTech();
            Log($"barriers={Barriers.Count}"); var e = TestDummy(new Vector2(-100, 0)); e.Home = null; e.Guard = null; e.Pos = TileCenter(a.X + 4, a.Y + 3); e.Age = 1; e.Hp = e.MaxHp = 999;
            PPos = TileCenter(a.X - 5, a.Y + 3); float x0 = e.Pos.X; for (int i = 0; i < 80; i++) UpdateEnemies(.05f);
            Log($"barrier holds enemy: x {x0:F0} -> {e.Pos.X:F0} (barrier x={TileCenter(a.X, a.Y).X:F0}) crossed={e.Pos.X < TileCenter(a.X, a.Y).X}");
            Enemies.Clear(); int h0 = HeroLampLv; float pr = PlayerLightR; Inv[It.Lens] = 10; Inv[It.AncCrystal] = 5; UpgradeHeroLamp(); UpgradeHeroLamp(); UpgradeHeroLamp(); Log($"hero lamp {h0}->{HeroLampLv} light {pr:F0}->{PlayerLightR:F0}");
        });
        S(.5f, () => Shot("s4b_6_light"));
        // ── окна ──
        S(.2f, () =>
        {
            Tp(spawn + new Vector2(0, 40)); HasFurnace = true; GiveWeapon("sword1", false); GiveWeapon("bow1", false); GiveRelic("r_embersp", true); GiveRelic("r_frostshard", true); ModInv[0] = 1; InstallMod(0);
            OpenWin(Win.Equip); WinTab = 0; WinCur = 1;
        });
        S(.4f, () => { Shot("s4b_7_equip_weapons"); WinTab = 1; WinCur = 0; });
        S(.4f, () => { Shot("s4b_8_equip_relics"); WinTab = 2; WinCur = 0; });
        S(.4f, () => { Shot("s4b_9_equip_craft"); WinTab = 3; });
        S(.4f, () => { Shot("s4b_10_equip_lamp"); OpenWin(Win.Journal); });
        S(.4f, () => { Shot("s4b_11_journal"); OpenWin(Win.Prod); });
        S(.4f, () => { Shot("s4b_12_prod"); OpenWin(Win.Build); });
        S(.4f, () => { Shot("s4b_13_build"); CloseWindows(); Buildings.Add(new Building { Type = Tl.Stall, X = W.Spawn.X - 3, Y = W.Spawn.Y }); OpenTrade(null); });
        S(.4f, () => { Shot("s4b_14_trade_stall"); TradeTab = 1; });
        S(.4f, () => { Shot("s4b_15_trade_sell"); CloseWindows(); Save(); Log($"saved: events={Events.Count} bought={Bought.Count} relics={OwnedRelics.Count}"); });
    }
}
