using Godot;
using System;

namespace Lamplighter;

/// <summary>Проверка отладочной панели (LAMP_STAGE=debug): открытие по F10, смена этапов без дублей, правка чисел боя на лету,
/// бессмертие, перехват клавиш полем ввода, создание врагов, телепорт, скриншоты вкладок.</summary>
public partial class Game
{
    void BuildDebugScript()
    {
        float hpBefore = 0, enemyHp0 = 0; int bCount = 0;
        S(.4f, () =>
        {
            Check("debug: panel exists and is hidden at start", DebugUi != null && !DebugUi.Visible);
            Check("debug: allowed in the test build", DebugAllowed);
            enemyHp0 = Tune.T.EnemyHp;
            var f10 = new InputEventKey { Keycode = Key.F10, Pressed = true };
            Check("debug: F10 is swallowed and opens the panel", DebugInput(f10) && DebugUi.Visible);
        });
        S(.5f, () => { Check("debug: info text is filled", DebugInfo().Contains("Герой")); Check("debug: tuning rows built", DebugUi.TuneRowCount > 40, $"rows={DebugUi.TuneRowCount}"); DebugUi.ShowTab(0); Shot("debug1_info"); });
        S(.1f, () =>
        {
            var msg = DebugStage("mid");
            Check("debug: stage mid raises level, tool, beacon and bosses", Level >= 9 && Tier >= 2 && BeaconLevel >= 3 && BossDown(1) && BossDown(2), $"lv={Level} tier={Tier} beacon={BeaconLevel} mask={BossMask}");
            Check("debug: stage mid builds the village", Buildings.Count > 0 && Villagers.Count >= 3, $"buildings={Buildings.Count} villagers={Villagers.Count}");
            bCount = Buildings.Count; DebugStage("mid");
            Check("debug: repeating a stage does not duplicate buildings or weapons", Buildings.Count == bCount && Owned.FindAll(o => o.Id == "sword1").Count == 1, $"b={Buildings.Count}/{bCount}");
            Log(msg); DebugUi.ShowTab(1); Shot("debug2_stage");
        });
        S(.2f, () =>
        {
            DebugStage("late"); Check("debug: stage late", Level >= 18 && BeaconLevel >= 5 && BossCount >= 4 && Tier >= 4, $"lv={Level} beacon={BeaconLevel} bosses={BossCount}");
            int lvBefore = Level; DebugStage("mid"); Check("debug: an earlier stage never lowers progress", Level >= lvBefore && BeaconLevel >= 5);
            DebugStage("post"); Check("debug: stage post finishes the game", Finished && BossCount == 5 && Level >= 24);
            DebugSetBoss(5, false); Check("debug: a boss can be reset", !BossDown(5)); DebugSetBoss(5, true);
            DebugSetLevel(12); Check("debug: set level restores health", Level == 12 && Math.Abs(Hp - MaxHp) < .01f);
        });
        S(.1f, () =>
        {
            DebugUi.ShowTab(2);
            Check("debug: tuning edit applies at once", DebugUi.SetTuning("EnemyHp", "3.5") && Math.Abs(Tune.T.EnemyHp - 3.5f) < 1e-4f, $"EnemyHp={Tune.T.EnemyHp}");
            Check("debug: array tuning edit", DebugUi.SetTuning("BossHp", "9, 8, 7, 6, 5") && Math.Abs(Tune.T.BossHp[2] - 7f) < 1e-4f);
            Check("debug: a bad value is rejected", !DebugUi.SetTuning("EnemyHp", "abc") && Math.Abs(Tune.T.EnemyHp - 3.5f) < 1e-4f);
            Check("debug: the tuned value reaches the game formulas", Math.Abs(Tune.T.EnemyHp - 3.5f) < 1e-4f && CombatTuning.At(Tune.T.BossHp, 2) == 7f);
            DebugUi.SetTuning("EnemyHp", enemyHp0.ToString(System.Globalization.CultureInfo.InvariantCulture)); Shot("debug3_tuning");
        });
        S(.1f, () =>
        {
            DebugGod = true; Invuln = 0; hpBefore = Hp; HurtPlayer(25, Vector2.Zero);
            Check("debug: god mode ignores damage", Math.Abs(Hp - hpBefore) < .01f);
            DebugGod = false; Invuln = 0; HurtPlayer(10, Vector2.Zero); Check("debug: damage works again without god mode", Hp < hpBefore);
            DebugHeal(); Check("debug: heal", Math.Abs(Hp - MaxHp) < .01f);
            Enemies.Clear(); DebugSpawn(EK.Shade, 3); Check("debug: spawn enemies", Enemies.Count >= 3, $"n={Enemies.Count}");
            DebugKillAll(); Check("debug: kill all sets health to zero", Enemies.TrueForAll(e => e.Hp <= 0));
            var a = BossLantern(1); var p0 = PPos; DebugTeleportAncient(a); Check("debug: teleport moves the hero", a != null && PPos != p0 && PPos.DistanceTo(a.Pos) < 5 * TS);
            DebugUi.ShowTab(3); Shot("debug4_tools");
        });
        S(.2f, () => { DebugUi.FirstTuneEdit.GrabFocus(); });
        S(.2f, () =>
        {
            Check("debug: a focused text field is detected", DebugUi.HasTextFocus());
            var e = new InputEventKey { Keycode = Key.E, Pressed = true };
            Check("debug: game keys are swallowed while typing", DebugInput(e));
            DebugInput(new InputEventKey { Keycode = Key.Escape, Pressed = true });
            Check("debug: Escape returns control to the game", !DebugUi.HasTextFocus() && !DebugInput(new InputEventKey { Keycode = Key.E, Pressed = true }));
            Check("debug: mouse over the panel is swallowed, elsewhere is not", DebugInput(new InputEventMouseMotion { Position = DebugUi.GlobalPosition + new Vector2(20, 20) }) && !DebugInput(new InputEventMouseMotion { Position = new Vector2(2, 700) }));
            Engine.TimeScale = 0.25f; DebugInput(new InputEventKey { Keycode = Key.F10, Pressed = true });
            Check("debug: closing the panel hides it and restores game speed", !DebugUi.Visible && Math.Abs(Engine.TimeScale - 1) < 1e-6);
            Tune.Reload();
            Log("debug done"); GD.Print("[check] SUMMARY failures=" + checkFails); GD.Print("DEBUG DONE");
        });
    }
}
