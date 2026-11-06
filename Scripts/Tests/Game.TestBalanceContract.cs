using Godot;
using System;
using System.Linq;
using Lamplighter.Balance;

namespace Lamplighter;

/// <summary>Сверка поведения модели с настоящим Game. Изолированный мир LAMP_STAGE=balancecontract.</summary>
public partial class Game
{
    void BuildBalanceContractScript()
    {
        int failures = 0;
        void Check(bool ok, string text) { if (!ok) failures++; GD.Print($"[check] {(ok ? "PASS" : "FAIL")} {text}"); }
        bool Same(float a, float b) => Math.Abs(a - b) < .003f;
        S(.1f, () =>
        {
            Menu = Mn.None; noHeal = true; TestNoSpawn = true; EventsEnabled = false; TestPlayers = 0;
            Enemies.Clear(); Projs.Clear(); Array.Clear(SkillLv); EquippedRelics.Clear(); TempRelics.Clear(); RecomputeEffects();
            var m = BalanceModel.FromGame();
            Check(ModelHealth.Errors(m).Count == 0, "contract: model data are complete and finite");
            var broken = m.Clone(); broken.T.EnemyDamage = float.NaN;
            Check(ModelHealth.Errors(broken).Count > 0, "contract: invalid numeric input is rejected");
            foreach (int lv in new[] { 1, 5, 20 }) foreach (int lamps in new[] { 0, 1, 4 })
            {
                Level = lv;
                for (int reg = 0; reg < 5; reg++)
                {
                    int count = 0;
                    foreach (var a in W.Ancients) if (a != W.Ancients[0] && !a.Boss && a.Region == reg) a.Lit = count++ < lamps;
                }
                var p = new SimPlayer(m) { Level = lv }; for (int reg = 0; reg < 5; reg++) p.Lit[reg] = reg == 0 ? LitCount : LitIn(reg);
                bool same = p.LitCount == LitCount;
                for (int reg = 0; reg < 5; reg++) foreach (bool guarded in new[] { false, true }) same &= Same(EncounterBudget(reg, guarded, 1), p.EncounterBudget(reg, guarded));
                Check(same, $"contract: encounter budgets, level {lv}, lanterns {lamps}");
            }
            Level = 10; Hp = MaxHp = 150; pVel = Vector2.Zero;
            PPos = W.Ancients[0].Pos + new Vector2(50, 0);
            var grove = SpawnEnemy(PPos + new Vector2(90, 0), EK.Warden, null, 1);
            foreach (int phase in new[] { 0, 1, 2 }) foreach (string attack in new[] { "root", "branch", "rootseries" })
            {
                GroveReset(); GroveRoots.Clear(); GroveSweeps.Clear(); groveLocal = null;
                var pc = new BossPhaseController(new[] {
                    new BossPhaseData { Threshold = 1, Attacks = new[] { attack } },
                    new BossPhaseData { Threshold = .7f, Attacks = new[] { attack } },
                    new BossPhaseData { Threshold = .35f, Attacks = new[] { attack } } });
                if (phase >= 1) pc.Update(.6f); if (phase == 2) pc.Update(.3f);
                grove.PhaseCtl = pc; GroveChoose(grove, pc, 90, 1);
                var move = m.Bosses[0].Moves.First(a => a.Name == attack);
                float warning = phase == 2 && move.LateWindup > 0 ? move.LateWindup : phase == 1 && move.MidWindup > 0 ? move.MidWindup : move.Windup;
                if (attack == "branch") Check(GroveSweeps.Count == 1 && Same(GroveSweeps[0].Warn, warning), $"contract: grove branch warning, phase {phase + 1}");
                else
                {
                    Check(GroveRoots.Count >= 1 && Same(GroveRoots[0].Warn, warning), $"contract: grove {attack} warning, phase {phase + 1}");
                    if (attack == "root")
                    {
                        var offsets = GroveRoots.Skip(1).Select(r => r.Warn - warning).OrderBy(t => t).ToArray();
                        Check(move.Followups != null && offsets.Length == move.Followups.Length && offsets.Zip(move.Followups.OrderBy(t => t), Same).All(v => v), $"contract: root count and follow-up warnings, phase {phase + 1}");
                    }
                }
            }
            Enemies.Clear(); GroveReset();
            var depth = SpawnEnemy(PPos + new Vector2(100, 0), EK.DepthWarden, null, 1);
            var dive = m.Bosses[1].Moves.First(a => a.Name == "dive"); var tide = m.Bosses[1].Moves.First(a => a.Name == "tide");
            depth.State = 2; depth.T2 = 0; UpdateDepthWarden(depth, 0, Vector2.Right, 100, false);
            Check(Same(depth.T2, dive.Post), "contract: depth dive recovery");
            depth.State = 3; depth.T2 = 0; UpdateDepthWarden(depth, 0, Vector2.Right, 100, false);
            Check(Same(depth.T1, dive.Gap * BossTempoMul()), "contract: depth dive pause");
            depth.State = 4; depth.T2 = 0; depth.Mark = PPos + new Vector2(500, 0); UpdateDepthWarden(depth, 0, Vector2.Right, 100, false);
            Check(Same(depth.T1, tide.Gap * BossTempoMul()), "contract: depth tide pause");
            Check(Same(m.T.BossWarnMul, Tune.T.BossWarnMul), "contract: live boss wind-up multiplier is captured");
            Enemies.Clear(); Projs.Clear(); MaxHp = 100; Hp = 0; killHealPool = -1;
            var sim = new SimPlayer(m) { Level = 5, BonusHp = 100 - CombatMath.BaseHealth(5), Hp = 0 }; // 100 HP
            KillHealGain(50); sim.GainKillHealing(50); KillHealGain(50); sim.GainKillHealing(50);
            Check(Same(Hp, sim.Hp) && Same(Hp, 20), "contract: kill healing is capped at twenty percent");
            regenDelay = 999; PPos = W.Ancients[0].Pos; UpdatePlayer(1); sim.RefillKillHealing(1); KillHealGain(3); sim.GainKillHealing(3);
            Check(Same(Hp, sim.Hp), "contract: kill-healing pool recovers at the live rate");
            var batch = new Runner.Batch(); batch.Runs.Add(new RunResult { Profile = "novice", Out = Outcome.Stuck, StopKind = "preparation_limit", Hours = 1, Level = 1, SkillLv = new int[m.Skills.Length] });
            var summary = Summary.Build(batch, m, new Targets()).Profiles.Single();
            Check(double.IsNaN(summary.HoursMedian) && summary.PreparationStops == 1 && summary.DeathsMean == 0, "contract: zero victories have no winning time; preparation is not a combat death");
            // The simulator alone missed these spatial staff exploits. Exercise real projectiles.
            Diff = 0; PPos = FindDarkArena(); Enemies.Clear(); PProjs.Clear();
            Array.Clear(SkillLv); EquippedRelics.Clear(); TempRelics.Clear(); Owned.Clear(); CurW = -1; RecomputeEffects();
            Enemy Target(float x)
            {
                var e = SpawnEnemy(PPos + new Vector2(x, 0), EK.Shade, null, 1);
                e.Hp = e.MaxHp = 1000; return e;
            }
            var first = Target(20); var second = Target(34); var third = Target(48);
            Array.Clear(EffP); EffP[(int)Fxk.Chain] = 1;
            HitEnemy(first, 20, Vector2.Right, false);
            Check(Same(first.Hp, 980) && Same(second.Hp, 991) && Same(third.Hp, 995.95f), "contract: chain does not revisit its origin and loses damage at each jump");
            Enemies.Clear(); first = Target(20); second = Target(34); Array.Clear(EffP);
            var orb = new PProj { Pos = first.Pos, Vel = Vector2.Right * 100, Dmg = 20, Life = 1, Life0 = 1, Kind = 1, Pierce = 3, Blast = 26 };
            PProjs.Add(orb); UpdateWeaponFx(0); UpdateWeaponFx(0); orb.Life = 0; UpdateWeaponFx(0);
            Check(Same(first.Hp, 980) && Same(second.Hp, 993) && orb.Hit.Count == 2, "contract: piercing orb and expiry cannot damage the same target twice; splash is 35 percent");
            GiveWeapon("staff1", false); CurW = 0; RecomputeEffects(); AtkCd = 0; PProjs.Clear();
            float baseRange = ShotRange(); SkillLv[(int)Tech.Overcharge] = 5;
            float boostedRange = ShotRange(); FireRanged(CurDef, Vector2.Right);
            Check(Same(boostedRange / baseRange, 1f + E(Tech.Overcharge)) && Same(PProjs[0].Vel.Length() * PProjs[0].Life0, boostedRange), "contract: overcharge range grows linearly and matches the projectile");
            Level = 1; float damage1 = WeaponDmg(CurDef); Level = 41; float damage41 = WeaponDmg(CurDef); Level = 80;
            Check(Same(WeaponDmg(CurDef), damage41) && Same(damage41 - damage1, 7f), "contract: damage from character levels has diminishing returns and caps at seven");
            var staffModel = new SimPlayer(m) { Level = 80 }; staffModel.GiveWeapon(Array.FindIndex(m.Weapons, w => w.Id == "staff1"));
            Check(Same(staffModel.WeaponDmg(staffModel.Cur), WeaponDmg(CurDef)), "contract: high-level staff damage matches the simulator");
            GD.Print("[model-contract] source_sha256=" + ModelHealth.SourceHash());
            GD.Print("[model-contract] assembly_sha256=" + ModelHealth.AssemblyHash());
            GD.Print($"[check] SUMMARY failures={failures}"); GD.Print("[check] MODEL CONTRACT DONE");
        });
    }
}
