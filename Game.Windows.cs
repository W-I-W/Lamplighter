using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

public enum Win { None, Equip, Journal, Prod, Build, Trade, Story, Rift }

public sealed class BuildEntry { public string Name, Cost, Lock; public bool Unlocked, Affordable; public Action Do; }

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

    public List<BuildEntry> BuildEntries()
    {
        var l = new List<BuildEntry>();
        void E(string n, int[][] cost, bool unl, string lk, Action a) => l.Add(new BuildEntry { Name = n, Cost = cost == null ? "" : CostStr(cost), Unlocked = unl, Lock = lk, Affordable = cost == null || Can(cost), Do = a });
        E("Печь [1]", CostFurnace, true, "", BuildFurnace);
        E("Фонарь [2]", CostLantern, true, "", () => BuildLamp(0));
        E("Боевой фонарь [8]", LampCost[1], Has(Tech.CombatLamp), "улучшение [T] «Боевой фонарь»", () => BuildLamp(1));
        E("Кристальный фонарь [9]", LampCost[2], Has(Tech.Optics), "улучшение [T] «Оптика»", () => BuildLamp(2));
        E("Зеркало [0]", MirrorCost, Has(Tech.Optics), "улучшение [T] «Оптика»", BuildMirror);
        for (int bk = 0; bk < BCount; bk++)
        {
            int k = bk; bool has = HasBuilding(BTypes[k]);
            E(BNames[k] + (has ? " (построено)" : ""), has ? null : BCost(k, 1), BUnlocked(k), BLock(k), () => BuildBuilding(k));
        }
        E("Светокристалл", AmpCost, BpDone.Contains("bp_amp"), "чертёж «Светокристаллы»", BuildAmp);
        E("Столб-барьер", BarrierCost, BpDone.Contains("bp_barrier"), "чертёж «Световые барьеры»", BuildBarrier);
        for (int dk = 0; dk < 3; dk++) { int kk = dk; E(DecorNames[kk], DecorCostArr(kk), BeaconLevel >= 2, "улучшите Центральный маяк", () => BuildDecor(kk)); }
        return l;
    }

    public List<int> SellList()
    {
        var l = new List<int>(); for (int i = 0; i < It.Count; i++) if (Tradable(i) && Inv[i] > 0) l.Add(i); return l;
    }

    public int WinLen()
    {
        switch (Window)
        {
            case Win.Equip: return WinTab == 0 ? Owned.Count + 1 : WinTab == 1 ? OwnedRelics.Count : WinTab == 2 ? CraftableWeapons().Count + Db.Mods.Length : 1;
            case Win.Journal: return WinTab == 0 ? Db.Bps.Length : Found.Count;
            case Win.Prod: return ProdList().Count;
            case Win.Build: return BuildEntries().Count;
            case Win.Trade: return TradeTab == 0 ? CurOffers.Count : SellList().Count;
        }
        return 0;
    }

    // ───────────────────────── ввод ─────────────────────────
    bool WindowKey(Key k, bool shift)
    {
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
            case Win.Journal: if (k == Key.Left) { WinTab = 0; WinCur = 0; } else if (k == Key.Right) { WinTab = 1; WinCur = 0; } break;
            case Win.Prod: ProdKey(k); break;
            case Win.Build: if (k == Key.Enter || k == Key.KpEnter) { var l = BuildEntries(); if (WinCur < l.Count) { var e = l[WinCur]; if (!e.Unlocked) Fail("Недоступно: " + e.Lock); else { var tmp = e; Window = Win.None; tmp.Do(); } } } break;
            case Win.Trade: TradeKey(k, shift); break;
            case Win.Story: if (k == Key.E || k == Key.Enter || k == Key.KpEnter || k == Key.Space) CloseWindows(); break;
        }
        return true;
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
