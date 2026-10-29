using Godot;

namespace Lamplighter;

/// <summary>Предметы на земле: иконка ресурса с лёгким покачиванием и количеством. Видны только рядом с игроком.</summary>
public partial class Hud
{
    void DrawDrops(Vector2 vs)
    {
        foreach (var d in G.Drops)
        {
            if (d.Pos.DistanceTo(G.PPos) > 220f) continue;
            var s = ToScreen(d.Pos).Round(); float bob = Mathf.Round(Mathf.Sin(G.Time * 3f + d.Id) * 1.2f);
            Disc(s + new Vector2(0, 5), 4, new Color(0, 0, 0, .35f));
            Tex(UiIcons.Item(d.Item), new Rect2(s.X - 6, s.Y - 8 + bob, 12, 12));
            if (d.Count > 1) Txt("x" + d.Count, s + new Vector2(-14, 8), 9, new Color(.9f, .95f, 1f, .9f), HorizontalAlignment.Center, 28);
        }
    }
}
