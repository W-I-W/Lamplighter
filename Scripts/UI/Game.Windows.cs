using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

public enum Win { None, Equip, Journal, Prod, Build, Trade, Story, Rift, Inventory }

public sealed class BuildEntry { public string Name, Cost, Lock, Hint = ""; public bool Unlocked, Affordable; public Action Do; public int Cat, Tile = -1; public int[][] CostArr; }

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
        CloseWindows(); Window = w; WinTab = 0; WinCur = 0; Sfx.Play("tick");
    }

    // ───────────────────────── списки окон ─────────────────────────
    public List<WeaponDef> CraftableWeapons() { var l = new List<WeaponDef>(); foreach (var w in Db.Weapons) if (w.Cost != null) l.Add(w); return l; }
    public List<Building> ProdList() => Buildings.FindAll(b => IsProducer(b.Type));

    public static readonly string[] BuildCatNames = { "Всё", "Базовое", "Производство", "Декор" };

    /// <summary>Все постройки с категорией и иконкой тайла (для меню строительства). Cat: 1 — базовое, 2 — производство, 3 — декор.</summary>
    public List<BuildEntry> BuildEntries()
    {
        var l = new List<BuildEntry>();
        void E(int cat, int tile, string n, int[][] cost, bool unl, string lk, Action a, string hint = "") => l.Add(new BuildEntry { Cat = cat, Tile = tile, Name = n, Hint = hint, CostArr = cost, Cost = cost == null ? "" : CostStr(cost), Unlocked = unl, Lock = lk, Affordable = cost == null || Can(cost), Do = a });
        E(1, Art.Furnace, "Печь", CostFurnace, true, "", BuildFurnace, "Плавит руду в слитки, улучшает кирку [1]");
        E(1, Art.Lantern, "Фонарь", CostLantern, true, "", () => BuildLamp(0), "Постоянный свет на клетке [2]");
        E(1, Art.CombatLantern, "Боевой фонарь", LampCost[1], Has(Tech.CombatLamp), "улучшение [T] «Боевой фонарь»", () => BuildLamp(1), "Жжёт врагов рядом [8]");
        E(1, Art.CrystalLantern, "Кристальный фонарь", LampCost[2], Has(Tech.Optics), "улучшение [T] «Оптика»", () => BuildLamp(2), "Пускает луч света [9]");
        E(1, Art.Mirror1, "Зеркало", MirrorCost, Has(Tech.Optics), "улучшение [T] «Оптика»", BuildMirror, "Поворачивает луч [0]");
        E(1, Art.AmpCrystal, "Светокристалл", AmpCost, BpDone.Contains("bp_amp"), "чертёж «Светокристаллы»", BuildAmp, "Усиливает соседние фонари");
        E(1, Art.BarrierPost, "Столб-барьер", BarrierCost, BpDone.Contains("bp_barrier"), "чертёж «Световые барьеры»", BuildBarrier, "Два столба держат теней");
        for (int bk = 0; bk < BCount; bk++)
        {
            int k = bk; bool has = HasBuilding(BTypes[k]);
            E(2, AtlasX(BTypes[k], 1), BNames[k] + (has ? " ✓" : ""), has ? null : BCost(k, 1), BUnlocked(k), BLock(k), () => BuildBuilding(k), BDesc[k][0]);
        }
        int[] dt = { Art.Statue, Art.Fountain, Art.Banner };
        for (int dk = 0; dk < 3; dk++) { int kk = dk; E(3, dt[dk], DecorNames[kk], DecorCostArr(kk), BeaconLevel >= 2, "улучшите Центральный маяк", () => BuildDecor(kk), "Украшение: бонус к производству"); }
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
            case Win.Equip: return WinTab == 0 ? Owned.Count + 1 : WinTab == 1 ? OwnedRelics.Count : WinTab == 2 ? CraftableWeapons().Count + Db.Mods.Length : 1;
            case Win.Journal: return WinTab == 0 ? Db.Bps.Length : WinTab == 2 ? Goals.Count : Found.Count;
            case Win.Prod: return ProdList().Count;
            case Win.Build: return BuildEntriesFiltered().Count;
            case Win.Inventory: return It.Count;
            case Win.Trade: return TradeTab == 0 ? CurOffers.Count : SellList().Count;
        }
        return 0;
    }

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
                case Key.Tab: if (Window == Win.Build) { BuildCat = (BuildCat + 1) % 4; WinCur = 0; Sfx.Play("tick", -6, 1.3f); } return true;
                case Key.Enter: case Key.KpEnter:
                    if (Window == Win.Build) { var l = BuildEntriesFiltered(); if (WinCur < l.Count) { var e = l[WinCur]; if (!e.Unlocked) Fail("Недоступно: " + e.Lock); else if (!e.Affordable) Fail("Не хватает ресурсов: " + MissingStr(e.CostArr)); else { var tmp = e; Window = Win.None; tmp.Do(); } } }
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
            case Key.T: CloseWindows(); TechOpen = true; return true;
            case Key.M: CloseWindows(); MapOpen = true; return true;
            case Key.F5: Save(); Toast("Игра сохранена"); return true;
            case Key.Up: WinCur = Math.Max(0, WinCur - 1); return true;
            case Key.Down: WinCur = Math.Min(Math.Max(0, WinLen() - 1), WinCur + 1); return true;
        }
        int len = WinLen(); if (WinCur >= len) WinCur = Math.Max(0, len - 1);
        switch (Window)
        {
            case Win.Equip: EquipKey(k); break;
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

    /// <summary>Мышь в окне улучшений [T]: вкладки, клик по строке — изучить, ПКМ — закрыть.</summary>
    bool TechMouse(InputEventMouse e)
    {
        if (hud == null || e is not InputEventMouseButton mb || !mb.Pressed) return false;
        var p = e.Position / UiScaleVp;
        if (mb.ButtonIndex == MouseButton.Right) { CloseWindows(); return true; }
        if (mb.ButtonIndex != MouseButton.Left) return false;
        int tab = hud.TabHit(p), row = hud.RowHit(p);
        if (tab >= 0) { TechPage = tab; Sfx.Play("tick"); return true; }
        if (row >= 1) { Digit(row); return true; }
        return false;
    }

    void EquipKey(Key k)
    {
        if (k == Key.Left) { WinTab = (WinTab + 3) % 4; WinCur = 0; return; }
        if (k == Key.Right) { WinTab = (WinTab + 1) % 4; WinCur = 0; return; }
        bool enter = k == Key.Enter || k == Key.KpEnter;
        switch (WinTab)
        {
            case 0:
                if (enter) EquipWeapon(WinCur - 1);
                else if (k >= Key.Key1 && k <= Key.Key7) InstallMod((int)k - (int)Key.Key1);
                else if (k == Key.Backspace || k == Key.Delete) { var ow = CurOw; if (ow != null && ow.Mods.Count > 0) RemoveMod(ow.Mods.Count - 1); }
                break;
            case 1: if (enter && WinCur < OwnedRelics.Count) ToggleRelic(OwnedRelics[WinCur]); break;
            case 2:
                if (enter)
                {
                    var ws = CraftableWeapons();
                    if (WinCur < ws.Count) CraftWeapon(ws[WinCur]); else CraftMod(WinCur - ws.Count);
                }
                break;
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
