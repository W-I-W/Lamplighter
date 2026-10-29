using Godot;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Деревья в две клетки и кусты. Ствол — обычная твёрдая клетка; крона — тайл в клетке над ним (слой выше игрока, без коллизии), поэтому за деревом можно пройти.
/// Кусты — декор леса, который ломается с одного удара и даёт 1 дерево.</summary>
public partial class Game
{
    public readonly HashSet<int> CutBush = new();              // срубленные кусты (World.Key клетки)

    const float TreeFadeAlpha = .5f;                           // прозрачность кроны и ствола, когда за деревом игрок или постройка

    static bool IsPlantTile(Tl t) => t == Tl.Tree || t == Tl.Pine || t == Tl.DeadTree;

    static bool IsBuiltTile(Tl t) => IsProducer(t) || t == Tl.Furnace || t == Tl.Lantern || t == Tl.Warehouse || t == Tl.House || t == Tl.Tent || t == Tl.Stall || t == Tl.Hub
        || t == Tl.CrystalLantern || t == Tl.CombatLantern || t == Tl.AmpCrystal || t == Tl.BarrierPost;

    /// <summary>Тайл кроны для клетки (x, y): крона рисуется над стволом, если снизу стоит дерево, а сама клетка не занята твёрдым объектом.</summary>
    int CrownId(int x, int y)
    {
        if (!World.In(x, y + 1)) return -1;
        var below = W.Get(x, y + 1); if (!IsPlantTile(below)) return -1;
        var own = W.Get(x, y); if (own != Tl.None && !IsPlantTile(own) && !IsBuiltTile(own)) return -1;      // постройка за деревом — крона рисуется поверх неё, как поверх игрока
        return below == Tl.Tree ? Art.TreeTop : below == Tl.Pine ? Art.PineTop : Art.DeadTop;
    }

    /// <summary>Крона клетки (x, y): по умолчанию рисуется под игроком (слой B); пока игрок стоит за этим деревом — над ним (слой T).</summary>
    void RefreshCrown(int x, int y)
    {
        var ch = ChunkAt(x, y); if (ch == null) return;
        var lc = new Vector2I(x % CS, y % CS); int id = CrownId(x, y); bool above = behindTrunks.Contains(new Vector2I(x, y + 1)) || behindTrunks.Contains(new Vector2I(x, y)) || IsBuiltTile(W.Get(x, y));      // свой ствол за игроком или в этой клетке копия чужого ствола — крона выше
        if (id < 0) { ch.B.EraseCell(lc); ch.T.EraseCell(lc); return; }
        var layer = above ? ch.T : ch.B; var other = above ? ch.B : ch.T;
        layer.SetCell(lc, srcId, new Vector2I(id, 0)); other.EraseCell(lc);
    }

    /// <summary>Есть ли на клетке видимый куст (по слою растительности).</summary>
    bool IsBushAt(int x, int y)
    {
        var ch = ChunkAt(x, y); if (ch == null || W.Get(x, y) != Tl.None) return false;
        var lc = new Vector2I(x % CS, y % CS); return ch.F.GetCellSourceId(lc) >= 0 && ch.F.GetCellAtlasCoords(lc).X == FoliageBush;
    }

    void ActBush(Vector2I tile, float dt, ToolDef td)
    {
        if (PPos.DistanceTo(TileCenter(tile.X, tile.Y)) > 40f) { MineProg = 0; MineTile = new Vector2I(-1, -1); return; }
        float eff = td.Efficiency(Tl.Tree) * td.MiningPower;
        if (eff <= 0f) { MineProg = 0; MineTile = new Vector2I(-1, -1); return; }
        if (tile != MineTile) { MineTile = tile; MineProg = 0; }
        MineNeed = .45f; float before = MineProg; MineProg += dt * MineSpeed * eff; Swing = .2f;
        if ((int)(before * 5) != (int)(MineProg * 5)) { Fx("mine_hit", TileCenter(tile.X, tile.Y), new Color("#3b8a3a"), .7f); hitFlash = 1f; }
        if (MineProg >= MineNeed) CutBushAt(tile);
    }

    void CutBushAt(Vector2I tile)
    {
        CutBush.Add(World.Key(tile.X, tile.Y));
        var ch = ChunkAt(tile.X, tile.Y); ch?.F.EraseCell(new Vector2I(tile.X % CS, tile.Y % CS));
        MineProg = 0; MineTile = new Vector2I(-1, -1);
        var c = TileCenter(tile.X, tile.Y); Fx("tile_break", c, new Color("#3b8a3a")); Give(It.Wood, 1, c); GiveXp(1);
    }

    /// <summary>Предмет в руках при замахе: значок выбранного слота хотбара (кирка, топор, меч, лук, посох), повёрнутый по направлению удара.</summary>
    void DrawHeldItem(CanvasItem c, Vector2 hand, float ang)
    {
        string r = CurW >= 0 && CurW < Owned.Count ? RefWeapon(Owned[CurW].Id) : RefTool(CurTool);
        int icon = HotIcon(r); if (icon < 0) return;
        float baseAng = icon == UiIcons.Pickaxe ? -Mathf.Pi / 2 : icon == UiIcons.Bow ? 0f : -Mathf.Pi / 4;          // куда «смотрит» рисунок значка
        var centre = hand + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 8f;
        c.DrawSetTransform(centre, ang - baseAng, Vector2.One);
        c.DrawTextureRect(UiIcons.Item(icon), new Rect2(-8, -8, 16, 16), false);
        c.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }

    readonly HashSet<Vector2I> behindTrunks = new();

    /// <summary>Глубина дерева относительно игрока. Игрок севернее ствола (за деревом): крона и копия ствола рисуются выше игрока, его тени и инструмента.
    /// Игрок южнее или сбоку (перед деревом): всё остаётся под игроком, поэтому крона не перекрывает кирку. Небольшой гистерезис не даёт слоям мигать.</summary>
    void UpdateTreeOcclusion()
    {
        var want = new HashSet<Vector2I>(); var pt = ToTile(PPos);
        for (int ty = pt.Y; ty <= pt.Y + 3; ty++)
            for (int tx = pt.X - 2; tx <= pt.X + 2; tx++)
            {
                if (!World.In(tx, ty) || !IsPlantTile(W.Get(tx, ty))) continue;
                var k = new Vector2I(tx, ty); float lim = ty * TS + (behindTrunks.Contains(k) ? 11 : 5);
                if (PPos.Y < lim) want.Add(k);
            }
        if (want.SetEquals(behindTrunks)) return;
        var changed = new List<Vector2I>();
        foreach (var o in behindTrunks) if (!want.Contains(o)) changed.Add(o);
        foreach (var n in want) if (!behindTrunks.Contains(n)) changed.Add(n);
        behindTrunks.Clear(); foreach (var n in want) behindTrunks.Add(n);
        foreach (var k in changed)
        {
            var ch = ChunkAt(k.X, k.Y); var lc = new Vector2I(k.X % CS, k.Y % CS);
            if (ch != null) { if (behindTrunks.Contains(k) && IsPlantTile(W.Get(k.X, k.Y))) ch.K.SetCell(lc, srcId, new Vector2I(AtlasX(W.Get(k.X, k.Y), 1), 0)); else ch.K.EraseCell(lc); }
            RefreshCrown(k.X, k.Y - 1); RefreshCrown(k.X, k.Y);
        }
    }
}
