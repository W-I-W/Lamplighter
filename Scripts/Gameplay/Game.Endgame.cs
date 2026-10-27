using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

public sealed class LegendDef { public string Name, Desc, Eff; public int[][] Cost; }

/// <summary>Эндгейм после финала: Разломы Тьмы (волновые испытания по уровням), легендарные улучшения, украшение поселения и облики.</summary>
public partial class Game
{
    // ───────────────────────── данные ─────────────────────────
    public static readonly LegendDef[] Legends =
    {
        new() { Name = "Легендарное лезвие", Desc = "Урон оружия +25%", Eff = "Dmg:0.25", Cost = C(It.RiftShard, 12, It.AncCrystal, 2, It.Silver, 8) },
        new() { Name = "Кирка Источника", Desc = "Скорость добычи +35%", Eff = "MineSpd:0.35", Cost = C(It.RiftShard, 10, It.AncCrystal, 2, It.Iron, 12) },
        new() { Name = "Печать мастера", Desc = "Производство деревни +25%", Eff = "Prod:0.25", Cost = C(It.RiftShard, 14, It.Gear, 6, It.Lens, 4) },
        new() { Name = "Лампа последнего фонарщика", Desc = "Свет героя +12%, лампы +10%", Eff = "HeroRadius:0.12|LampRadius:0.1", Cost = C(It.RiftShard, 12, It.Lens, 6, It.Crystal, 6) },
        new() { Name = "Плащ рассвета", Desc = "Скорость +8%, убийство лечит", Eff = "Speed:0.08|KillHeal:1.5", Cost = C(It.RiftShard, 16, It.Frost, 8, It.Ember, 8) },
    };
    public static readonly string[] CosmNames = { "Без облика", "Золотой ореол", "Лазурный ореол", "Пурпурный ореол" };
    static readonly Color[] CosmCol = { Colors.Transparent, new("#ffd86b"), new("#7ad8ff"), new("#c070ff") };
    public static readonly string[] AffixNames = { "Лёд", "Пепел", "Глубина", "Иллюзии", "Тени" };
    static readonly EK[][] Affixes =
    {
        new[] { EK.IceWolf, EK.FrostPriest, EK.Sentinel }, new[] { EK.Imp, EK.Golem, EK.Spirit }, new[] { EK.Beetle, EK.Leech, EK.Spitter },
        new[] { EK.Mirage, EK.Weaver, EK.Stalker }, new[] { EK.Shade, EK.Wraith, EK.Bomber },
    };
    public const int MaxRiftTier = 8;

    // сохраняемое
    public int RiftUnlocked = 1, RiftFirst, LegendMask, CosmMask = 1, CosmSel, RiftClears;
    // состояние забега
    public bool RiftOn; public int RiftTier, RiftWave, RiftWaves, RiftState; public int[] RiftAffix = new int[2];
    public List<string> RiftOffer = new();
    float riftWaveT; bool riftSpawned; Vector2 riftReturn; GEvent riftEv;
    public int DecorCount, DecorRegions;

    public bool HasLegend(int i) => (LegendMask & (1 << i)) != 0;

    void ApplyEndgameEffects()
    {
        for (int i = 0; i < Legends.Length; i++) if (HasLegend(i)) foreach (var e in Legends[i].Eff.Split('|')) AddEffect(e);
        if (DecorCount > 0) Num["Prod"] = N("Prod") + Math.Min(.15f, .015f * DecorCount);
    }

    // ───────────────────────── врата разломов ─────────────────────────
    public Vector2I RiftGateTile()
    {
        var bc = W.Ancients[0].Tile;
        for (int r = 4; r <= 14; r++)
            for (int a = 0; a < 24; a++)
            {
                var t = bc + new Vector2I((int)Math.Round(Math.Cos(a * Math.PI / 12) * r), (int)Math.Round(Math.Sin(a * Math.PI / 12) * r));
                if (W.Get(t.X, t.Y) == Tl.RiftGate) return t;
            }
        for (int r = 5; r <= 14; r++)
            for (int a = 0; a < 24; a++)
            {
                var t = bc + new Vector2I((int)Math.Round(Math.Cos(a * Math.PI / 12 + .3) * r), (int)Math.Round(Math.Sin(a * Math.PI / 12 + .3) * r));
                bool ok = W.Get(t.X, t.Y) == Tl.None && W.Ground[World.Key(t.X, t.Y)] != Art.WaterG;
                foreach (var d in World.D4) ok &= W.Get(t.X + d.X, t.Y + d.Y) == Tl.None;
                if (ok) return t;
            }
        return new Vector2I(-1, -1);
    }

    void EnsureRiftGate()
    {
        if (!Finished) return;
        var t = RiftGateTile(); if (t.X < 0) return;
        if (W.Get(t.X, t.Y) != Tl.RiftGate) { SetObj(t.X, t.Y, Tl.RiftGate); Burst(TileCenter(t.X, t.Y), new Color("#c070ff"), 24, 90); }
    }

    public void RecountDecor()
    {
        DecorCount = 0; DecorRegions = 0;
        for (int y = 0; y < World.Ht; y++)
            for (int x = 0; x < World.Wd; x++)
            {
                var t = (Tl)W.Obj[y * World.Wd + x];
                if (t == Tl.Statue || t == Tl.Fountain || t == Tl.Banner) { DecorCount++; DecorRegions |= 1 << World.RegionOf(x); }
            }
        RecomputeEffects();
    }

    static readonly int[][] StatueCost = C(It.Stone, 20, It.Silver, 4, It.Coins, 60);
    static readonly int[][] FountainCost = C(It.Stone, 16, It.Crystal, 3, It.Coins, 80);
    static readonly int[][] BannerCost = C(It.Wood, 8, It.Coins, 25);
    public static int[][] DecorCostArr(int k) => k == 0 ? StatueCost : k == 1 ? FountainCost : BannerCost;
    public static readonly string[] DecorNames = { "Статуя Фонарщика", "Фонтан света", "Знамя маяка" };
    static readonly Tl[] DecorTiles = { Tl.Statue, Tl.Fountain, Tl.Banner };

    public void BuildDecor(int k)
    {
        if (BeaconLevel < 2) { Fail("Украшения открываются после первого улучшения Центрального маяка"); return; }
        var cost = DecorCostArr(k);
        if (!Can(cost)) { Fail(DecorNames[k] + ": нужно " + NeedStr(cost)); return; }
        var t = PlaceTarget(); if (t == null) return;
        Pay(cost); SetObj(t.Value.X, t.Value.Y, DecorTiles[k]); GiveXp(15);
        Burst(TileCenter(t.Value.X, t.Value.Y), new Color("#ffe27a"), 18, 80); Sfx.Play("build", -2, 1.2f);
        RecountDecor();
        Toast($"{DecorNames[k]} украшает землю. Украшений: {DecorCount} (производство +{(int)(Math.Min(.15f, .015f * DecorCount) * 100)}%)");
        Save();
    }

    // ───────────────────────── разлом ─────────────────────────
    public bool RiftTierOpen(int tier) => tier <= RiftUnlocked;

    public void StartRift(int tier)
    {
        if (RiftOn) return;
        if (!Finished) { Fail("Разломы Тьмы открываются после активации Центрального маяка"); return; }
        if (!RiftTierOpen(tier)) { Fail("Сначала пройдите разлом предыдущего уровня"); return; }
        if (Boss != null) { Fail("Сначала разберитесь с хранителем рядом"); return; }
        var r = new Random(System.Environment.TickCount);
        RiftAffix[0] = r.Next(Affixes.Length); do RiftAffix[1] = r.Next(Affixes.Length); while (RiftAffix[1] == RiftAffix[0]);
        RiftOn = true; RiftTier = tier; RiftWave = 0; RiftWaves = 3 + (tier >= 4 ? 1 : 0) + (tier >= 7 ? 1 : 0); RiftState = 0; riftSpawned = false; riftWaveT = 3.5f;
        riftReturn = PPos; riftEv = new GEvent { Kind = "riftx" };
        ClearTemp(); Enemies.Clear(); Projs.Clear(); Zones.Clear(); Boss = null; CloseWindows();
        var c = W.HeartArena; PPos = TileCenter(c.X, c.Y + 3); cam.ResetSmoothing(); Flash = .5f; Hp = MaxHp; Invuln = 2f;
        Announce($"Разлом Тьмы · уровень {tier}", $"Враги: {AffixNames[RiftAffix[0]]} и {AffixNames[RiftAffix[1]]}. Волн: {RiftWaves}. Прогресс мира не затронут", new Color("#c070ff"), "boss");
    }

    void RiftSpawnWave()
    {
        RiftWave++; riftSpawned = true;
        int n = 3 + RiftTier + RiftWave; var c = W.HeartArena; float scale = 1f + .22f * RiftTier;
        for (int i = 0; i < n; i++)
        {
            Vector2 pos = TileCenter(c.X, c.Y);
            for (int tries = 0; tries < 20; tries++)
            {
                var p = TileCenter(c.X, c.Y) + Vector2.FromAngle(rng.Randf() * Mathf.Tau) * rng.RandfRange(6.5f, 9.5f) * TS; var t = ToTile(p);
                if (W.Walkable(t.X, t.Y)) { pos = p; break; }
            }
            var pool = Affixes[RiftAffix[rng.Randf() < .5f ? 0 : 1]];
            var e = SpawnEnemy(pos, pool[rng.RandiRange(0, pool.Length - 1)], null, scale); e.Ev = riftEv; e.Age = -.8f;
            Burst(pos, new Color("#c070ff"), 8, 60);
            if (RiftWave == RiftWaves && i == 0) { e.Elite = true; e.Hp = e.MaxHp = e.MaxHp * 2.5f; }
        }
        Sfx.Play("boss", -5, 1.3f); Toast($"Разлом: волна {RiftWave}/{RiftWaves}");
    }

    void UpdateEndgame(float dt)
    {
        if (Finished && !riftGateChecked) { riftGateChecked = true; EnsureRiftGate(); RecountDecor(); }
        // фонтан света лечит рядом
        if (DecorCount > 0 && !RiftOn && Hp < MaxHp)
        {
            var pt = ToTile(PPos);
            for (int y = pt.Y - 4; y <= pt.Y + 4; y++) for (int x = pt.X - 4; x <= pt.X + 4; x++) if (W.Get(x, y) == Tl.Fountain) { Hp = Mathf.Min(MaxHp, Hp + 2.5f * dt); y = pt.Y + 5; break; }
        }
        if (!RiftOn) return;
        if (RiftState == 1) return;
        riftWaveT -= dt;
        if (!riftSpawned && riftWaveT <= 0 && RiftWave < RiftWaves) RiftSpawnWave();
        if (riftSpawned && !Enemies.Exists(e => e.Ev == riftEv))
        {
            riftSpawned = false;
            if (RiftWave >= RiftWaves) RiftWin();
            else OfferTemp();
        }
    }
    bool riftGateChecked;

    void OfferTemp()
    {
        RiftOffer.Clear(); var pool = new List<string>(TempIds); pool.RemoveAll(id => TempRelics.Contains(id));
        while (RiftOffer.Count < 3 && pool.Count > 0) { int i = rng.RandiRange(0, pool.Count - 1); RiftOffer.Add(pool[i]); pool.RemoveAt(i); }
        if (RiftOffer.Count == 0) { riftWaveT = 3f; return; }
        RiftState = 1; CloseWindows(); Window = Win.Rift; WinCur = 0; Sfx.Play("tech");
    }

    public void PickTemp(int i)
    {
        if (RiftState != 1 || i < 0 || i >= RiftOffer.Count) return;
        GrantTemp(RiftOffer[i]); Toast($"Временная реликвия: «{Db.Relic(RiftOffer[i]).Title}»");
        RiftState = 0; Window = Win.None; riftWaveT = 3f; Hp = Mathf.Min(MaxHp, Hp + MaxHp * .25f);
    }

    void RiftWin()
    {
        int tier = RiftTier; var r = new Random(System.Environment.TickCount);
        RiftClears++; bool first = (RiftFirst & (1 << tier)) == 0; RiftFirst |= 1 << tier;
        if (tier >= RiftUnlocked && tier < MaxRiftTier) RiftUnlocked = tier + 1;
        int shards = 3 + 2 * tier; Give(It.RiftShard, shards, PPos + new Vector2(0, -10));
        Inv[It.Coins] += 80 * tier; GiveXp(100 * tier); if (tier >= 3) Give(It.AncCrystal, 1, PPos + new Vector2(8, -8));
        GrantTag(RollTag(r, 3, .3f + .05f * tier), PPos);
        string extra = "";
        if (first)
        {
            int cos = tier == 1 ? 1 : tier == 3 ? 2 : tier == 5 ? 3 : 0;
            if (cos > 0 && (CosmMask & (1 << cos)) == 0) { CosmMask |= 1 << cos; extra = Loc.T($" Облик «{Loc.T(CosmNames[cos])}» открыт [E у врат]!"); }
            if (tier == MaxRiftTier) extra += Loc.T(" Вы прошли самый глубокий разлом!");
        }
        Announce(Loc.T($"Разлом пройден! Уровень {tier}"), Loc.T($"Осколков разлома: +{shards}, монеты, редкая находка.") + (tier < MaxRiftTier && tier + 1 == RiftUnlocked ? Loc.T($" Открыт уровень {tier + 1}.") : "") + extra, new Color("#ffe27a"), "ancient");
        RiftEnd(true);
    }

    void RiftEnd(bool win)
    {
        RiftOn = false; RiftState = 0; ClearTemp(); Enemies.RemoveAll(e => e.Ev == riftEv); Projs.Clear(); Zones.Clear();
        if (Window == Win.Rift) Window = Win.None;
        PPos = riftReturn; cam.ResetSmoothing(); Flash = .4f; Invuln = 2f; if (win) Hp = MaxHp;
        if (!win) Toast("Вы покинули разлом");
        Save();
    }

    void RiftOnDeath() { if (!RiftOn) return; RiftOn = false; RiftState = 0; ClearTemp(); if (Window == Win.Rift) Window = Win.None; Toast("Разлом поглотил вас — ничего не потеряно, кроме времени"); }

    void RiftIx(Ix ix)
    {
        int left = Enemies.FindAll(e => e.Ev == riftEv).Count;
        ix.Lines.Add($"Разлом уровня {RiftTier}: волна {RiftWave}/{RiftWaves} · врагов: {left}");
        ix.Lines.Add("[E] Покинуть разлом (без награды)"); ix.E = () => RiftEnd(false);
    }

    void RiftGateIx(Ix ix)
    {
        ix.Lines.Add($"Врата разломов · пройдено: {RiftClears} · доступно уровней: {RiftUnlocked}/{MaxRiftTier}");
        ix.Lines.Add("[E] Открыть: разломы, легендарные улучшения, облики"); ix.E = () => { if (!Finished) Fail("Врата спят: активируйте Центральный маяк"); else { CloseWindows(); Window = Win.Rift; WinTab = 0; WinCur = 0; Sfx.Play("tick"); } };
    }

    // ───────────────────────── окно врат ─────────────────────────
    public static readonly string[] RiftTabs = { "Разломы", "Легендарное", "Облики" };
    public int RiftWinLen => RiftState == 1 ? RiftOffer.Count : WinTab == 0 ? MaxRiftTier : WinTab == 1 ? Legends.Length : CosmNames.Length;

    bool RiftKey(Key k)
    {
        if (RiftState == 1)
        {
            if (k >= Key.Key1 && k <= Key.Key3) PickTemp((int)(k - Key.Key1));
            else if (k == Key.Enter || k == Key.KpEnter) PickTemp(WinCur);
            return true;
        }
        if (k == Key.Left) { WinTab = (WinTab + 2) % 3; WinCur = 0; return true; }
        if (k == Key.Right) { WinTab = (WinTab + 1) % 3; WinCur = 0; return true; }
        if (k != Key.Enter && k != Key.KpEnter) return true;
        if (WinTab == 0) StartRift(WinCur + 1);
        else if (WinTab == 1) BuyLegend(WinCur);
        else if (WinCur < CosmNames.Length)
        {
            if ((CosmMask & (1 << WinCur)) == 0) Fail("Облик открывается за первое прохождение разлома: 1, 3 и 5 уровней");
            else { CosmSel = WinCur; Sfx.Play("upgrade", -4, 1.4f); Toast("Облик: " + CosmNames[WinCur]); }
        }
        return true;
    }

    void BuyLegend(int i)
    {
        if (i < 0 || i >= Legends.Length) return;
        if (HasLegend(i)) { Toast("Уже получено"); return; }
        if (!Can(Legends[i].Cost)) { Fail($"{Legends[i].Name}: нужно {NeedStr(Legends[i].Cost)}"); return; }
        Pay(Legends[i].Cost); LegendMask |= 1 << i; RecomputeEffects(); Flash = .3f; Sfx.Play("upgrade");
        Announce("Легендарное улучшение", Legends[i].Name + ": " + Legends[i].Desc, new Color("#ffb04a"), "ancient"); Found.Add("Легенда: " + Legends[i].Name); Save();
    }

}
