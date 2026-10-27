using Godot;
using System;
using System.Collections.Generic;
using System.IO;

namespace Lamplighter;

/// <summary>Итоговые автопроверки (LAMP_STAGE=final): границы чанков, производство при выгруженных чанках, возобновление ресурсов,
/// сохранение после смерти/финала. Результат — строки "CHECK ... PASS/FAIL".</summary>
public partial class Game
{
    int checkFails;
    void Check(string name, bool ok, string info = "") { if (!ok) checkFails++; GD.Print($"[check] {(ok ? "PASS" : "FAIL")} {name} {info}"); }

    void BuildFinalScript()
    {
        Building sw0 = null; Vector2I borderTile = default; Vector2I regrowTile = default; int stockBefore = 0;
        S(.3f, () =>
        {
            Inv[It.Wood] = 200; Inv[It.Stone] = 200; BossMask |= 15; BeaconLevel = 5; Hp = MaxHp = 300;
            // 1) здание на границе чанков: x=31|32 (граница чанков 0 и 1 по X)
            borderTile = new Vector2I(CS * 2 - 1, W.Spawn.Y - 8);
            SetObj(borderTile.X, borderTile.Y, Tl.Warehouse); SetObj(borderTile.X + 1, borderTile.Y, Tl.Stone);
            Check("chunk border tiles drawn", ChunkAt(borderTile.X, borderTile.Y)?.O.GetCellSourceId(new Vector2I(borderTile.X % CS, borderTile.Y % CS)) >= 0
                && ChunkAt(borderTile.X + 1, borderTile.Y)?.O.GetCellSourceId(new Vector2I((borderTile.X + 1) % CS, borderTile.Y % CS)) >= 0, $"chunks={LoadedChunks}");
            // 2) лесопилка у склада: производство при уходе игрока
            var wh = new Building { Type = Tl.Warehouse, X = W.Spawn.X + 2, Y = W.Spawn.Y - 3, Level = 2, Forced = true };
            var sw = new Building { Type = Tl.Sawmill, X = W.Spawn.X + 5, Y = W.Spawn.Y - 3, Level = 2, Forced = true, Worker = true };
            sw0 = sw; Buildings.Add(wh); Buildings.Add(sw); SetObj(wh.X, wh.Y, Tl.Warehouse); SetObj(sw.X, sw.Y, Tl.Sawmill);
            stockBefore = Stock[It.Wood];
            // 3) возобновление: найдём дерево в генерации вдали от поселения
            for (int y = 10; y < 118 && regrowTile.X == 0; y++) for (int x = 20; x < 100; x++)
                if (W.GenObj[y * World.Wd + x] == (byte)Tl.Tree && W.Obj[y * World.Wd + x] == (byte)Tl.Tree && Math.Abs(x - W.Spawn.X) > 25) { regrowTile = new Vector2I(x, y); break; }
            SetObj(regrowTile.X, regrowTile.Y, Tl.None);
            Tp(TileCenter(regrowTile.X, regrowTile.Y + 20));      // игрок далеко от пня (>14 клеток), чанк с пнём может быть выгружен
        });
        S(8f, () =>
        {
            // игрок ушёл далеко от здания: перелёт в Ледяной монастырь (чанки леса выгружаются)
            Tp(TileCenter(World.IX0 + 40, 64)); Log($"sawmill: status={sw0.Status} prog={sw0.Prog:F1} linked={sw0.Linked} forced={sw0.Forced} worker={sw0.Worker} stock={Stock[It.Wood]}");
        });
        S(1f, () =>
        {
            Check("far chunks unloaded", ChunkAt(W.Spawn.X, W.Spawn.Y) == null, $"loaded={LoadedChunks}");
            Check("chunks bounded", LoadedChunks <= 16, $"loaded={LoadedChunks}");
        });
        S(12f, () =>
        {
            Check("production continues while chunks unloaded", Stock[It.Wood] > stockBefore || Buildings.Exists(b => b.Type == Tl.Sawmill && b.Status != 3), $"wood {stockBefore}->{Stock[It.Wood]}");
            Log($"sawmill later: status={sw0.Status} prog={sw0.Prog:F1} stock={Stock[It.Wood]}"); Tp(TileCenter(regrowTile.X, regrowTile.Y + 20));
            for (int i = 0; i < 3000; i++) { regrowT = 0; UpdateRegrow(.1f); }
            Check("resource regrowth", W.Get(regrowTile.X, regrowTile.Y) == Tl.Tree || W.Get(regrowTile.X, regrowTile.Y) == W.GenObj[regrowTile.Y * World.Wd + regrowTile.X].ToTl(), $"tile={regrowTile} now={W.Get(regrowTile.X, regrowTile.Y)}");
            Tp(TileCenter(borderTile.X, borderTile.Y + 3)); UpdateChunks(0, true);
            Check("border building survives unload/reload", W.Get(borderTile.X, borderTile.Y) == Tl.Warehouse && ChunkAt(borderTile.X, borderTile.Y)?.O.GetCellSourceId(new Vector2I(borderTile.X % CS, borderTile.Y % CS)) >= 0);
        });
        S(.5f, () =>
        {
            // 4) смерть и сохранение
            Hp = 1; var inv = (int[])Inv.Clone(); HurtPlayer(50, Vector2.Zero); Invuln = 0;
            Check("death returns to beacon with full hp", Hp >= MaxHp - .1f && ToTile(PPos) == new Vector2I(W.Spawn.X, W.Spawn.Y), $"hp={Hp} pos={ToTile(PPos)}");
            Save();
            Check("save file valid after death", ValidSave(SavePath));
        });
        S(.3f, () =>
        {
            // 5) уникальные награды не дублируются
            int before = Owned.Count; GiveWeapon("staff_w", false); GiveWeapon("staff_w", false);
            var ids = new HashSet<string>(); bool dup = false; foreach (var o in Owned) if (!ids.Add(o.Id)) dup = true;
            Check("no duplicate unique weapon", !dup, $"owned={Owned.Count} (was {before})");
            int rel = OwnedRelics.Count; GiveRelic("r_prismcore", true); GiveRelic("r_prismcore", true);
            Check("no duplicate relic", OwnedRelics.FindAll(r => r == "r_prismcore").Count == 1);
            // финал и повторный вход
            Finished = true; BeaconLevel = 6; BossMask |= 31; LegendMask = 5; RiftUnlocked = 3; CosmMask = 3; LoreMask = 0b111;
            Save(); Log($"final-state saved: finished={Finished} legend={LegendMask} rift={RiftUnlocked}");
        });
        S(.3f, () =>
        {
            // 6) материалы склада при крафте
            Inv[It.Wood] = 0; Inv[It.Stone] = 0; Stock[It.Wood] = 100; Stock[It.Stone] = 100;
            var whb = Buildings.Find(b => b.Type == Tl.Warehouse); Tp(whb.Pos + new Vector2(0, 30));
            Check("craft uses warehouse stock near warehouse", Can(CostFurnace));
            Pay(CostFurnace); Check("stock paid", Stock[It.Stone] == 90 && Stock[It.Wood] == 95, $"stone={Stock[It.Stone]} wood={Stock[It.Wood]}");
            Tp(TileCenter(World.IX0 + 40, 64));
            Check("no warehouse crafting when far", !Can(CostFurnace));
            // 7) быстрый путь после победы над стражем и возрождение у ближайшего фонаря
            BossMask |= 1; var l = new List<Ancient>(); foreach (var an in W.Ancients) if (an != W.Ancients[0] && an.Region == 0 && !an.Boss && l.Count < 2) { an.Lit = true; an.Anim = 1; l.Add(an); }
            Check("fast travel list >= 3", TravelList().Count >= 3, $"n={TravelList().Count}");
            Tp(l[0].Pos + new Vector2(0, 30)); Enemies.Clear(); travelCd = 0; FastTravel(l[1]);
            Check("fast travel moved player", PPos.DistanceTo(l[1].Pos) < 60, $"dist={PPos.DistanceTo(l[1].Pos):F0}");
            Tp(l[1].Pos + new Vector2(60, 40)); Hp = 1; Invuln = 0; HurtPlayer(50, Vector2.Zero);
            Check("respawn at nearest lit lantern", PPos.DistanceTo(l[1].Pos) < 60 && Hp > 0, $"dist={PPos.DistanceTo(l[1].Pos):F0}");
        });
        S(.2f, () => GD.Print($"[check] SUMMARY failures={checkFails}"));
    }

    // Загрузка сохранённого финала (LAMP_LOAD=1 LAMP_STAGE=reload)
    void BuildReloadScript()
    {
        S(.3f, () =>
        {
            Check("reload: finished kept", Finished && BeaconLevel == 6);
            Check("reload: bosses kept", BossMask == 31, $"mask={BossMask}");
            Check("reload: endgame kept", LegendMask == 5 && RiftUnlocked == 3 && CosmMask == 3 && LoreMask == 7, $"legend={LegendMask} rift={RiftUnlocked} cosm={CosmMask} lore={LoreMask}");
            Check("reload: seals open", W.Get(W.Seal3[0].X, W.Seal3[0].Y) == Tl.None && W.Get(W.Seal4[0].X, W.Seal4[0].Y) == Tl.None);
            Check("reload: no finale re-trigger", FinStage == 0);
            Check("reload: weapon unique", Owned.FindAll(o => o.Id == "staff_w").Count <= 1);
            var bl = BossLantern(5); Tp(bl.Pos + new Vector2(0, 80));
        });
        S(3f, () => { Check("reload: final boss not respawned", Boss == null && !Enemies.Exists(e => e.IsBoss)); Check("reload: rift gate present", W.Get(RiftGateTile().X, RiftGateTile().Y) == Tl.RiftGate); GD.Print($"[check] SUMMARY failures={checkFails}"); });
    }
}

static class TlExt { public static Tl ToTl(this byte b) => (Tl)b; }

public partial class Game
{
    /// <summary>Длительный прогон (LAMP_STAGE=soak): ~100 с перелётов по всем биомам, врагов, событий; контроль роста объектов, памяти и чанков.</summary>
    void BuildSoakScript()
    {
        double obj0 = 0, mem0 = 0; double maxProc = 0; int maxEnemies = 0, maxParts = 0, maxZones = 0, maxChunks = 0; var fpsList = new List<float>();
        S(.5f, () =>
        {
            BossMask |= 15; BeaconLevel = 5; EventsEnabled = true; Hp = MaxHp = 5000; noHeal = false;
            foreach (var sl in new[] { W.Seal1, W.Seal2, W.Seal3, W.Seal4 }) foreach (var t in sl) SetObj(t.X, t.Y, Tl.None);
        });
        S(3f, () => { obj0 = Performance.GetMonitor(Performance.Monitor.ObjectCount); mem0 = Performance.GetMonitor(Performance.Monitor.MemoryStatic) / 1048576.0; Log($"soak start: objects={obj0} mem={mem0:F0}MB"); });
        int[] xs = { 70, 190, 320, 450, 560, 60, 200, 330, 470, 570 };
        for (int i = 0; i < 28; i++)
        {
            int ii = i;
            S(3.5f, () =>
            {
                var tx = xs[ii % xs.Length] + (ii / xs.Length) * 11; var tile = new Vector2I(tx, 40 + (ii * 17) % 60);
                // ближайшая проходимая клетка
                for (int r = 0; r < 12; r++) { bool found = false; for (int dy = -r; dy <= r && !found; dy++) for (int dx = -r; dx <= r; dx++) if (W.Walkable(tile.X + dx, tile.Y + dy)) { tile = new Vector2I(tile.X + dx, tile.Y + dy); found = true; break; } if (found) break; }
                Tp(TileCenter(tile.X, tile.Y)); UpdateChunks(0, true);
                for (int k = 0; k < 14; k++) { var e = SpawnEnemy(PPos + new Vector2(rng.RandfRange(-130, 130), rng.RandfRange(-90, 90)), ii % 2 == 0 ? EK.Shade : EK.Wraith); e.Age = 1; }
                if (ii % 5 == 0) for (int z = 0; z < 8; z++) AddZone(PPos + new Vector2(rng.RandfRange(-60, 60), rng.RandfRange(-60, 60)), 20, .5f, 3f, 0f);
                maxProc = Math.Max(maxProc, Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000); fpsList.Add((float)Engine.GetFramesPerSecond());
                maxEnemies = Math.Max(maxEnemies, Enemies.Count); maxParts = Math.Max(maxParts, Parts.Count); maxZones = Math.Max(maxZones, Zones.Count); maxChunks = Math.Max(maxChunks, LoadedChunks);
            });
        }
        S(1f, () =>
        {
            Enemies.Clear(); Zones.Clear(); Parts.Clear(); Projs.Clear();
            double obj1 = Performance.GetMonitor(Performance.Monitor.ObjectCount), mem1 = Performance.GetMonitor(Performance.Monitor.MemoryStatic) / 1048576.0;
            fpsList.Sort(); float p5 = fpsList.Count > 0 ? fpsList[(int)(fpsList.Count * .05f)] : 0;
            Log($"soak end: objects={obj0}->{obj1} mem={mem0:F0}->{mem1:F0}MB maxProcessMs={maxProc:F1} fps p5={p5:F0} maxEnemies={maxEnemies} maxParts={maxParts} maxZones={maxZones} maxChunks={maxChunks} nodes={Performance.GetMonitor(Performance.Monitor.ObjectNodeCount)}");
            Check("soak: objects growth < 25%", obj1 < obj0 * 1.25 + 200, $"{obj0}->{obj1}");
            Check("soak: memory growth < 60 MB", mem1 - mem0 < 60, $"{mem0:F0}->{mem1:F0}");
            Check("soak: chunks bounded", maxChunks <= 30, $"{maxChunks}");
            Check("soak: particles/zones capped", maxParts <= 900 && maxZones <= 75, $"parts={maxParts} zones={maxZones}");
            Check("soak: fps p5 >= 60", p5 >= 60, $"p5={p5:F0}");
            GD.Print($"[check] SUMMARY failures={checkFails}");
        });
    }
}
