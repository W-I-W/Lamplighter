using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Lamplighter.Balance;

namespace Lamplighter;

/// <summary>Сверка симулятора баланса с настоящей игрой (LAMP_STAGE=balance): формулы опыта, урона, света, навыков, цен, условий открытия, эффектов оружия и реликвий, здоровье и урон врагов, число ударов до убийства.
/// Здесь работает настоящий Game, симулятор считает то же самое на обычных классах — результаты должны совпасть.</summary>
public partial class Game
{
    void BuildBalanceScript()
    {
        int fails = 0;
        void Check(bool ok, string what) { if (!ok) fails++; GD.Print($"[check] {(ok ? "PASS" : "FAIL")} {what}"); }
        bool Eq(float a, float b, float tol = 1e-3f) => Mathf.Abs(a - b) <= tol * Math.Max(1f, Mathf.Abs(b));
        T Priv<T>(string name) => (T)typeof(Game).GetProperty(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).GetValue(this);

        S(.3f, () =>
        {
            Menu = Mn.None; noHeal = true; Hp = MaxHp; Enemies.Clear(); EventsEnabled = false; TestNoSpawn = true;
            var m = BalanceModel.FromGame(); var rnd = new Random(7);
            // ── опыт и уровни
            bool xpOk = true, hpOk = true;
            for (int lv = 1; lv <= 60; lv++) { Level = lv; var sp = new SimPlayer(m) { Level = lv }; if (!Eq(XpNeed, sp.XpNeed)) xpOk = false; }
            Check(xpOk, "формула опыта на уровень (1…60) совпадает с Game.XpNeed");
            foreach (float xp in new[] { 10f, 400f, 3000f, 20000f, 90000f })
            {
                Level = 1; Xp = 0; MaxHp = 60; Hp = 60; GiveXp(xp, true); var sp = new SimPlayer(m); sp.GiveXp(xp, true);
                if (Level != sp.Level || !Eq(MaxHp, sp.MaxHp) || !Eq(Xp, sp.Xp, 1e-2f)) hpOk = false;
            }
            Check(hpOk, "прирост уровней и здоровья от опыта совпадает с Game.GiveXp (60 + 10 за уровень)");
            Level = 1; MaxHp = 60; Xp = 0;
            bool fearOk = true; for (int lv = 1; lv <= 9; lv++) { Level = lv; if (!Eq(FearMul, new SimPlayer(m) { Level = lv }.FearMul)) fearOk = false; }
            Level = 1; Check(fearOk && FearMul > 2f, $"страх тени: множитель урона ниже 5 уровня совпадает (1 ур. ×{FearMul:0.00}, 5 ур. ×1)");
            // ── урон, добыча
            bool dmgOk = true, mineOk = true, lightOk = true;
            foreach (int tier in new[] { 0, 1, 2, 3, 4 }) foreach (int lv in new[] { 1, 6, 15, 30 })
                {
                    Tier = tier; Level = lv; var sp = new SimPlayer(m) { Tier = tier, Level = lv };
                    if (!Eq(PlayerDamage, sp.PlayerDamage)) dmgOk = false; if (!Eq(Priv<float>("MineSpeed"), sp.MineSpeed)) mineOk = false;
                    foreach (var w in Db.Weapons) { var wm = m.Weapons.First(x => x.Id == w.Id); if (!Eq(WeaponDmg(w), sp.WeaponDmg(wm))) dmgOk = false; }
                }
            Check(dmgOk, "урон без оружия и урон всех видов оружия (тир × уровень) совпадает с Game");
            Check(mineOk, "скорость добычи (тир × уровень) совпадает с Game.MineSpeed");
            // ── навыки: случайные уровни → бонусы
            bool skOk = true; string skBad = "";
            for (int trial = 0; trial < 30; trial++)
            {
                Tier = rnd.Next(0, 5); Level = rnd.Next(1, 35); var sp = new SimPlayer(m) { Tier = Tier, Level = Level };
                for (int i = 0; i < SkillLv.Length; i++) { int v = rnd.Next(0, SkillMax(i) + 1); SkillLv[i] = v; sp.Sk[i] = v; }
                var pairs = new (string n, float g, float s)[] { ("Lens", LensBonus, sp.LensBonus), ("FarLamp", FarLampBonus, sp.FarLampBonus), ("Dash", DashCooldown, sp.DashCooldown), ("Spin", SpinScale, sp.SpinScale), ("Flare", FlareDuration, sp.FlareDuration), ("Pulse", PulseCooldown, sp.PulseCooldown),
                    ("Reaper", ReaperHeal, sp.ReaperHeal), ("Heat", HeatTaken, sp.HeatTaken), ("Cold", ColdTaken, sp.ColdTaken), ("Aura", AuraDps, sp.AuraDps) };
                foreach (var (n, g, s) in pairs) if (!Eq(g, s)) { skOk = false; skBad = $"{n}: игра {g} / модель {s}"; }
                float hl = HeroLampLv; var simLight = sp.LightR; if (HeroLampLv == 0 && !Eq(PlayerLightR, simLight)) { skOk = false; skBad = $"PlayerLightR: игра {PlayerLightR} / модель {simLight}"; }
            }
            Check(skOk, "бонусы навыков (свет, рывок, круг, ракета, импульс, жатва, жар, холод, аура, радиус света) совпадают с Game" + (skOk ? "" : " — " + skBad));
            Array.Clear(SkillLv); Tier = 0; Level = 1;
            // ── цены и родители
            bool costOk = true, parOk = true, maxOk = true;
            for (int i = 0; i < Techs.Length; i++)
            {
                if (SkillMax(i) != m.Skills[i].Max) maxOk = false;
                for (int lv = 1; lv <= SkillMax(i); lv++)
                {
                    var g = LevelCost(i, lv); var s = m.Skills[i].LevelCost(lv, 1f); if (g.Length != s.Length || g.Where((c, k) => c[0] != s[k][0] || c[1] != s[k][1]).Any()) costOk = false;
                }
                var pr = ParentReqs(i); var ms = m.Skills[i]; if (pr.Count != ms.ParentTech.Length || pr.Where((x, k) => x.tech != ms.ParentTech[k] || x.min != ms.ParentMin[k]).Any()) parOk = false;
            }
            Check(costOk, $"цены всех уровней всех навыков ({Techs.Length} навыков) совпадают с Game.LevelCost (включая TechCostMul)");
            Check(parOk && maxOk, "родители и максимальные уровни навыков совпадают");
            // ── условия открытия
            bool unlockOk = true; string unBad = "";
            var checkable = new[] { Tech.Dash, Tech.Reaper, Tech.Boots, Tech.FlameEdge, Tech.HeatShield, Tech.Collector, Tech.SpikeBoots, Tech.FrostEdge, Tech.FrostArmor, Tech.Pulse, Tech.Beam, Tech.Fireproof, Tech.Prism, Tech.Optics, Tech.Marksman, Tech.Lens, Tech.HeroLamp, Tech.Piercing };
            for (int trial = 0; trial < 40; trial++)
            {
                Tier = rnd.Next(0, 5); Kills = rnd.Next(0, 60); BeaconLevel = rnd.Next(1, 7); BossMask = rnd.Next(0, 32); Inv[It.Crystal] = rnd.Next(0, 2); HasFurnace = rnd.Next(0, 2) == 1;
                Owned.Clear(); if (rnd.Next(0, 2) == 1) Owned.Add(new OwnedWeapon { Id = "bow1" }); Array.Clear(SkillLv); if (rnd.Next(0, 2) == 1) SkillLv[(int)Tech.Marksman] = 1; if (rnd.Next(0, 2) == 1) SkillLv[(int)Tech.Lens] = 1;
                var sp = new SimPlayer(m) { Tier = Tier, Kills = Kills, Beacon = BeaconLevel, BossMask = BossMask, HasFurnace = HasFurnace };
                sp.Inv[It.Crystal] = Inv[It.Crystal]; if (Owned.Count > 0) sp.GiveWeapon(Array.FindIndex(m.Weapons, w => w.Id == "bow1")); sp.Sk[(int)Tech.Marksman] = SkillLv[(int)Tech.Marksman]; sp.Sk[(int)Tech.Lens] = SkillLv[(int)Tech.Lens];
                foreach (var t in checkable) { bool g = Techs[(int)t].Ok(this), s = sp.Unlocked((int)t); if (g != s) { unlockOk = false; unBad = $"{t}: игра {g} / модель {s}"; } }
            }
            Check(unlockOk, "условия открытия навыков (зависящие от уровня кирки, убийств, маяка, боссов, оружия) совпадают с TechDef.Ok" + (unlockOk ? "" : " — " + unBad));
            var all = new SimPlayer(m) { Tier = 4, Kills = 500, Beacon = 6, BossMask = 31, HasFurnace = true, HasForge = true, HasLantern = true, PoisDone = 10 }; for (int i = 0; i < 5; i++) all.Lit[i] = 5; all.Inv[It.Crystal] = 5; all.GiveWeapon(Array.FindIndex(m.Weapons, w => w.Id == "bow1")); all.Sk[(int)Tech.Marksman] = 1; all.Sk[(int)Tech.Lens] = 1;
            Check(Enumerable.Range(0, m.Skills.Length).Where(i => !m.Skills[i].Reward).All(i => all.Unlocked(i)), "у каждого не наградного навыка в модели есть условие открытия");
            Owned.Clear(); Array.Clear(SkillLv); Tier = 0; Kills = 0; BeaconLevel = 1; BossMask = 0; Inv[It.Crystal] = 0;
            // ── эффекты оружия, вставок, реликвий и синергий
            bool fxOk = true; string fxBad = "";
            var perm = Db.Relics.Where(r => r.Permanent).Select(r => r.Id).ToArray();
            for (int trial = 0; trial < 60; trial++)
            {
                Owned.Clear(); EquippedRelics.Clear(); Array.Clear(SkillLv); SkillLv[(int)Tech.Pulse] = rnd.Next(0, 2); SkillLv[(int)Tech.Dash] = rnd.Next(0, 2); SkillLv[(int)Tech.Spin] = rnd.Next(0, 2); SkillLv[(int)Tech.Flare] = rnd.Next(0, 2);
                int wi = rnd.Next(0, Db.Weapons.Length); var ow = new OwnedWeapon { Id = Db.Weapons[wi].Id }; for (int k = 0; k < Db.Weapons[wi].Slots; k++) if (rnd.Next(0, 2) == 1) ow.Mods.Add(rnd.Next(0, Db.Mods.Length));
                Owned.Add(ow); CurW = 0; for (int k = 0; k < rnd.Next(0, 5); k++) { var id = perm[rnd.Next(0, perm.Length)]; if (!EquippedRelics.Contains(id)) EquippedRelics.Add(id); }
                BossMask = 0; RecomputeEffects();
                var sp = new SimPlayer(m); sp.GiveWeapon(Array.FindIndex(m.Weapons, w => w.Id == ow.Id)); sp.Owned[0].Mods.AddRange(ow.Mods); sp.RelicsEq.AddRange(EquippedRelics);
                for (int i = 0; i < SkillLv.Length; i++) sp.Sk[i] = SkillLv[i]; sp.HasLantern = Lamps.Count > 0; sp.Recompute();
                for (int k = 0; k < EffP.Length; k++) if (!Eq(EffP[k], sp.EffP[k])) { fxOk = false; fxBad = $"{(Fxk)k}: игра {EffP[k]} / модель {sp.EffP[k]} ({ow.Id}+{string.Join(",", ow.Mods)} реликвии {string.Join(",", EquippedRelics)})"; }
                foreach (var kv in Num) if (!Eq(kv.Value, sp.N(kv.Key))) { fxOk = false; fxBad = $"Num {kv.Key}: игра {kv.Value} / модель {sp.N(kv.Key)}"; }
                if (!Flags.SetEquals(sp.Flags)) { fxOk = false; fxBad = "Flags: " + string.Join(",", Flags) + " / " + string.Join(",", sp.Flags); }
            }
            Check(fxOk, "эффекты оружия, вставок, реликвий и синергий (60 случайных сборок) совпадают с Game.RecomputeEffects" + (fxOk ? "" : " — " + fxBad));
            Owned.Clear(); EquippedRelics.Clear(); CurW = -1; Array.Clear(SkillLv); RecomputeEffects();
            // ── враги: здоровье и масштаб урона
            bool hpE = true, dmE = true, bossE = true; string eBad = "";
            var regStart = new[] { 20, World.MX0 + 20, World.AX0 + 20, World.IX0 + 20, World.HX0 + 20 };
            foreach (float danger in new[] { 1f, 1.54f, 2.3f })
                foreach (var e in m.Enemies)
                {
                    var k = Enum.Parse<EK>(e.Kind); var re = SpawnEnemy(TileCenter(regStart[e.Region], 30), k, null, danger);
                    var sp = new SimPlayer(m); var cs = new CombatSim(sp, Profile.Expert(), new Rng(1), e.Region); var f = cs.MakeFoe(e, danger, 0, false);
                    if (!Eq(re.MaxHp, f.MaxHp)) { hpE = false; eBad = $"{e.Kind}@{e.Region} здоровье: игра {re.MaxHp} / модель {f.MaxHp}"; }
                    if (!Eq(re.Scale, f.DmgScale)) { dmE = false; eBad = $"{e.Kind}@{e.Region} урон: игра {re.Scale} / модель {f.DmgScale}"; }
                    Enemies.Remove(re);
                }
            foreach (var b in m.Bosses)
            {
                var k = Enum.Parse<EK>(b.Kind); var re = SpawnEnemy(TileCenter(regStart[Math.Min(4, b.Region)], 30), k, null, 1.9f); var sp = new SimPlayer(m); var cs = new CombatSim(sp, Profile.Expert(), new Rng(1), b.Region); var f = cs.MakeBoss(b);
                if (!Eq(re.MaxHp, f.MaxHp) || !Eq(BossDamageMul(k), f.DmgScale)) { bossE = false; eBad = $"{b.Kind}: здоровье {re.MaxHp}/{f.MaxHp}, урон ×{BossDamageMul(k)}/{f.DmgScale}"; }
                Enemies.Remove(re);
            }
            Enemies.Clear();
            Check(hpE, "здоровье всех видов врагов (3 уровня опасности × биомы) совпадает с Game.SpawnEnemy" + (hpE ? "" : " — " + eBad));
            Check(dmE, "множитель урона врагов (опасность × биом × EnemyDamage) совпадает с Enemy.Scale" + (dmE ? "" : " — " + eBad));
            Check(bossE, "здоровье и урон боссов совпадают" + (bossE ? "" : " — " + eBad));
            // ── опасность
            bool dgOk = true;
            for (int trial = 0; trial < 20; trial++)
            {
                BossMask = rnd.Next(0, 32); var sp = new SimPlayer(m) { BossMask = BossMask }; sp.Lit[0] = LitIn(0); sp.Lit[1] = LitIn(1); sp.Lit[2] = LitIn(2); sp.Lit[3] = LitIn(3); sp.Lit[4] = LitIn(4);
                int total = sp.LitCount; if (total != LitCount) { dgOk = false; }
                if (!Eq(Priv<float>("DangerScale"), sp.DangerScale)) dgOk = false;
            }
            Check(dgOk, "масштаб опасности (фонари × боссы) совпадает с Game.DangerScale");
            BossMask = 0;
            // ── число ударов до смерти: настоящий Hurt против модели
            bool hitsOk = true; string hBad = "";
            var simPlayer = new SimPlayer(m); simPlayer.GiveWeapon(Array.FindIndex(m.Weapons, w => w.Id == "sword2")); simPlayer.Level = 1;
            Owned.Clear(); Owned.Add(new OwnedWeapon { Id = "sword2" }); CurW = 0; Level = 1; RecomputeEffects();
            foreach (int silver in new[] { 0, 3 })
                foreach (var kind in new[] { EK.Shade, EK.Wraith, EK.Golem, EK.Sentinel, EK.Imp, EK.Eel, EK.Leech })
                {
                    SkillLv[(int)Tech.SilverEdge] = silver; simPlayer.Sk[(int)Tech.SilverEdge] = silver; simPlayer.Recompute(); RecomputeEffects();
                    var def = m.Enemies.First(x => x.Kind == kind.ToString()); var re = SpawnEnemy(TileCenter(regStart[def.Region], 30), kind, null, 1.7f); re.Hp = re.MaxHp = 60f;
                    int gHits = 0; var dir = new Vector2(1, 0); while (re.Hp > 0 && gHits < 500) { gHits++; Hurt(re, WeaponDmg(Db.Weapon("sword2")), dir, true); }
                    var sp = simPlayer.Copy(); var cs = new CombatSim(sp, Profile.Expert(), new Rng(5), def.Region); var f = cs.MakeFoe(def, 1.7f, 0, false); f.Hp = f.MaxHp = 60f; cs.Foes.Add(f);
                    int sHits = 0; while (f.Hp > 0 && sHits < 500) { sHits++; cs.TestHit(f, sp.WeaponDmg(m.Weapons[sp.Owned[0].W]), true); }
                    if (gHits != sHits) { hitsOk = false; hBad = $"{kind} серебро {silver}: игра {gHits} ударов / модель {sHits}"; }
                    Enemies.Remove(re);
                }
            Check(hitsOk, "число ударов до гибели врага (с учётом «серебряного лезвия» и щита стража) совпадает: настоящий Game.Hurt против модели" + (hitsOk ? "" : " — " + hBad));
            Enemies.Clear(); Owned.Clear(); CurW = -1; Array.Clear(SkillLv); RecomputeEffects();
            // ── цена инструментов и печь
            Check(CostFurnaceOk(m) && m.CostPick.Length == 4, "стоимость печи, фонаря и кирок взята из игры (reflection)");
            GD.Print($"[check] SUMMARY failures={fails}"); GD.Print("[check] BALANCE DONE");
        });
    }

    static bool CostFurnaceOk(BalanceModel m) => m.CostFurnace != null && m.CostFurnace.Length == 2 && m.CostLantern != null && m.CostPick.All(c => c.Length >= 2);
}
