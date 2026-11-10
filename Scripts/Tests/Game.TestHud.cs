using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lamplighter;

/// <summary>Проверка HUD (LAMP_STAGE=hud): обычный, минимальный и полный режимы, сворачивание подсказки, одно событие, ограничение меток, временные ресурсы, уведомления. Скриншоты — по режимам.</summary>
public partial class Game
{
    void BuildHudScript()
    {
        S(.3f, () => { HudDensity.Mode = HudMode.Normal; Inv[It.Wood] = 0; Inv[It.Stone] = 0; Check("hud: normal mode is the default", HudDensity.Mode == HudMode.Normal); });
        S(.5f, () => Shot("hud1_normal_start"));
        S(.1f, () =>
        {
            
            Inv[It.Wood] += 3; Toast("Новый рецепт: оружие"); Announce("Событие: Заблудший путник", "Загляните к нему", new Color("#8ff0ff"));
            Events.Clear(); Events.Add(new GEvent { Kind = "traveler", Pos = PPos + new Vector2(40, 30), Life = 60 }); Events.Add(new GEvent { Kind = "traveler", Pos = PPos + new Vector2(300, 90), Life = 60 });
        });
        S(1.2f, () => { });
        S(45f, () => Shot("hud2_normal_notes"));
        S(.1f, () => { Log($"hud t={UiTime:F1}"); Check("hud: tutorial hint collapsed to one line after a few seconds", GameHudNode.GoalCollapsed);  Shot("hud3_normal_collapsed"); });
        S(.1f, () => { GameHudNode.GoalsOpen = false; GameHudNode.ClickGoalForTest(); Check("hud: a mouse click on the goal block expands the details", GameHudNode.GoalsOpen); GameHudNode.ClickGoalForTest(); Check("hud: a second click collapses them", !GameHudNode.GoalsOpen); GameHudNode.ClickGoalForTest(); });
        S(.4f, () => { Check("hud: [G] expands details", !GameHudNode.GoalCollapsed); Shot("hud4_normal_expanded"); GameHudNode.GoalsOpen = false; HudDensity.Mode = HudMode.Minimal; });
        S(.5f, () => { Check("hud: minimal hides the region block", !GameHudNode.RegionVisible); Shot("hud5_minimal"); HudDensity.Mode = HudMode.Full; });
        S(.5f, () => { Check("hud: full shows the debug line", GameHudNode.DebugVisible); Shot("hud6_full"); HudDensity.Mode = HudMode.Normal; Events.Clear(); });
        S(.2f, () => { OpenWin(Win.Equip); });
        S(.6f, () => { Check("hud: the equipment window ([C]) fills in on a fresh hero", equipWindow.CardCount > 0); Shot("hud7_equip_fresh"); CloseWindows(); });
        S(.1f, () => { var an = W.Ancients.Find(x => x.Region == 1 && !x.Boss); if (an != null) Tp(an.Pos + new Vector2(0, 40)); });
        S(1.6f, () => { });
        S(.3f, () => { Check("hud: walking into another location replaces the title with the new name", RegionTitle.Showing && RegionTitle.Text == Loc.T(RegionNames[1])); Shot("hud8_region_title"); Tp(TileCenter(W.Spawn.X, W.Spawn.Y)); });
        S(.2f, () => { Inv[It.Stone] = 3; Inv[It.Wood] = 0; TestOpenBench(); });
        int benchFurnace = 0, benchHot = 0; BuildEntry benchFe = null;
        S(.6f, () =>
        {
            var l = BenchEntries(); Check("bench: the window opens with a short list", benchWindow.Visible && l.Count > 0 && l.Count <= 4 + 3 + 24);
            Check("bench: quick craft lists only affordable recipes on the first tab", BenchRecipes(false).TrueForAll(x => x.times > 0) && BenchRecipes(true).Count == l.Count);
            Check("bench: at most three locked entries are shown", l.FindAll(e => !e.Unlocked).Count <= 3);
            benchFe = l.Find(e => e.Key == "furnace"); benchFurnace = StockOf("furnace");
            Check("bench: with empty cells nothing is created", !BenchCraftFromCells(false) && StockOf("furnace") == benchFurnace);
            Check("bench: with not enough resources a recipe click lays nothing out", !BenchLayout(benchFe, false) && CraftIngredient(0).n == 0);
            Shot("bench1_short");
            Inv[It.Stone] = 60; Inv[It.Wood] = 60; SyncInventorySlots();
        });
        S(.4f, () => { Check("bench: clicking a recipe lays its components out in the cells", BenchLayout(benchFe, false) && CraftIngredient(0) == (It.Stone, 12) && MatchBench().State == BenchState.Ready && Inv[It.Stone] == 48); });
        S(.4f, () =>
        {
            var tb = BenchEntries().Find(e => e.Key == "bld" + ResearchBk);
            if (tb != null)
            {
                ReturnCellsOf(benchCells, true, BenchInputs); var invKeep = (int[])Inv.Clone(); foreach (var c in BCost(ResearchBk, 1)) Inv[c[0]] += c[1];
                bool lay = BenchLayout(tb, false); var mm = MatchBench();
                Check("bench: the research table recipe lays out and matches the table, not the workbench", lay && mm.Entry?.Key == tb.Key && mm.State == BenchState.Ready);
                ReturnCellsOf(benchCells, true, BenchInputs); Array.Copy(invKeep, Inv, invKeep.Length); SyncInventorySlots(); BenchLayout(benchFe, false);
            }
        });
        S(.4f, () => { Shot("bench2_ready"); RealClick(benchWindow.Result, true); });
        S(.3f, () =>
        {
            Check("bench: clicking the result spends the cells and puts the building in the hand, not in the bag", CraftIngredient(4).n == 1 && CraftIngredient(0).n == 0 && StockOf("furnace") == benchFurnace && invWindow.GetNode<UiSlot>("CarryPreview").Visible);
            benchHot = Enumerable.Range(0, QuickSlotCount).First(i => QuickStack(i).n == 0); RealClick(invWindow.HotbarSlot(benchHot), true);
        });
        S(.3f, () =>
        {
            Check("bench: the player places the taken result into a hotbar cell", CraftIngredient(4).n == 0 && StockOf("furnace") == benchFurnace + 1 && QuickStack(benchHot).n == 1);
            int si = InventorySlots().ToList().FindIndex(x => x.item == It.Stone); RealClick(benchWindow.TestBagSlot(si), true);
        });
        S(.3f, () => { RealClick(benchWindow.TestCell(1), true); });
        S(.3f, () =>
        {
            Check("bench: a stack from the bag is carried into a component cell like in the inventory", CraftIngredient(1) == (It.Stone, 48) && Inv[It.Stone] == 0);
            Shot("bench3_stock"); CloseWindows();
            Check("bench: closing the window returns the cells to the bag", CraftIngredient(1).n == 0 && Inv[It.Stone] == 48);
            Placing = EntryOf("furnace");
        });
        S(.5f, () => { Check("bench: in placement mode the window is closed and one short hint remains", Placing != null && !benchWindow.Visible && PlaceHintVisible); Shot("bench4_placing"); Placing = null; });
        S(.2f, () => { Check("hud: H cycles the mode", HudDensity.Next() == HudMode.Minimal && HudDensity.Next() == HudMode.Full && HudDensity.Next() == HudMode.Normal); Log("hud done"); GD.Print("[check] SUMMARY failures=" + checkFails); GD.Print("HUD DONE"); });
    }
}
