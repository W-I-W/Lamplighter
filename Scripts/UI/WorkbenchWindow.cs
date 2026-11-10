using Godot;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Окно Рабочего стола: слева быстрый крафт (постройки, доски, рукоять, сборка инструментов и оружия), в центре креста результат, вокруг него четыре ячейки компонентов, снизу сумка и горячая панель.
/// Ячейки и сумка работают как в инвентаре: предметы берутся в руку, кладутся, делятся, меняются местами (ввод и перенос мышью обрабатывает InventoryWindow).
/// Клик по рецепту выкладывает его компоненты из сумки в ячейки; клик по результату создаёт постройку, она остаётся в ячейке, пока игрок не заберёт её сам.</summary>
public partial class WorkbenchWindow : Control
{
    public Game G;
    Control panel; GridContainer listGrid, invGrid; Label title, nameL, statusL, invTitle; HotbarRow hotbar; ItemTipPanel tipPanel;
    readonly List<UiSlot> listSlots = new(), invSlots = new();
    Button tabAvailable, tabAll; bool quickAll; Label quickTitle; List<(BenchRecipe r, int times)> quickList = new();
    UiSlot[] cells; UiSlot result;                       // cells: сверху, слева, справа, снизу = ячейки 0–3; result = ячейка 4
    string sig = "";

    public UiSlot Result => result;

    public override void _Ready()
    {
        Visible = false; SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); MouseFilter = MouseFilterEnum.Ignore; ZIndex = 4040;
        Theme = GD.Load<Theme>("res://Resources/UI/LanternTheme.tres");
        var cc = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore }; cc.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(cc);
        var p = new PanelContainer { Name = "Panel" }; cc.AddChild(p); panel = p;
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 3); p.AddChild(v);
        title = new Label { ThemeTypeVariation = "GoldLabel" }; v.AddChild(title);

        var body = new HBoxContainer(); body.AddThemeConstantOverride("separation", 8); v.AddChild(body);
        var left = new VBoxContainer(); left.AddThemeConstantOverride("separation", 3); body.AddChild(left);
        quickTitle = new Label { ThemeTypeVariation = "GoldLabel" }; left.AddChild(quickTitle);
        var head = new HBoxContainer(); head.AddThemeConstantOverride("separation", 2); left.AddChild(head);
        tabAvailable = QuickTab("Доступные", false); tabAll = QuickTab("Все известные", true); head.AddChild(tabAvailable); head.AddChild(tabAll);
        var sc = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(Game.BenchCols * 25, 3 * 25 + 2), SizeFlagsVertical = SizeFlags.ExpandFill };
        left.AddChild(sc);
        listGrid = new GridContainer { Columns = Game.BenchCols }; listGrid.AddThemeConstantOverride("h_separation", 1); listGrid.AddThemeConstantOverride("v_separation", 1); sc.AddChild(listGrid);
        UpdateQuickTabs();

        var mid = new VBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkBegin }; mid.AddThemeConstantOverride("separation", 2); body.AddChild(mid);
        var cross = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ShrinkCenter }; cross.AddThemeConstantOverride("h_separation", 1); cross.AddThemeConstantOverride("v_separation", 1); mid.AddChild(cross);
        var top = Cell(0); var lft = Cell(1); var right = Cell(2); var bottom = Cell(3); result = Cell(Game.BenchInputs);
        cells = new[] { top, lft, right, bottom, result };
        cross.AddChild(Gap()); cross.AddChild(top); cross.AddChild(Gap());                 // крест: сверху, слева/центр/справа, снизу
        cross.AddChild(lft); cross.AddChild(result); cross.AddChild(right);
        cross.AddChild(Gap()); cross.AddChild(bottom); cross.AddChild(Gap());
        nameL = new Label { ThemeTypeVariation = "GoldLabel", HorizontalAlignment = HorizontalAlignment.Center, CustomMinimumSize = new Vector2(110, 0), AutowrapMode = TextServer.AutowrapMode.WordSmart }; mid.AddChild(nameL);
        statusL = new Label { ThemeTypeVariation = "PlainLabel", HorizontalAlignment = HorizontalAlignment.Center, CustomMinimumSize = new Vector2(110, 0), AutowrapMode = TextServer.AutowrapMode.WordSmart }; mid.AddChild(statusL);

        invTitle = new Label { ThemeTypeVariation = "GoldLabel" }; v.AddChild(invTitle);
        invGrid = new GridContainer { Columns = Game.ChestCols, SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        invGrid.AddThemeConstantOverride("h_separation", 1); invGrid.AddThemeConstantOverride("v_separation", 1); v.AddChild(invGrid);
        v.AddChild(new Control { CustomMinimumSize = new Vector2(0, 3) });
        hotbar = HotbarRow.Create(); v.AddChild(hotbar);
        tipPanel = ItemTipPanel.Create(this);
    }

    static Control Gap() => new Control { CustomMinimumSize = new Vector2(24, 24), MouseFilter = MouseFilterEnum.Ignore };

    static UiSlot Cell(int index)
    {
        var s = GD.Load<PackedScene>("res://Scenes/UI/Slot.tscn").Instantiate<UiSlot>(); s.Index = index; return s;
    }

    internal Control TestListSlot(int i) => i < listSlots.Count ? listSlots[i] : null;
    internal void TestShowAll() { quickAll = true; sig = ""; UpdateQuickTabs(); }
    internal UiSlot TestCell(int i) => cells[i];
    internal Control TestBagSlot(int i) => i >= 0 && i < invSlots.Count ? invSlots[i] : null;

    internal UiSlot HotbarSlot(int i) => hotbar.Slot(i);
    internal int QuickAt(Vector2 position) => hotbar?.QuickAt(position) ?? -1;

    static bool Hit(Control c, Vector2 position) => c.IsVisibleInTree() && new Rect2(Vector2.Zero, c.Size).HasPoint(c.GetGlobalTransformWithCanvas().AffineInverse() * position);

    /// <summary>Номер ячейки сумки под курсором (или -1).</summary>
    internal int CellAt(Vector2 position) => invSlots.FindIndex(s => Hit(s, position));

    /// <summary>Номер ячейки стола под курсором: 0–3 компоненты, 4 результат (или -1).</summary>
    internal int CraftAt(Vector2 position) => System.Array.FindIndex(cells, s => Hit(s, position));

    Button QuickTab(string text, bool all)
    {
        var b = new Button { Text = Loc.T(text), FocusMode = FocusModeEnum.None };
        b.Pressed += () => { quickAll = all; sig = ""; UpdateQuickTabs(); };
        return b;
    }

    void UpdateQuickTabs()
    {
        tabAvailable.ThemeTypeVariation = quickAll ? "Tab" : "TabSelected"; tabAll.ThemeTypeVariation = quickAll ? "TabSelected" : "Tab";
    }

    UiSlot ListSlot(int i)
    {
        while (listSlots.Count <= i)
        {
            var s = Cell(listSlots.Count);
            s.Clicked += k => { if (k < quickList.Count) G.BenchLayout(quickList[k].r, Input.IsKeyPressed(Key.Shift)); };
            listGrid.AddChild(s); listSlots.Add(s);
        }
        return listSlots[i];
    }

    UiSlot InvSlot(int i)
    {
        while (invSlots.Count <= i)
        {
            var s = Cell(invSlots.Count); invGrid.AddChild(s); invSlots.Add(s);
        }
        return invSlots[i];
    }

    public bool IsOver(Vector2 p) => Visible && panel != null && new Rect2(Vector2.Zero, panel.Size).HasPoint(panel.GetGlobalTransform().AffineInverse() * (p * G.UiScaleVp));

    public override void _Process(double delta)
    {
        if (G == null || G.W == null) return;
        bool show = G.Window == Win.Bench && G.Menu == Mn.None; Visible = show; if (!show) { sig = ""; return; }
        var bag = G.InventorySlots();
        var sb = new System.Text.StringBuilder(); sb.Append(Loc.Lang).Append(G.InventoryUi.CarrySignature).Append(hotbar.Signature(G)).Append(quickAll).Append(G.BenchSignature()).Append('|');
        sb.Append('|'); foreach (var (it, n) in bag) sb.Append(it).Append(':').Append(n).Append(',');
        sb.Append('|'); for (int i = 0; i <= Game.BenchInputs; i++) { var c = G.CraftIngredient(i); sb.Append(c.item).Append(':').Append(c.n).Append(','); }
        string sg = sb.ToString();
        if (sg != sig)
        {
            sig = sg; Refresh(bag);
        }
        UpdateTip(bag);
    }

    void Refresh(IReadOnlyList<(int item, int n)> bag)
    {
        title.Text = Loc.T("Рабочий стол"); invTitle.Text = Loc.T("Инвентарь");
        quickTitle.Text = Loc.T("Быстрый крафт"); tabAvailable.Text = Loc.T("Доступные"); tabAll.Text = Loc.T("Все известные");
        quickList = G.BenchRecipes(quickAll);
        for (int i = 0; i < quickList.Count; i++)
        {
            var (e, times) = quickList[i]; var s = ListSlot(i); s.Visible = true; int idx = i;
            s.Set(e.Icon, times > 1 ? times.ToString() : "", "", times > 0 ? SlotState.Normal : SlotState.Disabled, 0f, "", times > 0 ? 1f : .45f);
            ItemTipHost.Bind(s, () => idx < quickList.Count ? G.BenchRecipeTip(quickList[idx].r, quickList[idx].times) : null);
        }
        for (int i = quickList.Count; i < listSlots.Count; i++) listSlots[i].Visible = false;

        for (int k = 0; k < Game.BenchInputs; k++)
        {
            var (it, n) = G.CraftIngredient(k); (it, n) = G.InventoryUi.DisplayStack(3, k, it, n);
            cells[k].Set(n > 0 ? G.InventoryItemTexture(it) : null, n > 0 ? n.ToString() : "", "", SlotState.Normal);
        }
        var m = G.MatchBench(); var (ri, rn) = G.CraftIngredient(Game.BenchInputs); (ri, rn) = G.InventoryUi.DisplayStack(3, Game.BenchInputs, ri, rn);
        if (rn > 0) result.Set(G.InventoryItemTexture(ri), rn.ToString(), "", SlotState.Normal);
        else if (m.State == Game.BenchState.Ready) result.Set(m.Entry.Icon, "", "", SlotState.Ok, 0f, "", .6f);          // предпросмотр, только когда создать действительно можно
        else result.Set(null, "", "", SlotState.Normal);

        nameL.Text = m.Entry != null ? Loc.T(m.Entry.Name) : "";
        statusL.Text = m.State switch
        {
            Game.BenchState.Ready => Loc.T("Клик по результату — создать"),
            Game.BenchState.Short => Loc.T("Не хватает ресурсов"),
            Game.BenchState.Blocked => Loc.T("Сначала заберите результат"),
            Game.BenchState.Unknown => Loc.T("Неизвестная комбинация"),
            _ => Loc.T("Выберите рецепт или положите ресурсы в ячейки"),
        };

        for (int i = 0; i < bag.Count; i++)
        {
            var (it, n) = bag[i]; var s = InvSlot(i); s.Visible = true;
            (it, n) = G.InventoryUi.DisplayStack(2, i, it, n); bool has = n > 0 && it != -1;
            s.Set(has ? G.InventoryItemTexture(it) : null, has ? n.ToString() : "", "", SlotState.Normal);
        }
        for (int i = bag.Count; i < invSlots.Count; i++) invSlots[i].Visible = false;
        hotbar.Refresh(G);
    }

    /// <summary>Подсказка у курсора — только пока мышь над непустой ячейкой и в руке ничего нет.</summary>
    void UpdateTip(IReadOnlyList<(int item, int n)> bag)
    {
        var mouse = GetViewport().GetMousePosition();
        (int item, int n) cur = (-1, 0);
        int c = CraftAt(mouse), b = c >= 0 ? -1 : CellAt(mouse), q = c >= 0 || b >= 0 ? -1 : hotbar.QuickAt(mouse);
        if (c >= 0) cur = G.CraftIngredient(c); else if (b >= 0 && b < bag.Count) cur = bag[b]; else if (q >= 0) cur = G.QuickStack(q);
        if (cur.n <= 0 || G.InventoryUi.CarrySignature != "") { tipPanel.Visible = false; return; }
        tipPanel.Set(G.InventoryItemTip(cur.item, cur.n)); tipPanel.ShowAt(this, mouse);
    }
}
