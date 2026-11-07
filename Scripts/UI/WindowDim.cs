using Godot;

namespace Lamplighter;

/// <summary>Затемнение мира под открытыми панелями (инвентарь, сундук, навыки, постройки и т.д.). Лежит над основным HUD и под окнами (ZIndex 4020: окна от 4030), плавно появляется и исчезает.</summary>
public partial class WindowDim : ColorRect
{
    public Game G;
    const float Max = .6f;
    float a;

    public override void _Ready()
    {
        Name = "WindowDim"; MouseFilter = MouseFilterEnum.Ignore; Color = new Color(0, 0, .03f, 0); Visible = false;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); ZIndex = 4020;
    }

    public override void _Process(double delta)
    {
        if (G == null || G.W == null) return;
        bool want = G.Menu == Mn.None && (G.Window != Win.None || G.TechOpen || G.MapOpen);
        MouseFilter = want ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;     // клики не проходят к закрытому окном HUD, даже пока затемнение ещё нарастает
        Visible = want || a > .005f;
        a = Mathf.MoveToward(a, want ? Max : 0f, (float)delta * 4f);
        Color = new Color(0, 0, .03f, a);
    }
}
