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
        S(.2f, () => { Check("hud: H cycles the mode", HudDensity.Next() == HudMode.Minimal && HudDensity.Next() == HudMode.Full && HudDensity.Next() == HudMode.Normal); Log("hud done"); GD.Print("[check] SUMMARY failures=" + checkFails); GD.Print("HUD DONE"); });
    }
}
