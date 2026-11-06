using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Ставимый сундук (меню [B], вещь из запаса): [E] открывает окно переноса. В сундуке 20 слотов по 99 (те же стопки, что в инвентаре, монеты не кладутся).
/// Содержимое — часть мира (сохраняется вместе с ним). В кооперативе сундуком управляет хозяин мира: у гостя он только виден.</summary>
public partial class Game
{
    public const int ChestSlots = 20;
    public static readonly int[][] CostChest = { new[] { It.Wood, 20 }, new[] { It.Stone, 5 } };
    public readonly Dictionary<int, int[]> Chests = new();          // World.Key клетки → количество каждого ресурса
    public Vector2I ChestAt = new(-1, -1); public int ChestQty = 1; public string ChestMsg = "";

    public static int SlotsOfPublic(int[] a) => SlotsOf(a);
    static int SlotsOf(int[] a) { int n = 0; for (int i = 0; i < It.Count; i++) if (UsesSlot(i) && a[i] > 0) n += (a[i] + StackMax - 1) / StackMax; return n; }
    int[] ChestInv(Vector2I t) { int k = World.Key(t.X, t.Y); if (!Chests.TryGetValue(k, out var a)) Chests[k] = a = new int[It.Count]; return a; }
    public bool ChestEmpty(int x, int y) { if (!Chests.TryGetValue(World.Key(x, y), out var a)) return true; foreach (var v in a) if (v > 0) return false; return true; }
    public int ChestRoom(int[] a, int item) { int stackLeft = a[item] > 0 ? (StackMax - a[item] % StackMax) % StackMax : 0; return stackLeft + Math.Max(0, ChestSlots - SlotsOf(a)) * StackMax; }

    void BuildChest()
    {
        if (!Can(CostChest)) { Fail("Для сундука нужно: 20 дерева и 5 камня"); return; }
        var t = PlaceTarget(); if (t == null) return;
        Pay(CostChest); SetObj(t.Value.X, t.Value.Y, Tl.StorageChest);
        PlaceFx(t.Value, new Color("#b07a40"), 1.1f); Sfx.Play("build");
        GiveXp(10); Toast("Сундук готов"); Save();
    }

    public void OpenChest(int x, int y)
    {
        if (NetClient) { Fail("Сундуком управляет хозяин мира"); return; }
        CloseWindows(); Window = Win.Chest; ChestAt = new Vector2I(x, y); WinTab = 0; WinCur = 0; ChestMsg = ""; Sfx.Play("tick");
    }

    /// <summary>Закрывает окно, если сундук убрали или игрок ушёл.</summary>
    void UpdateChest()
    {
        if (Window != Win.Chest) return;
        if (W.Get(ChestAt.X, ChestAt.Y) != Tl.StorageChest || PPos.DistanceTo(TileCenter(ChestAt.X, ChestAt.Y)) > 72f) CloseWindows();
    }

    /// <summary>Строки окна: вкладка «Положить» — ресурсы инвентаря, «Забрать» — содержимое сундука.</summary>
    public List<int> ChestRows()
    {
        var l = new List<int>(); if (Window != Win.Chest) return l; var c = ChestInv(ChestAt);
        for (int i = 0; i < It.Count; i++) if (UsesSlot(i) && (WinTab == 0 ? Inv[i] > 0 : c[i] > 0)) l.Add(i);
        return l;
    }

    /// <summary>Переносит до n штук (n ≤ 0 — сколько влезет). Возвращает перенесённое число.</summary>
    public int ChestMove(bool deposit, int item, int n)
    {
        if (!UsesSlot(item) || Window != Win.Chest) return 0; var c = ChestInv(ChestAt);
        int have = deposit ? Inv[item] : c[item], room = deposit ? ChestRoom(c, item) : Room(item);
        int k = n <= 0 ? Math.Min(have, room) : Math.Min(n, Math.Min(have, room));
        if (k <= 0) { if (have > 0) { if (deposit) Fail("В сундуке нет места"); else FullMsg(); } return 0; }
        if (deposit) { Inv[item] -= k; c[item] += k; } else { c[item] -= k; Inv[item] += k; }
        Sfx.Play("pickup", -4, deposit ? 1f : 1.2f); ChestMsg = (deposit ? "Положено: " : "Взято: ") + It.Names[item] + " ×" + k; return k;
    }

    void ChestKey(Key k, bool shift)
    {
        if (k == Key.Left || k == Key.Right) { WinTab = 1 - WinTab; WinCur = 0; return; }
        if (k == Key.Equal || k == Key.Plus || k == Key.KpAdd) { ChestQtyStep(1); return; }
        if (k == Key.Minus || k == Key.KpSubtract) { ChestQtyStep(-1); return; }
        if (k != Key.Enter && k != Key.KpEnter) return;
        var rows = ChestRows(); if (WinCur < rows.Count) ChestMove(WinTab == 0, rows[WinCur], shift ? 0 : ChestQty);
    }
    void ChestQtyStep(int d) { int i = Array.IndexOf(TradeQtySteps, ChestQty); if (i < 0) i = 0; ChestQty = TradeQtySteps[Math.Clamp(i + d, 0, TradeQtySteps.Length - 1)]; Sfx.Play("tick", -6, 1.4f); }

    // ───────────────────────── сохранение ─────────────────────────
    void SaveChests(WorldSave d)
    {
        var ks = new List<int>(); var vs = new List<int[]>();
        foreach (var kv in Chests) { bool any = false; foreach (var v in kv.Value) if (v > 0) { any = true; break; } if (any) { ks.Add(kv.Key); vs.Add((int[])kv.Value.Clone()); } }
        d.ChestK = ks.ToArray(); d.ChestV = vs.ToArray();
    }

    void LoadChests(WorldSave d)
    {
        Chests.Clear(); if (d.ChestK == null || d.ChestV == null) return;
        for (int i = 0; i < d.ChestK.Length && i < d.ChestV.Length; i++) { var a = new int[It.Count]; if (d.ChestV[i] != null) Array.Copy(d.ChestV[i], a, Math.Min(a.Length, d.ChestV[i].Length)); Chests[d.ChestK[i]] = a; }
    }
}
