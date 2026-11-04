using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lamplighter;

/// <summary>Пожиратель Рощи и общая шкала фаз (LAMP_STAGE=grove): контроллер фаз (переход один раз, без пропусков, без отката при лечении, 1–4 фазы), бой в арене
/// (неподвижность, фазы на 70% и 35%, неуязвимость перехода, поглощение с лимитом и срывом, «Голод Рощи» с безопасными секторами, кора и открытое ядро, наказание за стояние
/// и за дальний бой), гибель и награда ровно один раз; скриншоты каждой фазы, перехода, «Голода», гибели и шкал с 1, 3 и 4 фазами.</summary>
public partial class Game
{
    void BuildGroveScript()
    {
        int fails = 0; void Check(bool ok, string what) { if (!ok) fails++; GD.Print($"[check] {(ok ? "PASS" : "FAIL")} {what}"); }
        Enemy boss = null; Vector2 pos0 = default; BossArena A = null; int cores0 = 0;
        void Near(Vector2 p) { PPos = p; pVel = Vector2.Zero; cam?.ResetSmoothing(); }
        void Snap(string n) { CloseWindows(); Window = Win.None; Shot(n); }

        // ── контроллер фаз: данные, а не интерфейс ──
        S(.4f, () =>
        {
            Menu = Mn.None; CloseWindows(); Window = Win.None; TestNoSpawn = true; MaxHp = Hp = 99999f;
            var gp = BossPhaseBook.For(EK.Warden);
            Check(gp.Length == 3 && gp[0].Threshold == 1f && Mathf.IsEqualApprox(gp[1].Threshold, .70f) && Mathf.IsEqualApprox(gp[2].Threshold, .35f) && gp[1].Name == "Поглощение" && gp[2].Name == "Сердце Тьмы",
                "phases: the Grove Devourer has three phases from data — 100–70 Awakening, 70–35 Absorption, 35–0 Heart of Darkness");
            Check(gp[0].Has("root") && gp[0].Has("branch") && !gp[0].Has("spores") && gp[1].Has("spores") && gp[1].Has("absorb") && gp[2].Has("hunger") && gp[2].Has("rootseries"), "phases: each phase carries its own attack set");
            var c = new BossPhaseController(gp);
            Check(!c.Update(.71f) && c.Index == 0 && c.Update(.70f) && c.Index == 1 && !c.Update(.69f) && c.Transitions == 1, "phases: the 70% transition happens exactly once");
            c = new BossPhaseController(gp);
            bool s1 = c.Update(.05f); int i1 = c.Index; bool s2 = c.Update(.05f); int i2 = c.Index; bool s3 = c.Update(.05f);
            Check(s1 && i1 == 1 && s2 && i2 == 2 && !s3 && c.Transitions == 2, "phases: huge damage (100% → 5%) does not skip a phase — II then III, one step per tick");
            Check(!c.Update(1f) && c.Index == 2, "phases: healing back to full does not return the boss to an earlier phase");
            c = new BossPhaseController(gp); Check(Mathf.IsEqualApprox(c.FloorFrac, .70f) && c.Update(.7f) && Mathf.IsEqualApprox(c.FloorFrac, .35f) && c.Update(.35f) && c.FloorFrac == 0f, "phases: a hit cannot go past the next threshold before its transition");
            var one = new BossPhaseController(new[] { new BossPhaseData { Id = "a", Name = "Один", Threshold = 1f } });
            Check(one.Count == 1 && !one.Update(0f) && one.Index == 0, "phases: a one-phase boss never transitions");
            var four = new BossPhaseController(new[] { new BossPhaseData { Threshold = 1f }, new BossPhaseData { Threshold = .8f }, new BossPhaseData { Threshold = .45f }, new BossPhaseData { Threshold = .1f } });
            var seq = new List<int>(); for (float f = 1f; f >= -.01f; f -= .013f) if (four.Update(f)) seq.Add(four.Index);
            Check(seq.SequenceEqual(new[] { 1, 2, 3 }) && four.Transitions == 3, $"phases: a four-phase boss goes 1→2→3→4 once each ({string.Join(",", seq)})");
            var two = new BossPhaseController(new[] { new BossPhaseData { Threshold = 1f }, new BossPhaseData { Threshold = .5f } });
            Check(!two.Update(.51f) && two.Update(.5f) && !two.Update(0f) && two.Count == 2, "phases: a two-phase boss");
            var net = new BossPhaseController(gp); Check(net.ApplyNet(2) && net.Index == 1 && net.ApplyNet(2) && net.Index == 2 && !net.ApplyNet(1) && !net.ApplyNet(2), "phases: a guest follows the host's phase forward only, one step at a time");
            bool threw = false; try { new BossPhaseController(new[] { new BossPhaseData { Threshold = 1f }, new BossPhaseData { Threshold = 1f } }); } catch (ArgumentException) { threw = true; }
            Check(threw, "phases: non-decreasing thresholds are rejected");
            bool all = true; foreach (var k in new[] { EK.Warden, EK.DepthWarden, EK.AshWarden, EK.WinterWarden, EK.Source }) { var p = BossPhaseBook.For(k); if (p.Length < 2 || p[0].Threshold != 1f) all = false; }
            Check(all && Mathf.IsEqualApprox(BossPhaseBook.For(EK.WinterWarden)[1].Threshold, .66f) && Mathf.IsEqualApprox(BossPhaseBook.For(EK.Source)[2].Threshold, .33f), "phases: every existing boss has phase data matching its real thresholds");
            foreach (var n in GroveSprites) { var t = GD.Load<Texture2D>(GroveDir + n + ".png"); Check(t != null, $"grove sprite {n}.png loads"); }
            var bt = GD.Load<Texture2D>(GroveDir + "grove_devourer.png"); Check(bt != null && bt.GetWidth() == GroveFrame * GroveFrames && bt.GetHeight() == GroveFrame, "grove: the boss sheet is 21 frames of 128×128 (idle, roots, spores, branch, hurt, phase, death)");
        });

        // ── бой в арене ──
        S(.4f, () =>
        {
            A = W.ArenaOf(1); BossMask = 0; Check(A != null && A.Radius == 10, "grove: the Devourer has its own small arena (radius 10)");
            Near(TileCenter(A.Totem.X, A.Totem.Y + 2)); BreakTile(A.Totem, Tl.BossTotem, false); bossT = 0; UpdateBossSpawn(.3f);
            boss = Enemies.Find(e => e.Kind == EK.Warden); pos0 = boss?.Pos ?? default;
            Check(boss != null && boss.IsBoss && Game.EnemyName(boss.Kind) == "Пожиратель Рощи" && ArenaIsLocked(1), "grove: breaking the totem wakes the Grove Devourer and locks the arena");
            Check(boss != null && boss.Pos.DistanceTo(A.Pos) < 1f, "grove: the tree stands in the centre of its arena");
            Check(!Enemies.Exists(e => e.Kind == EK.Warden && e != boss) && BossKindOf(1) == EK.Warden, "grove: the old guardian does not exist alongside it");
            Near(A.Pos + new Vector2(-44, -20));
        });
        S(7f, () => { });        // идёт бой: игрок стоит на месте
        S(.2f, () =>
        {
            Check(boss.Pos == pos0, "grove: after 7 s of fighting the tree has not moved (no walking, jumping or dashing)");
            Check(GroveStillN > 0, $"grove: standing still is punished by a root under the player ({GroveStillN})");
            Check(PhaseOf(boss).Index == 0 && boss.State != 20, "grove: phase I while health is above 70%");
            Snap("grove_1_phase1");
        });
        S(.3f, () => { GroveSweeps.Add(new GroveSweep { C = boss.Pos + new Vector2(0, -10), Ang = Mathf.Pi * .7f, Half = .95f, R0 = 14, R1 = 100, Warn = 1f, Dmg = 0 }); GroveRootAt(PPos + new Vector2(30, 0), 1f, 0f); });
        S(.4f, () => Snap("grove_2_telegraphs"));
        // ── фаза II на 70% ──
        S(.35f, () =>
        {
            GroveRoots.Clear(); GroveSweeps.Clear(); boss.Hp = boss.MaxHp * .695f;
        });
        S(1.8f, () =>
        {
            var pc = PhaseOf(boss);
            Check(pc.Index == 1 && pc.Transitions == 1 && boss.State == 9, "grove: phase II «Absorption» starts at 70%, once, with a transition");
            float h = boss.Hp; Hurt(boss, 40f, Vector2.Right); Check(boss.Hp == h, "grove: the tree is invulnerable during the phase transition");
            Snap("grove_3_phase2_caption");
        });
        S(.1f, () => { Check(boss.State != 9 && PhaseOf(boss).Transitions == 1, "grove: the transition ends and does not repeat"); });
        S(.6f, () => { boss.State = 0; GroveAbsorbStart(boss); Check(boss.State == 4 && boss.Over > 0 && GroveNodes.Length == 3, "grove: absorption — lines to three roots, the core opens"); });
        S(.1f, () => Snap("grove_4_absorb"));
        S(.1f, () =>
        {
            Hurt(boss, boss.MaxHp * .06f, Vector2.Right);
            Check(boss.State == 7 && GroveAbsorbT <= 0 && GroveNodes.Length == 0 && boss.Over > 0, "grove: damage to the open core interrupts absorption and stuns the tree");
            // лимит лечения: много поглощений подряд не лечат бесконечно, дальше только усиление
            float healed0 = GroveHealed; boss.Hp = boss.MaxHp * .4f;
            for (int k = 0; k < 6; k++) { boss.State = 0; GroveAbsorbStart(boss); for (int t = 0; t < 60 && boss.State == 4; t++) UpdateGrove(boss, .1f, Vector2.Zero, 60f); }
            Check(GroveHealed <= GroveHealCapTotal * boss.MaxHp + .5f && GroveHealed > healed0, $"grove: absorption heals at most {GroveHealCapTotal:P0} of health per fight ({GroveHealed:0} of {boss.MaxHp:0})");
            Check(GroveAbsorbBuff && boss.T3 > 0, "grove: once healing is spent, absorption empowers the tree instead");
            boss.Hp = boss.MaxHp; BossPhaseTick(boss); Check(PhaseOf(boss).Index == 1, "grove: full healing does not return phase I");
            boss.T3 = 0; boss.State = 0; boss.T1 = 2f;
        });
        // ── фаза III на 35%: огромный удар не проскакивает фазу ──
        S(.35f, () =>
        {
            boss.Hp = boss.MaxHp * .5f; Hurt(boss, boss.MaxHp * .9f, Vector2.Right);
            Check(boss.Hp > 0 && Mathf.IsEqualApprox(boss.Hp, boss.MaxHp * .35f), $"grove: one huge hit stops at the 35% threshold ({boss.Hp / boss.MaxHp:P0}) — phase III is not skipped");
        });
        S(1.8f, () => { var pc = PhaseOf(boss); Check(pc.Index == 2 && pc.Transitions == 2 && boss.State == 9, "grove: phase III «Heart of Darkness» starts at 35%"); Snap("grove_5_phase3_caption"); });
        S(1.0f, () =>
        {
            boss.State = 0; boss.T1 = 5f; boss.Over = 0; UpdateGrove(boss, .01f, Vector2.Zero, 60f);
            float h = boss.Hp; Hurt(boss, 10f, Vector2.Right); float dShield = h - boss.Hp;
            Check(boss.Shield && dShield < 3.5f, $"grove: in phase III the bark closes the core — weak damage ({dShield:0.0} of 10)");
            GroveRoots.Clear(); GroveSweeps.Clear(); GroveSpores.Clear(); groveStill.Clear(); boss.T1 = 9f;
            GroveHungerStart(boss); var hg = GroveHungers[^1];
            int safe = 0; bool adj = false; for (int s = 0; s < hg.N; s++) if ((hg.Safe & (1 << s)) != 0) { safe++; if ((hg.Safe & (1 << ((s + 1) % hg.N))) != 0) adj = true; }
            Check(safe >= 2 && safe < hg.N && !adj, $"grove: «Hunger of the Grove» always leaves safe sectors between the deadly ones ({safe} of {hg.N}, none adjacent)");
            int ss = Enumerable.Range(0, hg.N).First(s => (hg.Safe & (1 << s)) != 0); Near(hg.C + Vector2.FromAngle(hg.Ang0 + (ss + .5f) * Mathf.Tau / hg.N) * 70f);
            Check(HungerSafe(hg, PPos), "grove: the player stands in a safe sector");
            noHeal = true; Hp = MaxHp; Invuln = 0;
        });
        S(1.3f, () => { Snap("grove_6_hunger"); var hg = GroveHungers[^1]; Near(PPos + (PPos - hg.C).Normalized() * 14f); groveStill.Clear(); Check(HungerSafe(hg, PPos), "grove: a step inside the safe sector keeps the player safe"); });      // шаг: корень за стояние на месте не должен совпасть с проверкой
        S(2.3f, () =>
        {
            Check(Hp >= MaxHp - .01f, $"grove: a player in a safe sector takes no damage from the Hunger (lost {MaxHp - Hp:0.0}, roots {GroveRoots.Count}/{GroveRoots.Count(r => r.Hit)}, spores {GroveSpores.Count}, projs {Projs.Count}, safe {HungerSafe(GroveHungers.Count > 0 ? GroveHungers[^1] : new GroveHunger(), PPos)}, enemies {Enemies.Count})");
            Check(boss.State == 6 && boss.Over > 0 && !boss.Shield, "grove: after the Hunger the core is open for a while");
            float h = boss.Hp; Hurt(boss, 10f, Vector2.Right); Check(h - boss.Hp > 13f, $"grove: the open core takes extra damage ({h - boss.Hp:0.0} of 10)");
            Snap("grove_7_core_open");
            GroveRoots.Clear(); groveStill.Clear(); GroveHungerStart(boss); var hg = GroveHungers[^1];
            int bad = Enumerable.Range(0, hg.N).First(s => (hg.Safe & (1 << s)) == 0); Near(hg.C + Vector2.FromAngle(hg.Ang0 + (bad + .5f) * Mathf.Tau / hg.N) * 70f); Hp = MaxHp; Invuln = 0;
        });
        S(.2f, () =>
        {
            Check(Hp < MaxHp - 10f, $"grove: a player in a deadly sector is hit by the Hunger ({MaxHp - Hp:0})");
            noHeal = false; Hp = MaxHp;
            // дальний бой и бой из-за стены: корень под стрелком и залп шипов
            groveFarCd = 0; int far0 = GroveFarN, pj = Projs.Count; Near(boss.Pos + new Vector2(0, 156)); Hurt(boss, 5f, Vector2.Up);
            Check(GroveFarN == far0 + 1 && GroveRoots.Exists(r => r.Pos.DistanceTo(PPos) < 2f) && Projs.Count > pj, "grove: damage from far away answers with a root under the shooter and thorns");
            groveFarCd = 0; boss.Over = 1f; boss.Shield = false; Near(boss.Pos + new Vector2(0, (A.Radius + 4) * TS)); float h = boss.Hp; Hurt(boss, 10f, Vector2.Up);
            Check(GroveFarN == far0 + 2 && h - boss.Hp < 4f, $"grove: hits from behind the arena wall are weak ({h - boss.Hp:0.0} of 10) and punished");
            boss.Over = 0;
            boss.Kb = new Vector2(900, 300); Near(A.Pos + new Vector2(-44, -20)); for (int i = 0; i < 10; i++) UpdateEnemies(.05f);
            Check(boss.Pos == pos0, "grove: knockback does not move the tree");
            Near(boss.Pos + new Vector2(4, 2)); UpdateEnemies(.02f); Check(PPos.DistanceTo(boss.Pos) >= 17.5f, "grove: the trunk is solid — the hero is pushed out");
            Near(A.Pos + new Vector2(-44, -20));
        });
        // ── гибель и награда ровно один раз ──
        S(1.2f, () =>
        {
            cores0 = Inv[It.Core]; GroveRoots.Clear(); GroveSweeps.Clear(); GroveSpores.Clear(); GroveHungers.Clear();
            boss.State = 0; boss.T1 = 9f; Hurt(boss, boss.MaxHp * 3f, Vector2.Right); UpdateEnemies(.02f);
            Check(!Enemies.Contains(boss) && BossDown(1) && Inv[It.Core] == cores0 + 1, "grove: death gives the core and marks the world progress");
            Check(GroveDeath != null && GroveRoots.Count == 0 && GroveSpores.Count == 0, "grove: death starts its effect and clears every hazard");
            NetBossDown(1, boss.Pos); KillWorldOnly(boss);
            Check(Inv[It.Core] == cores0 + 1 && GroveDeath.T < 1f, "grove: a repeated death notice gives nothing and does not restart the effect");
        });
        S(2.4f, () => Snap("grove_8_death"));
        S(.2f, () =>
        {
            Snap("grove_9_revived");
            bossT = 0; UpdateBossSpawn(.3f); for (int i = 0; i < 10; i++) UpdateEnemies(.1f);
            Check(Inv[It.Core] == cores0 + 1 && !Enemies.Exists(e => e.Kind == EK.Warden) && !ArenaIsLocked(1), "grove: the reward came exactly once, the tree does not wake again, the arena is open");
        });
        // ── шкала фаз у других боссов (1, 3 и 4 фазы) ──
        S(.2f, () =>
        {
            Enemies.Clear(); Near(TileCenter(W.Spawn.X, W.Spawn.Y) + new Vector2(0, 40));
            var b = SpawnEnemy(PPos + new Vector2(60, -10), EK.DepthWarden, null, 1f); b.Home = b.Pos; b.Hp = b.MaxHp * .5f; BossPhaseTick(b); b.T1 = 99f;
            Check(PhaseOf(b).Index == 1 && BossInFight() == b, "hud: the Depth Warden shows phase 2 of 3 at 50%");
        });
        S(.5f, () => Snap("grove_hud_3phases"));
        S(.2f, () =>
        {
            Enemies.Clear(); var b = SpawnEnemy(PPos + new Vector2(60, -10), EK.AshWarden, null, 1f); b.Home = b.Pos; b.T1 = 99f;
            b.PhaseCtl = new BossPhaseController(new[] { new BossPhaseData { Id = "a", Name = "Тление", Threshold = 1f }, new BossPhaseData { Id = "b", Name = "Пламя", Threshold = .8f }, new BossPhaseData { Id = "c", Name = "Ярость", Threshold = .45f }, new BossPhaseData { Id = "d", Name = "Пепел", Threshold = .15f } });
            b.Hp = b.MaxHp * .6f;
        });
        S(.2f, () => Snap("grove_hud_4phases"));
        S(.2f, () =>
        {
            Enemies.Clear(); var b = SpawnEnemy(PPos + new Vector2(60, -10), EK.AshWarden, null, 1f); b.Home = b.Pos; b.T1 = 99f;
            b.PhaseCtl = new BossPhaseController(new[] { new BossPhaseData { Id = "a", Name = "Бой", Threshold = 1f } });
        });
        S(.3f, () => { Snap("grove_hud_1phase"); Enemies.Clear(); Boss = null; });
        S(.2f, () => { GD.Print("[check] SUMMARY failures=" + fails); GD.Print("GROVE DONE"); GetTree().Quit(); });
    }
}
