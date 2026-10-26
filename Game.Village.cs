using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

public partial class Game
{
    public List<Building> Buildings = new();
    public List<Villager> Villagers = new();
    public int[] Stock = new int[It.Count];
    public int PoisDone { get { int n = 0; foreach (var p in W.Pois) if (p.State == 3) n++; return n; } }

    public static readonly string[] BNames = { "Склад", "Лесопилка", "Шахта", "Кузница", "Автобур", "Коллектор пепла" };
    static readonly Tl[] BTypes = { Tl.Warehouse, Tl.Sawmill, Tl.Mine, Tl.Forge, Tl.Drill, Tl.Collector };
    static readonly int[][][][] BCosts =
    {
        new[] { C(It.Wood, 15, It.Stone, 10), C(It.Wood, 20, It.Stone, 20, It.Copper, 4), C(It.Iron, 6, It.Relic, 1), C(It.Silver, 6, It.Crystal, 3) },
        new[] { C(It.Wood, 12, It.Stone, 6, It.Copper, 2), C(It.Wood, 15, It.Copper, 6), C(It.Iron, 6, It.Aether, 2), C(It.Silver, 6, It.Crystal, 3) },
        new[] { C(It.Stone, 10, It.Copper, 4, It.Wood, 6), C(It.Iron, 5, It.Wood, 10), C(It.Iron, 8, It.Aether, 3), C(It.Silver, 8, It.Crystal, 4) },
        new[] { C(It.Stone, 15, It.Copper, 6, It.Iron, 3), C(It.Iron, 6, It.Relic, 1), C(It.Iron, 10, It.Aether, 3), C(It.Silver, 8, It.Crystal, 4) },
        new[] { C(It.Crystal, 5, It.Silver, 5, It.Iron, 4), C(It.Silver, 4, It.Iron, 4, It.Wood, 6), C(It.Silver, 6, It.Crystal, 3), C(It.Crystal, 5, It.Silver, 6, It.Relic, 1) },
        new[] { C(It.Obsidian, 4, It.Silver, 4, It.Crystal, 3), C(It.Ember, 3, It.Obsidian, 3), C(It.Ember, 4, It.Silver, 4), C(It.Obsidian, 5, It.Ember, 5, It.Crystal, 3) },
    };
    static readonly string[][] BDesc =
    {
        new[] { "хранит 60 каждого ресурса", "хранит 120", "хранит 250 и передаёт ресурсы вам рядом", "хранит 500, передаёт ресурсы дальше" },
        new[] { "1 дерево / 8 с", "1 дерево / 5 с", "2 дерева / 3 с", "3 дерева / 2,5 с" },
        new[] { "камень и медная руда / 9 с", "+ железная руда / 7 с", "+ осколки света / 5 с", "+ серебряная руда / 4 с" },
        new[] { "плавит руду со склада / 4 с", "плавка / 2,5 с", "плавка / 1,5 с", "плавка / 1 с" },
        new[] { "серебряная руда и кристаллы / 12 с", "быстрее / 9 с", "быстрее / 6 с", "очень быстро / 4,5 с" },
        new[] { "обсидиан и уголь пепла / 12 с", "быстрее / 9 с", "быстрее / 6 с", "очень быстро / 4,5 с" },
    };
    static readonly float[][] BInterval = { null, new[] { 8f, 5f, 3.2f, 2.5f }, new[] { 9f, 7f, 5f, 4f }, new[] { 4f, 2.5f, 1.5f, 1f }, new[] { 12f, 9f, 6f, 4.5f }, new[] { 12f, 9f, 6f, 4.5f } };
    static readonly int[] WhCap = { 20, 60, 120, 250, 500 };

    public static int[][] BCost(int bk, int toLevel) => BCosts[bk][toLevel - 1];
    public bool BUnlocked(int bk) => bk switch
    {
        0 => HasFurnace, 1 => HasBuilding(Tl.Warehouse), 2 => LitCount >= 1, 3 => Villagers.Count >= 1, 4 => Has(Tech.AutoDrill), _ => Has(Tech.Collector)
    };
    public static string BLock(int bk) => bk switch
    {
        0 => "Постройте печь", 1 => "Постройте склад", 2 => "Зажгите древний фонарь", 3 => "Спасите жителя", 4 => "Изучите «Автобур» [T]", _ => "Изучите «Коллектор» [T]"
    };

    public bool HasBuilding(Tl t) { foreach (var b in Buildings) if (b.Type == t) return true; return false; }
    public int LevelAt(int x, int y) { foreach (var b in Buildings) if (b.X == x && b.Y == y) return b.Level; return 1; }
    public Building BuildingNear(Vector2 p, float r)
    {
        Building best = null; float bd = r;
        foreach (var b in Buildings) { float d = b.Pos.DistanceTo(p); if (d < bd) { bd = d; best = b; } }
        return best;
    }
    public int StockCap { get { int lv = 0; foreach (var b in Buildings) if (b.Type == Tl.Warehouse) lv = Math.Max(lv, b.Level); return WhCap[lv]; } }
    public int StockTotal { get { int n = 0; foreach (var x in Stock) n += x; return n; } }
    static int BIndex(Tl t) => Array.IndexOf(BTypes, t);

    void InitVillage()
    {
        int rescued = 0; foreach (var p in W.Pois) if (p.Kind == PK.Captive && p.State == 3) rescued++;
        var wh = Buildings.Find(b => b.Type == Tl.Warehouse);
        var home = wh != null ? wh.Pos + new Vector2(0, 16) : TileCenter(W.Spawn.X + 2, W.Spawn.Y);
        for (int i = 0; i < rescued; i++) Villagers.Add(new Villager { Pos = home + new Vector2(i * 10, 0), Wander = home });
    }

    // ───────────────────────── строительство ─────────────────────────
    void BuildBuilding(int bk)
    {
        if (!BUnlocked(bk)) { Fail($"{BNames[bk]} пока недоступна: {BLock(bk)}"); return; }
        if (HasBuilding(BTypes[bk])) { Fail($"{BNames[bk]} уже построена — улучшайте её [U]"); return; }
        var cost = BCost(bk, 1);
        if (!Can(cost)) { Fail($"{BNames[bk]}: нужно {CostStr(cost)}"); return; }
        var t = PlaceTarget(true); if (t == null) return;
        int reg = World.RegionOf(t.Value.X);
        if (bk == 4 && reg != 1) { Fail("Автобур можно ставить только в Затопленных шахтах (на освещённой земле)"); return; }
        if (bk == 5 && reg != 2) { Fail("Коллектор можно ставить только в Пепельных пустошах (на освещённой земле)"); return; }
        Pay(cost);
        var b = new Building { Type = BTypes[bk], X = t.Value.X, Y = t.Value.Y };
        Buildings.Add(b); SetObj(b.X, b.Y, b.Type);
        Burst(b.Pos, new Color("#d9b27c"), 16, 70); Sfx.Play("build"); GiveXp(30);
        Announce($"Построено: {BNames[bk]}", BDesc[bk][0], new Color("#d9b27c"), "build");
        Save();
    }

    void UpgradeBuilding(Building b)
    {
        int bk = BIndex(b.Type);
        if (b.Level >= 4) { Fail("Здание уже улучшено до максимума"); return; }
        var cost = BCost(bk, b.Level + 1);
        if (!Can(cost)) { Fail($"Улучшение: нужно {CostStr(cost)}"); return; }
        Pay(cost); b.Level++; SetObj(b.X, b.Y, b.Type);
        Burst(b.Pos, new Color("#ffe27a"), 22, 90); Sfx.Play("upgrade"); GiveXp(40); Shake = 2;
        Announce($"{BNames[bk]}: уровень {b.Level}", "Новое: " + BDesc[bk][b.Level - 1], new Color("#ffe27a"), "upgrade");
        Save();
    }

    void BuildingIx(Building b, Ix ix)
    {
        int bk = BIndex(b.Type);
        ix.Lines.Add($"{BNames[bk]} ур.{b.Level}: {BDesc[bk][b.Level - 1]}" + (b.Type == Tl.Warehouse ? "" : b.Worker ? " · работник на месте" : " · без работника (40% скорости)"));
        int tot = StockTotal;
        ix.Lines.Add(tot > 0 ? $"[E] Забрать со склада ({tot} шт.)" : "Склад пуст — производство идёт само");
        if (tot > 0) ix.E = CollectStock;
        if (b.Level < 4) { ix.Lines.Add($"[U] Улучшить до ур.{b.Level + 1}: {CostStr(BCost(bk, b.Level + 1))} → {BDesc[bk][b.Level]}"); ix.U = () => UpgradeBuilding(b); }
        else ix.Lines.Add("Максимальный уровень");
    }

    void CollectStock()
    {
        int tot = 0;
        for (int i = 0; i < It.Count; i++)
            if (Stock[i] > 0) { Inv[i] += Stock[i]; tot += Stock[i]; Texts.Add(new FText { P = PPos + new Vector2(rng.RandfRange(-8, 8), -12 - i * 3), S = $"+{Stock[i]} {It.Names[i]}", Life = 1.2f, C = It.Col[i] }); Stock[i] = 0; }
        if (tot > 0) Sfx.Play("pickup");
    }

    // ───────────────────────── производство ─────────────────────────
    float whAuto, ambT;

    void UpdateVillage(float dt)
    {
        int vi = 0;
        foreach (var b in Buildings) { if (b.Type == Tl.Warehouse) { b.Worker = false; continue; } b.Worker = vi < Villagers.Count; if (b.Worker) vi++; }
        int cap = StockCap; ambT -= dt;
        foreach (var b in Buildings)
        {
            int bk = BIndex(b.Type);
            if (b.Type == Tl.Warehouse) continue;
            float rate = (b.Worker ? 1f : .4f) * ProdMul * (b.Type == Tl.Forge && Has(Tech.ForgeHeat) ? 2f : 1f);
            float interval = BInterval[bk][b.Level - 1];
            bool working = false;
            switch (b.Type)
            {
                case Tl.Sawmill:
                    if (Stock[It.Wood] >= cap) { b.Status = 2; break; }
                    b.Status = 0; working = true; b.Prog += dt * rate;
                    if (b.Prog >= interval) { b.Prog = 0; Produce(It.Wood, b.Level == 3 ? 2 : b.Level >= 4 ? 3 : 1, b); }
                    break;
                case Tl.Mine:
                    b.Status = 0; working = true; b.Prog += dt * rate;
                    if (b.Prog >= interval)
                    {
                        b.Prog = 0;
                        int cyc = b.Level == 1 ? 2 : b.Level == 2 ? 3 : b.Level == 3 ? 4 : 5; int step = b.Count++ % cyc;
                        int item = step == 0 ? It.Stone : step == 1 ? It.CopperOre : step == 2 ? It.IronOre : step == 3 ? It.Shard : It.SilverOre;
                        if (Stock[item] >= cap) { b.Status = 2; b.Count--; } else Produce(item, 1, b);
                    }
                    break;
                case Tl.Drill:
                    b.Status = 0; working = true; b.Prog += dt * rate;
                    if (b.Prog >= interval)
                    {
                        b.Prog = 0; int step = b.Count++ % 3; int item = step == 2 ? It.Crystal : It.SilverOre;
                        if (Stock[item] >= cap) { b.Status = 2; b.Count--; } else Produce(item, b.Level >= 3 ? 2 : 1, b);
                    }
                    break;
                case Tl.Collector:
                    b.Status = 0; working = true; b.Prog += dt * rate;
                    if (b.Prog >= interval)
                    {
                        b.Prog = 0; int item = (b.Count++ % 2 == 0) ? It.Obsidian : It.Ember;
                        if (Stock[item] >= cap) { b.Status = 2; b.Count--; } else Produce(item, b.Level >= 3 ? 2 : 1, b);
                    }
                    break;
                case Tl.Forge:
                    int ore = Stock[It.CopperOre] > 0 ? It.CopperOre : Stock[It.IronOre] > 0 ? It.IronOre : Stock[It.SilverOre] > 0 ? It.SilverOre : -1;
                    int ingot = ore < 0 ? -1 : It.IngotOf(ore);
                    if (ore < 0 || Stock[It.Wood] < 1) { b.Status = 1; break; }
                    if (Stock[ingot] >= cap) { b.Status = 2; break; }
                    b.Status = 0; working = true; b.Prog += dt * rate;
                    if (b.Prog >= interval) { b.Prog = 0; Stock[ore]--; Stock[It.Wood]--; Produce(ingot, 1, b); GiveXp(.5f); }
                    break;
            }
            if (working && ambT <= 0 && rng.Randf() < .5f)
            {
                var col = b.Type == Tl.Forge || b.Type == Tl.Collector ? new Color("#ff9a2e") : b.Type == Tl.Sawmill ? new Color("#d9b27c") : b.Type == Tl.Drill ? new Color("#bfe9ff") : new Color("#9aa0ae");
                Parts.Add(new Particle { P = b.Pos + new Vector2(b.Type == Tl.Forge ? 4 : rng.RandfRange(-5, 5), b.Type == Tl.Forge ? -8 : 2), V = new Vector2(rng.RandfRange(-8, 8), -rng.RandfRange(10, 24)), Life = .7f, Max = .7f, C = col, Size = 1.5f });
            }
        }
        if (ambT <= 0) ambT = .25f;

        var wh = Buildings.Find(x => x.Type == Tl.Warehouse);
        if (wh != null && wh.Level >= 3 && wh.Pos.DistanceTo(PPos) < (wh.Level >= 4 ? 200 : 96) && StockTotal > 0)
        {
            whAuto -= dt;
            if (whAuto <= 0) { whAuto = .25f; for (int i = 0; i < It.Count; i++) if (Stock[i] > 0) { Stock[i]--; Inv[i]++; Texts.Add(new FText { P = PPos + new Vector2(rng.RandfRange(-8, 8), -12), S = "+1 " + It.Names[i], Life = .8f, C = It.Col[i] }); break; } }
        }

        UpdateVillagers(dt);
    }

    void Produce(int item, int n, Building b)
    {
        int add = Math.Min(n, StockCap - Stock[item]); if (add <= 0) return;
        Stock[item] += add;
        Texts.Add(new FText { P = b.Pos + new Vector2(0, -10), S = "+" + add, Life = .9f, C = It.Col[item] });
        Sfx.Play("tick", -10, 1.2f);
    }

    void UpdateVillagers(float dt)
    {
        var wh = Buildings.Find(x => x.Type == Tl.Warehouse);
        var home = wh != null ? wh.Pos + new Vector2(0, 16) : TileCenter(W.Spawn.X + 2, W.Spawn.Y);
        var jobs = new List<int>();
        for (int bi = 0; bi < Buildings.Count; bi++) if (Buildings[bi].Type != Tl.Warehouse) jobs.Add(bi);
        for (int i = 0; i < Villagers.Count; i++)
        {
            var v = Villagers[i]; v.Job = i < jobs.Count ? jobs[i] : -1;
            Vector2 target;
            if (v.Job >= 0) target = Buildings[v.Job].Pos + new Vector2(0, 13);
            else
            {
                v.WanderT -= dt;
                if (v.WanderT <= 0) { v.Wander = home + new Vector2(rng.RandfRange(-28, 28), rng.RandfRange(-12, 12)); v.WanderT = rng.RandfRange(2, 5); }
                target = v.Wander;
            }
            var d = target - v.Pos;
            if (d.Length() > 3)
            {
                var before = v.Pos; v.Pos = Move(v.Pos, d.Normalized() * 30f * dt, 4f, false);
                v.Walk += dt * 8f; v.Working = false; if (Mathf.Abs(d.X) > 1) v.Face = d.X < 0 ? -1 : 1;
                if (before.DistanceTo(v.Pos) < .1f * dt * 30) { v.Stuck += dt; if (v.Stuck > 2.5f) { v.Pos = target; v.Stuck = 0; } } else v.Stuck = 0;
            }
            else
            {
                v.Working = v.Job >= 0; v.Stuck = 0;
                if (v.Working) { v.Walk += dt * 14f; if (rng.Randf() < dt * 4) Burst(v.Pos + new Vector2(v.Face * 6, -4), new Color("#ffe27a"), 1, 25); }
            }
        }
    }

    // ───────────────────────── отрисовка ─────────────────────────
    void DrawBuildings(EntityLayer c)
    {
        foreach (var b in Buildings)
        {
            if (b.Type == Tl.Warehouse)
            {
                float f = Mathf.Clamp(StockTotal / (float)(StockCap * 3), 0, 1);
                var o = b.Pos + new Vector2(-7, 9);
                c.DrawRect(new Rect2(o, new Vector2(14, 2)), new Color(0, 0, 0, .6f));
                c.DrawRect(new Rect2(o, new Vector2(14 * f, 2)), new Color("#d9b27c"));
                continue;
            }
            int bk = BIndex(b.Type); float p = Mathf.Clamp(b.Prog / BInterval[bk][b.Level - 1], 0, 1);
            var o2 = b.Pos + new Vector2(-7, 9);
            c.DrawRect(new Rect2(o2, new Vector2(14, 2)), new Color(0, 0, 0, .6f));
            c.DrawRect(new Rect2(o2, new Vector2(14 * p, 2)), b.Type == Tl.Forge || b.Type == Tl.Collector ? new Color("#ff9a2e") : b.Type == Tl.Sawmill ? new Color("#7ad45a") : new Color("#8ff0ff"));
            if (b.Status == 1) { c.DrawCircle(b.Pos + new Vector2(0, -11), 2.2f, new Color("#ff5050")); c.DrawRect(new Rect2(b.Pos + new Vector2(-.5f, -13.5f), new Vector2(1, 2.5f)), Colors.White); }
            else if (b.Status == 2) c.DrawCircle(b.Pos + new Vector2(0, -11), 2.2f, new Color("#ffd86b"));
        }
    }

    void DrawVillager(EntityLayer c, Villager v)
    {
        Shadow(c, v.Pos, 4);
        int f = (int)v.Walk % 2; float bob = v.Working ? -Mathf.Abs(Mathf.Sin(v.Walk)) * 1.5f : 0;
        c.DrawSetTransform(v.Pos + new Vector2(0, bob), 0, new Vector2(v.Face, 1));
        c.DrawTextureRectRegion(Art.VillagerTex, new Rect2(-8, -11, 16, 16), new Rect2(f * 16, 0, 16, 16));
        c.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }

    // ───────────────────────── открытия ─────────────────────────
    float unlockT;
    public List<Goal> Goals = new();

    void UpdateUnlocks(float dt)
    {
        unlockT -= dt; if (unlockT > 0) return; unlockT = .4f;
        string[] hint = { "Хранилище и центр производства [4]", "Производит дерево само [5]", "Добывает руду сама [6]", "Плавит руду со склада [7]", "Добывает серебро и кристаллы [Z]", "Добывает обсидиан и уголь [X]" };
        for (int bk = 0; bk < 6; bk++)
            if ((Ann & (1L << bk)) == 0 && BUnlocked(bk))
            {
                Ann |= 1L << bk;
                Announce($"Открыта постройка: {BNames[bk]}", hint[bk], new Color("#d9b27c"), "tech");
            }
        for (int i = 0; i < Techs.Length; i++)
            if (!Techs[i].Reward && (Ann & (1L << (8 + i))) == 0 && Techs[i].Ok(this))
            {
                Ann |= 1L << (8 + i);
                Announce("Доступно улучшение: " + Techs[i].Name, "Откройте меню улучшений [T], вкладка «" + TechPages[Techs[i].Page] + "»", new Color("#8ff0ff"), "tech");
            }
        Goals = BuildGoals();
    }
}
