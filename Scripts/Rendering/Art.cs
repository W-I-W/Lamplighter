using Godot;

namespace Lamplighter;

/// <summary>Индексы тайлов атласа и текстуры, загруженные из PNG (Assets/Sprites). Игра ничего не рисует кодом: SpriteLibrary.Load собирает атласы из файлов.</summary>
public static partial class Art
{
    public const int Dirt = 4, Tree = 5, Stone = 6, Copper = 7, Iron = 8, Lumen = 9,
        AncOff = 10, AncOn = 11, Furnace = 12, Lantern = 13, Cliff = 14, TileCount = 134, IconCount = 24;
    public static Texture2D TileTex, PlayerTex, ShadeTex, WraithTex, IconTex;
    public const int Chest = 15, ChestOpen = 16, Ruin = 17, House = 18, Tent = 19, Shrine = 20, ShrineUsed = 21,
        AetherOre = 22, Cage = 23, BossOff = 24, BossOn = 25, Obelisk = 26, ObeliskUsed = 27,
        Warehouse = 28, Sawmill = 31, Mine = 34, Forge = 37;
    public static Texture2D VillagerTex, SpitterTex, BomberTex;
    public const int CaveFloor = 40, WaterG = 43, AshFloor = 44, Scorch = 47, HotAsh = 48,
        CaveWall = 49, CrystalOre = 50, SilverOre = 51, Machine = 52, MachineOn = 53, Mirror1 = 54, Mirror2 = 55,
        Receiver = 56, ReceiverOn = 57, Emitter = 58, CrystalLantern = 59, CombatLantern = 60, Rubble = 61,
        BridgeBroken = 62, Bridge = 63, Deep = 64, Seal = 65, SecretDoor = 66, Drill = 67, Collector = 71,
        Wh4 = 75, Saw4 = 76, Mine4 = 77, Forge4 = 78, DeadTree = 79, Boulder = 80, Obsidian = 81, Ember = 82, AshRuin = 83, Vent = 84;
    public static Texture2D EelTex, LeechTex, BeetleTex, DeepTex, ImpTex, GolemTex, SpiritTex, AshBossTex;
    public const int TechCache = 85, Spire = 86, Altar = 87, AltarOn = 88, Stall = 89, Hub = 92, Refinery = 95, AmpCrystal = 98, BarrierPost = 99, MeteorOre = 100;
    public static Texture2D TraderTex, TravelerTex;
    public const int Snow = 101, IceFloor = 104, HeartFloor = 106, VoidG = 109, FrostOre = 110, Pine = 111, IceRuin = 112, IceBlock = 113, VoidObj = 114,
        Monolith = 115, Tablet = 116, FalseLantern = 117, Pylon = 118, PylonOn = 119, ShadowPillar = 120, Fragment = 121, RiftGate = 122,
        Statue = 123, Fountain = 124, Banner = 125, Cryo = 126, LightBridge = 129;
    public static Texture2D WolfTex, PriestTex, SentinelTex, WinterTex, MirageTex, WeaverTex, StalkerTex, SourceTex;

    public static void Build() => SpriteLibrary.Load();
}
