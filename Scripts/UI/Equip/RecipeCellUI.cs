using Godot;
using System;

namespace Lamplighter;

/// <summary>Ячейка рецепта в сетке «Создание»: пиксельная иконка в тонкой рамке, подсветка при наведении, золотая рамка при выборе и точка доступности (зелёная — можно создать, красная — не хватает ресурсов, серая — уже есть).
/// Название — во всплывающей подсказке. Один щелчок выбирает, двойной создаёт.</summary>
public partial class RecipeCellUI : Control
{
    public const float Cell = 28f;
    public int Index; public event Action<int> Picked, Activated;
    Texture2D icon; RecState state; bool selected, hover;
    static readonly Color Gold = new("#ffd23f"), Good = new("#9be07a"), Bad = new("#f06a5e"), Grey = new("#6a7288");

    public void Bind(int index, Texture2D ic, RecState st, bool sel, string name)
    {
        Index = index; icon = ic; state = st; selected = sel; TooltipText = name; QueueRedraw();
    }

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(Cell, Cell); MouseFilter = MouseFilterEnum.Stop; MouseDefaultCursorShape = CursorShape.PointingHand; TextureFilter = TextureFilterEnum.Nearest;
        MouseEntered += () => { hover = true; QueueRedraw(); }; MouseExited += () => { hover = false; QueueRedraw(); };
        GuiInput += e =>
        {
            if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left) { if (mb.DoubleClick) Activated?.Invoke(Index); else Picked?.Invoke(Index); AcceptEvent(); }
        };
    }

    public override void _Draw()
    {
        var r = new Rect2(Vector2.Zero, Size);
        DrawRect(r, selected ? new Color("#1d2a47") : hover ? new Color("#16233f") : new Color("#0f1a30"));
        var border = selected ? Gold : hover ? new Color("#5a6a90") : new Color("#243250"); float t = selected ? 2 : 1;
        DrawRect(new Rect2(0, 0, r.Size.X, t), border); DrawRect(new Rect2(0, r.Size.Y - t, r.Size.X, t), border); DrawRect(new Rect2(0, 0, t, r.Size.Y), border); DrawRect(new Rect2(r.Size.X - t, 0, t, r.Size.Y), border);
        if (icon != null) DrawTextureRect(icon, new Rect2((r.Size.X - 16) / 2, (r.Size.Y - 16) / 2, 16, 16), false, state == RecState.Ready ? Colors.White : new Color(1, 1, 1, state == RecState.Owned ? .45f : .7f));
        var dot = state == RecState.Ready ? Good : state == RecState.Missing ? Bad : Grey;
        DrawRect(new Rect2(r.Size.X - 8, 3, 5, 5), new Color(0, 0, 0, .8f)); DrawRect(new Rect2(r.Size.X - 7, 4, 3, 3), dot);
    }
}
