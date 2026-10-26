using Godot;
using System;

namespace Lamplighter;

/// <summary>Графика этапа 2: места интереса, здания по уровням, новые существа.</summary>
public static partial class Art
{
    public const int Chest = 15, ChestOpen = 16, Ruin = 17, House = 18, Tent = 19, Shrine = 20, ShrineUsed = 21,
        AetherOre = 22, Cage = 23, BossOff = 24, BossOn = 25, Obelisk = 26, ObeliskUsed = 27,
        Warehouse = 28, Sawmill = 31, Mine = 34, Forge = 37;

    public static ImageTexture VillagerTex, SpitterTex, BomberTex;

    static void ExtraTiles(Image img, Random r)
    {
        // сундук
        var c = new Cv(img, Chest * 16, 0);
        c.R(3, 7, 10, 7, H("#8a5a2e")); c.R(3, 5, 10, 3, H("#a8743d")); c.R(3, 5, 10, 1, H("#c68f52"));
        c.R(3, 5, 1, 9, H("#4a4f5e")); c.R(12, 5, 1, 9, H("#4a4f5e")); c.R(7, 8, 2, 3, H("#ffd86b")); c.P(8, 9, H("#8a6a10"));
        c.Outline(H("#1a1008"));
        c = new Cv(img, ChestOpen * 16, 0);
        c.R(3, 8, 10, 6, H("#8a5a2e")); c.R(4, 8, 8, 2, H("#1b1208")); c.R(3, 2, 10, 3, H("#a8743d")); c.R(3, 4, 10, 1, H("#6b4423"));
        c.R(3, 8, 1, 6, H("#4a4f5e")); c.R(12, 8, 1, 6, H("#4a4f5e")); c.P(7, 8, H("#ffe27a")); c.P(9, 9, H("#fffbe0"));
        c.Outline(H("#1a1008"));
        // руина
        c = new Cv(img, Ruin * 16, 0);
        c.R(4, 6, 8, 9, H("#7a7f8c")); c.R(5, 4, 4, 2, H("#7a7f8c")); c.R(9, 5, 2, 1, H("#7a7f8c")); c.R(4, 6, 2, 9, H("#979cab"));
        c.R(10, 8, 2, 7, H("#5b5f6c")); c.P(7, 8, H("#3b3e48")); c.P(8, 9, H("#3b3e48")); c.P(7, 11, H("#3b3e48"));
        c.Speckle(r, 6, H("#4a8742"), 4, 5, 12, 15);
        c.Outline(H("#1c1d24"));
        // дом
        c = new Cv(img, House * 16, 0);
        c.R(2, 8, 12, 7, H("#a88a62")); c.R(2, 8, 12, 1, H("#c4a67c"));
        for (int y = 2; y < 8; y++) { int hw = 2 + (y - 2); c.R(8 - hw, y, hw * 2, 1, y % 2 == 0 ? H("#7a3b2e") : H("#8c4a3a")); }
        c.R(7, 11, 3, 4, H("#3a2512")); c.R(3, 10, 3, 2, H("#1b1e2b")); c.R(11, 10, 2, 2, H("#1b1e2b"));
        c.P(6, 3, Clear); c.P(7, 4, H("#2a1a14")); c.Speckle(r, 5, H("#4a8742"), 2, 8, 14, 15);
        c.Outline(H("#1a1008"));
        // шатёр лагеря
        c = new Cv(img, Tent * 16, 0);
        for (int y = 3; y < 15; y++) { int hw = (int)((y - 2) * .62f) + 1; c.R(8 - hw, y, hw * 2, 1, y % 3 == 0 ? H("#6a2a2a") : H("#8c3b3b")); }
        c.R(7, 10, 2, 5, H("#1a1010")); c.P(8, 1, H("#6b4423")); c.P(8, 2, H("#6b4423")); c.P(9, 1, H("#c04040"));
        c.Outline(H("#14080a"));
        // святилище
        for (int on = 0; on < 2; on++)
        {
            c = new Cv(img, (on == 0 ? Shrine : ShrineUsed) * 16, 0);
            c.R(3, 12, 10, 3, H("#6e7790")); c.R(6, 7, 4, 5, H("#8e98b4")); c.R(4, 5, 8, 3, H("#a0aac6")); c.R(5, 4, 6, 1, H("#6e7790"));
            if (on == 0) { c.R(7, 1, 2, 3, H("#8ff0ff")); c.P(8, 2, H("#ffffff")); c.P(6, 3, H("#5fe4ff")); c.P(9, 3, H("#5fe4ff")); }
            else { c.P(7, 3, H("#454a5c")); c.P(8, 3, H("#454a5c")); }
            c.Outline(H("#14161f"));
        }
        // эфирный кристалл
        c = new Cv(img, AetherOre * 16, 0);
        DrawRock(c, H("#3a2f5a"), H("#5a4a8a"), H("#241c3d"));
        foreach (var p in new[] { (5, 3, 4), (9, 4, 5), (7, 6, 3), (11, 7, 3) })
        {
            for (int k = 0; k < p.Item3; k++) { c.P(p.Item1, p.Item2 + 7 - k, H("#c070ff")); c.P(p.Item1 + 1, p.Item2 + 7 - k, H("#efc8ff")); }
            c.P(p.Item1, p.Item2 + 7 - p.Item3, H("#ffffff"));
        }
        c.Outline(H("#140c26"));
        // клетка
        c = new Cv(img, Cage * 16, 0);
        c.R(3, 2, 10, 2, H("#4a4f5e")); c.R(3, 13, 10, 2, H("#4a4f5e")); c.R(4, 4, 8, 9, H("#15121c"));
        foreach (int x in new[] { 4, 7, 9, 12 }) c.R(x, 4, 1, 9, H("#8a8f9c"));
        c.P(8, 9, H("#f0c8a0")); c.P(8, 8, H("#f0c8a0")); c.R(7, 10, 3, 2, H("#4a8a4a"));
        c.Outline(H("#0c0a12"));
        // фонарь Хранителя
        for (int on = 0; on < 2; on++)
        {
            c = new Cv(img, (on == 0 ? BossOff : BossOn) * 16, 0);
            c.R(2, 13, 12, 2, H("#3d3350")); c.R(4, 11, 8, 2, H("#54476e"));
            c.R(6, 4, 4, 8, H("#5d4f7a")); c.R(6, 4, 1, 8, H("#7e6ea0")); c.R(9, 4, 1, 8, H("#40355a"));
            c.R(3, 2, 10, 3, H("#6a5a8a")); c.R(4, 1, 2, 1, H("#8e7cb4")); c.R(10, 1, 2, 1, H("#8e7cb4")); c.R(7, 0, 2, 2, H("#8e7cb4"));
            if (on == 0) c.R(7, 5, 2, 4, H("#150f20"));
            else { c.R(7, 5, 2, 4, H("#d58cff")); c.R(7, 6, 2, 2, H("#ffffff")); c.P(2, 6, H("#e8b8ff")); c.P(13, 6, H("#e8b8ff")); }
            c.Outline(H("#0d0916"));
        }
        // обелиск
        for (int on = 0; on < 2; on++)
        {
            c = new Cv(img, (on == 0 ? Obelisk : ObeliskUsed) * 16, 0);
            c.R(4, 13, 8, 2, H("#4a5470")); c.R(6, 3, 4, 10, H("#5f6f94")); c.R(7, 2, 2, 1, H("#5f6f94")); c.R(6, 3, 1, 10, H("#7c8db4"));
            var rune = on == 0 ? H("#8ff0ff") : H("#3a4258");
            c.P(8, 5, rune); c.P(7, 7, rune); c.P(8, 8, rune); c.P(8, 10, rune); c.P(9, 7, rune);
            c.Outline(H("#12161f"));
        }
        for (int k = 0; k < 4; k++)
            for (int lv = 1; lv <= 3; lv++)
            {
                int[] baseIdx = { Warehouse, Sawmill, Mine, Forge };
                DrawBuilding(new Cv(img, (baseIdx[k] + lv - 1) * 16, 0), k, lv, r);
            }
        ExtraTiles2(img, r);
    }

    // kind: 0 склад, 1 лесопилка, 2 шахта, 3 кузница
    static void DrawBuilding(Cv c, int kind, int lv, Random r)
    {
        switch (kind)
        {
            case 0:
                c.R(2, 7, 12, 8, H("#8a5a2e")); c.R(2, 7, 12, 1, H("#a8743d"));
                var roof = lv == 1 ? H("#6b4423") : lv == 2 ? H("#8c3b3b") : H("#c9a24a");
                c.R(1, 4, 14, 3, roof); c.R(3, 3, 10, 1, roof); c.R(1, 6, 14, 1, new Color(roof.R * .7f, roof.G * .7f, roof.B * .7f));
                c.R(6, 10, 4, 5, H("#3a2512"));
                if (lv >= 2) { c.R(2, 13, 12, 2, H("#7a7f8c")); c.R(3, 8, 2, 2, H("#d9b27c")); c.R(11, 8, 2, 2, H("#d9b27c")); }
                if (lv >= 3) { c.R(1, 10, 2, 2, H("#ffd86b")); c.P(1, 10, H("#fffbe0")); c.P(8, 2, H("#ffe27a")); c.P(7, 3, H("#ffe27a")); c.P(9, 3, H("#ffe27a")); }
                break;
            case 1:
                c.R(1, 12, 14, 3, H("#6b4423")); c.R(1, 12, 14, 1, H("#8a5a2e"));
                c.R(2, 9, 5, 3, H("#a8743d")); c.R(2, 9, 5, 1, H("#d9b27c")); c.P(3, 10, H("#6b4423"));
                c.R(8, 4, 1, 8, H("#4a3320")); c.R(14 - 1, 4, 1, 8, H("#4a3320")); c.R(8, 4, 6, 1, H("#4a3320"));
                c.R(9, 8, 4, 1, H("#cfd6e2")); c.P(10, 7, H("#cfd6e2")); c.P(12, 7, H("#cfd6e2"));
                if (lv >= 2) { c.R(7, 2, 8, 2, H("#8c3b3b")); c.R(8, 1, 6, 1, H("#8c3b3b")); c.R(9, 6, 4, 2, H("#e9f0fb")); }
                if (lv >= 3) { c.R(2, 8, 5, 1, H("#d9b27c")); c.R(0, 13, 2, 2, H("#5a8ab8")); c.R(14, 13, 2, 2, H("#5a8ab8")); c.P(11, 5, H("#ffe27a")); c.P(10, 8, H("#ffffff")); }
                break;
            case 2:
                c.E(8, 11, 7, 5, H("#6a6a76")); c.E(6, 9, 3.5f, 2.5f, H("#8a8a96"));
                c.R(4, 6, 8, 9, H("#15121c")); c.R(3, 5, 10, 1, H("#6b4423")); c.R(3, 5, 1, 10, H("#6b4423")); c.R(12, 5, 1, 10, H("#6b4423"));
                if (lv >= 2) { c.R(4, 14, 8, 1, H("#9aa0ae")); c.R(5, 11, 4, 3, H("#5a5f6e")); c.R(5, 10, 4, 1, H("#cfd6e2")); c.P(6, 12, H("#e58a3d")); }
                if (lv >= 3) { c.R(2, 3, 12, 2, H("#8e98b4")); c.P(8, 7, H("#8ff0ff")); c.P(7, 8, H("#5fe4ff")); c.P(9, 8, H("#5fe4ff")); c.P(2, 2, H("#8ff0ff")); c.P(13, 2, H("#8ff0ff")); }
                break;
            default:
                c.R(1, 8, 14, 7, H("#74747f")); c.R(1, 8, 14, 1, H("#9a9aa6"));
                c.R(10, 1, 3, 7, H("#5d5d68")); c.R(10, 1, 3, 1, H("#74747f"));
                c.R(3, 10, 6, 5, H("#14121a"));
                var f1 = lv >= 3 ? H("#5fb4ff") : H("#ff9a2e"); var f2 = lv >= 3 ? H("#e8ffff") : H("#ffe27a");
                c.R(4, 11, 4, 3, f1); c.R(5, 12, 2, 2, f2);
                c.R(11, 11, 4, 2, H("#3a3f4d")); c.R(12, 10, 2, 1, H("#cfd6e2"));
                if (lv >= 2) { c.R(0, 14, 16, 1, H("#5d5d68")); c.R(12, 8, 1, 1, H("#ffd86b")); }
                if (lv >= 3) { c.R(11, 11, 4, 1, H("#c9a24a")); c.R(10, 0, 3, 1, H("#c9a24a")); }
                break;
        }
        if (lv >= 4)
        {
            c.P(1, 1, H("#e8eef6")); c.P(14, 1, H("#8ff0ff")); c.P(2, 2, H("#8ff0ff")); c.P(13, 2, H("#e8eef6"));
            c.R(1, 14, 14, 1, H("#cfd6e2")); c.P(8, 0, H("#8ff0ff"));
        }
        c.Outline(H("#14110c"));
    }

    static void ExtraEntities()
    {
        // житель
        var img = NewImg(32);
        for (int f = 0; f < 2; f++)
        {
            var c = new Cv(img, f * 16, 0);
            c.R(5, 8, 6, 5, H("#4a8a4a")); c.R(6, 10, 4, 3, H("#d9d2b8")); c.E(8, 5.5f, 3f, 3f, H("#f0c8a0"));
            c.R(4, 3, 8, 1, H("#c9a24a")); c.R(5, 2, 6, 2, H("#d9b84a")); c.P(7, 6, H("#1a1a2a")); c.P(9, 6, H("#1a1a2a"));
            c.R(3, 9 + f, 2, 3, H("#f0c8a0")); c.R(11, 9 + (1 - f), 2, 3, H("#f0c8a0"));
            if (f == 0) { c.R(5, 13, 2, 2, H("#4a3320")); c.R(9, 13, 2, 1, H("#4a3320")); }
            else { c.R(5, 13, 2, 1, H("#4a3320")); c.R(9, 13, 2, 2, H("#4a3320")); }
            c.Outline(H("#14110c"));
        }
        VillagerTex = ImageTexture.CreateFromImage(img);

        // плевун
        img = NewImg(32);
        for (int f = 0; f < 2; f++)
        {
            var c = new Cv(img, f * 16, 0);
            c.E(8, 9.5f, 5.8f, 5.2f, H("#3f5e36")); c.E(8, 7, 4.5f, 4f, H("#4a6a3a"));
            c.Speckle(new Random(9 + f), 14, H("#5e8648"), 3, 4, 13, 13);
            c.R(5, 11, 6, 2, H("#1a2a10")); c.R(6, 11, 4, 1, H("#b6ff5e"));
            c.R(5, 6, 2, 2, H("#e0ff6a")); c.R(9, 6, 2, 2, H("#e0ff6a")); c.P(6, 7, H("#1a2a10")); c.P(10, 7, H("#1a2a10"));
            c.P(4, 3 - f, H("#5e8648")); c.P(8, 2, H("#5e8648")); c.P(12, 3 - f, H("#5e8648"));
            c.R(4, 14 + f, 2, 1, H("#3f5e36")); c.R(10, 14 + (1 - f), 2, 1, H("#3f5e36"));
            c.Outline(H("#0e1808"));
        }
        SpitterTex = ImageTexture.CreateFromImage(img);

        // подрывник
        img = NewImg(32);
        for (int f = 0; f < 2; f++)
        {
            var c = new Cv(img, f * 16, 0);
            c.E(8, 9.5f, 5.6f, 5.4f, H("#7a2a2a")); c.E(7, 8, 2.5f, 2f, H("#a03a3a"));
            c.Speckle(new Random(21 + f), 10, H("#ff9a2e"), 3, 5, 13, 14);
            c.R(5, 8, 2, 2, H("#ffd86b")); c.R(9, 8, 2, 2, H("#ffd86b")); c.R(6, 12, 4, 1, H("#1a0808"));
            c.R(8, 2, 1, 3, H("#6b4423")); c.P(8, 1, f == 0 ? H("#ffe27a") : H("#ff7a2e")); c.P(7, 0 + f, H("#ffffff"));
            c.R(4, 14 + f, 2, 1, H("#5a1a1a")); c.R(10, 14 + (1 - f), 2, 1, H("#5a1a1a"));
            c.Outline(H("#1a0606"));
        }
        BomberTex = ImageTexture.CreateFromImage(img);
        ExtraEntities2();
    }

    static void ExtraIcons(Image img)
    {
        ExtraIcons2(img);
        // 7 эфир, 8 реликвия
        var c = new Cv(img, 7 * 16, 0);
        for (int y = 2; y < 14; y++) { int w = y < 8 ? 1 + (y - 2) / 2 : 3 - (y - 8) / 2; c.R(8 - w, y, w * 2, 1, H("#c070ff")); }
        c.R(7, 4, 1, 6, H("#f6d0ff")); c.P(10, 5, H("#efc8ff")); c.Outline(H("#2a0c4a"));
        c = new Cv(img, 8 * 16, 0);
        c.E(8, 8, 6, 6, H("#c9a24a")); c.E(8, 8, 3.5f, 3.5f, Clear); c.E(8, 8, 1.5f, 1.5f, H("#8ff0ff"));
        c.R(7, 1, 2, 2, H("#ffe27a")); c.R(7, 13, 2, 2, H("#ffe27a")); c.R(1, 7, 2, 2, H("#ffe27a")); c.R(13, 7, 2, 2, H("#ffe27a"));
        c.Outline(H("#3a2a08"));
    }
}
