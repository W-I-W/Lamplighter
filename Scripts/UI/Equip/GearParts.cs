using Godot;
using System;

namespace Lamplighter;

/// <summary>Карточка выбранного предмета: крупная иконка в рамке, название, статус и (необязательно) одна строка описания. Общая для вкладок оружия, реликвий, создания и фонаря.</summary>
public static class ItemDetailsCardUI
{
    public static Control Make(Texture2D icon, Color? tint, string name, Color nameCol, string status, Color statusCol, string line = null)
    {
        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore }; box.AddThemeConstantOverride("separation", 3);
        var head = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore }; head.AddThemeConstantOverride("separation", 8); box.AddChild(head);
        var frame = new PanelContainer { ThemeTypeVariation = "Slot", CustomMinimumSize = new Vector2(52, 52), MouseFilter = Control.MouseFilterEnum.Ignore }; head.AddChild(frame);
        frame.AddChild(new TextureRect { Texture = icon, Modulate = tint ?? Colors.White, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, TextureFilter = CanvasItem.TextureFilterEnum.Nearest, MouseFilter = Control.MouseFilterEnum.Ignore });
        var tt = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter }; head.AddChild(tt);
        var n = new Label { Text = name, ThemeTypeVariation = "GoldLabel", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(80, 0), MouseFilter = Control.MouseFilterEnum.Ignore }; n.AddThemeColorOverride("font_color", nameCol); tt.AddChild(n);
        if (!string.IsNullOrEmpty(status)) { var s = new Label { Text = status, MouseFilter = Control.MouseFilterEnum.Ignore }; s.AddThemeColorOverride("font_color", statusCol); tt.AddChild(s); }
        if (!string.IsNullOrEmpty(line)) box.AddChild(new Label { Text = line, ThemeTypeVariation = "DimLabel", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(120, 0), MouseFilter = Control.MouseFilterEnum.Ignore });
        return box;
    }
}

/// <summary>Главное действие вкладки: одна крупная кнопка. Если действие недоступно, кнопка неактивна, а под ней короткая причина простыми словами.</summary>
public static class MainActionButtonUI
{
    public static Control Make(string text, Action act, bool enabled, string reason = null)
    {
        var v = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore }; v.AddThemeConstantOverride("separation", 2);
        var b = new Button { Text = Loc.T(text), Disabled = !enabled, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(0, 24), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, ClipText = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
        if (enabled) { b.AddThemeColorOverride("font_color", new Color("#ffd23f")); b.AddThemeColorOverride("font_hover_color", new Color("#fff2b0")); }
        b.Pressed += () => act?.Invoke(); v.AddChild(b);
        if (!enabled && !string.IsNullOrEmpty(reason)) { var l = new Label { Text = reason, ThemeTypeVariation = "DimLabel", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(120, 0), MouseFilter = Control.MouseFilterEnum.Ignore }; l.AddThemeColorOverride("font_color", new Color("#ffb36a")); v.AddChild(l); }
        return v;
    }
}

/// <summary>Дружелюбная заглушка пустой вкладки: бледные силуэты слотов, короткий заголовок и подсказка, что делать дальше.</summary>
public static class EmptyStatePanelUI
{
    public static Control Make(Texture2D ghost, int slots, string title, string hint)
    {
        var v = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center }; v.AddThemeConstantOverride("separation", 6);
        if (slots > 0)
        {
            var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore }; row.AddThemeConstantOverride("separation", 6); v.AddChild(row);
            for (int i = 0; i < slots; i++)
            {
                var p = new PanelContainer { ThemeTypeVariation = "Slot", CustomMinimumSize = new Vector2(34, 34), MouseFilter = Control.MouseFilterEnum.Ignore }; row.AddChild(p);
                p.AddChild(new TextureRect { Texture = ghost, Modulate = new Color(.55f, .62f, .8f, .28f), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, TextureFilter = CanvasItem.TextureFilterEnum.Nearest, MouseFilter = Control.MouseFilterEnum.Ignore });
            }
        }
        var t = new Label { Text = title, ThemeTypeVariation = "GoldLabel", HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(140, 0), MouseFilter = Control.MouseFilterEnum.Ignore }; v.AddChild(t);
        v.AddChild(new Label { Text = hint, ThemeTypeVariation = "DimLabel", HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(140, 0), MouseFilter = Control.MouseFilterEnum.Ignore });
        return v;
    }
}

/// <summary>Слот хотбара в виде небольшой пиктограммы: цифра, иконка предмета, который там сейчас лежит, золотая рамка, если здесь выбранный предмет. Клик назначает (или освобождает) слот.</summary>
public partial class HotSlotChipUI : Control
{
    public int Slot; public Texture2D Occupant; public bool Mine; public Action Clicked; bool hover;
    static readonly Color Gold = new("#ffd23f");

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(40, 34); MouseFilter = MouseFilterEnum.Stop; TextureFilter = TextureFilterEnum.Nearest; MouseDefaultCursorShape = CursorShape.PointingHand;
        MouseEntered += () => { hover = true; QueueRedraw(); }; MouseExited += () => { hover = false; QueueRedraw(); };
        GuiInput += e => { if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left) { Clicked?.Invoke(); AcceptEvent(); } };
    }

    public override void _Draw()
    {
        var r = new Rect2(Vector2.Zero, Size); DrawRect(r, Mine ? new Color("#243457") : new Color("#141d33"));
        var b = Mine ? Gold : hover ? new Color("#7e8fb5") : new Color("#2c3d62"); int w = Mine ? 2 : 1;
        DrawRect(new Rect2(0, 0, r.Size.X, w), b); DrawRect(new Rect2(0, r.Size.Y - w, r.Size.X, w), b); DrawRect(new Rect2(0, 0, w, r.Size.Y), b); DrawRect(new Rect2(r.Size.X - w, 0, w, r.Size.Y), b);
        var f = GetThemeDefaultFont(); DrawString(f, new Vector2(4, 11), (Slot + 1).ToString(), HorizontalAlignment.Left, -1, 9, Mine ? Gold : new Color("#8a96b0"));
        if (Occupant != null) DrawTextureRect(Occupant, new Rect2(Size.X - 20, Size.Y - 20, 16, 16), false, Mine ? Colors.White : new Color(1, 1, 1, .8f));
    }
}
