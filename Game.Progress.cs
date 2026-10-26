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

    static readonly string[] BeaconGain = { "", "Откроется путь в Затопленные шахты", "Откроется путь в Пепельные пустоши", "Маяк станет ярче: радиус света ещё больше" };

    void UpgradeBeacon()
    {
        if (BeaconLevel >= 4) { Fail("Центральный маяк уже на максимальном уровне"); return; }
        if (Inv[It.Core] < 1) { Fail("Нужно ядро света — его даёт победа над стражем биома"); return; }
        Inv[It.Core]--; BeaconLevel++;
        var a = W.Ancients[0]; a.TargetRadius = BeaconRadius(BeaconLevel); a.Pulse = 1f;
        Flash = .6f; Shake = 6; Burst(a.Pos, new Color("#fff2b0"), 40, 160); Sfx.Play("ancient");
        GiveXp(80);
        if (BeaconLevel == 2) { OpenSeal(W.Seal1, "Затопленные шахты"); }
        else if (BeaconLevel == 3) { OpenSeal(W.Seal2, "Пепельные пустоши"); }
        else Announce("Центральный маяк: уровень " + BeaconLevel, BeaconGain[BeaconLevel - 1], new Color("#fff2b0"), "ancient");
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
        ix.Lines.Add($"Центральный маяк · уровень {BeaconLevel}/4 · радиус {(int)BeaconRadius(BeaconLevel)}");
        if (BeaconLevel < 4)
        {
            ix.Lines.Add($"[E] Улучшить за 1 ядро света (есть {Inv[It.Core]}): {BeaconGain[BeaconLevel]}");
            ix.E = UpgradeBeacon;
        }
    }

    Ix FindIx()
    {
        var ix = new Ix();
        for (int i = 0; i < W.Ancients.Count; i++)
        {
            var a = W.Ancients[i]; int idx = i;
            if (a.Pos.DistanceTo(PPos) >= 44) continue;
            if (i == 0) { BeaconIx(ix); return ix; }
            if (a.Lit) continue;
            if (a.Boss && !BossDown(a.BossId)) ix.Lines.Add("Фонарь стража: победите стража, чтобы зажечь его");
            else { ix.Lines.Add($"[E] Зажечь древний фонарь ({a.Cost} осколков света)"); ix.E = () => Activate(a, idx); }
            return ix;
        }
        var c = ToTile(PPos); Vector2I best = default; Tl bt = Tl.None; float bd = 45;
        for (int y = c.Y - 3; y <= c.Y + 3; y++)
            for (int x = c.X - 3; x <= c.X + 3; x++)
            {
                var t = W.Get(x, y);
                if (t == Tl.Chest || t == Tl.Shrine || t == Tl.Obelisk || t == Tl.Cage || t == Tl.Machine || t == Tl.MachineOn || t == Tl.Mirror1 || t == Tl.Mirror2
                    || t == Tl.BridgeBroken || t == Tl.Seal || t == Tl.Receiver || t == Tl.ReceiverOn)
                { float d = TileCenter(x, y).DistanceTo(PPos); if (d < bd) { bd = d; best = new Vector2I(x, y); bt = t; } }
            }
        if (bt != Tl.None && TileIx(best.X, best.Y, bt, ix)) return ix;
        var lamp = LampNear(PPos, 32);
        if (lamp != null) { LampIx(lamp, ix); return ix; }
        var b = BuildingNear(PPos, 44);
        if (b != null) { BuildingIx(b, ix); return ix; }
        if (FurnaceNear(44, out var f)) { ix.Lines.Add(SmeltQ.Count > 0 ? "[E] Добавить в плавку" : "[E] Плавить руду (1 руда + 1 дерево = слиток)"); ix.E = () => StartSmelt(f); }
        return ix;
    }

    bool TileIx(int x, int y, Tl t, Ix ix)
    {
        W.ByTile.TryGetValue(World.Key(x, y), out var p);
        switch (t)
        {
            case Tl.Chest: case Tl.Shrine: case Tl.Obelisk: case Tl.Cage: return p != null && PoiIx(p, ix);
            case Tl.Machine:
                ix.Lines.Add("Заброшенный механизм древних");
                ix.Lines.Add($"[E] Починить: {CostStr(MachineCost)} → производство деревни +15%"); ix.E = () => RestoreMachine(x, y, p);
                return true;
            case Tl.MachineOn: ix.Lines.Add("Механизм работает: производство деревни ускорено"); return true;
            case Tl.Mirror1: case Tl.Mirror2:
                ix.Lines.Add("Кристальное зеркало · " + (t == Tl.Mirror1 ? "наклон /" : "наклон \\"));
                ix.Lines.Add("[E] Повернуть зеркало"); ix.E = () => { SetObj(x, y, t == Tl.Mirror1 ? Tl.Mirror2 : Tl.Mirror1); Sfx.Play("tick", 0, 1.3f); Burst(TileCenter(x, y), new Color("#bfe9ff"), 5, 40); };
                return true;
            case Tl.BridgeBroken:
                ix.Lines.Add("Разрушенный мост");
                ix.Lines.Add($"[E] Починить мост: {CostStr(BridgeCost)}"); ix.E = () => RepairBridge(x, y);
                return true;
            case Tl.Seal:
                ix.Lines.Add(x < 200 ? "Древняя печать. Её снимет только улучшенный Центральный маяк (ядро света)" : "Печать пепла. Её снимет второе улучшение Центрального маяка");
                return true;
            case Tl.Receiver: ix.Lines.Add("Приёмник света: направьте на него луч через зеркала"); return true;
            case Tl.ReceiverOn: ix.Lines.Add("Приёмник светится"); return true;
        }
        return false;
    }

    static readonly int[][] MachineCost = C(It.Crystal, 3, It.Silver, 2, It.Relic, 1);
    static readonly int[][] BridgeCost = C(It.Wood, 8);
    public int MachinesRestored { get { int n = 0; foreach (var p in W.Pois) if (p.Kind == PK.Workshop && p.State == 3) n++; return n; } }
    public float ProdMul => (Has(Tech.SilverGears) ? 1.5f : 1f) * (1f + .15f * MachinesRestored);

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
    public static readonly string[] RegionNames = { "Тёмный лес", "Затопленные шахты", "Пепельные пустоши" };
    static readonly string[] RegionFlavor = {
        "Древний лес, где всё началось",
        "Вода замедляет шаг, кристаллы направляют свет",
        "Горячий пепел жжёт, дым гасит свет" };

    void UpdateRegion(float dt)
    {
        regionT -= dt;
        int r = RegionAt(PPos);
        if (r != lastRegion)
        {
            if (lastRegion >= 0 && regionT <= 0)
            {
                Announce(RegionNames[r], RegionFlavor[r], r == 1 ? new Color("#7ac8ff") : r == 2 ? new Color("#ff9a5a") : new Color("#7ad45a"), "discover");
                regionT = 6;
            }
            lastRegion = r;
        }
    }

    // ───────────────────────── карта ─────────────────────────
    public byte[] Expl = new byte[(World.Wd / 4) * (World.Ht / 4)];
    Image mapImg; public ImageTexture MapTex; bool mapDirty = true; public bool MapOpen; float mapT;
    const int CW = World.Wd / 4;

    void InitMap()
    {
        mapImg = Image.CreateEmpty(World.Wd, World.Ht, false, Image.Format.Rgba8);
        mapImg.Fill(new Color(0, 0, 0, 0));
        MapTex = ImageTexture.CreateFromImage(mapImg);
    }

    void Reveal(Vector2 p, float rad)
    {
        int cx0 = (int)((p.X - rad) / TS / 4), cx1 = (int)((p.X + rad) / TS / 4), cy0 = (int)((p.Y - rad) / TS / 4), cy1 = (int)((p.Y + rad) / TS / 4);
        for (int cy = Math.Max(0, cy0); cy <= Math.Min(World.Ht / 4 - 1, cy1); cy++)
            for (int cx = Math.Max(0, cx0); cx <= Math.Min(CW - 1, cx1); cx++)
            {
                var cc = new Vector2((cx * 4 + 2) * TS, (cy * 4 + 2) * TS);
                if (cc.DistanceTo(p) <= rad && Expl[cy * CW + cx] == 0) { Expl[cy * CW + cx] = 1; mapDirty = true; }
            }
    }

    void UpdateMap(float dt)
    {
        mapT -= dt; if (mapT > 0) return; mapT = .5f;
        Reveal(PPos, 9 * TS);
        foreach (var a in W.Ancients) if (a.Lit) Reveal(a.Pos, AncientR(a));
        foreach (var l in Lamps) Reveal(l.Pos, 5 * TS);
        if (mapDirty && (MapOpen || mapImg == null)) RebuildMap();
        else if (mapDirty && Time % 4f < .6f) RebuildMap();
    }

    public bool IsExplored(Vector2 p) { int cx = (int)(p.X / TS / 4), cy = (int)(p.Y / TS / 4); return cx >= 0 && cy >= 0 && cx < CW && cy < World.Ht / 4 && Expl[cy * CW + cx] != 0; }

    public void RebuildMap()
    {
        mapDirty = false;
        var data = new byte[World.Wd * World.Ht * 4];
        for (int y = 0; y < World.Ht; y++)
            for (int x = 0; x < World.Wd; x++)
            {
                if (Expl[(y / 4) * CW + x / 4] == 0) continue;
                var c = MapColor(x, y); int i = (y * World.Wd + x) * 4;
                data[i] = (byte)(c.R * 255); data[i + 1] = (byte)(c.G * 255); data[i + 2] = (byte)(c.B * 255); data[i + 3] = 255;
            }
        mapImg = Image.CreateFromData(World.Wd, World.Ht, false, Image.Format.Rgba8, data);
        MapTex.Update(mapImg);
    }

    Color MapColor(int x, int y)
    {
        var t = W.Get(x, y); int reg = World.RegionOf(x); byte g = W.Ground[World.Key(x, y)];
        switch (t)
        {
            case Tl.Cliff: return new Color("#1c2420");
            case Tl.CaveWall: return new Color("#1a2030");
            case Tl.Seal: return new Color("#d58cff");
            case Tl.Tree: return new Color("#2d6a30");
            case Tl.DeadTree: return new Color("#3a2c26");
            case Tl.Deep: return new Color("#17365a");
            case Tl.Bridge: case Tl.BridgeBroken: return new Color("#a8743d");
            case Tl.AncOn: case Tl.BossOn: return new Color("#ffe27a");
            case Tl.AncOff: case Tl.BossOff: return new Color("#ffffff");
            case Tl.Chest: return new Color("#ffd86b");
            case Tl.Lantern: case Tl.CombatLantern: case Tl.CrystalLantern: return new Color("#ffd86b");
            case Tl.Warehouse: case Tl.Sawmill: case Tl.Mine: case Tl.Forge: case Tl.Drill: case Tl.Collector: case Tl.Furnace: return new Color("#d9b27c");
            case Tl.Machine: case Tl.MachineOn: case Tl.Mirror1: case Tl.Mirror2: case Tl.Receiver: case Tl.ReceiverOn: case Tl.Emitter: return new Color("#8ff0ff");
            case Tl.CrystalOre: case Tl.SilverOre: return new Color("#e4f4ff");
            case Tl.Obsidian: case Tl.Ember: return new Color("#ff9a2e");
            case Tl.Shrine: case Tl.Obelisk: return new Color("#8ff0ff");
            case Tl.Stone: case Tl.Copper: case Tl.Iron: case Tl.Lumen: case Tl.Aether: return new Color("#8a8f98");
        }
        if (g == Art.WaterG) return new Color("#2a5d8a");
        if (g == Art.HotAsh) return new Color("#c0501e");
        if (reg == 1) return new Color("#3a3f4d");
        if (reg == 2) return new Color("#5b5754");
        return g == Art.Dirt ? new Color("#6b4f33") : new Color("#3f7a3a");
    }

    // ───────────────────────── силуэты в темноте ─────────────────────────
    public IEnumerable<Vector4> Marks()
    {
        int n = 0;
        float pulse = .85f + .15f * Mathf.Sin(Time * 2.5f);
        foreach (var a in W.Ancients)
        {
            if (a.Lit) continue;
            float d = a.Pos.DistanceTo(PPos); if (d > (a.Boss ? 420 : 340)) continue;
            yield return new Vector4(a.Pos.X, a.Pos.Y, a.Boss ? 70 : 50, -(a.Boss ? .55f : .45f) * pulse); if (++n >= 12) yield break;
        }
        foreach (var p in W.Pois)
        {
            if (p.State >= 3 && p.Kind != PK.Chest) continue;
            if (!(p.Kind == PK.Workshop || p.Kind == PK.Puzzle || p.Kind == PK.Obelisk || p.Kind == PK.Shrine || p.Kind == PK.Camp || p.Kind == PK.Ruins || p.Kind == PK.House)) continue;
            if (p.Pos.DistanceTo(PPos) > 300) continue;
            yield return new Vector4(p.Pos.X, p.Pos.Y, 34, -.4f * pulse); if (++n >= 12) yield break;
        }
        foreach (var e in Enemies) if (e.IsBoss && !e.Hidden && e.Pos.DistanceTo(PPos) < 360) { yield return new Vector4(e.Pos.X, e.Pos.Y, 46, -.55f * pulse); if (++n >= 12) yield break; }
        foreach (var v in W.Vents)
        {
            var p = TileCenter(v.X, v.Y); if (p.DistanceTo(PPos) > 260) continue;
            yield return new Vector4(p.X, p.Y, 26, -.4f * pulse); if (++n >= 12) yield break;
        }
        foreach (var (t, _) in W.Emitters)
        {
            var p = TileCenter(t.X, t.Y); if (p.DistanceTo(PPos) > 300) continue;
            yield return new Vector4(p.X, p.Y, 28, -.5f * pulse); if (++n >= 12) yield break;
        }
        foreach (var s in new[] { W.Seal1, W.Seal2 })
            if (s.Count > 0 && W.Get(s[0].X, s[0].Y) == Tl.Seal)
            {
                var p = TileCenter(s[0].X, s[0].Y) + new Vector2(8, 56); if (p.DistanceTo(PPos) > 400) continue;
                yield return new Vector4(p.X, p.Y, 80, -.55f * pulse); if (++n >= 12) yield break;
            }
    }
}
