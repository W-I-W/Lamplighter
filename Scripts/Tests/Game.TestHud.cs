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
        S(.1f, () => { GameHudNode.GoalsOpen = true; });
        S(.4f, () => { Check("hud: [G] expands details", !GameHudNode.GoalCollapsed); Shot("hud4_normal_expanded"); GameHudNode.GoalsOpen = false; HudDensity.Mode = HudMode.Minimal; });
        S(.5f, () => { Check("hud: minimal hides the region block", !GameHudNode.RegionVisible); Shot("hud5_minimal"); HudDensity.Mode = HudMode.Full; });
        S(.5f, () => { Check("hud: full shows the debug line", GameHudNode.DebugVisible); Shot("hud6_full"); HudDensity.Mode = HudMode.Normal; Events.Clear(); });
        S(.2f, () => { OpenWin(Win.Equip); });
        S(.6f, () => { Check("hud: the equipment window ([C]) fills in on a fresh hero", equipWindow.CardCount > 0); Shot("hud7_equip_fresh"); CloseWindows(); });
        S(.1f, () => { var an = W.Ancients.Find(x => x.Region == 1 && !x.Boss); if (an != null) Tp(an.Pos + new Vector2(0, 40)); });
        S(1.6f, () => { });
        S(.3f, () => { Check("hud: walking into another location replaces the title with the new name", RegionTitle.Showing && RegionTitle.Text == Loc.T(RegionNames[1])); Shot("hud8_region_title"); Tp(TileCenter(W.Spawn.X, W.Spawn.Y)); });
        S(.2f, () => { Inv[It.Stone] = 3; Inv[It.Wood] = 0; OpenWin(Win.Build); });
        S(.6f, () =>
        {
            var l = BuildEntriesFiltered(); Check("build: the compact window opens with a short list", buildMenu.PanelVisible && l.Count > 0 && l.Count <= 4 + 3 + 24);
            Check("build: at most three locked entries are shown", l.FindAll(e => !e.Unlocked).Count <= 3);
            Check("build: with not enough resources the action line has no create command", buildMenu.ActionText.StartsWith("Tab"));
            Shot("build1_short");
            Inv[It.Stone] = 60; Inv[It.Wood] = 60; BuildCat = 0; WinCur = l.FindIndex(e => e.Key == "furnace");
        });
        S(.5f, () => { Check("build: when resources suffice the line offers Enter — create", buildMenu.ActionText.StartsWith("Enter — создать")); Shot("build2_ready"); PressWindowKey(Key.Enter); });
        S(.4f, () => { Check("build: after crafting the line offers placing and one more", buildMenu.ActionText.Contains("Enter — поставить")); Shot("build3_stock"); PressWindowKey(Key.Enter); });
        S(.5f, () => { Check("build: in placement mode the window is closed and one short hint remains", Placing != null && !buildMenu.PanelVisible && buildMenu.PlaceHintVisible); Shot("build4_placing"); Placing = null; });
        S(.2f, () => { Check("hud: H cycles the mode", HudDensity.Next() == HudMode.Minimal && HudDensity.Next() == HudMode.Full && HudDensity.Next() == HudMode.Normal); Log("hud done"); GD.Print("[check] SUMMARY failures=" + checkFails); GD.Print("HUD DONE"); });
    }
}
