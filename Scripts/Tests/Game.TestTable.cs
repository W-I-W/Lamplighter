using Godot;
using System;
using System.Linq;

namespace Lamplighter;

/// <summary>Стол исследований (LAMP_STAGE=table): навыки покупаются только у стола, уровни стола открывают ступени исследований, кузница и печь навыков не учат,
/// награды стражей приходят сами, дерево в меню героя только для просмотра, стол сохраняется и общий для игроков (команды хосту идемпотентны).</summary>
public partial class Game
{
    void BuildTableScript()
    {
        int fails = 0; void Check(bool ok, string what) { if (!ok) fails++; GD.Print($"[check] {(ok ? "PASS" : "FAIL")} {what}"); }
        Building Table() => Buildings.Find(b => b.Type == Tl.ResearchTable);
        Vector2I FreeTile() { var t = W.Spawn + new Vector2I(2, 3); for (int r = 0; r < 12 && W.Get(t.X, t.Y) != Tl.None; r++) t += new Vector2I(1, 0); return t; }
        void Give(int[][] cost) { foreach (var c in cost) Inv[c[0]] = Math.Max(Inv[c[0]], c[1] + 5); }
        void Clean() { Window = Win.None; TechOpen = false; TechAtTable = false; }
        WorldSave ReadSave() => SaveManager.LoadWorld(GameSession.WorldId);
        S(.4f, () =>
        {
            Menu = Mn.None; noHeal = true; Hp = MaxHp; Invuln = 99f; Enemies.Clear(); TestNoSpawn = true; StrictResearch = true; Array.Clear(SkillLv); TechMask = 0; RecomputeEffects();
            for (int i = 0; i < It.Count; i++) Inv[i] = 0; Buildings.Clear();
            Check(BActive(ResearchBk) && BUnlocked(ResearchBk) && BNames[ResearchBk] == "Стол исследований", "table: the research table is an active building, available from the start");
            var cost = BCost(ResearchBk, 1); Check(cost.Length == 2 && cost.All(c => c[0] == It.Wood || c[0] == It.Stone), "table: level I needs only wood and stone");
            var e = BuildEntries().Find(x => x.Key == "bld" + ResearchBk); Check(e != null && e.Cat == 1 && e.Unlocked, "table: it sits in the basic build category and is unlocked");
            Check(!FreeResearch && TableLevel == 0 && !NearTable(), "table: strict mode — no table in the world yet");
            Check(!LearnTech((int)Tech.Spark) && !Has(Tech.Spark), "table: without a table the first light cannot be learned");
            Check(TableReason((int)Tech.Spark) == "Постройте Стол исследований [B]", "table: the reason says to build the table");
            Check(StateOfTech((int)Tech.Spark) == TechState.Locked, "table: the spark shows as locked while there is no table");
            var nd = World.StartNeeds(); Check(nd.trees >= 1 && nd.stones >= 1, $"table: the start clearing is guaranteed to hold the table and the spark ({nd.trees} trees, {nd.stones} stones)");
        });
        S(.3f, () =>
        {
            Give(BCost(ResearchBk, 1)); Inv[It.Wood] = 8; Inv[It.Stone] = 5; var tile = FreeTile(); Tp(TileCenter(tile.X, tile.Y) + new Vector2(0, 30)); Aim = TileCenter(tile.X, tile.Y);
            Inv[It.Wood] = 8; Inv[It.Stone] = 5; BuildBuilding(ResearchBk);
            var t = Table(); Check(t != null && t.Level == 1 && Inv[It.Wood] == 0 && Inv[It.Stone] == 0, "table: built from 8 wood and 5 stone, level I");
            BuildBuilding(ResearchBk); Check(Buildings.Count(b => b.Type == Tl.ResearchTable) == 1, "table: only one table per world");
            Check(AtlasX(Tl.ResearchTable, 1) == Art.ResTable && AtlasX(Tl.ResearchTable, 5) == Art.ResTable + 4, "table: five levels pick five atlas tiles");
            var fr = SpriteLibrary.TileFrames; Check(fr[Art.ResTable] == 2 && fr[Art.ResTable + 1] == 2 && fr[Art.ResTable + 2] == 3 && fr[Art.ResTable + 3] == 4 && fr[Art.ResTable + 4] == 4, "table: PNG sprites for all stages are loaded with their animation frames");
            var ai = Art.TileTex.GetImage(); bool all = true; for (int k = 0; k < 5; k++) { bool any = false; for (int y = 0; y < 16 && !any; y++) for (int x = 0; x < 16; x++) if (ai.GetPixel((Art.ResTable + k) * 16 + x, y).A > 0) { any = true; break; } all &= any; }
            Check(all, "table: every stage sprite has visible pixels in the atlas");
            Check(TableLevel == 1 && NearTable(), "table: standing next to it counts as being at the table");
        });
        S(.3f, () =>
        {
            // кузница и печь не учат навыкам: окно кузни — станция предметов, дерево не открывается
            var fg = new Building { Type = Tl.Forge, X = Table().X + 3, Y = Table().Y, Level = 1 }; Buildings.Add(fg); Tp(fg.Pos + new Vector2(0, 14));
            var ix = new Ix(); BuildingIx(fg, ix); Check(ix.E != null, "forge: the forge offers its station");
            ix.E(); Check(Window == Win.Equip && CraftStation == "forge" && !TechOpen && !TechAtTable, "forge: [E] opens the item crafting station, not the skill tree");
            Check(!EquipTabs.Any(n => n.Contains("Навык") || n.Contains("Исслед")), "forge: its window has only item recipes and no skill or research tabs");
            Clean(); Buildings.Remove(fg); SetObj(fg.X, fg.Y, Tl.None);
            // T — только просмотр
            Tp(Table().Pos + new Vector2(0, 14)); OpenTech(true); Check(TechOpen && !TechAtTable, "view: [T] opens the tree as view-only");
            Give(LevelCost((int)Tech.Spark, 1)); TechSel = (int)Tech.Spark; TechKey(Key.Enter); upgradeWindow.ClickLearn(); Check(!Has(Tech.Spark), "view: Enter and the Learn click in the character menu cannot buy skills (even next to the table)");
            Check(!Has(Tech.Spark), "view: nothing was bought through the character menu"); Clean();
        });
        S(.5f, () =>
        {
            // у стола: Искра света за 3 дерева и 2 камня
            var ix = new Ix(); BuildingIx(Table(), ix); Check(ix.E != null, "table: the table offers [E] research");
            for (int i = 0; i < It.Count; i++) Inv[i] = 0; Inv[It.Wood] = 3; Inv[It.Stone] = 2; ix.E();
            Check(TechOpen && TechAtTable, "table: [E] opens the research window at the table");
            TechPage = 0; TechSel = (int)Tech.Spark;
        });
        S(.5f, () =>
        {
            Check(upgradeWindow.Details.LearnEnabled, "table: the Learn button is enabled for the spark at the table");
            upgradeWindow.ClickLearn(); Check(Has(Tech.Spark) && HasHandLamp && Inv[It.Wood] == 0 && Inv[It.Stone] == 0, "table: the spark costs exactly 3 wood and 2 stone and gives the first light");
            Clean();
        });
        S(.3f, () =>
        {
            // у каждого навыка свои условия: Линза — печь, Рывок — кирка и ступень II, Широкий замах — кузница
            Tp(Table().Pos + new Vector2(0, 14)); Give(LevelCost((int)Tech.Lens, 1)); HasFurnace = false;
            Check(!LearnTech((int)Tech.Lens), "skill: the lens still needs a furnace even at the table"); HasFurnace = true;
            Check(LearnTech((int)Tech.Lens) && Has(Tech.Lens), "skill: with a furnace and materials the lens is learned at the level I table");
            Tier = 1; Give(LevelCost((int)Tech.Dash, 1));
            Check(TableReq((int)Tech.Dash) == 2 && !LearnTech((int)Tech.Dash) && TableReason((int)Tech.Dash) == "Улучшите Стол исследований до 2 уровня", "skill: the dash needs a level II table — the reason is shown");
            Check(StateOfTech((int)Tech.Dash) == TechState.Locked, "skill: the dash is locked at table level I");
            // улучшение стола до II: предметы кузницы
            var up = BCost(ResearchBk, 2); Check(up.Any(c => c[0] == It.Gear), "table: level II needs a forge-made item (gears)");
            foreach (var c in up) Inv[c[0]] = 0; UpgradeBuilding(Table()); Check(Table().Level == 1, "table: without the materials it is not upgraded");
            Give(up); UpgradeBuilding(Table()); Check(Table().Level == 2, "table: with the materials it becomes level II");
            Give(LevelCost((int)Tech.Dash, 1)); Check(LearnTech((int)Tech.Dash) && Has(Tech.Dash), "skill: at level II the dash is learned (pickaxe tier + materials)");
            Tier = 0; Array.Clear(SkillLv); SkillLv[(int)Tech.Spark] = 1; RecomputeEffects(); Give(LevelCost((int)Tech.Dash, 1));
            Check(!LearnTech((int)Tech.Dash) || Tier >= 1, "skill: without the copper pickaxe the dash is not learned");
            Tier = 1; HasFurnace = true;
        });
        S(.3f, () =>
        {
            // ступени III–V требуют биом и материалы; уровни стола не выдают навыки сами
            var tb = Table(); foreach (var a in W.Ancients) a.Lit = false;
            Give(BCost(ResearchBk, 3)); UpgradeBuilding(tb); Check(tb.Level == 2 && BuildUpBlock(tb) == "Зажгите древний фонарь в шахтах", "table: level III needs a lantern lit in the mines");
            foreach (var a in W.Ancients) if (a.Region == 1 && !a.Boss && a != W.Ancients[0]) { a.Lit = true; a.Anim = 1; break; }
            Give(BCost(ResearchBk, 3)); UpgradeBuilding(tb); Check(tb.Level == 3 && BuildUpBlock(tb) == "Зажгите древний фонарь в пустошах", "table: after the mines lantern it reaches level III, level IV waits for the wastes");
            Check(!Has(Tech.Optics) && !Has(Tech.SilverEdge), "table: a higher level does not hand out skills by itself");
            Check(Techs.Select((_, i) => i).Where(i => !Retired(i) && !Techs[i].Reward).All(i => TableReq(i) >= 1 && TableReq(i) <= 5), "skills: every active skill has a reachable table level (1..5)");
            Check(Techs.Select((_, i) => i).Where(i => Techs[i].Reward).All(i => TableReq(i) == 1), "skills: boss rewards need no table level (only later levels use a table)");
            int[] perLevel = new int[6]; for (int i = 0; i < Techs.Length; i++) if (!Retired(i) && !Techs[i].Reward) perLevel[TableReq(i)]++;
            Check(perLevel[1] > 0 && perLevel[2] > 0 && perLevel[3] > 0 && perLevel[4] > 0 && perLevel[5] > 0, $"skills: each table level opens its own research ({string.Join("/", perLevel.Skip(1))})");
            Check(TableReq((int)Tech.Optics) == 3 && TableReq((int)Tech.FlameEdge) == 4 && TableReq((int)Tech.FrostEdge) == 5, "skills: optics = III, ash = IV, frost = V");
            // региональные категории — по зажжённому древнему фонарю
            Upgrades.Update(false); Check(Upgrades.IsPageUnlocked(1) && !Upgrades.IsPageUnlocked(2) && !Upgrades.IsPageUnlocked(3), "categories: Depths opened by the lit lantern, Ash and Ice still closed");
            Give(LevelCost((int)Tech.Optics, 1)); Inv[It.Crystal] = 99; Check(LearnTech((int)Tech.Optics) || !Techs[(int)Tech.Optics].Ok(this), "skill: optics is learned at level III once its own condition holds");
            var ctr = W.Ancients[0]; bool was = ctr.Lit; foreach (var a in W.Ancients) if (a.Region == 2) a.Lit = false; ctr.Lit = true; Upgrades.Update(false);
            Check(!BiomeDiscovered(2), "categories: the Central Beacon does not count as a discovered biome"); ctr.Lit = was;
        });
        S(.3f, () =>
        {
            // награды стражей: сразу, без стола; дальнейшие уровни — у стола
            Array.Clear(SkillLv); TechMask = 0; Buildings.Clear(); RecomputeEffects();
            TechMask |= 1 << (int)Tech.Pulse; TechMask |= 1 << (int)Tech.Beam; TechMask |= 1 << (int)Tech.Fireproof; TechMask |= 1 << (int)Tech.Prism;
            Check(Has(Tech.Pulse) && Has(Tech.Beam) && Has(Tech.Fireproof) && Has(Tech.Prism), "rewards: boss rewards arrive with no research table at all");
            Check(!LearnTech((int)Tech.Pulse), "rewards: raising a reward level still needs a table");
        });
        S(.3f, () =>
        {
            // кооператив: стол общий, команды хоста идемпотентны
            var tile = FreeTile(); int bk = ResearchBk; Check(NetBuildCmd(1, bk, tile.X, tile.Y) == null && Table() != null && Table().Level == 1, "coop: a host builds the table for a client's command");
            var again = NetBuildCmd(1, bk, tile.X + 1, tile.Y); Check(again != null && Buildings.Count(b => b.Type == Tl.ResearchTable) == 1, "coop: a repeated build request is refused and refunded (no second table)");
            int key = BKey(Table()); Check(NetBuildCmd(2, key, 1, bk) == null && Table().Level == 2, "coop: the host upgrades the table from level I to II once");
            var dup = NetBuildCmd(2, key, 1, bk); Check(dup != null && Table().Level == 2, "coop: a duplicated upgrade request is refunded and does not skip a level");
            foreach (var a in W.Ancients) a.Lit = false; var blocked = NetBuildCmd(2, key, 2, bk); Check(blocked != null && Table().Level == 2, "coop: the host refuses level III until the mines lantern is lit");
            Array.Clear(SkillLv); Give(LevelCost((int)Tech.Spark, 1)); Tp(Table().Pos + new Vector2(0, 14)); Check(LearnTech((int)Tech.Spark) && Lv(Tech.Spark) == 1, "coop: another hero uses the same table; skills are the hero's own");
            Array.Clear(SkillLv); SkillLv[(int)Tech.Spark] = 1; SkillLv[(int)Tech.Lens] = 3; Buildings.Clear(); RecomputeEffects();
            Check(Has(Tech.Spark) && Lv(Tech.Lens) == 3 && HasHandLamp, "coop: learned skills keep working in a world with no table");
        });
        S(.3f, () =>
        {
            // сохранение: стол и его уровень в мире
            var tile = FreeTile(); CreateBuilding(ResearchBk, tile); Table().Level = 3; Save(); var d = ReadSave();
            int idx = Array.IndexOf(d.BT, (int)Tl.ResearchTable); Check(idx >= 0 && d.BL[idx] == 3, "save: the table and its level are stored in the world");
            Check(d.BX[idx] == tile.X && d.BY[idx] == tile.Y, "save: the table keeps its place");
        });
        S(.4f, () =>
        {
            // окно у стола закрывается, когда герой уходит
            Tp(Table().Pos + new Vector2(0, 14)); Upgrades.Update(false); OpenTech(true, true); Check(TechOpen && TechAtTable, "ui: the research window opens at the table"); Check(TechKey(Key.E) && !TechOpen, "ui: E closes the research window opened by E"); OpenTech(true, true);
        });
        S(.9f, () =>
        {
            Shot("table_01_window"); Tp(Table().Pos + new Vector2(400, 0)); TickResearch(); Check(!TechOpen, "ui: walking away closes the research window");
            Tp(Table().Pos + new Vector2(0, 14)); OpenTech(true);
        });
        S(.9f, () => { Shot("table_02_view"); });
        S(.4f, () =>
        {
            // окно сундука из ячеек: сундук сверху, инвентарь снизу, щелчок переносит стопку
            Clean(); StrictResearch = false; var ct = W.Spawn + new Vector2I(-3, 2); for (int r = 0; r < 8 && W.Get(ct.X, ct.Y) != Tl.None; r++) ct += new Vector2I(-1, 0);
            SetObj(ct.X, ct.Y, Tl.StorageChest); Tp(TileCenter(ct.X, ct.Y) + new Vector2(0, 20)); for (int i = 0; i < It.Count; i++) Inv[i] = 0; Inv[It.Wood] = 150; Inv[It.Stone] = 30;
            OpenChest(ct.X, ct.Y); Check(Window == Win.Chest && ChestStacks().Count == ChestSlots && ChestStacks().TrueForAll(x => x.item < 0), "chest window: an empty chest is twenty empty cells");
            ChestClick(false, 0, false, false); var st = ChestStacks(); Check(st[0].item >= 0 && st[0].n > 0 && Inv[st[0].item] < (st[0].item == It.Wood ? 150 : 30), "chest window: a click on an inventory cell moves that stack into the chest");
            int firstCount = st[0].n; ChestClick(true, 0, true, false);
            Check(ChestStacks()[0].n == firstCount / 2, "chest window: right click takes half back");
            WinTab = 0; WinCur = 0; Check(ChestNav(Key.Up, false) && WinTab == 1, "chest window: Up from the top inventory row enters the chest grid"); Check(ChestNav(Key.Down, false) && WinTab == 0, "chest window: Down from the chest grid returns to the inventory");
            Inv[It.Wood] = 120; ChestMove(true, It.Wood, 120);
            for (int qi = 0; qi < QuickSlotCount; qi++) ClearQuickSlot(qi);
            { int wc = ChestStacks().FindIndex(x => x.item == It.Wood); Check(wc >= 0, "chest window: wood is in the chest");
              ShiftChestOut(wc, 1); Check(QuickStack(0) == (It.Wood, 1), "chest window: Ctrl+Shift moves one item into the hotbar");
              ShiftChestOut(wc, 5); Check(QuickStack(0) == (It.Wood, 6), "chest window: Shift moves the stack into the hotbar first, adding to the same stack"); }
        });
        S(.9f, () => { Shot("table_03_chest"); Check(chestWindow.Visible, "chest window: the cell window is visible"); Check(WindowKey(Key.E, false) && Window == Win.None, "chest window: E closes the window opened by E"); CloseWindows(); });
        S(.3f, () =>
        {
            // найденный в мире сундук — обычный: добыча внутри, при поломке всё выпадает
            var poi = W.Pois.Find(q => q.Kind == PK.Chest && q.Variant == 0 && W.Get(q.Tile.X, q.Tile.Y) == Tl.Chest);
            Check(poi != null, "world chest: the world has a found chest"); if (poi == null) return;
            for (int i = 0; i < It.Count; i++) Inv[i] = 0;
            Tp(poi.Pos + new Vector2(0, 14)); OpenChest(poi);
            Check(W.Get(poi.Tile.X, poi.Tile.Y) == Tl.StorageChest && Window == Win.Chest && !ChestEmpty(poi.Tile.X, poi.Tile.Y), "world chest: opening a found chest turns it into an ordinary chest with the loot inside");
            Check(Inv[It.Wood] == 0 && Inv[It.Copper] == 0, "world chest: the loot is not given automatically");
            var tile = poi.Tile; CloseWindows(); BreakTile(tile, Tl.StorageChest, false);
            Check(W.Get(tile.X, tile.Y) != Tl.StorageChest && ChestEmpty(tile.X, tile.Y) && Inv[It.Wood] > 0, "world chest: a broken chest drops everything it held");
        });
        S(.5f, () =>
        {
            Check(!upgradeWindow.Details.LearnEnabled, "view: the character-menu tree shows no Learn button");
            Clean(); StrictResearch = false; GD.Print($"[check] SUMMARY failures={fails}");
        });
    }
}
