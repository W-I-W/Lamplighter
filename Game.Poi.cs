using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

public sealed class Goal { public string Title, Needs; public Vector2? Target; public bool Done; }

public partial class Game
{
    float poiT;

    public static string PoiName(PK k) => k switch
    {
        PK.Chest => "Сундук путника", PK.Ruins => "Древние руины", PK.House => "Заброшенный дом", PK.Camp => "Лагерь теней",
        PK.Shrine => "Святилище света", PK.Captive => "Пленник Тьмы", PK.Obelisk => "Обелиск", PK.Workshop => "Заброшенная мастерская", PK.Puzzle => "Хранилище света", PK.BridgeSite => "Разрушенный мост", _ => "Эфирная роща"
    };

    static string PoiHint(PK k) => k switch
    {
        PK.Chest => "Откройте сундук [E]", PK.Ruins => "Руины хранят добычу — осмотрите центр", PK.House => "В доме остались припасы и реликвии",
        PK.Camp => "Стража охраняет сундук — награда велика", PK.Shrine => "Святилище дарует благословение [E]",
        PK.Captive => "Победите стражу и освободите жителя — он будет работать в деревне",
        PK.Obelisk => "Коснитесь [E]: он укажет другие места",
        PK.Workshop => "Древний механизм можно починить — он ускорит деревню", PK.Puzzle => "Направьте луч света на приёмник зеркалами — откроется хранилище",
        PK.BridgeSite => "Почините мост [E] и пройдите на ту сторону", _ => "Редкие кристаллы эфира — нужна железная кирка"
    };

    public static Color PoiColor(PK k) => k switch
    {
        PK.Camp => new Color("#ff6a5a"), PK.Captive => new Color("#7ad45a"), PK.Shrine => new Color("#8ff0ff"), PK.Obelisk => new Color("#9fb4ff"),
        PK.Grove => new Color("#d58cff"), PK.Workshop => new Color("#8ff0ff"), PK.Puzzle => new Color("#ffe27a"), PK.BridgeSite => new Color("#d9b27c"), _ => new Color("#ffd86b")
    };

    void UpdatePois(float dt)
    {
        poiT -= dt; if (poiT > 0) return; poiT = .4f;
        float seeR = 7 * TS;
        foreach (var p in W.Pois)
        {
            float d = p.Pos.DistanceTo(PPos);
            if (!p.Seen && d < seeR)
            {
                p.Seen = true; GiveXp(8);
                Announce("Найдено: " + PoiName(p.Kind), PoiHint(p.Kind), PoiColor(p.Kind), "discover");
                if (p.Kind == PK.Grove) p.State = 3;
            }
            if ((p.Kind == PK.Camp || p.Kind == PK.Captive) && p.State == 0 && d < 13 * TS) SpawnGuards(p);
            if (p.State == 1)
            {
                bool any = false; foreach (var e in Enemies) if (e.Guard == p) { any = true; break; }
                if (!any)
                {
                    p.State = 2; Sfx.Play("tick");
                    if (p.Kind == PK.Camp) Announce("Лагерь зачищен", "Сундук теперь можно открыть [E]", new Color("#7ad45a"), "discover");
                    else Announce("Стража побеждена", "Подойдите к клетке и освободите жителя [E]", new Color("#7ad45a"), "discover");
                    GiveXp(20);
                }
            }
        }
    }

    void SpawnGuards(Poi p)
    {
        p.State = 1;
        float distC = p.Pos.DistanceTo(TileCenter(World.N / 2, World.N / 2)) / TS;
        float scale = 1f + distC / 90f;
        for (int i = 0; i < p.Guards; i++)
        {
            for (int tries = 0; tries < 20; tries++)
            {
                float a = rng.Randf() * Mathf.Tau, d = rng.RandfRange(24, 56);
                var pos = p.Pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d; var t = ToTile(pos);
                if (W.Get(t.X, t.Y) != Tl.None) continue;
                EK k;
                int reg = World.RegionOf(p.Tile.X);
                if (reg == 1) { float r = rng.Randf(); k = r < .35f ? EK.Beetle : r < .7f ? EK.Leech : r < .85f ? EK.Spitter : EK.Bomber; }
                else if (reg == 2) { float r = rng.Randf(); k = r < .4f ? EK.Imp : r < .65f ? EK.Spirit : r < .9f ? EK.Golem : EK.Bomber; }
                else if (p.Kind == PK.Captive) k = i == 2 ? EK.Wraith : EK.Shade;
                else { float r = rng.Randf(); k = r < .4f ? EK.Shade : r < .65f ? EK.Spitter : r < .85f ? EK.Wraith : EK.Bomber; }
                var e = SpawnEnemy(pos, k, p, scale); e.Age = -.2f;
                break;
            }
        }
        Announce(p.Kind == PK.Camp ? "Стража лагеря!" : "Стража пленника!", "Тени заметили вас — сражайтесь", new Color("#ff6a5a"), "boss");
    }

    bool PoiIx(Poi p, Ix ix)
    {
        var t = W.Get(p.Tile.X, p.Tile.Y);
        switch (t)
        {
            case Tl.Chest:
                if (p.Kind == PK.Camp && p.State < 2) ix.Lines.Add("Сундук охраняется: победите стражу лагеря");
                else { ix.Lines.Add("[E] Открыть сундук"); ix.E = () => OpenChest(p); }
                return true;
            case Tl.Shrine: ix.Lines.Add("[E] Помолиться у святилища света"); ix.E = () => UseShrine(p); return true;
            case Tl.Obelisk: ix.Lines.Add("[E] Коснуться обелиска"); ix.E = () => UseObelisk(p); return true;
            case Tl.Cage:
                if (p.State < 2) ix.Lines.Add("Пленника охраняют: победите стражу");
                else { ix.Lines.Add("[E] Освободить жителя"); ix.E = () => Rescue(p); }
                return true;
        }
        return false;
    }

    void OpenChest(Poi p)
    {
        var r = new Random(W.Seed * 7919 + p.Tile.X * 131 + p.Tile.Y);
        float distC = p.Pos.DistanceTo(TileCenter(World.N / 2, World.N / 2)) / TS;
        int reg = World.RegionOf(p.Tile.X);
        int tier = reg > 0 ? 3 : distC < 22 ? 0 : distC < 40 ? 1 : 2;
        var got = new List<(int, int)>();
        void G(int item, int n) { if (n > 0) got.Add((item, n)); }
        G(It.Wood, r.Next(3, 7) + tier * 2);
        if (r.NextDouble() < .6) G(It.Stone, r.Next(3, 8));
        G(It.Copper, r.Next(1, 3) + (tier > 0 ? 1 : 0));
        if (tier >= 1) G(It.Iron, r.Next(1, 3) + tier - 1);
        G(It.Shard, r.Next(1, 3) + tier);
        float rel = .12f + .15f * tier + (p.Kind == PK.Ruins ? .25f : p.Kind == PK.House ? .2f : p.Kind == PK.Camp ? .25f : 0f);
        if (r.NextDouble() < rel) G(It.Relic, 1);
        if (tier >= 2 && r.NextDouble() < .4) G(It.Aether, r.Next(1, 3));
        if (p.Kind == PK.Camp) G(It.Shard, 3);
        if (reg == 1) { G(It.Silver, r.Next(1, 4)); G(It.Crystal, r.Next(1, 3)); }
        if (reg == 2) { G(It.Obsidian, r.Next(1, 3)); G(It.Ember, r.Next(1, 3)); }
        if (p.Variant == 1) { G(It.Relic, 1); G(It.Crystal, 2); if (reg > 0) G(It.Aether, 1 + r.Next(2)); }
        var parts = new List<string>(); int k = 0;
        foreach (var (item, n) in got)
        {
            Inv[item] += n; parts.Add($"{n} {It.Ab[item]}");
            Texts.Add(new FText { P = p.Pos + new Vector2((k % 3 - 1) * 16, -10 - (k / 3) * 8), S = $"+{n} {It.Names[item]}", Life = 1.6f, C = It.Col[item] }); k++;
        }
        SetObj(p.Tile.X, p.Tile.Y, Tl.ChestOpen); p.State = 3; GiveXp(15 + tier * 5);
        Burst(p.Pos, new Color("#ffe27a"), 18, 80);
        Announce(p.Variant == 1 ? "Тайник вскрыт!" : "Сундук открыт!", string.Join(", ", parts), new Color("#ffd86b"), "chest");
        Save();
    }

    void UseShrine(Poi p)
    {
        var r = new Random(W.Seed * 104729 + p.Tile.X * 31 + p.Tile.Y);
        int b = r.Next(3);
        SetObj(p.Tile.X, p.Tile.Y, Tl.ShrineUsed); p.State = 3;
        Burst(p.Pos, new Color("#8ff0ff"), 26, 100);
        if (b == 0) { MaxHp += 20; Hp = MaxHp; Announce("Благословение стойкости", "Максимум здоровья +20", new Color("#8ff0ff"), "levelup"); }
        else if (b == 1) { Announce("Благословение мудрости", "Вы получили целый уровень опыта", new Color("#8ff0ff"), "levelup"); GiveXp(XpNeed - Xp + .01f); }
        else { Inv[It.Relic]++; Inv[It.Shard] += 3; Announce("Дар света", "Реликвия и 3 осколка света", new Color("#8ff0ff"), "chest"); }
        GiveXp(10); Save();
    }

    void UseObelisk(Poi p)
    {
        var list = new List<Poi>();
        foreach (var q in W.Pois) if (q != p && q.State < 3 && !q.Seen && !q.Revealed && q.Kind != PK.Obelisk) list.Add(q);
        list.Sort((a, b) => a.Pos.DistanceTo(PPos).CompareTo(b.Pos.DistanceTo(PPos)));
        var names = new List<string>();
        for (int i = 0; i < Math.Min(2, list.Count); i++) { list[i].Revealed = true; names.Add($"{PoiName(list[i].Kind)} ({(int)(list[i].Pos.DistanceTo(PPos) / TS)})"); }
        SetObj(p.Tile.X, p.Tile.Y, Tl.ObeliskUsed); p.State = 3; GiveXp(15);
        Burst(p.Pos, new Color("#9fb4ff"), 22, 90);
        Announce("Обелиск показал путь", names.Count > 0 ? string.Join(", ", names) + " — метки на экране" : "Тайн больше нет", new Color("#9fb4ff"), "tech");
        Save();
    }

    void Rescue(Poi p)
    {
        SetObj(p.Tile.X, p.Tile.Y, Tl.None); p.State = 3;
        var wh = Buildings.Find(b => b.Type == Tl.Warehouse);
        Villagers.Add(new Villager { Pos = p.Pos, Wander = wh != null ? wh.Pos : TileCenter(W.Spawn.X + 2, W.Spawn.Y) });
        GiveXp(50); Burst(p.Pos, new Color("#7ad45a"), 24, 100);
        Announce("Житель освобождён!", Villagers.Count == 1 ? "Он пойдёт в деревню и начнёт работать. Открыта Кузница [7]" : "Ещё один работник в деревне", new Color("#7ad45a"), "rescue");
        Save();
    }

    public IEnumerable<Poi> MarkerPois()
    {
        foreach (var p in W.Pois)
        {
            if (p.State >= 3) continue;
            if (p.Revealed && !p.Seen) { yield return p; continue; }
            if (Has(Tech.Sight) && p.Kind != PK.Grove && p.Pos.DistanceTo(PPos) < 22 * TS) yield return p;
        }
    }

    // ───────────────────────── цели ─────────────────────────
    Poi NearestPoi(PK kind, bool knownOnly)
    {
        Poi best = null; float bd = float.MaxValue;
        foreach (var p in W.Pois)
        {
            if (p.Kind != kind || p.State >= 3) continue;
            if (knownOnly && !p.Seen && !p.Revealed) continue;
            float d = p.Pos.DistanceTo(PPos); if (d < bd) { bd = d; best = p; }
        }
        return best;
    }

    List<Goal> BuildGoals()
    {
        var g = new List<Goal>();
        void Add(string t, bool done, string needs, Vector2? target = null) { if (!done) g.Add(new Goal { Title = t, Needs = needs, Target = target }); }
        int rescued = Villagers.Count;
        var a1 = W.Ancients[1]; var boss1 = BossLantern(1);
        bool Lv2() { foreach (var b in Buildings) if (b.Level >= 2) return true; return false; }
        Add("Построить печь [1]", HasFurnace, NeedStr(CostFurnace));
        Add("Медная кирка [3] у печи", Tier >= 1, Inv[It.Copper] < 5 ? "Переплавьте медь [E у печи]: " + NeedStr(CostPick[0]) : NeedStr(CostPick[0]));
        Add("Зажечь древний фонарь #1", a1.Lit, $"Осколки света {Math.Min(Inv[It.Shard], a1.Cost)}/{a1.Cost} (их роняют тени)", a1.Pos);
        Add("Построить склад [4]", HasBuilding(Tl.Warehouse), NeedStr(BCost(0, 1)));
        var cap = NearestPoi(PK.Captive, true);
        Add("Спасти жителя", rescued >= 1, cap != null ? "Победите стражу у клетки" : "Ищите клетку: места и обелиски подскажут", cap?.Pos);
        Add("Построить лесопилку [5]", HasBuilding(Tl.Sawmill), NeedStr(BCost(1, 1)));
        Add("Построить шахту [6]", HasBuilding(Tl.Mine), LitCount >= 1 ? NeedStr(BCost(2, 1)) : "Сначала зажгите древний фонарь");
        Add("Железная кирка [3]", Tier >= 2, Tier < 1 ? "Сначала медная кирка" : NeedStr(CostPick[1]));
        Add("Построить кузницу [7]", HasBuilding(Tl.Forge), rescued >= 1 ? NeedStr(BCost(3, 1)) : "Нужен спасённый житель");
        Add("Исследовать 4 места", PoisDone >= 4, $"Исследовано {PoisDone}/4 (сундуки, руины, лагеря…)");
        Add("Улучшить здание до ур.2 [U]", Lv2(), "Подойдите к зданию и нажмите U");
        Add("Изучить 3 улучшения [T]", TechCount >= 3, $"Изучено {TechCount}/3");
        Add("Победить Хранителя Тёмного леса", BossDown(1), "Он ждёт у особого фонаря на краю леса — награда: первое ядро света", boss1?.Pos);
        Add("Улучшить Центральный маяк", BeaconLevel >= 2, $"Ядро света {Math.Min(Inv[It.Core], 1)}/1 — подойдите к маяку и нажмите E: откроется путь в шахты", W.Ancients[0].Pos);
        if (BeaconLevel >= 2)
        {
            var nm = NearestUnlit(1);
            Add("Зажечь фонари шахт", LitIn(1) >= 3, $"Зажжено {LitIn(1)}/3 — осколки света {Inv[It.Shard]}", nm?.Pos);
            Add("Решить загадку света", PuzzlesSolved >= 1, "Найдите хранилище: зеркала [E] направляют луч на приёмник");
            Add("Кристальная кирка [3]", Tier >= 3, Tier < 2 ? "Сначала железная кирка" : NeedStr(CostPick[2]));
            Add("Изучить «Кристальную оптику» [T]", Has(Tech.Optics), "Нужны кристаллы и серебро (вкладка «Глубины»)");
            Add("Победить Хранителя глубин", BossDown(2), "Он в самом глубоком озере шахт — награда: Прожектор [R] и второе ядро", BossLantern(2)?.Pos);
            Add("Улучшить маяк: путь в пустоши", BeaconLevel >= 3, $"Ядро света {Math.Min(Inv[It.Core], 1)}/1 — нажмите E у маяка", W.Ancients[0].Pos);
        }
        if (BeaconLevel >= 3)
        {
            var na = NearestUnlit(2);
            Add("Зажечь фонари пустошей", LitIn(2) >= 3, $"Зажжено {LitIn(2)}/3 — осколки света {Inv[It.Shard]}", na?.Pos);
            Add("Теплозащита или огненное лезвие [T]", Has(Tech.HeatShield) || Has(Tech.FlameEdge), "Вкладка «Пепел»: нужны обсидиан и уголь пепла");
            Add("Обсидиановая кирка [3]", Tier >= 4, Tier < 3 ? "Сначала кристальная кирка (шахты)" : NeedStr(CostPick[3]));
            Add("Построить коллектор пепла [X]", HasBuilding(Tl.Collector), Has(Tech.Collector) ? "Ставьте на освещённой земле пустошей" : "Изучите «Пепельный коллектор» [T]");
            Add("Победить Пепельного стража", BossDown(3), "Он у края пустошей — награда: огнестойкость и третье ядро", BossLantern(3)?.Pos);
            Add("Улучшить маяк до 4 уровня", BeaconLevel >= 4, $"Ядро света {Math.Min(Inv[It.Core], 1)}/1 — нажмите E у маяка", W.Ancients[0].Pos);
        }
        return g;
    }

    public Ancient BossLantern(int id) { foreach (var a in W.Ancients) if (a.Boss && a.BossId == id) return a; return null; }
    Ancient NearestUnlit(int region)
    {
        Ancient best = null; float bd = float.MaxValue;
        foreach (var a in W.Ancients) if (a.Region == region && !a.Boss && !a.Lit) { float d = a.Pos.DistanceTo(PPos); if (d < bd) { bd = d; best = a; } }
        return best;
    }
    public int PuzzlesSolved { get { int n = 0; foreach (var pz in W.Puzzles) if (pz.Poi.State == 3) n++; return n; } }
}
