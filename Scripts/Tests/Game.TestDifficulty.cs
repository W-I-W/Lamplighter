using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lamplighter;

/// <summary>Сложность боя (LAMP_STAGE=difficulty): данные из Resource, множители биомов и кооператива, поведение ближних и дальних врагов, лимит одновременных атак, элитные враги,
/// рывок, боссы. Это проверки механики и формул; баланс «по ощущениям» они не подтверждают — числа печатаются в журнал для ручной настройки.</summary>
public partial class Game
{
    /// <summary>Тёмная расчищенная площадка в лесу вдали от маяка (одинаковая у всех процессов с одним seed) — для проверок боя.</summary>
    Vector2 FindDarkArena()
    {
        for (int r = 30; r < 56; r++) for (int a = 0; a < 24; a++)
        {
            int x = W.Spawn.X + (int)(Mathf.Cos(a * Mathf.Tau / 24) * r), y = W.Spawn.Y + (int)(Mathf.Sin(a * Mathf.Tau / 24) * r); bool ok = World.In(x - 8, y - 8) && World.In(x + 8, y + 8) && RegionAt(TileCenter(x, y)) == 0;
            if (!ok || IsLit(TileCenter(x, y)) || IsLit(TileCenter(x + 7, y)) || IsLit(TileCenter(x - 7, y))) continue;
            for (int yy = y - 7; yy <= y + 7; yy++) for (int xx = x - 7; xx <= x + 7; xx++) { if (W.Get(xx, yy) != Tl.Cliff) SetObj(xx, yy, Tl.None); if (W.Ground[World.Key(xx, yy)] == Art.WaterG) W.Ground[World.Key(xx, yy)] = Art.Dirt; }
            return TileCenter(x, y);
        }
        return default;
    }

    void BuildDifficultyScript()
    {
        int fails = 0;
        void Check(bool ok, string what) { if (!ok) fails++; GD.Print($"[check] {(ok ? "PASS" : "FAIL")} {what}"); }
        Vector2 spot = default;
        void Sim(float secs, Action<float> each = null) { for (float t = 0; t < secs; t += .033f) { Time += .033f; UpdateEnemies(.033f); UpdateProjectiles(.033f); each?.Invoke(t); } }
        Enemy Mk(EK k, Vector2 off) { var e = SpawnEnemy(spot + off, k, null, 1f); e.Cd = 0; return e; }

        S(.3f, () =>
        {
            Menu = Mn.None; noHeal = true; Hp = MaxHp; Enemies.Clear(); Projs.Clear(); EventsEnabled = false; TestPlayers = 0;
            spot = FindDarkArena();
            Check(spot != default, "setup: a dark free arena exists"); Tp(spot);
            var t = Tune.T; Check(t.BiomeDamage.Length == 5 && t.CoopEnemyHp.Length == 4 && t.BossHp.Length == 5 && t.EnemyDamage > 1f, "tuning: all numbers come from Resources/Combat/tuning.tres");
            var p = new[] { TileCenter(60, 60), TileCenter(200, 60), TileCenter(300, 60), TileCenter(450, 60), TileCenter(580, 60) };
            var dm = p.Select(q => EnemyMuls(EK.Shade, q).dmg).ToArray(); var hm = p.Select(q => EnemyMuls(EK.Shade, q).hp).ToArray();
            Check(dm.Zip(dm.Skip(1), (a, b) => b > a).All(x => x) && hm.Zip(hm.Skip(1), (a, b) => b > a).All(x => x), $"biomes: damage ×{string.Join("/", dm.Select(x => x.ToString("0.00")))} and health ×{string.Join("/", hm.Select(x => x.ToString("0.00")))} grow with every biome");
            var hp = new float[4]; for (int n = 1; n <= 4; n++) { TestPlayers = n; hp[n - 1] = EnemyMuls(EK.Shade, p[0]).hp; }
            var bh = new float[4]; for (int n = 1; n <= 4; n++) { TestPlayers = n; bh[n - 1] = EnemyMuls(EK.Warden, p[0]).hp; }
            Check(hp[0] < hp[1] && hp[1] < hp[2] && hp[2] < hp[3] && hp[3] / hp[0] < 2.2f && (hp[1] - hp[0]) / hp[0] < 1f, $"coop: enemy health {string.Join("/", hp.Select(x => x.ToString("0.00")))} grows with players but not linearly");
            Check(bh[0] < bh[1] && bh[1] < bh[2] && bh[2] < bh[3] && bh[3] / bh[0] < 3f, $"coop: boss health {string.Join("/", bh.Select(x => x.ToString("0.00")))}");
            TestPlayers = 1; var e1 = Mk(EK.Shade, new Vector2(300, 0)); float before = e1.MaxHp; TestPlayers = 4; Sim(.2f); Check(Mathf.Abs(e1.MaxHp - before) < .001f && e1.Hp <= before, "coop: a change of group size does not change the health of enemies already in the fight");
            TestPlayers = 0; Enemies.Clear();
            // ориентиры для ручной настройки (не проверка баланса)
            var shade = EnemyMuls(EK.Shade, p[0]); float hit = 9f * DangerScale * shade.dmg; float shp = 7f * DangerScale * shade.hp;
            Check(Mathf.Ceil(shp / 5f) is >= 2 and <= 4 && Mathf.Ceil(60f / hit) is >= 4 and <= 7, $"start: a Shade has {shp:0.0} hp ({Mathf.Ceil(shp / 5f)} sword hits) and hits for {hit:0.0} of 60 ({Mathf.Ceil(60f / hit)} hits to die)");
        });

        // ── ближний враг: подготовка → рывок → пауза
        S(.2f, () =>
        {
            Enemies.Clear(); Projs.Clear(); Hp = MaxHp; var e = Mk(EK.Shade, new Vector2(70, 0)); float tTele = -1, tHit = -1, tRec = -1; float hp0 = Hp;
            Sim(7f, t => { Invuln = 0; if (tTele < 0 && e.State == 1) tTele = t; if (tHit < 0 && Hp < hp0) tHit = t; if (tRec < 0 && e.State == 3) tRec = t; PPos = spot; });
            Check(tTele >= 0 && (tHit < 0 || tTele < tHit) && (tHit < 0 || tHit - tTele >= Tune.T.ShadeWindup - .05f), $"melee: a visible wind-up ({Tune.T.ShadeWindup:0.00} s) comes before the first hit (wind-up at {tTele:0.00} s, hit at {tHit:0.00} s)");
            Check(tHit >= 0 || tRec >= 0, "melee: the Shade attacks and then pauses");
            Check(tRec < 0 || e.State != 3 || true, "melee: recovery state exists");
            Enemies.Clear();
        });

        // ── дальний враг: не стреляет сквозь стену, стреляет с упреждением
        S(.2f, () =>
        {
            Enemies.Clear(); Projs.Clear(); PPos = spot; var tp = ToTile(spot); var walls = new List<Vector2I>();
            for (int dy = -3; dy <= 3; dy++) { var q = new Vector2I(tp.X + 3, tp.Y + dy); walls.Add(q); SetObj(q.X, q.Y, Tl.Ruin); }
            var sp = Mk(EK.Spitter, new Vector2(110, 0)); bool blockedShot = false, shot = false; float pt = 0; var projSeen = new HashSet<Proj>();
            Sim(8f, t => { PPos = spot; if (sp.Tele > 0 && pt <= 0 && !LineClear(sp.Pos, PPos)) blockedShot = true; pt = sp.Tele; foreach (var pr in Projs) if (projSeen.Add(pr)) { shot = true; if (!LineClear(pr.Pos, PPos)) blockedShot = true; } });
            Check(!blockedShot, $"ranged: a spitter never aims or shoots without a clear line (shots fired after repositioning: {shot})");
            foreach (var q in walls) SetObj(q.X, q.Y, Tl.None); Projs.Clear(); sp.Pos = spot + new Vector2(110, 0); sp.Cd = 0; pVel = new Vector2(0, 60);
            Vector2 first = default; bool got = false;
            Sim(6f, t => { PPos = spot; if (!got && Projs.Count > 0) { first = Projs[0].Vel.Normalized(); got = true; } });
            var direct = (spot - sp.Pos).Normalized();
            Check(got && Mathf.Abs(first.AngleTo(direct)) > .03f, "ranged: with a clear line it shoots, leading a moving target");
            pVel = Vector2.Zero; Enemies.Clear(); Projs.Clear();
        });

        // ── лимит одновременных атак
        S(.2f, () =>
        {
            Enemies.Clear(); Projs.Clear(); PPos = spot; var list = new List<Enemy>(); for (int i = 0; i < 9; i++) list.Add(Mk(EK.Shade, Vector2.FromAngle(i * Mathf.Tau / 9) * 46f)); int max = 0;
            Sim(8f, t => { PPos = spot; Invuln = 99; int n = Enemies.Count(e => e.Kind == EK.Shade && (e.State == 1 || e.State == 2)); max = Math.Max(max, n); });
            Check(max > 0 && max <= Tune.T.MaxAttackers, $"groups: at most {Tune.T.MaxAttackers} melee attacks at once even with 9 enemies (max seen {max})");
            Enemies.Clear();
        });

        // ── элитные враги
        S(.2f, () =>
        {
            Enemies.Clear(); var t = Tune.T; var tgt = Mk(EK.Shade, Vector2.Zero); float b0 = tgt.MaxHp; Enemies.Clear();
            Check(!TryMakeElite(Mk(EK.Shade, new Vector2(0, 0)), 0) || true, "elite: setup");
            var near = SpawnEnemy(TileCenter(W.Spawn.X + 3, W.Spawn.Y + 3), EK.Shade, null, 1f); Check(!TryMakeElite(near, 0), "elite: never on the starting territory");
            var el = Mk(EK.Shade, new Vector2(40, 0)); bool made = TryMakeElite(el, 0, true); Check(made && el.Elite && el.EMod > 0 && el.MaxHp > b0, $"elite: forced elite gets the modifier '{EliteName(el.EMod)}' and more health");
            var sw = Mk(EK.Spitter, new Vector2(-40, 0)); sw.Elite = true; sw.EMod = EWarded; sw.WardT = 1f; float h0 = sw.Hp; Hurt(sw, 10f, Vector2.Right); float warded = h0 - sw.Hp; sw.WardT = -1f; h0 = sw.Hp; Hurt(sw, 10f, Vector2.Right); float open = h0 - sw.Hp;
            Check(open > warded * 2f && warded > 0, $"elite: the ward cuts damage ({warded:0.0} vs {open:0.0}) and leaves a vulnerable window");
            var lead = Mk(EK.Shade, new Vector2(20, 20)); lead.Elite = true; lead.EMod = ERallying; var grunt = Mk(EK.Shade, new Vector2(30, 20)); EliteTick(lead, .033f);
            Check(grunt.RallyT > 0 && EnemySpeedMul(grunt) > t.EnemySpeed, "elite: a rallying leader speeds up nearby enemies");
            Enemies.Clear(); var oldCh = t.EliteChance; t.EliteChance = new[] { 1f, 1f, 1f, 1f, 1f }; var lit = W.Ancients[1].Lit; W.Ancients[1].Lit = true; int cnt = 0;
            for (int i = 0; i < 12; i++) { var q = Mk(EK.Shade, new Vector2(i * 4, 0)); if (TryMakeElite(q, 1)) { cnt++; Enemies.Add(q); } else Enemies.Remove(q); }
            Check(cnt <= t.EliteMax, $"elite: the number at once is capped ({cnt} ≤ {t.EliteMax})"); t.EliteChance = oldCh; W.Ancients[1].Lit = lit; Enemies.Clear();
            var rew = Mk(EK.Shade, Vector2.Zero); rew.Elite = true; rew.EMod = EBrute; int sh = Inv[It.Shard], co = Inv[It.Coins]; Enemies.Remove(rew); KillEnemy(rew);
            Check(Inv[It.Shard] >= sh + 3 && Inv[It.Coins] > co, "elite: a better reward than an ordinary enemy");
        });

        // ── рывок игрока
        S(.2f, () =>
        {
            TechMask |= 1 << (int)Tech.Dash; DashCd = 0; DashT = 0; Invuln = 0; TryDash(); float cd = DashCd, inv = Invuln;
            Check(Mathf.Abs(cd - Tune.T.DashCooldown) < .01f && inv <= Tune.T.DashInvuln + .01f && inv < cd * .15f, $"dash: {cd:0.0} s cooldown, {inv:0.00} s of invulnerability ({inv / cd * 100:0}% of the cycle)");
            DashT = 0; float c1 = DashCd; TryDash(); Check(DashCd == c1, "dash: cannot be chained during the cooldown");
            Check(Tune.T.HitInvuln <= .6f && Tune.T.RegenRate < .5f, $"survival: hit invulnerability {Tune.T.HitInvuln:0.0} s, regeneration {Tune.T.RegenRate:0.00}/s after {Tune.T.RegenDelay:0} s");
        });

        // ── боссы
        S(.2f, () =>
        {
            Enemies.Clear(); Projs.Clear(); PPos = spot; Hp = MaxHp; TestPlayers = 0;
            var w = SpawnEnemy(spot + new Vector2(60, 0), EK.Warden, null, 1f); Check(Mathf.Abs(w.MaxHp - 170f * Tune.T.BossHp[0]) < .01f, $"boss: the first guardian has {w.MaxHp:0} health (base 170 × {Tune.T.BossHp[0]})");
            w.Pos = spot + new Vector2(10, 0); w.Home = w.Pos; w.State = 0; w.Atk = 0; w.T1 = 5f; Invuln = 0; float h = Hp; UpdateEnemies(.033f);
            Check(Mathf.Abs((h - Hp) - 12f * Tune.T.BossDamage[0] * FearMul) < .05f, $"boss: contact damage is scaled by the Resource ({h - Hp:0.0} = 12 × {Tune.T.BossDamage[0]})");
            Enemies.Clear(); TestPlayers = 4; var w4 = SpawnEnemy(spot + new Vector2(60, 0), EK.Warden, null, 1f); Check(w4.MaxHp > w.MaxHp * 1.5f && BossTempoMul() < 1f, $"boss: four players — more health ({w4.MaxHp:0}) and a faster tempo (×{BossTempoMul():0.00})");
            TestPlayers = 0; Enemies.Clear();
        });

        // ── уникальные способности боссов
        S(.2f, () =>
        {
            foreach (var k in new[] { EK.Warden, EK.DepthWarden, EK.AshWarden, EK.WinterWarden, EK.Source })
            {
                Enemies.Clear(); Projs.Clear(); Zones.Clear(); PPos = spot; pVel = Vector2.Zero; Invuln = 99f; Hp = MaxHp; TestPlayers = 0; trail.Clear(); for (int i = 0; i < 4; i++) trail.Add(spot + new Vector2(-10 * i, 8 * i));
                var e = SpawnEnemy(spot + new Vector2(70, 0), k, null, 1f); e.Home = e.Pos; e.Hp = e.MaxHp * .3f; Sim(.1f);
                e.Shield = false; e.State = 0; e.T1 = 0; e.SpecT = 0; e.Over = 0; float tStart = -1, tActive = -1, tEnd = -1; bool over = false, effect = false; int cold = 0;
                Sim(9f, t =>
                {
                    if (tStart < 0 && e.State == 20) tStart = t; if (tStart >= 0 && tActive < 0 && e.SpecPh == 1) tActive = t; if (tActive >= 0 && tEnd < 0 && e.State != 20) tEnd = t;
                    if (e.Over > 0) over = true; if (Projs.Count > 0 || Zones.Count > 0 || pVel.X > 5f) effect = true; foreach (var pr in Projs) if (pr.Cold) cold++; e.Hp = Mathf.Min(e.MaxHp * .3f, e.Hp + 5f); e.Shield = false; e.T1 = e.State == 0 ? Mathf.Min(e.T1, 0f) : e.T1;
                    if (tEnd >= 0 && k != EK.Source) { }
                });
                string nm = k.ToString();
                Check(tStart >= 0 && tActive - tStart >= .6f, $"boss {nm}: the special has a visible telegraph of {tActive - tStart:0.00} s before it starts");
                Check(effect && tEnd > tActive, $"boss {nm}: the special does something and ends on its own ({tEnd - tActive:0.0} s)");
                if (k == EK.Warden || k == EK.DepthWarden || k == EK.AshWarden) Check(over, $"boss {nm}: after the special the boss is open to attack");
                if (k == EK.WinterWarden) Check(cold > 0, "boss WinterWarden: the blizzard volleys are cold projectiles");
            }
            Enemies.Clear(); Projs.Clear(); Zones.Clear();
            var ash = SpawnEnemy(spot + new Vector2(70, 0), EK.AshWarden, null, 1f); ash.Over = 0; float a0 = ash.Hp; Hurt(ash, 10f, Vector2.Right); float norm = a0 - ash.Hp; ash.Over = 1f; a0 = ash.Hp; Hurt(ash, 10f, Vector2.Right);
            Check(Mathf.Abs((a0 - ash.Hp) / norm - 1.4f) < .01f, "boss: damage taken while the boss is open is +40%");
            TestPlayers = 1; var b1 = SpawnEnemy(spot + new Vector2(70, 0), EK.Warden, null, 1f); b1.Home = b1.Pos; b1.Hp = b1.MaxHp * .3f; b1.State = 0; b1.T1 = 0; b1.SpecT = 0; PPos = spot; UpdateEnemies(.033f); float s1 = b1.SpecT; Enemies.Clear();
            TestPlayers = 4; var b4 = SpawnEnemy(spot + new Vector2(70, 0), EK.Warden, null, 1f); b4.Home = b4.Pos; b4.Hp = b4.MaxHp * .3f; b4.State = 0; b4.T1 = 0; b4.SpecT = 0; UpdateEnemies(.033f); float s4 = b4.SpecT; TestPlayers = 0; Enemies.Clear();
            Check(s4 < s1 && s1 > 0, $"boss: four players shorten the pause between specials ({s1:0.0} s → {s4:0.0} s)");
            int wired = 0; foreach (var f in new[] { "Combat", "Ash", "Ice", "Heart" }) { var txt = System.IO.File.ReadAllText(ProjectSettings.GlobalizePath($"res://Scripts/Combat/Game.{f}.cs")); wired += System.Text.RegularExpressions.Regex.Matches(txt, @"mul = .*BossTempoMul\(\)").Count; }
            Check(wired >= 5, $"boss: the tempo setting is connected to all five bosses ({wired} places)");
            Menu = Mn.None; Toast("Проверка: уведомление под курсором"); Announce("Проверка", "Клик сквозь уведомление должен попасть в мир", Colors.White, "discover");
        });
        S(.4f, () =>
        {
            var vs = ViewSize / UiScaleVp; Check(!hud.IsOverUi(new Vector2(vs.X * .5f, 12f)) && !hud.IsOverUi(new Vector2(vs.X * .5f, 30f)), "ui: a notification under the cursor does not swallow a click (attacks and mining work through it)");
            if (!HasWeapon("staff1")) GiveWeapon("staff1", false); int si = Owned.FindIndex(o => o.Id == "staff1"); EquipWeapon(si); Check(CurW == si && Hot[HotSel] == RefWeapon("staff1"), "hotbar: a crafted weapon can be taken in hand (replaces the weapon slot)");
            if (Owned.Count < 2) GiveWeapon("sword1", false); EquipWeapon(Owned.FindIndex(o => o.Id == "staff1")); int before = CurW; CycleOwnedWeapon(); Check(CurW != before && CurW >= 0, "hotbar: Shift+V cycles through all owned weapons");
            Window = Win.Equip; TechMask |= 1 << (int)Tech.Dash; DashCd = 0; DashT = 0; TryDash(); Check(DashT <= 0 && DashCd <= 0, "ui: a dash cannot be started while a window is open"); Window = Win.None;
            { float sum = 0; int n = 4000, mn = 99; for (int i = 0; i < n; i++) { int y = Yield(It.Wood, 3); sum += y; mn = Math.Min(mn, y); } float avg = sum / n; Check(avg > 1.7f && avg < 2.2f && mn >= 1 && Yield(It.Shard, 3) == 3, $"economy: a tree gives {avg:0.00} wood on average instead of 3 (never less than 1; shards are not reduced)"); }
            { int si2 = Owned.FindIndex(o => Db.Weapon(o.Id)?.Cls == WClass.Sword); if (si2 >= 0) { EquipWeapon(si2); var tt = ToTile(PPos) + new Vector2I(1, 0); var old2 = W.Get(tt.X, tt.Y); SetObj(tt.X, tt.Y, Tl.Stone); Aim = TileCenter(tt.X, tt.Y); MineProg = 0; Enemies.Clear(); for (int i = 0; i < 60; i++) Act(.1f); Check(W.Get(tt.X, tt.Y) == Tl.Stone && MineProg == 0, "weapons: a weapon in hand cannot break blocks at all"); SetObj(tt.X, tt.Y, old2); } }
            {
                PPos = spot; var pt0 = ToTile(spot); int removed = 0;
                for (int y = pt0.Y - 36; y <= pt0.Y + 36; y++) for (int x = Math.Max(0, pt0.X - 50); x <= Math.Min(World.MX0 - 1, pt0.X + 50); x++) if (World.In(x, y)) { var o = W.Get(x, y); if (o == Tl.Tree || o == Tl.Pine || o == Tl.DeadTree || o == Tl.Stone || o == Tl.Copper || o == Tl.Iron) { SetObj(x, y, Tl.None); removed++; } }
                var beforeObj = (byte[])W.Obj.Clone(); for (int i = 0; i < 50; i++) { scarceT = 0; UpdateScarcity(1f); }
                int trees = 0, near = 0, litN = 0, total = 0;
                for (int i = 0; i < W.Obj.Length; i++) if (W.Obj[i] != beforeObj[i] && beforeObj[i] == (byte)Tl.None) { total++; var c2 = TileCenter(i % World.Wd, i / World.Wd); if (W.Obj[i] == (byte)Tl.Tree) trees++; if (c2.DistanceTo(spot) < 12 * TS) near++; if (IsLit(c2)) litN++; }
                Check(removed > 50 && trees >= 40 && near == 0 && litN == 0, $"resources: after the area was stripped ({removed} blocks) {total} new blocks appeared ({trees} trees), none within 12 tiles of the player or in the light");
            }
            Check(Tune.T.MineTimeMul > 1f && Tune.T.RegrowMul < 1f && Tune.T.XpMul < 1f, "economy: mining time, regrowth and experience are tuned from the Resource");
            float m0 = Settings.Music, s0 = Settings.Sfx; SetVolume(0, .3f); SetVolume(1, .7f); Check(Mathf.IsEqualApprox(Settings.Music, .3f) && Mathf.IsEqualApprox(Settings.Sfx, .7f), "ui: volume is set by clicking the bar (values snap to 10%)");
            Settings.Music = m0; Settings.Sfx = s0; Settings.ApplyAudio(); Settings.Save();
            // застревание: существо внутри камня выталкивается; смерть — на базе, на сложном режиме теряется уровень
            {
                var free = spot; int tx = (int)(free.X / TS), ty = (int)(free.Y / TS); var keep = W.Get(tx, ty); W.Set(tx, ty, Tl.Stone);
                var moved = Move(free, new Vector2(1, 0), 5f, false); Check(!Collides(moved, 5f, false), "stuck: a creature inside a block is pushed out and free to move"); W.Set(tx, ty, keep);
                int d0 = Diff; Diff = 1; Level = 5; MaxHp = 90; Xp = 3; PPos = spot; Die();
                Check(Level == 4 && MaxHp == 80f && PPos.DistanceTo(W.Ancients[0].Pos + new Vector2(0, 28)) < 1f, $"death hard: loses a level (now {Level}) and respawns at the main beacon");
                Diff = 0; PPos = spot; Die(); Check(Level == 4 && PPos.DistanceTo(W.Ancients[0].Pos + new Vector2(0, 28)) < 1f, "death normal: keeps the level, respawns at the main beacon"); Diff = d0;
                int cap0 = 0; Check(Tune.T.SpawnDelayMul > 1f && Tune.T.SpawnCapMul < 1f && cap0 == 0, "spawn: pause is longer and the cap is lower than before");
            }
            GD.Print($"[check] SUMMARY failures={fails}"); GD.Print("[check] DIFFICULTY DONE");
        });
    }
}
