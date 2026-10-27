using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

public sealed class GEvent
{
    public string Kind; public Vector2 Pos; public float T, Life, V; public int State, Count, Seed; public bool Done;
    public Vector2 Npc; public int Wave; public float WaveT;
}

/// <summary>Динамические события: редкие, зависят от биома и прогресса, не трогают постройки и древние фонари.</summary>
public partial class Game
{
    public List<GEvent> Events = new();
    float evNext = 150; public bool EventsEnabled = true;
    public int ExtraVillagers;

    public static string EvName(string k) => k switch
    {
        "caravan" => "Теневой караван", "lamp" => "Осаждённый фонарь", "trader" => "Странствующий торговец", "meteor" => "Падение метеорита",
        "rift" => "Разлом Тьмы", "elite" => "Редкий монстр", _ => "Заблудший путник"
    };
    public static Color EvColor(string k) => k switch
    {
        "caravan" => new Color("#ffb04a"), "lamp" => new Color("#ffe27a"), "trader" => new Color("#7ad45a"), "meteor" => new Color("#fff2b0"),
        "rift" => new Color("#c070ff"), "elite" => new Color("#ff6a5a"), _ => new Color("#8ff0ff")
    };

    EK[] RegionPool(int reg) => reg == 1 ? new[] { EK.Beetle, EK.Leech, EK.Spitter } : reg == 2 ? new[] { EK.Imp, EK.Golem, EK.Spirit } : new[] { EK.Shade, EK.Wraith, EK.Spitter };

    // ───────────────────────── выбор места ─────────────────────────
    bool SpotOk(Vector2 p, bool needDark, int clear = 2)
    {
        var t = ToTile(p);
        if (!World.In(t.X, t.Y)) return false;
        for (int y = -clear; y <= clear; y++) for (int x = -clear; x <= clear; x++) { if (W.Get(t.X + x, t.Y + y) != Tl.None) return false; }
        var g = W.Ground[World.Key(t.X, t.Y)]; if (g == Art.WaterG || g == Art.HotAsh) return false;
        foreach (var b in Buildings) if (b.Pos.DistanceTo(p) < 100) return false;
        foreach (var l in Lamps) if (l.Pos.DistanceTo(p) < 48) return false;
        foreach (var a in W.Ancients) if (a.Pos.DistanceTo(p) < 11 * TS) return false;
        foreach (var q in W.Pois) if (q.Pos.DistanceTo(p) < 5 * TS) return false;
        foreach (var e in Events) if (e.Pos.DistanceTo(p) < 8 * TS) return false;
        if (needDark && IsLit(p)) return false;
        return true;
    }

    Vector2? FindSpot(float minD, float maxD, bool needDark, int clear = 2, bool sameRegion = true)
    {
        int reg = RegionAt(PPos);
        for (int i = 0; i < 60; i++)
        {
            float a = rng.Randf() * Mathf.Tau, d = rng.RandfRange(minD, maxD) * TS;
            var p = PPos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
            if (sameRegion && RegionAt(p) != reg) continue;
            if (p.X < 8 * TS || p.Y < 8 * TS || p.Y > (World.Ht - 8) * TS) continue;
            if (SpotOk(p, needDark, clear)) return p;
        }
        return null;
    }

    Vector2? FindLitSpot()
    {
        for (int i = 0; i < 80; i++)
        {
            var p = PPos + new Vector2(rng.RandfRange(-110, 110), rng.RandfRange(-80, 80));
            var t = ToTile(p); if (!World.In(t.X, t.Y)) continue;
            if (!IsLit(p) || W.Get(t.X, t.Y) != Tl.None || W.Get(t.X + 1, t.Y) != Tl.None || W.Get(t.X, t.Y + 1) != Tl.None) continue;
            if (W.Ground[World.Key(t.X, t.Y)] == Art.WaterG) continue;
            bool near = false; foreach (var b in Buildings) if (b.Pos.DistanceTo(p) < 28) near = true;
            foreach (var e in Events) if (e.Pos.DistanceTo(p) < 40) near = true;
            if (!near) return p;
        }
        return null;
    }

    // ───────────────────────── планировщик ─────────────────────────
    List<string> EventPool()
    {
        var l = new List<string> { "elite", "traveler" };
        int reg = RegionAt(PPos);
        if (LitCount >= 1 || BeaconLevel >= 2) l.Add("meteor");
        if (LitCount >= 2 || BeaconLevel >= 2) l.Add("caravan");
        if (Lamps.Count >= 1) l.Add("lamp");
        if (BossDown(1)) l.Add("rift");
        if (Villagers.Count >= 1 || HasBuilding(Tl.Stall)) l.Add("trader");
        if (reg == 0 && LitCount < 1) { l.Remove("traveler"); }
        return l;
    }

    void UpdateEvents(float dt)
    {
        for (int i = Events.Count - 1; i >= 0; i--) { var ev = Events[i]; UpdateEvent(ev, dt); if (ev.Done) EndEvent(ev); }
        if (!EventsEnabled) return;
        evNext -= dt;
        if (evNext > 0) return;
        evNext = rng.RandfRange(130, 260);
        if (Events.Count >= 2 || Boss != null || TrialActive) return;
        var pool = EventPool(); pool.RemoveAll(k => Events.Exists(e => e.Kind == k));
        if (pool.Count == 0) return;
        SpawnEvent(pool[rng.RandiRange(0, pool.Count - 1)]);
    }

    public GEvent SpawnEvent(string kind, Vector2? at = null)
    {
        Vector2? pos = at;
        if (pos == null)
            pos = kind == "trader" ? FindLitSpot() : kind == "lamp" ? FindSpot(10, 18, true, 2) : FindSpot(11, 19, true, kind == "meteor" ? 2 : 1);
        if (pos == null) return null;
        var ev = new GEvent { Kind = kind, Pos = pos.Value, Seed = (int)(rng.Randi() % 100000), Life = kind switch { "trader" => 170f, "caravan" => 200f, "lamp" => 150f, "meteor" => 220f, "rift" => 240f, "elite" => 170f, _ => 200f } };
        InitEvent(ev); Events.Add(ev); return ev;
    }

    EK PoolKind(int reg, bool heavy = false)
    {
        var pool = RegionPool(reg); return heavy ? pool[reg == 0 ? 0 : reg == 1 ? 0 : 1] : pool[rng.RandiRange(0, pool.Length - 1)];
    }

    Enemy EvEnemy(GEvent ev, Vector2 p, EK k, float hpMul = 1f, bool elite = false)
    {
        var e = SpawnEnemy(p, k, null, DangerScale * (elite ? 1.3f : 1f)); e.Ev = ev; e.Home = ev.Pos; e.Age = -.3f;
        e.Hp *= hpMul; e.MaxHp = e.Hp; e.Elite = elite; return e;
    }

    /// <summary>Восстановление события из сохранения: заново создаёт его сущности.</summary>
    void InitEventState(GEvent ev)
    {
        int reg = World.RegionOf((int)(ev.Pos.X / TS));
        switch (ev.Kind)
        {
            case "elite": EvEnemy(ev, ev.Pos, PoolKind(reg, true), 3.5f, true); break;
            case "caravan": { var c = EvEnemy(ev, ev.Pos, PoolKind(reg, true), 2.2f, true); c.Carrier = true; for (int i = 0; i < 3; i++) EvEnemy(ev, ev.Pos + Vector2.FromAngle(i * 2.1f) * 26, PoolKind(reg)); break; }
            case "trader": case "traveler": ev.Npc = ev.Pos; break;
            case "rift": if (ev.State == 1) { ev.State = 0; ev.Life = 240; ev.T = 0; } break;
        }
    }

    void InitEvent(GEvent ev)
    {
        int reg = World.RegionOf((int)(ev.Pos.X / TS));
        Color col = EvColor(ev.Kind);
        string sub = "";
        switch (ev.Kind)
        {
            case "elite": { var k = PoolKind(reg, true); if (rng.Randf() < .5f) k = PoolKind(reg); var e = EvEnemy(ev, ev.Pos, k, 3.5f, true); sub = $"{EnemyName(k)} усилен: ценная добыча. Метка на экране"; break; }
            case "caravan":
                {
                    var c = EvEnemy(ev, ev.Pos, PoolKind(reg, true), 2.2f, true); c.Carrier = true;
                    for (int i = 0; i < 3; i++) EvEnemy(ev, ev.Pos + Vector2.FromAngle(i * 2.1f) * 26, PoolKind(reg));
                    sub = "Победите носильщика (золотой) — в мешках редкие ресурсы"; break;
                }
            case "lamp": ev.V = 60f; ev.Wave = 0; ev.WaveT = 4f; sub = "Защитите фонарь от теней 45 секунд — он останется вашим навсегда"; break;
            case "trader": ev.Npc = ev.Pos; sub = "Подойдите и нажмите E: редкие товары на ограниченное время"; break;
            case "meteor": ev.State = 0; ev.V = 3.2f; sub = "С неба падает свет — осколок упадёт в отмеченном месте"; break;
            case "rift": ev.State = 0; sub = "Необязательное испытание: временная реликвия, три волны, награда за победу"; break;
            default: ev.Npc = ev.Pos; sub = "Подойдите [E] и проводите к освещённой земле"; break;
        }
        Announce("Событие: " + EvName(ev.Kind), sub, col, ev.Kind == "rift" ? "boss" : "discover");
    }

    void EndEvent(GEvent ev)
    {
        Events.Remove(ev);
        for (int i = Enemies.Count - 1; i >= 0; i--) if (Enemies[i].Ev == ev) { if (Enemies[i] == Boss) Boss = null; Enemies.RemoveAt(i); }
        if (ev.Kind == "meteor") { var t = ToTile(ev.Pos); if (W.Get(t.X, t.Y) == Tl.MeteorOre) SetObj(t.X, t.Y, Tl.None); }
        if (ev.Kind == "rift") { ClearTemp(); TrialActive = false; }
    }

    // ───────────────────────── поведение ─────────────────────────
    public bool TrialActive;
    float trialT; public float TrialLeft => trialT;

    void UpdateEvent(GEvent ev, float dt)
    {
        ev.T += dt;
        float dist = ev.Pos.DistanceTo(PPos);
        int reg = World.RegionOf((int)(ev.Pos.X / TS));
        if (ev.T > ev.Life && !(ev.Kind == "rift" && ev.State == 1)) { Fade(ev); ev.Done = true; return; }
        switch (ev.Kind)
        {
            case "elite":
                if (!Enemies.Exists(e => e.Ev == ev)) ev.Done = true;
                else if (dist > 50 * TS && ev.T > 20) { Fade(ev); ev.Done = true; }
                break;
            case "caravan":
                if (!Enemies.Exists(e => e.Ev == ev && e.Carrier)) ev.Done = true;
                else if (dist > 55 * TS && ev.T > 20) { Fade(ev); ev.Done = true; }
                break;
            case "lamp":
                {
                    ev.WaveT -= dt;
                    if (ev.Wave < 4 && ev.WaveT <= 0 && dist < 40 * TS)
                    {
                        ev.Wave++; ev.WaveT = 11f;
                        for (int i = 0; i < 3; i++) EvEnemy(ev, ev.Pos + Vector2.FromAngle(rng.Randf() * Mathf.Tau) * rng.RandfRange(70, 95), PoolKind(reg));
                        Sfx.Play("boss", -8, 1.4f); Toast($"Тени осаждают фонарь: волна {ev.Wave}/4");
                    }
                    foreach (var e in Enemies) if (e.Ev == ev && e.Pos.DistanceTo(ev.Pos) < 26) ev.V -= 2.2f * dt;
                    if (ev.V <= 0) { Announce("Фонарь погас", "Тени сломили осаждённый фонарь — ничего не потеряно", new Color("#9aa6bf"), "error"); ev.Done = true; }
                    else if (ev.Wave >= 4 && !Enemies.Exists(e => e.Ev == ev)) LampSaved(ev);
                    break;
                }
            case "trader":
                if (dist > 60 * TS && ev.T > 15) ev.Done = true;
                break;
            case "meteor":
                if (ev.State == 0)
                {
                    ev.V -= dt;
                    if (rng.Randf() < dt * 25) Parts.Add(new Particle { P = ev.Pos + new Vector2(rng.RandfRange(-30, 30), -ev.V * 90), V = new Vector2(0, 80), Life = .3f, Max = .3f, C = new Color("#fff2b0"), Size = 2 });
                    if (ev.V <= 0) { ev.State = 1; MeteorImpact(ev); }
                }
                else { var t = ToTile(ev.Pos); if (W.Get(t.X, t.Y) != Tl.MeteorOre) ev.Done = true; }
                break;
            case "rift": UpdateRift(ev, dt); break;
            default:
                if (ev.State == 1)
                {
                    var to = PPos - ev.Npc; if (to.Length() > 28) ev.Npc = Move(ev.Npc, to.Normalized() * 40f * dt, 4f, false);
                    ev.Pos = ev.Npc;
                    if (IsLit(ev.Npc)) TravelerSaved(ev);
                    else if (dist > 40 * TS) { ev.Done = true; Toast("Путник потерялся в темноте"); }
                }
                break;
        }
    }

    void Fade(GEvent ev) { if (dist(ev) < 30 * TS) Toast($"{EvName(ev.Kind)}: событие закончилось"); }
    float dist(GEvent ev) => ev.Pos.DistanceTo(PPos);

    void LampSaved(GEvent ev)
    {
        var t = ToTile(ev.Pos);
        if (W.Get(t.X, t.Y) == Tl.None && Lamps.Count < MaxLamps) { SetObj(t.X, t.Y, Tl.Lantern); Lamps.Add(new Lamp { X = t.X, Y = t.Y, Level = 2, Anim = 0 }); }
        Inv[It.Coins] += 35; GrantTag(RollTag(new Random(ev.Seed), 2), ev.Pos);
        Announce("Фонарь спасён!", "Он теперь ваш: постоянный свет и 35 монет", new Color("#ffe27a"), "ancient"); GiveXp(70); ev.Done = true; Save();
    }

    void TravelerSaved(GEvent ev)
    {
        Inv[It.Coins] += 40; GrantTag(RollTag(new Random(ev.Seed), 1), PPos);
        string extra = "";
        if (ExtraVillagers < 3) { ExtraVillagers++; var wh = Buildings.Find(b => b.Type == Tl.Warehouse); Villagers.Add(new Villager { Pos = ev.Npc, Wander = wh != null ? wh.Pos : TileCenter(W.Spawn.X + 2, W.Spawn.Y) }); extra = " Путник остаётся в деревне работником!"; }
        Announce("Путник спасён!", "Он благодарит: 40 монет и подарок." + extra, new Color("#8ff0ff"), "rescue"); GiveXp(50); ev.Done = true; Save();
    }

    void MeteorImpact(GEvent ev)
    {
        var t = ToTile(ev.Pos);
        if (W.Get(t.X, t.Y) == Tl.None) SetObj(t.X, t.Y, Tl.MeteorOre);
        Shake = 6; Flash = .4f; Sfx.Play("boom"); Burst(ev.Pos, new Color("#fff2b0"), 40, 160);
        Beacons.Add(new Beacon { Pos = ev.Pos, Dur = 2f, R = 90, Ring = 1f });
        Announce("Метеорит упал!", "Светящаяся руда в кратере: добудьте её (нужна медная кирка)", new Color("#fff2b0"), "tech");
    }

    public void EvEnemyKilled(Enemy e)
    {
        var ev = e.Ev; if (ev == null) return;
        int reg = RegionAt(e.Pos); var r = new Random(ev.Seed);
        if (ev.Kind == "elite")
        {
            Inv[It.Coins] += 25 + r.Next(20); ModInv[r.Next(7)]++; GrantTag(RollTag(r, 2, .25f), e.Pos);
            Announce("Редкий монстр повержен!", "Монеты, модификатор оружия и редкая находка", new Color("#ff6a5a"), "chest"); GiveXp(60); ev.Done = true;
        }
        else if (ev.Kind == "caravan" && e.Carrier)
        {
            int[] rare = reg == 1 ? new[] { It.Crystal, It.Silver } : reg == 2 ? new[] { It.Obsidian, It.Ember } : new[] { It.Iron, It.Copper };
            foreach (var it in rare) Give(it, 3 + r.Next(3), e.Pos + new Vector2(r.Next(-8, 8), -6));
            Inv[It.Coins] += 40 + r.Next(40); GrantTag(RollTag(r, 2), e.Pos);
            Announce("Караван разгромлен!", "Редкие ресурсы, монеты и находка из мешков", new Color("#ffb04a"), "chest"); GiveXp(80); ev.Done = true;
        }
    }

    // ───────────────────────── разлом и испытание ─────────────────────────
    static readonly string[] TempIds = { "t_flame", "t_frost", "t_light", "t_storm" };

    void StartTrial(GEvent ev)
    {
        if (ev.State != 0) return;
        ev.State = 1; ev.Wave = 0; ev.WaveT = 1f; ev.T = 0; ev.Life = 9999; trialT = 80f; TrialActive = true;
        var id = TempIds[ev.Seed % 4]; GrantTemp(id);
        Announce("Испытание Тьмы началось!", $"Временная реликвия: «{Db.Relic(id).Title}». Три волны за 80 секунд — прогресс мира не затронут", new Color("#c070ff"), "boss");
    }

    void UpdateRift(GEvent ev, float dt)
    {
        if (ev.State == 0) return;
        int reg = World.RegionOf((int)(ev.Pos.X / TS));
        trialT -= dt; ev.WaveT -= dt;
        if (ev.Wave < 3 && ev.WaveT <= 0)
        {
            ev.Wave++; ev.WaveT = 24f;
            for (int i = 0; i < 3 + ev.Wave; i++) EvEnemy(ev, ev.Pos + Vector2.FromAngle(rng.Randf() * Mathf.Tau) * rng.RandfRange(50, 90), PoolKind(reg), 1.1f);
            Sfx.Play("boss", -6, 1.2f); Toast($"Испытание: волна {ev.Wave}/3");
        }
        bool clear = !Enemies.Exists(e => e.Ev == ev);
        if (ev.Wave >= 3 && clear)
        {
            var r = new Random(ev.Seed); Inv[It.Coins] += 60 + r.Next(40); GrantTag(RollTag(r, 3, .5f), ev.Pos); Give(It.AncCrystal, 1, ev.Pos + new Vector2(0, -10));
            Announce("Испытание пройдено!", "Награда: монеты, редкая находка и древний кристалл. Временная реликвия исчезла", new Color("#ffe27a"), "ancient"); GiveXp(120); ev.Done = true;
        }
        else if (trialT <= 0 || ev.Pos.DistanceTo(PPos) > 30 * TS) { Announce("Испытание провалено", "Ничего не потеряно — разлом закрылся", new Color("#9aa6bf"), "error"); ev.Done = true; }
    }

    void EventsOnDeath() { foreach (var ev in Events) if (ev.Kind == "rift" && ev.State == 1) ev.Done = true; }

    // ───────────────────────── интерфейс ─────────────────────────
    bool EventIx(Ix ix)
    {
        foreach (var ev in Events)
        {
            float d = ev.Pos.DistanceTo(PPos);
            if (ev.Kind == "trader" && d < 44) { ix.Lines.Add("Странствующий торговец: редкие товары"); ix.Lines.Add("[E] Торговаться"); ix.E = () => OpenTrade(ev); return true; }
            if (ev.Kind == "rift" && ev.State == 0 && d < 44) { ix.Lines.Add("Разлом Тьмы: необязательное испытание"); ix.Lines.Add("[E] Войти в разлом (временная реликвия, 3 волны)"); ix.E = () => StartTrial(ev); return true; }
            if (ev.Kind == "traveler" && ev.State == 0 && d < 44) { ix.Lines.Add("Заблудший путник просит помощи"); ix.Lines.Add("[E] Проводить к освещённой земле"); ix.E = () => { ev.State = 1; Toast("Путник идёт за вами. Доведите его до света!"); if (!Enemies.Exists(e => e.Ev == ev)) for (int i = 0; i < 2; i++) EvEnemy(ev, ev.Pos + Vector2.FromAngle(rng.Randf() * Mathf.Tau) * 110, PoolKind(RegionAt(ev.Pos))); }; return true; }
            if (ev.Kind == "lamp" && d < 44) { ix.Lines.Add($"Осаждённый фонарь: прочность {(int)Mathf.Max(0, ev.V)}/60 · волна {ev.Wave}/4"); return true; }
            if (ev.Kind == "rift" && ev.State == 1) { }
        }
        return false;
    }

}
