using Godot;

namespace Lamplighter;

/// <summary>Предметы на земле: иконка ресурса с лёгким покачиванием и количеством. Видны только рядом с игроком.</summary>
public partial class Hud
{
    void DrawFlies()
    {
        foreach (var f in G.Flies)
        {
            var s = ToScreen(f.P).Round(); float a = Mathf.Clamp((f.T) / .08f, 0f, 1f);
            Tex(UiIcons.Item(f.Item), new Rect2(s.X - 5, s.Y - 5, 10, 10), new Color(1, 1, 1, a));
        }
    }

    void DrawDrops(Vector2 vs)
    {
        DrawFlies();
        foreach (var d in G.Drops)
        {
            if (d.Pos.DistanceTo(G.PPos) > 220f) continue;
            var s = ToScreen(d.Pos).Round(); float age = G.DropAge(d), hop = age >= 0 && age < .35f ? -Mathf.Sin(age / .35f * Mathf.Pi) * 6f : 0f; float bob = Mathf.Round(Mathf.Sin(G.Time * 3f + d.Id) * 1.2f + hop);
            Disc(s + new Vector2(0, 5), 4, new Color(0, 0, 0, .35f));
            Tex(UiIcons.Item(Game.IsRelicDrop(d.Item) ? It.Relic : d.Item), new Rect2(s.X - 6, s.Y - 8 + bob, 12, 12));
            if (d.Count > 1) Txt("x" + d.Count, s + new Vector2(-14, 8), 9, new Color(.9f, .95f, 1f, .9f), HorizontalAlignment.Center, 28);
        }
    }
}
