using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Lamplighter;

public static class It
{
    public const int Wood = 0, Stone = 1, CopperOre = 2, IronOre = 3, Shard = 4, Copper = 5, Iron = 6, Aether = 7, Relic = 8,
        SilverOre = 9, Silver = 10, Crystal = 11, Obsidian = 12, Ember = 13, Core = 14, AncCrystal = 15, Coins = 16, Gear = 17, Lens = 18, Frost = 19, RiftShard = 20, Count = 21;
    public static readonly string[] Names = { "Дерево", "Камень", "Медная руда", "Железная руда", "Осколок света", "Медь", "Железо", "Эфир", "Реликвия",
        "Серебряная руда", "Серебро", "Кристалл", "Обсидиан", "Уголь пепла", "Ядро света", "Древний кристалл", "Монеты", "Шестерня", "Линза", "Ледяной кристалл", "Осколок разлома" };
    public static readonly string[] Ab = { "дер.", "кам.", "м.руда", "ж.руда", "оск.", "мед.", "жел.", "эфир", "рел.", "с.руда", "сер.", "крист.", "обсид.", "угол.", "ядро", "др.крист.", "мон.", "шест.", "линза", "лед.крист.", "оск.разл." };
    public static readonly Color[] Col = {
        new("#c9a070"), new("#c0c0cc"), new("#f0a060"), new("#c8d4e8"), new("#8ff0ff"), new("#f2b062"), new("#dfe8f6"), new("#d58cff"), new("#ffd86b"),
        new("#e8eef6"), new("#cfd6e2"), new("#bfe9ff"), new("#a890ff"), new("#ff9a2e"), new("#fff2b0"), new("#ffb0ff"), new("#ffd86b"), new("#b0b8c8"), new("#9fe8ff"), new("#bfe9ff"), new("#c070ff") };
    /// <summary>Слиток, получаемый из руды (или -1).</summary>
    public static int IngotOf(int ore) => ore == CopperOre ? Copper : ore == IronOre ? Iron : ore == SilverOre ? Silver : -1;
}

public sealed class Particle { public Vector2 P, V; public float Life, Max, Size = 1.5f; public Color C; }
public sealed class FText { public Vector2 P; public string S; public float Life; public Color C; }
public sealed class Toast { public string S; public float Life = 4.5f; }
public sealed class Banner { public string Title, Sub; public Color C; public float Life = 5f; }
public sealed class Proj { public Vector2 Pos, Vel; public float Life = 3f, Dmg; public Color C = new("#ff5ec8"); public bool Fire, Zone, Cold, Dark; }
public sealed class Beacon { public Vector2 Pos; public float T, Dur, R, Ring, Burn; }
public sealed class Building { public Tl Type; public int X, Y, Level = 1, Count, Status, Prio = 1, Mode; public float Prog; public bool Worker, Off, Forced, Linked; public int[] Local = new int[It.Count]; public Vector2 Pos => Game.TileCenter(X, Y); }
public sealed class Villager { public Vector2 Pos; public int Job = -1; public float Walk, Stuck; public bool Working; public int Face = 1; public Vector2 Wander; public float WanderT; }
public sealed class Ix { public List<string> Lines = new(); public Action E, U; }

public sealed class SaveData
{
    public int Version = 5, Seed; public string Obj; public bool[] Lit;
    public float Px, Py, Hp, MaxHp, Xp; public int Level, Tier, Tech, Ann, Kills, BossMask, Beacon; public long Ann2;
    public int[] Inv, Stock;
    public int[] BT, BX, BY, BL; public byte[] PoiState; public bool Boss;
    public int[] LX, LY, LK, LL, LD; public string Explored;
    // этап 4
    public string[] WId; public int[][] WMods; public int CurW, HeroLamp; public int[] ModInv;
    public string[] Relics, Equipped, BpDone, FragK, Found; public int[] FragV;
    public int[] BPrio, BMode; public bool[] BOff, BForced; public int[] BLocal;
    public int TradeEpoch, ExtraV; public string[] Bought; public float[] Demand; public float EvNext;
    public string[] EvK; public float[] EvX, EvY, EvT, EvL, EvV; public int[] EvS, EvW, EvSeed;
    // этап 5
    public bool Finished; public int LoreMask; public string Stamp; public float PlaySecs; public string[] Hints;
    public int RiftUnlocked = 1, RiftFirst, LegendMask, CosmMask = 1, CosmSel, RiftClears;
}

public partial class Game : Node2D
{
    public const int TS = 16;
    public const float Zoom = 3f;
    static bool forceNew, showPrologue;

    public World W;
    int srcId;
    const int LightLimit = 96;                       // должен совпадать с размером массива lights в Assets/Shaders/Lighting/lights_common.gdshaderinc
    EntityLayer ents; Camera2D cam; ColorRect darkRect, glowRect; ShaderMaterial darkMat, glowMat; Hud hud;
    public readonly RandomNumberGenerator rng = new();

    // игрок
    public Vector2 PPos; Vector2 pVel; public int Facing = 1; public float Walk;
    public float Hp = 60, MaxHp = 60, Xp; public int Level = 1, Tier;
    public int[] Inv = new int[It.Count];
    public float Invuln, AtkCd, Swing, MineProg, MineNeed, Shake, Flash, Time;
    float regenDelay, tierMsgCd, rippleT;
    public Vector2I MineTile = new(-1, -1);
    public Vector2 Aim;
    public Ix Cur = new();
    public Vector2 SmeltAt; public List<int> SmeltQ = new(); float smeltT; public float SmeltProg => SmeltQ.Count > 0 ? smeltT / SmeltTime : 0;
    const float SmeltTime = 0.9f;
    public bool HasFurnace;
    public int HeroLampLv;
    public int TechMask; public long Ann; public bool TechOpen; public int TechPage;

    public List<Particle> Parts = new();
    public List<FText> Texts = new();
    public List<Toast> Toasts = new();
    public List<Banner> Banners = new();
    float saveT, regionT; int lastRegion = -1;
    public int Kills;
    Vector3 tint = new(.012f, .016f, .045f);

    static readonly int[][] CostFurnace = { new[] { It.Stone, 10 }, new[] { It.Wood, 5 } };
    static readonly int[][] CostLantern = { new[] { It.Wood, 4 }, new[] { It.Copper, 2 } };
    static readonly int[][][] CostPick = {
        new[] { new[] { It.Copper, 5 }, new[] { It.Wood, 5 } },
        new[] { new[] { It.Iron, 6 }, new[] { It.Shard, 3 } },
        new[] { new[] { It.Crystal, 6 }, new[] { It.Silver, 4 }, new[] { It.Iron, 3 } },
        new[] { new[] { It.Obsidian, 5 }, new[] { It.Ember, 5 }, new[] { It.Silver, 3 } } };
    public static readonly string[] PickNames = { "Деревянная кирка", "Медная кирка", "Железная кирка", "Кристальная кирка", "Обсидиановая кирка" };
    static readonly string[] PickGain = {
        "", "Добыча быстрее, урон выше, берёт железную руду",
        "Ещё быстрее, берёт люмен-руду, эфир, серебро и кристаллы",
        "Добыча жил: ломает до 3 соседних блоков руды сразу",
        "Добыча жил до 5 блоков, удары поджигают врагов" };

    public float XpNeed => 25 + 20 * Level;
    public int LitCount { get { int n = 0; for (int i = 1; i < W.Ancients.Count; i++) { var a = W.Ancients[i]; if (!a.Boss && a.Region == 0 && a.Lit) n++; } return n; } }
    public int LitIn(int region) { int n = 0; foreach (var a in W.Ancients) if (a.Region == region && !a.Boss && a.Lit && a != W.Ancients[0]) n++; return n; }
    public Vector2 ViewSize => GetViewportRect().Size;
    public Vector2 CamCenter => cam.GetScreenCenterPosition();
    public bool Has(Tech t) => ((TechMask >> (int)t) & 1) == 1;
    public int RegionAt(Vector2 p) => World.RegionOf((int)Mathf.Floor(p.X / TS));
    public bool WaterAt(Vector2 p) { var t = ToTile(p); return World.In(t.X, t.Y) && W.Ground[World.Key(t.X, t.Y)] == Art.WaterG; }
    public float PlayerLightR => (72f + 8 * Tier + 2 * Level) * (Has(Tech.Lens) ? 1.3f : 1f) * (Has(Tech.HeroLamp) ? 1.25f : 1f) * (1f + .12f * HeroLampLv + N("HeroRadius")) * (Surge > 0 ? 1.4f : 1f) * LightDim;

    // ───────────────────────── инициализация ─────────────────────────
    public override void _Ready()
    {
        rng.Randomize();
        if (OS.GetEnvironment("LAMP_STAGE") == "gengfx") { GraphicsExport.Run(); GetTree().Quit(); return; }
        Diag.Enabled = OS.GetEnvironment("LAMP_DIAG") != "";
        testing = OS.GetEnvironment("LAMP_AUTOTEST") != ""; testDir = OS.GetEnvironment("LAMP_AUTOTEST");
        Settings.Load(); if (testing && OS.GetEnvironment("LAMP_LANG") != "") { Settings.Lang = OS.GetEnvironment("LAMP_LANG"); Loc.LogMissing = true; }
        Settings.Apply(testing); Ach.Load(); if (Settings.Diag) Diag.Enabled = true; if (Diag.Enabled && testing) Diag.OutPath = System.IO.Path.Combine(testDir, "diagnostics.json");
        Art.Build();
        Db.Init();
        AddChild(new Sfx()); AddChild(new Music());
        testing = OS.GetEnvironment("LAMP_AUTOTEST") != "";
        testDir = OS.GetEnvironment("LAMP_AUTOTEST");
        if (!LoadGame()) NewWorld(testing ? 4242 : (int)(rng.Randi() % 1000000));
        ApplyBeaconRadius();
        InitVillage();
        InitMap();
        InitAsh();

        BuildTileMaps();
        ents = new EntityLayer { G = this };
        AddChild(ents);

        cam = new Camera2D { Zoom = new Vector2(Zoom, Zoom), PositionSmoothingEnabled = true, PositionSmoothingSpeed = 9f, Position = PPos };
        cam.LimitLeft = 0; cam.LimitTop = 0; cam.LimitRight = World.Wd * TS; cam.LimitBottom = World.Ht * TS;
        AddChild(cam); cam.MakeCurrent(); cam.ResetSmoothing();

        var dl = new CanvasLayer { Layer = 5 }; AddChild(dl);
        var vcat = VisualCatalog.Get();
        darkMat = vcat?.Darkness ?? GD.Load<ShaderMaterial>("res://Assets/Materials/darkness.tres");
        glowMat = vcat?.Glow ?? GD.Load<ShaderMaterial>("res://Assets/Materials/glow.tres");
        darkRect = new ColorRect { Material = darkMat, MouseFilter = Control.MouseFilterEnum.Ignore, Color = Colors.White };
        glowRect = new ColorRect { Material = glowMat, MouseFilter = Control.MouseFilterEnum.Ignore, Color = Colors.White };
        dl.AddChild(darkRect); dl.AddChild(glowRect);

        var hl = new CanvasLayer { Layer = 10 }; AddChild(hl);
        hud = new Hud { G = this, MouseFilter = Control.MouseFilterEnum.Ignore };
        hl.AddChild(hud);

        Toast("Соберите дерево и камень (ЛКМ), постройте печь [1]. Тьма вокруг опасна — держитесь у маяка. [M] — карта");
        if (pendingMsg != null) Toast(pendingMsg);
        if (pendingMsg2 != null) Toast(pendingMsg2);
        InitLightTech();
        foreach (var ev in pendingEvents) { InitEventState(ev); Events.Add(ev); }
        EventsEnabled = !testing;
        StartMenuOnLaunch();
        if (showPrologue || (!testing && !HasSave && Menu == Mn.None)) { showPrologue = false; OpenStory("Фонарщик", string.Join("\n\n", Story.Prologue), "Пролог"); }
        GD.Print($"[Lamplighter] seed={W.Seed} ancients={W.Ancients.Count} pois={W.Pois.Count}");
    }

    void NewWorld(int seed)
    {
        W = World.Generate(seed);
        PPos = new Vector2((W.Spawn.X + .5f) * TS, (W.Spawn.Y + .5f) * TS);
    }

    void BuildTileMaps()
    {
        InitChunks();
        HasFurnace = false;
        for (int k = 0; k < W.Obj.Length; k++) if (W.Obj[k] == (byte)Tl.Furnace) { HasFurnace = true; break; }
        UpdateChunks(0, true);
    }

    static int AtlasX(Tl t, int lv = 1) => t switch
    {
        Tl.Tree => Art.Tree, Tl.Stone => Art.Stone, Tl.Copper => Art.Copper, Tl.Iron => Art.Iron, Tl.Lumen => Art.Lumen,
        Tl.AncOff => Art.AncOff, Tl.AncOn => Art.AncOn, Tl.Furnace => Art.Furnace, Tl.Lantern => Art.Lantern, Tl.Cliff => Art.Cliff,
        Tl.Chest => Art.Chest, Tl.ChestOpen => Art.ChestOpen, Tl.Ruin => Art.Ruin, Tl.House => Art.House, Tl.Tent => Art.Tent,
        Tl.Shrine => Art.Shrine, Tl.ShrineUsed => Art.ShrineUsed, Tl.Aether => Art.AetherOre, Tl.Cage => Art.Cage,
        Tl.BossOff => Art.BossOff, Tl.BossOn => Art.BossOn, Tl.Obelisk => Art.Obelisk, Tl.ObeliskUsed => Art.ObeliskUsed,
        Tl.Warehouse => lv >= 4 ? Art.Wh4 : Art.Warehouse + lv - 1, Tl.Sawmill => lv >= 4 ? Art.Saw4 : Art.Sawmill + lv - 1,
        Tl.Mine => lv >= 4 ? Art.Mine4 : Art.Mine + lv - 1, Tl.Forge => lv >= 4 ? Art.Forge4 : Art.Forge + lv - 1,
        Tl.Drill => Art.Drill + Math.Min(lv, 4) - 1, Tl.Collector => Art.Collector + Math.Min(lv, 4) - 1,
        Tl.CaveWall => Art.CaveWall, Tl.CrystalOre => Art.CrystalOre, Tl.SilverOre => Art.SilverOre, Tl.Machine => Art.Machine, Tl.MachineOn => Art.MachineOn,
        Tl.Mirror1 => Art.Mirror1, Tl.Mirror2 => Art.Mirror2, Tl.Receiver => Art.Receiver, Tl.ReceiverOn => Art.ReceiverOn, Tl.Emitter => Art.Emitter,
        Tl.CrystalLantern => Art.CrystalLantern, Tl.CombatLantern => Art.CombatLantern, Tl.Rubble => Art.Rubble,
        Tl.BridgeBroken => Art.BridgeBroken, Tl.Bridge => Art.Bridge, Tl.Deep => Art.Deep, Tl.Seal => Art.Seal, Tl.SecretDoor => Art.SecretDoor,
        Tl.DeadTree => Art.DeadTree, Tl.Obsidian => Art.Obsidian, Tl.Ember => Art.Ember, Tl.AshRuin => Art.AshRuin, Tl.Vent => Art.Vent,
        Tl.TechCache => Art.TechCache, Tl.Spire => Art.Spire, Tl.Altar => Art.Altar, Tl.AltarOn => Art.AltarOn, Tl.AmpCrystal => Art.AmpCrystal, Tl.BarrierPost => Art.BarrierPost, Tl.MeteorOre => Art.MeteorOre,
        Tl.FrostOre => Art.FrostOre, Tl.Pine => Art.Pine, Tl.IceRuin => Art.IceRuin, Tl.IceBlock => Art.IceBlock, Tl.Void => Art.VoidObj, Tl.Monolith => Art.Monolith,
        Tl.Tablet => Art.Tablet, Tl.FalseLantern => Art.FalseLantern, Tl.Pylon => Art.Pylon, Tl.PylonOn => Art.PylonOn, Tl.ShadowPillar => Art.ShadowPillar, Tl.Fragment => Art.Fragment,
        Tl.RiftGate => Art.RiftGate, Tl.Statue => Art.Statue, Tl.Fountain => Art.Fountain, Tl.Banner => Art.Banner, Tl.Cryo => Art.Cryo + Math.Min(lv, 3) - 1, Tl.LightBridge => Art.LightBridge,
        Tl.Stall => Art.Stall + Math.Min(lv, 3) - 1, Tl.Hub => Art.Hub + Math.Min(lv, 3) - 1, Tl.Refinery => Art.Refinery + Math.Min(lv, 3) - 1,
        _ => Art.Cliff
    };

    public static Vector2 TileCenter(int x, int y) => new((x + .5f) * TS, (y + .5f) * TS);
    public static Vector2I ToTile(Vector2 p) => new((int)Mathf.Floor(p.X / TS), (int)Mathf.Floor(p.Y / TS));

    public void SetObj(int x, int y, Tl t)
    {
        W.Set(x, y, t);
        var ch = ChunkAt(x, y);
        if (ch != null)
        {
            var lc = new Vector2I(x % CS, y % CS);
            if (t == Tl.None || t == Tl.SecretCache) ch.O.EraseCell(lc); else ch.O.SetCell(lc, srcId, new Vector2I(AtlasX(t, LevelAt(x, y)), 0));
        }
    }

    // ───────────────────────── сохранение ─────────────────────────
    string pendingMsg, pendingMsg2; readonly List<GEvent> pendingEvents = new();
    static int[] Flat(List<Building> bs) { var a = new int[bs.Count * It.Count]; for (int i = 0; i < bs.Count; i++) Array.Copy(bs[i].Local, 0, a, i * It.Count, It.Count); return a; }
    string SavePath => SlotPath(0);

    static bool ValidSave(string path)
    {
        try { if (!File.Exists(path)) return false; using var doc = JsonDocument.Parse(File.ReadAllText(path)); return doc.RootElement.TryGetProperty("Version", out var v) && v.GetInt32() >= 2 && doc.RootElement.TryGetProperty("Obj", out _); }
        catch { return false; }
    }

    /// <summary>Атомарная запись: во временный файл, затем замена; перед заменой последний корректный файл сохраняется как save.bak.</summary>
    void WriteSave(string json)
    {
        var path = SavePath; var tmp = path + ".tmp";
        File.WriteAllText(tmp, json);
        try { if (ValidSave(path)) File.Copy(path, BakPath, true); } catch { }
        File.Move(tmp, path, true);
    }

    public void Save()
    {
        try
        {
            while (SmeltQ.Count > 0) { int ig = It.IngotOf(SmeltQ[0]); if (ig >= 0) Inv[ig]++; SmeltQ.RemoveAt(0); }
            var d = new SaveData
            {
                Seed = W.Seed, Obj = Convert.ToBase64String(W.Obj), Lit = new bool[W.Ancients.Count],
                Px = PPos.X, Py = PPos.Y, Hp = Hp, MaxHp = MaxHp, Xp = Xp, Level = Level, Tier = Tier, Inv = Inv, Stock = Stock,
                Tech = TechMask, Ann2 = Ann, Kills = Kills, BossMask = BossMask, Beacon = BeaconLevel,
                BT = new int[Buildings.Count], BX = new int[Buildings.Count], BY = new int[Buildings.Count], BL = new int[Buildings.Count],
                PoiState = new byte[W.Pois.Count], Explored = Convert.ToBase64String(Expl),
                WId = Owned.ConvertAll(o => o.Id).ToArray(), WMods = Owned.ConvertAll(o => o.Mods.ToArray()).ToArray(), CurW = CurW, HeroLamp = HeroLampLv, ModInv = ModInv,
                Relics = OwnedRelics.ToArray(), Equipped = EquippedRelics.ToArray(), BpDone = new List<string>(BpDone).ToArray(), FragK = new List<string>(Frag.Keys).ToArray(), FragV = new List<int>(Frag.Values).ToArray(), Found = Found.ToArray(),
                BPrio = Buildings.ConvertAll(b => b.Prio).ToArray(), BMode = Buildings.ConvertAll(b => b.Mode).ToArray(), BOff = Buildings.ConvertAll(b => b.Off).ToArray(), BForced = Buildings.ConvertAll(b => b.Forced).ToArray(),
                BLocal = Flat(Buildings), TradeEpoch = TradeEpoch, ExtraV = ExtraVillagers, Bought = new List<string>(Bought).ToArray(), Demand = Demand, EvNext = evNext,
                EvK = Events.ConvertAll(e => e.Kind).ToArray(), EvX = Events.ConvertAll(e => e.Pos.X).ToArray(), EvY = Events.ConvertAll(e => e.Pos.Y).ToArray(), EvT = Events.ConvertAll(e => e.T).ToArray(), EvL = Events.ConvertAll(e => e.Life).ToArray(),
                Finished = Finished, LoreMask = LoreMask, Hints = new List<string>(HintsShown).ToArray(), RiftUnlocked = RiftUnlocked, RiftFirst = RiftFirst, LegendMask = LegendMask, CosmMask = CosmMask, CosmSel = CosmSel, RiftClears = RiftClears, EvV = Events.ConvertAll(e => e.V).ToArray(), EvS = Events.ConvertAll(e => e.State).ToArray(), EvW = Events.ConvertAll(e => e.Wave).ToArray(), EvSeed = Events.ConvertAll(e => e.Seed).ToArray(),
                LX = new int[Lamps.Count], LY = new int[Lamps.Count], LK = new int[Lamps.Count], LL = new int[Lamps.Count], LD = new int[Lamps.Count]
            };
            for (int i = 0; i < W.Ancients.Count; i++) d.Lit[i] = W.Ancients[i].Lit;
            for (int i = 0; i < Buildings.Count; i++) { var b = Buildings[i]; d.BT[i] = (int)b.Type; d.BX[i] = b.X; d.BY[i] = b.Y; d.BL[i] = b.Level; }
            for (int i = 0; i < Lamps.Count; i++) { var l = Lamps[i]; d.LX[i] = l.X; d.LY[i] = l.Y; d.LK[i] = l.Kind; d.LL[i] = l.Level; d.LD[i] = l.Dir; }
            for (int i = 0; i < W.Pois.Count; i++)
            {
                var p = W.Pois[i];
                d.PoiState[i] = (byte)((p.State == 1 ? 0 : p.State) | (p.Seen ? 16 : 0) | (p.Revealed ? 32 : 0));
            }
            d.Stamp = DateTime.Now.ToString("dd.MM.yyyy HH:mm"); d.PlaySecs = PlaySecs;
            WriteSave(JsonSerializer.Serialize(d, new JsonSerializerOptions { IncludeFields = true }));
        }
        catch (Exception e) { GD.PushWarning("save failed: " + e.Message); }
    }

    const int OldWd = 384;
    bool LoadGame()
    {
        if (forceNew) { forceNew = false; return false; }
        if (testing && OS.GetEnvironment("LAMP_LOAD") == "") return false;
        if (LoadFrom(SavePath)) return true;
        if (ValidSave(BakPath) && LoadFrom(BakPath)) { pendingMsg = "Основное сохранение повреждено — загружена резервная копия"; return true; }
        return false;
    }

    bool LoadFrom(string path)
    {
        try
        {
            if (!File.Exists(path)) return false;
            var d = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(path), new JsonSerializerOptions { IncludeFields = true });
            if (d == null || d.Version < 2 || string.IsNullOrEmpty(d.Obj)) return false;
            PlaySecs = d.PlaySecs;
            W = World.Generate(d.Seed);
            var obj = Convert.FromBase64String(d.Obj);
            bool legacy = d.Version < 3; var genObj = (byte[])W.Obj.Clone();
            bool wide = false;
            if (!legacy)
            {
                if (obj.Length == OldWd * World.Ht)
                {
                    // сохранение до этапа 5: мир был шириной 384 — переносим его в новый, остальное берём из генерации
                    wide = true;
                    for (int y = 0; y < World.Ht; y++) Array.Copy(obj, y * OldWd, W.Obj, y * World.Wd, OldWd);
                    for (int y = W.IceGateY - 3; y <= W.IceGateY + 3; y++) for (int x = 358; x < OldWd; x++) W.Obj[y * World.Wd + x] = genObj[y * World.Wd + x];
                }
                else if (obj.Length != W.Obj.Length) return false;
                else W.Obj = obj;
            }
            else
            {
                if (obj.Length != World.N * World.N) return false;
                // перенос старого леса 128x128 в новый мир (проход к шахтам остаётся новым)
                for (int y = 0; y < World.N; y++)
                    for (int x = 0; x < World.N; x++)
                    {
                        if (x >= 116 && y >= 61 && y <= 67) continue;
                        W.Obj[y * World.Wd + x] = obj[y * World.N + x];
                    }
            }
            OverlayNewFinds(genObj, d.PoiState != null ? d.PoiState.Length : 0, legacy);
            { int lv = legacy ? 1 : Math.Max(1, d.Beacon); if (lv >= 2) foreach (var t in W.Seal1) W.Set(t.X, t.Y, Tl.None); if (lv >= 3) foreach (var t in W.Seal2) W.Set(t.X, t.Y, Tl.None); if (lv >= 4) foreach (var t in W.Seal3) W.Set(t.X, t.Y, Tl.None); if (lv >= 5) foreach (var t in W.Seal4) W.Set(t.X, t.Y, Tl.None); }
            for (int i = 0; i < W.Ancients.Count && i < d.Lit.Length; i++) if (d.Lit[i]) { W.Ancients[i].Lit = true; W.Ancients[i].Anim = 1; }
            PPos = new Vector2(d.Px, d.Py); Hp = d.Hp; MaxHp = d.MaxHp; Xp = d.Xp; Level = d.Level; Tier = d.Tier;
            TechMask = d.Tech; Kills = d.Kills; Ann = legacy ? 0 : d.Ann2;
            BossMask = legacy ? (d.Boss ? 1 : 0) : d.BossMask; BeaconLevel = legacy ? 1 : Math.Max(1, d.Beacon);
            if (d.Inv != null) for (int i = 0; i < Inv.Length && i < d.Inv.Length; i++) Inv[i] = d.Inv[i];
            if (legacy && d.Boss) { Inv[It.Core]++; pendingMsg = "Сохранение обновлено: за победу над Хранителем леса вы получили ядро света — улучшите Центральный маяк!"; }
            if (d.Stock != null) for (int i = 0; i < Stock.Length && i < d.Stock.Length; i++) Stock[i] = d.Stock[i];
            if (d.BT != null) for (int i = 0; i < d.BT.Length; i++)
                {
                    var bb = new Building { Type = (Tl)d.BT[i], X = d.BX[i], Y = d.BY[i], Level = d.BL[i] };
                    if (!legacy)
                    {
                        if (d.BPrio != null && i < d.BPrio.Length) bb.Prio = d.BPrio[i]; if (d.BMode != null && i < d.BMode.Length) bb.Mode = d.BMode[i];
                        if (d.BOff != null && i < d.BOff.Length) bb.Off = d.BOff[i]; if (d.BForced != null && i < d.BForced.Length) bb.Forced = d.BForced[i];
                        if (d.BLocal != null && (i + 1) * It.Count <= d.BLocal.Length) Array.Copy(d.BLocal, i * It.Count, bb.Local, 0, It.Count);
                    }
                    else bb.Forced = true;
                    Buildings.Add(bb);
                }
            if (legacy) { Ann = -1L; pendingMsg2 = "Сохранение обновлено: старые постройки связаны со складом автоматически, новые нужно подключать узлами снабжения"; }
            if (d.PoiState != null)
                for (int i = 0; i < W.Pois.Count && i < d.PoiState.Length; i++)
                {
                    var b = d.PoiState[i]; var p = W.Pois[i];
                    p.State = (byte)(b & 15); p.Seen = (b & 16) != 0; p.Revealed = (b & 32) != 0;
                }
            if (!legacy)
            {
                TradeEpoch = d.TradeEpoch; ExtraVillagers = d.ExtraV; if (d.Bought != null) foreach (var b in d.Bought) Bought.Add(b);
                if (d.Demand != null) for (int i = 0; i < Demand.Length && i < d.Demand.Length; i++) Demand[i] = d.Demand[i];
                evNext = d.EvNext > 0 ? d.EvNext : 150;
                if (d.EvK != null) for (int i = 0; i < d.EvK.Length; i++) pendingEvents.Add(new GEvent { Kind = d.EvK[i], Pos = new Vector2(d.EvX[i], d.EvY[i]), T = d.EvT[i], Life = d.EvL[i], V = d.EvV[i], State = d.EvS[i], Wave = d.EvW[i], Seed = d.EvSeed[i] });
            }
            if (!legacy && d.LX != null)
                for (int i = 0; i < d.LX.Length; i++) Lamps.Add(new Lamp { X = d.LX[i], Y = d.LY[i], Kind = d.LK[i], Level = d.LL[i], Dir = d.LD[i], Anim = 1 });
            else
                for (int y = 0; y < World.N; y++) for (int x = 0; x < World.N; x++) if (W.Get(x, y) == Tl.Lantern) Lamps.Add(new Lamp { X = x, Y = y, Anim = 1 });
            if (d.WId != null) for (int i = 0; i < d.WId.Length; i++) { var o = new OwnedWeapon { Id = d.WId[i] }; if (d.WMods != null && i < d.WMods.Length && d.WMods[i] != null) o.Mods.AddRange(d.WMods[i]); if (Db.Weapon(o.Id) != null) Owned.Add(o); }
            CurW = Math.Min(d.CurW, Owned.Count - 1); HeroLampLv = d.HeroLamp;
            if (d.ModInv != null) for (int i = 0; i < ModInv.Length && i < d.ModInv.Length; i++) ModInv[i] = d.ModInv[i];
            if (d.Relics != null) OwnedRelics.AddRange(d.Relics); if (d.Equipped != null) EquippedRelics.AddRange(d.Equipped);
            if (d.BpDone != null) foreach (var b in d.BpDone) BpDone.Add(b);
            if (d.FragK != null && d.FragV != null) for (int i = 0; i < d.FragK.Length && i < d.FragV.Length; i++) Frag[d.FragK[i]] = d.FragV[i];
            if (d.Found != null) Found.AddRange(d.Found);
            if (d.Hints != null) foreach (var h in d.Hints) HintsShown.Add(h); else HintsShown.Add("move");
            Finished = d.Finished; LoreMask = d.LoreMask; if (Finished) Dawn = 1;
            RiftUnlocked = Math.Max(1, d.RiftUnlocked); RiftFirst = d.RiftFirst; LegendMask = d.LegendMask; CosmMask = d.CosmMask | 1; CosmSel = d.CosmSel; RiftClears = d.RiftClears;
            if (d.Explored != null)
            {
                var e = Convert.FromBase64String(d.Explored);
                if (e.Length == Expl.Length) Expl = e;
                else if (e.Length == (OldWd / 4) * (World.Ht / 4)) for (int y = 0; y < World.Ht / 4; y++) Array.Copy(e, y * (OldWd / 4), Expl, y * (World.Wd / 4), OldWd / 4);
            }
            return true;
        }
        catch (Exception e) { GD.PushWarning("load failed: " + e.Message); return false; }
    }

    /// <summary>Старые сохранения не знают о новых находках: переносим их плитки из свежесгенерированного мира.</summary>
    void OverlayNewFinds(byte[] gen, int known, bool legacy)
    {
        for (int i = known; i < W.Pois.Count; i++)
        {
            var p = W.Pois[i]; if (legacy && World.RegionOf(p.Tile.X) != 0) continue;
            for (int y = p.Tile.Y - 5; y <= p.Tile.Y + 5; y++)
                for (int x = p.Tile.X - 5; x <= p.Tile.X + 5; x++)
                {
                    if (!World.In(x, y)) continue; int k = World.Key(x, y);
                    if (W.Obj[k] == (byte)Tl.None && gen[k] != (byte)Tl.None) W.Obj[k] = gen[k];
                }
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest) { Save(); Diag.Write(this); }
    }

    void NewGame()
    {
        try { if (File.Exists(SavePath)) { File.Copy(SavePath, Path.Combine(SaveDir, "save_prev.json"), true); File.Delete(SavePath); } if (File.Exists(BakPath)) File.Delete(BakPath); } catch { }
        forceNew = true; skipMenu = true; showPrologue = true;
        GetTree().ReloadCurrentScene();
    }

    // ───────────────────────── ввод ─────────────────────────
    public override void _Input(InputEvent e)
    {
        if (Menu != Mn.None) { if (e is InputEventKey mk && mk.Pressed && !mk.Echo) MenuKey(mk.Keycode); return; }
        if (FinStage > 0) { if (e is InputEventKey fk && fk.Pressed && !fk.Echo) FinaleKey(fk.Keycode); return; }
        if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Right && Window == Win.None) TrySpin();
        if (e is InputEventKey k && k.Pressed && !k.Echo)
        {
            var key = KeyMap.Translate(k.Keycode); if (key == Key.None) return;
            if (Window != Win.None) { WindowKey(key, k.ShiftPressed); return; }
            switch (key)
            {
                case Key.E: if (Cur.E != null) Cur.E(); else Fail("Рядом нет ничего, с чем можно взаимодействовать"); break;
                case Key.U: if (Cur.U != null) Cur.U(); else Fail("Рядом нет здания или фонаря для улучшения"); break;
                case Key.Key1: Digit(1); break;
                case Key.Key2: Digit(2); break;
                case Key.Key3: Digit(3); break;
                case Key.Key4: Digit(4); break;
                case Key.Key5: Digit(5); break;
                case Key.Key6: Digit(6); break;
                case Key.Key7: Digit(7); break;
                case Key.Key8: Digit(8); break;
                case Key.Key9: Digit(9); break;
                case Key.Key0: Digit(0); break;
                case Key.Z: if (!TechOpen) BuildBuilding(4); break;
                case Key.X: if (!TechOpen) BuildBuilding(5); break;
                case Key.T: { bool o = !TechOpen; CloseWindows(); TechOpen = o; Sfx.Play("tick"); break; }
                case Key.M: { bool o = !MapOpen; CloseWindows(); MapOpen = o; Sfx.Play("tick"); break; }
                case Key.Left: if (TechOpen) { TechPage = (TechPage + 3) % 4; Sfx.Play("tick"); } break;
                case Key.Right: if (TechOpen) { TechPage = (TechPage + 1) % 4; Sfx.Play("tick"); } break;
                case Key.Escape: if (TechOpen || MapOpen) CloseWindows(); else OpenMenu(Mn.Pause); break;
                case Key.Shift: TryDash(); break;
                case Key.Q: TryFlare(); break;
                case Key.V: CycleWeapon(); break;
                case Key.C: OpenWin(Win.Equip); break;
                case Key.J: OpenWin(Win.Journal); break;
                case Key.P: OpenWin(Win.Prod); break;
                case Key.B: OpenWin(Win.Build); break;
                case Key.F: TryPulse(); break;
                case Key.F5: Save(); Toast("Игра сохранена"); break;
            }
        }
    }

    void Digit(int n)
    {
        if (TechOpen) { if (n >= 1) BuyTech(n - 1); return; }
        switch (n)
        {
            case 1: BuildFurnace(); break;
            case 2: BuildLamp(0); break;
            case 3: UpgradePick(); break;
            case 4: BuildBuilding(0); break;
            case 5: BuildBuilding(1); break;
            case 6: BuildBuilding(2); break;
            case 7: BuildBuilding(3); break;
            case 8: BuildLamp(1); break;
            case 9: BuildLamp(2); break;
            case 0: BuildMirror(); break;
        }
    }

    public void Fail(string msg) { Toast(msg); Sfx.Play("error"); }

    // ───────────────────────── игровой цикл ─────────────────────────
    public override void _Process(double delta)
    {
        if (W == null) return;
        float dt = Mathf.Min((float)delta, 0.05f);
        Time += dt;
        Aim = aimOverride ?? GetGlobalMousePosition();
        if (testing) AutoTest(dt);
        UiTime += dt; UpdateMusic(dt);
        if (Menu != Mn.None) { UpdateShaders(dt); ents.QueueRedraw(); hud.QueueRedraw(); return; }
        PlaySecs += dt; achT -= dt; if (achT <= 0) { achT = 2f; Ach.Check(this); }
        if (FinStage >= 2) { UpdateFinale(dt); UpdateShaders(dt); ents.QueueRedraw(); hud.QueueRedraw(); return; }
        if (FinStage == 1) UpdateFinale(dt);

        UpdatePlayer(dt);
        UpdateAncients(dt);
        UpdateLamps(dt);
        UpdateBeams(dt);
        UpdateWeaponFx(dt);
        UpdateIceBlocks(dt);
        UpdateHeart(dt);
        UpdateEndgame(dt);
        UpdateRegrow(dt);
        UpdatePolish(dt);
        UpdateDesign(dt);
        UpdateChunks(dt);
        UpdateEvents(dt);
        UpdateTrade(dt);
        UpdateLightTech(dt);
        UpdateRelics(dt);
        UpdateEnemies(dt);
        UpdateProjectiles(dt);
        UpdateBeacons(dt);
        UpdateBossSpawn(dt);
        UpdateSpawning(dt);
        UpdateVillage(dt);
        UpdatePois(dt);
        UpdateSmelting(dt);
        UpdateRegion(dt);
        UpdateMap(dt);
        UpdateFx(dt);
        UpdateUnlocks(dt);
        Cur = FindIx();

        saveT += dt; if (saveT > 10) { saveT = 0; Save(); }

        Shake = Mathf.Max(0, Shake - dt * 14); Flash = Mathf.Max(0, Flash - dt * 1.2f);
        cam.Position = PPos;
        cam.Offset = Shake > 0 ? new Vector2(rng.RandfRange(-1, 1), rng.RandfRange(-1, 1)) * Shake * Settings.Shake : Vector2.Zero;
        UpdateShaders(dt);
        ents.QueueRedraw(); hud.QueueRedraw();
    }

    // ───────────────────────── свет ─────────────────────────
    public static float Ease(float t) => 1 - Mathf.Pow(1 - Mathf.Clamp(t, 0, 1), 3);
    public float AncientR(Ancient a) => a.RadiusTiles * TS * Ease(a.Anim);

    /// <summary>Находится ли точка в постоянном свете (маяк/фонарь) — там тьма не властна.</summary>
    public bool IsLit(Vector2 p)
    {
        foreach (var a in W.Ancients) if (a.Lit && p.DistanceTo(a.Pos) < AncientR(a) * .85f) return true;
        foreach (var l in Lamps) if (p.DistanceTo(l.Pos) < LampRad(l) * .85f) return true;
        return false;
    }

    float dawn, achT;
    public int TotalLit { get { int n = 0; for (int i = 1; i < W.Ancients.Count; i++) if (W.Ancients[i].Lit) n++; return n; } }
    // ───────────────────────── игрок ─────────────────────────
    public static bool IsWall(Tl t) => t == Tl.Cliff || t == Tl.CaveWall || t == Tl.Seal || t == Tl.Void;
    bool Solid(int tx, int ty, bool phase) { var t = W.Get(tx, ty); return phase ? IsWall(t) : World.Blocks(t); }
    public bool Collides(Vector2 p, float r, bool phase)
    {
        int x0 = (int)Mathf.Floor((p.X - r) / TS), x1 = (int)Mathf.Floor((p.X + r) / TS);
        int y0 = (int)Mathf.Floor((p.Y - r) / TS), y1 = (int)Mathf.Floor((p.Y + r) / TS);
        for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) if (Solid(x, y, phase)) return true;
        return false;
    }
    public Vector2 Move(Vector2 pos, Vector2 d, float r, bool phase)
    {
        var p = pos; p.X += d.X; if (Collides(p, r, phase)) p.X = pos.X;
        var q = p; q.Y += d.Y; if (Collides(q, r, phase)) q.Y = p.Y;
        return q;
    }

    void UpdatePlayer(float dt)
    {
        Invuln = Mathf.Max(0, Invuln - dt); AtkCd = Mathf.Max(0, AtkCd - dt); Swing = Mathf.Max(0, Swing - dt);
        SpinT = Mathf.Max(0, SpinT - dt);
        DashCd = Mathf.Max(0, DashCd - dt); SpinCd = Mathf.Max(0, SpinCd - dt); FlareCd = Mathf.Max(0, FlareCd - dt); PulseCd = Mathf.Max(0, PulseCd - dt);
        regenDelay = Mathf.Max(0, regenDelay - dt); tierMsgCd = Mathf.Max(0, tierMsgCd - dt);

        var dir = Vector2.Zero;
        if (!testing || aimOverride == null)
        {
            bool arrows = Window == Win.None && !TechOpen;
            if (KeyMap.Held("Up") || (arrows && Input.IsKeyPressed(Key.Up))) dir.Y -= 1;
            if (KeyMap.Held("Down") || (arrows && Input.IsKeyPressed(Key.Down))) dir.Y += 1;
            if (KeyMap.Held("Left") || (arrows && Input.IsKeyPressed(Key.Left))) dir.X -= 1;
            if (KeyMap.Held("Right") || (arrows && Input.IsKeyPressed(Key.Right))) dir.X += 1;
        }
        dir = dir.Normalized(); LastMoveDir = dir;
        bool water = WaterAt(PPos);
        float speed = 80f * (1f + N("Speed")) * (water ? (Has(Tech.Boots) ? .9f : .55f) : 1f);
        if (DashT > 0)
        {
            DashT -= dt; pVel = DashDir * 260f;
            if (rng.Randf() < dt * 60) Burst(PPos, new Color("#8ff0ff"), 1, 20);
        }
        else
        {
            if (ChillT > 0) { ChillT -= dt; speed *= .62f; }
            bool slick = OnIce(PPos) && !Has(Tech.SpikeBoots);
            pVel = pVel.Lerp(dir * speed, Mathf.Min(1, dt * (slick ? 2.4f : 16f)));
            if (slick && dir.LengthSquared() < .01f && pVel.LengthSquared() > 16 && rng.Randf() < dt * 20) Burst(PPos + new Vector2(0, 5), new Color("#e8faff"), 1, 20);
        }
        PPos = Move(PPos, pVel * dt, 5f, false);
        if (pVel.LengthSquared() > 100) Walk += dt * 9f * (water ? .6f : 1f);
        if (water && pVel.LengthSquared() > 100) { rippleT -= dt; if (rippleT <= 0) { rippleT = .12f; Burst(PPos + new Vector2(0, 4), new Color("#9fd8ff"), 2, 25); } }
        if (Mathf.Abs(Aim.X - PPos.X) > 2) Facing = Aim.X < PPos.X ? -1 : 1;
        ApplyHazards(dt);

        if (Hp < MaxHp)
        {
            if (IsLit(PPos)) Hp = Mathf.Min(MaxHp, Hp + 4f * dt);
            else if (regenDelay <= 0) Hp = Mathf.Min(MaxHp, Hp + .5f * dt);
        }

        bool act = Input.IsMouseButtonPressed(MouseButton.Left) || KeyMap.Held("Attack");
        if (testing && aimOverride != null) act = false;
        if (act) Act(dt); else { MineProg = 0; MineTile = new Vector2I(-1, -1); }
    }

    float MineSpeed => (1f + .7f * Tier) * (1f + .06f * (Level - 1)) * (1f + N("MineSpd"));
    public float PlayerDamage => (2f + 2f * Tier + .4f * (Level - 1)) * (1f + N("Dmg"));

    static bool Mineable(Tl t) => t == Tl.Tree || t == Tl.Stone || t == Tl.Copper || t == Tl.Iron || t == Tl.Lumen || t == Tl.Aether || t == Tl.Ruin
        || t == Tl.CrystalOre || t == Tl.SilverOre || t == Tl.Rubble || t == Tl.DeadTree || t == Tl.Obsidian || t == Tl.Ember || t == Tl.MeteorOre || t == Tl.FrostOre || t == Tl.Pine || t == Tl.IceBlock || t == Tl.ShadowPillar;

    static int TierNeed(Tl t) => t switch
    {
        Tl.Iron => 1, Tl.MeteorOre => 1, Tl.FrostOre => 3, Tl.Lumen => 2, Tl.Aether => 2, Tl.CrystalOre => 2, Tl.SilverOre => 2, Tl.Obsidian => 3, Tl.Ember => 3, _ => 0
    };

    void ActPick(float dt, bool melee)
    {
        var toAim = Aim - PPos; var dir = toAim.LengthSquared() > 1 ? toAim.Normalized() : new Vector2(Facing, 0);
        Enemy best = null; float bd = 30f;
        foreach (var e in Enemies)
        {
            if (e.Hidden) continue;
            float d = e.Pos.DistanceTo(PPos) - e.Radius;
            if (d < bd && (d < 12 || (e.Pos - PPos).Normalized().Dot(dir) > .1f)) { bd = d; best = e; }
        }
        if (!melee) best = null;
        if (best != null)
        {
            MineProg = 0; MineTile = new Vector2I(-1, -1);
            if (AtkCd <= 0) { AtkCd = .42f; Swing = .22f; Hurt(best, PlayerDamage, dir, true); Sfx.Play("hit"); Diag.Count("attack_melee"); }
            return;
        }
        var tile = ToTile(Aim);
        var t = W.Get(tile.X, tile.Y);
        if (PPos.DistanceTo(TileCenter(tile.X, tile.Y)) > 40f || !Mineable(t))
        { MineProg = 0; MineTile = new Vector2I(-1, -1); return; }
        if (Tier < TierNeed(t))
        {
            if (tierMsgCd <= 0)
            {
                tierMsgCd = 2;
                Fail(t == Tl.Iron ? "Железо не берётся этой киркой — улучшите её до медной [3]" : TierNeed(t) == 3 ? "Эта руда требует кристальной кирки [3]" : "Эта руда требует железной кирки");
            }
            MineProg = 0; return;
        }
        if (tile != MineTile) { MineTile = tile; MineProg = 0; }
        MineNeed = t switch { Tl.Tree => 1.1f, Tl.DeadTree => 1.0f, Tl.Stone => 1.5f, Tl.Ruin => 1.8f, Tl.Rubble => 1.2f, Tl.Copper => 1.8f, Tl.Iron => 2.6f, Tl.SilverOre => 3f, Tl.Lumen => 3.2f, Tl.CrystalOre => 3.6f, Tl.Obsidian => 4.2f, Tl.Ember => 4f, Tl.MeteorOre => 3f, Tl.FrostOre => 4f, Tl.Pine => 1.1f, Tl.IceBlock => 1.1f, Tl.ShadowPillar => 1.8f, _ => 4f };
        float before = MineProg;
        MineProg += dt * MineSpeed; Swing = .2f; Diag.Add("mine_secs", dt);
        if ((int)(before * 5) != (int)(MineProg * 5)) { Fx("mine_hit", TileCenter(tile.X, tile.Y), TileColor(t), t == Tl.Tree ? .73f : 1f); hitFlash = 1f; }
        if (MineProg >= MineNeed) BreakTile(tile, t, true);
    }

    public static Color TileColor(Tl t) => t switch
    {
        Tl.Tree => new Color("#3b8a3a"), Tl.Stone => new Color("#a0a0ac"), Tl.Copper => new Color("#e58a3d"), Tl.Ruin => new Color("#9a9fae"),
        Tl.Iron => new Color("#cbd6e8"), Tl.Lumen => new Color("#5fe4ff"), Tl.Aether => new Color("#d58cff"),
        Tl.CrystalOre => new Color("#bfe9ff"), Tl.SilverOre => new Color("#e8eef6"), Tl.Rubble => new Color("#8a8f9c"), Tl.DeadTree => new Color("#5a4a44"),
        Tl.Obsidian => new Color("#a890ff"), Tl.Ember => new Color("#ff9a2e"), Tl.MeteorOre => new Color("#fff2b0"), Tl.FrostOre => new Color("#bfe9ff"), Tl.Pine => new Color("#2a5a3a"), Tl.IceBlock => new Color("#bfe9ff"), Tl.ShadowPillar => new Color("#8a40c0"), _ => Colors.White
    };

    int ChainLimit => (Tier >= 4 ? 5 : Tier >= 3 ? 3 : Has(Tech.WideSwing) ? 1 : 0) + (int)N("MineChain");

    void BreakTile(Vector2I tile, Tl t, bool chain)
    {
        SetObj(tile.X, tile.Y, Tl.None);
        MineProg = 0; MineTile = new Vector2I(-1, -1);
        var c = TileCenter(tile.X, tile.Y);
        Fx("tile_break", c, TileColor(t));
        switch (t)
        {
            case Tl.Tree: Give(It.Wood, rng.RandiRange(2, 3), c); GiveXp(3); break;
            case Tl.DeadTree: Give(It.Wood, rng.RandiRange(1, 2), c); GiveXp(3); break;
            case Tl.Stone: Give(It.Stone, rng.RandiRange(1, 2), c); GiveXp(4); break;
            case Tl.Ruin: Give(It.Stone, 2, c); GiveXp(2); break;
            case Tl.Rubble: Give(It.Stone, 1, c); GiveXp(1); break;
            case Tl.Copper: Give(It.CopperOre, rng.RandiRange(1, 2), c); GiveXp(6); break;
            case Tl.Iron: Give(It.IronOre, rng.RandiRange(1, 2), c); GiveXp(10); break;
            case Tl.Lumen: Give(It.Shard, rng.RandiRange(1, 2), c); GiveXp(15); break;
            case Tl.Aether: Give(It.Aether, 1, c); GiveXp(25); break;
            case Tl.SilverOre: Give(It.SilverOre, rng.RandiRange(1, 2), c); GiveXp(14); break;
            case Tl.CrystalOre: Give(It.Crystal, rng.RandiRange(1, 2), c); GiveXp(20); break;
            case Tl.Obsidian: Give(It.Obsidian, 1, c); GiveXp(24); break;
            case Tl.Ember: Give(It.Ember, rng.RandiRange(1, 2), c); GiveXp(24); break;
            case Tl.FrostOre: Give(It.Frost, rng.RandiRange(1, 2), c); GiveXp(26); break;
            case Tl.Pine: Give(It.Wood, rng.RandiRange(2, 3), c); GiveXp(3); break;
            case Tl.IceBlock: case Tl.ShadowPillar: GiveXp(1); break;
            case Tl.MeteorOre: Give(It.AncCrystal, 1, c); Give(It.Shard, 3, c + new Vector2(0, -6)); Give(It.Crystal, 1, c + new Vector2(0, -12)); GiveXp(40); Found.Add("Осколок светового метеорита"); break;
        }
        if (chain && ChainLimit > 0)
        {
            int left = ChainLimit; var q = new Queue<Vector2I>(); q.Enqueue(tile);
            while (q.Count > 0 && left > 0)
            {
                var cur = q.Dequeue();
                foreach (var d in World.D4)
                {
                    var n = cur + d;
                    if (left > 0 && W.Get(n.X, n.Y) == t) { BreakTile(n, t, false); q.Enqueue(n); left--; }
                }
            }
        }
    }

    public void Give(int item, int n, Vector2 at)
    {
        Inv[item] += n; Diag.Count("give_" + item);
        Texts.Add(new FText { P = at, S = $"+{n} {It.Names[item]}", Life = 1.1f, C = It.Col[item] });
        if (IsRareItem(item)) RareGiveFx(item, at);
        Sfx.Play("pickup", -6, 1f + .05f * (item % 5));
    }

    public void GiveXp(float x)
    {
        Xp += x;
        while (Xp >= XpNeed)
        {
            Xp -= XpNeed; Level++; MaxHp += 10; Hp = MaxHp;
            Toast($"Уровень {Level}! Здоровье +10, добыча и урон выросли");
            Texts.Add(new FText { P = PPos + new Vector2(0, -16), S = "УРОВЕНЬ!", Life = 1.6f, C = new Color("#ffe27a") });
            Fx("level_up", PPos);
        }
    }

    public void Toast(string s)
    {
        Toasts.Add(new Toast { S = s });
        if (Toasts.Count > 3) Toasts.RemoveAt(0);
    }

    public void Announce(string title, string sub, Color c, string sfx = "discover")
    {
        Banners.Add(new Banner { Title = title, Sub = sub, C = c });
        if (Banners.Count > 3) Banners.RemoveAt(0);
        Sfx.Play(sfx);
    }

    // ───────────────────────── крафт и строительство ─────────────────────────
    public static bool CanPay(int[] inv, int[][] cost) { foreach (var c in cost) if (inv[c[0]] < c[1]) return false; return true; }
    public void Pay(int[][] cost) { Diag.Mark("first_spend"); PayPool(cost); }
    public bool Can(int[][] cost) => CanPool(cost);
    public static int[][] FurnaceCost => CostFurnace;
    public static int[][] LanternCost => CostLantern;
    public int[][] NextPickCost => Tier < 4 ? CostPick[Tier] : null;

    public static string CostStr(int[][] cost)
    {
        var s = new List<string>(); foreach (var c in cost) s.Add($"{c[1]} {It.Ab[c[0]]}"); return string.Join(" ", s);
    }
    public string NeedStr(int[][] cost)
    {
        var s = new List<string>(); foreach (var c in cost) s.Add($"{It.Names[c[0]]} {Math.Min(Inv[c[0]], c[1])}/{c[1]}"); return string.Join(" · ", s);
    }

    public Vector2I? PlaceTarget(bool needLit = false)
    {
        var tile = ToTile(Aim);
        if (PPos.DistanceTo(TileCenter(tile.X, tile.Y)) > 56f) { Fail("Слишком далеко: наведите курсор на свободную клетку рядом"); return null; }
        if (W.Get(tile.X, tile.Y) != Tl.None) { Fail("Клетка занята: наведите курсор на свободную"); return null; }
        if (ToTile(PPos) == tile) { Fail("Нельзя строить под собой"); return null; }
        if (W.Ground[World.Key(tile.X, tile.Y)] == Art.WaterG) { Fail("В воде строить нельзя"); return null; }
        if (needLit && !IsLit(TileCenter(tile.X, tile.Y))) { Fail("Здания строятся только на освещённой земле — расширяйте свет"); return null; }
        return tile;
    }

    void BuildFurnace()
    {
        if (!Can(CostFurnace)) { Fail("Для печи нужно: 10 камня и 5 дерева"); return; }
        var t = PlaceTarget(); if (t == null) return;
        Pay(CostFurnace); SetObj(t.Value.X, t.Value.Y, Tl.Furnace); HasFurnace = true;
        Burst(TileCenter(t.Value.X, t.Value.Y), new Color("#ff9a2e"), 14, 60); Sfx.Play("build");
        GiveXp(20); Toast("Печь построена! Подойдите и нажмите E, чтобы плавить руду (1 руда + 1 дерево)");
        Save();
    }

    bool FurnaceNear(float r, out Vector2 at)
    {
        at = default; var c = ToTile(PPos); int k = (int)(r / TS) + 1; float bd = r + 1; bool f = false;
        for (int y = c.Y - k; y <= c.Y + k; y++)
            for (int x = c.X - k; x <= c.X + k; x++)
                if (W.Get(x, y) == Tl.Furnace) { var p = TileCenter(x, y); float d = p.DistanceTo(PPos); if (d < bd) { bd = d; at = p; f = true; } }
        return f && bd <= r;
    }

    void UpgradePick()
    {
        if (Tier >= 4) { Fail("Кирка уже обсидиановая — лучше не бывает"); return; }
        if (!HasFurnace || !FurnaceNear(56, out _) && !HasBuilding(Tl.Forge)) { Fail("Улучшать кирку можно только рядом с печью"); return; }
        if (!FurnaceNear(56, out _) && BuildingNear(PPos, 56) == null) { Fail("Улучшать кирку можно только рядом с печью или кузницей"); return; }
        var cost = CostPick[Tier];
        if (!Can(cost)) { Fail($"Для «{PickNames[Tier + 1]}» нужно: {NeedStr(cost)}"); return; }
        Pay(cost); Tier++; Diag.Mark("first_tool_upgrade"); GiveXp(40 + 20 * Tier); Shake = 3;
        Burst(PPos, new Color("#ffe27a"), 18, 90); Sfx.Play("upgrade");
        Announce(PickNames[Tier], PickGain[Tier], new Color("#ffe27a"), "upgrade");
        Save();
    }

    void StartSmelt(Vector2 at)
    {
        int loaded = 0;
        while (Inv[It.Wood] > 0 && loaded < 12)
        {
            int ore = Inv[It.CopperOre] > 0 ? It.CopperOre : Inv[It.IronOre] > 0 ? It.IronOre : Inv[It.SilverOre] > 0 ? It.SilverOre : -1;
            if (ore < 0) break;
            Inv[ore]--; Inv[It.Wood]--; SmeltQ.Add(ore); loaded++;
        }
        if (loaded == 0) { Fail("Нужна руда и дерево (топливо): по 1 дереву на слиток"); return; }
        SmeltAt = at; Sfx.Play("build", -4);
        Toast($"Плавка: {loaded} шт.");
    }

    void UpdateSmelting(float dt)
    {
        if (SmeltQ.Count == 0) { smeltT = 0; return; }
        smeltT += dt;
        if (Time % .12f < dt) Burst(SmeltAt + new Vector2(rng.RandfRange(-3, 3), -6), new Color("#ff9a2e"), 1, 18);
        if (smeltT >= SmeltTime)
        {
            smeltT = 0; int ore = SmeltQ[0]; SmeltQ.RemoveAt(0);
            int ingot = It.IngotOf(ore);
            Inv[ingot]++; GiveXp(2); Sfx.Play("tick");
            Texts.Add(new FText { P = SmeltAt + new Vector2(0, -8), S = $"+1 {It.Names[ingot]}", Life = 1.1f, C = It.Col[ingot] });
        }
    }

    // ───────────────────────── древние фонари ─────────────────────────
    void Activate(Ancient a, int idx)
    {
        if (a.Boss && !BossDown(a.BossId)) { Fail("Фонарь охраняет страж — сначала победите его"); return; }
        if (Inv[It.Shard] < a.Cost) { Fail($"Для зажигания нужно осколков света: {a.Cost} (у вас {Inv[It.Shard]}). Их роняют тени и люмен-руда"); return; }
        Inv[It.Shard] -= a.Cost; a.Lit = true; a.Anim = 0; Diag.Mark("first_lantern"); Diag.Count("lanterns_lit");
        SetObj(a.Tile.X, a.Tile.Y, a.Boss ? Tl.BossOn : Tl.AncOn);
        var ex = new List<string>(); var seen = new HashSet<Tl>();
        foreach (var v in a.Veins)
        {
            if (W.Get(v.x, v.y) != Tl.None) continue;
            SetObj(v.x, v.y, v.t); Burst(TileCenter(v.x, v.y), TileColor(v.t), 8, 50);
            if (seen.Add(v.t)) ex.Add(v.t switch { Tl.Iron => "железная руда", Tl.Lumen => "люмен-руда", Tl.Aether => "эфирные кристаллы", Tl.SilverOre => "серебро", Tl.CrystalOre => "кристаллы", Tl.Obsidian => "обсидиан", Tl.Ember => "уголь пепла", _ => "руда" });
        }
        GiveXp(a.Boss ? 120 : 60); Shake = 5; Flash = .7f; TradeEpoch++;
        Burst(a.Pos, new Color("#ffe9a0"), 40, 140); Sfx.Play("ancient");
        string extra = ex.Count > 0 ? "Открыто: " + string.Join(", ", ex) : "Территория освобождена";
        Announce(a.Boss ? "Фонарь стража зажжён!" : $"Древний фонарь #{idx} зажжён!", "Тьма навсегда отступает. " + extra, new Color("#ffe9a0"), "ancient");
        Save();
    }

    void UpdateAncients(float dt)
    {
        foreach (var a in W.Ancients)
        {
            if (a.Lit && a.Anim < 1) { a.Anim = Mathf.Min(1, a.Anim + dt / 3.5f); Shake = Mathf.Max(Shake, 1.5f * (1 - a.Anim)); }
            if (a.TargetRadius > a.RadiusTiles) { a.RadiusTiles = Mathf.Min(a.TargetRadius, a.RadiusTiles + dt * 2.5f); Shake = Mathf.Max(Shake, 1.2f); }
            a.Pulse = Mathf.Max(0, a.Pulse - dt * .4f);
        }
    }

    // ───────────────────────── эффекты ─────────────────────────
    public void Burst(Vector2 p, Color c, int n, float speed)
    {
        for (int i = 0; i < n; i++)
        {
            float a = rng.Randf() * Mathf.Tau, s = rng.RandfRange(.3f, 1f) * speed;
            float life = rng.RandfRange(.3f, .7f);
            Parts.Add(new Particle { P = p, V = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s, Life = life, Max = life, C = c });
        }
        if (Parts.Count > 800) Parts.RemoveRange(0, Parts.Count - 800);
    }

    void UpdateFx(float dt)
    {
        for (int i = Parts.Count - 1; i >= 0; i--)
        {
            var p = Parts[i]; p.Life -= dt; if (p.Life <= 0) { Parts.RemoveAt(i); continue; }
            p.P += p.V * dt; p.V *= Mathf.Max(0, 1 - 4 * dt);
        }
        for (int i = Texts.Count - 1; i >= 0; i--) { var t = Texts[i]; t.Life -= dt; t.P += new Vector2(0, -14 * dt); if (t.Life <= 0) Texts.RemoveAt(i); }
        for (int i = Toasts.Count - 1; i >= 0; i--) { Toasts[i].Life -= dt; if (Toasts[i].Life <= 0) Toasts.RemoveAt(i); }
        for (int i = Banners.Count - 1; i >= 0; i--) { Banners[i].Life -= dt; if (Banners[i].Life <= 0) Banners.RemoveAt(i); }
        // атмосфера биома
        int reg = RegionAt(PPos);
        if (reg != 0 && rng.Randf() < dt * 10)
        {
            var p = PPos + new Vector2(rng.RandfRange(-190, 190), rng.RandfRange(-110, 110));
            if (reg == 1) Parts.Add(new Particle { P = p, V = new Vector2(rng.RandfRange(-4, 4), -rng.RandfRange(4, 12)), Life = 2f, Max = 2f, C = new Color(.55f, .85f, 1f), Size = 1.2f });
            else if (reg == 3) Parts.Add(new Particle { P = p, V = new Vector2(rng.RandfRange(-14, -4), rng.RandfRange(8, 20)), Life = 2.6f, Max = 2.6f, C = new Color(.92f, .97f, 1f), Size = 1.5f });
            else if (reg == 4) Parts.Add(new Particle { P = p, V = new Vector2(rng.RandfRange(-5, 5), -rng.RandfRange(3, 9)), Life = 2.4f, Max = 2.4f, C = rng.Randf() < .5f ? new Color(.75f, .5f, 1f) : new Color(1f, .85f, .4f), Size = 1.3f });
            else Parts.Add(new Particle { P = p, V = new Vector2(rng.RandfRange(-6, 6), rng.Randf() < .25f ? -rng.RandfRange(10, 22) : rng.RandfRange(6, 14)), Life = 2.5f, Max = 2.5f, C = rng.Randf() < .25f ? new Color(1f, .55f, .2f) : new Color(.6f, .58f, .55f), Size = 1.4f });
        }
    }

    // ───────────────────────── компас ─────────────────────────
    public Vector2? CompassTarget()
    {
        Ancient best = null; float bd = float.MaxValue;
        int reg = RegionAt(PPos);
        for (int i = 1; i < W.Ancients.Count; i++) { var a = W.Ancients[i]; if (a.Lit || a.Boss || a.Region != reg) continue; float d = a.Pos.DistanceTo(PPos); if (d < bd) { bd = d; best = a; } }
        if (best == null) foreach (var a in W.Ancients) if (a.Boss && !a.Lit && a.Region == reg && BossDown(a.BossId)) return a.Pos;
        return best?.Pos;
    }
}
