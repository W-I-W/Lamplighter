using Godot;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Записки и финал: окно записки, кадры концовки и титры.</summary>
public partial class Hud
{
    void DrawStory(Vector2 vs)
    {
        float w = Mathf.Min(560, vs.X - 40), h = 250;
        var r = Frame(new Rect2((vs.X - w) / 2, (vs.Y - h) / 2, w, h), G.StoryTitle, new Color("#ffd86b"), "[E] / [Esc] — закрыть");
        if (G.StorySub != "") Txt(G.StorySub, new Vector2(r.Position.X + 16, r.Position.Y + 44), 11, Dim);
        DrawMultilineString(font, new Vector2(r.Position.X + 18, r.Position.Y + 72), Loc.T(G.StoryText), HorizontalAlignment.Left, w - 36, 15, -1, new Color(.92f, .94f, 1f));
    }

    void DrawFinale(Vector2 vs)
    {
        if (G.FinStage == 0) return;
        if (G.FinStage == 1)
        {
            float k = Mathf.Clamp((G.FinT - 6f) / 3f, 0, 1);
            if (k > 0) Txt("СВЕТ ВОЗВРАЩАЕТСЯ", new Vector2(0, vs.Y * .4f), 30, new Color(1, .95f, .7f, k), HorizontalAlignment.Center, vs.X);
            return;
        }
        float fade = Mathf.Clamp(G.FinT / 1.2f, 0, 1);
        DrawRect(new Rect2(Vector2.Zero, vs), new Color(.01f, .01f, .03f, G.FinStage == 3 ? .94f : .82f));
        if (G.FinStage == 2)
        {
            DrawMultilineString(font, new Vector2((vs.X - Mathf.Min(640, vs.X - 80)) / 2, vs.Y * .38f), Loc.T(Story.Finale[G.FinScene]), HorizontalAlignment.Center, Mathf.Min(640, vs.X - 80), 20, -1, new Color(1, .96f, .85f, fade));
            Txt(G.FinScene >= Story.Finale.Length - 1 ? "[Enter] — титры" : "[Enter] — дальше · [Esc] — пропустить", new Vector2(0, vs.Y - 40), 12, Dim, HorizontalAlignment.Center, vs.X);
            return;
        }
        float y = vs.Y - G.FinT * 42f + 20;
        for (int i = 0; i < Story.Credits.Length; i++)
        {
            float yy = y + i * 34; if (yy < -30 || yy > vs.Y + 30) continue;
            bool head = i == 0 || (i > 0 && Story.Credits[i - 1] == "");
            Txt(Story.Credits[i], new Vector2(0, yy), head ? 22 : 16, head ? new Color("#ffe27a") : new Color(.9f, .92f, 1f), HorizontalAlignment.Center, vs.X);
        }
        Txt("[Enter] — продолжить игру", new Vector2(0, vs.Y - 24), 12, Dim, HorizontalAlignment.Center, vs.X);
    }
}
