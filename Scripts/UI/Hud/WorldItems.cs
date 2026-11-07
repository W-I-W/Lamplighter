using Godot;

namespace Lamplighter;

/// <summary>Добыча и анимация её подбора в отдельном слое между миром и интерфейсом.</summary>
public partial class WorldItems : Control
{
    public Game G;
    Texture2D shadow;

    public override void _Ready() => shadow = GD.Load<Texture2D>("res://Assets/Sprites/Vfx/disc.png");

    public override void _Process(double delta)
    {
        if (G == null) return;
        Scale = Vector2.One * G.UiScaleVp;
        QueueRedraw();
    }

    Vector2 ToScreen(Vector2 world) => (((world - G.CamCenter) * Game.Zoom + G.ViewSize * .5f) / G.UiScaleVp).Round();

    public override void _Draw()
    {
        if (G?.W == null || G.Menu is Mn.Main or Mn.Flow) return;

        foreach (var f in G.Flies)
        {
            var s = ToScreen(f.P);
            float a = Mathf.Clamp(f.T / .08f, 0f, 1f);
            DrawTextureRect(UiIcons.Item(f.Item), new Rect2(s.X - 5, s.Y - 5, 10, 10), false, new Color(1, 1, 1, a));
        }

        var font = GetThemeDefaultFont();
        int fontSize = GetThemeDefaultFontSize();
        var shadowColor = GetThemeColor("font_shadow_color", "Label");
        var shadowOffset = new Vector2(GetThemeConstant("shadow_offset_x", "Label"), GetThemeConstant("shadow_offset_y", "Label"));
        foreach (var d in G.Drops)
        {
            if (d.Pos.DistanceTo(G.PPos) > 220f) continue;
            var s = ToScreen(d.Pos);
            float age = G.DropAge(d);
            float hop = age >= 0 && age < .35f ? -Mathf.Sin(age / .35f * Mathf.Pi) * 6f : 0f;
            float bob = Mathf.Round(Mathf.Sin(G.Time * 3f + d.Id) * 1.2f + hop);
            DrawTextureRect(shadow, new Rect2(s.X - 4, s.Y + 1, 8, 8), false, new Color(0, 0, 0, .35f));
            var icon = UiIcons.Item(Game.IsRelicDrop(d.Item) ? It.Relic : d.Item);
            DrawTextureRect(icon, new Rect2(s.X - 6, s.Y - 8 + bob, 12, 12), false);
            if (d.Count <= 1) continue;
            string count = $"x{d.Count}";
            var baseline = s + new Vector2(-Mathf.Round(font.GetStringSize(count, fontSize: fontSize).X * .5f), 8);
            DrawString(font, baseline + shadowOffset, count, fontSize: fontSize, modulate: shadowColor);
            DrawString(font, baseline, count, fontSize: fontSize, modulate: new Color(.9f, .95f, 1f, .9f));
        }
    }
}
