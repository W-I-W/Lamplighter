using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Рабочий стол: ставимая постройка, у которой [E] открывает окно создания. Туда перенесены все крафты, что раньше были в меню строительства:
/// слева список рецептов, в центре результат и до четырёх компонентов вокруг него (есть/нужно), снизу инвентарь.
/// Создание не меняет механику построек: цена списывается из инвентаря, готовая постройка попадает в запас (сумку) и ставится оттуда.</summary>
public partial class Game
{
    public static readonly int[][] CostWorkbench = { new[] { It.Wood, 6 }, new[] { It.Stone, 2 } };
    public const int BenchCols = 6, BenchRange = 72;
    public Vector2I BenchAt = new(-1, -1);

    /// <summary>Рабочий стол поставлен в мире (для заданий): среди клеток, поставленных игроком, есть стол.</summary>
    public bool HasWorkbench { get { foreach (int k in Placed) if (k >= 0 && k < W.Obj.Length && W.Obj[k] == (byte)Tl.Workbench) return true; return false; } }

    void BuildWorkbench()
    {
        if (!Can(CostWorkbench)) { Fail("Для рабочего стола нужно: 6 дерева и 2 камня"); return; }
        var t = PlaceTarget(); if (t == null) return;
        Pay(CostWorkbench); SetObj(t.Value.X, t.Value.Y, Tl.Workbench);
        PlaceFx(t.Value, new Color("#b07a40"), 1.1f); Sfx.Play("build");
        GiveXp(10); Toast("Рабочий стол готов"); Save();
    }

    public void OpenBench(int x, int y)
    {
        CloseWindows(); Placing = null; Window = Win.Bench; BenchAt = new Vector2I(x, y); WinCur = 0; Sfx.Play("tick"); PokeHover();
    }

    /// <summary>Закрывает окно, если стол убрали или игрок ушёл.</summary>
    void UpdateBench()
    {
        if (Window != Win.Bench) return;
        if (W.Get(BenchAt.X, BenchAt.Y) != Tl.Workbench || PPos.DistanceTo(TileCenter(BenchAt.X, BenchAt.Y)) > BenchRange) CloseWindows();
    }

    // ───────────────────────── подсказка режима установки ─────────────────────────
    internal bool PlaceHintVisible => placeHint != null && placeHint.Visible;

    void CreatePlaceHint()
    {
        placeHint = new BuildActionHintUI
        {
            Name = "PlacementHint", Visible = false, ZIndex = 4030, MouseFilter = Control.MouseFilterEnum.Ignore, TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            BbcodeEnabled = true, FitContent = true, ScrollActive = false, AutowrapMode = TextServer.AutowrapMode.Off, Modulate = new Color(1, 1, 1, .9f),
            Theme = GD.Load<Theme>("res://Resources/UI/LanternTheme.tres"),
        };
        placeHint.SetAnchorsPreset(Control.LayoutPreset.CenterBottom); placeHint.OffsetTop = -70; placeHint.OffsetBottom = -58; placeHint.GrowHorizontal = Control.GrowDirection.Both;
        placeHint.AddThemeColorOverride("default_color", new Color(0.847f, 0.878f, 0.941f)); placeHint.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0.04f, 0.95f));
        placeHint.AddThemeConstantOverride("shadow_offset_x", 1); placeHint.AddThemeConstantOverride("shadow_offset_y", 1);
        uiRoot.AddChild(placeHint);
    }

    /// <summary>В режиме установки окон нет, а над горячей панелью остаётся одна короткая подсказка.</summary>
    void UpdatePlaceHint()
    {
        if (placeHint == null) return;
        placeHint.Visible = Placing != null && Window == Win.None && Menu == Mn.None;
        if (placeHint.Visible) placeHint.ShowActions("ЛКМ — поставить · ПКМ — отмена");
    }

    /// <summary>Тестовый помощник: ставит рабочий стол рядом с игроком и открывает его окно.</summary>
    internal void TestOpenBench()
    {
        var t = ToTile(PPos + FreeAdj()); SetObj(t.X, t.Y, Tl.Workbench); Placed.Add(World.Key(t.X, t.Y)); PPos = TileCenter(t.X, t.Y) + new Vector2(0, 16);
        OpenBench(t.X, t.Y);
    }

    /// <summary>Рецепты стола: все доступные постройки (закрытые не показываются, как неизвестные рецепты в инвентаре).</summary>
    public List<BuildEntry> BenchEntries() => BuildEntries().FindAll(e => !BuiltOnce(e) && e.Unlocked && e.CostArr != null);

    /// <summary>Сколько раз рецепт можно выполнить из сумки.</summary>
    internal int BenchTimes(BuildEntry e)
    {
        var cost = e.Raw ?? e.CostArr; if (cost == null) return 0;
        int t = 99; foreach (var c in cost) t = Math.Min(t, Inv[c[0]] / c[1]); return t;
    }

    /// <summary>Быстрый крафт стола: те же вкладки, что в инвентаре — только доступные сейчас или все.</summary>
    internal List<(BuildEntry e, int times)> BenchRecipes(bool all)
    {
        var list = new List<(BuildEntry, int)>();
        foreach (var e in BenchEntries()) { int t = BenchTimes(e); if (all || t > 0) list.Add((e, t)); }
        return list;
    }

    /// <summary>Отпечаток данных быстрого крафта стола: меняется при расходовании ресурсов и открытии построек.</summary>
    internal string BenchSignature()
    {
        var sb = new System.Text.StringBuilder();
        foreach (int n in Inv) sb.Append(n).Append(',');
        foreach (var e in BenchEntries()) sb.Append(e.Key).Append(e.Stock).Append(',');
        return sb.ToString();
    }

    /// <summary>Подсказка рецепта: что получится, нужные компоненты (есть/нужно) и сколько раз можно создать — как у быстрого крафта в инвентаре.</summary>
    internal ItemTipData BenchRecipeTip(BuildEntry e, int times)
    {
        var d = new ItemTipData { Name = e.Name, Count = 1, Kind = "Рецепт", Accent = TipGold }; var bad = new Color("#f06a5e");
        foreach (var c in e.Raw ?? e.CostArr) { int have = HaveOf(c[0]); d.Stat(It.Names[c[0]], $"{have}/{c[1]}", have >= c[1] ? TipGood : bad); }
        d.Stat("Можно создать", times.ToString(), times > 0 ? TipGood : bad);
        if (times > 1) d.Stat("Shift + клик", "максимум", TipGold);
        return d;
    }

    // ───────────────────────── ячейки стола ─────────────────────────
    /// <summary>0–3 — компоненты (сверху, слева, справа, снизу), 4 — готовая постройка, которую игрок забирает сам. Работают как ячейки крафта в инвентаре (общие операции переноса, см. CC).</summary>
    readonly (int item, int n)[] benchCells = { (-1, 0), (-1, 0), (-1, 0), (-1, 0), (-1, 0) };
    public const int BenchInputs = 4;
    internal bool CraftFromCells(bool max) => Window == Win.Bench ? BenchCraftFromCells(max) : CombineFromCells(max);

    internal enum BenchState { Empty, Unknown, Short, Blocked, Ready }
    internal sealed class BenchMatchInfo { public BenchState State; public BuildEntry Entry; public int Times; }

    /// <summary>Что получится из ячеек: рецепт, у которого набор ресурсов совпадает с содержимым ячеек (порядок не важен); хватает ли количества и свободна ли ячейка результата.</summary>
    internal BenchMatchInfo MatchBench()
    {
        var have = new Dictionary<int, int>();
        for (int i = 0; i < BenchInputs; i++) if (benchCells[i].n > 0 && benchCells[i].item >= 0) have[benchCells[i].item] = have.GetValueOrDefault(benchCells[i].item) + benchCells[i].n;
        if (have.Count == 0) return new BenchMatchInfo { State = BenchState.Empty };
        BuildEntry short_ = null;
        foreach (var e in BenchEntries())
        {
            var cost = e.Raw ?? e.CostArr; if (!e.Unlocked || cost == null || cost.Length != have.Count) continue;
            int times = 99; bool same = true;
            foreach (var c in cost) { if (!have.TryGetValue(c[0], out int n)) { same = false; break; } times = Math.Min(times, n / c[1]); }
            if (!same) continue;
            if (times < 1) { short_ ??= e; continue; }
            var res = benchCells[BenchInputs]; int id = InventoryItemId(e.Key);
            bool free = res.n <= 0 || res.item == id && res.n < StackMax;
            return new BenchMatchInfo { Entry = e, Times = times, State = free ? BenchState.Ready : BenchState.Blocked };
        }
        return short_ != null ? new BenchMatchInfo { Entry = short_, State = BenchState.Short } : new BenchMatchInfo { State = BenchState.Unknown };
    }

    /// <summary>Создание из ячеек: одна операция проверки, списания и выдачи. Готовая постройка лежит в ячейке результата, пока игрок не заберёт её сам. max — столько раз, сколько позволяют ячейки.</summary>
    internal bool BenchCraftFromCells(bool max)
    {
        if (combining) return false;
        combining = true;
        try
        {
            var m = MatchBench();
            if (m.State == BenchState.Blocked) { Fail("Сначала заберите результат"); return false; }
            if (m.State != BenchState.Ready) { if (m.State == BenchState.Short) Fail("Не хватает ресурсов"); else if (m.State == BenchState.Unknown) Fail("Неизвестная комбинация"); return false; }
            var e = m.Entry; var cost = e.Raw ?? e.CostArr; var res = benchCells[BenchInputs];
            int times = Math.Max(1, Math.Min(max ? m.Times : 1, StackMax - res.n));
            foreach (var c in cost)
            {
                int need = c[1] * times;
                for (int i = 0; i < BenchInputs && need > 0; i++)
                {
                    var st = benchCells[i]; if (st.item != c[0] || st.n <= 0) continue;
                    int take = Math.Min(need, st.n); need -= take; benchCells[i] = take == st.n ? (-1, 0) : (st.item, st.n - take);
                }
            }
            benchCells[BenchInputs] = (InventoryItemId(e.Key), res.n + times);
            GiveXp(BuildCraftXp(e.Key) * times); Sfx.Play("build", -3, 1.2f);
            Toast(Loc.T("Создано") + ": " + Loc.T(e.Name) + (times > 1 ? " ×" + times : ""));
            return true;
        }
        finally { combining = false; }
    }

    /// <summary>Клик по рецепту в списке: компоненты выкладываются из сумки в ячейки (как быстрый крафт в инвентаре). max — на максимум повторов.</summary>
    internal bool BenchLayout(BuildEntry e, bool max)
    {
        if (combining || e == null) return false; var cost = e.Raw ?? e.CostArr;
        if (!e.Unlocked) { Fail("Недоступно: " + e.Lock); return false; }
        if (cost == null || cost.Length > BenchInputs) return false;
        ReturnCellsOf(benchCells, true, BenchInputs);                              // прежние компоненты возвращаются в сумку; готовый результат остаётся в ячейке
        int times = 99; int maxNeed = 1;
        foreach (var c in cost) { times = Math.Min(times, Inv[c[0]] / c[1]); maxNeed = Math.Max(maxNeed, c[1]); }
        if (times <= 0) { Fail("Не хватает ресурсов"); return false; }
        times = max ? Math.Min(times, StackMax / maxNeed) : 1;
        for (int k = 0; k < cost.Length; k++) { Inv[cost[k][0]] -= cost[k][1] * times; benchCells[k] = (cost[k][0], cost[k][1] * times); }
        Sfx.Play("tick", -6, 1.3f);
        return true;
    }

    bool BenchKey(Key k, bool shift = false)
    {
        switch (k)
        {
            case Key.Escape: case Key.Tab: case Key.E: CloseWindows(); break;
            case Key.Q: BenchCraftFromCells(shift); break;
        }
        return true;
    }
}
