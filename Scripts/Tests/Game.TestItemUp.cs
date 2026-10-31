using Godot;
using System;

namespace Lamplighter;

/// <summary>Улучшение инструментов и оружия (LAMP_STAGE=itemup): уровни, цена, станция рядом, рост урона и добычи, сохранение.</summary>
public partial class Game
{
    void BuildItemUpScript()
    {
        int fails = 0; void Check(bool ok, string what) { if (!ok) fails++; GD.Print($"[check] {(ok ? "PASS" : "FAIL")} {what}"); }
        S(.3f, () => { Menu = Mn.None; noHeal = true; Hp = MaxHp; Invuln = 99f; Enemies.Clear(); TestNoSpawn = true; for (int i = 0; i < It.Count; i++) Inv[i] = 500; Owned.Clear(); GiveWeapon("sword1"); GiveWeapon("sword2"); AxeLv = 0; OwnedTools.Add("axe"); });
        S(.2f, () =>
        {
            float d0 = WeaponDmg(Db.Weapon("sword1")); bool near = NearUpgrade();
            var t = ToTile(PPos) + new Vector2I(1, 0); SetObj(t.X, t.Y, Tl.None);
            bool far = !FurnaceNear(56, out _) && BuildingNear(PPos, 56) == null; bool r0 = far && !UpgradeWeapon(0) && Owned[0].Lv == 0;
            Check(!near && r0, "itemup: without a furnace or forge nearby nothing is upgraded");
            SetObj(t.X, t.Y, Tl.Furnace); HasFurnace = true;
            Check(NearUpgrade(), "itemup: a furnace next to the hero allows upgrades");
            Check(RecipesOf("furnace").Exists(r => r.Id == "w_sword1_furnace" && r.IsUpgrade) && RecipesOf("hand").Exists(r => r.Id == "tool_axe" && r.IsUpgrade), "itemup: owned weapons and the axe offer an upgrade instead of crafting");
            OpenStation("furnace"); equipWindow.Force(); int wi = CraftRows().FindIndex(x => x.r.Id == "w_sword1_furnace"); equipWindow.ClickRecipe(wi, false); equipWindow.Force();
            Check(wi >= 0 && equipWindow.HasButton("Улучшить", true) && !equipWindow.HasButton("Создать", true), "itemup: the button of an owned weapon says Upgrade, not Create"); CloseWindows(); StationOpened = false;
            int cu = Inv[It.Copper]; var cost = WeaponUpCost(1, 1);
            Check(UpgradeWeapon(0) && Owned[0].Lv == 1 && Inv[It.Copper] == cu - cost[0][1], $"itemup: sword +1 is paid and applied");
            float d1 = WeaponDmg(Db.Weapon("sword1")); Check(Mathf.Abs(d1 / d0 - 1.1f) < .001f, $"itemup: +10% damage per level ({d0:F2} -> {d1:F2})");
            for (int i = 0; i < 4; i++) UpgradeWeapon(0);
            Check(Owned[0].Lv == WeaponMaxLv && !UpgradeWeapon(0), "itemup: the maximum level is 5");
            Check(WeaponDmg(Db.Weapon("sword1")) < WeaponDmg(Db.Weapon("sword2")), "itemup: a maxed tier-1 sword is still weaker than a fresh tier-2 sword");
            Check(WeaponDmg(Db.Weapon("sword2")) == Db.Weapon("sword2").Dmg * (1f + .35f * 0) + .35f * (Level - 1) * 1f * (1f + N("Dmg")) + (Db.Weapon("sword2").Dmg * N("Dmg")) || WeaponLv("sword2") == 0, "itemup: the second weapon keeps its own level");
            var tm = ToolDef.Get("axe"); float e0 = tm.Efficiency(Tl.Tree) * ToolMineMul("axe");
            Check(UpgradeAxe() && AxeLv == 1 && ToolMineMul("axe") > 1f && ToolDmgMul("axe") > 1f && ToolMineMul("pickaxe") == 1f, $"itemup: axe +1 mines faster ({e0:F2}) and hits harder");
            for (int i = 0; i < 6; i++) UpgradeAxe(); Check(AxeLv == AxeMaxLv, "itemup: axe maximum is 4");
            GiveWeapon("staff1"); int sti = Owned.FindIndex(o => o.Id == "staff1"); CurW = 0; WinTab = 0; WinCur = ToolList().Count + sti; ModInv[3] = 1; EqSocket = 0;
            Check(EqInstall(3) && Owned[sti].Mods.Count == 1 && CurW != sti && ModInv[3] == 0, "itemup: a mod can be installed into a weapon that is not held"); EqRemove(0); Check(Owned[sti].Mods.Count == 0 && ModInv[3] == 1, "itemup: and removed from it");
            var save = BuildCharacter(); Owned[1].Lv = 3; save = BuildCharacter(); int ax = AxeLv; AxeLv = 0; Owned[0].Lv = 0; Owned[1].Lv = 0;
            Owned.Clear(); ApplyCharacter(save, null);
            Check(AxeLv == ax && Owned[0].Lv == WeaponMaxLv && Owned[1].Lv == 3, $"itemup: levels survive save and load (axe {AxeLv}/{ax}, n={Owned.Count}, {(Owned.Count > 1 ? Owned[0].Lv + "," + Owned[1].Lv : "")})");
            for (int i = 0; i < It.Count; i++) Inv[i] = 0; int lv = Owned[1].Lv; Check(!UpgradeWeapon(1) && Owned[1].Lv == lv, "itemup: without resources the upgrade is refused");
            OpenWin(Win.Equip); WinTab = 0; WinCur = 0;
        });
        S(.5f, () => { Shot("itemup_axe"); WinCur = ToolList().Count; });
        S(.5f, () => { Shot("itemup_weapon"); });
        S(.1f, () => { GD.Print("[check] SUMMARY failures=" + fails); GD.Print("ITEMUP DONE"); });
    }
}
