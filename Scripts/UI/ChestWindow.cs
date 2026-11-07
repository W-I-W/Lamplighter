using Godot;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Сетки сундука и рюкзака. Ввод и перенос мышью обрабатывает InventoryWindow.</summary>
public partial class ChestWindow : Control
{
    public Game G;
    Control panel; GridContainer chestGrid, invGrid; HotbarRow hotbar; PanelContainer info; Label nameL, hintL, chestTitle, invTitle;
    readonly List<UiSlot> chestSlots = new(), invSlots = new();
    string sig = "";

    public override void _Ready()
    {
        Visible = false; SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); MouseFilter = MouseFilterEnum.Ignore; ZIndex = 4040;
        Theme = GD.Load<Theme>("res://Resources/UI/LanternTheme.tres");
        var cc = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore }; cc.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(cc);
        var p = new PanelContainer { Name = "Panel" }; cc.AddChild(p); panel = p;
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 3); p.AddChild(v);
        chestTitle = new Label { ThemeTypeVariation = "GoldLabel" }; v.AddChild(chestTitle);
        chestGrid = Grid(v);
        invTitle = new Label { ThemeTypeVariation = "GoldLabel" }; v.AddChild(invTitle);
        invGrid = Grid(v);
        v.AddChild(new Control { CustomMinimumSize = new Vector2(0, 3) });
        hotbar = HotbarRow.Create(); v.AddChild(hotbar);
        info = new PanelContainer { ThemeTypeVariation = "PanelDark", Visible = false, MouseFilter = MouseFilterEnum.Ignore }; AddChild(info);
        var iv = new VBoxContainer(); info.AddChild(iv);
        nameL = new Label { ThemeTypeVariation = "GoldLabel", Text = " " }; iv.AddChild(nameL);
        hintL = new Label { ThemeTypeVariation = "DimLabel", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(260, 0) }; iv.AddChild(hintL);
    }

    static GridContainer Grid(Control parent)
    {
        var g = new GridContainer { Columns = Game.ChestCols, SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        g.AddThemeConstantOverride("h_separation", 1); g.AddThemeConstantOverride("v_separation", 1); parent.AddChild(g); return g;
    }

    public bool IsOver(Vector2 p) => Visible && panel != null && new Rect2(Vector2.Zero, panel.Size).HasPoint(panel.GetGlobalTransform().AffineInverse() * (p * G.UiScaleVp));

    internal UiSlot HotbarSlot(int i) => hotbar.Slot(i);

    internal int QuickAt(Vector2 position) => hotbar?.QuickAt(position) ?? -1;

    internal int CellAt(Vector2 position, bool chestSide)
    {
        var list = chestSide ? chestSlots : invSlots;
        return list.FindIndex(slot => slot.IsVisibleInTree() && new Rect2(Vector2.Zero, slot.Size)
            .HasPoint(slot.GetGlobalTransformWithCanvas().AffineInverse() * position));
    }

    UiSlot Slot(List<UiSlot> list, GridContainer grid, bool chestSide, int i)
    {
        while (list.Count <= i)
        {
            var s = GD.Load<PackedScene>("res://Scenes/UI/Slot.tscn").Instantiate<UiSlot>(); int idx = list.Count; s.Index = idx;
            grid.AddChild(s); list.Add(s);
        }
        return list[i];
    }

    public override void _Process(double delta)
    {
        if (G == null || G.W == null) return;
        bool show = G.Window == Win.Chest && G.Menu == Mn.None; Visible = show; if (!show) { sig = ""; return; }
        var chest = G.ChestStacks(); var bag = G.InventorySlots();
        bool chestFocus = G.WinTab == 1; int sel = G.WinCur;
        var sb = new System.Text.StringBuilder(); sb.Append(chestFocus).Append(sel).Append(Loc.Lang).Append(G.InventoryUi.CarrySignature).Append(hotbar.Signature(G));
        foreach (var (it, n) in chest) sb.Append(it).Append(':').Append(n).Append(','); sb.Append('|'); foreach (var (it, n) in bag) sb.Append(it).Append(':').Append(n).Append(',');
        string sg = sb.ToString();
        if (sg != sig)
        {
            sig = sg;
            chestTitle.Text = Loc.T("Сундук"); invTitle.Text = Loc.T("Инвентарь");
            Fill(chestSlots, chestGrid, true, chest, chestFocus, sel);
            Fill(invSlots, invGrid, false, bag, !chestFocus, sel);
            hotbar.Refresh(G);
        }
        UpdateTip(chest, bag);
    }

    /// <summary>Подсказка у курсора — только пока мышь над непустой ячейкой.</summary>
    void UpdateTip(IReadOnlyList<(int item, int n)> chest, IReadOnlyList<(int item, int n)> bag)
    {
        var mouse = GetViewport().GetMousePosition();
        (int item, int n) cur = (-1, 0);
        int c = CellAt(mouse, true), b = c >= 0 ? -1 : CellAt(mouse, false), q = c >= 0 || b >= 0 ? -1 : hotbar.QuickAt(mouse);
        if (c >= 0 && c < chest.Count) cur = chest[c]; else if (b >= 0 && b < bag.Count) cur = bag[b]; else if (q >= 0) cur = G.QuickStack(q);
        bool carrying = G.InventoryUi.CarrySignature != "";
        if (cur.n <= 0 || carrying) { info.Visible = false; return; }
        string details = G.InventoryItemDetails(cur.item);
        if (string.IsNullOrWhiteSpace(details)) { info.Visible = false; return; }
        nameL.Text = $"{Loc.T(G.InventoryItemName(cur.item))} ×{cur.n}";
        hintL.Text = details;
        info.ResetSize();
        var at = GetGlobalTransformWithCanvas().AffineInverse() * mouse + new Vector2(12, 14);
        at.X = Mathf.Clamp(at.X, 0, Mathf.Max(0, Size.X - info.Size.X)); at.Y = Mathf.Clamp(at.Y, 0, Mathf.Max(0, Size.Y - info.Size.Y));
        info.Position = at; info.Visible = true;
    }

    void Fill(List<UiSlot> list, GridContainer grid, bool chestSide, IReadOnlyList<(int item, int n)> stacks, bool focused, int sel)
    {
        for (int i = 0; i < stacks.Count; i++)
        {
            var (it, n) = stacks[i]; var s = Slot(list, grid, chestSide, i); s.Visible = true;
            n -= G.InventoryUi.CarriedCount(false, chestSide, i);
            bool has = n > 0 && it != -1;
            s.Set(has ? G.InventoryItemTexture(it) : null, has ? n.ToString() : "", "", focused && i == sel ? SlotState.Selected : SlotState.Normal);
        }
        for (int i = stacks.Count; i < list.Count; i++) list[i].Visible = false;
    }
}
