using Godot;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Деревья в две клетки и кусты. Ствол — обычная твёрдая клетка; крона — часть того же спрайта 32×32 (рисуется в клетке над стволом, без коллизии), поэтому за деревом можно пройти.
/// Кусты — декор леса, который ломается с одного удара и даёт 1 дерево.</summary>
public partial class Game
{
    public readonly HashSet<int> CutBush = new();              // срубленные кусты (World.Key клетки)

    const float TreeFadeAlpha = .5f;                           // прозрачность кроны и ствола, когда за деревом игрок или постройка

    static bool IsPlantTile(Tl t) => t == Tl.Tree || t == Tl.Pine || t == Tl.DeadTree;

    /// <summary>Камни и руды: крона дерева позади такого тайла всё равно рисуется, поверх него.</summary>
    static bool IsRockTile(Tl t) => t == Tl.Stone || t == Tl.Copper || t == Tl.Iron || t == Tl.SilverOre || t == Tl.CrystalOre || t == Tl.Obsidian || t == Tl.Ember || t == Tl.FrostOre || t == Tl.Rubble || t == Tl.Lumen;

    static bool IsBuiltTile(Tl t) => t == Tl.Forge || t == Tl.Furnace || t == Tl.Lantern || t == Tl.Warehouse || t == Tl.House || t == Tl.Tent || t == Tl.Stall || t == Tl.Hub
        || t == Tl.CrystalLantern || t == Tl.CombatLantern || t == Tl.AmpCrystal || t == Tl.BarrierPost;

    public const int TreeVariants = 4;                         // 0 — исходный вид, 1..3 — оттенки
    static readonly int[] AllTreeCols = { Art.BigTree, 188, 190, Art.BigPine, 192, 194, Art.BigDead, 196, 198 };
    /// <summary>Вид дерева в клетке (рисунок: форма кроны): стабильно по координатам, одинаково у всех игроков. Равные доли обычного вида и двух новых.</summary>
    public static int TreeKind(int x, int y) { uint h = (uint)(x * 19349663) ^ (uint)(y * 83492791) ^ 0x9e3779b9u; h ^= h >> 15; h *= 0x2c1b3c6du; h ^= h >> 12; return (int)(h % 3); }
    /// <summary>Столбец атласа дерева клетки: семейство берётся по типу тайла, форма — по координатам.</summary>
    public static int TreeAtlas(Tl t, int x, int y) { var k = t == Tl.Pine ? Art.PineKinds : t == Tl.DeadTree ? Art.DeadKinds : Art.TreeKinds; return k[TreeKind(x, y)]; }
    /// <summary>Номер разновидности дерева в клетке: стабильно по координатам, одинаков у ствола и кроны и у всех игроков.</summary>
    public static int TreeAlt(int x, int y) { uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663); h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15; return (int)(h % TreeVariants); }
    static Color TreeTint(int col, int v)
    {
        int fam = Art.TreeFamily(col); bool pine = fam == 1, dead = fam == 2;
        if (dead) return v == 1 ? new Color(1.12f, 1.05f, .95f) : v == 2 ? new Color(.88f, .88f, .94f) : new Color(1f, .92f, .85f);
        if (pine) return v == 1 ? new Color(.9f, 1.04f, 1.1f) : v == 2 ? new Color(1.08f, 1.08f, .95f) : new Color(.95f, .95f, 1.05f);
        return v == 1 ? new Color(1.14f, 1.1f, .78f) : v == 2 ? new Color(.82f, 1.02f, .96f) : new Color(1.06f, .9f, .88f);          // жёлто-зелёное, сине-зелёное, осеннее
    }

    /// <summary>Дерево клетки (x, y) — один тайл 32×32 (крона и ствол вместе): по умолчанию в слое под игроком (R); пока игрок стоит за деревом — целиком в слое над ним (T), полупрозрачном.</summary>
    void SetTreeCell(int x, int y)
    {
        var ch = ChunkAt(x, y); if (ch == null) return;
        var lc = new Vector2I(x % CS, y % CS); var t = W.Get(x, y);
        if (!IsPlantTile(t)) { ch.R.EraseCell(lc); ch.T.EraseCell(lc); return; }
        bool above = behindTrunks.Contains(new Vector2I(x, y)); var layer = above ? ch.T : ch.R; var other = above ? ch.R : ch.T;
        layer.SetCell(lc, srcId, new Vector2I(TreeAtlas(t, x, y), 0), TreeAlt(x, y)); other.EraseCell(lc);
    }

    /// <summary>Есть ли на клетке видимый куст (по слою растительности).</summary>
    bool IsBushAt(int x, int y)
    {
        var ch = ChunkAt(x, y); if (ch == null || W.Get(x, y) != Tl.None) return false;
        var lc = new Vector2I(x % CS, y % CS); return ch.F.GetCellSourceId(lc) >= 0 && ForageCode(ch.F.GetCellAtlasCoords(lc).X);
    }

    static bool ForageCode(int id) => id == FoliageBush || id == FoliageStick || id == FoliagePebble;
    int ForageIdAt(Vector2I t) { var ch = ChunkAt(t.X, t.Y); return ch == null ? FoliageBush : ch.F.GetCellAtlasCoords(new Vector2I(t.X % CS, t.Y % CS)).X; }

    public const int BushCutCode = 255;                          // особый «тип клетки» в сетевом сообщении о клетке: куст срублен
    readonly Dictionary<int, (float p, float t)> mineMemo = new();
    /// <summary>Остановка добычи: прогресс по клетке запоминается, чтобы продолжить с того же места, а не с нуля.</summary>
    const float MineMemoSecs = 30f;                            // брошенный блок через это время ломается заново, а запись о нём удаляется (память не растёт)
    void MineStop() { MineMemoPrune(); if (MineTile.X >= 0 && MineProg > 0) mineMemo[World.Key(MineTile.X, MineTile.Y)] = (MineProg, Time); MineProg = 0; MineTile = new Vector2I(-1, -1); }
    float MineRecall(Vector2I tile) => mineMemo.TryGetValue(World.Key(tile.X, tile.Y), out var m) && Time - m.t < MineMemoSecs ? m.p : 0f;
    void MineMemoPrune()
    {
        if (mineMemo.Count == 0) return; List<int> old = null;
        foreach (var kv in mineMemo) if (Time - kv.Value.t >= MineMemoSecs) (old ??= new List<int>()).Add(kv.Key);
        if (old != null) foreach (var k in old) mineMemo.Remove(k);
    }
    public int MineMemoCount => mineMemo.Count;

    void ActBush(Vector2I tile, float dt, ToolDef td)
    {
        if (PPos.DistanceTo(TileCenter(tile.X, tile.Y)) > 40f) { MineStop(); return; }
        float eff = td.Efficiency(Tl.Tree) * td.MiningPower;
        if (eff <= 0f) { MineProg = 0; MineTile = new Vector2I(-1, -1); return; }
        if (tile != MineTile) { MineStop(); MineTile = tile; MineProg = MineRecall(tile); }
        int fid = ForageIdAt(tile); MineNeed = fid == FoliageBush ? .45f : 1.2f; Swing = .2f;
        if (MineStrike(dt))
        {
            MineProg += MineSpeed * eff / MineHz; Fx("mine_hit", TileCenter(tile.X, tile.Y), fid == FoliagePebble ? new Color("#a0a0ac") : fid == FoliageStick ? new Color("#8c6034") : new Color("#3b8a3a"), fid == FoliagePebble ? 1f : fid == FoliageStick ? 1f : .7f, fid == FoliagePebble ? "mine_stone" : fid == FoliageStick ? "mine_wood" : null); hitFlash = 1f;
            if (MineProg >= MineNeed) CutBushAt(tile);
        }
    }

    void CutBushAt(Vector2I tile)
    {
        int fid = ForageIdAt(tile); CutBush.Add(World.Key(tile.X, tile.Y));
        var ch = ChunkAt(tile.X, tile.Y); ch?.F.EraseCell(new Vector2I(tile.X % CS, tile.Y % CS));
        MineProg = 0; MineTile = new Vector2I(-1, -1); mineMemo.Remove(World.Key(tile.X, tile.Y));
        if (NetService.Active) Net?.SendTile(tile.X, tile.Y, BushCutCode, 0);          // срубленный куст виден всем
        var c = TileCenter(tile.X, tile.Y); Fx("tile_break", c, fid == FoliagePebble ? new Color("#a0a0ac") : fid == FoliageStick ? new Color("#8c6034") : new Color("#3b8a3a"));
        Give(fid == FoliagePebble ? It.Stone : It.Wood, fid == FoliageBush ? 1 : rng.RandiRange(1, 2), c); GiveXp(1);
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

    static readonly Dictionary<int, Texture2D> heldTex = new();
    static readonly Dictionary<int, string> heldName = new() { [UiIcons.Pickaxe] = "pickaxe", [UiIcons.Axe] = "axe", [UiIcons.Shovel] = "shovel", [UiIcons.Hammer] = "hammer", [UiIcons.Sword] = "sword", [UiIcons.Bow] = "bow", [UiIcons.Staff] = "staff" };

    /// <summary>Спрайт предмета в руке по значку (Assets/Sprites/Held/held_*.png, 16×16, смотрит вправо). Нет файла — рисуется значок, как раньше.</summary>
    static Texture2D HeldTex(int icon)
    {
        if (heldTex.TryGetValue(icon, out var t)) return t;
        Texture2D tex = null;
        if (heldName.TryGetValue(icon, out var n)) { var im = SpriteLibrary.LoadImage($"{SpriteLibrary.Root}Held/held_{n}.png"); if (im != null) tex = ImageTexture.CreateFromImage(im); }
        heldTex[icon] = tex; return tex;
    }

    const float HeldHandDx = 3f, HeldHandDy = 6f;

    void DrawHeldItem(CanvasItem c, Vector2 hand, float ang)
    {
        if (CurW < 0 && !OwnedTools.Contains(CurTool)) return;                                                                  // руки пусты: ничего не рисуется
        string r = CurW >= 0 && CurW < Owned.Count ? RefWeapon(Owned[CurW].Id) : RefTool(CurTool);
        int icon = HotIcon(r); if (icon < 0) return;
        float baseAng = icon == UiIcons.Pickaxe ? -Mathf.Pi / 2 : icon == UiIcons.Bow ? 0f : -Mathf.Pi / 4;          // куда «смотрит» рисунок значка
        // при прицеле вправо рисунок зеркалится вдоль своей оси, чтобы рабочая сторона инструмента смотрела вперёд
        // сторона зеркала берётся от направления прицела, а не от текущего угла: угол замаха уходит за вертикаль, и предмет «переворачивался» посреди анимации
        var aimD = Aim - PPos; bool aimRight = (aimD.LengthSquared() > 1 ? aimD.X : Facing) >= 0;
        hand += new Vector2(-HeldHandDx, HeldHandDy);                                                    // кисть героя: слева на экране у пояса (лампа в другой руке), а не у шеи
        var centre = hand + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 10f;
        var held = HeldTex(icon);
        if (held != null)
        {
            // отдельные спрайты в руке (Assets/Sprites/Held): все смотрят вправо, при прицеле влево зеркалятся, чтобы не оказаться вверх ногами
            c.DrawSetTransformMatrix(new Transform2D(ang, centre) * (aimRight ? Transform2D.Identity : Transform2D.FlipY));
            c.DrawTextureRect(held, new Rect2(-8, -8, 16, 16), false);
            c.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
            return;
        }
        c.DrawSetTransformMatrix(new Transform2D(ang, centre) * (aimRight ? Transform2D.FlipY : Transform2D.Identity) * new Transform2D(-baseAng, Vector2.Zero));
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
        foreach (var k in changed) SetTreeCell(k.X, k.Y);
    }
}
