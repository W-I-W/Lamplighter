using Godot;

namespace Lamplighter;

/// <summary>Название локации крупным цветным текстом по центру сверху, без фона: плавно появляется, держится пару секунд и тает. При переходе в другую локацию старое название сразу стирается, появляется новое.</summary>
public partial class RegionTitleHud : Control
{
    public Game G;
    Vector2 restPosition;
    Label label; int shown = -1, cand = -1; float candT, age = 99f, fadeOut = 1f;
    const float FadeIn = .5f, Hold = 2.6f, FadeOut = 1.6f, Settle = .6f;

    public override void _Ready()
    {
        label = GetNode<Label>("Title");
        restPosition = label.Position;
    }

    public bool Showing => label != null && label.Visible && label.Modulate.A > .02f;
    public string Text => label?.Text ?? "";

    public override void _Process(double delta)
    {
        if (G == null || G.W == null) return;
        bool ui = G.Menu == Mn.None && G.FinStage == 0 && G.Window == Win.None && !G.MapOpen && !G.TechOpen; float dt = (float)delta;
        int r = G.RegionAt(G.PPos);
        // новая локация принимается, только если игрок в ней задержался: на границе название не мигает
        if (r != shown) { if (r != cand) { cand = r; candT = 0; } candT += dt; if (candT >= (shown < 0 ? .3f : Settle)) { shown = r; age = 0; label.Text = Loc.T(Game.RegionNames[r]); label.Modulate = new Color(Game.RegionCol(r), 0); } }
        else cand = -1;
        age += dt; float a = age < FadeIn ? age / FadeIn : age < FadeIn + Hold ? 1f : Mathf.Clamp(1f - (age - FadeIn - Hold) / FadeOut, 0, 1);
        label.Visible = ui && a > .01f && shown >= 0;
        if (!label.Visible) return;
        var c = Game.RegionCol(shown); label.Modulate = new Color(c.R, c.G, c.B, a);
        label.Position = restPosition - new Vector2(0, 3 * (1f - Mathf.Min(1f, age / FadeIn)));
    }
}
