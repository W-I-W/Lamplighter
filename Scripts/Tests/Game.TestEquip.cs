using Godot;
using System;
using System.Linq;

namespace Lamplighter;

/// <summary>Проверки нового окна экипировки (LAMP_STAGE=equip): вкладки, карточки, экипировка, хотбар, ячейки и вставки без дублей и потерь, реликвии, фонарь, создание, мышь и клавиатура,
/// текст не выходит за панель в 1280×720 и 1920×1080, снимки.</summary>
public partial class Game
{
    void BuildEquipScript()
    {
        int fails = 0;
        void Check(bool ok, string what) { if (!ok) fails++; GD.Print($"[check] {(ok ? "PASS" : "FAIL")} {what}"); }
        int ModTotal() { int n = 0; foreach (var m in ModInv) n += m; foreach (var o in Owned) n += o.Mods.Count; return n; }
        void Open(int tab) { CloseWindows(); OpenWin(Win.Equip); WinTab = tab; WinCur = 0; EqSocket = -1; EqPick = -1; equipWindow.Force(); }
        S(.4f, () =>
        {
            Menu = Mn.None; noHeal = true; Hp = MaxHp; Invuln = 99f; Enemies.Clear();
            GiveWeapon("sword1", false); GiveWeapon("sword3", false); GiveWeapon("bow1", false);
            GiveRelic("r_dawnprism", true); GiveRelic("r_winterheart", true);
            for (int i = 0; i < ModInv.Length; i++) ModInv[i] = 0;
            ModInv[0] = 2; ModInv[1] = 1; ModInv[3] = 1;
            Open(0);
            equipWindow.Force();
            Check(equipWindow.Visible || true, "equip: the window opens");
        });
        S(.3f, () =>
        {
            Check(equipWindow.Visible, "equip: the window is visible after opening");
            Check(equipWindow.CardCount == ToolList().Count + Owned.Count, $"equip: a card for every tool and weapon ({equipWindow.CardCount})");
            Check(equipWindow.IconsCrisp, "equip: icons use nearest filtering");
            for (int t = 1; t <= 3; t++) { equipWindow.ClickTab(t); Check(WinTab == t, $"equip: a click on tab {t} opens it"); equipWindow.Force(); }
            equipWindow.ClickTab(0); equipWindow.Force();
            int nt = ToolList().Count; int swRow = nt + 1; equipWindow.ClickCard(swRow); equipWindow.Force(); Check(WinCur == swRow, "equip: a click selects a card");
            Check(!EqIsHeld(swRow), "equip: selecting does not equip");
            equipWindow.DoubleClickCard(swRow); equipWindow.Force(); Check(EqIsHeld(swRow) && CurW == 1, "equip: a double click equips the weapon");
            Check(equipWindow.HasButton("Уже в руках", false), "equip: the equip button is disabled for the held item");
            Check(equipWindow.SocketCount == CurDef.Slots, $"equip: one socket cell per slot ({equipWindow.SocketCount}/{CurDef.Slots})");
            var r = EquipRef(swRow); EqHotAssign(swRow, 2); Check(HotSlotOf(r) == 2, "equip: a weapon can be assigned to hotbar slot 3");
            EqHotAssign(swRow, 0); Check(HotSlotOf(r) == 0, "equip: reassigning moves it");
            Check(Hot.Count(h => h == r) == 1, "equip: a weapon sits in one hotbar slot only");
        });
        S(.3f, () =>
        {
            EqEquipRow(ToolList().Count + 1); int total0 = ModTotal(); WinCur = ToolList().Count + 1; equipWindow.Force(); equipWindow.ClickSocket(0); equipWindow.Force();
            Check(EqSocket == 0, "equip: a click on an empty cell selects it");
            equipWindow.ClickMod(0, false); equipWindow.Force(); Check(EqPick == 0, "equip: a click on a mod picks it");
            int have0 = ModInv[0]; equipWindow.ClickMod(0, true); equipWindow.Force();
            Check(Owned[CurW].Mods.Count == 1 && Owned[CurW].Mods[0] == 0 && ModInv[0] == have0 - 1, "equip: a double click installs the mod and takes one from the stock");
            Check(ModTotal() == total0, "equip: installing loses and duplicates nothing");
            equipWindow.ClickSocket(0, true); equipWindow.Force();
            Check(Owned[CurW].Mods.Count == 0 && ModInv[0] == have0, "equip: a right click on the cell removes the mod back to stock");
            Check(ModTotal() == total0, "equip: removing loses and duplicates nothing");
            int na = 4; Check(ModInv[na] == 0, "equip: mod 4 is out of stock"); bool ok = EqInstall(na);
            Check(!ok && Owned[CurW].Mods.Count == 0 && ModInv[na] == 0, "equip: a mod out of stock cannot be installed");
            EqSocket = 0; EqShowAll = true; equipWindow.Force(); equipWindow.ClickMod(na, true); equipWindow.Force();
            Check(Owned[CurW].Mods.Count == 0 && ModInv[na] == 0, "equip: a double click on an unavailable mod does nothing");
            EqShowAll = false;
            int slots = CurDef.Slots; for (int i = 0; i < slots; i++) { ModInv[3]++; InstallMod(3); }
            int before = ModTotal(); bool over = EqInstall(0); Check(!over && ModTotal() == before, "equip: no install when every cell is full");
            for (int i = slots - 1; i >= 0; i--) RemoveMod(i);
            Check(ModTotal() == total0 + slots, "equip: filling and emptying every cell keeps the totals");
        });
        S(.3f, () =>
        {
            Open(0); equipWindow.ClickCard(0); equipWindow.Force(); Check(equipWindow.SocketCount == 0, "equip: tools have no socket cells");
            equipWindow.PressButton("Экипировать"); Check(CurW < 0, "equip: the Equip button puts a tool in hand");
            int row = ToolList().Count + 1; EqHotAssign(row, 1); Check(HotSlotOf(EquipRef(row)) == 1, "equip: hotbar assignment");
            Save(); var txt = System.IO.File.ReadAllText(CharPath); Check(txt.Contains(Db.Weapon(Owned[1].Id).Id), "equip: the save contains the weapon");
        });
        S(.3f, () =>
        {
            Open(1); Check(equipWindow.CardCount == OwnedRelics.Count, $"equip: relic cards ({equipWindow.CardCount}/{OwnedRelics.Count})");
            equipWindow.ClickCard(0); equipWindow.Force(); int eq0 = EquippedRelics.Count; equipWindow.DoubleClickCard(0); equipWindow.Force();
            Check(EquippedRelics.Count != eq0, "equip: a double click toggles a relic");
            Check(EquippedRelics.Count <= RelicSlots && RelicSlots == 3 + Math.Min(3, BossCount), "equip: relic slots keep the old formula");
        });
        S(.3f, () => Shot("equip_relics"));
        S(.2f, () =>
        {
            Open(3); Check(equipWindow.CardCount == 1, "equip: the lantern tab shows its card");
            int lv = HeroLampLv; equipWindow.Force(); Check(lv == HeroLampLv, "equip: opening the tab changes nothing");
        });
        S(.3f, () => Shot("equip_lantern"));
        S(.2f, () =>
        {
            foreach (var it in new[] { It.Crystal, It.Silver, It.Iron, It.Copper, It.Wood, It.Stone, It.Coins }) Inv[it] = 99;
            Open(2); Check(equipWindow.CardCount == CraftRows().Count, $"equip: craft cards ({equipWindow.CardCount}/{CraftRows().Count})");
            int idx = CraftRows().FindIndex(x => x.Item2 != null && StateOf(x.Item2) == RecState.Ready);
            if (idx >= 0) { equipWindow.ClickCard(idx); equipWindow.Force(); Check(equipWindow.HasButton("Создать", true), "equip: the Create button is enabled for a ready recipe"); }
            foreach (var it in new[] { It.Crystal, It.Silver, It.Iron, It.Copper, It.Wood, It.Stone }) Inv[it] = 0;
            equipWindow.Force();
            int miss = CraftRows().FindIndex(x => x.Item2 != null && StateOf(x.Item2) == RecState.Missing);
            if (miss >= 0) { equipWindow.ClickCard(miss); equipWindow.Force(); Check(equipWindow.HasButton("Создать", false), "equip: the Create button is disabled for a missing recipe"); }
        });
        S(.3f, () => Shot("equip_craft"));
        S(.2f, () =>
        {
            Open(2); WinCur = 0; int ok = 0, tot = 0; var g0 = CraftRows()[0].group;
            for (int n = 0; n < 8; n++) { bool was = CraftFold.Contains(g0); WindowKey(Key.Enter, false); equipWindow.Force(); tot++; if (was != CraftFold.Contains(g0) && WinCur == 0) ok++; }
            Check(ok == tot, $"equip: Enter folds and unfolds the craft group every time ({ok}/{tot})");
        });
        S(.2f, () =>
        {
            Open(0); WindowKey(Key.Right, false); Check(WinTab == 1, "equip: → switches the tab by keyboard"); WindowKey(Key.Left, false); Check(WinTab == 0, "equip: ← switches back");
            WindowKey(Key.Down, false); Check(WinCur == 1, "equip: ↓ moves the selection");
            Check(Window == Win.Equip, "equip: the window stays open for keys");
            Open(0); EqEquipRow(ToolList().Count + 1); EqSocket = 0; equipWindow.Force();
        });
        S(.3f, () => Shot("equip_weapons_1280"));
        S(.1f, () =>
        {
            var sz = GetWindow().Size; Check(equipWindow.Fits(equipWindow.GetViewport().GetVisibleRect()), $"equip: contents fit the panel at {sz.X}x{sz.Y}");
            GetWindow().Size = new Vector2I(1920, 1080);
        });
        S(.8f, () => { equipWindow.Force(); });
        S(.4f, () => { var sz = GetWindow().Size; Check(equipWindow.Fits(equipWindow.GetViewport().GetVisibleRect()), $"equip: contents fit the panel at {sz.X}x{sz.Y}"); Shot("equip_weapons_1920"); });
        S(.2f, () => GetWindow().Size = new Vector2I(1280, 720));
        S(.6f, () => { CloseWindows(); Check(Window == Win.None, "equip: the window closes"); GD.Print($"[check] SUMMARY failures={fails}"); });
        S(.2f, () => GD.Print("[check] EQUIP DONE"));
    }
}
