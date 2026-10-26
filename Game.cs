using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Lamplighter;

public static class It
{
    public const int Wood = 0, Stone = 1, CopperOre = 2, IronOre = 3, Shard = 4, Copper = 5, Iron = 6, Count = 7;
    public static readonly string[] Names = { "Дерево", "Камень", "Медная руда", "Железная руда", "Осколок света", "Медь", "Железо" };
}

public sealed class Enemy
{
    public Vector2 Pos, Kb, DashDir;
    public float Hp, MaxHp, Atk, Flash, Phase, Dash, DashCd = 1.5f, Tele, Age;
    public bool Wraith;
}
public sealed class Particle { public Vector2 P, V; public float Life, Max, Size = 1.5f; public Color C; }
public sealed class FText { public Vector2 P; public string S; public float Life; public Color C; }
public sealed class Lamp { public Vector2 Pos; public float Anim; }
public sealed class Toast { public string S; public float Life = 4.5f; }

public sealed class SaveData
{
    public int Seed; public string Obj; public bool[] Lit;
    public float Px, Py, Hp, MaxHp, Xp; public int Level, Tier; public int[] Inv;
}

public partial class Game : Node2D
{
    public const int TS = 16;
    public const float Zoom = 3f;
    static bool forceNew;

    public World W;
    TileMapLayer groundL, objL; int srcId;
    EntityLayer ents; Camera2D cam; ColorRect darkRect, glowRect; ShaderMaterial darkMat, glowMat; Hud hud;
    readonly RandomNumberGenerator rng = new();

    // игрок
    public Vector2 PPos; Vector2 pVel; public int Facing = 1; public float Walk;
    public float Hp = 60, MaxHp = 60, Xp; public int Level = 1, Tier;
    public int[] Inv = new int[It.Count];
    public float Invuln, AtkCd, Swing, MineProg, MineNeed, Shake, Flash, Time;
    float regenDelay, tierMsgCd;
    public Vector2I MineTile = new(-1, -1);
    public Vector2 Aim;
    public string Prompt = "";
    public Vector2 SmeltAt; public List<int> SmeltQ = new(); float smeltT; public float SmeltProg => SmeltQ.Count > 0 ? smeltT / SmeltTime : 0;
    const float SmeltTime = 0.9f;
    public bool HasFurnace;

    public List<Enemy> Enemies = new();
    public List<Particle> Parts = new();
    public List<FText> Texts = new();
    public List<Toast> Toasts = new();
    public List<Lamp> Lamps = new();
    float spawnT = 4, saveT;
    public int Kills;

    // автотест (отладка): LAMP_AUTOTEST=1 — скриптованный прогон со скриншотами
    string testDir; int testStep; float testT; bool testing;
    Vector2? aimOverride;

    static readonly int[][] CostFurnace = { new[] { It.Stone, 10 }, new[] { It.Wood, 5 } };
    static readonly int[][] CostLantern = { new[] { It.Wood, 4 }, new[] { It.Copper, 2 } };
    static readonly int[][][] CostPick = {
        new[] { new[] { It.Copper, 5 }, new[] { It.Wood, 5 } },
        new[] { new[] { It.Iron, 6 }, new[] { It.Shard, 3 } } };
    public static readonly string[] PickNames = { "Деревянная кирка", "Медная кирка", "Железная кирка" };
    public const int MaxLamps = 40;
    const float LampR = 96, PlayerLightBase = 72;

    public float XpNeed => 25 + 20 * Level;
    public int LitCount { get { int n = 0; for (int i = 1; i < W.Ancients.Count; i++) if (W.Ancients[i].Lit) n++; return n; } }
    public Vector2 ViewSize => GetViewportRect().Size;
    public Vector2 CamCenter => cam.GetScreenCenterPosition();
    public float PlayerLightR => PlayerLightBase + 8 * Tier + 2 * Level;

    // ───────────────────────── инициализация ─────────────────────────
    public override void _Ready()
    {
        rng.Randomize();
        Art.Build();
        testing = OS.GetEnvironment("LAMP_AUTOTEST") != "";
        testDir = OS.GetEnvironment("LAMP_AUTOTEST");
        if (!LoadGame()) NewWorld(testing ? 4242 : (int)(rng.Randi() % 1000000));

        BuildTileMaps();
        ents = new EntityLayer { G = this };
        AddChild(ents);

        cam = new Camera2D { Zoom = new Vector2(Zoom, Zoom), PositionSmoothingEnabled = true, PositionSmoothingSpeed = 9f, Position = PPos };
        cam.LimitLeft = 0; cam.LimitTop = 0; cam.LimitRight = World.N * TS; cam.LimitBottom = World.N * TS;
        AddChild(cam); cam.MakeCurrent(); cam.ResetSmoothing();

        var dl = new CanvasLayer { Layer = 5 }; AddChild(dl);
        darkMat = new ShaderMaterial { Shader = new Shader { Code = Fx.Dark } };
        glowMat = new ShaderMaterial { Shader = new Shader { Code = Fx.Glow } };
        darkRect = new ColorRect { Material = darkMat, MouseFilter = Control.MouseFilterEnum.Ignore, Color = Colors.White };
        glowRect = new ColorRect { Material = glowMat, MouseFilter = Control.MouseFilterEnum.Ignore, Color = Colors.White };
        dl.AddChild(darkRect); dl.AddChild(glowRect);

        var hl = new CanvasLayer { Layer = 10 }; AddChild(hl);
        hud = new Hud { G = this, MouseFilter = Control.MouseFilterEnum.Ignore };
        hl.AddChild(hud);

        Toast("Соберите дерево и камень (ЛКМ), постройте печь [1]. Тьма вокруг опасна — держитесь у маяка.");
        GD.Print($"[Lamplighter] seed={W.Seed} ancients={W.Ancients.Count}");
    }

    void NewWorld(int seed)
    {
        W = World.Generate(seed);
        PPos = new Vector2((W.Spawn.X + .5f) * TS, (W.Spawn.Y + .5f) * TS);
    }

    void BuildTileMaps()
    {
        var ts = new TileSet { TileSize = new Vector2I(TS, TS) };
        var src = new TileSetAtlasSource { Texture = Art.TileTex, TextureRegionSize = new Vector2I(TS, TS) };
        for (int i = 0; i < Art.TileCount; i++) src.CreateTile(new Vector2I(i, 0));
        srcId = ts.AddSource(src);
        groundL = new TileMapLayer { TileSet = ts }; objL = new TileMapLayer { TileSet = ts };
        AddChild(groundL); AddChild(objL);
        HasFurnace = false;
        for (int y = 0; y < World.N; y++)
            for (int x = 0; x < World.N; x++)
            {
                groundL.SetCell(new Vector2I(x, y), srcId, new Vector2I(W.Ground[y * World.N + x], 0));
                var t = W.Get(x, y);
                if (t != Tl.None) objL.SetCell(new Vector2I(x, y), srcId, new Vector2I(AtlasX(t), 0));
                if (t == Tl.Furnace) HasFurnace = true;
                if (t == Tl.Lantern) Lamps.Add(new Lamp { Pos = TileCenter(x, y), Anim = 1 });
            }
    }

    static int AtlasX(Tl t) => t switch
    {
        Tl.Tree => Art.Tree, Tl.Stone => Art.Stone, Tl.Copper => Art.Copper, Tl.Iron => Art.Iron, Tl.Lumen => Art.Lumen,
        Tl.AncOff => Art.AncOff, Tl.AncOn => Art.AncOn, Tl.Furnace => Art.Furnace, Tl.Lantern => Art.Lantern, _ => Art.Cliff
    };

    public static Vector2 TileCenter(int x, int y) => new((x + .5f) * TS, (y + .5f) * TS);
    static Vector2I ToTile(Vector2 p) => new((int)Mathf.Floor(p.X / TS), (int)Mathf.Floor(p.Y / TS));

    void SetObj(int x, int y, Tl t)
    {
        W.Set(x, y, t);
        if (t == Tl.None) objL.EraseCell(new Vector2I(x, y));
        else objL.SetCell(new Vector2I(x, y), srcId, new Vector2I(AtlasX(t), 0));
    }

    // ───────────────────────── сохранение ─────────────────────────
    static string SavePath => ProjectSettings.GlobalizePath("user://save.json");

    public void Save()
    {
        if (testing) return;
        try
        {
            // завершаем плавку, чтобы ничего не терялось
            while (SmeltQ.Count > 0) { Inv[SmeltQ[0] == It.CopperOre ? It.Copper : It.Iron]++; SmeltQ.RemoveAt(0); }
            var d = new SaveData
            {
                Seed = W.Seed, Obj = Convert.ToBase64String(W.Obj), Lit = new bool[W.Ancients.Count],
                Px = PPos.X, Py = PPos.Y, Hp = Hp, MaxHp = MaxHp, Xp = Xp, Level = Level, Tier = Tier, Inv = Inv
            };
            for (int i = 0; i < W.Ancients.Count; i++) d.Lit[i] = W.Ancients[i].Lit;
            File.WriteAllText(SavePath, JsonSerializer.Serialize(d, new JsonSerializerOptions { IncludeFields = true }));
        }
        catch (Exception e) { GD.PushWarning("save failed: " + e.Message); }
    }

    bool LoadGame()
    {
        if (testing || forceNew) { forceNew = false; return false; }
        try
        {
            if (!File.Exists(SavePath)) return false;
            var d = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(SavePath), new JsonSerializerOptions { IncludeFields = true });
            W = World.Generate(d.Seed);
            var obj = Convert.FromBase64String(d.Obj);
            if (obj.Length != W.Obj.Length) return false;
            W.Obj = obj;
            for (int i = 0; i < W.Ancients.Count && i < d.Lit.Length; i++) if (d.Lit[i]) { W.Ancients[i].Lit = true; W.Ancients[i].Anim = 1; }
            PPos = new Vector2(d.Px, d.Py); Hp = d.Hp; MaxHp = d.MaxHp; Xp = d.Xp; Level = d.Level; Tier = d.Tier;
            if (d.Inv != null) for (int i = 0; i < Inv.Length && i < d.Inv.Length; i++) Inv[i] = d.Inv[i];
            return true;
        }
        catch (Exception e) { GD.PushWarning("load failed: " + e.Message); return false; }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest) Save();
    }

    void NewGame()
    {
        try { if (File.Exists(SavePath)) File.Delete(SavePath); } catch { }
        forceNew = true;
        GetTree().ReloadCurrentScene();
    }

    // ───────────────────────── ввод ─────────────────────────
    public override void _Input(InputEvent e)
    {
        if (e is InputEventKey k && k.Pressed && !k.Echo)
        {
            switch (k.Keycode)
            {
                case Key.E: Interact(); break;
                case Key.Key1: BuildFurnace(); break;
                case Key.Key2: BuildLantern(); break;
                case Key.Key3: UpgradePick(); break;
                case Key.F5: Save(); Toast("Игра сохранена"); break;
                case Key.F9: NewGame(); break;
            }
        }
    }

    // ───────────────────────── игровой цикл ─────────────────────────
    public override void _Process(double delta)
    {
        float dt = Mathf.Min((float)delta, 0.05f);
        Time += dt;
        Aim = aimOverride ?? GetGlobalMousePosition();
        if (testing) AutoTest(dt);

        UpdatePlayer(dt);
        UpdateAncients(dt);
        foreach (var l in Lamps) if (l.Anim < 1) l.Anim = Mathf.Min(1, l.Anim + dt / 1.2f);
        UpdateEnemies(dt);
        UpdateSmelting(dt);
        UpdateSpawning(dt);
        UpdateFx(dt);
        UpdatePrompt();

        saveT += dt; if (saveT > 10) { saveT = 0; Save(); }

        Shake = Mathf.Max(0, Shake - dt * 14); Flash = Mathf.Max(0, Flash - dt * 1.2f);
        cam.Position = PPos;
        cam.Offset = Shake > 0 ? new Vector2(rng.RandfRange(-1, 1), rng.RandfRange(-1, 1)) * Shake : Vector2.Zero;
        UpdateShaders();
        ents.QueueRedraw(); hud.QueueRedraw();
    }

    // ───────────────────────── свет ─────────────────────────
    static float Ease(float t) => 1 - Mathf.Pow(1 - Mathf.Clamp(t, 0, 1), 3);
    public float AncientR(Ancient a) => a.RadiusTiles * TS * Ease(a.Anim);
    float LampRad(Lamp l) => LampR * Ease(l.Anim);

    /// <summary>Находится ли точка в постоянном свете (маяк/фонарь) — там тьма не властна.</summary>
    public bool IsLit(Vector2 p)
    {
        foreach (var a in W.Ancients) if (a.Lit && p.DistanceTo(a.Pos) < AncientR(a) * .85f) return true;
        foreach (var l in Lamps) if (p.DistanceTo(l.Pos) < LampRad(l) * .85f) return true;
        return false;
    }

    void UpdateShaders()
    {
        var vs = ViewSize;
        darkRect.Size = vs; glowRect.Size = vs; darkRect.Position = glowRect.Position = Vector2.Zero;
        var arr = new Vector4[Fx.MaxLights]; int n = 0;
        float fl = 1f + .05f * Mathf.Sin(Time * 11f) + .03f * Mathf.Sin(Time * 23f + 1f);
        arr[n++] = new Vector4(PPos.X, PPos.Y, PlayerLightR * fl, 0);
        foreach (var a in W.Ancients) if (a.Lit && n < Fx.MaxLights) arr[n++] = new Vector4(a.Pos.X, a.Pos.Y, AncientR(a), a.Anim < 1 ? 1 - a.Anim : 0);
        foreach (var l in Lamps) if (n < Fx.MaxLights) arr[n++] = new Vector4(l.Pos.X, l.Pos.Y, LampRad(l) * (1f + .02f * Mathf.Sin(Time * 9f + l.Pos.X)), 0);
        foreach (var m in new[] { darkMat, glowMat })
        {
            m.SetShaderParameter("cam", CamCenter);
            m.SetShaderParameter("view", vs);
            m.SetShaderParameter("zoom", Zoom);
            m.SetShaderParameter("time", Time);
            m.SetShaderParameter("count", n);
            m.SetShaderParameter("lights", arr);
            m.SetShaderParameter("flash", Flash);
        }
    }

    // ───────────────────────── игрок ─────────────────────────
    bool Solid(int tx, int ty, bool phase) { var t = W.Get(tx, ty); return phase ? t == Tl.Cliff : t != Tl.None; }
    bool Collides(Vector2 p, float r, bool phase)
    {
        int x0 = (int)Mathf.Floor((p.X - r) / TS), x1 = (int)Mathf.Floor((p.X + r) / TS);
        int y0 = (int)Mathf.Floor((p.Y - r) / TS), y1 = (int)Mathf.Floor((p.Y + r) / TS);
        for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) if (Solid(x, y, phase)) return true;
        return false;
    }
    Vector2 Move(Vector2 pos, Vector2 d, float r, bool phase)
    {
        var p = pos; p.X += d.X; if (Collides(p, r, phase)) p.X = pos.X;
        var q = p; q.Y += d.Y; if (Collides(q, r, phase)) q.Y = p.Y;
        return q;
    }

    void UpdatePlayer(float dt)
    {
        Invuln = Mathf.Max(0, Invuln - dt); AtkCd = Mathf.Max(0, AtkCd - dt); Swing = Mathf.Max(0, Swing - dt);
        regenDelay = Mathf.Max(0, regenDelay - dt); tierMsgCd = Mathf.Max(0, tierMsgCd - dt);

        var dir = Vector2.Zero;
        if (!testing || aimOverride == null)
        {
            if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) dir.Y -= 1;
            if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) dir.Y += 1;
            if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) dir.X -= 1;
            if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) dir.X += 1;
        }
        dir = dir.Normalized();
        pVel = pVel.Lerp(dir * 80f, Mathf.Min(1, dt * 16f));
        PPos = Move(PPos, pVel * dt, 5f, false);
        if (pVel.LengthSquared() > 100) Walk += dt * 9f;
        if (Mathf.Abs(Aim.X - PPos.X) > 2) Facing = Aim.X < PPos.X ? -1 : 1;

        // регенерация: быстро в свете, медленно в темноте
        if (Hp < MaxHp)
        {
            if (IsLit(PPos)) Hp = Mathf.Min(MaxHp, Hp + 4f * dt);
            else if (regenDelay <= 0) Hp = Mathf.Min(MaxHp, Hp + .5f * dt);
        }

        bool act = Input.IsMouseButtonPressed(MouseButton.Left) || Input.IsKeyPressed(Key.Space);
        if (testing && aimOverride != null) act = false;
        if (act) Act(dt); else { MineProg = 0; MineTile = new Vector2I(-1, -1); }
    }

    float MineSpeed => (1f + .7f * Tier) * (1f + .06f * (Level - 1));
    public float PlayerDamage => 2f + 2f * Tier + .4f * (Level - 1);

    void Act(float dt)
    {
        var toAim = Aim - PPos; var dir = toAim.LengthSquared() > 1 ? toAim.Normalized() : new Vector2(Facing, 0);
        // 1) враг рядом — бьём
        Enemy best = null; float bd = 30f;
        foreach (var e in Enemies)
        {
            float d = e.Pos.DistanceTo(PPos);
            if (d < bd && (d < 14 || (e.Pos - PPos).Normalized().Dot(dir) > .1f)) { bd = d; best = e; }
        }
        if (best != null)
        {
            MineProg = 0; MineTile = new Vector2I(-1, -1);
            if (AtkCd <= 0) { AtkCd = .42f; Swing = .22f; Hurt(best, PlayerDamage, dir); }
            return;
        }
        // 2) добыча тайла под курсором
        var tile = ToTile(Aim);
        var t = W.Get(tile.X, tile.Y);
        if (PPos.DistanceTo(TileCenter(tile.X, tile.Y)) > 40f || !(t == Tl.Tree || t == Tl.Stone || t == Tl.Copper || t == Tl.Iron || t == Tl.Lumen))
        { MineProg = 0; MineTile = new Vector2I(-1, -1); return; }
        int need = t == Tl.Iron ? 1 : t == Tl.Lumen ? 2 : 0;
        if (Tier < need)
        {
            if (tierMsgCd <= 0) { tierMsgCd = 2; Toast(t == Tl.Iron ? "Железо не берётся этой киркой — улучшите её до медной [3]" : "Люмен-руда требует железной кирки"); }
            MineProg = 0; return;
        }
        if (tile != MineTile) { MineTile = tile; MineProg = 0; }
        MineNeed = t switch { Tl.Tree => 1.1f, Tl.Stone => 1.5f, Tl.Copper => 1.8f, Tl.Iron => 2.6f, _ => 3.2f };
        float before = MineProg;
        MineProg += dt * MineSpeed; Swing = .2f;
        if ((int)(before * 5) != (int)(MineProg * 5)) Burst(TileCenter(tile.X, tile.Y), TileColor(t), 3, 40);
        if (MineProg >= MineNeed) BreakTile(tile, t);
    }

    static Color TileColor(Tl t) => t switch
    {
        Tl.Tree => new Color("#3b8a3a"), Tl.Stone => new Color("#a0a0ac"), Tl.Copper => new Color("#e58a3d"),
        Tl.Iron => new Color("#cbd6e8"), Tl.Lumen => new Color("#5fe4ff"), _ => Colors.White
    };

    void BreakTile(Vector2I tile, Tl t)
    {
        SetObj(tile.X, tile.Y, Tl.None);
        MineProg = 0; MineTile = new Vector2I(-1, -1);
        var c = TileCenter(tile.X, tile.Y);
        Burst(c, TileColor(t), 10, 70);
        switch (t)
        {
            case Tl.Tree: Give(It.Wood, rng.RandiRange(2, 3), c); GiveXp(3); break;
            case Tl.Stone: Give(It.Stone, rng.RandiRange(1, 2), c); GiveXp(4); break;
            case Tl.Copper: Give(It.CopperOre, rng.RandiRange(1, 2), c); GiveXp(6); break;
            case Tl.Iron: Give(It.IronOre, rng.RandiRange(1, 2), c); GiveXp(10); break;
            case Tl.Lumen: Give(It.Shard, rng.RandiRange(1, 2), c); GiveXp(15); break;
        }
    }

    void Give(int item, int n, Vector2 at)
    {
        Inv[item] += n;
        Texts.Add(new FText { P = at, S = $"+{n} {It.Names[item]}", Life = 1.1f, C = item == It.Shard ? new Color("#8ff0ff") : Colors.White });
    }

    void GiveXp(float x)
    {
        Xp += x;
        while (Xp >= XpNeed)
        {
            Xp -= XpNeed; Level++; MaxHp += 10; Hp = MaxHp;
            Toast($"Уровень {Level}! Здоровье +10, добыча и урон выросли");
            Texts.Add(new FText { P = PPos + new Vector2(0, -16), S = "УРОВЕНЬ!", Life = 1.6f, C = new Color("#ffe27a") });
            Burst(PPos, new Color("#ffe27a"), 16, 90);
        }
    }

    public void Toast(string s)
    {
        Toasts.Add(new Toast { S = s });
        if (Toasts.Count > 3) Toasts.RemoveAt(0);
    }

    // ───────────────────────── крафт и строительство ─────────────────────────
    public static bool CanPay(int[] inv, int[][] cost) { foreach (var c in cost) if (inv[c[0]] < c[1]) return false; return true; }
    void Pay(int[][] cost) { foreach (var c in cost) Inv[c[0]] -= c[1]; }
    public bool Can(int[][] cost) => CanPay(Inv, cost);
    public static int[][] FurnaceCost => CostFurnace;
    public static int[][] LanternCost => CostLantern;
    public int[][] NextPickCost => Tier < 2 ? CostPick[Tier] : null;

    Vector2I? PlaceTarget()
    {
        var tile = ToTile(Aim);
        if (PPos.DistanceTo(TileCenter(tile.X, tile.Y)) > 56f) { Toast("Слишком далеко: наведите курсор на свободную клетку рядом"); return null; }
        if (W.Get(tile.X, tile.Y) != Tl.None) { Toast("Клетка занята: наведите курсор на свободную"); return null; }
        if (ToTile(PPos) == tile) { Toast("Нельзя строить под собой"); return null; }
        return tile;
    }

    void BuildFurnace()
    {
        if (!Can(CostFurnace)) { Toast("Для печи нужно: 10 камня и 5 дерева"); return; }
        var t = PlaceTarget(); if (t == null) return;
        Pay(CostFurnace); SetObj(t.Value.X, t.Value.Y, Tl.Furnace); HasFurnace = true;
        Burst(TileCenter(t.Value.X, t.Value.Y), new Color("#ff9a2e"), 14, 60);
        GiveXp(20); Toast("Печь построена! Подойдите и нажмите E, чтобы плавить руду (1 руда + 1 дерево)");
        Save();
    }

    void BuildLantern()
    {
        if (!Can(CostLantern)) { Toast("Для фонаря нужно: 4 дерева и 2 слитка меди"); return; }
        if (Lamps.Count >= MaxLamps) { Toast("Достигнут предел фонарей"); return; }
        var t = PlaceTarget(); if (t == null) return;
        Pay(CostLantern); SetObj(t.Value.X, t.Value.Y, Tl.Lantern);
        Lamps.Add(new Lamp { Pos = TileCenter(t.Value.X, t.Value.Y), Anim = 0 });
        Burst(TileCenter(t.Value.X, t.Value.Y), new Color("#ffd86b"), 14, 70);
        GiveXp(25); Save();
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
        if (Tier >= 2) { Toast("Кирка уже железная — лучше не бывает"); return; }
        if (!HasFurnace || !FurnaceNear(56, out _)) { Toast("Улучшать кирку можно только рядом с печью"); return; }
        var cost = CostPick[Tier];
        if (!Can(cost)) { Toast(Tier == 0 ? "Для медной кирки: 5 слитков меди и 5 дерева" : "Для железной кирки: 6 слитков железа и 3 осколка света"); return; }
        Pay(cost); Tier++; GiveXp(40); Shake = 3;
        Burst(PPos, new Color("#ffe27a"), 18, 90);
        Toast(Tier == 1 ? $"{PickNames[Tier]}: добыча быстрее, урон выше, берёт железную руду" : $"{PickNames[Tier]}: добыча ещё быстрее, берёт люмен-руду");
        Save();
    }

    void Interact()
    {
        for (int i = 1; i < W.Ancients.Count; i++)
        {
            var a = W.Ancients[i];
            if (!a.Lit && a.Pos.DistanceTo(PPos) < 44) { Activate(a, i); return; }
        }
        if (FurnaceNear(44, out var f)) { StartSmelt(f); return; }
        Toast("Рядом нет ничего, с чем можно взаимодействовать");
    }

    void StartSmelt(Vector2 at)
    {
        int loaded = 0;
        while (Inv[It.Wood] > 0 && loaded < 12 && (Inv[It.CopperOre] > 0 || Inv[It.IronOre] > 0))
        {
            int ore = Inv[It.CopperOre] > 0 ? It.CopperOre : It.IronOre;
            Inv[ore]--; Inv[It.Wood]--; SmeltQ.Add(ore); loaded++;
        }
        if (loaded == 0) { Toast("Нужна руда и дерево (топливо): по 1 дереву на слиток"); return; }
        SmeltAt = at;
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
            int ingot = ore == It.CopperOre ? It.Copper : It.Iron;
            Inv[ingot]++; GiveXp(2);
            Texts.Add(new FText { P = SmeltAt + new Vector2(0, -8), S = $"+1 {It.Names[ingot]}", Life = 1.1f, C = Colors.White });
        }
    }

    void Activate(Ancient a, int idx)
    {
        if (Inv[It.Shard] < a.Cost) { Toast($"Для зажигания нужно осколков света: {a.Cost} (у вас {Inv[It.Shard]}). Их роняют тени и люмен-руда"); return; }
        Inv[It.Shard] -= a.Cost; a.Lit = true; a.Anim = 0;
        SetObj(a.Tile.X, a.Tile.Y, Tl.AncOn);
        bool iron = false, lum = false;
        foreach (var v in a.Veins)
        {
            if (W.Get(v.x, v.y) != Tl.None) continue;
            SetObj(v.x, v.y, v.t); Burst(TileCenter(v.x, v.y), TileColor(v.t), 8, 50);
            if (v.t == Tl.Iron) iron = true; else lum = true;
        }
        GiveXp(60); Shake = 5; Flash = .7f;
        Burst(a.Pos, new Color("#ffe9a0"), 40, 140);
        string extra = iron && lum ? " Открыты железная и люмен-руда!" : iron ? " Открыта железная руда!" : lum ? " Открыта люмен-руда!" : "";
        Toast(LitCount >= 5 ? "Все древние фонари зажжены — Тьма отступила!" : $"Древний фонарь #{idx} зажжён, Тьма навсегда отступает.{extra}");
        Save();
    }

    void UpdateAncients(float dt)
    {
        foreach (var a in W.Ancients)
            if (a.Lit && a.Anim < 1) { a.Anim = Mathf.Min(1, a.Anim + dt / 3.5f); Shake = Mathf.Max(Shake, 1.5f * (1 - a.Anim)); }
    }

    void UpdatePrompt()
    {
        Prompt = "";
        for (int i = 1; i < W.Ancients.Count; i++)
        {
            var a = W.Ancients[i];
            if (!a.Lit && a.Pos.DistanceTo(PPos) < 44) { Prompt = $"[E] Зажечь древний фонарь ({a.Cost} осколков света)"; return; }
        }
        if (FurnaceNear(44, out _)) Prompt = SmeltQ.Count > 0 ? "[E] Добавить в плавку" : "[E] Плавить руду";
    }

    // ───────────────────────── враги ─────────────────────────
    float DangerScale => 1f + .18f * LitCount;

    void UpdateSpawning(float dt)
    {
        if (IsLit(PPos)) return;
        float nearUnlit = float.MaxValue;
        for (int i = 1; i < W.Ancients.Count; i++) if (!W.Ancients[i].Lit) nearUnlit = Mathf.Min(nearUnlit, W.Ancients[i].Pos.DistanceTo(PPos));
        bool guarded = nearUnlit < 9 * TS;
        int cap = 3 + LitCount * 2 + (guarded ? 3 : 0); cap = Math.Min(cap, 12);
        spawnT -= dt;
        if (spawnT > 0) return;
        spawnT = rng.RandfRange(2.2f, 3.8f) * (guarded ? .55f : 1f);
        if (Enemies.Count >= cap) return;
        for (int tries = 0; tries < 12; tries++)
        {
            float a = rng.Randf() * Mathf.Tau, d = rng.RandfRange(130, 200);
            var p = PPos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
            var t = ToTile(p);
            if (W.Get(t.X, t.Y) != Tl.None || IsLit(p)) continue;
            SpawnEnemy(p, rng.Randf() < .3f + .08f * LitCount);
            return;
        }
    }

    void SpawnEnemy(Vector2 p, bool wraith)
    {
        float hp = (wraith ? 3.5f : 7f) * DangerScale;
        Enemies.Add(new Enemy { Pos = p, Wraith = wraith, Hp = hp, MaxHp = hp, Phase = rng.Randf() * 6.28f });
    }

    void Hurt(Enemy e, float dmg, Vector2 dir)
    {
        e.Hp -= dmg; e.Flash = .15f; e.Kb += dir * (e.Wraith ? 160f : 120f);
        Burst(e.Pos, new Color("#c070ff"), 6, 70);
        Texts.Add(new FText { P = e.Pos + new Vector2(0, -8), S = ((int)Mathf.Round(dmg)).ToString(), Life = .6f, C = new Color("#ffd0d0") });
        Shake = Mathf.Max(Shake, 1.2f);
    }

    void UpdateEnemies(float dt)
    {
        for (int i = Enemies.Count - 1; i >= 0; i--)
        {
            var e = Enemies[i];
            e.Age += dt; e.Flash = Mathf.Max(0, e.Flash - dt); e.Atk = Mathf.Max(0, e.Atk - dt);
            e.Kb *= Mathf.Max(0, 1 - 9 * dt);
            var to = PPos - e.Pos; float dist = to.Length(); var dir = dist > .01f ? to / dist : Vector2.Zero;

            if (dist > 380) { Enemies.RemoveAt(i); continue; }

            bool lit = IsLit(e.Pos);
            if (lit)
            {
                e.Hp -= 5f * dt; e.Flash = .05f;
                if (rng.Randf() < dt * 20) Burst(e.Pos, new Color("#ffe9a0"), 1, 30);
                if (e.Hp <= 0) { Burst(e.Pos, new Color("#ffe9a0"), 10, 60); Enemies.RemoveAt(i); continue; }
            }
            float k = lit ? .5f : 1f;

            Vector2 vel;
            if (!e.Wraith) vel = dir * (36f + 2f * LitCount) * k;
            else
            {
                e.DashCd -= dt;
                if (e.Tele > 0) { e.Tele -= dt; vel = Vector2.Zero; if (e.Tele <= 0) { e.Dash = .32f; e.DashDir = dir; } }
                else if (e.Dash > 0) { e.Dash -= dt; vel = e.DashDir * 170f * k; }
                else
                {
                    var perp = new Vector2(-dir.Y, dir.X);
                    vel = (dir + perp * Mathf.Sin(Time * 3f + e.Phase) * .8f).Normalized() * 58f * k;
                    if (e.DashCd <= 0 && dist < 100) { e.Tele = .35f; e.DashCd = rng.RandfRange(2.2f, 3.2f); }
                }
            }
            e.Pos = Move(e.Pos, (vel + e.Kb) * dt, 5f, e.Wraith);

            // отталкивание друг от друга
            foreach (var o in Enemies) if (o != e) { var dd = e.Pos - o.Pos; float l = dd.Length(); if (l > .01f && l < 10) e.Pos += dd / l * (10 - l) * .3f; }

            // атака игрока
            float reach = e.Wraith ? (e.Dash > 0 ? 12 : 8) : 12;
            if (dist < reach && e.Atk <= 0 && Invuln <= 0 && (!e.Wraith || e.Dash > 0))
            {
                e.Atk = .9f;
                HurtPlayer((e.Wraith ? 7f : 9f) * DangerScale, -dir);
            }
            else if (!e.Wraith && dist < 12 && e.Atk <= 0 && Invuln <= 0) { e.Atk = .9f; HurtPlayer(9f * DangerScale, -dir); }

            if (e.Hp <= 0) { KillEnemy(e); Enemies.RemoveAt(i); }
        }
    }

    void KillEnemy(Enemy e)
    {
        Kills++; GiveXp(e.Wraith ? 12 : 15);
        Burst(e.Pos, new Color("#a060ff"), 14, 90);
        float chance = e.Wraith ? .45f : .7f;
        if (rng.Randf() < chance) Give(It.Shard, 1, e.Pos);
    }

    void HurtPlayer(float dmg, Vector2 kb)
    {
        Hp -= dmg; Invuln = .6f; regenDelay = 4f; Shake = 3f;
        pVel += kb * 120f;
        Burst(PPos, new Color("#ff5050"), 8, 80);
        Texts.Add(new FText { P = PPos + new Vector2(0, -12), S = "-" + (int)Mathf.Round(dmg), Life = .8f, C = new Color("#ff6060") });
        if (Hp <= 0)
        {
            Enemies.Clear();
            int lost = Inv[It.Shard] / 2; Inv[It.Shard] -= lost;
            PPos = new Vector2((W.Spawn.X + .5f) * TS, (W.Spawn.Y + .5f) * TS); Hp = MaxHp; Invuln = 1.5f;
            Toast(lost > 0 ? $"Тьма поглотила вас. Вы очнулись у маяка (потеряно осколков: {lost})" : "Тьма поглотила вас. Вы очнулись у маяка");
            cam.ResetSmoothing();
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
        if (Parts.Count > 600) Parts.RemoveRange(0, Parts.Count - 600);
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
    }

    // ───────────────────────── подсказки HUD ─────────────────────────
    public Vector2? CompassTarget()
    {
        Ancient best = null; float bd = float.MaxValue;
        for (int i = 1; i < W.Ancients.Count; i++) { var a = W.Ancients[i]; if (a.Lit) continue; float d = a.Pos.DistanceTo(PPos); if (d < bd) { bd = d; best = a; } }
        return best?.Pos;
    }

    public string Objective()
    {
        if (!HasFurnace) return "Цель: добудьте 10 камня и 5 дерева и постройте печь [1]";
        if (Tier == 0)
        {
            if (Inv[It.Copper] >= 5 && Inv[It.Wood] >= 5) return "Цель: у печи нажмите [3] — улучшить кирку";
            return "Цель: добудьте медную руду и переплавьте у печи [E] — нужно 5 слитков меди и 5 дерева";
        }
        int lit = LitCount;
        if (lit == 0)
        {
            int need = W.Ancients[1].Cost;
            return Inv[It.Shard] >= need ? "Цель: идите к древнему фонарю (стрелка) и зажгите его [E]" : $"Цель: убивайте теней во Тьме — нужно {need} осколков света, затем к фонарю (стрелка)";
        }
        if (Tier == 1) return "Цель: добудьте железо у зажжённого фонаря, переплавьте (6 слитков) и улучшите кирку [3]";
        if (lit < 5) { int need = W.Ancients[lit + 1].Cost; return $"Цель: зажгите древний фонарь {lit + 1}/5 — осколков света: {Inv[It.Shard]}/{need}"; }
        return "Все древние фонари зажжены — мир освещён. Расширяйте свет своими фонарями!";
    }

    // ───────────────────────── отрисовка сущностей ─────────────────────────
    public void DrawEntities(EntityLayer c)
    {
        // подсветка тайла-цели и прогресс добычи
        var at = ToTile(Aim);
        if (PPos.DistanceTo(TileCenter(at.X, at.Y)) <= 40f && W.Get(at.X, at.Y) != Tl.None && W.Get(at.X, at.Y) != Tl.Cliff)
            c.DrawRect(new Rect2(at.X * TS, at.Y * TS, TS, TS), new Color(1, 1, 1, .25f), false, 1f);
        if (MineTile.X >= 0 && MineProg > 0)
        {
            var o = new Vector2(MineTile.X * TS, MineTile.Y * TS + TS + 1);
            c.DrawRect(new Rect2(o, new Vector2(TS, 2)), new Color(0, 0, 0, .7f));
            c.DrawRect(new Rect2(o, new Vector2(TS * Mathf.Clamp(MineProg / MineNeed, 0, 1), 2)), new Color("#ffe27a"));
        }
        if (SmeltQ.Count > 0)
        {
            var o = SmeltAt + new Vector2(-8, 9);
            c.DrawRect(new Rect2(o, new Vector2(16, 2)), new Color(0, 0, 0, .7f));
            c.DrawRect(new Rect2(o, new Vector2(16 * SmeltProg, 2)), new Color("#ff9a2e"));
        }

        // сортировка по Y: враги и игрок
        var list = new List<(float y, Action a)>();
        foreach (var e in Enemies) { var ee = e; list.Add((e.Pos.Y, () => DrawEnemy(c, ee))); }
        list.Add((PPos.Y, () => DrawPlayer(c)));
        list.Sort((a, b) => a.y.CompareTo(b.y));
        foreach (var l in list) l.a();

        foreach (var p in Parts)
        {
            var col = p.C; col.A = Mathf.Clamp(p.Life / p.Max * 1.5f, 0, 1);
            c.DrawRect(new Rect2(p.P - new Vector2(p.Size, p.Size) * .5f, new Vector2(p.Size, p.Size)), col);
        }
    }

    void Shadow(EntityLayer c, Vector2 p, float r)
    {
        c.DrawSetTransform(p + new Vector2(0, 6), 0, new Vector2(1, .4f));
        c.DrawCircle(Vector2.Zero, r, new Color(0, 0, 0, .35f));
        c.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }

    void DrawPlayer(EntityLayer c)
    {
        Shadow(c, PPos, 5);
        if (Invuln > 0 && ((int)(Time * 20) % 2 == 0)) return;
        int f = pVel.LengthSquared() > 100 ? (int)Walk % 2 : 0;
        float bob = pVel.LengthSquared() > 100 ? -Mathf.Abs(Mathf.Sin(Walk)) : 0;
        c.DrawSetTransform(PPos + new Vector2(0, bob), 0, new Vector2(Facing, 1));
        c.DrawTextureRectRegion(Art.PlayerTex, new Rect2(-8, -11, 16, 16), new Rect2(f * 16, 0, 16, 16));
        c.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        if (Swing > 0)
        {
            var d = (Aim - PPos); d = d.LengthSquared() > 1 ? d.Normalized() : new Vector2(Facing, 0);
            float t = 1 - Swing / .22f; float ang = d.Angle() - 1.0f + t * 2.0f;
            var a = PPos + new Vector2(0, -3); var b = a + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 13;
            Color head = Tier == 0 ? new Color("#c9b08a") : Tier == 1 ? new Color("#e58a3d") : new Color("#dfe8f6");
            c.DrawLine(a, b, new Color("#6b4423"), 1.5f);
            c.DrawLine(b + new Vector2(Mathf.Cos(ang + 1.57f), Mathf.Sin(ang + 1.57f)) * 3, b - new Vector2(Mathf.Cos(ang + 1.57f), Mathf.Sin(ang + 1.57f)) * 3, head, 2f);
        }
    }

    void DrawEnemy(EntityLayer c, Enemy e)
    {
        Shadow(c, e.Pos, 5);
        var tex = e.Wraith ? Art.WraithTex : Art.ShadeTex;
        int f = (int)(Time * 4 + e.Phase) % 2;
        float bob = Mathf.Sin(Time * 4 + e.Phase) * 1.2f - (e.Wraith ? 2 : 0);
        float a = Mathf.Clamp(e.Age * 2f, 0, 1);
        Color mod = e.Flash > 0 ? new Color(1.8f, .6f, .6f, a) : new Color(1, 1, 1, a);
        if (e.Tele > 0 && ((int)(Time * 25) % 2 == 0)) mod = new Color(1.5f, 1.5f, 2f, a);
        int face = PPos.X < e.Pos.X ? -1 : 1;
        c.DrawSetTransform(e.Pos + new Vector2(0, bob), 0, new Vector2(face, 1));
        c.DrawTextureRectRegion(tex, new Rect2(-8, -10, 16, 16), new Rect2(f * 16, 0, 16, 16), mod);
        c.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        if (e.Hp < e.MaxHp)
        {
            var o = e.Pos + new Vector2(-6, -14);
            c.DrawRect(new Rect2(o, new Vector2(12, 1.5f)), new Color(0, 0, 0, .7f));
            c.DrawRect(new Rect2(o, new Vector2(12 * Mathf.Clamp(e.Hp / e.MaxHp, 0, 1), 1.5f)), new Color("#ff5ec8"));
        }
    }

    // ───────────────────────── автотест ─────────────────────────
    void Shot(string name)
    {
        var img = GetViewport().GetTexture().GetImage();
        img.SavePng(Path.Combine(testDir, name + ".png"));
        GD.Print($"[test] shot {name}");
    }

    void AutoTest(float dt)
    {
        testT += dt;
        void Log(string s) => GD.Print($"[test t={testT:F1}] {s}");
        switch (testStep)
        {
            case 0 when testT > 1.2f: Shot("1_start"); testStep++; break;
            case 1 when testT > 1.5f:
                Inv[It.Wood] = 30; Inv[It.Stone] = 30; Inv[It.CopperOre] = 6; Inv[It.IronOre] = 0;
                aimOverride = Aim = PPos + new Vector2(24, 0); BuildFurnace();
                Log($"furnace={HasFurnace} wood={Inv[It.Wood]} stone={Inv[It.Stone]}");
                Interact(); Log($"smelt queue={SmeltQ.Count}");
                testStep++; break;
            case 2 when testT > 8.5f:
                Log($"copper ingots={Inv[It.Copper]} xp={Xp} lvl={Level}");
                Inv[It.Copper] += 6; UpgradePick(); Log($"tier={Tier}");
                aimOverride = Aim = PPos + new Vector2(-24, 0); BuildLantern(); Log($"lamps={Lamps.Count}");
                testStep++; break;
            case 3 when testT > 9.5f:
                Shot("2_lamp"); Inv[It.Shard] = 10;
                PPos = W.Ancients[1].Pos + new Vector2(0, 30); cam.ResetSmoothing();
                SpawnEnemy(PPos + new Vector2(60, 10), false); SpawnEnemy(PPos + new Vector2(-50, 30), true);
                testStep++; break;
            case 4 when testT > 11f: Shot("3_dark_enemies"); Interact(); Log($"lit={LitCount}"); testStep++; break;
            case 5 when testT > 12.2f: Shot("4_reveal_mid"); testStep++; break;
            case 6 when testT > 16f:
                Shot("5_reveal_end"); Log($"obj iron nearby: {CountTiles(Tl.Iron)}");
                Enemies.Clear(); SpawnEnemy(PPos + new Vector2(30, 0), false);
                aimOverride = null; testStep++; break;
            case 7 when testT > 17f:
                // боевой тест: удары по ближайшему врагу
                foreach (var e in Enemies) Hurt(e, PlayerDamage, Vector2.Right);
                Log($"enemy hp={(Enemies.Count > 0 ? Enemies[0].Hp : -1)}");
                testStep++; break;
            case 8 when testT > 17.5f:
                Save2Test(); GetTree().Quit(); break;
        }
    }

    int CountTiles(Tl t) { int n = 0; foreach (var b in W.Obj) if (b == (byte)t) n++; return n; }

    void Save2Test()
    {
        GD.Print($"[test] done: tier={Tier} lvl={Level} xp={Xp:F0} kills={Kills} lit={LitCount} inv=[{string.Join(",", Inv)}]");
    }
}
