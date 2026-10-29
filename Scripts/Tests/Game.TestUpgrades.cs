using Godot;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Lamplighter;

/// <summary>Проверки окна улучшений и открытия категорий (LAMP_STAGE=upgrades): только «Основные» в начале, открытие по реальному обнаружению биома, порядок вкладок, сохранение,
/// старые сохранения, состояния карточек, мышь и клавиатура, снимки интерфейса.</summary>
public partial class Game
{
    void BuildUpgradesScript()
    {
        int fails = 0; int fired = 0;
        void Check(bool ok, string what) { if (!ok) fails++; GD.Print($"[check] {(ok ? "PASS" : "FAIL")} {what}"); }
        string Ids() => string.Join(",", Upgrades.Visible().Select(c => c.Id));
        Ancient Lantern(int region) { foreach (var a in W.Ancients) if (a.Region == region && !a.Boss && a != W.Ancients[0]) return a; return null; }
        void Light(int region) { var a = Lantern(region); if (a != null) { a.Lit = true; a.Anim = 1; } }
        WorldSave ReadSave() => SaveManager.LoadWorld(GameSession.WorldId);
        S(.4f, () =>
        {
            Menu = Mn.None; noHeal = true; Hp = MaxHp; Invuln = 99f; Enemies.Clear(); Upgrades.CategoryUnlocked += _ => fired++;
            Check(Ids() == "basic", $"upgrades: a new game shows only the Basic category ({Ids()})");
            Check(Upgrades.All.Count == 4, "upgrades: the four categories come from data files");
            // обнаружение — это зажжённый фонарь, а не генерация, загрузка или близость
            Check(!BiomeDiscovered(1) && !BiomeDiscovered(2) && !BiomeDiscovered(3), "upgrades: no biome is discovered by world generation");
            var a1 = Lantern(1); Check(a1 != null, "upgrades: the flooded mines have an ancient lantern");
            if (a1 != null) { Tp(a1.Pos + new Vector2(0, 40)); }
        });
        S(1.2f, () =>
        {
            Check(Ids() == "basic" && fired == 0, "upgrades: standing next to an unlit lantern (chunks loaded) does not open a category");
            Light(1);
        });
        S(.8f, () =>
        {
            Check(Ids() == "basic,depths" && fired == 1, $"upgrades: lighting a lantern in the flooded mines opens Depths ({Ids()}, events {fired})");
            Check(Upgrades.IsNew("depths") && TabAnim.Contains("depths"), "upgrades: the new category is marked new and queued for the tab animation");
            Check(Notify.History.Exists(m => m.Text.StartsWith(Loc.T("Открыта категория"))), "upgrades: the log announces the opened category");
            Light(3);
        });
        S(.8f, () =>
        {
            Check(Ids() == "basic,depths,ice" && fired == 2, $"upgrades: Ice opens before Ash and the order stays fixed ({Ids()})");
            Light(2);
        });
        S(.8f, () =>
        {
            Check(Ids() == "basic,depths,ash,ice" && fired == 3, $"upgrades: all four categories are open in the data order ({Ids()})");
            // состояния улучшений
            var basic = PageList(0); Check(basic.Count == 18, $"upgrades: a category shows all of its upgrades, even unavailable ones ({basic.Count})");
            int lens = (int)Tech.Lens; HasFurnace = false; Check(StateOfTech(lens) == TechState.Locked, "upgrades: unmet condition → Locked");
            HasFurnace = true; SetSkillLevel((int)Tech.Spark, 1); for (int i = 0; i < It.Count; i++) Inv[i] = 0; Check(StateOfTech(lens) == TechState.Missing, "upgrades: condition met, no materials → Missing (still visible)");
            foreach (var cc in LevelCost(lens, 1)) Inv[cc[0]] = cc[1]; Check(StateOfTech(lens) == TechState.Available, "upgrades: condition met and materials in place → Available");
            TechPage = 0; Check(LearnTech(lens) && Has(Tech.Lens) && Lv(Tech.Lens) == 1 && Inv[It.Copper] == 0 && Inv[It.Shard] == 0, "upgrades: learning pays the cost of level 1 and grants exactly one level");
            Check(StateOfTech(lens) == TechState.Missing && !LearnTech(lens) && Lv(Tech.Lens) == 1, "upgrades: level 2 needs its own materials and is not bought without them");
            SetSkillLevel(lens, 5); Check(StateOfTech(lens) == TechState.Owned && !LearnTech(lens) && Lv(Tech.Lens) == 5, "upgrades: a maxed upgrade is Owned and cannot be bought again"); SetSkillLevel(lens, 0);
            Check(StateOfTech((int)Tech.Pulse) != TechState.Available, "upgrades: a boss reward cannot be bought");
            // сохранение: открытые вкладки и непросмотренные
            Save(); var d = ReadSave();
            Check(d.UpCats != null && d.UpCats.Contains("depths") && d.UpCats.Contains("ash") && d.UpCats.Contains("ice") && d.UpCats.Contains("basic"), "save: opened categories are written");
            Check(d.UpNew != null && d.UpNew.Contains("depths"), "save: the not-yet-viewed flag is written");
            var s2 = new UpgradeProgressionSystem(this); int fired2 = 0; s2.CategoryUnlocked += _ => fired2++; s2.Restore(d.UpCats, d.UpNew);
            Check(string.Join(",", s2.Visible().Select(c => c.Id)) == "basic,depths,ash,ice" && fired2 == 0, "save: loading restores the tabs without repeating notifications");
            Check(s2.IsNew("depths"), "save: the new-category mark survives a restart");
            // старые сохранения: без записи о категориях — только подтверждённый прогресс
            var old = new UpgradeProgressionSystem(this); old.Restore(null, null);
            Check(string.Join(",", old.Visible().Select(c => c.Id)) == "basic,depths,ash,ice", "old saves: lit lanterns in each biome restore the categories");
            foreach (var r in new[] { 1, 2, 3 }) { var a = Lantern(r); if (a != null) a.Lit = false; }
            var old2 = new UpgradeProgressionSystem(this); old2.Restore(null, null); Check(string.Join(",", old2.Visible().Select(c => c.Id)) == "basic", "old saves: with no confirmed progress only Basic is shown");
            TechMask |= 1 << (int)Tech.Boots; var old3 = new UpgradeProgressionSystem(this); old3.Restore(null, null);
            Check(string.Join(",", old3.Visible().Select(c => c.Id)) == "basic,depths", "old saves: an already learned upgrade restores its category and is kept");
            TechMask &= ~(1 << (int)Tech.Boots);
            foreach (var r in new[] { 1, 2, 3 }) Light(r);
            // смерть не закрывает категории
            Hp = 1; Invuln = 0; HurtPlayer(50, Vector2.Zero); Check(Ids() == "basic,depths,ash,ice", "upgrades: dying does not close categories");
            Invuln = 99f;
        });
        // интерфейс: окно, вкладки, клавиатура и мышь
        S(.3f, () =>
        {
            for (int i = 0; i < It.Count; i++) Inv[i] = 0; Inv[It.Copper] = 20; Inv[It.Shard] = 10; Inv[It.Iron] = 12; Inv[It.Wood] = 30; HasFurnace = true; Tier = 1; Kills = 9;
            OpenTech(true);
        });
        S(.5f, () =>
        {
            Check(upgradeWindow.Visible && upgradeWindow.TabCount == 4, $"ui: the window shows four tabs ({upgradeWindow.TabCount})");
            Check(upgradeWindow.NodeCount == PageList(TechPage).Count, $"ui: every upgrade of the category is a node ({upgradeWindow.NodeCount})");
            Check(upgradeWindow.LinkCount >= 8, $"ui: the tree draws dependency links ({upgradeWindow.LinkCount})");
            Check(TechSel == (int)Tech.Spark, "ui: the first root (Spark of Light) is selected on opening"); TechKey(Key.Down); Check(TechSel != (int)Tech.Spark, "ui: ↓ moves to the branch below");
            TechKey(Key.Up); Check(TechSel == (int)Tech.Spark, "ui: ↑ moves back up");
            TechKey(Key.Tab); Check(TechPage == 1, "ui: Tab switches to the next category");
            Check(!Upgrades.IsNew("depths"), "ui: viewing a new category clears its mark");
            TechKey(Key.Pageup); Check(TechPage == 0, "ui: PgUp switches back");
            upgradeWindow.ClickTab(2); Check(TechPage == 2, "ui: a click on a tab selects it"); upgradeWindow.ClickTab(0);
            upgradeWindow.ClickNode((int)Tech.Spin); Check(TechSel == (int)Tech.Spin, "ui: a click on a node selects it");
            for (int i = 0; i < It.Count; i++) Inv[i] = 99;
            TechSel = (int)Tech.Dash; int c0 = Inv[It.Iron]; upgradeWindow.ClickLearn();
            Check(Has(Tech.Dash) && Inv[It.Iron] < c0, "ui: the Learn button learns the selected upgrade");
            TechSel = (int)Tech.Reaper; TechKey(Key.Enter); Check(!Has(Tech.Reaper), "ui: a locked branch cannot be bought while its parent is below the required level");
            TechSel = (int)Tech.Dash; TechKey(Key.Enter); Check(Lv(Tech.Dash) == 2, "ui: Enter buys exactly one more level"); TechSel = (int)Tech.Reaper; TechKey(Key.Enter); Check(Has(Tech.Reaper), "ui: the branch opens when the parent reaches level 2");
            TechPage = 0; TechSel = -1; Flash = 0; Notify.History.Clear(); Banners.Clear();
        });
        S(.5f, () => { Log($"upgwin open={TechOpen} vis={upgradeWindow.Visible} menu={Menu} nodes={upgradeWindow.NodeCount}"); Flash = 0; Shot("upg_basic"); TechSel = (int)Tech.FarLanterns; });
        S(.4f, () => Shot("upg_available"));
        S(.3f, () => { SelectTechPage(1); });
        S(.5f, () => Shot("upg_depths"));
        S(.2f, () => { CloseWindows(); Check(!TechOpen, "ui: the window closes"); GD.Print($"[check] SUMMARY failures={fails}"); });
        // новая категория: уведомление и вкладка с отметкой «новое»
        S(.2f, () => { foreach (var r in new[] { 1, 2, 3 }) { var a = Lantern(r); if (a != null) a.Lit = false; } var fresh = new UpgradeProgressionSystem(this); GD.Print("[upgrades] fresh system ok " + fresh.Visible().Count); });
        S(.2f, () => GD.Print("[check] UPGRADES DONE"));
    }
}
