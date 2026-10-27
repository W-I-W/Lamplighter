using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

public partial class Game
{
    void BuildAshScript()
    {
        var spawn = TileCenter(W.Spawn.X, W.Spawn.Y);
        var ent = new Vector2I(World.AX0 + 8, 64);
        S(.3f, () =>
        {
            BossMask |= 3; BeaconLevel = 3; ApplyBeaconRadius();
            foreach (var t in W.Seal1) SetObj(t.X, t.Y, Tl.None); foreach (var t in W.Seal2) SetObj(t.X, t.Y, Tl.None);
            for (int i = 0; i < Inv.Length; i++) Inv[i] = 60; Inv[It.Core] = 2; Tier = 3; HasFurnace = true;
            TechMask |= 0b0011_1111; TechMask |= 1 << (int)Tech.Optics | 1 << (int)Tech.Beam;
            foreach (var a in W.Ancients) if (a.Region < 2 && !a.Boss) { a.Lit = true; a.Anim = 1; }
            var reach = W.Reach(ent); int ok = 0, bad = 0;
            foreach (var a in W.Ancients) if (a.Region == 2) { var tt = a.Tile + new Vector2I(0, a.Boss ? 3 : 2); if (reach[World.Key(tt.X, tt.Y)]) ok++; else bad++; }
            Log($"ash: ancients reachable={ok} unreachable={bad} pois={W.Pois.FindAll(p => World.RegionOf(p.Tile.X) == 2).Count} vents={W.Vents.Count} puzzles={W.Puzzles.Count}");
            int badPoi = 0;
            foreach (var p in W.Pois)
            {
                if (World.RegionOf(p.Tile.X) != 2) continue;
                bool any = false; foreach (var d in World.D4) { var n = p.Tile + d; if (reach[World.Key(n.X, n.Y)]) any = true; }
                if (!any && p.Variant == 0 && p.Kind != PK.Puzzle) badPoi++;
            }
            int hot = 0; for (int y = 0; y < World.Ht; y++) for (int x = World.AX0; x <= World.AX1; x++) if (W.Ground[World.Key(x, y)] == Art.HotAsh) hot++;
            Log($"ash: pois unreachable={badPoi} hotTiles={hot} gate corridor reachable from mines={W.Reach(new Vector2I(245, 64))[World.Key(ent.X, ent.Y)]}");
            Tp(TileCenter(ent.X, ent.Y));
        });
        S(1.8f, () => Shot("a1_ash_entry"));
        S(.2f, () =>
        {
            // горячий пепел: ждём активную фазу рядом с игроком
            Vector2I? ht = null;
            for (int y = 20; y < 110 && ht == null; y++) for (int x = World.AX0 + 20; x < World.AX1 - 10; x++) if (W.Ground[World.Key(x, y)] == Art.HotAsh && W.Get(x, y) == Tl.None) { ht = new Vector2I(x, y); break; }
            Tp(TileCenter(ht.Value.X, ht.Value.Y));
            noHeal = true; Hp = MaxHp;
            float t0 = Time; while (!HotActive(t0 + 0, ht.Value.X, ht.Value.Y, out _)) t0 += .1f;
            Time = t0 + .1f;     // перематываем в активную фазу
            Log($"hot tile {ht.Value} active={HotActive(Time, ht.Value.X, ht.Value.Y, out _)}");
        });
        S(1.0f, () => { Log($"hp after standing on hot ash ~1s: {Hp:F1}/{MaxHp:F0}"); Shot("a2_hot_ash"); noHeal = false; Hp = MaxHp; TechMask &= ~(1 << (int)Tech.HeatShield); });
        S(.2f, () =>
        {
            // дым: облако на игроке
            Clouds[0].Pos = PPos; Clouds[0].R = 90;
            float lit0 = PlayerLightR; TechMask &= ~(1 << (int)Tech.Fireproof);
            Log($"in smoke: smoke={SmokeAt(PPos):F2} lightR={lit0:F0} (normal {(72 + 8 * Tier + 2 * Level) * (Has(Tech.Lens) ? 1.3f : 1f):F0})");
            TechMask |= 1 << (int)Tech.Fireproof; Log($"fireproof lightR={PlayerLightR:F0}"); TechMask &= ~(1 << (int)Tech.Fireproof);
        });
        S(.8f, () => { Shot("a3_smoke"); Clouds[0].Pos = PPos + new Vector2(2000, 0); });
        S(.2f, () =>
        {
            Tp(TileCenter(ent.X + 8, ent.Y));
            foreach (var k in new[] { EK.Imp, EK.Golem, EK.Spirit }) SpawnEnemy(PPos + new Vector2(rng.RandfRange(-50, 50), rng.RandfRange(-40, -10)), k).Age = 1;
        });
        S(1.5f, () => { Shot("a4_ash_enemies"); Log($"enemies={Enemies.Count} zones={Zones.Count} projs={Projs.Count}"); });
        S(.1f, () =>
        {
            var g = Enemies.Find(e => e.Kind == EK.Golem); int z0 = Zones.Count; if (g != null) { Hurt(g, 1, Vector2.Right, true); Hurt(g, 1, Vector2.Right, true); Hurt(g, 1, Vector2.Right, true); }
            Log($"golem puffs added={Zones.Count - z0}");
        });
        // ── способности и огнеупорность ──
        S(.2f, () => { Enemies.Clear(); Zones.Clear(); TechMask |= 1 << (int)Tech.Fireproof | 1 << (int)Tech.Dash; AddZone(PPos, 30, 0, 3f, 20f); noHeal = true; Hp = MaxHp; });
        S(.8f, () => { Log($"fireproof in fire zone: hp={Hp:F1}/{MaxHp:F0} (expect no loss)"); noHeal = false; TechMask &= ~(1 << (int)Tech.Fireproof); noHeal = true; Hp = MaxHp; });
        S(.8f, () => { Log($"no fireproof in fire zone: hp={Hp:F1}/{MaxHp:F0} (expect loss)"); noHeal = false; Zones.Clear(); Enemies.Clear(); });
        // ── кирка обсидиановая и руда ──
        S(.2f, () =>
        {
            var t = ToTile(PPos + new Vector2(0, 24)); SetObj(t.X, t.Y, Tl.Furnace);
            Tier = 3; UpgradePick(); Log($"pick tier={Tier} chain={ChainLimit}");
            var o = ToTile(PPos + new Vector2(-70, -30));
            for (int i = 0; i < 8; i++) SetObj(o.X + i, o.Y, Tl.Obsidian);
            BreakTile(o, Tl.Obsidian, true);
            int left = 0; for (int i = 0; i < 8; i++) if (W.Get(o.X + i, o.Y) == Tl.Obsidian) left++;
            Log($"obsidian vein chain: remaining {left}/8 (expect 2)");
        });
        // ── фонарь пустошей, коллектор ──
        S(.3f, () =>
        {
            var a = W.Ancients.Find(x => x.Region == 2 && !x.Boss); Tp(a.Pos + new Vector2(0, 40)); Cur = FindIx(); Cur.E?.Invoke(); a.Anim = 1;
            Log($"ash ancient lit={a.Lit}; veins revealed obsidian={CountTiles(Tl.Obsidian)} ember={CountTiles(Tl.Ember)}");
            TechPage = 2; for (int i = 0; i < 5; i++) BuyTech(i);
            Log($"ash techs: flame={Has(Tech.FlameEdge)} shield={Has(Tech.HeatShield)} collector={Has(Tech.Collector)} forgeHeat={Has(Tech.ForgeHeat)} fireLens={Has(Tech.FireLens)}");
            if (!HasBuilding(Tl.Warehouse)) { TestAim(FreeAdj(true)); BuildBuilding(0); }
            TestAim(FreeAdj(true)); BuildBuilding(5);
            Log($"collector built={HasBuilding(Tl.Collector)}");
            Stock[It.Obsidian] = 0; Stock[It.Ember] = 0; for (int i = 0; i < 400; i++) UpdateVillage(.2f);
            Log($"collector output 80s: obsidian={Stock[It.Obsidian]} ember={Stock[It.Ember]}");
        });
        S(.8f, () => Shot("a5_collector"));
        // ── босс ──
        S(.2f, () => { var bl = BossLantern(3); aimOverride = null; Tp(bl.Pos + new Vector2(-100, -60)); Log($"ash boss lantern {bl.Tile}"); });
        S(2f, () => { Log($"boss alive={Boss != null} kind={Boss?.Kind}"); Shot("a6_boss"); if (Boss != null) { Boss.State = 1; Boss.T2 = .05f; } });
        S(.9f, () => { Log($"pillars zones={Zones.Count}"); Shot("a7_pillars"); if (Boss != null) { Boss.State = 3; Boss.T2 = .05f; } });
        S(1.2f, () => { Log($"smoke zones={Zones.FindAll(z => z.Smoke).Count}"); Shot("a8_smoke_zones"); if (Boss != null) { Boss.State = 2; Boss.T2 = .6f; } });
        S(.5f, () => { Shot("a9_stomp_telegraph"); if (Boss != null) { Boss.State = 0; Boss.Hp = Boss.MaxHp * .3f; Boss.T1 = 5; } });
        S(1.5f, () => { Log($"rage announced={Boss?.Mark.X == 1}"); Shot("a10_rage"); if (Boss != null) { Boss.State = 4; Boss.T2 = .05f; } });
        S(.8f, () => { Log($"imps summoned={Enemies.FindAll(e => e.Kind == EK.Imp).Count}"); if (Boss != null) { Hurt(Boss, 9999, Vector2.Right); } });
        S(.8f, () => { Log($"boss3 defeated={BossDown(3)} fireproof={Has(Tech.Fireproof)} cores={Inv[It.Core]}"); });
        S(.2f, () =>
        {
            TestAim(new Vector2(0, 0)); TechMask |= 1 << (int)Tech.Dash; TryDash(); trailT = 0;
            for (int i = 0; i < 10; i++) ApplyHazards(.02f);
            Log($"dash fire trail zones={Zones.FindAll(z => z.Friendly).Count}"); aimOverride = null;
        });
        S(.2f, () =>
        {
            var bl = BossLantern(3); Tp(bl.Pos + new Vector2(0, 30)); Cur = FindIx(); Cur.E?.Invoke(); Log($"ash boss lantern lit={bl.Lit}");
            Tp(W.Ancients[0].Pos + new Vector2(0, 30)); Cur = FindIx(); Cur.E?.Invoke(); Log($"beacon level={BeaconLevel} radius={W.Ancients[0].TargetRadius}");
        });
        S(2f, () => Shot("a11_beacon4"));
        S(.2f, () => { MapOpen = true; for (int i = 0; i < Expl.Length; i++) Expl[i] = 1; });
        S(.6f, () => { Shot("a12_map"); MapOpen = false; TechOpen = true; TechPage = 2; });
        S(.5f, () => { Shot("a13_tech_ash"); TechOpen = false; });
    }

    int CountTiles(Tl t) { int n = 0; foreach (var b in W.Obj) if (b == (byte)t) n++; return n; }
}
