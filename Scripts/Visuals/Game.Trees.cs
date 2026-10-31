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

    /// <summary>Камни и руды: крона дерева позади такого тайла всё равно рисуется, поверх него.</summary>
    static bool IsRockTile(Tl t) => t == Tl.Stone || t == Tl.Copper || t == Tl.Iron || t == Tl.SilverOre || t == Tl.CrystalOre || t == Tl.Obsidian || t == Tl.Ember || t == Tl.FrostOre || t == Tl.Rubble || t == Tl.Lumen;

    static bool IsBuiltTile(Tl t) => IsProducer(t) || t == Tl.Furnace || t == Tl.Lantern || t == Tl.Warehouse || t == Tl.House || t == Tl.Tent || t == Tl.Stall || t == Tl.Hub
        || t == Tl.CrystalLantern || t == Tl.CombatLantern || t == Tl.AmpCrystal || t == Tl.BarrierPost;

    /// <summary>Тайл кроны для клетки (x, y): крона рисуется над стволом, если снизу стоит дерево, а сама клетка не занята твёрдым объектом.</summary>
    int CrownId(int x, int y)
    {
        if (!World.In(x, y + 1)) return -1;
        var below = W.Get(x, y + 1); if (!IsPlantTile(below)) return -1;
        var own = W.Get(x, y); if (own != Tl.None && !IsPlantTile(own) && !IsBuiltTile(own) && !IsRockTile(own)) return -1;      // постройка за деревом — крона рисуется поверх неё, как поверх игрока
        return below == Tl.Tree ? Art.TreeTop : below == Tl.Pine ? Art.PineTop : Art.DeadTop;
    }

    /// <summary>Крона клетки (x, y): по умолчанию рисуется под игроком (слой B); пока игрок стоит за этим деревом — над ним (слой T).</summary>
    void RefreshCrown(int x, int y)
    {
        var ch = ChunkAt(x, y); if (ch == null) return;
        var lc = new Vector2I(x % CS, y % CS); int id = CrownId(x, y); bool above = behindTrunks.Contains(new Vector2I(x, y + 1)) || behindTrunks.Contains(new Vector2I(x, y)) || IsBuiltTile(W.Get(x, y)) || IsRockTile(W.Get(x, y));      // свой ствол за игроком или в этой клетке копия чужого ствола — крона выше
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
        MineNeed = .45f; Swing = .2f;
        if (MineStrike(dt))
        {
            MineProg += MineSpeed * eff / MineHz; Fx("mine_hit", TileCenter(tile.X, tile.Y), new Color("#3b8a3a"), .7f); hitFlash = 1f;
            if (MineProg >= MineNeed) CutBushAt(tile);
        }
    }

    void CutBushAt(Vector2I tile)
    {
        CutBush.Add(World.Key(tile.X, tile.Y));
        var ch = ChunkAt(tile.X, tile.Y); ch?.F.EraseCell(new Vector2I(tile.X % CS, tile.Y % CS));
        MineProg = 0; MineTile = new Vector2I(-1, -1);
        var c = TileCenter(tile.X, tile.Y); Fx("tile_break", c, new Color("#3b8a3a")); Give(It.Wood, 1, c); GiveXp(1);
    }

    /// <summary>Предмет в руках при замахе: значок выбранного слота хотбара (кирка, топор, меч, лук, посох), повёрнутый по направлению удара.</summary>
    static bool TreeFell(Tl t) => t == Tl.Tree || t == Tl.Pine || t == Tl.DeadTree;

    /// <summary>Эффекты рубки: с каждым ударом с кроны сыплются листья, ближе к концу — всё больше и крона качается сильнее; последний удар (k ≥ 1) роняет облако листьев и щепки.</summary>
    void TreeHitFx(Vector2I tile, Tl t, float k)
    {
        var crown = TileCenter(tile.X, tile.Y) + new Vector2(0, -16); var trunk = TileCenter(tile.X, tile.Y) + new Vector2(0, 2);
        bool last = k >= 1f; int n = last ? 14 : 2 + (int)(k * 5);
        string[] greens = t == Tl.Pine ? new[] { "#2a5a3a", "#1f4a30", "#3a7048" } : t == Tl.DeadTree ? new[] { "#5a4a44", "#6e5a4e", "#473a36" } : new[] { "#3b8a3a", "#58a84a", "#2f6e2f" };
        for (int i = 0; i < n; i++)
            Parts.Add(new Particle { P = crown + new Vector2(rng.RandfRange(-10, 10), rng.RandfRange(-8, 6)), V = new Vector2(rng.RandfRange(-18, 18), rng.RandfRange(-4, 14)), Life = rng.RandfRange(.6f, 1.1f), Max = 1.1f, C = new Color(greens[rng.RandiRange(0, 2)]), Size = 1.5f });
        if (k >= .66f)
            for (int i = 0; i < (last ? 6 : 2); i++)
                Parts.Add(new Particle { P = trunk + new Vector2(rng.RandfRange(-3, 3), rng.RandfRange(-3, 1)), V = new Vector2(rng.RandfRange(-30, 30), -rng.RandfRange(15, 40)), Life = .4f, Max = .4f, C = new Color("#a8743c"), Size = 1.2f });
    }

    public const float MineHz = 3f;
    float mineAcc, mineLastT = -9f;
    /// <summary>Удары кирки идут с частотой MineHz и совпадают с моментом удара в анимации (фаза 0.5): прогресс и звук приходят строго на ударе, а не плавно между ними.</summary>
    bool MineStrike(float dt)
    {
        if (Time - mineLastT > .25f) mineAcc = Time * MineHz % 1f;
        mineLastT = Time; float prev = mineAcc; mineAcc += dt * MineHz;
        return Mathf.Floor(mineAcc - .5f) > Mathf.Floor(prev - .5f);
    }

    void DrawHeldItem(CanvasItem c, Vector2 hand, float ang)
    {
        string r = CurW >= 0 && CurW < Owned.Count ? RefWeapon(Owned[CurW].Id) : RefTool(CurTool);
        int icon = HotIcon(r); if (icon < 0) return;
        float baseAng = icon == UiIcons.Pickaxe ? -Mathf.Pi / 2 : icon == UiIcons.Bow ? 0f : -Mathf.Pi / 4;          // куда «смотрит» рисунок значка
        var centre = hand + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 10f;
        // при прицеле вправо рисунок зеркалится вдоль своей оси, чтобы рабочая сторона инструмента смотрела вперёд
        bool mirror = Mathf.Cos(ang) >= 0;
        c.DrawSetTransformMatrix(new Transform2D(ang, centre) * (mirror ? Transform2D.FlipY : Transform2D.Identity) * new Transform2D(-baseAng, Vector2.Zero));
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
                var k = new Vector2I(tx, ty); bool was = behindTrunks.Contains(k); float lim = ty * TS + (was ? 11 : 5);
                // дерево уходит наверх (и становится прозрачным) только пока игрок реально под кроной: рядом по ширине и не дальше двух клеток за стволом
                if (PPos.Y < lim && PPos.Y > (ty - 2) * TS - (was ? 10 : 6) && Mathf.Abs(PPos.X - (tx * TS + TS * .5f)) < (was ? 24 : 20)) want.Add(k);
            }
        if (want.SetEquals(behindTrunks)) return;
        var changed = new List<Vector2I>();
        foreach (var o in behindTrunks) if (!want.Contains(o)) changed.Add(o);
        foreach (var n in want) if (!behindTrunks.Contains(n)) changed.Add(n);
        behindTrunks.Clear(); foreach (var n in want) behindTrunks.Add(n);
        foreach (var k in changed)
        {
            var ch = ChunkAt(k.X, k.Y); var lc = new Vector2I(k.X % CS, k.Y % CS);
            if (ch != null)
            {
                var tt = W.Get(k.X, k.Y);
                // пока копия ствола в K полупрозрачна, настоящий ствол слоя O скрыт — иначе низ дерева выглядит плотным, а крона прозрачной
                if (behindTrunks.Contains(k) && IsPlantTile(tt)) { ch.K.SetCell(lc, srcId, new Vector2I(AtlasX(tt, 1), 0)); ch.O.EraseCell(lc); }
                else { ch.K.EraseCell(lc); if (IsPlantTile(tt)) ch.O.SetCell(lc, srcId, new Vector2I(AtlasX(tt, LevelAt(k.X, k.Y)), 0)); }
            }
            RefreshCrown(k.X, k.Y - 1); RefreshCrown(k.X, k.Y);
        }
    }
}
