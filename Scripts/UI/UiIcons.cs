using Godot;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Иконки интерфейса как AtlasTexture из общих атласов (предметы и тайлы). Кэшируются; сами PNG лежат в Assets/Sprites.</summary>
public static class UiIcons
{
    static readonly Dictionary<int, AtlasTexture> items = new(), tiles = new();
    static AtlasTexture Make(Texture2D t, int i) => new() { Atlas = t, Region = new Rect2(i * 16, 0, 16, 16), FilterClip = true };
    public static Texture2D Item(int i) { if (!items.TryGetValue(i, out var a)) items[i] = a = Make(Art.IconTex, i); return a; }
    public static Texture2D Tile(int i) { if (!tiles.TryGetValue(i, out var a)) tiles[i] = a = Make(Art.TileTex, i); return a; }
    public const int Pickaxe = 23, Axe = 24, Shovel = 25, Hammer = 26, Sword = 27, Bow = 28, Staff = 29;
}
