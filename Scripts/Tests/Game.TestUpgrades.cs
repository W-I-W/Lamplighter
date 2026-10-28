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
        SaveData ReadSave() => JsonSerializer.Deserialize<SaveData>(File.ReadAllText(SavePath), new JsonSerializerOptions { IncludeFields = true });
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
            Check(Banners.Exists(b => b.Title.StartsWith("Открыта категория")), "upgrades: a pixel notification announces the opened category");
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
            var basic = PageList(0); Check(basic.Count == 10, $"upgrades: a category shows all of its upgrades, even unavailable ones ({basic.Count})");
            int lens = (int)Tech.Lens; HasFurnace = false; Check(StateOfTech(lens) == TechState.Locked, "upgrades: unmet condition → Locked");
            HasFurnace = true; for (int i = 0; i < It.Count; i++) Inv[i] = 0; Check(StateOfTech(lens) == TechState.Missing, "upgrades: condition met, no materials → Missing (still visible)");
            Inv[It.Copper] = 4; Inv[It.Shard] = 2; Check(StateOfTech(lens) == TechState.Available, "upgrades: condition met and materials in place → Available");
            TechPage = 0; TechCur = 0; Check(LearnTech(lens) && Has(Tech.Lens) && Inv[It.Copper] == 0 && Inv[It.Shard] == 0, "upgrades: learning pays the cost and grants the upgrade");
            Check(StateOfTech(lens) == TechState.Owned && !LearnTech(lens), "upgrades: a learned upgrade is Owned and cannot be bought twice");
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
            Check(upgradeWindow.CardCount == PageList(TechPage).Count, $"ui: every upgrade of the category has a card ({upgradeWindow.CardCount})");
            int cur = TechCur; TechKey(Key.Down); Check(TechCur == cur + 1, "ui: ↓ moves the selection");
            TechKey(Key.Up); Check(TechCur == cur, "ui: ↑ moves the selection back");
            TechKey(Key.Right); Check(TechPage == 1, "ui: → switches to the next category");
            Check(!Upgrades.IsNew("depths"), "ui: viewing a new category clears its mark");
            TechKey(Key.Left); Check(TechPage == 0, "ui: ← switches back");
            upgradeWindow.ClickTab(2); Check(TechPage == 2, "ui: a click on a tab selects it"); upgradeWindow.ClickTab(0);
            upgradeWindow.ClickCard(2); Check(TechCur == 2, "ui: a click on a card selects it");
            TechCur = PageList(0).IndexOf((int)Tech.Dash); int c0 = Inv[It.Iron]; upgradeWindow.ClickLearn();
            Check(Has(Tech.Dash) && Inv[It.Iron] < c0, "ui: the Learn button learns the selected upgrade");
            TechCur = PageList(0).IndexOf((int)Tech.Reaper); TechKey(Key.Enter); Check(Has(Tech.Reaper), "ui: Enter learns the selected upgrade");
            TechPage = 0; TechCur = 0; Flash = 0; Toasts.Clear(); Banners.Clear();
        });
        S(.5f, () => { Flash = 0; Shot("upg_basic"); TechCur = PageList(0).IndexOf((int)Tech.FarLanterns); });
        S(.4f, () => Shot("upg_available"));
        S(.3f, () => { SelectTechPage(1); TechCur = 1; });
        S(.5f, () => Shot("upg_depths"));
        S(.2f, () => { CloseWindows(); Check(!TechOpen, "ui: the window closes"); GD.Print($"[check] SUMMARY failures={fails}"); });
        // новая категория: уведомление и вкладка с отметкой «новое»
        S(.2f, () => { foreach (var r in new[] { 1, 2, 3 }) { var a = Lantern(r); if (a != null) a.Lit = false; } var fresh = new UpgradeProgressionSystem(this); GD.Print("[upgrades] fresh system ok " + fresh.Visible().Count); });
        S(.2f, () => GD.Print("[check] UPGRADES DONE"));
    }
}
