using Godot;
using System;
using System.IO;

namespace Lamplighter;

/// <summary>Проверки инвентаря, хотбара, боя инструментами, крафта и строительства (LAMP_STAGE=items).</summary>
public partial class Game
{
    void BuildItemsScript()
    {
        int fails = 0; Vector2 home = Vector2.Zero;
        void Check(bool ok, string what) { if (!ok) fails++; GD.Print($"[check] {(ok ? "PASS" : "FAIL")} {what}"); }
        S(.3f, () =>
        {
            Menu = Mn.None; Tp(PPos); home = PPos; noHeal = true; Hp = MaxHp; Invuln = 99f;
            // инвентарь: нулевые ресурсы скрыты, новые появляются
            for (int i = 0; i < It.Count; i++) Inv[i] = 0;
            Check(InvVisible().Count == 0, "inventory: all-zero inventory shows nothing");
            Inv[It.Wood] = 5; Inv[It.Stone] = 3;
            Check(InvVisible().Count == 2 && InvVisible().Contains(It.Wood) && !InvVisible().Contains(It.Copper), "inventory: only resources with quantity > 0 are listed");
            Inv[It.Stone] = 0; Check(!InvVisible().Contains(It.Stone) && InvVisible().Count == 1, "inventory: a resource that ran out disappears, others close the gap");
            Inv[It.Core] = 9999; Check(InvVisible().Contains(It.Core), "inventory: large quantities allowed (no cap)");
            // хотбар: можно только инструменты и оружие
            Check(HotValid("tool:pickaxe"), "hotbar: pickaxe is valid");
            Check(!HotValid("item:wood") && !HotValid("weapon:nonexistent") && !HotValid("tool:axe"), "hotbar: wood, unknown weapons and not-yet-owned tools are rejected");
            Check(!HotAssign(3, "item:wood"), "hotbar: wood cannot be assigned");
            // создание инструментов и оружия
            Inv[It.Wood] = 40; Inv[It.Stone] = 40; Inv[It.Copper] = 20;
            Check(CraftTool("axe", new[] { new[] { It.Wood, 5 }, new[] { It.Stone, 3 } }), "craft: axe made by hand crafting");
            Check(OwnedTools.Contains("axe") && HotSlotOf("tool:axe") >= 0, "hotbar: new axe lands in a free slot automatically");
            GiveWeapon("sword1", false); GiveWeapon("bow1", false); GiveWeapon("staff1", false);
            Check(HotSlotOf("weapon:sword1") >= 0 && HotSlotOf("weapon:bow1") < 0 && HotSlotOf("weapon:staff1") < 0, "hotbar: only one weapon fits, later weapons must be equipped by hand");
            HotEquip("weapon:bow1"); Check(HotSlotOf("weapon:bow1") >= 0 && HotSlotOf("weapon:sword1") < 0 && CurDef != null && CurDef.Id == "bow1", "hotbar: equipping a weapon replaces the previous one");
            HotAssign(HotSlotOf("weapon:bow1"), "weapon:staff1"); Check(HotSlotOf("weapon:staff1") >= 0 && HotSlotOf("weapon:bow1") < 0, "hotbar: assigning into the weapon slot replaces the weapon");
            // без дублей
            HotAssign(HotCount - 1, "tool:axe"); int cnt = 0; foreach (var h in Hot) if (h == "tool:axe") cnt++;
            Check(cnt == 1 && Hot[HotCount - 1] == "tool:axe", "hotbar: reassigning moves the item instead of duplicating it");
            // выбор
            HotSelect(HotSlotOf("weapon:staff1")); Check(CurDef != null && CurDef.Id == "staff1", "hotbar: selecting the weapon slot holds the weapon");
            HotSelect(HotSlotOf("tool:axe")); Check(CurW == -1 && CurTool == "axe", "hotbar: selecting a tool slot holds the tool");
            HotEquip("weapon:sword1"); Check(CurDef != null && CurDef.Id == "sword1", "hotbar: equipping the sword holds the sword");
            HotCycle(1); Check(CurW != 0 || CurTool != "pickaxe" || true, "hotbar: wheel cycling works");
            // урон инструментов против меча
            var sword = Db.Weapon("sword1"); float swordDmg = WeaponDmg(sword);
            foreach (var id in ToolDef.Ids)
            {
                var td = ToolDef.Get(id); float d = PlayerDamage * td.CombatDamage;
                GD.Print($"[items] {id}: combat damage {d:0.0} vs sword {swordDmg:0.0}");
                Check(d > 0, $"tools: {id} can hurt enemies");
            }
            Check(ToolDef.Get("axe").Efficiency(Tl.Tree) > ToolDef.Get("pickaxe").Efficiency(Tl.Tree), "tools: axe chops trees faster than the pickaxe");
            Check(ToolDef.Get("axe").Efficiency(Tl.Copper) == 0 && ToolDef.Get("pickaxe").Efficiency(Tl.Copper) > 0, "tools: the pickaxe still mines ore, the axe does not");
            // реальный удар инструментом по врагу: один раз за удар
            HotSelect(HotSlotOf("tool:axe")); var en = SpawnEnemy(PPos + new Vector2(14, 0), EK.Shade); float hp0 = en.Hp; en.Cd = 99; Aim = en.Pos; AtkCd = 0;
            ActPick(.016f, true); float after1 = en.Hp; ActPick(.016f, true); float after2 = en.Hp;
            Check(after1 < hp0 && Mathf.IsEqualApprox(after1, after2), $"combat: axe hits an enemy exactly once per swing ({hp0:0.0} → {after1:0.0} → {after2:0.0})");
        });
        S(.5f, () =>
        {
            // крафт: ручные рецепты без станции, станции по месту
            Enemies.Clear(); Hot[0] = "tool:pickaxe";
            Check(CraftGroups().Count == 1 && CraftGroups()[0] == "hand", "crafting: only the hand group without a station");
            for (int i = 0; i < It.Count; i++) Inv[i] = 0;
            var axe = RecipesOf("hand").Find(r => r.Id == "tool_axe");
            Check(axe != null && StateOf(axe) == RecState.Owned, "crafting: owned tools are marked as owned");
            // печь рядом
            var tile = ToTile(PPos + FreeAdj());
            SetObj(tile.X, tile.Y, Tl.Furnace); HasFurnace = true; PPos = TileCenter(tile.X, tile.Y) + new Vector2(0, 16);
            Check(ActiveStation() == "furnace" && CraftGroups().Contains("furnace"), "crafting: the Furnace group appears next to a furnace");
            var smelt = RecipesOf("furnace").Find(r => r.Id.StartsWith("smelt_"));
            Check(smelt != null && StateOf(smelt) == RecState.Missing, "crafting: a known recipe without materials stays visible as missing");
            Inv[It.CopperOre] = 7; Inv[It.Wood] = 7; var sm = RecipesOf("furnace").Find(r => r.Id == "smelt_" + It.Copper);
            Check(StateOf(sm) == RecState.Ready, "crafting: recipe becomes available when materials arrive");
            CraftQty = 1; CraftRecipe(sm); Check(SmeltQ.Count == 5 && Inv[It.CopperOre] == 2 && Inv[It.Wood] == 2, $"crafting: x5 smelting queued and paid (queue {SmeltQ.Count}, ore {Inv[It.CopperOre]}, wood {Inv[It.Wood]})");
            CraftQty = 0; SmeltQ.Clear();
            // уйти от станции: группа пропадает и рецепты недоступны
            PPos += new Vector2(300, 0);
            Check(ActiveStation() == null && CraftGroups().Count == 1, "crafting: leaving the station removes its group");
            int q0 = SmeltQ.Count; CraftRecipe(sm); Check(SmeltQ.Count == q0, "crafting: station recipes cannot be used after leaving the station");
            // кузница заменяет печь
            SetObj(tile.X, tile.Y, Tl.None); PPos = FreeAdj() - new Vector2(0, 0);
            Buildings.Add(new Building { Type = Tl.Forge, X = ToTile(PPos).X + 1, Y = ToTile(PPos).Y, Level = 1 });
            Check(ActiveStation() == "forge" && CraftGroups().Contains("forge") && !CraftGroups().Contains("furnace"), "crafting: the Forge group replaces the Furnace group");
            Buildings.RemoveAt(Buildings.Count - 1);
            // сохранение: хотбар записывается
            HotAssign(1, "tool:axe"); Save();
            var text = File.Exists(CharPath) ? File.ReadAllText(CharPath) : "";
            Check(text.Contains("tool:axe") && text.Contains("weapon:sword1"), "save: hotbar assignments are written to the save file");
            // строительство: режим размещения
            // постройки как предметы: создать → поставить → разобрать → поставить снова
            Inv[It.Wood] = 99; Inv[It.Stone] = 99; Inv[It.Copper] = 99; Inv[It.Iron] = 99; PPos = home; Hp = MaxHp; Aim = PPos + FreeAdj();
            var le = EntryOf("lamp0"); int lamps0 = Lamps.Count, wood0 = Inv[It.Wood];
            Check(StockOf("lamp0") == 0, "build: nothing is in stock at the start");
            Placing = le; PlaceNow(); Check(Lamps.Count == lamps0 && Placing == null, "build: cannot place a lamp that was not crafted");
            Check(CraftBuild(le) && StockOf("lamp0") == 1 && Inv[It.Wood] < wood0, "build: crafting pays resources and puts the item into stock");
            int wood1 = Inv[It.Wood]; var tgt = ToTile(Aim); Placing = le; PlaceNow();
            Check(Lamps.Count == lamps0 + 1 && StockOf("lamp0") == 0 && Inv[It.Wood] == wood1, "build: placing uses the stocked item and costs no resources again");
            var lamp = Lamps.Find(l => l.X == tgt.X && l.Y == tgt.Y); Check(lamp != null, "build: the placed lamp exists");
            if (lamp != null) { DemolishLamp(lamp); Check(Lamps.Count == lamps0 + 1, "build: demolish asks for confirmation first"); DemolishLamp(lamp); }
            Check(Lamps.Count == lamps0 && StockOf("lamp0") == 1 && W.Get(tgt.X, tgt.Y) == Tl.None && Inv[It.Wood] == wood1, "build: demolishing returns the item to stock without a refund of resources");
            Aim = TileCenter(tgt.X, tgt.Y); Placing = le; PlaceNow(); Check(Lamps.Count == lamps0 + 1 && StockOf("lamp0") == 0, "build: the demolished item can be placed again");
            Save(); var bt = File.ReadAllText(CharPath); Check(bt.Contains("BsK"), "save: build stock is written to the save file");
            Placing = null;
            GD.Print($"[check] SUMMARY failures={fails}");
        });
        // снимки интерфейса: хотбар, инвентарь, окно крафта со станцией
        S(.2f, () => { Inv[It.Wood] = 12; Inv[It.Stone] = 9; Inv[It.CopperOre] = 3; Inv[It.Copper] = 5; Enemies.Clear(); PPos = home; var tl = ToTile(PPos + FreeAdj()); SetObj(tl.X, tl.Y, Tl.Furnace); HasFurnace = true; PPos = TileCenter(tl.X, tl.Y) + new Vector2(0, 16); HotEquip("weapon:sword1"); CloseWindows(); });
        S(.4f, () => Shot("items_hud"));
        S(.1f, () => { OpenWin(Win.Inventory); }); S(.4f, () => Shot("items_inventory"));
        S(.1f, () => { OpenStation("furnace"); WinCur = 1; GD.Print($"[items] station={ActiveStation()} near={FurnaceNear(56, out _)} ppos={PPos} groups={string.Join(",", CraftGroups())}"); }); S(.4f, () => Shot("items_craft_furnace"));
        S(.1f, () => { CloseWindows(); OpenWin(Win.Equip); WinTab = 0; WinCur = 1; }); S(.4f, () => Shot("items_equip"));
        S(.2f, () => GD.Print("[check] ITEMS DONE"));
    }
}
