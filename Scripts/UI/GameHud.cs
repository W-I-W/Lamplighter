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
    Label level, hpLabel, goalTitle, goalNeeds, goalMore, biome, lLabel, beacon, hintLabel, dTitle, dBody;
    ProgressBar hp, xp, goalBar, lBar;
    UiSlot weapon; readonly UiSlot[] hot = new UiSlot[8]; readonly UiSlot[] abil = new UiSlot[5];
    readonly Control[] chips = new Control[5];
    readonly int[] lastInv = new int[It.Count]; readonly float[] lastChange = new float[It.Count];
    float flashT, hintA; int flashSlot = -1; Control[] panels;
    public bool GoalsOpen;                                      // раскрытый список целей
    static readonly Regex Frac = new(@"(\d+)\s*/\s*(\d+)", RegexOptions.Compiled);
    Texture2D[] abilIcons;

    public override void _Ready()
    {
        status = GetNode<Control>("%Status"); goal = GetNode<Control>("%Goal"); world = GetNode<Control>("%World"); hint = GetNode<Control>("%Hint"); detail = GetNode<Control>("%Detail"); abilities = GetNode<Control>("%Abilities");
        level = GetNode<Label>("%Level"); hpLabel = GetNode<Label>("%HpLabel"); goalTitle = GetNode<Label>("%Title"); goalNeeds = GetNode<Label>("%Needs"); goalMore = GetNode<Label>("%More");
        biome = GetNode<Label>("%Biome"); lLabel = GetNode<Label>("%LLabel"); beacon = GetNode<Label>("%Beacon"); hintLabel = GetNode<Label>("%HintLabel"); dTitle = GetNode<Label>("%DTitle"); dBody = GetNode<Label>("%DBody");
        hp = GetNode<ProgressBar>("%HpBar"); xp = GetNode<ProgressBar>("%XpBar"); goalBar = GetNode<ProgressBar>("%Bar"); lBar = GetNode<ProgressBar>("%LBar");
        weapon = GetNode<UiSlot>("%Weapon");
        var slots = GetNode("Bottom/Row/Hotbar/Slots"); for (int i = 0; i < 8; i++) hot[i] = slots.GetChild<UiSlot>(i);
        for (int i = 0; i < 5; i++) abil[i] = abilities.GetChild<UiSlot>(i);
        for (int i = 0; i < 5; i++) chips[i] = GetNode<Control>($"%Chip{i}");
        abilIcons = new[] { "dash", "spin", "flare", "pulse", "beam" }.Select(n => GD.Load<Texture2D>($"res://Assets/UI/ability_{n}.png")).ToArray();
        panels = new[] { status, goal, world, hint, detail, GetNode<Control>("Bottom/Row/Hotbar"), weapon, abilities };
    }

    /// <summary>Находится ли точка (в логических координатах интерфейса) над видимой панелью HUD.</summary>
    public bool IsOver(Vector2 p)
    {
        if (panels == null) return false;
        foreach (var c in panels) if (c != null && c.IsVisibleInTree() && c.GetGlobalRect().HasPoint(p * 1f)) return true;
        return false;
    }

    public void Flash(int slot) { flashSlot = slot; flashT = .9f; }

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
        level.Text = G.Level.ToString();
        hp.Value = Mathf.Clamp(G.Hp / G.MaxHp, 0, 1); hpLabel.Text = $"{(int)G.Hp}";
        xp.Value = Mathf.Clamp(G.Xp / G.XpNeed, 0, 1);
    }

    void UpdateGoal()
    {
        var goals = G.Goals; var g = goals.Count > 0 ? goals[0] : null;
        goalTitle.Text = g == null ? T("Все цели выполнены!") : T(g.Title);
        goalNeeds.Text = g == null ? "" : T(g.Needs);
        goalNeeds.Visible = g != null && (GoalsOpen || true);
        var m = g != null ? Frac.Match(g.Needs ?? "") : null;
        goalBar.Visible = m != null && m.Success && int.Parse(m.Groups[2].Value) > 0;
        if (goalBar.Visible) goalBar.Value = Mathf.Clamp(float.Parse(m.Groups[1].Value) / float.Parse(m.Groups[2].Value), 0, 1);
        string more = "";
        if (GoalsOpen) for (int i = 1; i < Math.Min(4, goals.Count); i++) more += "• " + T(goals[i].Title) + "\n";
        if (goals.Count > 1) more += GoalsOpen ? T("[G] свернуть · [J] журнал") : string.Format(T("ещё {0} · [G] развернуть"), goals.Count - 1);
        goalMore.Text = more.TrimEnd('\n'); goalMore.Visible = goals.Count > 1;
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

    void UpdateHotbar()
    {
        // Слоты повторяют клавиши 1–8 (действия те же, что и раньше).
        var cost = new int[8][][]; var built = new bool[8]; var unlocked = new bool[8]; Texture2D[] ic = new Texture2D[8]; string[] cnt = new string[8];
        cost[0] = Game.FurnaceCost; unlocked[0] = true; ic[0] = UiIcons.Tile(Art.Furnace); cnt[0] = G.HasFurnace ? "✓" : "";
        cost[1] = Game.LanternCost; unlocked[1] = true; ic[1] = UiIcons.Tile(Art.Lantern); cnt[1] = G.Lamps.Count > 0 ? G.Lamps.Count.ToString() : "";
        cost[2] = G.NextPickCost; unlocked[2] = G.HasFurnace && G.NextPickCost != null; ic[2] = UiIcons.Item(UiIcons.Pickaxe); cnt[2] = G.Tier.ToString();
        int[] bk = { 0, 1, 2, 3 }; int[] tile = { Art.Warehouse, Art.Sawmill, Art.Mine, Art.Forge };
        for (int i = 0; i < 4; i++) { cost[3 + i] = Game.BCost(bk[i], 1); unlocked[3 + i] = G.BUnlocked(bk[i]); ic[3 + i] = UiIcons.Tile(tile[i]); cnt[3 + i] = G.HasBuilding(Game.BTypes[bk[i]]) ? "✓" : ""; }
        cost[7] = Game.LampCostOf(1); unlocked[7] = G.Has(Tech.CombatLamp); ic[7] = UiIcons.Tile(Art.CombatLantern); cnt[7] = "";
        for (int i = 0; i < 8; i++)
        {
            bool ok = unlocked[i] && cost[i] != null && G.Can(cost[i]);
            var st = flashSlot == i && flashT > 0 ? SlotState.Selected : !unlocked[i] ? SlotState.Disabled : ok ? SlotState.Ok : SlotState.Normal;
            hot[i].Set(ic[i], cnt[i], (i + 1).ToString(), st, 0, "", unlocked[i] ? 1f : .35f);
        }
        var w = G.CurDef; string ch = w == null ? "" : w.Cls == WClass.Sword ? "М" : w.Cls == WClass.Bow ? "Л" : w.Cls == WClass.Staff ? "П" : "";
        weapon.Set(w == null ? UiIcons.Item(UiIcons.Pickaxe) : null, "", "V", SlotState.Normal, 0, ch);
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

    void UpdateHint(float dt)
    {
        var ix = G.Cur; var sel = G.Interact.Selected;
        bool show = sel != null && (ix.Lines.Count > 0 || ix.Title != null);
        string action = "";
        foreach (var l in ix.Lines) if (l.StartsWith("[")) { action = l; break; }
        string label = ix.Title ?? "";
        hint.Visible = show; if (show) hintLabel.Text = T(action != "" ? action : label) + (ix.U != null && action.StartsWith("[E]") ? "   " + T("[U] улучшить") : "");
        bool hover = sel != null && G.Interact.Hover?.Key == sel.Key;
        detail.Visible = show && (hover || G.DetailT > 0);
        if (detail.Visible)
        {
            dTitle.Text = T(ix.Title ?? "");
            var sb = new System.Text.StringBuilder(); foreach (var l in ix.Lines) sb.Append(T(l)).Append('\n');
            dBody.Text = sb.ToString().TrimEnd('\n');
        }
    }
}

static class EnumerableExt
{
    public static System.Collections.Generic.IEnumerable<R> Select<T, R>(this T[] a, Func<T, R> f) { foreach (var x in a) yield return f(x); }
    public static R[] ToArray<R>(this System.Collections.Generic.IEnumerable<R> e) { var l = new List<R>(e); return l.ToArray(); }
}
