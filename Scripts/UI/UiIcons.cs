using Godot;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Иконки интерфейса как AtlasTexture из общих атласов (предметы и тайлы). Кэшируются; сами PNG лежат в Assets/Sprites.</summary>
public static class UiIcons
{
    static readonly Dictionary<int, AtlasTexture> items = new(), tiles = new();
    static readonly Dictionary<Texture2D, Image> imgs = new();
    static AtlasTexture Make(Texture2D t, int i) { var a = new AtlasTexture { Atlas = t, Region = new Rect2(i * 16, 0, 16, 16), FilterClip = true }; Center(a, t, i); return a; }
    /// <summary>Сдвигает рисунок иконки в центр клетки 16×16 (Margin атласной текстуры): считается по видимым пикселям всех кадров, размер иконки не меняется.</summary>
    static void Center(AtlasTexture a, Texture2D atlas, int i)
    {
        try
        {
            if (!imgs.TryGetValue(atlas, out var img)) { img = atlas.GetImage(); if (img != null && img.IsCompressed()) img.Decompress(); imgs[atlas] = img; }
            if (img == null) return;
            int x0 = 99, x1 = -1, y0 = 99, y1 = -1;
            for (int f = 0; (f + 1) * 16 <= img.GetHeight() && f < SpriteLibrary.MaxFrames; f++)
            {
                if ((i + 1) * 16 > img.GetWidth()) break;
                var u = img.GetRegion(new Rect2I(i * 16, f * 16, 16, 16)).GetUsedRect(); if (u.Size.X <= 0 || u.Size.Y <= 0) continue;
                x0 = Mathf.Min(x0, u.Position.X); x1 = Mathf.Max(x1, u.End.X - 1); y0 = Mathf.Min(y0, u.Position.Y); y1 = Mathf.Max(y1, u.End.Y - 1);
            }
            if (x1 < 0) return;
            a.Margin = new Rect2((15 - x1 - x0) / 2, (15 - y1 - y0) / 2, 0, 0);
        }
        catch { }
    }
    /// <summary>Крутит кадры анимированных иконок (предметы и тайлы в окнах): вызывается каждый кадр из игры, меняет Region у кэшированных AtlasTexture.</summary>
    public static void Tick(double time)
    {
        int f = (int)(time / .22) % SpriteLibrary.MaxFrames;
        foreach (var kv in items) { int n = kv.Key < SpriteLibrary.IconFrames.Length ? SpriteLibrary.IconFrames[kv.Key] : 1; if (n > 1) kv.Value.Region = new Rect2(kv.Key * 16, (f % n) * 16, 16, 16); }
        foreach (var kv in tiles) { int n = kv.Key < SpriteLibrary.TileFrames.Length ? SpriteLibrary.TileFrames[kv.Key] : 1; if (n > 1) kv.Value.Region = new Rect2(kv.Key * 16, (f % n) * 16, 16, 16); }
    }
    public static Texture2D Item(int i) { if (!items.TryGetValue(i, out var a)) { items[i] = a = Make(Art.IconTex, i); if (i == Pickaxe) { a.Region = new Rect2(PickIndex(PickTier) * 16, 0, 16, 16); Center(a, Art.IconTex, PickIndex(PickTier)); } } return a; }
    public static Texture2D Tile(int i) { if (!tiles.TryGetValue(i, out var a)) tiles[i] = a = Make(Art.TileTex, i); return a; }
    /// <summary>Тир кирки героя: значок кирки (общий для всех окон) переключается на каменную/медную/железную/кристальную/обсидиановую. Файлы: icon_023 и icon_030..033.</summary>
    public static int PickTier;
    public static void SetPickTier(int tier)
    {
        tier = Mathf.Clamp(tier, 0, 4); if (tier == PickTier) return; PickTier = tier;
        if (items.TryGetValue(Pickaxe, out var a)) { a.Region = new Rect2(PickIndex(tier) * 16, 0, 16, 16); Center(a, Art.IconTex, PickIndex(tier)); }
    }
    /// <summary>Индекс значка кирки нужного тира в атласе: 23 — каменная, 30..33 — медная…обсидиановая.</summary>
    public static int PickIndex(int tier) => tier <= 0 ? Pickaxe : 29 + Mathf.Min(tier, 4);
    public static bool IsPick(int icon) => icon == Pickaxe || icon >= 30 && icon <= 33;
    static AtlasTexture stonePick;
    /// <summary>Значок предмета для чужого игрока: кирка по присланному индексу тира, а не по тиру своего героя (общий значок 23 переключается на свой тир).</summary>
    public static Texture2D ItemFixed(int i)
    {
        if (i != Pickaxe) return Item(i);
        if (stonePick == null || stonePick.Atlas != Art.IconTex) stonePick = Make(Art.IconTex, Pickaxe);
        return stonePick;
    }
    public const int Pickaxe = 23, Axe = 24, Shovel = 25, Hammer = 26, Sword = 27, Bow = 28, Staff = 29;
}
