using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Слоты инвентаря: стопка — до 99 штук одного ресурса, в начале игры 10 слотов, дальше слоты добавляет навык «Рюкзак» (+2 за уровень).
/// Ресурс занимает ceil(количество / 99) слотов; монеты слотов не занимают. Хранилище остаётся счётчиками Inv[]: лимит проверяется там, где предметы получают (добыча, подбор, покупка, плавка, создание, склад).
/// Награды и возврат вещей из могилы лимит не режет: слотов может стать больше, чем есть, тогда новые предметы не подбираются, пока место не освободится.</summary>
public partial class Game
{
    public const int StackMax = 99, SlotsBase = 10, SlotsPerPackLv = 2;
    float fullMsgT = -9f;

    /// <summary>Сколько слотов сейчас у героя.</summary>
    public int SlotCap => SlotsBase + Mathf.RoundToInt(E(Tech.Pack));
    public static bool UsesSlot(int item) => item != It.Coins;
    public bool TestSlots;                                      // автотесты: лимит слотов включён только там, где он проверяется (в остальных проверках инвентарь забит произвольно)

    public int SlotsUsed() { int n = 0; for (int i = 0; i < It.Count; i++) if (UsesSlot(i) && Inv[i] > 0) n += (Inv[i] + StackMax - 1) / StackMax; return n; }

    /// <summary>Сколько штук ресурса ещё поместится: остаток неполной стопки плюс свободные слоты.</summary>
    public int Room(int item)
    {
        if (!UsesSlot(item) || (testing && !TestSlots)) return int.MaxValue;
        int stackLeft = Inv[item] > 0 ? (StackMax - Inv[item] % StackMax) % StackMax : 0;
        return stackLeft + Math.Max(0, SlotCap - SlotsUsed()) * StackMax;
    }

    void FullMsg() { if (Time - fullMsgT > 2.5f) { fullMsgT = Time; Fail("Инвентарь полон: освободите слот или улучшите «Рюкзак» [T]"); } }

    /// <summary>Добавляет ресурс в инвентарь с учётом слотов. Лишнее падает на землю у <paramref name="at"/> (если там можно выбросить), иначе пропадает. Возвращает, сколько легло в инвентарь.</summary>
    public int AddInv(int item, int n, Vector2 at)
    {
        if (n <= 0) return 0; int room = Room(item), put = Math.Min(n, room);
        if (put > 0) Inv[item] += put;
        if (put < n) { FullMsg(); SpawnLoot(item, n - put, at); }
        return put;
    }

    /// <summary>Стопки для окна инвентаря: каждая — (ресурс, количество ≤ 99). Ресурс, который лежит только на складе, показывается одной пустой стопкой.</summary>
    public List<(int item, int n)> InvStacks()
    {
        var l = new List<(int, int)>();
        foreach (int i in InvVisible()) { int n = Inv[i]; if (n <= 0) { l.Add((i, 0)); continue; } for (int k = 0; k * StackMax < n; k++) l.Add((i, Math.Min(StackMax, n - k * StackMax))); }
        return l;
    }
}
