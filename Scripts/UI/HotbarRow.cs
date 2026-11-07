using Godot;

namespace Lamplighter;

/// <summary>Нижний ряд быстрых слотов 1–0 внутри окон инвентаря и сундука (как хотбар в Minecraft). Только показывает; перенос обрабатывает InventoryWindow.</summary>
public partial class HotbarRow : GridContainer
{
    readonly UiSlot[] slots = new UiSlot[Game.QuickSlotCount];
    public static HotbarRow Create()
    {
        var row = new HotbarRow { Columns = Game.QuickSlotCount, SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        row.AddThemeConstantOverride("h_separation", 1);
        var scene = GD.Load<PackedScene>("res://Scenes/UI/Slot.tscn");
        for (int i = 0; i < row.slots.Length; i++)
        {
            var s = scene.Instantiate<UiSlot>(); s.Index = i; row.slots[i] = s; row.AddChild(s);
        }
        return row;
    }

    public UiSlot Slot(int i) => slots[i];

    public int QuickAt(Vector2 p)
    {
        for (int i = 0; i < slots.Length; i++)
            if (slots[i].IsVisibleInTree() && new Rect2(Vector2.Zero, slots[i].Size).HasPoint(slots[i].GetGlobalTransformWithCanvas().AffineInverse() * p)) return i;
        return -1;
    }

    public string Signature(Game g)
    {
        var sb = new System.Text.StringBuilder().Append(g.SelectedQuickSlot);
        for (int i = 0; i < slots.Length; i++) { var (it, n) = g.QuickStack(i); sb.Append(',').Append(it).Append(':').Append(n - g.InventoryUi.CarriedCount(true, false, i)); }
        return sb.ToString();
    }

    public void Refresh(Game g)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            var (item, n) = g.QuickStack(i);
            (item, n) = g.InventoryUi.DisplayStack(0, i, item, n);
            slots[i].Set(n > 0 ? g.InventoryItemTexture(item) : null, n > 1 ? n.ToString() : "", "",
                n > 0 ? SlotState.Normal : SlotState.Disabled);
        }
    }
}
