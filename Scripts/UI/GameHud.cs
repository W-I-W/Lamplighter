using Godot;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Lamplighter;

/// <summary>Основной HUD на сцене Scenes/UI/GameHud.tscn: здоровье и опыт, цель, мир, способности, контекстная подсказка.
/// Скрипт только читает состояние игры и заполняет готовые узлы; оформление задаётся сценой и темой Resources/UI/LanternTheme.tres.</summary>
public partial class GameHud : Control
{
    public Game G;
    Control status, goal, world, abilities;
    RichTextLabel goalTitle, goalNeeds, goalMore; Label level, lvCap, hpLabel, biome, lLabel, beacon;
    ProgressBar hp, xp, goalBar, lBar;
    readonly UiSlot[] abil = new UiSlot[5];
    readonly Control[] chips = new Control[5]; readonly TextureRect[] chipIcons = new TextureRect[5];
    readonly int[] lastInv = new int[It.Count]; readonly float[] lastChange = new float[It.Count];
    float flashT; int flashSlot = -1; Control[] panels;
    public bool GoalsOpen;                                      // раскрытый список целей
    static readonly Regex Frac = new(@"(\d+)\s*/\s*(\d+)", RegexOptions.Compiled);
    Texture2D[] abilIcons;
    Control topLeft, topRight, lRow, resRow, xpRow; Label dbg;
    string lastGoal = ""; float goalExpandT;                    // подсказка цели: после смены раскрыта несколько секунд, затем сворачивается в строку
    float lastUiT; bool regionOn;
    public bool GoalCollapsed => !goalBar.Visible;
    public bool RegionVisible => topRight.Visible && world.IsVisibleInTree();
    public bool DebugVisible => dbg.Visible;
    public void ClickGoalForTest() => goal.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { Pressed = true, ButtonIndex = MouseButton.Left });
    public float LeftBottom => Bottom(topLeft);                 // нижняя граница блоков слева и справа (логические координаты) — под ними рисуются реликвии и трекер события
    public float RightBottom => topRight != null && topRight.Visible && world.Visible ? Bottom(topRight) : 4f;
    static float Bottom(Control c) { if (c == null) return 4f; float b = c.Position.Y; foreach (var k in c.GetChildren()) if (k is Control ch && ch.Visible) b = Mathf.Max(b, c.Position.Y + ch.Position.Y + ch.Size.Y); return b; }

    readonly UiSlot[] quickSlots = new UiSlot[Game.QuickSlotCount];
    Control quickWheel;
    float quickWheelTime;
    int wheelHover = -1;
    public bool QuickWheelHeld { get; private set; }

    Control BindQuickSlots()
    {
        var row = GetNode<Control>("QuickSlots");
        quickWheel = row;
        for (int i = 0; i < quickSlots.Length; i++)
        {
            var slot = row.GetNode<UiSlot>($"Quick{i + 1}");
            slot.Index = i;
            slot.Clicked += index => { if (G.Window == Win.None) G.UseQuickSlot(index); };
            slot.RightClicked += index => G.ClearQuickSlot(index);
            quickSlots[i] = slot;
        }
        return row;
    }

    public void BeginQuickWheel()
    {
        QuickWheelHeld = true;
        wheelHover = -1;
        quickWheel.Show();
    }

    public void EndQuickWheel()
    {
        if (!QuickWheelHeld) return;
        QuickWheelHeld = false;
        if (wheelHover >= 0) G.SelectQuickSlot(wheelHover);
        wheelHover = -1;
        quickWheelTime = 0;
        quickWheel.Hide();
    }

    public bool ConfirmQuickWheel(Vector2 viewportPosition)
    {
        if (!quickWheel.IsVisibleInTree()) return false;
        int slot = QuickSlotAt(viewportPosition);
        if (slot < 0) slot = wheelHover;
        if (slot >= 0) G.SelectQuickSlot(slot);
        QuickWheelHeld = false;
        wheelHover = -1;
        quickWheelTime = 0;
        quickWheel.Hide();
        return true;
    }

    public void ShowQuickWheel()
    {
        wheelHover = -1;
        quickWheelTime = 1.2f;
        quickWheel.Show();
    }

    public void PointQuickWheel(Vector2 viewportPosition)
    {
        var point = quickWheel.GetGlobalTransformWithCanvas().AffineInverse() * viewportPosition;
        var direction = point - quickWheel.Size * .5f;
        if (direction.Length() < 16f) { wheelHover = -1; return; }
        float angle = Mathf.PosMod(direction.Angle() + Mathf.Pi / 2f + Mathf.Pi / Game.QuickSlotCount, Mathf.Tau);
        wheelHover = Mathf.FloorToInt(angle / Mathf.Tau * Game.QuickSlotCount);
    }

    internal int QuickSlotAt(Vector2 viewportPosition)
    {
        for (int i = 0; i < quickSlots.Length; i++)
        {
            var slot = quickSlots[i];
            if (slot != null && slot.IsVisibleInTree() &&
                new Rect2(Vector2.Zero, slot.Size).HasPoint(slot.GetGlobalTransform().AffineInverse() * viewportPosition)) return i;
        }
        return -1;
    }

    void UpdateQuickSlots()
    {
        for (int i = 0; i < quickSlots.Length; i++)
        {
            var (item, n) = G.QuickStack(i);
            n -= G.InventoryUi.CarriedCount(true, false, i);
            var slot = quickSlots[i];
            slot.Set(n > 0 ? G.InventoryItemTexture(item) : null, n > 1 ? n.ToString() : "", ((i + 1) % 10).ToString(),
                i == (wheelHover >= 0 && QuickWheelHeld ? wheelHover : G.SelectedQuickSlot) ? SlotState.Selected : n > 0 ? SlotState.Normal : SlotState.Disabled);
            int qi = i; ItemTipHost.Bind(slot, () => { var (it, k) = G.QuickStack(qi); return k > 0 ? G.InventoryItemTip(it, k) : null; });
        }
    }


    public override void _Ready()
    {
        status = GetNode<Control>("%Status"); goal = GetNode<Control>("%Goal"); world = GetNode<Control>("%World"); abilities = GetNode<Control>("%Abilities");
        level = GetNode<Label>("%Level"); lvCap = GetNode<Label>("%LvCap"); hpLabel = GetNode<Label>("%HpLabel"); goalTitle = GetNode<RichTextLabel>("%Title"); goalNeeds = GetNode<RichTextLabel>("%Needs"); goalMore = GetNode<RichTextLabel>("%More");
        biome = GetNode<Label>("%Biome"); lLabel = GetNode<Label>("%LLabel"); beacon = GetNode<Label>("%Beacon");
        hp = GetNode<ProgressBar>("%HpBar"); xp = GetNode<ProgressBar>("%XpBar"); goalBar = GetNode<ProgressBar>("%Bar"); lBar = GetNode<ProgressBar>("%LBar");
        for (int i = 0; i < 5; i++) abil[i] = abilities.GetChild<UiSlot>(i);
        for (int i = 0; i < 5; i++) chips[i] = GetNode<Control>($"%Chip{i}");
        for (int i = 0; i < chips.Length; i++) chipIcons[i] = chips[i].GetNode<TextureRect>("IconMargin/Icon");
        abilIcons = new[] { "dash", "spin", "flare", "pulse", "beam" }.Select(n => GD.Load<Texture2D>($"res://Assets/UI/ability_{n}.png")).ToArray();
        // клик мышью по блоку цели разворачивает и сворачивает подробности (то же, что клавиша G)
        goal.MouseFilter = MouseFilterEnum.Stop; goal.MouseDefaultCursorShape = CursorShape.PointingHand; foreach (var k in goal.FindChildren("*", "Control", true, false)) ((Control)k).MouseFilter = MouseFilterEnum.Ignore;
        goal.GuiInput += e => { if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left) { GoalsOpen = !GoalsOpen; Sfx.Play("tick", -6, 1.3f); goal.AcceptEvent(); } };
        topLeft = GetNode<Control>("TopLeft"); topRight = GetNode<Control>("TopRight"); lRow = lBar.GetParent<Control>(); resRow = chips[0].GetParent<Control>(); xpRow = xp.GetParent<Control>();
        foreach (var k in lRow.GetChildren()) if (k is TextureRect || k is ProgressBar) ((Control)k).Visible = false;      // «Фонари 2/5» — текстом, без полосы
        regionOn = topRight.Visible;
        dbg = GetNode<Label>("DebugText");
        panels = new[] { status, goal, world, abilities, BindQuickSlots() };
    }

    /// <summary>Находится ли точка (в логических координатах интерфейса) над видимой панелью HUD.</summary>
    /// <summary>Свободный по горизонтали промежуток между панелями слева и справа в полосе y0..y1.</summary>
    public (float l, float r) FreeSpan(float y0, float y1, float vsX, float scale)
    {
        float l = 0, r = vsX;
        if (panels == null) return (l, r);
        foreach (var c in panels)
        {
            if (c == null || !c.IsVisibleInTree()) continue;
            var g = c.GetGlobalRect(); g = new Rect2(g.Position / scale, g.Size / scale); if (g.End.Y < y0 || g.Position.Y > y1) continue;
            if (g.GetCenter().X < vsX * .5f) l = Mathf.Max(l, g.End.X); else r = Mathf.Min(r, g.Position.X);
        }
        return (l, r);
    }

    /// <summary>Точка в логических координатах интерфейса над панелью HUD. Раньше логическая точка сравнивалась с прямоугольником в пикселях окна, поэтому при масштабе интерфейса ×2–×3
    /// клик мимо панели (правее и ниже неё) считался кликом по панели и не бил и не добывал — отсюда «иногда не срабатывает левая кнопка».</summary>
    public bool IsOver(Vector2 logical, float scale)
    {
        if (panels == null) return false; var vp = logical * scale;
        foreach (var c in panels) if (c != null && c.IsVisibleInTree() && new Rect2(Vector2.Zero, c.Size).HasPoint(c.GetGlobalTransform().AffineInverse() * vp)) return true;
        return false;
    }

    public void Flash(int slot) { flashSlot = slot; flashT = .9f; }

    static string T(string s) => Loc.T(s);
    public override void _Process(double delta)
    {
        if (G == null || G.W == null) return;
        if (G.Menu != Mn.None || G.Window != Win.None || G.TechOpen || G.MapOpen || G.ChatOpen)
        {
            QuickWheelHeld = false;
            wheelHover = -1;
            quickWheelTime = 0;
            quickWheel.Hide();
        }
        else if (!QuickWheelHeld && quickWheelTime > 0)
        {
            quickWheelTime -= (float)delta;
            if (quickWheelTime <= 0) quickWheel.Hide();
        }
        float dt = (float)delta; flashT = Mathf.Max(0, flashT - dt);
        bool menu = G.Menu == Mn.Main || G.Menu == Mn.Flow;
        Visible = !menu && (G.Window == Win.None || G.Window == Win.Build || G.Window == Win.Inventory || G.Window == Win.Chest) && !G.TechOpen && !G.MapOpen && G.Menu == Mn.None;
        UpdateStatus(); UpdateGoal(dt); UpdateWorld(dt); UpdateAbilities(); UpdateQuickSlots(); UpdateDebug();
    }

    void UpdateStatus()
    {
        level.Text = G.Level.ToString(); lvCap.Text = T("УР");
        hp.Value = Mathf.Clamp(G.Hp / G.MaxHp, 0, 1); hpLabel.Text = $"{(int)G.Hp}";
        xp.Value = Mathf.Clamp(G.Xp / G.XpNeed, 0, 1); xpRow.Visible = !HudDensity.Minimal;
    }

    /// <summary>Цель/подсказка слева: после смены цели раскрыта 8 секунд (название, нужные предметы, полоса), затем сворачивается до одной строки. [G] — развернуть подробности и список целей.</summary>
    void UpdateGoal(float dt)
    {
        var goals = G.Goals; var g = goals.Count > 0 ? goals[0] : null;
        string title = g == null ? "" : g.Title; if (title != lastGoal) { lastGoal = title; goalExpandT = HudDensity.Minimal ? 0f : 8f; }
        dt = Mathf.Max(0f, (float)(G.UiTime - lastUiT)); lastUiT = (float)G.UiTime; goalExpandT = Mathf.Max(0, goalExpandT - dt);      // по игровому времени, как уведомления
        bool open = !HudDensity.Minimal && (GoalsOpen || goalExpandT > 0);
        TextFx.Apply(goalTitle, g == null ? T("Все цели выполнены!") : T(g.Title), open ? new Color("#ffd23f") : new Color("#d9c27a"), 90f);
        bool lite = !HudDensity.Minimal;                          // нужные ресурсы и подсказка клавиши видны всегда; полоса и список целей — только в раскрытом виде
        goalNeeds.Visible = lite && g != null; if (goalNeeds.Visible) { TextFx.Apply(goalNeeds, T(g.Needs), new Color("#8a96b0"), 0); goalNeeds.MouseFilter = MouseFilterEnum.Pass; goalNeeds.TooltipText = It.NamesIn(g.Needs); }
        var m = g != null ? Frac.Match(g.Needs ?? "") : null;
        goalBar.Visible = open && m != null && m.Success && int.Parse(m.Groups[2].Value) > 0;
        if (goalBar.Visible) goalBar.Value = Mathf.Clamp(float.Parse(m.Groups[1].Value) / float.Parse(m.Groups[2].Value), 0, 1);
        string more = "";
        if (GoalsOpen) for (int i = 1; i < Math.Min(4, goals.Count); i++) more += "• " + T(goals[i].Title) + "\n";
        if (GoalsOpen) more += T("[G] свернуть · [J] журнал"); else if (goals.Count > 1) more = string.Format(T("ещё {0} · [G] развернуть"), goals.Count - 1);
        TextFx.Apply(goalMore, more.TrimEnd('\n'), new Color("#8a96b0"), 0); goalMore.Visible = lite && more != "";
    }

    /// <summary>Блок региона: название, «Фонари 2/5», «Маяк 1/6». Ресурсы — только что полученные, несколько секунд; полный список — в инвентаре [I]. В минимальном режиме блока нет.</summary>
    void UpdateWorld(float dt)
    {
        topRight.Visible = regionOn && !HudDensity.Minimal;      // блок региона включается в сцене GameHud.tscn (узел TopRight, Visible)
        int reg = G.RegionAt(G.PPos);
        biome.Text = T(Game.RegionNames[reg]); biome.AddThemeColorOverride("font_color", Game.RegionCol(reg));
        int lit = reg == 0 ? G.LitCount : G.LitIn(reg), total = reg == 0 ? 5 : 3;
        lLabel.Text = $"{T("Фонари")} {lit}/{total}";
        beacon.Text = $"{T("Маяк")} {G.BeaconLevel}/6";
        float now = (float)Time.GetTicksMsec() / 1000f;
        for (int i = 0; i < It.Count; i++) { if (G.Inv[i] != lastInv[i]) { if (lastInv[i] > 0 || G.Inv[i] > 0) lastChange[i] = now; lastInv[i] = G.Inv[i]; } }
        var order = new List<int>(); for (int i = 0; i < It.Count; i++) if (G.Inv[i] > 0 && now - lastChange[i] < 4f) order.Add(i);
        order.Sort((a, b) => lastChange[b].CompareTo(lastChange[a]));
        for (int c = 0; c < chips.Length; c++)
        {
            var chip = chips[c]; bool has = c < order.Count && c < 3; chip.Visible = has; if (!has) continue;
            int it = order[c]; chip.MouseFilter = MouseFilterEnum.Pass; int tipItem = it; ItemTipHost.Bind(chip, () => G.InventoryItemTip(tipItem, G.Inv[tipItem])); chipIcons[c].Texture = UiIcons.Item(it); chip.GetNode<Label>("Label").Text = Short(G.Inv[it]);
        }
        resRow.Visible = order.Count > 0;
    }

    /// <summary>Служебная строка полного режима: кадры в секунду, клетка и регион.</summary>
    void UpdateDebug()
    {
        dbg.Visible = HudDensity.Full; if (!dbg.Visible) return;
        dbg.Text = $"{Engine.GetFramesPerSecond()} fps · {(int)(G.PPos.X / 16)},{(int)(G.PPos.Y / 16)} · {G.RegionAt(G.PPos)} · enemies {G.Enemies.Count}";
    }

    static string Short(int n) => n >= 10000 ? $"{n / 1000}k" : n >= 1000 ? $"{n / 1000f:0.#}k" : n.ToString();

    void UpdateAbilities()
    {
        var list = new (Func<bool> own, float cd, float max)[]
        {
            (() => G.Has(Tech.Dash), G.DashCd, 1.6f), (() => G.Has(Tech.Spin), G.SpinCd, 3f), (() => G.Has(Tech.Flare), G.FlareCd, 8f), (() => G.Has(Tech.Pulse), G.PulseCd, 12f),
            (() => G.Has(Tech.Beam), G.BeamOn || G.beamLockPublic ? 1f : 1f - G.BeamE / 100f, 1f)
        };
        string[] keys = { "⇧", "ПКМ", "Q", "F", "R" };
        keys[0] = KeyMap.Name("Dash"); keys[2] = KeyMap.Name("Flare"); keys[3] = KeyMap.Name("Pulse"); keys[4] = KeyMap.Name("Beam");
        bool any = false;
        for (int i = 0; i < 5; i++)
        {
            bool own = list[i].own(); abil[i].Visible = own; if (!own) continue; any = true;
            float cd = list[i].cd <= 0 ? 0 : Mathf.Clamp(list[i].cd / list[i].max, 0, 1);
            abil[i].Set(abilIcons[i], "", keys[i] == "Shift" ? "Sh" : keys[i].Length > 3 ? keys[i].Substring(0, 3) : keys[i], cd <= 0 ? SlotState.Ok : SlotState.Normal, cd);
        }
        abilities.Visible = any && !HudDensity.Minimal;
    }

}

static class EnumerableExt
{
    public static System.Collections.Generic.IEnumerable<R> Select<T, R>(this T[] a, Func<T, R> f) { foreach (var x in a) yield return f(x); }
    public static R[] ToArray<R>(this System.Collections.Generic.IEnumerable<R> e) { var l = new List<R>(e); return l.ToArray(); }
}
