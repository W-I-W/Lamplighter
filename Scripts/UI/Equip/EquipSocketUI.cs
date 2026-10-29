using Godot;
using System;

namespace Lamplighter;

/// <summary>Ячейка вставки оружия: пустая рамка или иконка установленной вставки. Выбранная — в золотой рамке; при наведении показывает название и эффект (подсказка).
/// Сцена — Scenes/UI/EquipSocket.tscn, стили слотов — LanternTheme (Slot, SlotSelected, SlotOk).</summary>
public partial class EquipSocketUI : PanelContainer
{
    public int Index = -1;
    public event Action<EquipSocketUI> Picked, Removed;
    TextureRect icon; bool hover; Tween tw;

    public override void _Ready()
    {
        icon = GetNode<TextureRect>("%Icon"); MouseFilter = MouseFilterEnum.Stop; PivotOffset = Size / 2;
        MouseEntered += () => { hover = true; Tint(); }; MouseExited += () => { hover = false; Tint(); };
        GuiInput += e =>
        {
            if (e is InputEventMouseButton mb && mb.Pressed) { if (mb.ButtonIndex == MouseButton.Left) Picked?.Invoke(this); else if (mb.ButtonIndex == MouseButton.Right) Removed?.Invoke(this); }
        };
    }

    public void Bind(int index, Texture2D ic, Color tint, bool selected, string tip)
    {
        Index = index; icon.Texture = ic; icon.Visible = ic != null; icon.Modulate = tint; TooltipText = tip ?? "";
        ThemeTypeVariation = selected ? "SlotSelected" : ic != null ? "SlotOk" : "Slot"; Tint();
    }

    void Tint() { tw?.Kill(); tw = CreateTween(); tw.TweenProperty(this, "modulate", hover ? new Color(1.25f, 1.25f, 1.25f) : Colors.White, .08f); }

    /// <summary>Вспышка при установке или снятии вставки.</summary>
    public void Flash(Color c) { tw?.Kill(); Modulate = new Color(1.8f, 1.7f, 1.3f); tw = CreateTween(); tw.TweenProperty(this, "modulate", Colors.White, .25f); }
}
