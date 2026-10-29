using Godot;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Lamplighter;

/// <summary>Основной HUD на сцене Scenes/UI/GameHud.tscn: здоровье и опыт, цель, мир, хотбар, способности, контекстная подсказка.
/// Скрипт только читает состояние игры и заполняет готовые узлы; оформление задаётся сценой и темой Resources/UI/LanternTheme.tres.</summary>
public partial class GameHud : Control
{
    public Game G;
    Control status, goal, world, hint, detail, abilities;
    RichTextLabel goalTitle, goalNeeds, goalMore; Label level, lvCap, hpLabel, biome, lLabel, beacon, hintLabel, dTitle, dBody;
    ProgressBar hp, xp, goalBar, lBar;
    UiSlot weapon; readonly UiSlot[] hot = new UiSlot[Game.HotCount]; readonly UiSlot[] abil = new UiSlot[5];
    readonly Control[] chips = new Control[5];
    readonly int[] lastInv = new int[It.Count]; readonly float[] lastChange = new float[It.Count];
    float flashT, hintA; int flashSlot = -1; Control[] panels;
    public bool GoalsOpen;                                      // раскрытый список целей
    static readonly Regex Frac = new(@"(\d+)\s*/\s*(\d+)", RegexOptions.Compiled);
    Texture2D[] abilIcons;

    public override void _Ready()
    {
        status = GetNode<Control>("%Status"); goal = GetNode<Control>("%Goal"); world = GetNode<Control>("%World"); hint = GetNode<Control>("%Hint"); detail = GetNode<Control>("%Detail"); abilities = GetNode<Control>("%Abilities");
        level = GetNode<Label>("%Level"); lvCap = GetNode<Label>("%LvCap"); hpLabel = GetNode<Label>("%HpLabel"); goalTitle = GetNode<RichTextLabel>("%Title"); goalNeeds = GetNode<RichTextLabel>("%Needs"); goalMore = GetNode<RichTextLabel>("%More");
        biome = GetNode<Label>("%Biome"); lLabel = GetNode<Label>("%LLabel"); beacon = GetNode<Label>("%Beacon"); hintLabel = GetNode<Label>("%HintLabel"); dTitle = GetNode<Label>("%DTitle"); dBody = GetNode<Label>("%DBody");
        hp = GetNode<ProgressBar>("%HpBar"); xp = GetNode<ProgressBar>("%XpBar"); goalBar = GetNode<ProgressBar>("%Bar"); lBar = GetNode<ProgressBar>("%LBar");
        weapon = GetNode<UiSlot>("%Weapon");
        var slots = GetNode("Bottom/Row/Hotbar/Slots"); for (int i = 0; i < Game.HotCount; i++) hot[i] = slots.GetChild<UiSlot>(i);
        for (int i = 0; i < 5; i++) abil[i] = abilities.GetChild<UiSlot>(i);
        for (int i = 0; i < 5; i++) chips[i] = GetNode<Control>($"%Chip{i}");
        abilIcons = new[] { "dash", "spin", "flare", "pulse", "beam" }.Select(n => GD.Load<Texture2D>($"res://Assets/UI/ability_{n}.png")).ToArray();
        panels = new[] { status, goal, world, hint, detail, GetNode<Control>("Bottom/Row/Hotbar"), weapon, abilities };
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

    /// <summary>Номер слота хотбара под точкой (логические координаты) или -1.</summary>
    public int HotSlotAt(Vector2 p) { for (int i = 0; i < Game.HotCount; i++) if (hot[i] != null && hot[i].IsVisibleInTree() && hot[i].GetGlobalRect().HasPoint(p)) return i; return -1; }

    TextureRect dragRect;
    void UpdateDrag()
    {
        if (dragRect == null) { dragRect = new TextureRect { MouseFilter = MouseFilterEnum.Ignore, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, TextureFilter = TextureFilterEnum.Nearest, ZIndex = 4090, CustomMinimumSize = new Vector2(32, 32), Size = new Vector2(32, 32) }; AddChild(dragRect); }
        int ic = G.DragIcon; dragRect.Visible = ic >= 0;
        if (ic >= 0) { dragRect.Texture = UiIcons.Item(ic); dragRect.GlobalPosition = G.DragPos - dragRect.Size * .5f; }
    }

    static string T(string s) => Loc.T(s);

    public override void _Process(double delta)
    {
        if (G == null || G.W == null) return;
        float dt = (float)delta; flashT = Mathf.Max(0, flashT - dt);
        bool menu = G.Menu == Mn.Main;
        Visible = !menu && (G.Window == Win.None || G.Window == Win.Build || G.Window == Win.Inventory) && !G.TechOpen && !G.MapOpen && G.Menu == Mn.None;
        UpdateStatus(); UpdateGoal(); UpdateWorld(dt); UpdateHotbar(); UpdateAbilities(); UpdateHint(dt);
    }

    void UpdateStatus()
    {
        level.Text = G.Level.ToString(); lvCap.Text = T("УР");
        hp.Value = Mathf.Clamp(G.Hp / G.MaxHp, 0, 1); hpLabel.Text = $"{(int)G.Hp}";
        xp.Value = Mathf.Clamp(G.Xp / G.XpNeed, 0, 1);
    }

    void UpdateGoal()
    {
        var goals = G.Goals; var g = goals.Count > 0 ? goals[0] : null;
        TextFx.Apply(goalTitle, g == null ? T("Все цели выполнены!") : T(g.Title), new Color("#ffd23f"), 90f);
        TextFx.Apply(goalNeeds, g == null ? "" : T(g.Needs), new Color("#8a96b0"), 0);
        goalNeeds.Visible = g != null && (GoalsOpen || true);
        var m = g != null ? Frac.Match(g.Needs ?? "") : null;
        goalBar.Visible = m != null && m.Success && int.Parse(m.Groups[2].Value) > 0;
        if (goalBar.Visible) goalBar.Value = Mathf.Clamp(float.Parse(m.Groups[1].Value) / float.Parse(m.Groups[2].Value), 0, 1);
        string more = "";
        if (GoalsOpen) for (int i = 1; i < Math.Min(4, goals.Count); i++) more += "• " + T(goals[i].Title) + "\n";
        if (goals.Count > 1) more += GoalsOpen ? T("[G] свернуть · [J] журнал") : string.Format(T("ещё {0} · [G] развернуть"), goals.Count - 1);
        TextFx.Apply(goalMore, more.TrimEnd('\n'), new Color("#8a96b0"), 0); goalMore.Visible = goals.Count > 1;
    }

    void UpdateWorld(float dt)
    {
        int reg = G.RegionAt(G.PPos);
        biome.Text = T(Game.RegionNames[reg]); biome.AddThemeColorOverride("font_color", Game.RegionCol(reg));
        int lit = reg == 0 ? G.LitCount : G.LitIn(reg), total = reg == 0 ? 5 : 3;
        lBar.Value = Mathf.Clamp(lit / (float)total, 0, 1); lLabel.Text = $"{lit}/{total}";
        beacon.Text = $"{T("Маяк")} {G.BeaconLevel}/6";
        // ресурсы: последние изменявшиеся (пустых не показываем; полный список — в инвентаре [I])
        for (int i = 0; i < It.Count; i++) { if (G.Inv[i] != lastInv[i]) { lastInv[i] = G.Inv[i]; lastChange[i] = (float)Time.GetTicksMsec() / 1000f; } }
        var order = new List<int>(); for (int i = 0; i < It.Count; i++) if (G.Inv[i] > 0) order.Add(i);
        order.Sort((a, b) => lastChange[b].CompareTo(lastChange[a]));
        for (int c = 0; c < chips.Length; c++)
        {
            var chip = chips[c]; bool has = c < order.Count; chip.Visible = has; if (!has) continue;
            int it = order[c]; ((TextureRect)chip.GetChild(0)).Texture = UiIcons.Item(it); ((Label)chip.GetChild(1)).Text = Short(G.Inv[it]);
        }
    }

    static string Short(int n) => n >= 10000 ? $"{n / 1000}k" : n >= 1000 ? $"{n / 1000f:0.#}k" : n.ToString();

    /// <summary>Хотбар: инструменты и оружие. Выбранный слот — золотая рамка; клавиши 1–3, колесо мыши.</summary>
    void UpdateHotbar()
    {
        for (int i = 0; i < Game.HotCount; i++)
        {
            string r = G.Hot[i]; bool has = G.HotValid(r);
            var st = !has ? SlotState.Disabled : i == G.HotSel ? SlotState.Selected : SlotState.Normal;
            hot[i].Set(has ? UiIcons.Item(G.HotIcon(r)) : null, "", (i + 1).ToString(), st, 0, "", has ? 1f : .35f); hot[i].TooltipText = has ? Loc.T(G.HotName(r)) : "";
        }
        weapon.Visible = false; UpdateDrag();
    }

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
        abilities.Visible = any;
    }

    /// <summary>Подсказки к объектам рисует Hud.DrawTooltip по модели TipModel; старые панели сцены (Hint/Detail) скрыты.</summary>
    void UpdateHint(float dt)
    {
        hint.Visible = false; detail.Visible = false;
    }
}

static class EnumerableExt
{
    public static System.Collections.Generic.IEnumerable<R> Select<T, R>(this T[] a, Func<T, R> f) { foreach (var x in a) yield return f(x); }
    public static R[] ToArray<R>(this System.Collections.Generic.IEnumerable<R> e) { var l = new List<R>(e); return l.ToArray(); }
}
