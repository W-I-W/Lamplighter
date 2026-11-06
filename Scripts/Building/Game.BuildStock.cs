using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Постройки как предметы: сначала создаются (платятся ресурсы) и попадают в запас, затем ставятся из запаса, разбираются обратно в запас и ставятся снова.
/// Игровые функции строительства остаются прежними: при установке цена временно возвращается в инвентарь и тут же списывается ими же.</summary>
public partial class Game
{
    public readonly Dictionary<string, int> BuildStock = new();
    public readonly HashSet<int> Placed = new();                 // клетки (World.Key), где стоит предмет, поставленный игроком (зеркала мира разбирать нельзя)
    string demolishKey; float demolishT;
    bool placingStock;

    static float BuildCraftXp(string key) => key == "furnace" ? 20 : key.StartsWith("lamp") ? 25 : key.StartsWith("bld") ? 30 : key == "mirror" ? 5 : key == "amp" || key == "barrier" ? 10 : 30;

    public int StockOf(string key) => BuildStock.TryGetValue(key, out var n) ? n : 0;
    void StockAdd(string key, int d) { int n = Math.Max(0, StockOf(key) + d); if (n == 0) BuildStock.Remove(key); else BuildStock[key] = n; }

    BuildEntry EntryOf(string key) => BuildEntries().Find(e => e.Key == key);

    /// <summary>Создание постройки: платит цену и кладёт её в запас. Здания деревни существуют в одном экземпляре.</summary>
    public bool CraftBuild(BuildEntry e)
    {
        if (!e.Unlocked) { Fail("Недоступно: " + e.Lock); return false; }
        var cost = e.Raw ?? e.CostArr; if (cost == null) return false;
        if (!Can(cost)) { Fail("Не хватает: " + MissingStr(cost)); return false; }
        Pay(cost); StockAdd(e.Key, 1); GiveXp(BuildCraftXp(e.Key)); Sfx.Play("build", -3, 1.2f); Save();
        Toast($"Создано: {e.Name}"); return true;
    }

    /// <summary>Установка из запаса (ЛКМ в режиме размещения). Цена уже оплачена при создании.</summary>
    public bool PlaceEntry(BuildEntry e)
    {
        if (!e.Unlocked) { Fail("Недоступно: " + e.Lock); return false; }
        if (StockOf(e.Key) <= 0) { Fail("Нет в запасе: создайте в меню [B]"); Placing = null; return false; }
        var raw = e.Raw; var snap = (int[])Inv.Clone(); var tile = ToTile(Aim);
        bool ok;
        placingStock = true;
        try
        {
            if (raw != null) foreach (var c in raw) Inv[c[0]] += c[1];
            e.Do();
            ok = raw != null && System.Linq.Enumerable.SequenceEqual(Inv, snap);
        }
        finally { Array.Copy(snap, Inv, snap.Length); placingStock = false; }
        if (!ok) return false;
        StockAdd(e.Key, -1); Placed.Add(World.Key(tile.X, tile.Y)); PlaceFx(tile);
        if (StockOf(e.Key) <= 0) Placing = null;
        Save();
        return true;
    }

    /// <summary>Установка по ключу без меню (быстрые клавиши 9 и 0).</summary>
    void PlaceKey(string key)
    {
        var e = EntryOf(key); if (e == null) return;
        if (StockOf(key) <= 0) { Fail("Нет в запасе: создайте в меню [B]"); return; }
        PlaceEntry(e);
    }

    // ───────────────────────── разбор ─────────────────────────
    /// <summary>Первое нажатие [X] просит подтверждения, второе в течение 3 секунд разбирает предмет и возвращает его в запас.</summary>
    bool ConfirmDemolish(string key, string what, string warn = null)
    {
        if (demolishKey == key && demolishT > 0) { demolishKey = null; return true; }
        demolishKey = key; demolishT = 3f; Toast($"Разобрать: {what}? " + (warn != null ? warn + " " : "") + "Нажмите [X] ещё раз"); Sfx.Play("tick", -2, .8f); return false;
    }

    void DemolishDone(Vector2 at, string key, string name)
    {
        StockAdd(key, 1); Burst(at, new Color("#d9b27c"), 12, 60); Sfx.Play("build", -3, .8f); Toast($"{name}: в запасе {StockOf(key)}"); Save();
    }

    void DemolishTile(int x, int y, string key, string name)
    {
        if (key == "chest" && !ChestEmpty(x, y)) { Fail("Сначала заберите вещи из сундука"); return; }
        if (!ConfirmDemolish($"t{x}:{y}", name)) return;
        SetObj(x, y, Tl.None); Placed.Remove(World.Key(x, y)); if (key == "chest") Chests.Remove(World.Key(x, y));
        var t = key; if (t == "amp" || t == "barrier") { Posts.RemoveAll(p => p.X == x && p.Y == y); RecomputeLightTech(); }
        if (t.StartsWith("decor")) RecountDecor();
        if (t == "mirror") { }
        DemolishDone(TileCenter(x, y), key, name);
    }

    void DemolishLamp(Lamp l)
    {
        if (!ConfirmDemolish($"l{l.X}:{l.Y}", LampNames[l.Kind], l.Level > 1 ? "Улучшения фонаря пропадут." : null)) return;
        Lamps.Remove(l); SetObj(l.X, l.Y, Tl.None); RecomputeLightTech();
        DemolishDone(l.Pos, "lamp" + l.Kind, LampNames[l.Kind]);
    }

    void DemolishBuilding(Building b)
    {
        int bk = BIndex(b.Type);
        if (b.Type == Tl.Warehouse && StockTotal > 0) { Fail("Сначала заберите ресурсы со склада [E]"); return; }
        if (!ConfirmDemolish($"b{b.X}:{b.Y}", BNames[bk], b.Level > 1 ? "Улучшения пропадут." : null)) return;
        if (NetClient) Net?.SendBuildCmd(3, BKey(b), 0, 0);                         // клиенту добычу из здания вернёт хост (RpcGrant)
        else for (int i = 0; i < It.Count; i++) if (b.Local[i] > 0) { Inv[i] += b.Local[i]; Texts.Add(new FText { P = PPos + new Vector2(0, -12), S = $"{It.Ic(i)}+{b.Local[i]} {It.Names[i]}", Life = 1.2f, C = It.Col[i] }); b.Local[i] = 0; }     // накопленное внутри здания не пропадает
        Buildings.Remove(b); SetObj(b.X, b.Y, Tl.None); netT = 0;
        DemolishDone(b.Pos, "bld" + bk, BNames[bk]);
    }

    static string TileStockKey(Tl t) => t switch
    {
        Tl.Furnace => "furnace", Tl.StorageChest => "chest", Tl.AmpCrystal => "amp", Tl.BarrierPost => "barrier", Tl.Statue => "decor0", Tl.Fountain => "decor1", Tl.Banner => "decor2",
        Tl.Mirror1 or Tl.Mirror2 => "mirror", _ => null
    };

    /// <summary>Объект-клетка, поставленный игроком (из запаса), который можно разобрать.</summary>
    bool IsPlacedTile(int x, int y) => Placed.Contains(World.Key(x, y)) && TileStockKey(W.Get(x, y)) != null;

    /// <summary>Описание действия «Разобрать» для клетки игрока.</summary>
    void AddDemolishTile(int x, int y, Ix ix)
    {
        var t = W.Get(x, y); string key = TileStockKey(t); if (key == null) return;
        ix.Action("X", "Разобрать — вернуть в запас"); ix.X = () => DemolishTile(x, y, key, EntryOf(key)?.Name ?? "Предмет");
    }

    /// <summary>Прежние сохранения: печи, светокристаллы, столбы и декор игрока (в мире их нет) считаются поставленными игроком.</summary>
    void InitPlaced()
    {
        for (int y = 0; y < World.Ht; y++) for (int x = 0; x < World.Wd; x++)
        {
            var t = W.Get(x, y); if (t == Tl.Mirror1 || t == Tl.Mirror2) continue;
            if (TileStockKey(t) != null) Placed.Add(World.Key(x, y));
        }
    }

    void TickDemolish(float dt) { if (demolishT > 0) demolishT -= dt; }
}
