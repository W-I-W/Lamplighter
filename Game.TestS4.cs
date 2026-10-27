using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Тесты этапа 4 (LAMP_STAGE=s4).</summary>
public partial class Game
{
    Enemy TestDummy(Vector2 off, EK k = EK.Shade, float hp = 200)
    {
        var e = SpawnEnemy(PPos + off, k); e.Age = 1; e.Hp = e.MaxHp = hp; e.Home = e.Pos; e.Guard = new Poi(); return e;   // Guard: стоит на месте
    }

    void BuildS4Script()
    {
        var spawn = TileCenter(W.Spawn.X, W.Spawn.Y);
        S(.3f, () =>
        {
            for (int i = 0; i < Inv.Length; i++) Inv[i] = 60; Inv[It.Coins] = 500;
            Tp(spawn + new Vector2(0, 40)); HasFurnace = true;
            foreach (var id in new[] { "sword1", "bow1", "staff1" }) GiveWeapon(id, false);
            for (int i = 0; i < 7; i++) ModInv[i] = 2;
            Log($"weapons={Owned.Count} current={CurDef?.Name}");
        });
        // ── меч ──
        S(.2f, () =>
        {
            EquipWeapon(0); aimOverride = Aim = PPos + new Vector2(30, 0);
            var d1 = TestDummy(new Vector2(20, 0)); var d2 = TestDummy(new Vector2(20, 10)); var d3 = TestDummy(new Vector2(-30, 0));
            AtkCd = 0; var h0 = (d1.Hp, d2.Hp, d3.Hp);
            Act(.016f);
            Log($"sword arc: hit front1={h0.Item1 - d1.Hp:F1} front2={h0.Item2 - d2.Hp:F1} behind={h0.Item3 - d3.Hp:F1}");
        });
        S(.3f, () => { Enemies.Clear(); });
        // ── эффекты через вставки ──
        S(.1f, () =>
        {
            var ow = CurOw; ow.Mods.Clear(); InstallMod(0); Log($"sword slots={CurDef.Slots} mods={ow.Mods.Count} burn={E(Fxk.Burn)} tags fire={TagCount.GetValueOrDefault("fire")} syn=[{string.Join(",", ActiveSyn)}]");
            var d1 = TestDummy(new Vector2(20, 0)); AtkCd = 0; for (int i = 0; i < 4; i++) { AtkCd = 0; Act(.016f); }
            Log($"burning={d1.Burn > 0} burn={d1.Burn:F1} hp={d1.Hp:F1}");
        });
        S(.1f, () =>
        {
            Enemies.Clear(); EquipWeapon(1); CurOw.Mods.Clear(); InstallMod(1); InstallMod(3);
            Log($"bow mods: slow={E(Fxk.Slow)} freeze={E(Fxk.Freeze)} multi={E(Fxk.Multi)}");
            var d1 = TestDummy(new Vector2(90, 0)); AtkCd = 0; Act(.016f);
            Log($"arrows fired={PProjs.Count} (expect 2)");
        });
        S(.5f, () => Shot("s4_1_bow"));
        S(.4f, () =>
        {
            var d1 = Enemies.Find(e => e.Slow > 0 || e.Hp < 200); Log($"bow target hp={Enemies[0].Hp:F1} slow={Enemies[0].Slow:F1} freeze={Enemies[0].Freeze:F1}");
            Enemies.Clear(); EquipWeapon(2); CurOw.Mods.Clear(); InstallMod(2); InstallMod(5);
            var a = TestDummy(new Vector2(60, 0)); var b = TestDummy(new Vector2(75, 20)); var c = TestDummy(new Vector2(70, -22));
            AtkCd = 0; Act(.016f); BeamE = 10; Log($"staff mods: chain={E(Fxk.Chain)} siphon={E(Fxk.Siphon)} orb fired={PProjs.Count}");
        });
        S(.6f, () => { Log($"after staff: bolts={Bolts.Count} beamE={BeamE:F0} hps=[{string.Join(",", Enemies.ConvertAll(e => ((int)e.Hp).ToString()))}]"); Shot("s4_2_staff_chain"); });
        // ── блок взрыва при гибели ──
        S(.1f, () =>
        {
            Enemies.Clear(); CurOw.Mods.Clear(); InstallMod(6); var a = TestDummy(new Vector2(40, 0), EK.Shade, 1); var b = TestDummy(new Vector2(52, 8), EK.Shade, 30);
            a.Hp = 0; Log($"blast eff={E(Fxk.Blast)}"); var kill = a; Enemies.Remove(a); KillEnemy(kill); Log($"blast dmg to neighbour: {30 - b.Hp:F1}");
        });
        // ── реликвии и синергии ──
        S(.2f, () =>
        {
            Enemies.Clear(); OwnedRelics.Clear(); EquippedRelics.Clear(); EquipWeapon(0); CurOw.Mods.Clear();
            GiveRelic("r_embersp", true); RecomputeEffects();
            Log($"relic fire + sword: syn=[{string.Join(",", ActiveSyn)}] burn={E(Fxk.Burn):F1}");
            GiveRelic("r_frostshard", true); EquipWeapon(1); Log($"ice relic + bow: syn=[{string.Join(",", ActiveSyn)}] slow={E(Fxk.Slow):F1}");
            TechMask |= 1 << (int)Tech.Dash; RecomputeEffects(); Log($"ice + dash: FrostDash={Flags.Contains("FrostDash")}");
            GiveRelic("r_emberheart", true); GiveRelic("r_ashcrown", true); RecomputeEffects();
            Log($"slots={RelicSlots} equipped={EquippedRelics.Count} fire tags={TagCount.GetValueOrDefault("fire")} FireTrail={Flags.Contains("FireTrail")}");
            TechMask |= 1 << (int)Tech.Pulse; RecomputeEffects(); Log($"FirePulse={Flags.Contains("FirePulse")}");
            GiveRelic("r_dawnprism", true); GiveRelic("r_beaconwick", true);
        });
        S(.2f, () =>
        {
            // три световых эффекта: заменим слоты
            EquippedRelics.Clear(); ToggleRelic("r_dawnprism"); ToggleRelic("r_beaconwick"); ToggleRelic("r_firstlight"); GiveRelic("r_firstlight", true); ToggleRelic("r_firstlight");
            RecomputeEffects(); Log($"light tags={TagCount.GetValueOrDefault("light")} Surge flag={Flags.Contains("RadiantSurge")} lampRadius+={N("LampRadius"):F2}");
            // огненный след
            EquippedRelics.Clear(); ToggleRelic("r_embersp"); ToggleRelic("r_emberheart"); ToggleRelic("r_ashcrown"); RecomputeEffects();
            Log($"fire x3 => trail={Flags.Contains("FireTrail")}");
            pVel = new Vector2(80, 0); trailT2 = 0; int z0 = Zones.Count; UpdateRelics(.3f); Log($"fire trail zones added={Zones.Count - z0}");
        });
        S(.2f, () =>
        {
            // ледяной рывок и периодическая заморозка
            EquippedRelics.Clear(); ToggleRelic("r_frostshard"); ToggleRelic("r_winterheart"); GiveRelic("r_rimemirror", true); ToggleRelic("r_rimemirror"); RecomputeEffects();
            Log($"ice tags={TagCount.GetValueOrDefault("ice")} FreezePulse={Flags.Contains("FreezePulse")} FrostDash={Flags.Contains("FrostDash")}");
            Enemies.Clear(); var a = TestDummy(new Vector2(30, 0)); TryDash(); Log($"frost dash froze={a.Freeze > 0}");
            freezeT = 0; var b = TestDummy(new Vector2(-30, 0)); UpdateRelics(.01f); Log($"periodic freeze froze={b.Freeze > 0}");
        });
        S(.4f, () => Shot("s4_3_ice"));
        S(.2f, () =>
        {
            // временные реликвии
            GrantTemp("t_flame"); Log($"temp relic: burn={E(Fxk.Burn):F1} tempCount={TempRelics.Count}"); ClearTemp(); Log($"after clear burn={E(Fxk.Burn):F1}");
            Save(); Log($"saved: owned={OwnedRelics.Count} weapons={Owned.Count}");
        });
    }

    void BuildPerfScript()
    {
        var spawn = TileCenter(W.Spawn.X, W.Spawn.Y);
        S(.3f, () =>
        {
            for (int i = 0; i < Inv.Length; i++) Inv[i] = 80; Tp(spawn + new Vector2(0, 60)); aimOverride = null;
            for (int i = 0; i < 4; i++) { var a = TileCenter(W.Spawn.X + 4 + i * 3, W.Spawn.Y - 6); Buildings.Add(new Building { Type = i % 2 == 0 ? Tl.Sawmill : Tl.Mine, X = W.Spawn.X + 4 + i * 3, Y = W.Spawn.Y - 6, Forced = true }); }
            for (int i = 0; i < 30; i++) { var e = SpawnEnemy(PPos + new Vector2(rng.RandfRange(-120, 120), rng.RandfRange(-90, 90)), i % 3 == 0 ? EK.Wraith : EK.Shade); e.Age = 1; e.Hp = e.MaxHp = 9999; }
            for (int i = 0; i < 12; i++) Lamps.Add(new Lamp { X = W.Spawn.X - 10 + i, Y = W.Spawn.Y + 8, Kind = i % 3, Anim = 1 });
            GiveWeapon("staff1", false); EquipWeapon(Owned.Count - 1); ModInv[2] = 2; InstallMod(2); EventsEnabled = true;
            SpawnEvent("elite", PPos + new Vector2(150, 0)); SpawnEvent("trader", spawn + new Vector2(20, 30));
        });
        S(4f, () => { perfT0 = Time; });
        S(3f, () =>
        {
            var tp = Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000; var fps = Engine.GetFramesPerSecond();
            Log($"PERF: chunks={LoadedChunks} lastChunkMs={LastChunkMs:F2} fps={fps:F0} processMs={tp:F2} enemies={Enemies.Count} parts={Parts.Count} lamps={Lamps.Count} objects={Performance.GetMonitor(Performance.Monitor.ObjectCount)} mem={Performance.GetMonitor(Performance.Monitor.MemoryStatic) / 1048576:F0}MB");
        });
    }
    float perfT0;
}
