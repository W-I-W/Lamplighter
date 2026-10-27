using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Этап 6: подсказки по ходу игры, быстрый путь между фонарями, материалы со склада при крафте, возрождение у ближайшего фонаря,
/// маркеры причин остановки производства и отклик добычи.</summary>
public partial class Game
{
    // ───────────────────────── подсказки ─────────────────────────
    public HashSet<string> HintsShown = new();
    float hintT = 3f, lastHintAt = -99;

    void Hint(string id, string text)
    {
        if (HintsShown.Contains(id) || Time - lastHintAt < 6f) return;
        HintsShown.Add(id); lastHintAt = Time; Toast(text); Sfx.Play("tick", -3, 1.5f);
    }

    /// <summary>Контекстные подсказки: каждая показывается один раз и только когда она применима.</summary>
    void UpdateHints(float dt)
    {
        hintT -= dt; if (hintT > 0 || testing && !Diag.Enabled && OS.GetEnvironment("LAMP_HINTS") == "") return; hintT = 1.2f;
        bool early = Level <= 6 && BeaconLevel <= 1;
        if (early && PlaySecs < 120) Hint("move", "WASD — движение · ЛКМ или Пробел — бить врагов и добывать деревья и камни");
        var a1 = W.Ancients.Count > 1 ? W.Ancients[1] : null;
        if (early && !IsLit(PPos) && a1 != null) Hint("dark", "Вне света появляются тени. Бейте их: они роняют осколки света для древних фонарей");
        foreach (var e in Enemies) if (!e.IsBoss && e.Pos.DistanceTo(PPos) < 130 && early) { Hint("enemy", "Враг рядом! Бейте ЛКМ; держитесь у света — там тени слабеют и горят"); break; }
        if (a1 != null && !a1.Lit && Inv[It.Shard] >= a1.Cost && a1.Cost > 0) Hint("shards", $"Осколков хватает ({a1.Cost})! Идите к ближайшему древнему фонарю (метка у края карты) и нажмите E");
        if (LitCount >= 1) Hint("lit1", "Территория освобождена: в свете можно строить [B]. Дальше — другие фонари, находки и спасение жителей");
        if (HasFurnace && Tier < 1 && Inv[It.Copper] >= 5) Hint("pick", "Меди достаточно: нажмите 3 у печи — кирка станет лучше и возьмёт новые руды");
        if (!HasFurnace && Inv[It.Stone] >= 10 && Inv[It.Wood] >= 5) Hint("furnace", "Хватает на печь: нажмите 1 на свободной клетке рядом");
        if (Inv[It.Core] > 0 && BeaconLevel < 6) Hint("core", "Ядро света! Отнесите его к Центральному маяку в центре карты и нажмите E");
        if (Level >= 3 && LitCount >= 1) Hint("goal", "Большая цель: ядра света у стражей земель — они восстанавливают Центральный маяк. Карта — M");
        if (Buildings.Count >= 3 && HasBuilding(Tl.Warehouse)) Hint("auto", "Производство идёт само: смотрите состояние на зданиях, управление — P. Рядом со складом материалы берутся прямо из него");
        if (TotalLit >= 3 && BossDown(1) == false) Hint("boss1", "Страж леса ждёт у особого фонаря. Готовьте оружие [C] и реликвии");
        if (BossDown(1)) Hint("travel", "Быстрый путь: у зажжённого фонаря нажмите U — перенос к другому зажжённому фонарю уже освобождённых земель");
    }

    // ───────────────────────── быстрый путь ─────────────────────────
    float travelCd;

    bool FastOk(Ancient a)
    {
        if (!a.Lit || (a.Boss && !BossDown(a.BossId))) return false;
        if (a == W.Ancients[0]) return true;
        return BossDown(a.Region + 1);
    }

    List<Ancient> TravelList() { var l = new List<Ancient>(); foreach (var a in W.Ancients) if (FastOk(a)) l.Add(a); return l; }

    void FastTravelIx(Ancient from, Ix ix)
    {
        var l = TravelList(); if (!l.Contains(from) || l.Count < 2) return;
        int i = l.IndexOf(from); var to = l[(i + 1) % l.Count];
        string nm = to == W.Ancients[0] ? "Центральный маяк" : to.Boss ? "фонарь стража" : $"{RegionNames[to.Region]}, фонарь";
        ix.Lines.Add($"[U] Быстрый путь: {nm} ({l.Count} доступно)");
        ix.U = () => FastTravel(to);
    }

    void FastTravel(Ancient to)
    {
        foreach (var e in Enemies) if (!e.IsBoss && e.Pos.DistanceTo(PPos) < 120) { Fail("Враги слишком близко для переноса"); return; }
        if (Boss != null || RiftOn) { Fail("Сейчас перенос невозможен"); return; }
        if (travelCd > 0) { Fail("Свет ещё остывает после переноса"); return; }
        travelCd = 4f; AddRing(PPos, 50, .6f, new Color("#ffe27a")); Burst(PPos, new Color("#fff2b0"), 14, 90);
        PPos = to.Pos + new Vector2(0, 30); cam.ResetSmoothing(); UpdateChunks(0, true); Flash = .35f; Sfx.Play("flare", -2, 1.2f);
        AddRing(PPos, 60, .8f, new Color("#ffe27a")); Diag.Count("fast_travel");
    }

    // ───────────────────────── материалы склада при крафте ─────────────────────────
    public bool NearWarehouse { get { foreach (var b in Buildings) if (b.Type == Tl.Warehouse && b.Pos.DistanceTo(PPos) < 26 * TS) return true; return false; } }

    public int HaveAny(int item) => Inv[item] + (NearWarehouse ? Stock[item] : 0);

    bool CanPool(int[][] cost) { bool near = NearWarehouse; foreach (var c in cost) if (Inv[c[0]] + (near ? Stock[c[0]] : 0) < c[1]) return false; return true; }

    void PayPool(int[][] cost)
    {
        bool near = NearWarehouse;
        foreach (var c in cost)
        {
            int need = c[1], fromInv = Math.Min(Inv[c[0]], need); Inv[c[0]] -= fromInv; need -= fromInv;
            if (need > 0 && near) Stock[c[0]] = Math.Max(0, Stock[c[0]] - need);
        }
    }

    // ───────────────────────── возрождение ─────────────────────────
    Vector2 RespawnPos()
    {
        Ancient best = W.Ancients[0]; float bd = float.MaxValue;
        foreach (var a in W.Ancients)
        {
            if (!a.Lit || (a.Boss && !BossDown(a.BossId))) continue;
            float d = a.Pos.DistanceTo(PPos); if (d < bd) { bd = d; best = a; }
        }
        return best.Pos + new Vector2(0, 28);
    }

    // ───────────────────────── маркеры производства ─────────────────────────
    static string StatusText(int s) => s switch { 1 => "нет сырья", 2 => "склад полон", 3 => "нет связи", 4 => "пауза", 5 => "режим не подходит", _ => "" };

    // ───────────────────────── отклик добычи ─────────────────────────
    float hitFlash;
    static bool IsRareItem(int it) => it == It.Aether || it == It.Crystal || it == It.Obsidian || it == It.Ember || it == It.AncCrystal || it == It.Core || it == It.Relic || it == It.Frost || it == It.Lens || it == It.RiftShard;

    void RareGiveFx(int item, Vector2 at) => Fx(item == It.Core || item == It.AncCrystal ? "rare_pickup_big" : "rare_pickup", at, It.Col[item], item == It.Core ? 1f : 1f);

    void UpdateDesign(float dt)
    {
        travelCd = Mathf.Max(0, travelCd - dt); hitFlash = Mathf.Max(0, hitFlash - dt * 6f);
        UpdateHints(dt);
        if (!IsLit(PPos)) Diag.Add("dark_secs", dt); else Diag.Add("lit_secs", dt);
        Diag.Update(dt, this);
    }
}
