using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Прогрессия: ядра света, маяк, печати, боссы, карта, силуэты.</summary>
public partial class Game
{
    public int BeaconLevel = 1, BossMask;
    public bool BossDown(int id) => ((BossMask >> (id - 1)) & 1) == 1;
    public bool BossDefeated => BossDown(1);
    public static float BeaconRadius(int level) => 11 + 4 * (level - 1);

    void ApplyBeaconRadius()
    {
        var a = W.Ancients[0]; a.RadiusTiles = BeaconRadius(BeaconLevel); a.TargetRadius = a.RadiusTiles;
    }

    static readonly string[] BeaconOpens = { "", "Затопленные шахты", "Пепельные пустоши", "Ледяной монастырь", "Сердце Тьмы", "" };
    static readonly string[] BeaconGain = { "", "Откроется путь в Затопленные шахты", "Откроется путь в Пепельные пустоши", "Откроется путь в Ледяной монастырь", "Откроется путь в Сердце Тьмы", "Окончательная активация: волна света очистит мир" };

    void UpgradeBeacon()
    {
        if (BeaconLevel >= 6) { Fail("Центральный маяк уже активирован окончательно"); return; }
        if (BeaconLevel == 5 && !BossDown(5)) { Fail("Для окончательной активации нужно победить Источник Тьмы в Сердце Тьмы"); return; }
        if (Inv[It.Core] < 1) { Fail("Нужно ядро света — его даёт победа над стражем биома"); return; }
        Inv[It.Core]--; BeaconLevel++;
        var a = W.Ancients[0]; a.TargetRadius = BeaconRadius(BeaconLevel); a.Pulse = 1f; OnAncientLit(a, true);
        Flash = .6f; Shake = 6; Burst(a.Pos, new Color("#fff2b0"), 40, 160); Sfx.Play("ancient");
        GiveXp(80);
        TradeEpoch++;
        if (BeaconLevel == 2) { OpenSeal(W.Seal1, "Затопленные шахты"); }
        else if (BeaconLevel == 3) { OpenSeal(W.Seal2, "Пепельные пустоши"); }
        else if (BeaconLevel == 4) { OpenSeal(W.Seal3, "Ледяной монастырь"); }
        else if (BeaconLevel == 5) { OpenSeal(W.Seal4, "Сердце Тьмы"); }
        else StartFinale();
        Save();
    }

    void OpenSeal(List<Vector2I> seal, string biome)
    {
        foreach (var t in seal) { SetObj(t.X, t.Y, Tl.None); Burst(TileCenter(t.X, t.Y), new Color("#d58cff"), 8, 70); }
        Announce($"Центральный маяк: уровень {BeaconLevel}", $"Печать пала — путь в «{biome}» открыт!", new Color("#d58cff"), "ancient");
    }

    // ───────────────────────── взаимодействие ─────────────────────────
    void BeaconIx(Ix ix)
    {
        FastTravelIx(W.Ancients[0], ix);
        ix.Lines.Add($"Центральный маяк · уровень {BeaconLevel}/6 · радиус {(int)BeaconRadius(BeaconLevel)}");
        if (BeaconLevel < 6)
        {
            ix.Action("E", "Улучшить маяк").Need((It.Ic(It.Core) + It.Names[It.Core], 1, Inv[It.Core]));
            if (BeaconOpens[BeaconLevel] != "") ix.Gives("Откроет", BeaconOpens[BeaconLevel]); else ix.Gives("Результат", "Волна света очистит мир", TipRole.Key);
            ix.E = UpgradeBeacon;
        }
    }

    bool TileIx(int x, int y, Tl t, Ix ix)
    {
        W.ByTile.TryGetValue(World.Key(x, y), out var p);
        if (t == Tl.RiftGate) { RiftGateIx(ix); return true; }
        if (t == Tl.Tablet || t == Tl.FalseLantern || t == Tl.Pylon || t == Tl.PylonOn) return HeartIx(x, y, t, ix);
        switch (t)
        {
            case Tl.Chest: case Tl.Shrine: case Tl.Obelisk: case Tl.Cage: case Tl.TechCache: case Tl.Spire: return p != null && PoiIx(p, ix);
            case Tl.Altar: ix.Lines.Add("Алтарь лучей: направьте лучи на три приёмника вокруг (кристальные фонари, зеркала, Прожектор)"); return true;
            case Tl.AltarOn: ix.Lines.Add("Алтарь пробуждён"); return true;
            case Tl.Machine:
                ix.Lines.Add("Заброшенный механизм древних");
                ix.Action("E", "Починить механизм"); NeedCost(ix, MachineCost); ix.Gives("Даст", "Производство деревни +15%", TipRole.Ok); ix.E = () => RestoreMachine(x, y, p);
                return true;
            case Tl.MachineOn: ix.Lines.Add("Механизм работает: производство деревни ускорено"); return true;
            case Tl.Mirror1: case Tl.Mirror2:
                ix.Lines.Add("Кристальное зеркало · " + (t == Tl.Mirror1 ? "наклон /" : "наклон \\"));
                ix.Lines.Add("[E] Повернуть зеркало"); ix.E = () => { SetObj(x, y, t == Tl.Mirror1 ? Tl.Mirror2 : Tl.Mirror1); Sfx.Play("tick", 0, 1.3f); Burst(TileCenter(x, y), new Color("#bfe9ff"), 5, 40); };
                return true;
            case Tl.BridgeBroken:
                ix.Lines.Add("Разрушенный мост");
                ix.Action("E", "Починить мост"); NeedCost(ix, BridgeCost); ix.Gives("Откроет", "Новый путь"); ix.E = () => RepairBridge(x, y);
                return true;
            case Tl.Seal:
                ix.Lines.Add(x < 200 ? "Древняя печать. Её снимет только улучшенный Центральный маяк (ядро света)" : x < 300 ? "Печать пепла. Её снимет второе улучшение Центрального маяка" : x < 450 ? "Ледяная печать. Её снимет третье улучшение Центрального маяка" : "Печать пустоты. Её снимет четвёртое улучшение маяка");
                return true;
            case Tl.Receiver: ix.Lines.Add("Приёмник света: направьте на него луч через зеркала"); return true;
            case Tl.ReceiverOn: ix.Lines.Add("Приёмник светится"); return true;
        }
        return false;
    }

    static readonly int[][] MachineCost = C(It.Crystal, 3, It.Silver, 2, It.Relic, 1);
    static readonly int[][] BridgeCost = C(It.Wood, 8);
    public int MachinesRestored { get { int n = 0; foreach (var p in W.Pois) if (p.Kind == PK.Workshop && p.State == 3) n++; return n; } }
    public float ProdMul => (Has(Tech.SilverGears) ? 1.5f : 1f) * (1f + .15f * MachinesRestored) * (1f + N("Prod"));

    void RestoreMachine(int x, int y, Poi p)
    {
        if (!Can(MachineCost)) { Fail("Для починки нужно: " + NeedStr(MachineCost)); return; }
        Pay(MachineCost); SetObj(x, y, Tl.MachineOn); if (p != null) p.State = 3;
        Burst(TileCenter(x, y), new Color("#8ff0ff"), 24, 100); Sfx.Play("tech"); GiveXp(60); Shake = 3;
        Announce("Механизм починен!", $"Производство деревни +15% (всего +{15 * MachinesRestored}%)", new Color("#8ff0ff"), "upgrade");
        Save();
    }

    void RepairBridge(int x, int y)
    {
        if (!Can(BridgeCost)) { Fail("Для починки моста нужно: " + NeedStr(BridgeCost)); return; }
        Pay(BridgeCost);
        var q = new Queue<Vector2I>(); var start = new Vector2I(x, y); q.Enqueue(start); int n = 0;
        while (q.Count > 0)
        {
            var c = q.Dequeue(); if (W.Get(c.X, c.Y) != Tl.BridgeBroken) continue;
            SetObj(c.X, c.Y, Tl.Bridge); n++; Burst(TileCenter(c.X, c.Y), new Color("#d9b27c"), 5, 50);
            foreach (var d in World.D4) q.Enqueue(c + d);
        }
        foreach (var p in W.Pois) if (p.Kind == PK.BridgeSite && p.Pos.DistanceTo(TileCenter(x, y)) < 12 * TS) p.State = 3;
        Sfx.Play("build"); GiveXp(30);
        Announce("Мост восстановлен", "Теперь можно пройти на другую сторону", new Color("#d9b27c"), "build");
        Save();
    }

    // ───────────────────────── биомы ─────────────────────────
    public static readonly string[] RegionNames = { "Тёмный лес", "Затопленные шахты", "Пепельные пустоши", "Ледяной монастырь", "Сердце Тьмы" };
    public static Color RegionCol(int r) => r == 1 ? new Color("#7ac8ff") : r == 2 ? new Color("#ff9a5a") : r == 3 ? new Color("#bfe9ff") : r == 4 ? new Color("#c070ff") : new Color("#7ad45a");
    static readonly string[] RegionFlavor = {
        "Древний лес, где всё началось",
        "Вода замедляет шаг, кристаллы направляют свет",
        "Горячий пепел жжёт, дым гасит свет", "Лёд скользит, зеркала направляют свет", "Здесь свет лжёт: ищите настоящий" };

    void UpdateRegion(float dt)
    {
        regionT -= dt;
        int r = RegionAt(PPos);
        if (r != lastRegion)
        {
            if (lastRegion >= 0 && regionT <= 0)
            {
                Announce(RegionNames[r], RegionFlavor[r], RegionCol(r), "discover");
                regionT = 6;
            }
            lastRegion = r;
        }
    }

    // ───────────────────────── карта ─────────────────────────
    public byte[] Expl = new byte[(World.Wd / 4) * (World.Ht / 4)];
    public bool MapOpen; float mapT;
    const int CW = World.Wd / 4;

    void InitMap() { }

    void Reveal(Vector2 p, float rad)
    {
        int cx0 = (int)((p.X - rad) / TS / 4), cx1 = (int)((p.X + rad) / TS / 4), cy0 = (int)((p.Y - rad) / TS / 4), cy1 = (int)((p.Y + rad) / TS / 4);
        for (int cy = Math.Max(0, cy0); cy <= Math.Min(World.Ht / 4 - 1, cy1); cy++)
            for (int cx = Math.Max(0, cx0); cx <= Math.Min(CW - 1, cx1); cx++)
            {
                var cc = new Vector2((cx * 4 + 2) * TS, (cy * 4 + 2) * TS);
                if (cc.DistanceTo(p) <= rad && Expl[cy * CW + cx] == 0) Expl[cy * CW + cx] = 1;
            }
    }

    void UpdateMap(float dt)
    {
        mapT -= dt; if (mapT > 0) return; mapT = .5f;
        Reveal(PPos, 9 * TS);
        foreach (var a in W.Ancients) if (a.Lit) Reveal(a.Pos, AncientR(a));
        foreach (var l in Lamps) Reveal(l.Pos, 5 * TS);
    }

    public bool IsExplored(Vector2 p) { int cx = (int)(p.X / TS / 4), cy = (int)(p.Y / TS / 4); return cx >= 0 && cy >= 0 && cx < CW && cy < World.Ht / 4 && Expl[cy * CW + cx] != 0; }

    /// <summary>Индекс цвета клетки на карте в палитре Assets/UI/Map/map_palette.png (порядок цветов — в tools/GraphicsGenerator/make_map_palette.py).</summary>
    public int MapIdx(int x, int y)
    {
        var t = W.Get(x, y); int reg = World.RegionOf(x); byte g = W.Ground[World.Key(x, y)];
        switch (t)
        {
            case Tl.Cliff: return 0;
            case Tl.Pine: return 1;
            case Tl.IceRuin: case Tl.IceBlock: return 2;
            case Tl.FrostOre: return 3;
            case Tl.Void: return 4;
            case Tl.Fragment: return 5;
            case Tl.Monolith: case Tl.Pylon: case Tl.PylonOn: return 6;
            case Tl.Tablet: return 7;
            case Tl.FalseLantern: return 8;
            case Tl.LightBridge: return 8;
            case Tl.Cryo: return 9;
            case Tl.CaveWall: return 10;
            case Tl.Seal: return 11;
            case Tl.Tree: return 12;
            case Tl.DeadTree: return 13;
            case Tl.Deep: return 14;
            case Tl.Bridge: case Tl.BridgeBroken: return 15;
            case Tl.AncOn: case Tl.BossOn: return 8;
            case Tl.AncOff: case Tl.BossOff: return 16;
            case Tl.Chest: return 7;
            case Tl.Lantern: case Tl.CombatLantern: case Tl.CrystalLantern: return 7;
            case Tl.Warehouse: case Tl.Sawmill: case Tl.Mine: case Tl.Forge: case Tl.Drill: case Tl.Collector: case Tl.Furnace: return 9;
            case Tl.Machine: case Tl.MachineOn: case Tl.Mirror1: case Tl.Mirror2: case Tl.Receiver: case Tl.ReceiverOn: case Tl.Emitter: return 17;
            case Tl.CrystalOre: case Tl.SilverOre: return 18;
            case Tl.Obsidian: case Tl.Ember: return 19;
            case Tl.Shrine: case Tl.Obelisk: return 17;
            case Tl.Stone: case Tl.Copper: case Tl.Iron: case Tl.Lumen: case Tl.Aether: return 20;
        }
        if (g == Art.WaterG) return 21;
        if (g == Art.HotAsh) return 22;
        if (reg == 3) return g >= Art.IceFloor && g < Art.IceFloor + 2 ? 25 : 26;
        if (reg == 4) return g == Art.VoidG ? 27 : 28;
        if (reg == 1) return 23;
        if (reg == 2) return 24;
        return g == Art.Dirt ? 29 : 30;
    }

}
