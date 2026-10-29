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
    /// <summary>Значок предмета как символ шрифта (цветной глиф U+E000+индекс из Assets/UI/Fonts/lantern.png): ставится в тексте перед названием или числом.</summary>
    public static string Ic(int item) => ((char)(0xE000 + item)).ToString();
    /// <summary>Слиток, получаемый из руды (или -1).</summary>
    public static int IngotOf(int ore) => ore == CopperOre ? Copper : ore == IronOre ? Iron : ore == SilverOre ? Silver : -1;
}

public sealed class Particle { public Vector2 P, V; public float Life, Max, Size = 1.5f; public Color C; }
public sealed class FText { public Vector2 P; public string S; public float Life; public Color C; }
public sealed class Banner { public string Title, Sub; public Color C; public float Life = 3.5f, Age; }
public sealed class Proj { public Vector2 Pos, Vel; public float Life = 3f, Dmg; public Color C = new("#ff5ec8"); public bool Fire, Zone, Cold, Dark; }
public sealed class Beacon { public Vector2 Pos; public float T, Dur, R, Ring, Burn; }
public sealed class Building { public Tl Type; public int X, Y, Level = 1, Count, Status, Prio = 1, Mode; public float Prog; public bool Worker, Off, Forced, Linked, Manual; public float WorkMul = .6f; public int[] Local = new int[It.Count]; public Vector2 Pos => Game.TileCenter(X, Y); }
public sealed partial class Ix { public List<string> Lines = new(); public Action E, U, X; public string Title, Key; }


public partial class Game : Node2D
{
    public const int TS = 16;
    public const float ZoomBase = 3f;
    /// <summary>Масштаб мира. Подгоняется под окно так, чтобы пиксель арта занимал целое число пикселей экрана (при дробном масштабе между тайлами мелькают линии).</summary>
    public static float Zoom = ZoomBase;

    public World W;
    int srcId;
    const int LightLimit = 96;                       // должен совпадать с размером массива lights в Assets/Shaders/Lighting/lights_common.gdshaderinc
    EntityLayer ents; OverlayLayer overlay; public bool TestKeepMine; Camera2D cam; ColorRect darkRect, glowRect; ShaderMaterial darkMat, glowMat; Hud hud; Control uiRoot; public GameHud GameHudNode; BuildWindowUI buildMenu; InventoryWindow invWindow; UpgradeWindowUI upgradeWindow;
    public float UiScaleVp = 2f;
    public readonly RandomNumberGenerator rng = new();

    // игрок
    public Vector2 PPos; Vector2 pVel; public int Facing = 1; public float Walk;
    public float Hp = 60, MaxHp = 60, Xp; public int Level = 1, Tier;
    public int[] Inv = new int[It.Count];
    public float Invuln, AtkCd, Swing, MineProg, MineNeed, Shake, Flash, Time;
    float regenDelay, tierMsgCd, rippleT;
    public Vector2I MineTile = new(-1, -1);
    public Vector2 Aim;
    public Ix Cur = new(); public int BuildCat;
    public Vector2 SmeltAt; public List<int> SmeltQ = new(); float smeltT; public float SmeltProg => SmeltQ.Count > 0 ? smeltT / SmeltTime : 0;
    const float SmeltTime = 0.9f;
    public bool HasFurnace;
    public int HeroLampLv;
    public long Ann; public bool TechOpen; public int TechPage;

    public List<Particle> Parts = new();
    public List<FText> Texts = new();
    public readonly GameNotificationService Notify = new();     // единый журнал: все уведомления игры и чат
    public ChatHud Chat; public RegionTitleHud RegionTitle; public bool ChatOpen => Chat != null && Chat.IsOpen;
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

    public float XpNeed => Tune.T.XpBase + Tune.T.XpLinear * Level + Tune.T.XpQuad * Level * Level;        // раньше 25 + 20 × уровень: двадцатый уровень набирался за 10 минут
    public int LitCount { get { int n = 0; for (int i = 1; i < W.Ancients.Count; i++) { var a = W.Ancients[i]; if (!a.Boss && a.Region == 0 && a.Lit) n++; } return n; } }
    public int LitIn(int region) { int n = 0; foreach (var a in W.Ancients) if (a.Region == region && !a.Boss && a.Lit && a != W.Ancients[0]) n++; return n; }
    public Vector2 ViewSize => GetViewportRect().Size;
    /// <summary>Интерфейс масштабируется только целыми множителями физических пикселей (пиксель-перфект); логическая сетка при «Авто» — 640×360.</summary>
    void UpdateUiScale()
    {
        float wh = testing ? ViewSize.Y : DisplayServer.WindowGetSize().Y; int si = Settings.UiInt(wh);
        UiScaleVp = si * ViewSize.Y / Mathf.Max(1f, wh);
        uiRoot.Scale = new Vector2(UiScaleVp, UiScaleVp); uiRoot.Size = ViewSize / UiScaleVp; hud.Size = uiRoot.Size; GameHudNode.Size = uiRoot.Size; if (Chat != null) Chat.Size = uiRoot.Size; if (RegionTitle != null) RegionTitle.Size = uiRoot.Size; if (flow != null && GodotObject.IsInstanceValid(flow)) flow.Size = uiRoot.Size;
    }
    public Vector2 CamCenter => cam.GetScreenCenterPosition();
    public int RegionAt(Vector2 p) => World.RegionOf((int)Mathf.Floor(p.X / TS));
    public bool WaterAt(Vector2 p) { var t = ToTile(p); return World.In(t.X, t.Y) && W.Ground[World.Key(t.X, t.Y)] == Art.WaterG; }
    public float PlayerLightR => (72f + 8 * Tier + 2 * Level) * LampMul * (1f + LensBonus) * (1f + E(Tech.HeroLamp)) * (1f + .12f * HeroLampLv + N("HeroRadius")) * (Surge > 0 ? 1.4f : 1f) * LightDim;

    // ───────────────────────── инициализация ─────────────────────────
    public override void _Ready()
    {
        if (Balance.BalanceCli.Requested()) { Balance.BalanceCli.Run(this); return; }          // симулятор баланса: мир не создаётся, сохранения не читаются
        rng.Randomize();
        Diag.Enabled = OS.GetEnvironment("LAMP_DIAG") != "";
        testing = OS.GetEnvironment("LAMP_AUTOTEST") != ""; testDir = OS.GetEnvironment("LAMP_AUTOTEST");
        Settings.Load(); if (testing && OS.GetEnvironment("LAMP_LANG") != "") { Settings.Lang = OS.GetEnvironment("LAMP_LANG"); Loc.LogMissing = true; }
        Settings.Apply(testing); ThemeFix.Apply(GD.Load<Theme>(Hud.ThemePath)); Ach.Load(); if (Settings.Diag) Diag.Enabled = true; if (Diag.Enabled && testing) Diag.OutPath = System.IO.Path.Combine(testDir, "diagnostics.json");
        Art.Build();
        Db.Init();
        AddChild(new Sfx()); AddChild(new Music());
        testing = OS.GetEnvironment("LAMP_AUTOTEST") != "";
        testDir = OS.GetEnvironment("LAMP_AUTOTEST");
        if (testing) SaveManager.Root = testDir;
        if (testing && !GameSession.Active) TestSession();
        if (!LoadSession()) NewWorld(testing ? 4242 : 1337);                                // без сеанса — декорация главного меню: ничего не сохраняется
        InitNet(); StartNetFromEnv();
        if (Upgrades == null) InitUpgrades(null);
        ApplyBeaconRadius();
        InitVillage();
        InitMap(); InitHotbar();
        InitAsh();

        BuildTileMaps();
        ents = new EntityLayer { G = this };
        AddChild(ents); AddChild(trunkRoot); AddChild(topRoot);                 // порядок: игрок → копии стволов → кроны (крона дерева спереди закрывает ствол дерева позади)
        overlay = new OverlayLayer { G = this }; AddChild(overlay); InitEnemyFx(); InitAmbient();

        cam = new Camera2D { Zoom = new Vector2(Zoom, Zoom), PositionSmoothingEnabled = false, PositionSmoothingSpeed = 9f, Position = PPos };
        cam.LimitLeft = 0; cam.LimitTop = 0; cam.LimitRight = World.Wd * TS; cam.LimitBottom = World.Ht * TS;
        AddChild(cam); cam.MakeCurrent(); cam.ResetSmoothing(); SnapCamera(0);       // до первого _Process: загрузка занимает время, и первый кадр иначе рисуется с дробным зумом (полоски)

        var dl = new CanvasLayer { Layer = 5 }; AddChild(dl);
        var vcat = VisualCatalog.Get();
        darkMat = vcat?.Darkness ?? GD.Load<ShaderMaterial>("res://Assets/Materials/darkness.tres");
        glowMat = vcat?.Glow ?? GD.Load<ShaderMaterial>("res://Assets/Materials/glow.tres");
        darkRect = new ColorRect { Material = darkMat, MouseFilter = Control.MouseFilterEnum.Ignore, Color = Colors.White };
        glowRect = new ColorRect { Material = glowMat, MouseFilter = Control.MouseFilterEnum.Ignore, Color = Colors.White };
        dl.AddChild(darkRect); dl.AddChild(glowRect);

        var hl = new CanvasLayer { Layer = 10 }; AddChild(hl);
        uiRoot = new Control { MouseFilter = Control.MouseFilterEnum.Ignore }; hl.AddChild(uiRoot);
        hud = new Hud { G = this, MouseFilter = Control.MouseFilterEnum.Ignore }; uiRoot.AddChild(hud);
        GameHudNode = GD.Load<PackedScene>("res://Scenes/UI/GameHud.tscn").Instantiate<GameHud>(); GameHudNode.G = this; uiRoot.AddChild(GameHudNode); Chat = new ChatHud { G = this }; uiRoot.AddChild(Chat); RegionTitle = new RegionTitleHud { G = this }; uiRoot.AddChild(RegionTitle);
        buildMenu = GD.Load<PackedScene>("res://Scenes/UI/BuildMenu.tscn").Instantiate<BuildWindowUI>(); buildMenu.G = this; uiRoot.AddChild(buildMenu);
        invWindow = GD.Load<PackedScene>("res://Scenes/UI/InventoryWindow.tscn").Instantiate<InventoryWindow>(); invWindow.G = this; uiRoot.AddChild(invWindow);
        upgradeWindow = GD.Load<PackedScene>("res://Scenes/UI/UpgradeWindow.tscn").Instantiate<UpgradeWindowUI>(); upgradeWindow.G = this; uiRoot.AddChild(upgradeWindow);
        equipWindow = GD.Load<PackedScene>("res://Scenes/UI/EquipWindow.tscn").Instantiate<EquipWindowUI>(); equipWindow.G = this; uiRoot.AddChild(equipWindow);

        if (CarryMsg != null) { pendingMsg ??= CarryMsg; CarryMsg = null; }
        if (pendingMsg != null) Toast(pendingMsg);
        if (pendingMsg2 != null) Toast(pendingMsg2);
        InitLightTech();
        foreach (var ev in pendingEvents) { InitEventState(ev); Events.Add(ev); }
        EventsEnabled = !testing;
        StartMenuOnLaunch();
        if (freshWorld && !testing && Menu == Mn.None && GameSession.Mode != SessionMode.Guest) OpenStory("Фонарщик", string.Join("\n\n", Story.Prologue), "Пролог");
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
            if (t != Tl.None) ch.F.EraseCell(lc);
            if (t == Tl.None || t == Tl.SecretCache) ch.O.EraseCell(lc); else ch.O.SetCell(lc, srcId, new Vector2I(AtlasX(t, LevelAt(x, y)), 0));
        }
        RefreshCrown(x, y); RefreshCrown(x, y - 1);
    }

    // ───────────────────────── сохранение ─────────────────────────
    Vector2 camPos; bool camInit;
    public static string CarryMsg;                                       // сообщение, переживающее перезагрузку сцены
    string pendingMsg, pendingMsg2; readonly List<GEvent> pendingEvents = new();
    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest) { SaveAll(true); Diag.Write(this); }
    }

    // ───────────────────────── ввод ─────────────────────────
    public override void _Input(InputEvent e)
    {
        if (Menu == Mn.Flow) return;                                          // экран выбора героя и мира обрабатывает ввод сам
        if (Menu == Mn.NetJoin && e is InputEventKey nk && nk.Pressed) { NetJoinKey(nk); return; }
        if (Menu != Mn.None) { if (e is InputEventKey mk && mk.Pressed && !mk.Echo) MenuKey(mk.Keycode); else if (e is InputEventMouse me) MenuMouse(me); return; }
        if (ChatOpen) { if (e is InputEventKey ck && ck.Pressed && ck.Keycode == Key.Escape) { Chat.CloseChat(); GetViewport().SetInputAsHandled(); } return; }      // чат открыт: ввод идёт в поле, игровые клавиши и мышь отключены
        if (e is InputEventKey ek && ek.Pressed && !ek.Echo && (ek.Keycode == Key.Enter || ek.Keycode == Key.KpEnter) && Window == Win.None && !TechOpen && !MapOpen && FinStage == 0 && Chat != null) { Chat.OpenChat(); GetViewport().SetInputAsHandled(); return; }
        if (HotDragInput(e)) return;
        if (e is InputEventMouse wm && FinStage == 0 && (Window != Win.None && Window != Win.Build && Window != Win.Inventory && Window != Win.Equip ? WinMouse(wm) : TechOpen && TechMouse(wm))) return;
        if (e is InputEventMouseButton cb && cb.Pressed && cb.ButtonIndex == MouseButton.Left && FinStage == 0)
        {
            OverUi = UiOver(cb.Position / UiScaleVp);
            if (Placing != null && Window == Win.None && !OverUi) { PlaceNow(); return; }
            ClickWorld();
        }
        if (e is InputEventMouseButton pr && pr.Pressed && pr.ButtonIndex == MouseButton.Right && Placing != null) { Placing = null; Toast("Строительство отменено"); return; }
        if (FinStage > 0) { if (e is InputEventKey fk && fk.Pressed && !fk.Echo) FinaleKey(fk.Keycode); return; }
        if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Right && Window == Win.None) TrySpin();
        if (e is InputEventMouseButton wb && wb.Pressed && Window == Win.None && !TechOpen && !MapOpen && (wb.ButtonIndex == MouseButton.WheelUp || wb.ButtonIndex == MouseButton.WheelDown)) { HotCycle(wb.ButtonIndex == MouseButton.WheelUp ? -1 : 1); return; }
        if (e is InputEventKey k && k.Pressed && !k.Echo)
        {
            var key = KeyMap.Translate(k.Keycode); if (key == Key.None) return;
            if (Window != Win.None) { WindowKey(key, k.ShiftPressed); return; }
            if (TechOpen && TechKey(key)) return;
            switch (key)
            {
                case Key.E: DetailT = 3f; Cur.E?.Invoke(); break;
                case Key.X: DetailT = 3f; if (Cur.X != null) Cur.X(); else Fail("Рядом нет построенного предмета, который можно разобрать"); break;
                case Key.U: DetailT = 3f; if (Cur.U != null) Cur.U(); else Fail("Рядом нет здания или фонаря для улучшения"); break;
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
                case Key.T: OpenTech(!TechOpen); Sfx.Play("tick"); break;
                case Key.M: { bool o = !MapOpen; CloseWindows(); MapOpen = o; Sfx.Play("tick"); break; }
                case Key.Escape: if (Placing != null) { Placing = null; Toast("Строительство отменено"); } else if (TechOpen || MapOpen) CloseWindows(); else OpenMenu(Mn.Pause); break;
                case Key.Shift: TryDash(); break;
                case Key.Q: TryFlare(); break;
                case Key.V: if (k.ShiftPressed) CycleOwnedWeapon(); else CycleWeapon(); break;
                case Key.C: OpenWin(Win.Equip); break;
                case Key.J: OpenWin(Win.Journal); break;
                case Key.P: OpenWin(Win.Prod); break;
                case Key.B: OpenWin(Win.Build); break;
                case Key.I: OpenWin(Win.Inventory); break;
                case Key.G: if (GameHudNode != null) GameHudNode.GoalsOpen = !GameHudNode.GoalsOpen; break;
                case Key.H: Toast(HudDensity.Names[(int)HudDensity.Next()]); break;
                case Key.F: TryPulse(); break;
                case Key.F5: Save(); Toast("Игра сохранена"); break;
            }
        }
    }

    void Digit(int n)
    {
        if (TechOpen) { var vis = Upgrades.Visible(); if (n >= 1 && n <= vis.Count) SelectTechPage(vis[n - 1].Page); return; }
        if (n >= 1 && n <= HotCount) { HotSelect(n - 1); return; }                  // 1–3: выбор инструмента или оружия в хотбаре
        switch (n)                                                                  // постройки теперь — только через меню строительства [B]
        {
            case 9: PlaceKey("lamp2"); break;
            case 0: PlaceKey("mirror"); break;
        }
    }

    public void Fail(string msg) { Toast(msg, MsgTone.Warn); Sfx.Play("error"); }

    // ───────────────────────── игровой цикл ─────────────────────────
    public override void _Process(double delta)
    {
        if (W == null) return;
        float dt = Mathf.Min((float)delta, 0.05f);
        Time += dt;
        Aim = aimOverride ?? GetGlobalMousePosition();
        if (testing) AutoTest(dt);
        if (!IsInsideTree()) return;                                            // сцена перезагружена прямо в этом кадре (смена героя или мира)
        UiTime += dt; UpdateMusic(dt); UpdateUiScale();
        if (Menu != Mn.None) { NetJoinTick(); UpdateShaders(dt); ents.QueueRedraw(); overlay?.QueueRedraw(); breakArea?.QueueRedraw(); hud.QueueRedraw(); return; }
        PlaySecs += dt; achT -= dt; if (achT <= 0) { achT = 2f; Ach.Check(this); }
        if (FinStage >= 2) { UpdateFinale(dt); UpdateShaders(dt); ents.QueueRedraw(); overlay?.QueueRedraw(); breakArea?.QueueRedraw(); hud.QueueRedraw(); return; }
        if (FinStage == 1) UpdateFinale(dt);

        UpdatePlayer(dt);
        UpdateAncients(dt);
        UpdateLamps(dt);
        UpdateBeams(dt);
        UpdateWeaponFx(dt);
        UpdateIceBlocks(dt);
        UpdateHeart(dt);
        UpdateEndgame(dt);
        UpdateRegrow(dt); UpdateScarcity(dt);
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
        UpdateBossSpawn(dt); Upgrades?.Tick(dt); UpdateNet(dt);
        UpdateSpawning(dt);
        UpdateVillage(dt);
        UpdatePois(dt); UpdateRunes(dt); UpdateAncientKinds(dt); LampHints();
        UpdateSmelting(dt);
        UpdateRegion(dt);
        UpdateMap(dt);
        UpdateFx(dt);
        UpdateUnlocks(dt);
        UpdateUiScale();
        OverUi = UiOver(GetViewport().GetMousePosition() / UiScaleVp);
        RunInteraction(); UpdateTip(dt);
        UpdateSelectionVisual(dt);

        saveT += dt; if (saveT > 10) { saveT = 0; Save(); }

        Shake = Mathf.Max(0, Shake - dt * 14); Flash = Mathf.Max(0, Flash - dt * 1.2f);
        SnapCamera(dt);
        UpdateShaders(dt); UpdateVegetation(dt); UpdateEnemyVisuals(dt); UpdatePlayerFx(dt); UpdateWorldLife(dt); ambientLayer?.QueueRedraw();
        ents.QueueRedraw(); overlay?.QueueRedraw(); breakArea?.QueueRedraw(); hud.QueueRedraw();
    }

    void SnapCamera(float dt)
    {
        // Камера плавно следует за игроком и стоит строго на сетке физических пикселей: дробное положение давало светлые полоски между тайлами
        // масштаб окна по осям берём из итогового преобразования: размер вьюпорта округляется, и X-масштаб чуть отличается от Y — отсюда вертикальные/горизонтальные щели между тайлами
        var ft = GetViewport().GetFinalTransform(); float scX = Mathf.Max(.01f, ft.X.X), winScale = Mathf.Max(.01f, ft.Y.Y);
        float pxPerUnit = Mathf.Max(1f, Mathf.Round(ZoomBase * winScale)); Zoom = pxPerUnit / winScale; cam.Zoom = new Vector2(pxPerUnit / scX, Zoom);
        if (!camInit || camPos.DistanceTo(PPos) > 160f) { camPos = PPos; camInit = true; } else camPos = camPos.Lerp(PPos, 1f - Mathf.Exp(-9f * dt));
        // центр кадра в физических пикселях может быть дробным (ширина окна / масштаб не целая) — подгоняем камеру под эту дробь, иначе края тайлов падают на полпикселя
        var ctr = new Vector2(ViewSize.X * scX, ViewSize.Y * winScale) * .5f; var fr = new Vector2(ctr.X - Mathf.Floor(ctr.X), ctr.Y - Mathf.Floor(ctr.Y));
        cam.Position = ((camPos * pxPerUnit - fr).Round() + fr) / pxPerUnit;
        var shakeOff = Shake > 0 ? new Vector2(rng.RandfRange(-1, 1), rng.RandfRange(-1, 1)) * Shake * Settings.Shake : Vector2.Zero;
        cam.Offset = (shakeOff * pxPerUnit).Round() / pxPerUnit;
        if (backdrop != null) { var half = ViewSize / Zoom * .5f + new Vector2(32, 32); backdrop.Position = cam.Position - half; backdrop.Size = half * 2; }      // подложка под землёй следует за камерой
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
    /// <summary>Ближайшая свободная точка рядом с pos (до 4 клеток) — если существо оказалось внутри камня или поставленного блока.</summary>
    Vector2? NearestFree(Vector2 pos, float r, bool phase)
    {
        for (float rad = 2f; rad <= 64f; rad += 2f)
            for (int k = 0; k < 16; k++) { var q = pos + Vector2.FromAngle(k * Mathf.Tau / 16f) * rad; if (!Collides(q, r, phase)) return q; }
        return null;
    }

    public Vector2 Move(Vector2 pos, Vector2 d, float r, bool phase)
    {
        if (Collides(pos, r, phase)) { var free = NearestFree(pos, r, phase); if (free != null) pos = free.Value; }        // застрявший (блок поставили на него, толчок в камень) выталкивается наружу
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
        bool uiOpen = Window != Win.None || TechOpen || MapOpen || Menu != Mn.None || FinStage != 0 || ChatOpen;
        if (uiOpen) dir = Vector2.Zero;                                  // пока открыто окно или меню, персонаж стоит на месте
        dir = dir.Normalized(); LastMoveDir = dir;
        bool water = WaterAt(PPos);
        float speed = 80f * (1f + N("Speed")) * (water ? WaterSpeed : 1f);
        if (DashT > 0)
        {
            DashT -= dt; pVel = DashDir * 260f;
            if (rng.Randf() < dt * 60) Burst(PPos, new Color("#8ff0ff"), 1, 20);
        }
        else
        {
            if (uiOpen) { pVel = Vector2.Zero; speed = 0; }
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
            else if (regenDelay <= 0) Hp = Mathf.Min(MaxHp, Hp + RegenPerSec * dt);
        }

        bool act = Input.IsMouseButtonPressed(MouseButton.Left) || KeyMap.Held("Attack");
        if (testing && aimOverride != null) act = false;
        if (Placing != null || OverUi || ChatOpen) act = false;                       // режим строительства и клики по интерфейсу не бьют и не добывают
        if (act) Act(dt); else if (!TestKeepMine) { MineProg = 0; MineTile = new Vector2I(-1, -1); }
    }

    float MineSpeed => (1f + .7f * Tier) * (1f + .06f * (Level - 1)) * (1f + N("MineSpd"));
    public float PlayerDamage => (2f + 2f * Tier + .4f * (Level - 1)) * (1f + N("Dmg"));

    static bool Mineable(Tl t) => t == Tl.Tree || t == Tl.Stone || t == Tl.Copper || t == Tl.Iron || t == Tl.Lumen || t == Tl.Aether || t == Tl.Ruin
        || t == Tl.CrystalOre || t == Tl.SilverOre || t == Tl.Rubble || t == Tl.DeadTree || t == Tl.Obsidian || t == Tl.Ember || t == Tl.MeteorOre || t == Tl.FrostOre || t == Tl.Pine || t == Tl.IceBlock || t == Tl.ShadowPillar;

    /// <summary>Что можно ломать оружием в руках (очень медленно): мягкие блоки. Руда и кристаллы только инструментом.</summary>
    static bool WeaponCanBreak(Tl t) => t == Tl.Tree || t == Tl.DeadTree || t == Tl.Pine || t == Tl.Stone || t == Tl.Ruin || t == Tl.Rubble || t == Tl.IceBlock || t == Tl.IceRuin;

    static int TierNeed(Tl t) => t switch
    {
        Tl.Iron => 1, Tl.MeteorOre => 1, Tl.FrostOre => 3, Tl.Lumen => 2, Tl.Aether => 2, Tl.CrystalOre => 2, Tl.SilverOre => 2, Tl.Obsidian => 3, Tl.Ember => 3, _ => 0
    };

    void ActPick(float dt, bool melee)
    {
        var toAim = Aim - PPos; var dir = toAim.LengthSquared() > 1 ? toAim.Normalized() : new Vector2(Facing, 0);
        var td = CurW < 0 ? CurToolDef : ToolDef.Get("pickaxe");           // с оружием в руках добыча идёт кирой, как и раньше
        Enemy best = null; float bd = 30f + td.AttackRange;
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
            if (AtkCd <= 0) { AtkCd = .42f * td.AttackInterval; Swing = .22f; Hurt(best, PlayerDamage * td.CombatDamage, dir, true); Sfx.Play("hit"); Diag.Count("attack_melee"); }
            return;
        }
        var tile = ToTile(Aim);
        var t = W.Get(tile.X, tile.Y);
        if (t == Tl.None)                                                    // клик по кроне дерева — это удар по стволу под ней; кусты ломаются сразу
        {
            var bt = W.Get(tile.X, tile.Y + 1);
            if (bt == Tl.Tree || bt == Tl.Pine || bt == Tl.DeadTree) { tile = new Vector2I(tile.X, tile.Y + 1); t = bt; }
            else if (IsBushAt(tile.X, tile.Y)) { ActBush(tile, dt, td); return; }
        }
        if (PPos.DistanceTo(TileCenter(tile.X, tile.Y)) > 40f || !Mineable(t))
        { MineProg = 0; MineTile = new Vector2I(-1, -1); return; }
        if (CurW >= 0 && !WeaponCanBreak(t))
        {
            if (tierMsgCd <= 0) { tierMsgCd = 2; Fail("Оружием ломаются только дерево, камень и завалы: для руды возьмите кирку"); }
            MineProg = 0; return;
        }
        if (Tier < TierNeed(t))
        {
            if (tierMsgCd <= 0)
            {
                tierMsgCd = 2;
                Fail(t == Tl.Iron ? "Железо не берётся. Улучшите кирку у печи [E]" : TierNeed(t) == 3 ? "Эта руда требует кристальной кирки" : "Эта руда требует железной кирки");
            }
            MineProg = 0; return;
        }
        float eff = td.Efficiency(t) * td.MiningPower * (CurW >= 0 ? Tune.T.WeaponMineMul : 1f);        // оружием блоки ломаются очень медленно: для добычи нужен инструмент
        if (eff <= 0f)
        {
            if (tierMsgCd <= 0) { tierMsgCd = 2; Fail($"{td.Name} не подходит для добычи: {TileTitle(t)}"); }
            MineProg = 0; MineTile = new Vector2I(-1, -1); return;
        }
        if (tile != MineTile) { MineTile = tile; MineProg = 0; }
        MineNeed = t switch { Tl.Tree => 2.2f, Tl.DeadTree => 1.6f, Tl.Stone => 1.5f, Tl.Ruin => 1.8f, Tl.Rubble => 1.2f, Tl.Copper => 1.8f, Tl.Iron => 2.6f, Tl.SilverOre => 3f, Tl.Lumen => 3.2f, Tl.CrystalOre => 3.6f, Tl.Obsidian => 4.2f, Tl.Ember => 4f, Tl.MeteorOre => 3f, Tl.FrostOre => 4f, Tl.Pine => 2.2f, Tl.IceBlock => 1.1f, Tl.ShadowPillar => 1.8f, _ => 4f };
        MineNeed *= Tune.T.MineTimeMul;                                    // начало игры: добыча медленнее, чтобы сырьё не набиралось слишком легко
        float before = MineProg;
        MineProg += dt * MineSpeed * eff; Swing = .2f; Diag.Add("mine_secs", dt);
        if ((int)(before * 5) != (int)(MineProg * 5)) { Fx("mine_hit", TileCenter(tile.X, tile.Y), TileColor(t), t == Tl.Tree ? .73f : 1f); hitFlash = 1f; TileShake(tile.X, tile.Y); }
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

    void BreakTile(Vector2I tile, Tl t, bool chain) { inBreak = true; try { BreakTileCore(tile, t, chain); } finally { inBreak = false; } }

    void BreakTileCore(Vector2I tile, Tl t, bool chain)
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

    bool inBreak;
    /// <summary>Выпадение сырья при добыче блоков: множитель из tuning.tres, дробная часть — случайно, минимум 1 (осколки света, реликвии и ядра не меняются).</summary>
    int Yield(int item, int n)
    {
        if (item == It.Shard || item == It.Relic || item == It.Core || item == It.AncCrystal || item == It.Coins) return n;
        float v = n * Tune.T.GatherMul; int i = (int)v; if (rng.Randf() < v - i) i++; return Math.Max(1, i);
    }

    public void Give(int item, int n, Vector2 at)
    {
        if (inBreak) n = Yield(item, n);
        Inv[item] += n; Diag.Count("give_" + item);
        Texts.Add(new FText { P = at, S = $"{It.Ic(item)}+{n} {It.Names[item]}", Life = 1.1f, C = It.Col[item] });
        if (IsRareItem(item)) RareGiveFx(item, at);
        Sfx.Play("pickup", -6, 1f + .05f * (item % 5));
    }

    public void GiveXp(float x, bool raw = false)
    {
        Xp += raw ? x : x * Tune.T.XpMul;
        while (Xp >= XpNeed)
        {
            Xp -= XpNeed; Level++; MaxHp += 10; Hp = MaxHp;
            Toast($"Уровень {Level}! Здоровье +10, добыча и урон выросли", MsgTone.Gold);
            Texts.Add(new FText { P = PPos + new Vector2(0, -16), S = "УРОВЕНЬ!", Life = 1.6f, C = new Color("#ffe27a") });
            Fx("level_up", PPos);
        }
    }

    /// <summary>Короткое сообщение игроку: попадает в журнал в левом нижнем углу (см. ChatHud), отдельных панелей нет.</summary>
    public void Toast(string s, MsgTone tone = MsgTone.Info) => Notify.Post(MsgKind.System, tone, Loc.T(s));

    /// <summary>Событие игры: строка в журнале (цвет по смыслу звука/названия). Опасность боя и победы над стражами дополнительно показываются коротким баннером сверху.</summary>
    public void Announce(string title, string sub, Color c, string sfx = "discover")
    {
        bool important = sfx == "boss" || title.Contains("повержен") || title.Contains("Свет погас");
        var tone = important ? (sfx == "boss" ? MsgTone.Warn : MsgTone.Gold) : sfx switch
        {
            "build" or "tech" or "upgrade" => MsgTone.Gold, "ancient" or "discover" => MsgTone.Cyan, "chest" or "rescue" or "levelup" => MsgTone.Good, "error" => MsgTone.Warn, _ => MsgTone.Info
        };
        bool world = sfx == "ancient" || sfx == "discover";
        Notify.Post(important ? MsgKind.Important : world ? MsgKind.World : MsgKind.System, tone, Loc.T(title), null, important ? 2 : 1);
        if (important)
        {
            Banners.Add(new Banner { Title = title, Sub = sub, C = c, Life = Mathf.Min(5f, 3f + .03f * (title.Length + sub.Length)) });
            if (Banners.Count > 3) Banners.RemoveAt(1);
        }
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
        var s = new List<string>(); foreach (var c in cost) s.Add($"{It.Ic(c[0])}{c[1]}"); return string.Join(" ", s);
    }
    /// <summary>Компактная запись для целей: значок и «есть/нужно» без названий.</summary>
    public string NeedShort(int[][] cost)
    {
        var s = new List<string>(); foreach (var c in cost) s.Add($"{It.Ic(c[0])}{Math.Min(Inv[c[0]], c[1])}/{c[1]}"); return string.Join(" ", s);
    }
    public string NeedStr(int[][] cost)
    {
        var s = new List<string>(); foreach (var c in cost) s.Add($"{It.Ic(c[0])}{It.Names[c[0]]} {Math.Min(Inv[c[0]], c[1])}/{c[1]}"); return string.Join(" · ", s);
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
        GiveXp(20); Toast("Печь готова! Плавка: [E] (руда + дерево)");
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
            Texts.Add(new FText { P = SmeltAt + new Vector2(0, -8), S = $"{It.Ic(ingot)}+1 {It.Names[ingot]}", Life = 1.1f, C = It.Col[ingot] });
        }
    }

    // ───────────────────────── древние фонари ─────────────────────────
    void Activate(Ancient a, int idx)
    {
        if (a.Boss && !BossDown(a.BossId)) { Fail("Фонарь охраняет страж — сначала победите его"); return; }
        if (!LampReady(a, out var lampWhy)) { Fail(lampWhy); return; }
        if (Inv[It.Shard] < a.Cost) { Fail($"Нужно осколков: {a.Cost} (есть {Inv[It.Shard]})"); return; }
        Inv[It.Shard] -= a.Cost; PayRepair(a); a.Lit = true; a.Anim = 0; OnAncientLit(a); Diag.Mark("first_lantern"); Diag.Count("lanterns_lit");
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
        n = n > 0 ? Mathf.Max(Settings.Particles > .05f ? 1 : 0, Mathf.RoundToInt(n * Settings.Particles)) : 0;
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
        // пока открыто окно, меню или карта, сообщения не гаснут: игрок успевает их прочитать
        float msgDt = Window != Win.None || TechOpen || MapOpen || Menu != Mn.None ? 0f : dt;
        Notify.Tick(msgDt);
        if (Banners.Count > 0) { var cb = Banners[0]; cb.Age += msgDt; cb.Life -= msgDt * (Banners.Count > 1 ? 1.5f : 1f); if (cb.Life <= 0) Banners.RemoveAt(0); }       // по одному, по очереди
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
