using Godot;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Иконки интерфейса как AtlasTexture из общих атласов (предметы и тайлы). Кэшируются; сами PNG лежат в Assets/Sprites.</summary>
public static class UiIcons
{
    static readonly Dictionary<int, AtlasTexture> items = new(), tiles = new();
    static AtlasTexture Make(Texture2D t, int i) => new() { Atlas = t, Region = new Rect2(i * 16, 0, 16, 16), FilterClip = true };
    /// <summary>Крутит кадры анимированных иконок (предметы и тайлы в окнах): вызывается каждый кадр из игры, меняет Region у кэшированных AtlasTexture.</summary>
    public static void Tick(double time)
    {
        int f = (int)(time / .22) % SpriteLibrary.MaxFrames;
        foreach (var kv in items) { int n = kv.Key < SpriteLibrary.IconFrames.Length ? SpriteLibrary.IconFrames[kv.Key] : 1; if (n > 1) kv.Value.Region = new Rect2(kv.Key * 16, (f % n) * 16, 16, 16); }
        foreach (var kv in tiles) { int n = kv.Key < SpriteLibrary.TileFrames.Length ? SpriteLibrary.TileFrames[kv.Key] : 1; if (n > 1) kv.Value.Region = new Rect2(kv.Key * 16, (f % n) * 16, 16, 16); }
    }
    public static Texture2D Item(int i) { if (!items.TryGetValue(i, out var a)) { items[i] = a = Make(Art.IconTex, i); if (i == Pickaxe) a.Region = new Rect2(PickIndex(PickTier) * 16, 0, 16, 16); } return a; }
    public static Texture2D Tile(int i) { if (!tiles.TryGetValue(i, out var a)) tiles[i] = a = Make(Art.TileTex, i); return a; }
    /// <summary>Тир кирки героя: значок кирки (общий для всех окон) переключается на каменную/медную/железную/кристальную/обсидиановую. Файлы: icon_023 и icon_030..033.</summary>
    public static int PickTier;
    public static void SetPickTier(int tier)
    {
        tier = Mathf.Clamp(tier, 0, 4); if (tier == PickTier) return; PickTier = tier;
        if (items.TryGetValue(Pickaxe, out var a)) a.Region = new Rect2(PickIndex(tier) * 16, 0, 16, 16);
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
