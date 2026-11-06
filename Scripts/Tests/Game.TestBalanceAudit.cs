using Godot;
using System;
using System.Linq;
using Lamplighter.Balance;

namespace Lamplighter;

/// <summary>Регрессии аудита баланса: LAMP_STAGE=balanceaudit. Только отдельное тестовое сохранение.</summary>
public partial class Game
{
    void BuildBalanceAuditScript()
    {
        int fails = 0;
        void Check(bool ok, string text) { if (!ok) fails++; GD.Print($"[check] {(ok ? "PASS" : "FAIL")} {text}"); }
        bool Eq(float a, float b) => Mathf.Abs(a - b) < .002f * Mathf.Max(1, Mathf.Abs(b));
        void Clean()
        {
            Enemies.Clear(); Projs.Clear(); Beacons.Clear(); Lamps.Clear(); Zones.Clear();
            Owned.Clear(); CurW = -1; EquippedRelics.Clear(); TempRelics.Clear(); OwnedRelics.Clear(); Array.Clear(SkillLv);
            RecomputeEffects(); MaxHp = Hp = 60; Level = 1; Xp = 0; Invuln = 0; Diff = 0; ctxDmg = 1;
        }
        S(.1f, () => { Menu = Mn.None; noHeal = true; EventsEnabled = false; TestNoSpawn = true; Clean(); });
        S(.1f, () =>
        {
            Clean(); Level = 40; Xp = 0; SmeltQ.Clear(); Array.Clear(Inv); BuildStock.Clear();
            Inv[It.Stone] = 10; Inv[It.Wood] = 5; Aim = PPos + FreeAdj();
            var entry = EntryOf("furnace"); var tile = ToTile(Aim);
            Check(CraftBuild(entry) && Eq(Xp, 20f * Tune.T.XpMul), "build: creating one new furnace rewards XP once");
            float xp = Xp; bool placed = true;
            for (int i = 0; i < 4; i++)
            {
                placed &= PlaceEntry(entry);
                DemolishTile(tile.X, tile.Y, "furnace", "furnace"); DemolishTile(tile.X, tile.Y, "furnace", "furnace");
            }
            Check(placed && Eq(Xp, xp) && StockOf("furnace") == 1 && Inv[It.Stone] == 0 && Inv[It.Wood] == 0,
                "build: four relocations create neither XP nor extra items/resources");
            var invalid = tile + new Vector2I(200, 0); Aim = TileCenter(invalid.X, invalid.Y);
            Check(!PlaceEntry(entry) && Eq(Xp, xp) && StockOf("furnace") == 1 && !placingStock, "build: failed placement preserves stock and clears transaction state");
            var saved = BuildCharacter(); int ki = Array.IndexOf(saved.BsK, "furnace");
            Check(ki >= 0 && saved.BsV[ki] == 1, "build: save sees the final stock");
        });
        S(.1f, () =>
        {
            Clean(); Hp = 1; OwnedRelics.AddRange(new[] { "r_ironheart", "r_lifeseed", "r_titanheart" });
            for (int i = 0; i < 20; i++) foreach (var id in OwnedRelics.ToArray()) { ToggleRelic(id); ToggleRelic(id); }
            Check(Eq(Hp, 1) && Eq(MaxHp, 60), "relics: repeated flat/percent health swaps cannot heal");
            Hp = 0; ToggleRelic("r_ironheart"); Check(Hp == 0, "relics: equipping cannot resurrect a dead player");
            ToggleRelic("r_ironheart"); Check(!ToggleRelic("r_keenedge"), "relics: unowned relic cannot be equipped");
            Hp = 15; ToggleRelic("r_ironheart"); var saved = BuildCharacter();
            var loaded = new Game { W = W }; loaded.ApplyCharacter(saved, null); loaded.RecomputeEffects();
            Check(Eq(loaded.Hp, Hp) && Eq(loaded.MaxHp, MaxHp), "relics: save/load does not heal or rescale saved current health"); loaded.Free();
        });
        S(.1f, () =>
        {
            Clean(); Hp = 60; HurtPlayer(9f * Tune.T.EnemyDamage, Vector2.Zero);
            Check(Hp > 0 && Hp > 30, $"start: shade hit leaves {Hp:0.00}/60 health");
            Level = 5; MaxHp = Hp = 1000; Invuln = 0; HurtPlayer(7f * Tune.T.EnemyDamage, Vector2.Zero, true); float baseHit = 1000 - Hp;
            SetSkillLevel((int)Tech.HeatShield, SkillMax((int)Tech.HeatShield)); Hp = 1000; Invuln = 0;
            HurtPlayer(7f * Tune.T.EnemyDamage, Vector2.Zero, true);
            Check(Eq(1000 - Hp, baseHit * HeatTaken) && 1000 - Hp < baseHit, "fire: maximum heat shield reduces actual incoming damage");
            Hp = 1000; Invuln = 0; HurtPlayer(.1f, Vector2.Zero, true);
            Check(Eq(1000 - Hp, Tune.T.EnemyMinHit * HeatTaken), "fire: resistance also reduces the minimum base damage");
        });
        S(.1f, () =>
        {
            Clean(); bool tools = true;
            foreach (int lv in new[] { 1, 10, 20, 40, 60 }) foreach (int tier in new[] { 0, 1, 2, 3, 4 })
            {
                Level = lv; Tier = tier; AxeLv = Math.Min(tier, AxeMaxLv);
                var sword = Db.Weapon(tier < 2 ? "sword1" : tier == 2 ? "sword2" : "sword3");
                tools &= ToolHitDmg("pickaxe") / .42f < WeaponDmg(sword) / sword.Cd;
                tools &= ToolHitDmg("axe") / (.42f * ToolDef.Get("axe").AttackInterval) < WeaponDmg(sword) / sword.Cd;
            }
            Check(tools, "tools: pickaxe and upgraded axe stay below matching sword single-target DPS, levels 1-60");
            Clean(); SetSkillLevel((int)Tech.HeroLamp, 5); float last = AuraDps;
            bool aura = true;
            for (int lv = 1; lv <= SkillMax((int)Tech.Searing); lv++) { SetSkillLevel((int)Tech.Searing, lv); aura &= AuraDps > last; last = AuraDps; }
            Check(aura, "skills: every Searing level adds damage even with maximum HeroLamp");
            SetSkillLevel((int)Tech.Reaper, 5); Check(ReaperHeal >= 3 && ReaperHeal < 10, "skills: Reaper provides meaningful, bounded healing");
        });
        S(.1f, () =>
        {
            Clean(); var pos = PPos;
            foreach (var kind in new[] { EK.Source, EK.WinterWarden })
            {
                var e = SpawnEnemy(pos, kind, null, 1); e.Shield = true;
                float hp = e.Hp; HurtTick(e, 100, true);
                Check(Eq(hp - e.Hp, 6), $"dot: {kind} shield reduces continuous light damage to six percent");
                hp = e.Hp; NetEnemyHit(LocalPeer, e.NetId, 10, Vector2.Zero, false, true, true);
                Check(Eq(hp - e.Hp, .6f), $"network: {kind} periodic damage goes through the host shield calculation");
                Beacons.Clear(); Beacons.Add(new Beacon { Pos = pos, T = 1, Dur = 10, R = 72, Burn = 6 });
                hp = e.Hp; UpdateBeacons(1); Check(Eq(hp - e.Hp, .108f), $"flare: {kind} shield also protects against flare");
                Enemies.Clear(); Beacons.Clear();
            }
            var boss = SpawnEnemy(pos, EK.Source, null, 1); boss.Shield = false;
            HurtTick(boss, 100000); float floor = boss.MaxHp * .66f;
            Hurt(boss, 100000, Vector2.Zero); HurtTick(boss, 100000);
            Check(Eq(boss.Hp, floor), "phases: repeated hits and DOT cannot cross the pending phase threshold");
            BossPhaseTick(boss); HurtTick(boss, 100000); Check(Eq(boss.Hp, boss.MaxHp * .33f), "phases: next phase has its own damage floor");
            Enemies.Clear(); var grove = SpawnEnemy(pos, EK.Warden, null, 1); grove.State = 9; float gh = grove.Hp;
            HurtTick(grove, 100000, true); Check(Eq(grove.Hp, gh), "grove: transition invulnerability applies to DOT"); Enemies.Clear();
        });
        S(.1f, () =>
        {
            Clean(); var camp = W.Pois.First(p => p.Kind == PK.Camp && p.Pos.DistanceTo(W.Ancients[0].Pos) > 300);
            var lamp = W.Ancients.First(a => a.Kind == LampKind.Guarded && !a.Boss && a.Pos.DistanceTo(W.Ancients[0].Pos) > 300);
            camp.State = 1; lamp.Lit = false; lamp.Done = false; lamp.Phase = 1;
            Enemies.Add(new Enemy { Kind = EK.Shade, Hp = 10, MaxHp = 10, Guard = camp, Pos = camp.Pos });
            Enemies.Add(new Enemy { Kind = EK.Shade, Hp = 10, MaxHp = 10, Guard = lamp.GuardPoi, Pos = lamp.Pos });
            int kills = Kills; Die(); UpdatePois(1); UpdateAncientKinds(1);
            Check(camp.State == 0 && !lamp.Done && lamp.Phase == 0 && Kills == kills, "death: living guards reset instead of completing camp and lantern");
            PPos = camp.Pos; UpdatePois(1); Check(camp.State == 1 && Enemies.Exists(e => e.Guard == camp), "guards: retrying spawns a fresh wave");
            foreach (var e in Enemies.Where(e => e.Guard == camp).ToArray()) { e.Hp = 0; Enemies.Remove(e); }
            UpdatePois(1); Check(camp.State == 2, "guards: real completion still opens the camp");
        });
        S(.1f, () =>
        {
            Clean(); var m = BalanceModel.FromGame(); Level = 20; MaxHp = Hp = CombatMath.BaseHealth(20);
            var sp = new SimPlayer(m) { Level = 20, Hp = CombatMath.BaseHealth(20) };
            GiveWeapon("sword3", false); Owned[0].Lv = 4; CurW = 0;
            sp.GiveWeapon(Array.FindIndex(m.Weapons, w => w.Id == "sword3")); sp.Owned[0].Lv = 4;
            foreach (var id in new[] { "r_ironheart", "r_lifeseed" }) { OwnedRelics.Add(id); EquippedRelics.Add(id); sp.RelicsEq.Add(id); }
            RecomputeEffects(); sp.Recompute();
            Check(Eq(sp.MaxHp, MaxHp) && Eq(sp.Hp, Hp), "model: flat/percent relic health and current health match Game");
            Check(Eq(sp.WeaponDmg(sp.Cur), WeaponDmg(CurDef)), "model: upgraded weapon damage matches Game");
            sp.Inv[It.Coins] = 75; sp.Inv[It.Shard] = 9; sp.Tier = 3; float max = sp.MaxHp;
            var grave = sp.Bury(); Check(sp.Owned.Count == 0 && sp.Tier == 0 && sp.Inv.Sum() == 0 && Eq(sp.MaxHp, max), "model: grave holds resources, weapons and tier, relics stay equipped");
            sp.Recover(grave); Check(sp.Inv[It.Coins] == 75 && sp.Inv[It.Shard] == 9 && sp.Owned[0].Lv == 4 && sp.Tier == 3, "model: grave recovery returns exact resources and upgraded equipment");
            var copy = sp.Copy(); copy.Owned[0].Lv = 0; Check(sp.Owned[0].Lv == 4, "model: weapon levels are copied independently");
            bool range = true; for (int i = 0; i <= 100; i++)
            { float f = i / 100f; range &= Eq(RangeFalloff(new PProj { Kind = 0, Life0 = 1, Life = 1-f }), CombatMath.RangeFalloff(f)); }
            Check(range, "model: projectile falloff uses the exact game formula");
            var opt = Profile.Optimizer(); var many = Profile.ByName("probe-maxlamps");
            Check(opt.Skills == many.Skills && opt.RestBelow == many.RestBelow && opt.SimulatedReadiness == many.SimulatedReadiness && opt.DodgeSkill == many.DodgeSkill, "probes: all-lantern comparison changes only lantern count");
        });
        S(0, () => { GD.Print($"[check] SUMMARY failures={fails}"); GD.Print("[check] BALANCE AUDIT DONE"); });
    }
}
