using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

public enum Win { None, Equip, Journal, Bench, Trade, Story, Rift, Inventory, Talk, Chest }

public sealed class BuildEntry { public string Key, Name, Cost, Lock, Hint = ""; public bool Unlocked, Affordable; public Action Do; public int Cat, Tile = -1, Stock; public int[][] CostArr, Raw; }

/// <summary>Состояние и ввод окон: экипировка [C], журнал [J], рабочий стол (у постройки, [E]), торговля.</summary>
public partial class Game
{
    public Win Window; public int WinTab, WinCur;
    public bool TradeOpen => Window == Win.Trade;
    public static readonly string[] EquipTabs = { "Оружие", "Реликвии", "Создание", "Фонарь" };

    public void CloseWindows() { ReturnCraftIngredients(); Window = Win.None; TechOpen = false; TechAtTable = false; MapOpen = false; TravelMode = false; }

    void OpenWin(Win w)
    {
        if (Window == w) { CloseWindows(); return; }
        CloseWindows(); Placing = null; Window = w; WinTab = 0; WinCur = 0; Sfx.Play("tick"); PokeHover();
    }

    // ───────────────────────── списки окон ─────────────────────────
    public const int CraftCols = 5;
    public List<WeaponDef> CraftableWeapons() { var l = new List<WeaponDef>(); foreach (var w in Db.Weapons) if (w.Cost != null) l.Add(w); return l; }

    /// <summary>Все постройки с категорией и иконкой тайла (для Рабочего стола и установки). Cat: 1 — базовое, 2 — производство, 3 — декор.</summary>
    public List<BuildEntry> BuildEntries()
    {
        var l = new List<BuildEntry>();
        void E(string key, int cat, int tile, string n, int[][] cost, bool unl, string lk, Action a, string hint = "", int[][] raw = null) => l.Add(new BuildEntry { Key = key, Stock = StockOf(key), Raw = raw ?? cost, Cat = cat, Tile = tile, Name = n, Hint = hint, CostArr = cost, Cost = cost == null ? "" : CostStr(cost), Unlocked = unl, Lock = lk, Affordable = cost == null || Can(cost), Do = a });
        E("workbench", 1, Art.Workbench, "Рабочий стол", CostWorkbench, true, "", BuildWorkbench, "Создание построек: ресурсы берутся из сумки");
        E("furnace", 1, Art.Furnace, "Печь", CostFurnace, true, "", BuildFurnace, "Плавит руду в слитки, улучшает кирку");
        E("chest", 1, Art.StorageChest, "Сундук", CostChest, true, "", BuildChest, "Хранит вещи: 20 слотов по 99");
        E("lamp0", 1, Art.Lantern, "Фонарь", CostLantern, true, "", () => BuildLamp(0), "Постоянный свет на клетке");
        E("lamp1", 1, Art.CombatLantern, "Боевой фонарь", LampCost[1], Has(Tech.CombatLamp), "навык «Боевой фонарь» (Стол исследований)", () => BuildLamp(1), "Жжёт врагов рядом");
        E("lamp2", 1, Art.CrystalLantern, "Кристальный фонарь", LampCost[2], Has(Tech.Optics), "навык «Оптика» (Стол исследований)", () => BuildLamp(2), "Пускает луч света");
        E("mirror", 1, Art.Mirror1, "Зеркало", MirrorCost, Has(Tech.Optics), "навык «Оптика» (Стол исследований)", BuildMirror, "Поворачивает луч");
        E("amp", 1, Art.AmpCrystal, "Светокристалл", AmpCost, BpDone.Contains("bp_amp"), "чертёж «Светокристаллы»", BuildAmp, "Усиливает соседние фонари");
        E("barrier", 1, Art.BarrierPost, "Столб-барьер", BarrierCost, BpDone.Contains("bp_barrier"), "чертёж «Световые барьеры»", BuildBarrier, "Два столба держат теней");
        for (int bk = 0; bk < BCount; bk++)
        {
            if (!BActive(bk)) continue;
            int k = bk; bool has = HasBuilding(BTypes[k]);
            E("bld" + k, k == ResearchBk ? 1 : 2, AtlasX(BTypes[k], 1), BNames[k] + (has ? " ✓" : ""), BCost(k, 1), BUnlocked(k), BLock(k), () => BuildBuilding(k), BDesc[k][0], BCost(k, 1));
        }
        int[] dt = { Art.Statue, Art.Fountain, Art.Banner };
        for (int dk = 0; dk < 3; dk++) { int kk = dk; E("decor" + kk, 3, dt[dk], DecorNames[kk], DecorCostArr(kk), BeaconLevel >= 2, "улучшите Центральный маяк", () => BuildDecor(kk), "Украшение: бонус к производству"); }
        return l;
    }

    /// <summary>Здание деревни существует в одном экземпляре: когда оно уже стоит (и нет лишнего в запасе), из списка Рабочего стола оно пропадает.</summary>
    bool BuiltOnce(BuildEntry e) => e.Key.StartsWith("bld") && int.TryParse(e.Key.Substring(3), out var k) && k >= 0 && k < BCount && HasBuilding(BTypes[k]) && StockOf(e.Key) == 0;

    public List<int> SellList()
    {
        var l = new List<int>(); for (int i = 0; i < It.Count; i++) if (Tradable(i) && Inv[i] > 0) l.Add(i); return l;
    }

    public int WinLen()
    {
        switch (Window)
        {
            case Win.Equip: return WinTab == 0 ? ToolList().Count + Owned.Count : WinTab == 1 ? OwnedRelics.Count : WinTab == 2 ? CraftRows().Count : 1;
            case Win.Journal: return WinTab == 0 ? Db.Bps.Length : WinTab == 2 ? Goals.Count : Found.Count;
            case Win.Bench: return BenchEntries().Count;
            case Win.Inventory: return InventorySlots().Count;
            case Win.Trade: return TradeTab == 0 ? CurOffers.Count : SellList().Count;
            case Win.Chest: return ChestSlots;
            case Win.Talk: return 2;
        }
        return 0;
    }

    /// <summary>Режим размещения: постройка из запаса (сумка или горячая панель) следует за курсором; ЛКМ строит, ПКМ или Esc отменяют.</summary>
    public BuildEntry Placing;
    void PlaceNow() { if (Placing != null) PlaceEntry(Placing); }

    /// <summary>Предметы, которые видны в инвентаре: только с количеством больше нуля (данные нулевых ресурсов остаются в игре).</summary>
    /// <summary>Ресурсы в инвентаре; у склада — ещё и те, что лежат только на складе (их можно взять через меню).</summary>
    public List<int> InvVisible() { var l = new List<int>(); bool wh = NearWarehouse; for (int i = 0; i < It.Count; i++) if (Inv[i] > 0 || (wh && Stock[i] > 0 && i != It.Coins)) l.Add(i); return l; }

    // ───────────────────────── ввод ─────────────────────────
    bool WindowKey(Key k, bool shift)
    {
        if (k == Key.E && (Window is Win.Chest or Win.Trade or Win.Equip or Win.Bench || Window == Win.Rift && RiftState == 0)) { CloseWindows(); return true; }      // окно, открытое клавишей E, закрывается ею же
        if (Window is Win.Chest or Win.Inventory)
        {
            if (k is Key.Escape or Key.Tab || k == Key.I && Window == Win.Inventory) CloseWindows();
            else if (Window == Win.Inventory && k >= Key.Key0 && k <= Key.Key9) Digit((int)k - (int)Key.Key0);
            return true;
        }
        if (Window == Win.Equip && WinTab == 2)
        {
            // сетка рецептов: стрелки двигают выбор, Tab переключает ручной крафт и станцию
            int wl = WinLen(), cc = CraftCols;
            switch (k)
            {
                case Key.Left: if (WinCur > 0) { WinCur--; return true; } break;                      // с краёв сетки стрелки переключают вкладки, как раньше
                case Key.Right: if (WinCur < wl - 1) { WinCur++; return true; } break;
                case Key.Up: WinCur = Math.Max(0, WinCur - cc); return true;
                case Key.Down: WinCur = Math.Min(Math.Max(0, wl - 1), WinCur + cc); return true;
                case Key.Tab: { var g = CraftGroups(); EqCraftMode(g[(g.IndexOf(CraftGroup()) + 1) % g.Count]); return true; }
            }
        }
        if (Window == Win.Bench) return BenchKey(k, shift);
        if (Window == Win.Rift)
        {
            if (k == Key.Escape) { if (RiftState == 0) CloseWindows(); return true; }
            if (k == Key.Up) { WinCur = Math.Max(0, WinCur - 1); return true; }
            if (k == Key.Down) { WinCur = Math.Min(Math.Max(0, RiftWinLen - 1), WinCur + 1); return true; }
            return RiftKey(k);
        }
        switch (k)
        {
            case Key.Escape: CloseWindows(); return true;
            case Key.C: OpenWin(Win.Equip); return true;
            case Key.J: OpenWin(Win.Journal); return true;
            case Key.I: OpenWin(Win.Inventory); return true;
            case Key.M: CloseWindows(); MapOpen = true; return true;
            case Key.F5: Save(); Toast("Игра сохранена"); return true;
            case Key.Up: WinCur = Math.Max(0, WinCur - 1); return true;
            case Key.Down: WinCur = Math.Min(Math.Max(0, WinLen() - 1), WinCur + 1); return true;
        }
        int len = WinLen(); if (WinCur >= len) WinCur = Math.Max(0, len - 1);
        switch (Window)
        {
            case Win.Equip: EquipKey(k, shift); break;
            case Win.Journal: if (k == Key.Left) { WinTab = (WinTab + 2) % 3; WinCur = 0; } else if (k == Key.Right) { WinTab = (WinTab + 1) % 3; WinCur = 0; } break;
            case Win.Trade: TradeKey(k, shift); break;
            case Win.Talk: TalkKey(k); break;
            case Win.Story: if (k == Key.E || k == Key.Enter || k == Key.KpEnter || k == Key.Space) { if (!hud.StorySkip()) CloseWindows(); } break;
        }
        return true;
    }

    /// <summary>Мышь в окнах: наведение выбирает строку, ЛКМ — действие (Enter), ПКМ — закрыть (в лавке при продаже — ×10), колесо — прокрутка, клик по вкладке — переход.</summary>
    bool WinMouse(InputEventMouse e)
    {
        if (hud == null) return false;
        var p = e.Position / UiScaleVp; int row = hud.RowHit(p), tab = hud.TabHit(p);
        if (e is InputEventMouseMotion) { if (row >= 0 && row != WinCur) { WinCur = row; Sfx.Play("tick", -6, 1.4f); } return false; }
        if (e is not InputEventMouseButton mb || !mb.Pressed) return false;
        switch (mb.ButtonIndex)
        {
            case MouseButton.WheelUp: WindowKey(Key.Up, false); return true;
            case MouseButton.WheelDown: WindowKey(Key.Down, false); return true;
            case MouseButton.Left:
                if (tab >= 0 && Window != Win.Story)
                {
                    for (int n = 0; n < 5 && (Window == Win.Trade ? TradeTab : WinTab) != tab; n++) WindowKey(Key.Right, false);
                    return true;
                }
                if (Window == Win.Story) { WindowKey(Key.Enter, false); return true; }
                if (row >= 0) { WinCur = row; WindowKey(Key.Enter, false); }
                return true;
            case MouseButton.Right:
                if (Window == Win.Trade && TradeTab == 1 && row >= 0) { WinCur = row; WindowKey(Key.Enter, true); }
                else WindowKey(Key.Escape, false);
                return true;
        }
        return false;
    }

    /// <summary>Мышь в окне исследований: ПКМ закрывает окно; вкладки, карточки и кнопка — элементы UpgradeWindowUI.</summary>
    bool TechMouse(InputEventMouse e)
    {
        if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Right) { CloseWindows(); return true; }
        return false;
    }

    void EquipKey(Key k, bool shift = false)
    {
        if (StationOpened && (k == Key.Left || k == Key.Right)) return;
        if (k == Key.Left) { WinTab = (WinTab + 3) % 4; if (WinTab == 2 && !CraftTabAvail) WinTab = 1; WinCur = 0; return; }
        if (k == Key.Right) { WinTab = (WinTab + 1) % 4; if (WinTab == 2 && !CraftTabAvail) WinTab = 3; WinCur = 0; return; }
        bool enter = k == Key.Enter || k == Key.KpEnter;
        switch (WinTab)
        {
            case 0:
                if (enter) HoldEquipRow(WinCur);
                else if (k >= Key.Key1 && k <= Key.Key7) InstallMod((int)k - (int)Key.Key1);
                else if (k == Key.Backspace || k == Key.Delete) { var ow = CurOw; if (ow != null && ow.Mods.Count > 0) RemoveMod(ow.Mods.Count - 1); }
                break;
            case 1: if (enter && WinCur < OwnedRelics.Count) ToggleRelic(OwnedRelics[WinCur]); break;
            case 2:
                {
                    var rows = CraftRows(); if (WinCur >= rows.Count) break; var rec = rows[WinCur].r;
                    if (k == Key.Q) { CraftQty = (CraftQty + 1) % QtySteps.Length; Sfx.Play("tick", -6, 1.3f); }
                    else if (enter) CraftRecipe(rec);
                    break;
                }
            case 3: if (enter) UpgradeHeroLamp(); break;
        }
    }

    void TradeKey(Key k, bool shift)
    {
        if (k == Key.Left || k == Key.Right) { TradeTab = 1 - TradeTab; WinCur = 0; return; }
        if (k == Key.Equal || k == Key.Plus || k == Key.KpAdd) { TradeQtyStep(1); return; }
        if (k == Key.Minus || k == Key.KpSubtract) { TradeQtyStep(-1); return; }
        if (k != Key.Enter && k != Key.KpEnter) return;
        if (TradeTab == 0) { if (WinCur < CurOffers.Count) BuyOffer(CurOffers[WinCur], shift ? 0 : TradeQty); }
        else { var l = SellList(); if (WinCur < l.Count) SellItem(l[WinCur], shift ? Inv[l[WinCur]] : TradeQty); }
    }
}
