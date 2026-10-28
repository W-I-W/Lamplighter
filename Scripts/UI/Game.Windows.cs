using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

public enum Win { None, Equip, Journal, Prod, Build, Trade, Story, Rift, Inventory }

public sealed class BuildEntry { public string Key, Name, Cost, Lock, Hint = ""; public bool Unlocked, Affordable; public Action Do; public int Cat, Tile = -1, Stock; public int[][] CostArr, Raw; }

/// <summary>Состояние и ввод окон: экипировка [C], журнал [J], производство [P], постройки [B], торговля.</summary>
public partial class Game
{
    public Win Window; public int WinTab, WinCur;
    public bool TradeOpen => Window == Win.Trade;
    public static readonly string[] EquipTabs = { "Оружие", "Реликвии", "Создание", "Фонарь" };

    public void CloseWindows() { Window = Win.None; TechOpen = false; MapOpen = false; }

    void OpenWin(Win w)
    {
        if (Window == w) { CloseWindows(); return; }
        CloseWindows(); Placing = null; Window = w; WinTab = 0; WinCur = 0; Sfx.Play("tick");
    }

    // ───────────────────────── списки окон ─────────────────────────
    public List<WeaponDef> CraftableWeapons() { var l = new List<WeaponDef>(); foreach (var w in Db.Weapons) if (w.Cost != null) l.Add(w); return l; }
    public List<Building> ProdList() => Buildings.FindAll(b => IsProducer(b.Type));

    public static readonly string[] BuildCatNames = { "Всё", "Базовое", "Производство", "Декор" };

    /// <summary>Все постройки с категорией и иконкой тайла (для меню строительства). Cat: 1 — базовое, 2 — производство, 3 — декор.</summary>
    public List<BuildEntry> BuildEntries()
    {
        var l = new List<BuildEntry>();
        void E(string key, int cat, int tile, string n, int[][] cost, bool unl, string lk, Action a, string hint = "", int[][] raw = null) => l.Add(new BuildEntry { Key = key, Stock = StockOf(key), Raw = raw ?? cost, Cat = cat, Tile = tile, Name = n, Hint = hint, CostArr = cost, Cost = cost == null ? "" : CostStr(cost), Unlocked = unl, Lock = lk, Affordable = cost == null || Can(cost), Do = a });
        E("furnace", 1, Art.Furnace, "Печь", CostFurnace, true, "", BuildFurnace, "Плавит руду в слитки, улучшает кирку");
        E("lamp0", 1, Art.Lantern, "Фонарь", CostLantern, true, "", () => BuildLamp(0), "Постоянный свет на клетке");
        E("lamp1", 1, Art.CombatLantern, "Боевой фонарь", LampCost[1], Has(Tech.CombatLamp), "улучшение [T] «Боевой фонарь»", () => BuildLamp(1), "Жжёт врагов рядом");
        E("lamp2", 1, Art.CrystalLantern, "Кристальный фонарь", LampCost[2], Has(Tech.Optics), "улучшение [T] «Оптика»", () => BuildLamp(2), "Пускает луч света");
        E("mirror", 1, Art.Mirror1, "Зеркало", MirrorCost, Has(Tech.Optics), "улучшение [T] «Оптика»", BuildMirror, "Поворачивает луч");
        E("amp", 1, Art.AmpCrystal, "Светокристалл", AmpCost, BpDone.Contains("bp_amp"), "чертёж «Светокристаллы»", BuildAmp, "Усиливает соседние фонари");
        E("barrier", 1, Art.BarrierPost, "Столб-барьер", BarrierCost, BpDone.Contains("bp_barrier"), "чертёж «Световые барьеры»", BuildBarrier, "Два столба держат теней");
        for (int bk = 0; bk < BCount; bk++)
        {
            int k = bk; bool has = HasBuilding(BTypes[k]);
            E("bld" + k, 2, AtlasX(BTypes[k], 1), BNames[k] + (has ? " ✓" : ""), BCost(k, 1), BUnlocked(k), BLock(k), () => BuildBuilding(k), BDesc[k][0], BCost(k, 1));
        }
        int[] dt = { Art.Statue, Art.Fountain, Art.Banner };
        for (int dk = 0; dk < 3; dk++) { int kk = dk; E("decor" + kk, 3, dt[dk], DecorNames[kk], DecorCostArr(kk), BeaconLevel >= 2, "улучшите Центральный маяк", () => BuildDecor(kk), "Украшение: бонус к производству"); }
        return l;
    }

    /// <summary>Постройки выбранной категории (BuildCat; 0 — все).</summary>
    public List<BuildEntry> BuildEntriesFiltered() { var all = BuildEntries(); if (BuildCat == 0) return all; return all.FindAll(e => e.Cat == BuildCat); }

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
            case Win.Prod: return ProdList().Count;
            case Win.Build: return BuildEntriesFiltered().Count;
            case Win.Inventory: return InvVisible().Count;
            case Win.Trade: return TradeTab == 0 ? CurOffers.Count : SellList().Count;
        }
        return 0;
    }

    /// <summary>Режим размещения: выбранная в меню [B] постройка следует за курсором; ЛКМ строит, ПКМ или Esc отменяют.</summary>
    public BuildEntry Placing;
    void PlaceNow() { if (Placing != null) PlaceEntry(Placing); }

    /// <summary>Предметы, которые видны в инвентаре: только с количеством больше нуля (данные нулевых ресурсов остаются в игре).</summary>
    public List<int> InvVisible() { var l = new List<int>(); for (int i = 0; i < It.Count; i++) if (Inv[i] > 0) l.Add(i); return l; }

    // ───────────────────────── ввод ─────────────────────────
    bool WindowKey(Key k, bool shift)
    {
        if (Window == Win.Build || Window == Win.Inventory)
        {
            int cols = Window == Win.Build ? 5 : 7, wlen = WinLen();
            switch (k)
            {
                case Key.Left: WinCur = Math.Max(0, WinCur - 1); return true;
                case Key.Right: WinCur = Math.Min(Math.Max(0, wlen - 1), WinCur + 1); return true;
                case Key.Up: WinCur = Math.Max(0, WinCur - cols); return true;
                case Key.Down: WinCur = Math.Min(Math.Max(0, wlen - 1), WinCur + cols); return true;
                case Key.Q: if (Window == Win.Build) { var ql = BuildEntriesFiltered(); if (WinCur < ql.Count) CraftBuild(ql[WinCur]); return true; } break;
                case Key.Tab: if (Window == Win.Build) { BuildCat = (BuildCat + 1) % 4; WinCur = 0; Sfx.Play("tick", -6, 1.3f); } return true;
                case Key.Enter: case Key.KpEnter:
                    if (Window == Win.Build) { var l = BuildEntriesFiltered(); if (WinCur < l.Count) { var e = l[WinCur]; if (!e.Unlocked) Fail("Недоступно: " + e.Lock); else if (e.Stock > 0) { Placing = e; Window = Win.None; Toast("[ЛКМ] — поставить · [ПКМ] — отмена"); } else CraftBuild(e); } }
                    return true;
                case Key.Escape: CloseWindows(); return true;
                case Key.B: if (Window == Win.Build) { CloseWindows(); return true; } break;
                case Key.I: if (Window == Win.Inventory) { CloseWindows(); return true; } break;
            }
        }
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
            case Key.P: OpenWin(Win.Prod); return true;
            case Key.B: OpenWin(Win.Build); return true;
            case Key.I: OpenWin(Win.Inventory); return true;
            case Key.T: OpenTech(true); return true;
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
            case Win.Prod: ProdKey(k); break;
            case Win.Build: if (k == Key.Enter || k == Key.KpEnter) { var l = BuildEntriesFiltered(); if (WinCur < l.Count) { var e = l[WinCur]; if (!e.Unlocked) Fail("Недоступно: " + e.Lock); else { var tmp = e; Window = Win.None; tmp.Do(); } } } break;
            case Win.Trade: TradeKey(k, shift); break;
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

    /// <summary>Мышь в окне улучшений [T]: ПКМ закрывает окно; вкладки, карточки и кнопка — элементы UpgradeWindowUI.</summary>
    bool TechMouse(InputEventMouse e)
    {
        if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Right) { CloseWindows(); return true; }
        return false;
    }

    void EquipKey(Key k, bool shift = false)
    {
        if (k == Key.Left) { WinTab = (WinTab + 3) % 4; WinCur = 0; return; }
        if (k == Key.Right) { WinTab = (WinTab + 1) % 4; WinCur = 0; return; }
        bool enter = k == Key.Enter || k == Key.KpEnter;
        switch (WinTab)
        {
            case 0:
                if (enter) HoldEquipRow(WinCur);
                else if (shift && k >= Key.Key1 && k <= Key.Key3) { var r = EquipRef(WinCur); if (r != null) HotAssign((int)k - (int)Key.Key1, r); }      // Shift+1..8 — назначить в хотбар
                else if (k == Key.X) { var r = EquipRef(WinCur); int sl = r == null ? -1 : HotSlotOf(r); if (sl >= 0) HotClear(sl); }                  // X — убрать из хотбара
                else if (k >= Key.Key1 && k <= Key.Key7) InstallMod((int)k - (int)Key.Key1);
                else if (k == Key.Backspace || k == Key.Delete) { var ow = CurOw; if (ow != null && ow.Mods.Count > 0) RemoveMod(ow.Mods.Count - 1); }
                break;
            case 1: if (enter && WinCur < OwnedRelics.Count) ToggleRelic(OwnedRelics[WinCur]); break;
            case 2:
                {
                    var rows = CraftRows(); if (WinCur >= rows.Count) break; var (grp, rec) = rows[WinCur];
                    if (k == Key.Q) { CraftQty = (CraftQty + 1) % QtySteps.Length; Sfx.Play("tick", -6, 1.3f); }
                    else if (enter) { if (rec == null) { if (!CraftFold.Remove(grp)) CraftFold.Add(grp); Sfx.Play("tick", -6, 1.2f); } else CraftRecipe(rec); }
                    break;
                }
            case 3: if (enter) UpgradeHeroLamp(); break;
        }
    }

    void ProdKey(Key k)
    {
        var l = ProdList(); if (WinCur >= l.Count) return; var b = l[WinCur]; int bk = BIndex(b.Type);
        if (k == Key.Left || k == Key.Right)
        {
            var modes = BModes[bk]; if (modes == null || modes.Length < 2) return;
            b.Mode = (b.Mode + (k == Key.Right ? 1 : modes.Length - 1)) % modes.Length; b.Prog = 0; Sfx.Play("tick");
        }
        else if (k == Key.Key1 || k == Key.Key2 || k == Key.Key3) { b.Prio = (int)k - (int)Key.Key1; netT = 0; Sfx.Play("tick"); }
        else if (k == Key.Enter || k == Key.KpEnter) { b.Off = !b.Off; Sfx.Play("tick"); }
        else if (k == Key.U) UpgradeBuilding(b);
    }

    void TradeKey(Key k, bool shift)
    {
        if (k == Key.Left || k == Key.Right) { TradeTab = 1 - TradeTab; WinCur = 0; return; }
        if (k != Key.Enter && k != Key.KpEnter) return;
        if (TradeTab == 0) { if (WinCur < CurOffers.Count) BuyOffer(CurOffers[WinCur]); }
        else { var l = SellList(); if (WinCur < l.Count) SellItem(l[WinCur], shift ? 10 : 1); }
    }
}
