using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Тест Сердца Тьмы, Источника Тьмы и финала (LAMP_STAGE=heart).</summary>
public partial class Game
{
    void BuildHeartScript()
    {
        S(.3f, () =>
        {
            BossMask |= 15; BeaconLevel = 5; ApplyBeaconRadius(); foreach (var s in new[] { W.Seal1, W.Seal2, W.Seal3, W.Seal4 }) foreach (var t in s) SetObj(t.X, t.Y, Tl.None);
            foreach (var a in W.Ancients) if (a.Region < 4 && !a.Boss) { a.Lit = true; a.Anim = 1; }
            Hp = MaxHp = 600; Inv[It.Core] = 0;
            Tp(TileCenter(World.HX0 + 12, 64));
            Log($"heart region={RegionAt(PPos)} riddles={W.Riddles.Count} traps={W.Traps.Count} falseLights={W.FalseLights.Count} gaps={W.HeartGaps.Count} puzzles={W.Puzzles.FindAll(p => p.Bridge).Count}");
        });
        S(1.2f, () => Shot("h1_heart"));
        // мост 1: решаем сразу (луч + зеркала проверены в аудите достижимости; здесь — логика открытия)
        S(.2f, () =>
        {
            var pz = W.Puzzles.Find(p => p.Bridge); if (pz != null) { SolvePuzzle(pz); Log($"bridge1 tile after solve={W.Get(pz.Doors[0].X, pz.Doors[0].Y)}"); }
            var rd = W.Riddles.Count > 0 ? W.Riddles[0] : null;
            if (rd != null)
            {
                int wrong = (rd.Real + 1) % rd.Lanterns.Count; TouchLantern(rd, wrong); Log($"wrong lantern: decoys={Enemies.FindAll(e => e.Kind == EK.Decoy).Count} lightBlock={LightBlockT:F1} state={rd.Poi.State}");
                TouchLantern(rd, rd.Real); Log($"real lantern: state={rd.Poi.State} gap tile={W.Get(rd.Gap[0].X, rd.Gap[0].Y)}");
            }
        });
        S(.5f, () =>
        {
            Enemies.Clear(); LightBlockT = 0;
            var tp = W.Traps.Count > 0 ? W.Traps[0] : new Vector2I(-1, -1);
            if (tp.X >= 0) { Tp(TileCenter(tp.X, tp.Y)); Log($"trap tp zones before={Zones.Count}"); }
        });
        S(.4f, () => { Log($"trap zones after={Zones.FindAll(z => z.Dark).Count}"); Shot("h2_trap"); Zones.Clear(); Tp(TileCenter(World.HX0 + 12, 64)); });
        S(.2f, () =>
        {
            foreach (var k in new[] { EK.Mirage, EK.Weaver, EK.Stalker }) SpawnEnemy(PPos + new Vector2(rng.RandfRange(-70, 70), rng.RandfRange(-60, -30)), k).Age = 1;
            Log($"heart enemies spawned={Enemies.Count}");
        });
        S(5f, () => { Log($"after fight: enemies={Enemies.Count} projs={Projs.Count} zones={Zones.Count} hp={Hp:F0}"); Shot("h3_enemies"); Enemies.Clear(); Zones.Clear(); Projs.Clear(); });
        // босс
        S(.2f, () =>
        {
            var bl = BossLantern(5); if (bl == null) { Log("NO FINAL BOSS LANTERN"); return; }
            aimOverride = null; Tp(bl.Pos + new Vector2(-70, 60)); Log($"final boss lantern {bl.Tile} pylons={W.ArenaPylons.Count}");
        });
        S(2.5f, () => { Log($"boss alive={Boss != null} kind={Boss?.Kind} hp={Boss?.Hp}"); Shot("h4_boss"); if (Boss != null) { Boss.State = 1; Boss.T2 = .05f; } });
        S(1.3f, () => { Shot("h5_absorb"); if (Boss != null) { Boss.State = 2; Boss.T2 = .05f; } });
        S(.5f, () => { Log($"volley projs={Projs.Count}"); if (Boss != null) { Boss.State = 4; Boss.T2 = .05f; } });
        S(1.2f, () => { Log($"dark zones={Zones.FindAll(z => z.Dark).Count}"); Shot("h6_zones"); if (Boss != null) { Boss.State = 3; Boss.T2 = .05f; } });
        S(1f, () => { Log($"summons={Enemies.Count}"); Enemies.RemoveAll(e => !e.IsBoss); Zones.Clear(); Projs.Clear(); if (Boss != null) { Boss.Hp = Boss.MaxHp * .6f; } });
        S(.6f, () => { Log($"phase2 shield={Boss?.Shield} mark={Boss?.Mark.X}"); if (Boss != null) { Boss.State = 5; Boss.T2 = 1.6f; PlaceVoidRing(2); } });
        S(.8f, () => { Log($"void pending={VoidPending.Count}"); Shot("h7_void_telegraph"); });
        S(1.5f, () => { Log($"void tiles={VoidTemp.Count}"); Shot("h8_void"); if (Boss != null) { Boss.State = 6; Boss.T2 = .05f; } });
        S(.8f, () => { Log($"arena false lanterns={ArenaFalse.Count}"); Shot("h9_false"); if (Boss != null) { Boss.State = 7; Boss.T2 = .05f; } });
        S(1.5f, () => { Shot("h10_element"); Zones.Clear(); foreach (var p in W.ArenaPylons) PylonT[World.Key(p.X, p.Y)] = 5f; });
        S(.6f, () => { Log($"pylons on={PylonsOn()} shield={Boss?.Shield} vuln={Boss?.VulnT:F1}"); Shot("h11_broken"); });
        S(.2f, () => { if (Boss != null) { Boss.VulnT = 0; Boss.Hp = Boss.MaxHp * .3f; Boss.State = 0; } });
        S(.8f, () => { Log($"phase3 shield={Boss?.Shield} mark={Boss?.Mark.X}"); Shot("h12_phase3"); });
        S(.2f, () => { foreach (var p in W.ArenaPylons) PylonT[World.Key(p.X, p.Y)] = 5f; });
        S(.6f, () => { Log($"p3 pylons={PylonsOn()} vuln={Boss?.VulnT:F1}"); if (Boss != null) Hurt(Boss, 99999, Vector2.Right); });
        S(1.3f, () => { Log($"source defeated={BossDown(5)} cores={Inv[It.Core]} relic={OwnedRelics.Contains("r_voidlens")}"); Shot("h13_victory"); });
        // финал
        S(.2f, () =>
        {
            Tp(W.Ancients[0].Pos + new Vector2(0, 30)); BeaconLevel = 5; Cur = FindIx(); Log("beacon interact lines: " + string.Join(" | ", Cur.Lines));
            Cur.E?.Invoke(); Log($"finale started stage={FinStage} level={BeaconLevel} finished={Finished}");
        });
        S(3f, () => { Shot("f1_wave"); Log($"dawn={dawn:F2} stage={FinStage}"); });
        S(5.5f, () => { Shot("f2_dawn"); });
        S(3f, () => { Log($"scene stage={FinStage}"); Shot("f3_scene"); FinaleKey(Key.Enter); });
        S(1.5f, () => { Shot("f4_scene2"); FinaleKey(Key.Escape); });
        S(3f, () => { Log($"credits stage={FinStage}"); Shot("f5_credits"); FinaleKey(Key.Enter); });
        S(1.5f, () => { Log($"after finale stage={FinStage} finished={Finished} dawnTarget={DawnTarget}"); Shot("f6_after"); Save(); });
    }
}
