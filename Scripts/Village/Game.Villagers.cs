using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Живые жители (этап 9): постоянные личности, профессии из Resources/Villagers, назначение на рабочие места, простая машина состояний
/// Idle/Walking/Working/Interacting, поиск пути по освещённым клеткам и упрощённая симуляция далёких жителей. Производство остаётся в Game.UpdateVillage:
/// работник лишь меняет скорость здания (Building.WorkMul), поэтому ресурсы не удваиваются.</summary>
public partial class Game
{
    static readonly string[] VNames = { "Марк", "Лена", "Тихон", "Вера", "Ян", "Мира", "Олег", "Ада", "Борис", "Нора", "Кирилл", "Инга", "Лука", "Зоя", "Ярик", "Тая", "Глеб", "Ева", "Роман", "Алиса", "Даня", "Рада", "Фёдор", "Лиза" };
    const float ActiveR = 46 * TS, WalkSpeed = 28f;
    public int NextVillagerId = 5000;
    VillSave[] loadedVills;
    Villager talkV;                          // житель, с которым говорит игрок
    float gatherT;                           // после победы над стражем жители собираются у маяка
    float villT, decorT;

    public static string VillagerName(int id) => VNames[(id * 7 + id / 13) % VNames.Length];

    public Villager VillagerById(int id) { foreach (var v in Villagers) if (v.Id == id) return v; return null; }
    public Building BuildingByKey(int key) { if (key < 0) return null; foreach (var b in Buildings) if (World.Key(b.X, b.Y) == key) return b; return null; }
    public static int BKey(Building b) => World.Key(b.X, b.Y);
    public Villager WorkerOf(Building b) { int k = BKey(b); foreach (var v in Villagers) if (v.Work == k) return v; return null; }
    public static string ProfTitle(Villager v) => ProfDefs.Of(v.Prof)?.Title ?? "Житель";

    /// <summary>Профессия для нового жителя: первая из включённых, которой ещё нет в деревне; когда все есть — самая редкая.</summary>
    string NextProf()
    {
        ProfDef best = null; int bn = int.MaxValue;
        foreach (var d in ProfDefs.All)
        {
            if (!d.Enabled) continue; int n = 0; foreach (var v in Villagers) if (v.Prof == d.Id) n++;
            if (n < bn) { bn = n; best = d; }
        }
        return best?.Id ?? "";
    }

    /// <summary>Создаёт жителя. Повторный вызов с тем же Id ничего не делает (нет дублей после загрузки и в кооперативе).</summary>
    public Villager AddVillager(int id, Vector2 pos, string prof, string src, string name = null)
    {
        if (VillagerById(id) != null) return null;
        var v = new Villager { Id = id, Pos = pos, Name = name ?? VillagerName(id), Prof = prof ?? NextProf(), Src = src ?? "", NetPos = pos };
        if (v.Prof == "" ) v.Prof = NextProf();
        Villagers.Add(v); netT = 0; return v;
    }

    // ───────────────────────── точки и путь ─────────────────────────
    Vector2I HomeAnchor(Villager v)
    {
        var h = BuildingByKey(v.Home); if (h != null) return new Vector2I(h.X, h.Y + 1);
        var wh = Buildings.Find(b => b.Type == Tl.Warehouse); return wh != null ? new Vector2I(wh.X, wh.Y + 1) : new Vector2I(W.Spawn.X + 2, W.Spawn.Y);
    }

    /// <summary>Клетка, по которой можно идти: свободна, не вода и освещена (жители не уходят в Тьму).</summary>
    bool SafeTile(int x, int y) => World.In(x, y) && W.Walkable(x, y) && W.Ground[World.Key(x, y)] != Art.WaterG && IsLit(TileCenter(x, y));

    Vector2I WorkSpot(Building b)
    {
        foreach (var o in new[] { new Vector2I(0, 1), new Vector2I(1, 0), new Vector2I(-1, 0), new Vector2I(0, -1), new Vector2I(1, 1), new Vector2I(-1, 1) })
            if (SafeTile(b.X + o.X, b.Y + o.Y)) return new Vector2I(b.X + o.X, b.Y + o.Y);
        return new Vector2I(-1, -1);
    }

    Vector2I IdleSpot(Villager v, int radius)
    {
        var a = HomeAnchor(v);
        for (int i = 0; i < 10; i++)
        {
            var t = new Vector2I(a.X + rng.RandiRange(-radius, radius), a.Y + rng.RandiRange(-radius / 2, radius / 2));
            if (SafeTile(t.X, t.Y)) return t;
        }
        return SafeTile(a.X, a.Y) ? a : new Vector2I(-1, -1);
    }

    /// <summary>Поиск пути A* по освещённым свободным клеткам, не более <paramref name="cap"/> раскрытых клеток. null — маршрута нет.</summary>
    public List<Vector2> FindPath(Vector2I from, Vector2I to, int cap = 900)
    {
        var res = new List<Vector2>(); if (from == to) return res;
        var open = new PriorityQueue<Vector2I, int>(); var came = new Dictionary<int, int>(); var g = new Dictionary<int, int>(); var ok = new Dictionary<int, bool>();
        int K(Vector2I t) => World.Key(t.X, t.Y);
        bool Ok(Vector2I t) { int k = K(t); if (ok.TryGetValue(k, out var r)) return r; return ok[k] = SafeTile(t.X, t.Y); }
        open.Enqueue(from, 0); g[K(from)] = 0; came[K(from)] = -1; int n = 0;
        while (open.TryDequeue(out var c, out _))
        {
            if (c == to)
            {
                var rev = new List<Vector2>(); int k = K(c);
                while (k >= 0 && k != K(from)) { rev.Add(TileCenter(k % World.Wd, k / World.Wd)); k = came[k]; }
                rev.Reverse(); return rev;
            }
            if (++n > cap) break;
            foreach (var d in World.D4)
            {
                var nb = c + d; if (!World.In(nb.X, nb.Y) || (nb != to && !Ok(nb))) continue;
                int ng = g[K(c)] + 1, nk = K(nb);
                if (g.TryGetValue(nk, out var og) && og <= ng) continue;
                g[nk] = ng; came[nk] = K(c); open.Enqueue(nb, ng + Math.Abs(nb.X - to.X) + Math.Abs(nb.Y - to.Y));
            }
        }
        return null;
    }

    bool PathValid(Villager v)
    {
        for (int i = v.PathI; i < v.Path.Count; i++) { var t = ToTile(v.Path[i]); if (!W.Walkable(t.X, t.Y)) return false; }
        return true;
    }

    /// <summary>Маршрут к клетке. При неудаче житель переходит в безопасное состояние и через несколько попыток возвращается к маяку.</summary>
    void Plan(Villager v, Vector2I goal)
    {
        if (goal.X < 0) { v.Path.Clear(); v.Goal = goal; return; }
        var p = FindPath(ToTile(v.Pos), goal);
        if (p == null)
        {
            v.Path.Clear(); v.Goal = new Vector2I(-1, -1); v.State = VState.Idle; v.Working = false;
            if (++v.Fails >= 3) { v.Fails = 0; var s = SafeSpot(); if (s.X >= 0) { v.Pos = TileCenter(s.X, s.Y); v.Think = 1f; } }
            return;
        }
        v.Fails = 0; v.Path = p; v.PathI = 0; v.Goal = goal;
    }

    Vector2I SafeSpot()
    {
        for (int r = 0; r < 8; r++) for (int i = 0; i < 12; i++) { var t = new Vector2I(W.Spawn.X + rng.RandiRange(-r, r), W.Spawn.Y + 1 + rng.RandiRange(-r, r)); if (SafeTile(t.X, t.Y)) return t; }
        return new Vector2I(-1, -1);
    }

    // ───────────────────────── назначения ─────────────────────────
    /// <summary>Раз в секунду: убрать назначения на исчезнувшие здания, раздать свободных жителей по зданиям и жильём.</summary>
    void RecomputeVillagers()
    {
        if (!NetClient)
        {
        foreach (var v in Villagers)
        {
            var wb = BuildingByKey(v.Work); if (v.Work >= 0 && (wb == null || !IsProducer(wb.Type))) { v.Work = -1; v.Path.Clear(); v.Goal = new Vector2I(-1, -1); }
            if (v.Home >= 0 && BuildingByKey(v.Home) == null) v.Home = -1;
        }
        // двойное назначение невозможно по построению: у здания один работник
        var seen = new HashSet<int>(); foreach (var v in Villagers) if (v.Work >= 0 && !seen.Add(v.Work)) v.Work = -1;
        // автоматические назначения: своя профессия → переехать со «чужого» места → любой свободный
        var prods = Buildings.FindAll(b => IsProducer(b.Type) && !b.Manual);
        foreach (var b in prods) if (WorkerOf(b) == null) { var v = Villagers.Find(x => x.Work < 0 && ProfDefs.Of(x.Prof)?.BuildingTile == b.Type); if (v != null) v.Work = BKey(b); }
        foreach (var b in prods) if (WorkerOf(b) == null)
            {
                var v = Villagers.Find(x => ProfDefs.Of(x.Prof)?.BuildingTile == b.Type && x.Work >= 0 && BuildingByKey(x.Work) is Building ob && !ob.Manual && ProfDefs.Of(x.Prof)?.BuildingTile != ob.Type);
                if (v != null) v.Work = BKey(b);
            }
        foreach (var b in prods) if (WorkerOf(b) == null) { var v = Villagers.Find(x => x.Work < 0 && !(ProfDefs.Of(x.Prof)?.BuildingTile is Tl t && t != Tl.None && Buildings.Exists(q => q.Type == t && IsProducer(q.Type) && WorkerOf(q) == null && !q.Manual))); if (v == null) v = Villagers.Find(x => x.Work < 0); if (v != null) v.Work = BKey(b); }
        // жильё: места = уровень дома + 1
        foreach (var v in Villagers)
        {
            if (v.Home >= 0) continue;
            foreach (var h in Buildings) if (h.Type == Tl.House && HomeCount(h) < HouseCap(h)) { v.Home = BKey(h); break; }
        }
        }
        foreach (var b in Buildings)
        {
            if (!IsProducer(b.Type)) { b.Worker = false; b.WorkMul = 1f; continue; }
            var w = WorkerOf(b); b.Worker = w != null;
            var pd = w != null ? ProfDefs.Of(w.Prof) : null;
            b.WorkMul = w == null ? .6f : pd != null && pd.BuildingTile == b.Type ? pd.Rate : 1f;
        }
    }

    public static int HouseCap(Building h) => h.Level + 1;
    public int HomeCount(Building h) { int k = BKey(h), n = 0; foreach (var v in Villagers) if (v.Home == k) n++; return n; }
    public int HousingFree { get { int cap = 0, used = 0; foreach (var h in Buildings) if (h.Type == Tl.House) cap += HouseCap(h); foreach (var v in Villagers) if (v.Home >= 0) used++; return cap - used; } }

    /// <summary>Сменить работника здания по кругу: Авто → жители → никто → Авто. Любой ручной выбор отключает автоназначение для здания.</summary>
    public void CycleWorker(Building b)
    {
        if (!IsProducer(b.Type)) return;
        var cur = WorkerOf(b); var list = new List<Villager>(); foreach (var v in Villagers) list.Add(v);
        if (list.Count == 0) { Toast("В деревне нет жителей"); return; }
        int idx = !b.Manual ? -2 : cur == null ? list.Count : list.IndexOf(cur);       // −2 авто, n — никто
        idx++; if (idx == -1) idx = 0;
        if (idx > list.Count) { b.Manual = false; Sfx.Play("tick"); Toast("Работник: автоматически"); netT = 0; return; }
        b.Manual = true;
        if (idx == list.Count) { if (cur != null) cur.Work = -1; Toast("Работник убран"); }
        else { var v = list[idx]; foreach (var o in Villagers) if (o != v && o.Work == BKey(b)) o.Work = -1; v.Work = BKey(b); v.Path.Clear(); v.Goal = new Vector2I(-1, -1); Toast($"{v.Name}: {ProfTitle(v)} → {BNames[BIndex(b.Type)]}"); }
        Sfx.Play("tick"); netT = 0; RecomputeVillagers(); if (NetService.Active && NetService.Role == NetRole.Client) Net?.SendAssign(BKey(b), (b.Manual ? 1 : 0), WorkerOf(b)?.Id ?? -1);
    }

    public void ClearWorker(Building b) { if (!IsProducer(b.Type)) return; b.Manual = true; var cur = WorkerOf(b); if (cur != null) cur.Work = -1; Sfx.Play("tick"); Toast("Работник убран"); netT = 0; RecomputeVillagers(); if (NetService.Active && NetService.Role == NetRole.Client) Net?.SendAssign(BKey(b), 1, -1); }

    /// <summary>Назначение от хоста/участника по сети (хост применяет и рассылает).</summary>
    public bool NetApplyAssign(int key, int manual, int villagerId)
    {
        var b = BuildingByKey(key); if (b == null || !IsProducer(b.Type)) return false;
        b.Manual = manual != 0; foreach (var o in Villagers) if (o.Work == key) o.Work = -1;
        var v = villagerId >= 0 ? VillagerById(villagerId) : null; if (villagerId >= 0 && v == null) return false;
        if (v != null) { v.Work = key; v.Path.Clear(); v.Goal = new Vector2I(-1, -1); }
        RecomputeVillagers(); return true;
    }

    // ───────────────────────── поведение ─────────────────────────
    void UpdateVillagers(float dt)
    {
        gatherT = Mathf.Max(0, gatherT - dt);
        bool client = NetService.Active && NetService.Role == NetRole.Client;
        foreach (var v in Villagers) { if (client) NetFollow(v, dt); else StepVillager(v, dt); }
        if (!client) { UpdateVillageEvents(dt); UpdateDecorFx(dt); }
    }

    bool NearAnyPlayer(Vector2 p)
    {
        if (p.DistanceTo(PPos) < ActiveR) return true;
        if (NetService.Active) foreach (var r in Remotes()) if (p.DistanceTo(r.Pos) < ActiveR) return true;
        return false;
    }

    void StepVillager(Villager v, float dt)
    {
        v.Cool = Mathf.Max(0, v.Cool - dt);
        if (talkV == v && Window == Win.Talk) { v.State = VState.Interacting; v.Working = false; if (Mathf.Abs(PPos.X - v.Pos.X) > 1) v.Face = PPos.X < v.Pos.X ? -1 : 1; return; }
        if (!NearAnyPlayer(v.Pos)) { FarSim(v); return; }
        v.Think -= dt;
        if (v.Think <= 0) { v.Think = .6f + (v.Id % 5) * .07f; Decide(v); }
        Follow(v, dt);
        // реакция на игрока: короткое приветствие при приближении
        if (v.Cool <= 0 && v.State != VState.Walking && v.Pos.DistanceTo(PPos) < 34)
        {
            v.Cool = 50f; if (Mathf.Abs(PPos.X - v.Pos.X) > 1) v.Face = PPos.X < v.Pos.X ? -1 : 1;
            Texts.Add(new FText { P = v.Pos + new Vector2(0, -18), S = GreetLine(v), Life = 2.6f, C = ProfDefs.Of(v.Prof)?.Accent ?? Colors.White });
        }
    }

    /// <summary>Далёкие от игроков жители не ищут путь: они просто стоят на своём месте работы (или у дома).</summary>
    void FarSim(Villager v)
    {
        v.Path.Clear(); var wb = BuildingByKey(v.Work);
        if (wb != null) { var s = WorkSpot(wb); if (s.X >= 0) { v.Pos = TileCenter(s.X, s.Y); v.State = VState.Working; v.Working = true; v.Goal = s; return; } }
        v.State = VState.Idle; v.Working = false;
    }

    void Decide(Villager v)
    {
        var wb = BuildingByKey(v.Work); bool gather = gatherT > 0;
        if (v.Rest > 0) { v.Rest -= v.Think; if (v.Rest <= 0) v.WorkT = 0; }
        if (wb != null && !wb.Off && !gather && v.Rest <= 0)
        {
            var s = WorkSpot(wb);
            if (s.X < 0) { v.State = VState.Idle; v.Working = false; return; }
            var here = ToTile(v.Pos);
            if (here == s) { if (v.State != VState.Working) { v.State = VState.Working; v.Path.Clear(); v.Goal = s; v.WorkT = 0; } v.Working = true; v.WorkT += v.Think; if (v.WorkT > 55 + v.Id % 20) { v.Rest = 7 + v.Id % 4; v.State = VState.Idle; v.Working = false; v.Goal = new Vector2I(-1, -1); } return; }
            if (v.Goal != s || v.Path.Count == 0 || !PathValid(v)) Plan(v, s);
            return;
        }
        // отдых, сбор у маяка или нет работы: прогулка рядом с домом/маяком
        v.Working = false; v.WanderT -= v.Think;
        if (v.Path.Count == 0 || v.PathI >= v.Path.Count)
        {
            v.State = VState.Idle;
            if (v.WanderT <= 0) { v.WanderT = rng.RandfRange(3, 7); var s = gather ? SafeSpotNear(W.Spawn) : IdleSpot(v, wb != null ? 3 : 5); if (s.X >= 0) Plan(v, s); }
        }
        else if (!PathValid(v)) { v.Path.Clear(); v.State = VState.Idle; }
    }

    Vector2I SafeSpotNear(Vector2I c) { for (int i = 0; i < 12; i++) { var t = new Vector2I(c.X + rng.RandiRange(-4, 4), c.Y + 1 + rng.RandiRange(-3, 3)); if (SafeTile(t.X, t.Y)) return t; } return SafeSpot(); }

    void Follow(Villager v, float dt)
    {
        if (v.Path.Count > 0 && v.PathI < v.Path.Count)
        {
            var target = v.Path[v.PathI]; var d = target - v.Pos; float step = WalkSpeed * dt;
            if (d.Length() <= step) { v.Pos = target; v.PathI++; if (v.PathI >= v.Path.Count) { v.Path.Clear(); v.PathI = 0; v.Think = Mathf.Min(v.Think, .1f); } }
            else { v.Pos += d.Normalized() * step; if (Mathf.Abs(d.X) > .5f) v.Face = d.X < 0 ? -1 : 1; }
            v.State = VState.Walking; v.Working = false; v.Walk += dt * 8f; v.Stuck = 0; return;
        }
        if (v.State == VState.Working)
        {
            v.Anim += dt; v.Working = true; v.Fx -= dt;
            if (v.Fx <= 0) { v.Fx = .7f; WorkFx(v); }
        }
        else { v.Working = false; if (v.State == VState.Walking) v.State = VState.Idle; }
    }

    /// <summary>Рабочий удар: частицы и тихий звук (только рядом с игроком). Сама добыча тут не считается — она идёт в UpdateVillage.</summary>
    void WorkFx(Villager v)
    {
        var pd = ProfDefs.Of(v.Prof); if (pd == null) return; var wb = BuildingByKey(v.Work); if (wb != null && wb.Off) return;
        var p = v.Pos + new Vector2(v.Face * 7, -3); bool near = v.Pos.DistanceTo(PPos) < 130;
        switch (pd.Anim)
        {
            case "chop": Burst(p, new Color("#d9b27c"), 3, 40); if (near) Sfx.Play("tick", -14, .8f); break;
            case "pick": Burst(p, new Color("#cfd6e6"), 3, 45); if (near) Sfx.Play("tick", -14, 1.4f); break;
            case "hammer": Burst(p, new Color("#ffb04a"), 4, 55); if (near) Sfx.Play("tick", -12, 1.1f); break;
            case "water": Burst(p, new Color("#8ccaf0"), 3, 30); break;
            default: Burst(p, new Color("#8ff0ff"), 2, 25); break;
        }
    }

    /// <summary>Клиент: житель не симулируется, а плавно догоняет позицию хоста.</summary>
    void NetFollow(Villager v, float dt)
    {
        if (!v.NetInit) { v.Pos = v.NetPos; v.NetInit = true; }
        var d = v.NetPos - v.Pos;
        if (d.Length() > 90) v.Pos = v.NetPos; else v.Pos = v.Pos.Lerp(v.NetPos, 1f - Mathf.Exp(-9f * dt));
        if (Mathf.Abs(d.X) > .4f) v.Face = d.X < 0 ? -1 : 1;
        v.Working = v.State == VState.Working; if (v.State == VState.Walking) v.Walk += dt * 8f; else if (v.Working) v.Anim += dt;
    }

    // ───────────────────────── реплики ─────────────────────────
    string GreetLine(Villager v)
    {
        var pd = ProfDefs.Of(v.Prof); var l = pd?.Lines; if (l == null || l.Length == 0) return "Добрый свет.";
        return l[(v.Id + (int)(PlaySecs / 50)) % l.Length];
    }

    public List<string> TalkLines(Villager v)
    {
        var l = new List<string>(); var pd = ProfDefs.Of(v.Prof);
        if ((v.Talk & 1) == 0) l.Add($"Я {v.Name}. Спасибо, что вывели меня из тьмы. Теперь я ваш {(pd?.Title ?? "житель").ToLower()}.");
        if (pd != null) foreach (var s in pd.Lines) l.Add(s);
        var wb = BuildingByKey(v.Work);
        if (wb != null) l.Add($"Работаю: {BNames[BIndex(wb.Type)]}, уровень {wb.Level}. Быстрее идёт, когда рядом свет.");
        else if (pd != null && pd.Building != "") l.Add("Мне бы рабочее место. Постройте здание — я сразу приступлю.");
        if (BeaconLevel >= 2) l.Add("Печать пала — за ней шахты. Там тьма гуще, берегитесь.");
        if (BeaconLevel >= 3) l.Add("Слышал, на пустошах тлеет что-то древнее.");
        if (BeaconLevel >= 4) l.Add("Холодный ветер из монастыря... Свет там нужнее всего.");
        if (BossDown(1)) l.Add("Страж пал. Тени отступили — хотя бы ненадолго.");
        if (HousingFree < 0 || (v.Home < 0 && Buildings.Exists(b => b.Type == Tl.House))) l.Add("Домов не хватает, но я не жалуюсь: у маяка тепло.");
        else if (v.Home < 0) l.Add("Хорошо бы крышу над головой. Дом можно построить [B].");
        else l.Add("Дома тепло. Спасибо.");
        return l;
    }

    // ───────────────────────── события деревни ─────────────────────────
    int evLit = -1, evBoss = -1, evBeacon = -1; public int forgeMade; float noticeT = 120; public int VillEvents;
    void UpdateVillageEvents(float dt)
    {
        if (Villagers.Count == 0) return;
        if (evLit < 0) { evLit = LitCount; evBoss = BossMask; evBeacon = BeaconLevel; return; }
        if (LitCount > evLit) { evLit = LitCount; VillageSay(PPos, "Спасибо за свет! Здесь снова можно жить."); }
        if (BossMask != evBoss) { if ((BossMask & ~evBoss) != 0) { gatherT = 50f; VillageSay(TileCenter(W.Spawn.X, W.Spawn.Y), "Страж пал! Все к маяку!"); } evBoss = BossMask; }
        if (BeaconLevel > evBeacon) { evBeacon = BeaconLevel; VillageSay(PPos, "Печать пала. Путь открыт — но идите осторожно."); }
        noticeT -= dt; if (noticeT <= 0) { noticeT = 300; NoticeSomething(); }
    }

    void VillageSay(Vector2 near, string s)
    {
        Villager best = null; float bd = float.MaxValue; foreach (var v in Villagers) { float d = v.Pos.DistanceTo(near); if (d < bd) { bd = d; best = v; } }
        if (best != null) Texts.Add(new FText { P = best.Pos + new Vector2(0, -18), S = s, Life = 3.2f, C = ProfDefs.Of(best.Prof)?.Accent ?? Colors.White });
    }

    /// <summary>Не чаще раза в пять минут житель замечает необычное место неподалёку и отмечает его на карте.</summary>
    void NoticeSomething()
    {
        Poi best = null; float bd = 70 * TS;
        foreach (var p in W.Pois) { if (p.Seen || p.Revealed || p.State >= 3 || p.Kind == PK.Vault || p.Kind == PK.Secret) continue; float d = p.Pos.DistanceTo(TileCenter(W.Spawn.X, W.Spawn.Y)); if (d < bd) { bd = d; best = p; } }
        if (best == null) return;
        best.Revealed = true; Toast($"Житель заметил неподалёку: {PoiName(best.Kind)} — метка на карте"); VillageSay(PPos, "Кажется, там что-то светится...");
    }

    /// <summary>Кузнец заканчивает заказ: после каждых 30 слитков — реплика и небольшая награда. Не обязательное задание и без штрафов.</summary>
    void ForgeMilestone()
    {
        forgeMade++; if (forgeMade % 30 != 0) return;
        var sm = Villagers.Find(v => v.Prof == "smith"); if (sm == null) return;
        Inv[It.Coins] += 15; Texts.Add(new FText { P = sm.Pos + new Vector2(0, -18), S = "Заказ готов! Это вам.", Life = 3f, C = new Color("#ffb04a") }); Toast("Кузнец закончил заказ: +15 монет");
    }

    // ───────────────────────── взаимодействие ─────────────────────────
    void VillagerIx(Villager v, Ix ix)
    {
        var pd = ProfDefs.Of(v.Prof); ix.Title = v.Name;
        ix.Info($"{v.Name} · {pd?.Title ?? "Житель"}", TipRole.Text);
        var wb = BuildingByKey(v.Work); ix.Info(wb != null ? $"Работает: {BNames[BIndex(wb.Type)]}" : "Без рабочего места");
        ix.Action("E", "Поговорить"); ix.E = () => OpenTalk(v);
    }

    public int TalkIdx; public string TalkText = ""; public float TalkStart;
    public Villager TalkV => talkV;

    void OpenTalk(Villager v)
    {
        CloseWindows(); Placing = null; Window = Win.Talk; WinCur = 0; talkV = v; TalkIdx = -1; v.Think = 0; Sfx.Play("tick");
        TalkNext();
    }

    void TalkNext()
    {
        var v = talkV; if (v == null) return; var l = TalkLines(v);
        TalkIdx = (TalkIdx + 1) % l.Count; TalkText = l[TalkIdx]; TalkStart = UiTime;
        if (TalkIdx == 0 && (v.Talk & 1) == 0) v.Talk |= 1;
    }

    void TalkKey(Key k)
    {
        if (k == Key.Enter || k == Key.KpEnter || k == Key.E || k == Key.Space) { if (WinCur == 0) { TalkNext(); Sfx.Play("tick", -6, 1.2f); } else { talkV = null; CloseWindows(); } }
    }
}
