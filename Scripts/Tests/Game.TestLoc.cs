using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Проход по всем окнам и меню для проверки перевода (LAMP_STAGE=loc, LAMP_LANG=en).</summary>
public partial class Game
{
    void BuildLocScript()
    {
        void Open(Win w, int tab = 0) { CloseWindows(); Window = w; WinTab = tab; WinCur = 0; }
        S(.3f, () =>
        {
            BossMask |= 15; BeaconLevel = 5; Finished = true; Level = 8; Tier = 2; Inv[It.Coins] = 900; for (int i = 0; i < Inv.Length; i++) Inv[i] += 12;
            GiveRelic("r_prismcore", true); GiveRelic("r_embersp", true); GiveRelic("r_frostshard", true); GiveRelic("r_beaconwick", true);
            GiveWeapon("sword1", false); GiveWeapon("bow1", false); GiveWeapon("staff1", false); ModInv[0] = 1; ModInv[3] = 1;
            UnlockBp("bp_hub"); AddFragment("bp_refinery");
            Buildings.Add(new Building { Type = Tl.Sawmill, X = W.Spawn.X + 4, Y = W.Spawn.Y - 4, Level = 2 });
            Buildings.Add(new Building { Type = Tl.Stall, X = W.Spawn.X + 7, Y = W.Spawn.Y - 4, Level = 2 });
            Villagers.Add(new Villager { Pos = PPos });
            RecomputeEffects(); Toast("Translation test");
        });
        for (int t = 0; t < 4; t++) { int tt = t; S(.25f, () => Open(Win.Equip, tt)); }
        for (int t = 0; t < 2; t++) { int tt = t; S(.25f, () => Open(Win.Journal, tt)); }
        S(.25f, () => Open(Win.Prod));
        S(.25f, () => Open(Win.Build));
        S(.25f, () => { CloseWindows(); OpenTrade(null); });
        S(.25f, () => { TradeTab = 1; });
        for (int p = 0; p < 4; p++) { int pp = p; S(.25f, () => { foreach (var an in W.Ancients) if (an.Region >= 1 && an.Region <= 3 && !an.Boss) an.Lit = true; Upgrades.Update(false); OpenTech(true); TechPage = pp; TechSel = -1; }); }
        S(.25f, () => { CloseWindows(); MapOpen = true; });
        S(.25f, () => { CloseWindows(); var p = W.Pois.Find(x => x.Kind == PK.Lore); if (p != null) ReadLore(p); });
        S(.25f, () => { CloseWindows(); Window = Win.Rift; WinTab = 0; });
        S(.25f, () => { WinTab = 1; });
        S(.25f, () => { WinTab = 2; });
        foreach (var m in new[] { Mn.Main, Mn.Pause, Mn.Settings, Mn.Keys, Mn.Achv, Mn.Credits })
        { var mm = m; S(.25f, () => { CloseWindows(); MenuBack = Mn.Pause; OpenMenu(mm); }); }
        S(.25f, () => { AskConfirm("Загрузить? Несохранённое пропадёт.", () => { }, Mn.Pause); });
        S(.25f, () => { Menu = Mn.None; FinStage = 2; FinScene = 1; });
        S(.25f, () => { FinStage = 3; FinT = 3; });
        S(.25f, () => { FinStage = 0; Menu = Mn.None; Announce("Разлом пройден! Уровень 2", "Осколков разлома: +7", Colors.White); });
        S(.25f, () => { });
    }
}
