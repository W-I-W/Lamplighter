using Godot;
using System;

namespace Lamplighter;

/// <summary>Проверка графики (LAMP_STAGE=texcheck): все спрайты загружены из PNG, ничего не рисуется кодом.</summary>
public partial class Game
{
    void BuildTexCheckScript()
    {
        S(.2f, () =>
        {
            int bad = 0;
            void Check(bool ok, string what) { if (!ok) bad++; GD.Print($"[check] {(ok ? "PASS" : "FAIL")} {what}"); }
            Check(Art.TileTex != null && Art.TileTex.GetWidth() == 16 * Art.TileCount, "tile atlas built from PNG files");
            Check(Art.IconTex != null && Art.IconTex.GetWidth() == 16 * Art.IconCount, "icon atlas built from PNG files");
            foreach (var s in SpriteLibrary.Sheets) Check(s.get() != null, $"sheet loaded from PNG: {s.id}");
            Check(VisualCatalog.Get() != null, "visual catalog resource loaded");
            foreach (var n in new[] { "px", "line", "disc", "arrow", "diamond", "diamond_line" })
                Check(GD.Load<Texture2D>($"res://Assets/Sprites/Vfx/{n}.png") != null, $"effect primitive {n}.png");
            GD.Print($"[check] SUMMARY failures={bad}");
        });
    }
}
