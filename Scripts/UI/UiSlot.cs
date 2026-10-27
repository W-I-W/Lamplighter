using Godot;

namespace Lamplighter;

public enum SlotState { Normal, Selected, Disabled, Ok }

/// <summary>Слот интерфейса (хотбар, способности, инвентарь, меню строительства): иконка, количество, клавиша, перезарядка и состояние.
/// Внешний вид — в Scenes/UI/Slot.tscn и теме. Левый клик генерирует сигнал Clicked.</summary>
public partial class UiSlot : PanelContainer
{
    [Signal] public delegate void ClickedEventHandler(int index);
    public int Index;
    TextureRect icon; Label letter, count, key; ColorRect cd;
    SlotState state = (SlotState)(-1);

    public override void _Ready()
    {
        icon = GetNode<TextureRect>("%Icon"); letter = GetNode<Label>("%Letter"); count = GetNode<Label>("%Count"); key = GetNode<Label>("%Key"); cd = GetNode<ColorRect>("%Cooldown");
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left) EmitSignal(SignalName.Clicked, Index);
    }

    public void Set(Texture2D tex, string cnt, string keyText, SlotState st, float cooldown = 0f, string ch = "", float alpha = 1f)
    {
        if (icon == null) return;
        icon.Texture = tex; icon.Modulate = new Color(1, 1, 1, alpha); letter.Text = ch; count.Text = cnt; key.Text = keyText;
        if (st != state)
        {
            state = st; ThemeTypeVariation = st switch { SlotState.Selected => "SlotSelected", SlotState.Disabled => "SlotDisabled", SlotState.Ok => "SlotOk", _ => "Slot" };
        }
        cd.Visible = cooldown > 0.001f;
        if (cd.Visible) { cd.AnchorTop = 1f - Mathf.Clamp(cooldown, 0, 1); cd.OffsetTop = 0; }
    }
}
