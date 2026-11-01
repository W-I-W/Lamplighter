using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Правки по отзывам (LAMP_STAGE=fixes): память прогресса добычи, выпадение вещей при смерти, регенерация, реликвии на здоровье, срубленные кусты по сети, цели, склад в цене навыков.</summary>
public partial class Game
{
    void BuildFixesScript()
    {
        int fails = 0; void Check(bool ok, string what) { if (!ok) fails++; GD.Print($"[check] {(ok ? "PASS" : "FAIL")} {what}"); }
        S(.3f, () => { Menu = Mn.None; noHeal = true; Invuln = 0; Enemies.Clear(); TestNoSpawn = true; for (int i = 0; i < It.Count; i++) Inv[i] = 0; Drops.Clear(); EquippedRelics.Clear(); OwnedRelics.Clear(); RecomputeEffects(); });
        S(.2f, () =>
        {
            var tile = new Vector2I(40, 40); MineTile = tile; MineProg = 1.4f; MineStop();
            Check(MineProg == 0 && MineTile.X < 0, "mining: stopping resets the live progress");
            Check(Mathf.IsEqualApprox(MineRecall(tile), 1.4f), "mining: coming back to the same block continues from where it stopped");
            Check(MineRecall(new Vector2I(41, 40)) == 0f, "mining: another block starts from zero");
            Time += 31f; Check(MineRecall(tile) == 0f, "mining: a block abandoned for 30 s has to be broken from the start");
            MineTile = new Vector2I(42, 40); MineProg = .5f; MineStop(); Check(MineMemoCount == 1 && MineRecall(tile) == 0f, $"mining: stale progress records are dropped ({MineMemoCount} left)"); MineMemoPrune(); Time += 31f; MineMemoPrune(); Check(MineMemoCount == 0, "mining: the memory empties itself");
        });
        S(.2f, () =>
        {
            int ns = 0, np = 0, far = 0; var sp = W.Spawn;
            for (int y = sp.Y - 20; y <= sp.Y + 20; y++) for (int x = sp.X - 20; x <= sp.X + 20; x++)
            { int f = StartForage(x, y, W.Seed, sp); if (f == 0) continue; if (f == FoliageStick) ns++; else np++; int dx = x - sp.X, dy = y - sp.Y; if (dx * dx + dy * dy > 14 * 14) far++; }
            Check(ns >= 15 && np >= 15 && far == 0, $"forage: sticks ({ns}) and pebbles ({np}) grow like bushes around the beacon clearing");
            Check(StartForage(sp.X + 40, sp.Y, W.Seed, sp) == 0 && StartForage(sp.X, sp.Y, W.Seed, sp) == 0, "forage: none far from the start and none on the beacon");
        });
        S(.2f, () =>
        {
            Inv[It.Wood] = 30; Inv[It.Stone] = 2; Inv[It.Coins] = 90; Inv[It.Core] = 1; Drops.Clear(); Diff = 0; Tier = 2; AxeLv = 0; OwnedTools.Clear(); OwnedTools.Add("pickaxe"); OwnedTools.Add("axe"); CurTool = "pickaxe"; Graves.Clear();
            var spot = ToTile(PPos); SetObj(spot.X, spot.Y, Tl.None);
            Die();
            int sum = 0; foreach (var n in Inv) sum += n;
            Check(sum == 0 && OwnedTools.Count == 0 && Tier == 0, $"death: every resource and tool is lost (items {sum}, tools {OwnedTools.Count}, tier {Tier})");
            Check(Graves.Count == 1 && W.Get(Graves[0].X, Graves[0].Y) == Tl.Grave, "death: a grave stands where the hero fell");
            Check(Graves.Count == 1 && Graves[0].Inv[It.Wood] == 30 && Graves[0].Inv[It.Coins] == 90 && Graves[0].Inv[It.Core] == 1 && Graves[0].Tools.Length == 2 && Graves[0].Tier == 2, "death: the grave keeps resources, coins, cores, tools and pickaxe tier");
            Check(!Drops.Exists(d => d.Item == It.Wood), "death: nothing falls on the ground any more"); Check(Owned.Count == 0 || (CurW >= 0 && HeldIcon() >= 0), "death: with no tools left the weapon is taken into the hand");
        });
        S(.2f, () =>
        {
            var g = Graves[0]; var tile = new Vector2I(g.X, g.Y);
            var other = new GraveRec { X = tile.X + 5, Y = tile.Y, Seed = W.Seed, Inv = new int[It.Count], Tools = new string[0] }; Check(!OwnGrave(new Vector2I(other.X, other.Y)), "grave: a tile without our record is someone else's grave");
            Check(OwnGrave(tile), "grave: our own grave is recognised");
            var save = BuildCharacter(); Check(save.Graves != null && save.Graves.Length == 1 && save.Graves[0].Inv[It.Wood] == 30, "grave: it is written into the hero save");
            BreakTile(tile, Tl.Grave, true);
            Check(W.Get(tile.X, tile.Y) == Tl.None && Graves.Count == 0, "grave: breaking it removes the tile and the record");
            Check(Inv[It.Wood] == 30 && Inv[It.Coins] == 90 && Inv[It.Core] == 1 && OwnedTools.Contains("pickaxe") && OwnedTools.Contains("axe") && Tier == 2, "grave: breaking it returns everything that was lost");
        });
        S(.2f, () =>
        {
            OwnedTools.Clear(); CurTool = ""; Tier = 0; CurW = -1; Inv[It.Wood] = 3; Inv[It.Stone] = 4; Tp(W.Ancients[0].Pos + new Vector2(0, 28)); var ft = ToTile(PPos) + new Vector2I(1, 0); SetObj(ft.X, ft.Y, Tl.Furnace); HasFurnace = true; RecomputeEffects();
            var rec = RecipesOf("furnace").Find(r => r.Id == "tool_pickaxe");
            Check(rec != null && !RecipesOf("furnace").Exists(r => r.Id == "pick_furnace"), "start: without a pickaxe the furnace offers to craft one, not to improve one");
            CraftStation = "furnace"; if (rec != null) CraftRecipe(rec);
            Check(OwnedTools.Contains("pickaxe") && Inv[It.Wood] == 0 && Inv[It.Stone] == 0, "start: the pickaxe is crafted from wood and stone");
            OwnedTools.Clear(); CurTool = ""; CurW = -1; for (int i = 0; i < HotCount; i++) Hot[i] = null; if (Owned.Count == 0) GiveWeapon("sword1", false); CurW = -1; InitHotbar();
            Check(CurW >= 0 && HeldIcon() >= 0 && IsWeaponRef(Hot[HotSel]), $"start: a hero without a pickaxe holds the sword (CurW {CurW}, icon {HeldIcon()}, hot {Hot[HotSel]})");
            Aim = PPos + new Vector2(40, 10); Swing = 0f;
            SetObj(ft.X, ft.Y, Tl.None);
        });
        S(.2f, () => { Invuln = 0; Tp(W.Ancients[0].Pos + new Vector2(70, 60)); Aim = PPos + new Vector2(40, 10); });
        S(.0f, () => { GiveWeapon("bow1", false); OwnedTools.Clear(); CurTool = ""; CurW = -1; });
        S(.3f, () => Check(CurW >= 0 && HeldIcon() >= 0, "hand: a weapon is taken into the hand on its own when there are no tools"));
        S(.2f, () =>
        {
            Tp(TileCenter(W.Spawn.X, W.Spawn.Y)); UpdateChunks(0, true); var fgs = new System.Collections.Generic.List<Vector2I>();
            for (int dy = -14; dy <= 14 && fgs.Count < 2; dy++) for (int dx = -14; dx <= 14 && fgs.Count < 2; dx++) { int wx = W.Spawn.X + dx, wy = W.Spawn.Y + dy; if (World.In(wx, wy) && W.Obj[World.Key(wx, wy)] == (byte)Tl.None && W.Ground[World.Key(wx, wy)] <= 3 && StartForage(wx, wy, W.Seed, W.Spawn) != 0) fgs.Add(new Vector2I(wx, wy)); }
            Check(fgs.Count == 2 && IsBushAt(fgs[0].X, fgs[0].Y) && IsBushAt(fgs[1].X, fgs[1].Y), "forage: sticks and pebbles stand by the beacon");
            if (fgs.Count < 2) return;
            CutBushAt(fgs[0]); Check(NetApplyTile(fgs[1].X, fgs[1].Y, BushCutCode, 0) && !IsBushAt(fgs[1].X, fgs[1].Y), "forage: a stick or pebble taken by another player disappears here as well");
            var far = TileCenter(W.Spawn.X, W.Spawn.Y) + new Vector2(0, 60 * TS); Tp(far); UpdateChunks(0, true); for (int i = 0; i < 40; i++) { regrowT = 0; scarceT = 0; UpdateRegrow(1.5f); UpdateScarcity(1f); }
            Tp(TileCenter(W.Spawn.X, W.Spawn.Y)); UpdateChunks(0, true);
            Check(!IsBushAt(fgs[0].X, fgs[0].Y) && !IsBushAt(fgs[1].X, fgs[1].Y), "forage: collected sticks and pebbles do not come back after the chunk is reloaded and the resources regrow");
        });
        S(.4f, () => Shot("held_idle"));
        S(.0f, () => { Swing = .2f; AtkCd = .3f; Invuln = 0; Aim = PPos + new Vector2(40, 10); });
        S(.03f, () => Shot("held_swing"));
        S(.2f, () => { Hp = 10; regenDelay = 0; Tp(W.Ancients[0].Pos + new Vector2(0, 400 * 1)); });
        S(1.0f, () => { Check(!IsLit(PPos) && Hp <= 10.01f, $"regen: no regeneration in the dark without the skill (hp {Hp:0.0})"); Tp(W.Ancients[0].Pos + new Vector2(0, 30)); Hp = 10; });
        S(1.0f, () => { Check(Hp > 10.5f && Hp < 14f, $"regen: near the base it is slower than before, about 2 hp/s (hp {Hp:0.0})"); });
        S(.2f, () =>
        {
            float baseHp = MaxHp; GiveRelic("r_ironheart", true); if (!EquippedRelics.Contains("r_ironheart")) EquippedRelics.Add("r_ironheart"); RecomputeEffects();
            Check(Mathf.IsEqualApprox(MaxHp, baseHp + 20f), $"relic: Iron Heart adds 20 max health ({baseHp} → {MaxHp})");
            GiveRelic("r_lifeseed", true); if (!EquippedRelics.Contains("r_lifeseed")) EquippedRelics.Add("r_lifeseed"); RecomputeEffects();
            Check(Mathf.IsEqualApprox(MaxHp, baseHp + 20f + baseHp * .05f), $"relic: Seed of Life adds 5% of the base ({MaxHp})");
            EquippedRelics.Clear(); RecomputeEffects(); Check(Mathf.IsEqualApprox(MaxHp, baseHp), "relic: taking the relics off returns max health");
        });
        S(.2f, () =>
        {
            int bx = 60, by = 60; int key = World.Key(bx, by); CutBush.Remove(key);
            Check(NetApplyTile(bx, by, BushCutCode, 0) && CutBush.Contains(key), "net: a bush cut by another player disappears here too");
            Check(Array.IndexOf(NetStructTiles(), key) >= 0, "net: a player who joins later receives the cut bushes");
        });
        S(.2f, () =>
        {
            foreach (var p in W.Pois) if (p.Kind == PK.TechCache) p.State = 3;
            Check(!BuildGoals().Exists(g => g.Title == "Найти забытую технологию"), "goals: the forgotten-technology goal vanishes when every pedestal is taken");
            foreach (var a in W.Ancients) if (!a.Boss && a.Region == 1) a.Lit = true; BeaconLevel = Math.Max(BeaconLevel, 2);
            Check(!BuildGoals().Exists(g => g.Title == "Зажечь фонари шахт"), "goals: no 'light the lanterns' goal when all of them are already lit");
        });
        S(.2f, () =>
        {
            Inv[It.Crystal] = 0; Stock[It.Crystal] = 9; var wh = Buildings.Count; bool near = NearWarehouse;
            Check(HaveAny(It.Crystal) == (near ? 9 : 0), "skills: the cost check counts the warehouse like the purchase does");
            GD.Print("[check] SUMMARY failures=" + fails); GetTree().Quit();
        });
    }
}
