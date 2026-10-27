using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Проверка графики (LAMP_STAGE=texcheck): текстуры из PNG побайтно совпадают с процедурными.</summary>
public partial class Game
{
    static string H(Texture2D t)
    {
        if (t == null) return "null";
        var im = t.GetImage(); if (im.IsCompressed()) im.Decompress(); im.Convert(Image.Format.Rgba8);
        var d = im.GetData(); for (int i = 0; i < d.Length; i += 4) if (d[i + 3] == 0) { d[i] = 0; d[i + 1] = 0; d[i + 2] = 0; }   // импорт «чинит» цвет невидимых пикселей — на вид не влияет
        return System.Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(d));
    }

    void BuildTexCheckScript()
    {
        S(.2f, () =>
        {
            var loaded = new Dictionary<string, string> { ["tiles"] = H(Art.TileTex), ["icons"] = H(Art.IconTex) };
            foreach (var s in SpriteLibrary.Sheets) loaded[s.id] = H(s.get());
            GD.Print($"[gfx] shade pixel(0,0)={Art.ShadeTex.GetImage().GetPixel(0, 0)} size={Art.ShadeTex.GetSize()} path={Art.ShadeTex.ResourcePath}");
            bool fromFiles = VisualCatalog.Get() != null; bool skipped = Art.ProcTiles == null;
            GD.Print($"[check] {(skipped ? "PASS" : "FAIL")} procedural generation skipped at startup when PNGs exist");
            Art.TileTex = null; Art.BuildProcedural();
            var proc = new Dictionary<string, string> { ["tiles"] = H(Art.TileTex), ["icons"] = H(Art.IconTex) };
            foreach (var s in SpriteLibrary.Sheets) proc[s.id] = H(s.get());
            GD.Print($"[gfx] shade loaded={loaded["shade"]} proc={proc["shade"]} procPixel={Art.ShadeTex.GetImage().GetPixel(0, 0)}");
            int bad = 0; foreach (var kv in loaded) if (proc[kv.Key] != kv.Value) { bad++; GD.Print($"[check] FAIL texture differs: {kv.Key}"); }
            GD.Print($"[check] {(bad == 0 ? "PASS" : "FAIL")} textures from PNG identical to procedural: {loaded.Count - bad}/{loaded.Count} catalog={fromFiles}");
            GD.Print($"[check] SUMMARY failures={bad + (skipped ? 0 : 1)}");
        });
    }
}
