using Godot;
using System;

namespace Lamplighter;

/// <summary>Снимки интерфейса (LAMP_STAGE=ui, с сохранением mid: LAMP_LOAD=1). Проверка окон и HUD в разных разрешениях.</summary>
public partial class Game
{
    void BuildUiScript()
    {
        void Win1(Win w, int tab = 0, string name = null) { S(.2f, () => { CloseWindows(); Window = w; WinTab = tab; WinCur = 0; }); S(.5f, () => Shot(name ?? ("ui_" + w + tab))); }
        S(.3f, () =>
        {
            Inv[It.Coins] = 120; Inv[It.Core] = 0; Tp(PPos); Hp = MaxHp * .8f; noHeal = true; Menu = Mn.None; TestAim(new Vector2(900, 900));
            var wh = Buildings.Find(b => b.Type == Tl.Warehouse); if (wh != null) { PPos = wh.Pos + new Vector2(0, 22); cam.ResetSmoothing(); }
            for (int i = 0; i < 3; i++) Toast("Тестовое уведомление " + i); Announce("Открыта постройка: Лесопилка", "Производит дерево само [5]", new Color("#ffe27a"), "tech");
        });
        S(1.6f, () => { aimOverride = Aim = PPos + new Vector2(0, -22); });
        S(.8f, () => Shot("ui_hud"));
        Win1(Win.Build, 0, "ui_build");
        S(.2f, () => { BuildCat = 2; WinCur = 1; }); S(.5f, () => Shot("ui_build2")); S(.1f, () => BuildCat = 0);
        Win1(Win.Inventory, 0, "ui_inventory");
        for (int t = 0; t < 4; t++) Win1(Win.Equip, t);
        for (int t = 0; t < 3; t++) Win1(Win.Journal, t);
        Win1(Win.Prod); Win1(Win.Trade);
        S(.2f, () => { CloseWindows(); TechOpen = true; TechPage = 0; }); S(.5f, () => Shot("ui_tech"));
        S(.2f, () => { CloseWindows(); MapOpen = true; }); S(.5f, () => Shot("ui_map"));
        S(.2f, () => { CloseWindows(); OpenStory("Фонарщик", string.Join("\n\n", Story.Prologue), "Пролог"); }); S(.9f, () => Shot("ui_story_typing")); S(.2f, () => hud.StorySkip()); S(.6f, () => Shot("ui_story"));
        S(.2f, () => { CloseWindows(); OpenMenu(Mn.Pause); }); S(.5f, () => Shot("ui_pause"));
        S(.2f, () => { MenuBack = Mn.Pause; OpenMenu(Mn.Settings); }); S(.5f, () => Shot("ui_settings"));
        S(.2f, () => { Menu = Mn.None; Window = Win.Rift; WinTab = 0; Finished = true; }); S(.5f, () => Shot("ui_rift"));
        S(.2f, () => { CloseWindows(); OpenMenu(Mn.Main); }); S(.5f, () => Shot("ui_main"));
        S(.2f, () => { Menu = Mn.None; Settings.Lang = "en"; Loc.SetLang("en"); }); S(.8f, () => Shot("ui_hud_en"));
        S(.2f, () => GD.Print("[check] UI SCREENSHOTS DONE"));
    }
}
