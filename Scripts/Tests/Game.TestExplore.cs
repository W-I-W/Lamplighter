using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Lamplighter;

/// <summary>Живой мир (LAMP_STAGE=explore): генерация на многих сидах (новые места достижимы и не толпятся, виды фонарей, детерминизм, совместимость со старой генерацией),
/// затем игровые цепочки: башня, сокровищница, поляна, охраняемый/повреждённый/заражённый фонари, награды без повторов, сохранение состояний.</summary>
public partial class Game
{
    void BuildExploreScript()
    {
        int fails = 0;
        void Check(bool ok, string what) { if (!ok) fails++; GD.Print($"[check] {(ok ? "PASS" : "FAIL")} {what}"); }
        SaveData ReadSave() => JsonSerializer.Deserialize<SaveData>(File.ReadAllText(SavePath), new JsonSerializerOptions { IncludeFields = true });
        Poi P(PK k) => W.Pois.Find(p => p.Kind == k && (k != PK.Camp || p.Variant == 8) && World.RegionOf(p.Tile.X) == 0);
        Ancient Lamp(LampKind k) { foreach (var a in W.Ancients) if (a.Kind == k && !a.Boss && a != W.Ancients[0]) return a; return null; }
        int Idx(Ancient a) => W.Ancients.IndexOf(a);
        bool Opens(Poi p) { var ix = new Ix(); PoiIx(p, ix); return ix.E != null; }

        // ── генерация на многих сидах
        S(.3f, () =>
        {
            int bad = 0, seeds = 0; var counts = new int[5]; int mysteryWorlds = 0, hidden = 0, waterWorlds = 0; var regionKinds = new int[4, 24];
            for (int i = 1; i <= 40; i++)
            {
                int seed = i * 7919 % 100003; seeds++;
                var w = World.Generate(seed); var w2 = World.Generate(seed); var old = World.Generate(seed, 0); var g1 = World.Generate(seed, 1); var g2 = World.Generate(seed, 2);
                bool same = w.Obj.SequenceEqual(w2.Obj) && w.Pois.Count == w2.Pois.Count;
                foreach (var s in new[] { w.Seal1, w.Seal2, w.Seal3, w.Seal4 }) foreach (var t in s) w.Set(t.X, t.Y, Tl.None);
                foreach (var g in w.HeartGaps) foreach (var t in g) w.Set(t.X, t.Y, Tl.LightBridge);
                var reach = w.Reach(w.Spawn);
                var news = w.Pois.Where(p => p.Kind == PK.Tower || p.Kind == PK.Vault || p.Kind == PK.Glade || (p.Kind == PK.Camp && p.Variant == 8) || p.Kind == PK.SparkAltar || p.Kind == PK.RuneObelisk || p.Kind == PK.GiantTree || p.Kind == PK.Survivor || (p.Kind == PK.Secret && p.Variant == 10)).ToList();
                var kinds = new[] { PK.Tower, PK.Vault, PK.Glade, PK.SparkAltar, PK.RuneObelisk, PK.GiantTree }; hidden += news.Count(p => p.Kind == PK.Secret);
                foreach (var k in kinds) if (!news.Exists(p => p.Kind == k)) { bad++; GD.Print($"[explore] seed {seed}: no {k}"); }
                if (!news.Exists(p => p.Kind == PK.Camp)) { bad++; GD.Print($"[explore] seed {seed}: no dark camp"); }
                foreach (var p in news)
                {
                    var acc = p.Kind == PK.Vault || (p.Kind == PK.Survivor && p.Variant == 3) ? new Vector2I(p.Tile.X, p.Tile.Y + 3) : new Vector2I(p.Tile.X, p.Tile.Y + (p.Kind == PK.Tower ? 1 : 1));
                    bool any = false; foreach (var d in World.D4) { var n = (p.Kind == PK.Vault ? acc : p.Tile) + d; if (reach[World.Key(n.X, n.Y)]) any = true; }
                    if (p.Kind == PK.Vault || (p.Kind == PK.Survivor && p.Variant == 3)) any = reach[World.Key(acc.X, acc.Y)];
                    if (!any) { bad++; GD.Print($"[explore] seed {seed}: {p.Kind} {p.Tile} is not reachable"); }
                    foreach (var q in w.Pois) if (q != p && (q.Tile - p.Tile).LengthSquared() < 64 && news.IndexOf(q) < news.IndexOf(p) + 1 && w.Pois.IndexOf(q) < w.Pois.IndexOf(p)) { bad++; GD.Print($"[explore] seed {seed}: {p.Kind} too close to {q.Kind}"); }
                    foreach (var a in w.Ancients) if (Math.Abs(a.Tile.X - p.Tile.X) < 5 && Math.Abs(a.Tile.Y - p.Tile.Y) < 5) { bad++; GD.Print($"[explore] seed {seed}: {p.Kind} overlaps a lantern"); }
                }
                foreach (var a in w.Ancients) { var tt = a.Tile + new Vector2I(0, a.Boss ? 3 : 2); if (!reach[World.Key(tt.X, tt.Y)]) { bad++; GD.Print($"[explore] seed {seed}: lantern {a.Tile} unreachable"); } }
                // виды фонарей: первый обычный, остальные четыре — все разные
                var ks = new List<LampKind>(); for (int a = 1; a <= 5; a++) ks.Add(w.Ancients[a].Kind);
                if (w.Ancients[1].Kind != LampKind.Normal || ks.Distinct().Count() < 4 && ks.Count(k => k == LampKind.Infected) == 0) { bad++; GD.Print($"[explore] seed {seed}: lamp kinds {string.Join(",", ks)}"); }
                foreach (var a in w.Ancients) if (a.Kind == LampKind.Infected && w.Get(a.Crystal.X, a.Crystal.Y) != Tl.ShadowPillar) { bad++; GD.Print($"[explore] seed {seed}: infected lamp without a crystal"); }
                foreach (var k in ks) counts[(int)k]++;
                // другие биомы: башня, сокровищница, алтарь и обелиск в каждом; озёра только в новой версии
                foreach (var q in news) { int rg = World.RegionOf(q.Tile.X); if (rg > 0 && rg <= 3) regionKinds[rg, (int)q.Kind]++; }
                for (int rg = 1; rg <= 3; rg++) foreach (var k in new[] { PK.Tower, PK.Vault, PK.SparkAltar, PK.RuneObelisk }) if (regionKinds[rg, (int)k] == 0) { bad++; GD.Print($"[explore] seed {seed}: no {k} in biome {rg}"); }
                regionKinds = new int[4, 24];
                int wat(World ww) { int n = 0; for (int y = 0; y < World.Ht; y++) for (int x = 0; x < World.N; x++) if (ww.Ground[World.Key(x, y)] == Art.WaterG) n++; return n; }
                int w3 = wat(w); if (w3 > 12) waterWorlds++;
                if (wat(g2) != 0 || g2.Pois.Any(q => q.Kind == PK.RuneObelisk || q.Kind == PK.GiantTree) || g2.Pois.Count >= w.Pois.Count) { bad++; GD.Print($"[explore] seed {seed}: generation 2 changed"); }
                // загадочный фонарь: три светила и записка достижимы, порядок касания задан
                foreach (var a in w.Ancients.Where(a => a.Kind == LampKind.Mystery))
                {
                    mysteryWorlds++;
                    if (a.Pylons == null || a.Seq == null || a.Seq.Distinct().Count() != 3 || a.Pylons.Any(t => w.Get(t.X, t.Y) != Tl.Pylon) || w.Get(a.Tablet.X, a.Tablet.Y) != Tl.Tablet) { bad++; GD.Print($"[explore] seed {seed}: broken mystery lamp"); continue; }
                    foreach (var t in a.Pylons.Append(a.Tablet)) if (!World.D4.Any(d => reach[World.Key(t.X + d.X, t.Y + d.Y)])) { bad++; GD.Print($"[explore] seed {seed}: mystery pylon {t} is unreachable"); break; }
                }
                // версия генерации 1 (прежние новые игры) не получает загадочного фонаря, алтаря и скрытых тайников
                if (g1.Ancients.Any(a => a.Kind == LampKind.Mystery) || g1.Pois.Any(p => p.Kind == PK.SparkAltar || (p.Kind == PK.Secret && p.Variant == 10)) || g1.Pois.Count < old.Pois.Count + 3) { bad++; GD.Print($"[explore] seed {seed}: generation 1 changed"); }
                // детерминизм и совместимость: одинаковый сид — тот же мир; прежняя генерация не получает ничего нового и не меняет порядок старых мест
                if (!same) { bad++; GD.Print($"[explore] seed {seed}: generation is not deterministic"); }
                if (old.Pois.Count + news.Count != w.Pois.Count || old.Ancients.Any(a => a.Kind != LampKind.Normal)) { bad++; GD.Print($"[explore] seed {seed}: old generation changed (old {old.Pois.Count} new {w.Pois.Count} +{news.Count})"); }
                for (int j = 0; j < old.Pois.Count && j < w.Pois.Count; j++) if (old.Pois[j].Tile != w.Pois[j].Tile || old.Pois[j].Kind != w.Pois[j].Kind) { bad++; GD.Print($"[explore] seed {seed}: old poi order changed at {j}"); break; }
            }
            Check(bad == 0, $"explore: {seeds} seeds — new places reachable, spaced, lanterns varied, deterministic, old generation intact (problems={bad})");
            Check(counts[1] > 0 && counts[2] > 0 && counts[3] > 0, $"explore: all lamp kinds occur (guarded {counts[1]}, damaged {counts[2]}, infected {counts[3]})");
            Check(waterWorlds >= seeds * 3 / 4, $"explore: ponds and streams appear in the forest ({waterWorlds}/{seeds} worlds)");
            Check(mysteryWorlds >= seeds * 3 / 4 && hidden >= seeds * 2, $"explore: mystery lamps in {mysteryWorlds}/{seeds} worlds, hidden caches {hidden} in total");
            Check(PoiDefs.All.Count >= 4, $"explore: place kinds come from Resource files ({PoiDefs.All.Count})");
            Menu = Mn.None; noHeal = true; Hp = MaxHp; Invuln = 999f; Enemies.Clear();
            Check(W.GenV == World.CurrentGen, "explore: a new game uses the current generation");
        });

        // ── башня: стража → сундук → уникальная награда
        S(.3f, () =>
        {
            var p = P(PK.Tower); Check(p != null, "tower: exists in this world"); if (p == null) return;
            Tp(p.Pos + new Vector2(0, 40)); poiT = 0;
        });
        S(.8f, () =>
        {
            var p = P(PK.Tower); if (p == null) return;
            Shot("explore_tower"); int g = Enemies.Count(e => e.Guard == p);
            Check(p.State == 1 && g == p.Guards, $"tower: guards come out when approaching ({g}/{p.Guards})");
            Check(!Opens(p), "tower: the chest is locked while guards live");
            Enemies.RemoveAll(e => e.Guard == p); poiT = 0;
        });
        S(.8f, () =>
        {
            var p = P(PK.Tower); if (p == null) return;
            Check(p.State == 2, "tower: guards defeated opens the chest");
            int relics = OwnedRelics.Count, coins = Inv[It.Coins];
            var ix = new Ix(); PoiIx(p, ix); ix.E?.Invoke();
            Check(OwnedRelics.Contains("r_beaconwick") && p.State == 3 && W.Get(p.Tile.X, p.Tile.Y) == Tl.ChestOpen, "tower: unique relic granted and chest opened");
            Check(!Opens(p), "tower: an opened chest cannot be opened again");
            int rn = OwnedRelics.Count; GrantTag(p.Tag, p.Pos); Check(OwnedRelics.Count == rn, "tower: the same relic is never added twice");
        });

        // ── сокровищница: хрупкая стена → сундук
        S(.3f, () =>
        {
            var p = P(PK.Vault); Check(p != null, "vault: exists in this world"); if (p == null) return;
            var rub = new Vector2I(p.Tile.X, p.Tile.Y + 2);
            Check(W.Get(rub.X, rub.Y) == Tl.Rubble, "vault: a cracked wall is the way in");
            Check(MarkerPois().All(m => m != p), "vault: is not shown on the screen markers before it is found");
            Tp(TileCenter(rub.X, rub.Y + 2)); poiT = 0;
        });
        S(.5f, () =>
        {
            var p = P(PK.Vault); if (p == null) return; var rub = new Vector2I(p.Tile.X, p.Tile.Y + 2);
            Shot("explore_vault"); BreakTile(rub, Tl.Rubble, false);
            Check(W.Get(rub.X, rub.Y) == Tl.None, "vault: the wall can be broken");
        });
        S(.4f, () =>
        {
            var p = P(PK.Vault); if (p == null) return;
            var ix = new Ix(); PoiIx(p, ix); ix.E?.Invoke();
            Check(OwnedRelics.Contains("r_sparkcoil") && p.State == 3, "vault: rare relic granted once");
            Check(!Opens(p), "vault: cannot be looted twice");
        });

        // ── редкая поляна
        S(.3f, () =>
        {
            var p = P(PK.Glade); Check(p != null, "glade: exists in this world"); if (p == null) return;
            Tp(p.Pos + new Vector2(0, 40)); poiT = 0;
        });
        S(.8f, () =>
        {
            var p = P(PK.Glade); if (p == null) return;
            Shot("explore_glade"); if (p.Guards > 0) { Check(p.State == 1 && Enemies.Any(e => e.Guard == p), "glade: a guardian appears"); Enemies.RemoveAll(e => e.Guard == p); poiT = 0; }
        });
        S(.8f, () => { var p = P(PK.Glade); if (p != null) Check(p.State == 3, "glade: finished after the guardian (or without one)"); });

        // ── охраняемый фонарь
        S(.3f, () =>
        {
            var a = Lamp(LampKind.Guarded); Check(a != null, "guarded lamp: exists"); if (a == null) return;
            a.Phase = 0; a.Done = false; Enemies.Clear();           // телепорты прошлых шагов могли пройти мимо этого фонаря и очистить его стражу
            Inv[It.Shard] = 60; Check(!LampReady(a, out _), "guarded lamp: blocked until guards are beaten");
            Tp(a.Pos + new Vector2(0, 60)); lampT = 0;
        });
        S(.8f, () =>
        {
            var a = Lamp(LampKind.Guarded); if (a == null) return;
            Check(a.Phase == 1 && Enemies.Any(e => e.Guard == a.GuardPoi), "guarded lamp: guards come out");
            Activate(a, Idx(a)); Check(!a.Lit, "guarded lamp: cannot be lit while guards live");
            Enemies.RemoveAll(e => e.Guard == a.GuardPoi); lampT = 0;
        });
        S(.8f, () =>
        {
            var a = Lamp(LampKind.Guarded); if (a == null) return;
            Check(a.Done, "guarded lamp: guards beaten");
            Activate(a, Idx(a)); Check(a.Lit, "guarded lamp: lit after the fight");
        });

        // ── повреждённый фонарь
        S(.3f, () =>
        {
            var a = Lamp(LampKind.Damaged); Check(a != null, "damaged lamp: exists"); if (a == null) return;
            Inv[It.Shard] = 60; Inv[It.Wood] = Inv[It.Stone] = Inv[It.Copper] = 0; Tp(a.Pos + new Vector2(0, 40));
            Activate(a, Idx(a)); Check(!a.Lit, "damaged lamp: needs repair resources");
            Inv[It.Wood] = 20; Inv[It.Stone] = 20; Inv[It.Copper] = 20; int sh = Inv[It.Shard];
            Activate(a, Idx(a));
            Check(a.Lit && Inv[It.Wood] == 12 && Inv[It.Copper] == 17 && Inv[It.Shard] == sh - a.Cost, "damaged lamp: repaired and lit, resources spent");
        });

        // ── заражённый фонарь
        S(.3f, () =>
        {
            var a = Lamp(LampKind.Infected); Check(a != null, "infected lamp: exists"); if (a == null) return;
            Inv[It.Shard] = 60; Tp(a.Pos + new Vector2(0, 40)); Activate(a, Idx(a)); Check(!a.Lit, "infected lamp: shadow crystal blocks it");
            BreakTile(a.Crystal, Tl.ShadowPillar, false); lampT = 0;
        });
        S(.8f, () =>
        {
            var a = Lamp(LampKind.Infected); if (a == null) return;
            Check(a.Done, "infected lamp: purified after the crystal is destroyed");
            Activate(a, Idx(a)); Check(a.Lit, "infected lamp: lit");
            Check(GenAncientTilesLit(), "lamps: lit lanterns free their territory (veins revealed, light radius)");
        });

        // ── загадочный фонарь: светила в нужном порядке
        S(.3f, () =>
        {
            var a = Lamp(LampKind.Mystery); Check(a != null && a.Pylons != null, "mystery lamp: exists"); if (a == null || a.Pylons == null) return;
            Inv[It.Shard] = 60; Tp(a.Pos + new Vector2(0, 40)); Check(!LampReady(a, out var why) && why != "", "mystery lamp: blocked until the puzzle is solved");
            var tix = new Ix(); MysteryIx(a.Tablet.X, a.Tablet.Y, Tl.Tablet, tix);
            Check(tix.Lines.Count > 0 && tix.Lines[0].Contains(PylonSide[a.Seq[0]]), "mystery lamp: the note tells the order");
            Activate(a, Idx(a)); Check(!a.Lit, "mystery lamp: cannot be lit before the puzzle");
            int wrong = a.Seq[0] == 0 ? 1 : 0; TouchPylon(a, wrong);
            Check(a.SeqPos == 0 && a.Pylons.All(t => W.Get(t.X, t.Y) == Tl.Pylon), "mystery lamp: a wrong touch resets the lights");
            var pix = new Ix(); MysteryIx(a.Pylons[a.Seq[0]].X, a.Pylons[a.Seq[0]].Y, Tl.Pylon, pix); Check(pix.E != null, "mystery lamp: a light can be touched");
            TouchPylon(a, a.Seq[0]); TouchPylon(a, a.Seq[1]); Check(a.SeqPos == 2 && !a.Done, "mystery lamp: two right touches are not enough");
            TouchPylon(a, a.Seq[1]); Check(a.SeqPos == 0 && !a.Done, "mystery lamp: touching a lit light again breaks the order");
            TouchPylon(a, a.Seq[0]); TouchPylon(a, a.Seq[1]); TouchPylon(a, a.Seq[2]);
            Check(a.Done && a.Pylons.All(t => W.Get(t.X, t.Y) == Tl.PylonOn), "mystery lamp: solved, all lights burn");
            Activate(a, Idx(a)); Check(a.Lit, "mystery lamp: lit after the puzzle");
        });

        // ── обелиск рун
        S(.3f, () =>
        {
            var p = W.Pois.Find(q => q.Kind == PK.RuneObelisk && World.RegionOf(q.Tile.X) == 0); Check(p != null, "rune obelisk: exists"); if (p == null) return;
            Tp(p.Pos + new Vector2(0, 40)); var r = Rune(p); var ix = new Ix(); RuneIx(p, ix); Check(ix.E != null, "rune obelisk: can be listened to");
            RuneTouch(p, r.Seq[0]); Check(r.Pos == 0, "rune obelisk: touching before listening does nothing");
            ix.E?.Invoke(); Check(r.Showing, "rune obelisk: shows the order");
            RuneTouch(p, r.Seq[0]); Check(r.Pos == 0, "rune obelisk: touching while it shows does nothing");
        });
        S(5.2f, () => { });
        S(.3f, () =>
        {
            var p = W.Pois.Find(q => q.Kind == PK.RuneObelisk && World.RegionOf(q.Tile.X) == 0); if (p == null) return; var r = Rune(p);
            Check(!r.Showing, "rune obelisk: the demonstration ends");
            RuneTouch(p, r.Seq[0] == 0 ? 1 : 0); Check(r.Pos == 0 && Enumerable.Range(0, 4).All(i => W.Get(RunePylon(p, i).X, RunePylon(p, i).Y) == Tl.Pylon), "rune obelisk: a wrong touch resets the lights");
            RuneTouch(p, r.Seq[0]); RuneTouch(p, r.Seq[1]); RuneTouch(p, r.Seq[2]); Check(r.Pos == 3 && p.State == 0, "rune obelisk: three right touches are not enough");
            RuneTouch(p, r.Seq[3]); Check(p.State == 3 && OwnedRelics.Contains("r_merchant") && W.Get(p.Tile.X, p.Tile.Y) == Tl.ObeliskUsed, "rune obelisk: solved, relic granted");
            var ix = new Ix(); RuneIx(p, ix); Check(ix.E == null, "rune obelisk: solved obelisk is silent");
        });

        // ── древо-хранитель
        S(.3f, () =>
        {
            var p = P(PK.GiantTree); Check(p != null, "giant tree: exists"); if (p == null) return;
            Check(Enumerable.Range(-1, 3).All(i => W.Get(p.Tile.X + i, p.Tile.Y - 1) == Tl.Cliff), "giant tree: the trunk is solid");
            Tp(p.Pos + new Vector2(0, 700)); Check(Marks().Any(m => new Vector2(m.X, m.Y + 48).DistanceTo(p.Pos) < 4), "giant tree: its silhouette shows from far away");
            Tp(p.Pos + new Vector2(0, 50)); Hp = MaxHp - 30;
        });
        S(.5f, () =>
        {
            var p = P(PK.GiantTree); if (p == null) return; Shot("explore_gianttree");
            var ix = new Ix(); GiantTreeIx(p, ix); ix.E?.Invoke();
            Check(p.State == 3 && Hp == MaxHp && OwnedRelics.Contains("r_dawnprism") && W.Get(p.Tile.X, p.Tile.Y) == Tl.ShrineUsed, "giant tree: one gift, healing and relic");
            int rn = OwnedRelics.Count; UseGiantTree(p); Check(OwnedRelics.Count == rn, "giant tree: the gift is given once");
        });

        // ── алтарь искр
        S(.3f, () =>
        {
            Shot("explore_mystery"); var p = P(PK.SparkAltar); Check(p != null, "spark altar: exists"); if (p == null) return;
            Tp(p.Pos + new Vector2(0, 300)); Check(Marks().Any(m => new Vector2(m.X, m.Y).DistanceTo(p.Pos) < 2), "spark altar: its silhouette shows through the dark");
            Tp(p.Pos + new Vector2(0, 30));
            Inv[It.Shard] = 2; UseSparkAltar(p); Check(p.State == 0 && Inv[It.Shard] == 2 && !OwnedRelics.Contains("r_luckycoin"), "spark altar: not enough sparks changes nothing");
            Inv[It.Shard] = 5; var vl = P(PK.Vault); var ix = new Ix(); SparkAltarIx(p, ix); ix.E?.Invoke();
            Check(p.State == 3 && Inv[It.Shard] == 2 && OwnedRelics.Contains("r_luckycoin") && W.Get(p.Tile.X, p.Tile.Y) == Tl.AltarOn, "spark altar: three sparks give the relic and wake the altar");
            Check(vl == null || vl.Revealed || vl.Seen, "spark altar: reveals the hidden vault");
            int rn = OwnedRelics.Count; UseSparkAltar(p); Check(Inv[It.Shard] == 2 && OwnedRelics.Count == rn, "spark altar: cannot be used twice");
        });

        // ── скрытый тайник: проявляется только от света
        S(.3f, () =>
        {
            Shot("explore_altar"); var p = W.Pois.Find(q => q.Kind == PK.Secret && q.Variant == 10); Check(p != null, "hidden cache: exists"); if (p == null) return;
            Check(W.Get(p.Tile.X, p.Tile.Y) == Tl.SecretCache, "hidden cache: invisible at the start");
            Tp(p.Pos + new Vector2(0, 40)); RevealSecrets(); Check(W.Get(p.Tile.X, p.Tile.Y) == Tl.SecretCache, "hidden cache: walking close does not reveal it");
            Lamps.Add(new Lamp { X = p.Tile.X + 1, Y = p.Tile.Y, Level = 1 }); RevealSecrets(); Lamps.RemoveAt(Lamps.Count - 1);
            Check(W.Get(p.Tile.X, p.Tile.Y) == Tl.Chest && p.Seen, "hidden cache: a lamp reveals it");
            var ix = new Ix(); PoiIx(p, ix); ix.E?.Invoke(); Check(p.State == 3 && W.Get(p.Tile.X, p.Tile.Y) == Tl.ChestOpen, "hidden cache: can be opened");
        });

        // ── пробуждение теней
        Vector2 site = default; GEvent aw = null; int coinsBefore = 0;
        S(.3f, () =>
        {
            Enemies.Clear(); Events.Clear(); var c0 = ToTile(PPos); bool found = false;
            for (int r = 4; r < 60 && !found; r += 2)
                for (int a = 0; a < 16 && !found; a++)
                {
                    int cx = c0.X + (int)(Mathf.Cos(a * Mathf.Tau / 16) * r), cy = c0.Y + (int)(Mathf.Sin(a * Mathf.Tau / 16) * r); bool free = World.In(cx - 5, cy - 5) && World.In(cx + 5, cy + 5);
                    for (int y = cy - 3; y <= cy + 3 && free; y++) for (int x = cx - 3; x <= cx + 3 && free; x++) free = W.Get(x, y) == Tl.None;
                    if (free) { site = TileCenter(cx, cy); found = true; }
                }
            Check(found, "awakening: a free site exists"); if (!found) return;
            aw = SpawnEvent("awaken", site); var ct = ToTile(site);
            Check(aw != null && aw.Count == 3 && AwakenOffs.All(o => W.Get(ct.X + o.X, ct.Y + o.Y) == Tl.ShadowPillar), "awakening: three shadow pillars appear");
            Check(EvName("awaken") != "" && EventPool().Contains("awaken"), "awakening: is part of the event pool");
            coinsBefore = Inv[It.Coins]; Tp(site + new Vector2(0, 70));
        });
        S(5.6f, () => { });
        S(.3f, () =>
        {
            if (aw == null) return;
            Check(Enemies.Any(e => e.Ev == aw), "awakening: shadows come out of the pillars");
            Check(Enemies.Count(e => e.Ev == aw) <= 5, "awakening: the number of shadows is capped");
            var ct = ToTile(site); foreach (var o in AwakenOffs) BreakTile(new Vector2I(ct.X + o.X, ct.Y + o.Y), Tl.ShadowPillar, false);
        });
        S(.6f, () =>
        {
            if (aw == null) return;
            Check(!Events.Contains(aw) && Inv[It.Coins] > coinsBefore && !Enemies.Any(e => e.Ev == aw), "awakening: destroying the pillars ends the event with a reward");
            var e2 = SpawnEvent("awaken", site); var ct = ToTile(site); aw = e2;
            Check(e2 != null && W.Get(ct.X, ct.Y - 3) == Tl.ShadowPillar, "awakening: can start again"); if (e2 != null) e2.T = e2.Life + 1f;
        });
        S(.6f, () =>
        {
            if (aw == null) return; var ct = ToTile(site);
            Check(!Events.Contains(aw) && AwakenOffs.All(o => W.Get(ct.X + o.X, ct.Y + o.Y) == Tl.None), "awakening: an unfinished event leaves no pillars behind");
        });

        // ── сохранение
        S(.3f, () =>
        {
            Save(); var d = ReadSave();
            Check(d.GenV == World.CurrentGen && d.AncDone != null && d.AncDone.Length == W.Ancients.Count, "save: generation version and lamp states are written");
            var inf = Lamp(LampKind.Infected); var gd = Lamp(LampKind.Guarded);
            Check(inf != null && d.AncDone[Idx(inf)] == 1 && gd != null && d.AncDone[Idx(gd)] == 1, "save: purified and guarded states are stored");
            var my = Lamp(LampKind.Mystery); Check(my != null && d.AncDone[Idx(my)] == 1, "save: the solved mystery lamp is stored");
            var tw = P(PK.Tower); var vl = P(PK.Vault); var rune = W.Pois.Find(q => q.Kind == PK.RuneObelisk && World.RegionOf(q.Tile.X) == 0);
            Check(rune != null && (d.PoiState[W.Pois.IndexOf(rune)] & 15) == 3, "save: the solved rune obelisk is stored");
            Check(tw != null && (d.PoiState[W.Pois.IndexOf(tw)] & 15) == 3 && vl != null && (d.PoiState[W.Pois.IndexOf(vl)] & 15) == 3, "save: opened chests are stored as finished");
            var oldW = World.Generate(d.Seed, 0); Check(oldW.Pois.Count < W.Pois.Count && oldW.GenV == 0, "save: an old save (generation 0) regenerates the old world without new places");
            GD.Print($"[check] SUMMARY failures={fails}"); GD.Print("[check] EXPLORE DONE");
        });
    }

    bool GenAncientTilesLit() { foreach (var a in W.Ancients) if (a.Lit && a != W.Ancients[0] && W.Get(a.Tile.X, a.Tile.Y) != Tl.AncOn && W.Get(a.Tile.X, a.Tile.Y) != Tl.BossOn) return false; return true; }
}
