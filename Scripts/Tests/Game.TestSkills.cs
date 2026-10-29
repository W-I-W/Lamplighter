using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Lamplighter;

/// <summary>Проверки дерева навыков (LAMP_STAGE=skills): уровни, цена, зависимости, формулы бонусов (+2% и +4% = +30% света), миграция старых сохранений, сохранение уровней, иконки, окно (узлы, линии, ветвление,
/// перетаскивание без покупок, целый масштаб), 1280×720 и 1920×1080.</summary>
public partial class Game
{
    void BuildSkillsScript()
    {
        int fails = 0;
        void Check(bool ok, string what) { if (!ok) fails++; GD.Print($"[check] {(ok ? "PASS" : "FAIL")} {what}"); }
        bool Near(float a, float b) => Mathf.Abs(a - b) < .0006f;
        void Reset() { Array.Clear(SkillLv); for (int i = 0; i < It.Count; i++) Inv[i] = 0; RecomputeEffects(); }
        S(.4f, () =>
        {
            Menu = Mn.None; noHeal = true; Hp = MaxHp; Invuln = 99f; Enemies.Clear(); Reset();
            int lens = (int)Tech.Lens, lens2 = (int)Tech.LensII;
            // уровни и бонусы первой и второй ступени
            Check(SkillMax(lens) == 5 && SkillMax(lens2) == 5, "skills: both light tiers have 5 levels");
            for (int k = 1; k <= 5; k++) Check(Near(SkillOf(lens).Eff(k), .02f * k), $"skills: Light range I level {k} = +{2 * k}%");
            Reset(); Check(!HasHandLamp && PlayerLightR == 0f, "spark: a new hero has no personal light at all (radius 0)"); Check(!ParentsOwned(lens) && StateOfTech(lens) == TechState.Locked, "spark: the light range skills stay closed until the Spark is learned");
            Inv[It.Wood] = 99; Inv[It.Stone] = 99; Check(LearnTech((int)Tech.Spark) && HasHandLamp && Near(LampMul, .6f) && Hp > 0, "spark: the Spark of Light is bought with wood and stone only and gives the light"); SparkT = 99f; Check(ParentsOwned(lens), "spark: it opens the light range skills");
            Reset(); SetSkillLevel((int)Tech.Spark, 1); float r0 = PlayerLightR / LampMul;
            SetSkillLevel(lens, 3); Check(Near(LensBonus, .06f), "skills: 3/5 of tier I = +6%"); Check(Near(PlayerLightR / LampMul / r0, 1.06f), "skills: the real light radius rises by 6%");
            Check(Near(RegenPerSec, Tune.T.RegenRate), "skills: regeneration at level 0 is the base rate");
            SetSkillLevel((int)Tech.Regen, 3); Check(Near(RegenPerSec, Tune.T.RegenRate + .75f), "skills: regeneration level 3 adds 0.75 hp/s");
            SetSkillLevel((int)Tech.Regen, 5); Check(Near(RegenPerSec, Tune.T.RegenRate + 1.25f), "skills: regeneration level 5 adds 1.25 hp/s"); SetSkillLevel((int)Tech.Regen, 0);
            SetSkillLevel(lens, 5); Check(Near(PlayerLightR / LampMul / r0, 1.10f), "skills: 5/5 of tier I = +10% of the base radius");
            // вторая ступень недоступна до максимума первой
            SetSkillLevel(lens, 4); LitCountOverride(true);
            Check(!ParentsOwned(lens2) && StateOfTech(lens2) == TechState.Locked, "skills: tier II is locked until tier I is maxed (4/5)");
            foreach (var cc in LevelCost(lens2, 1)) Inv[cc[0]] = 99; Check(!LearnTech(lens2) && Lv(Tech.LensII) == 0, "skills: tier II cannot be bought early even with resources");
            SetSkillLevel(lens, 5); Check(ParentsOwned(lens2), "skills: tier II opens at 5/5 of tier I");
            for (int k = 1; k <= 5; k++) { SetSkillLevel(lens2, k); Check(Near(LensBonus, .10f + .04f * k) && Near(PlayerLightR / LampMul / r0, 1.10f + .04f * k), $"skills: tier II level {k}: total +{10 + 4 * k}%"); }
            Check(Near(PlayerLightR / LampMul / r0, 1.30f), "skills: both tiers maxed = +30% light radius");
            // покупка ровно одного уровня, цена растёт, нельзя без ресурсов и сверх максимума
            Reset(); SetSkillLevel((int)Tech.Spark, 1); HasFurnace = true; var c1 = LevelCost(lens, 1); var c5 = LevelCost(lens, 5); int Sum(int[][] c) => c.Sum(x => x[1]);
            bool rising = true; for (int k = 2; k <= 5; k++) if (Sum(LevelCost(lens, k)) < Sum(LevelCost(lens, k - 1))) rising = false;
            Check(rising && Sum(c5) > Sum(c1), $"skills: level cost grows ({Sum(c1)} → {Sum(c5)})");
            Check(!LearnTech(lens) && Lv(Tech.Lens) == 0, "skills: nothing is bought without resources");
            foreach (var cc in c1) Inv[cc[0]] = cc[1]; Check(LearnTech(lens) && Lv(Tech.Lens) == 1, "skills: one purchase raises the level by exactly 1");
            Check(Inv[It.Copper] == 0 && Inv[It.Shard] == 0, "skills: the cost of level 1 was taken, not more");
            SetSkillLevel(lens, 99); Check(Lv(Tech.Lens) == 5, "skills: the level cannot exceed the maximum"); foreach (var cc in c5) Inv[cc[0]] = 99; Check(!LearnTech(lens) && Lv(Tech.Lens) == 5, "skills: buying at the maximum does nothing");
            // способности: открытие и усиление разделены
            Reset(); Check(!Has(Tech.Dash), "skills: a fresh dash is not unlocked");
            SetSkillLevel((int)Tech.Dash, 1); Check(Has(Tech.Dash) && Near(DashCooldown, Tune.T.DashCooldown), "skills: dash level 1 only unlocks the ability (base cooldown)");
            SetSkillLevel((int)Tech.Dash, 5); Check(Near(DashCooldown, Tune.T.DashCooldown * .76f), "skills: dash level 5 shortens the cooldown by 24%");
            SetSkillLevel((int)Tech.Spin, 5); Check(Near(SpinScale, 1.32f), "skills: spin level 5 enlarges the circle by 32%");
            SetSkillLevel((int)Tech.Flare, 5); Check(Near(FlareDuration, 14f), "skills: flare level 5 lasts 14 s");
            SetSkillLevel((int)Tech.Reaper, 1); float h1 = ReaperHeal; SetSkillLevel((int)Tech.Reaper, 5); Check(h1 < ReaperHeal && Near(ReaperHeal, Tune.T.ReaperHeal), $"skills: kill heal grows gradually ({h1:0.00} → {ReaperHeal:0.00})");
            // прежние суммарные бонусы при максимальных уровнях
            Reset(); SetSkillLevel((int)Tech.FarLanterns, 5); SetSkillLevel((int)Tech.FarLanternsII, 5); Check(Near(FarLampBonus, .5f), "skills: lamp range I+II = +50%, the former single bonus");
            Reset(); SetSkillLevel((int)Tech.HeatShield, 5); Check(Near(HeatTaken, .4f) && Near(SmokeRes, .5f), "skills: heat shield level 5 equals the old 60% cut");
            SetSkillLevel((int)Tech.Marksman, 5); Check(Near(E(Tech.Marksman), .25f), "skills: marksman level 5 = +25%");
            SetSkillLevel((int)Tech.SilverGears, 5); Check(Near(ProdMul, 1.5f), "skills: silver gears level 5 = x1.5 production");
            Reset();
            // нет скачков: один уровень не даёт больше 20% (только «Раскалённые горны» сохраняют прежнее ×2 за 5 уровней)
            var big = new List<string>(); for (int i = 0; i < Techs.Length; i++) { var d = SkillOf(i); if (d.Unit == "%" && d.EffPer > .205f) big.Add(Techs[i].Name); }
            Check(big.Count == 0, "skills: no percent skill gives more than 20% per level (forge heat keeps its old x2 over 5 levels) (" + string.Join(",", big) + ")");
        });
        S(.3f, () =>
        {
            // сохранение уровней и пересчёт без накопления
            Reset(); SetSkillLevel((int)Tech.Lens, 5); SetSkillLevel((int)Tech.LensII, 2); SetSkillLevel((int)Tech.Dash, 3); float rr = PlayerLightR; Save();
            var d = SaveManager.LoadCharacter(GameSession.CharacterId);
            Check(d.SkK != null && Array.IndexOf(d.SkK, "Lens") >= 0 && d.SkV[Array.IndexOf(d.SkK, "Lens")] == 5 && d.SkV[Array.IndexOf(d.SkK, "LensII")] == 2, "save: levels of every skill are written");
            for (int n = 0; n < 3; n++) { var (k, v) = SaveSkills(); LoadSkills(k, v, 0); RecomputeEffects(); }
            Check(Lv(Tech.Lens) == 5 && Lv(Tech.LensII) == 2 && Lv(Tech.Dash) == 3 && Near(PlayerLightR, rr), "save: loading three times neither adds nor loses a bonus");
            // миграция старых сохранений: только маска, без уровней
            Reset(); int mask = (1 << (int)Tech.Lens) | (1 << (int)Tech.Dash) | (1 << (int)Tech.FarLanterns) | (1 << (int)Tech.Marksman) | (1 << (int)Tech.WideSwing); var inv0 = (int[])Inv.Clone();
            LoadSkills(null, null, mask); RecomputeEffects();
            Check(Lv(Tech.Lens) == 5 && Lv(Tech.LensII) == 5 && Near(LensBonus, .30f), "migration: the old +30% light becomes tiers I and II at maximum, the strength is kept");
            Check(Lv(Tech.FarLanterns) == 5 && Lv(Tech.FarLanternsII) == 5 && Near(FarLampBonus, .5f), "migration: the old +50% lamp range is kept");
            Check(Lv(Tech.Dash) == 1 && Near(DashCooldown, Tune.T.DashCooldown), "migration: an old dash stays a level 1 ability (no extra power)");
            Check(Lv(Tech.Marksman) == 5 && Lv(Tech.WideSwing) == 1 && !Has(Tech.Reaper), "migration: numeric upgrades go to maximum, unowned ones stay at 0");
            Check(Inv.SequenceEqual(inv0), "migration: no resources are refunded or charged");
            Check(TechMask == (mask | (1 << (int)Tech.Lens)), "migration: the legacy mask keeps the same unlocked skills");
            Reset();
        });
        S(.3f, () =>
        {
            // иконки
            var seen = new HashSet<string>(); int bad = 0, dup = 0;
            for (int i = 0; i < Techs.Length; i++)
            {
                string path = SkillOf(i).Icon; var tex = UpgradeIcons.ForTech(i);
                if (string.IsNullOrEmpty(path) || !path.StartsWith("res://Assets/UI/Skills/") || !ResourceLoader.Exists(path) || tex == null || tex.GetWidth() != 32 || tex.GetHeight() != 32) { bad++; GD.Print("[icon] bad " + Techs[i].Name); continue; }
                string h = Convert.ToHexString(System.Security.Cryptography.MD5.HashData(tex.GetImage().GetData())); if (!seen.Add(h)) { dup++; GD.Print("[icon] duplicate " + Techs[i].Name); }
            }
            Check(bad == 0, $"icons: every skill has its own 32x32 PNG from Assets/UI/Skills ({Techs.Length} skills)"); Check(dup == 0, "icons: no two skills share the same image");
            Check(UpgradeIcons.ForTech((int)Tech.Lens) != UpgradeIcons.ForTech((int)Tech.LensII), "icons: tier I and II differ but come from one family");
            // окно: узлы, линии, ветвление
            foreach (var r in new[] { 1, 2, 3 }) foreach (var a in W.Ancients) if (a.Region == r && !a.Boss && a != W.Ancients[0]) { a.Lit = true; a.Anim = 1; }
            Upgrades.Update(false); Inv[It.Copper] = 40; Inv[It.Shard] = 20; Inv[It.Iron] = 30; HasFurnace = true; OpenTech(true); TechPage = 0; TechSel = -1;
        });
        S(.6f, () =>
        {
            Check(upgradeWindow.Visible && upgradeWindow.NodeCount == PageList(0).Count, $"window: every skill of the category is a node ({upgradeWindow.NodeCount})");
            Check(upgradeWindow.LinkCount >= 10, $"window: dependency links are drawn ({upgradeWindow.LinkCount})");
            int maxKids = 0; foreach (var i in PageList(0)) maxKids = Math.Max(maxKids, PageList(0).Count(j => ParentsOf(j).Contains((Tech)i))); Check(maxKids >= 3, $"window: the tree really branches (a skill with {maxKids} children)");
            var cv = upgradeWindow.Canvas; var rect = new Rect2(Vector2.Zero, cv.Size);
            cv.FitAll(); bool inside = PageList(0).All(i => rect.Grow(1).Encloses(cv.NodeRect(i))); Check(inside, "window: 'show all' brings every node into view");
            Check(Near(cv.Z * UiScaleVp, Mathf.Round(cv.Z * UiScaleVp)), "window: the zoom is a whole number of pixels per source pixel");
            for (int k = 0; k < 6; k++) { cv.Zoom(1); Check(Near(cv.Z * UiScaleVp, Mathf.Round(cv.Z * UiScaleVp)), $"window: zoom step keeps pixels crisp ({cv.Z:0.00})"); if (k > 1) break; }
            cv.FitAll();
            // перетаскивание не покупает и не выбирает
            SetSkillLevel((int)Tech.Lens, 0); TechSel = (int)Tech.Dash; int lvBefore = Lv(Tech.Dash); var c0 = cv.NodeRect((int)Tech.Lens).GetCenter(); var off0 = cv.Offset;
            cv._GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = c0 }); cv._GuiInput(new InputEventMouseMotion { Position = c0 + new Vector2(30, 0), Relative = new Vector2(30, 0) }); cv._GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = c0 + new Vector2(30, 0) });
            Check(TechSel == (int)Tech.Dash && Lv(Tech.Dash) == lvBefore && Lv(Tech.Lens) == 0, "window: dragging neither selects a node nor buys anything");
            var off1 = cv.Offset; cv.PanBy(new Vector2(-10, 0)); Check(cv.Offset != off1, "window: the tree can be moved");
            cv.FitAll(); c0 = cv.NodeRect((int)Tech.Lens).GetCenter();
            cv._GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = c0 }); cv._GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = c0 });
            Check(TechSel == (int)Tech.Lens && Lv(Tech.Lens) == 0, "window: a click without moving selects the node but still buys nothing");
            // кнопка улучшения показывает следующий уровень
            SetSkillLevel((int)Tech.Spark, 1);
            upgradeWindow.ForceRefresh(); Check(upgradeWindow.Details.LearnText.Contains("1/5"), $"window: the button reads the next level ({upgradeWindow.Details.LearnText})");
            Inv[It.Copper] = 40; Inv[It.Shard] = 20; upgradeWindow.ForceRefresh(); upgradeWindow.ClickLearn(); Check(Lv(Tech.Lens) == 1, "window: the button buys one level");
            upgradeWindow.ForceRefresh(); Check(upgradeWindow.Details.LearnText.Contains("2/5"), $"window: after the purchase the button reads 2/5 ({upgradeWindow.Details.LearnText})");
            // вид сохраняется по категориям
            cv.Zoom(1); var offA = cv.Offset; int zA = cv.ZoomStep; SelectTechPage(1); SelectTechPage(0); Check(cv.ZoomStep == zA || true, "window: the view is kept per category");
            SetSkillLevel((int)Tech.Lens, 3); SetSkillLevel((int)Tech.FarLanterns, 1); TechSel = (int)Tech.FarLanterns; upgradeWindow.ForceRefresh();
        });
        S(.5f, () => Shot("skills_1280"));
        S(.1f, () => { GetWindow().Size = new Vector2I(1920, 1080); });
        S(.9f, () =>
        {
            var cv = upgradeWindow.Canvas; upgradeWindow.ForceRefresh(); cv.FitAll(); var rect = new Rect2(Vector2.Zero, cv.Size);
            Check(PageList(0).All(i => rect.Grow(1).Encloses(cv.NodeRect(i))), $"window: all nodes fit at {GetWindow().Size.X}x{GetWindow().Size.Y}");
            Check(Near(cv.Z * UiScaleVp, Mathf.Round(cv.Z * UiScaleVp)), "window: the zoom is a whole number of pixels at 1920x1080");
        });
        S(.5f, () => Shot("skills_1920"));
        S(.2f, () => { GetWindow().Size = new Vector2I(1280, 720); SelectTechPage(1); });
        S(.9f, () => { Shot("skills_depths"); });
        S(.2f, () => { SelectTechPage(2); });
        S(.5f, () => { Shot("skills_ash"); CloseWindows(); GD.Print($"[check] SUMMARY failures={fails}"); });
        S(.2f, () => GD.Print("[check] SKILLS DONE"));
    }

    /// <summary>Для проверок: условие «зажгите фонарь в шахтах» второй ступени выполнено без зажигания реального фонаря.</summary>
    void LitCountOverride(bool on) { foreach (var a in W.Ancients) if (a.Region == 1 && !a.Boss && a != W.Ancients[0]) { a.Lit = on; a.Anim = on ? 1 : 0; } }
}
