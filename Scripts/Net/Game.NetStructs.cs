using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lamplighter;

/// <summary>Общие постройки игроков в кооперативе: поставленные объекты (печь, фонари, светокристаллы, барьеры, декор, зеркала), добытые и разобранные блоки и фонари игрока, а также общие цели.
/// Изменения находятся сравнением со слепком каждые 0,4 с и пересылаются через хост; хост проверяет, что переход клетки разрешён (поставить на пустое, убрать добываемое), поэтому клиент
/// не может подменить мир. Вошедший игрок получает полный список изменений. Здания, места интереса и фонари древних синхронизируются отдельно (Game.NetVillage, Game.NetWorld).</summary>
public partial class Game
{
    byte[] shObj; readonly Dictionary<int, (int k, int l, int d)> shLamps = new(); float netStructT;

    static readonly HashSet<Tl> PlaceSet = new() { Tl.Furnace, Tl.Lantern, Tl.CombatLantern, Tl.CrystalLantern, Tl.AmpCrystal, Tl.BarrierPost, Tl.Statue, Tl.Fountain, Tl.Banner, Tl.Mirror1, Tl.Mirror2 };
    static readonly HashSet<Tl> MineSet = new() { Tl.Tree, Tl.Pine, Tl.DeadTree, Tl.Stone, Tl.Copper, Tl.Iron, Tl.Lumen, Tl.Aether, Tl.CrystalOre, Tl.SilverOre, Tl.Rubble, Tl.Ruin, Tl.Obsidian, Tl.Ember, Tl.MeteorOre, Tl.FrostOre, Tl.IceBlock,
        Tl.Furnace, Tl.Lantern, Tl.CombatLantern, Tl.CrystalLantern, Tl.AmpCrystal, Tl.BarrierPost, Tl.Statue, Tl.Fountain, Tl.Banner, Tl.Mirror1, Tl.Mirror2, Tl.ShadowPillar };

    /// <summary>Разрешённые для синхронизации переходы клетки: поставить объект на пустое место (или повернуть зеркало) и убрать добываемый или поставленный объект.</summary>
    static bool SyncTile(Tl o, Tl n) => (n == Tl.MeteorOre || n == Tl.ShadowPillar) ? o == Tl.None : (n != Tl.None && MineSet.Contains(n) && !PlaceSet.Contains(n)) ? o == Tl.None : PlaceSet.Contains(n) ? (o == Tl.None || PlaceSet.Contains(o)) : n == Tl.None && MineSet.Contains(o);

    HashSet<int> BuildingKeys() { var s = new HashSet<int>(); foreach (var b in Buildings) s.Add(BKey(b)); return s; }

    void NetStructTick(float dt)
    {
        if (!NetService.Active || Net == null) { shObj = null; return; }
        netStructT -= dt; if (netStructT > 0) return; netStructT = .4f;
        if (shObj == null || shObj.Length != W.Obj.Length) { RebuildStructShadow(); return; }
        if (!W.Obj.AsSpan().SequenceEqual(shObj))
        {
            var bk = BuildingKeys(); int sent = 0;
            for (int i = 0; i < W.Obj.Length; i++)
            {
                if (W.Obj[i] == shObj[i]) continue;
                if (sent < 300 && !bk.Contains(i) && SyncTile((Tl)shObj[i], (Tl)W.Obj[i])) { Net.SendTile(i % World.Wd, i / World.Wd, W.Obj[i], Placed.Contains(i) ? 1 : 0); sent++; }
                shObj[i] = W.Obj[i];
            }
        }
        var cur = new Dictionary<int, (int, int, int)>(); foreach (var l in Lamps) cur[World.Key(l.X, l.Y)] = (l.Kind, l.Level, l.Dir);
        foreach (var kv in cur) if (!shLamps.TryGetValue(kv.Key, out var o) || o != kv.Value) Net.SendLamp(1, kv.Key % World.Wd, kv.Key / World.Wd, kv.Value.Item1, kv.Value.Item2, kv.Value.Item3);
        foreach (var kv in shLamps) if (!cur.ContainsKey(kv.Key)) Net.SendLamp(2, kv.Key % World.Wd, kv.Key / World.Wd, 0, 1, 0);
        shLamps.Clear(); foreach (var kv in cur) shLamps[kv.Key] = kv.Value;
    }

    void RebuildStructShadow()
    {
        shObj = (byte[])W.Obj.Clone(); shLamps.Clear(); foreach (var l in Lamps) shLamps[World.Key(l.X, l.Y)] = (l.Kind, l.Level, l.Dir);
    }

    /// <summary>Хост/участник: изменение клетки от другого игрока. false — переход не разрешён.</summary>
    public bool NetApplyTile(int x, int y, int t, int flag)
    {
        if (!World.In(x, y) || !Enum.IsDefined(typeof(Tl), (byte)t)) return false; int key = World.Key(x, y);
        if (BuildingByKey(key) != null) return false; var cur = W.Get(x, y); if (!SyncTile(cur, (Tl)t)) return false;
        SetObj(x, y, (Tl)t); if (flag != 0) Placed.Add(key); else Placed.Remove(key);
        if ((Tl)t == Tl.Furnace) HasFurnace = true;                    // печь другого игрока считается построенной: улучшение кирки и цели работают рядом с ней у всех
        if (shObj != null && key < shObj.Length) shObj[key] = (byte)t;
        if ((Tl)t == Tl.None) Burst(TileCenter(x, y), TileColor(cur), 6, 45);
        return true;
    }

    public bool NetApplyLamp(int op, int x, int y, int kind, int level, int dir)
    {
        if (!World.In(x, y) || kind < 0 || kind > 2 || level < 1 || level > 5) return false; int key = World.Key(x, y);
        var l = Lamps.Find(q => q.X == x && q.Y == y);
        if (op == 2) { if (l != null) { Lamps.Remove(l); RecomputeLightTech(); } shLamps.Remove(key); return true; }
        if (l == null) { if (Lamps.Count >= MaxLamps + 16) return false; l = new Lamp { X = x, Y = y, Anim = 0 }; Lamps.Add(l); OnLampLit(l.Pos, kind); }
        l.Kind = kind; l.Level = level; l.Dir = dir; shLamps[key] = (kind, level, dir); RecomputeLightTech(); return true;
    }

    /// <summary>Хост: полный список изменений мира для вошедшего (тройки: клетка, тип, поставлено) и фонарей игроков (по пять чисел).</summary>
    public int[] NetStructTiles()
    {
        var l = new List<int>(); if (W.GenObj == null) return l.ToArray(); var bk = BuildingKeys();
        for (int i = 0; i < W.Obj.Length && l.Count < 90000; i++) if (W.Obj[i] != W.GenObj[i] && !bk.Contains(i) && SyncTile((Tl)W.GenObj[i], (Tl)W.Obj[i])) { l.Add(i); l.Add(W.Obj[i]); l.Add(Placed.Contains(i) ? 1 : 0); }
        return l.ToArray();
    }

    public int[] NetStructLamps() { var l = new List<int>(); foreach (var q in Lamps) { l.Add(q.X); l.Add(q.Y); l.Add(q.Kind); l.Add(q.Level); l.Add(q.Dir); } return l.ToArray(); }

    void NetApplyStructSnapshot(int[] tiles, int[] lamps)
    {
        if (tiles != null) for (int i = 0; i + 2 < tiles.Length; i += 3) NetApplyTile(tiles[i] % World.Wd, tiles[i] / World.Wd, tiles[i + 1], tiles[i + 2]);
        if (lamps != null) for (int i = 0; i + 4 < lamps.Length; i += 5) NetApplyLamp(1, lamps[i], lamps[i + 1], lamps[i + 2], lamps[i + 3], lamps[i + 4]);
        RebuildStructShadow();
    }

    // ───────────────────────── общий прогресс: уровень маяка и побеждённые стражи ─────────────────────────
    int shBeacon = -1, shBoss = -1; float netProgT;

    void NetProgressTick(float dt)
    {
        if (!NetService.Active || Net == null) { shBeacon = -1; return; }
        netProgT -= dt; if (netProgT > 0) return; netProgT = .5f;
        if (shBeacon < 0) { shBeacon = BeaconLevel; shBoss = BossMask; return; }
        if (BeaconLevel != shBeacon || BossMask != shBoss) { Net.SendProgress(BeaconLevel, BossMask); shBeacon = BeaconLevel; shBoss = BossMask; }
    }

    /// <summary>Уровень маяка и побеждённые стражи общие для всех: берётся наибольшее значение, печати открываются у всех. Награды за стражей получает тот, кто победил.</summary>
    public bool NetApplyProgress(int beacon, int boss)
    {
        if (beacon < 1 || beacon > 6 || boss < 0 || boss > 31) return false;
        if (beacon > BeaconLevel)
        {
            BeaconLevel = beacon; ApplyBeaconRadius();
            if (beacon >= 2) foreach (var t in W.Seal1) SetObj(t.X, t.Y, Tl.None); if (beacon >= 3) foreach (var t in W.Seal2) SetObj(t.X, t.Y, Tl.None);
            if (beacon >= 4) foreach (var t in W.Seal3) SetObj(t.X, t.Y, Tl.None); if (beacon >= 5) foreach (var t in W.Seal4) SetObj(t.X, t.Y, Tl.None);
            Announce($"Центральный маяк: уровень {BeaconLevel}", "Напарник улучшил маяк — путь открыт", new Color("#d58cff"), "ancient");
        }
        if ((boss & ~BossMask) != 0) { BossMask |= boss; Toast("Страж побеждён: прогресс общий"); }
        shBeacon = BeaconLevel; shBoss = BossMask; return true;
    }
}
