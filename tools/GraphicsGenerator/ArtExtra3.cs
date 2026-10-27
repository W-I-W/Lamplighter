using Godot;
using System;

namespace Lamplighter;

/// <summary>Графика этапа 4: находки, торговля, логистика, усилители света.</summary>
public static partial class Art
{
    public const int TechCache = 85, Spire = 86, Altar = 87, AltarOn = 88, Stall = 89, Hub = 92, Refinery = 95, AmpCrystal = 98, BarrierPost = 99, MeteorOre = 100;
    public static Texture2D TraderTex, TravelerTex;

    static void ExtraTiles3(Image img, Random r)
    {
        // забытая технология: постамент со светящейся табличкой
        var c = new Cv(img, TechCache * 16, 0);
        c.R(3, 13, 10, 2, H("#4a5470")); c.R(5, 8, 6, 5, H("#6a7a9c")); c.R(5, 8, 1, 5, H("#8e9ec0"));
        c.E(8, 5, 4.5f, 4f, H("#1a2a40")); c.E(8, 5, 3.4f, 3f, H("#8ff0ff")); c.E(8, 5, 1.4f, 1.4f, H("#ffffff"));
        foreach (var p in new[] { (3, 3), (13, 3), (8, 0), (8, 10) }) c.P(p.Item1, p.Item2, H("#bff9ff"));
        c.Outline(H("#0a1220"));
        // древний кристалл-шпиль
        c = new Cv(img, Spire * 16, 0);
        c.R(3, 13, 10, 2, H("#4a3a5a"));
        for (int y = 0; y < 13; y++) { int hw = y < 4 ? 1 + y / 2 : y < 9 ? 3 : 3 - (y - 9) / 2; c.R(8 - hw, y, hw * 2, 1, y % 2 == 0 ? H("#e07aff") : H("#f0a8ff")); }
        c.R(7, 2, 1, 9, H("#ffffff")); c.P(10, 5, H("#ffe0ff")); c.P(5, 8, H("#ffe0ff"));
        c.R(2, 11, 3, 3, H("#c070ff")); c.R(11, 10, 3, 4, H("#c070ff")); c.Outline(H("#2a0a3a"));
        // алтарь лучей
        for (int on = 0; on < 2; on++)
        {
            c = new Cv(img, (on == 0 ? Altar : AltarOn) * 16, 0);
            c.E(8, 12, 7, 3, H("#4a5470")); c.R(5, 7, 6, 5, H("#6a7a9c")); c.E(8, 6, 5.5f, 2.8f, H("#8e9ec0"));
            c.E(8, 5.5f, 3.6f, 1.6f, on == 0 ? H("#1a1e2b") : H("#ffe27a"));
            if (on == 1) { c.P(8, 1, H("#ffe27a")); c.P(8, 2, H("#fffbe0")); c.P(3, 4, H("#ffe27a")); c.P(13, 4, H("#ffe27a")); }
            c.Outline(H("#10141f"));
        }
        // лавка (3 уровня)
        for (int lv = 1; lv <= 3; lv++)
        {
            c = new Cv(img, (Stall + lv - 1) * 16, 0);
            c.R(2, 9, 12, 6, H("#8a5a2e")); c.R(2, 9, 12, 1, H("#a8743d")); c.R(3, 11, 4, 3, H("#d9b27c")); c.R(9, 11, 4, 3, H("#c9a24a"));
            for (int x = 1; x < 15; x++) c.R(x, 3, 1, 5, (x / 2) % 2 == 0 ? H(lv == 3 ? "#c9a24a" : "#c04a4a") : H("#e8e0d0"));
            c.R(1, 2, 14, 1, H("#6b4423")); c.R(2, 8, 1, 1, H("#6b4423")); c.R(13, 8, 1, 1, H("#6b4423"));
            if (lv >= 2) { c.P(2, 12, H("#ffd86b")); c.P(12, 10, H("#ffd86b")); c.R(7, 9, 2, 2, H("#ffe27a")); }
            if (lv >= 3) { c.P(0, 2, H("#ffe27a")); c.P(15, 2, H("#ffe27a")); c.P(8, 1, H("#fffbe0")); }
            c.Outline(H("#14110c"));
        }
        // узел снабжения (3 уровня)
        for (int lv = 1; lv <= 3; lv++)
        {
            c = new Cv(img, (Hub + lv - 1) * 16, 0);
            c.R(3, 12, 10, 3, H("#5a5f6e")); c.R(3, 12, 10, 1, H("#7a8094")); c.R(6, 6, 4, 6, H("#6a7080"));
            c.R(7, 1 + (lv >= 2 ? 0 : 1), 2, 5, H("#8a92a8")); c.E(8, 1.5f, 2, 2, lv >= 3 ? H("#fff2b0") : H("#8ff0ff"));
            c.R(1, 9, 3, 2, H("#4a4f5e")); c.R(12, 9, 3, 2, H("#4a4f5e")); c.P(2, 9, H("#8ff0ff")); c.P(13, 9, H("#8ff0ff"));
            if (lv >= 2) { c.R(4, 5, 8, 1, H("#8a92a8")); }
            if (lv >= 3) { c.P(4, 4, H("#ffe27a")); c.P(11, 4, H("#ffe27a")); }
            c.Outline(H("#14161f"));
        }
        // перерабатывающий цех (3 уровня)
        for (int lv = 1; lv <= 3; lv++)
        {
            c = new Cv(img, (Refinery + lv - 1) * 16, 0);
            c.R(1, 7, 14, 8, H("#5a5f6e")); c.R(1, 7, 14, 1, H("#8a92a8")); c.R(3, 10, 5, 5, H("#14121a")); c.R(4, 11, 3, 3, H("#ff9a2e"));
            c.E(11.5f, 11, 2.6f, 2.6f, H("#9aa0ae")); c.E(11.5f, 11, 1.1f, 1.1f, H("#2a2f3e"));
            c.R(2, 2, 2, 5, H("#4a4f5e")); c.R(2, 1, 4, 1, H("#7a8094"));
            if (lv >= 2) { c.R(8, 3, 6, 1, H("#7a8094")); c.R(13, 3, 1, 4, H("#7a8094")); }
            if (lv >= 3) { c.E(8, 4, 2f, 2f, H("#9fe8ff")); c.P(8, 4, H("#ffffff")); c.P(5, 1, H("#cfd6e2")); }
            c.Outline(H("#14110c"));
        }
        // усилитель света
        c = new Cv(img, AmpCrystal * 16, 0);
        c.R(4, 13, 8, 2, H("#4a5470"));
        for (int y = 2; y < 13; y++) { int hw = y < 6 ? 1 + (y - 2) / 2 : 3 - (y - 6) / 4; c.R(8 - hw, y, hw * 2, 1, y % 2 == 0 ? H("#ffb0ff") : H("#9fe8ff")); }
        c.R(7, 4, 1, 6, H("#ffffff")); c.P(4, 9, H("#9fe8ff")); c.P(12, 8, H("#ffb0ff")); c.Outline(H("#2a1040"));
        // столб-барьер
        c = new Cv(img, BarrierPost * 16, 0);
        c.R(5, 13, 6, 2, H("#4a5470")); c.R(7, 5, 2, 8, H("#6a7a9c")); c.E(8, 4, 3.4f, 3.4f, H("#2a3a6a")); c.E(8, 4, 2.4f, 2.4f, H("#8ff0ff")); c.P(7, 3, H("#ffffff"));
        c.P(4, 4, H("#8ff0ff")); c.P(12, 4, H("#8ff0ff")); c.Outline(H("#0e1430"));
        // метеоритная руда
        c = new Cv(img, MeteorOre * 16, 0);
        DrawRock(c, H("#2a2630"), H("#4a4458"), H("#18141e"));
        foreach (var p in new[] { (5, 6), (9, 5), (7, 10), (11, 9) }) { c.R(p.Item1, p.Item2, 2, 3, H("#fff2b0")); c.P(p.Item1, p.Item2, H("#ffffff")); c.P(p.Item1 + 1, p.Item2 + 2, H("#ffd04a")); }
        c.Outline(H("#0a080e"));
    }

    static void ExtraEntities3()
    {
        var img = NewImg(32);
        for (int f = 0; f < 2; f++)
        {
            var c = new Cv(img, f * 16, 0);
            c.R(4, 7, 8, 7, H("#6a3a8a")); c.R(5, 8, 6, 5, H("#8a5aaa")); c.E(8, 5, 3f, 3f, H("#e8c8a0")); c.R(2, 3, 12, 1, H("#4a2a6a")); c.R(4, 1, 8, 3, H("#5a3a7a"));
            c.P(7, 5, H("#1a1a2a")); c.P(9, 5, H("#1a1a2a")); c.R(3, 8, 2, 6, H("#c9a24a")); c.R(11, 9 + f, 2, 3, H("#e8c8a0"));
            c.R(5, 14 + f, 2, 1, H("#3a2a4a")); c.R(9, 14 + (1 - f), 2, 1, H("#3a2a4a")); c.P(8, 11, H("#ffd86b"));
            c.Outline(H("#140a1e"));
        }
        TraderTex = ImageTexture.CreateFromImage(img);
        img = NewImg(32);
        for (int f = 0; f < 2; f++)
        {
            var c = new Cv(img, f * 16, 0);
            c.R(4, 7, 8, 7, H("#6a707c")); c.R(5, 8, 6, 5, H("#8a909c")); c.E(8, 5, 3f, 3f, H("#e8c8a0")); c.R(4, 2, 8, 3, H("#4a505c"));
            c.P(7, 5, H("#1a1a2a")); c.P(9, 5, H("#1a1a2a")); c.R(3, 9 + f, 2, 3, H("#e8c8a0")); c.R(11, 9 + (1 - f), 2, 3, H("#e8c8a0"));
            c.R(5, 14 + f, 2, 1, H("#3a3f4a")); c.R(9, 14 + (1 - f), 2, 1, H("#3a3f4a")); c.R(12, 6, 2, 5, H("#c9a24a"));
            c.Outline(H("#101418"));
        }
        TravelerTex = ImageTexture.CreateFromImage(img);
    }
}
