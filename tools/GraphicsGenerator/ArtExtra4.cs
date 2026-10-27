using Godot;
using System;

namespace Lamplighter;

/// <summary>Графика этапа 5: Ледяной монастырь, Сердце Тьмы, эндгейм-постройки и новые существа.</summary>
public static partial class Art
{
    public const int Snow = 101, IceFloor = 104, HeartFloor = 106, VoidG = 109, FrostOre = 110, Pine = 111, IceRuin = 112, IceBlock = 113, VoidObj = 114,
        Monolith = 115, Tablet = 116, FalseLantern = 117, Pylon = 118, PylonOn = 119, ShadowPillar = 120, Fragment = 121, RiftGate = 122,
        Statue = 123, Fountain = 124, Banner = 125, Cryo = 126, LightBridge = 129;

    public static Texture2D WolfTex, PriestTex, SentinelTex, WinterTex, MirageTex, WeaverTex, StalkerTex, SourceTex;

    static void ExtraTiles4(Image img, Random r)
    {
        BridgeTile(img);
        for (int v = 0; v < 3; v++)
        {
            var c = new Cv(img, (Snow + v) * 16, 0);
            c.Fill(H("#dfe8f2")); c.Speckle(r, 40, H("#c8d6e6"), opaqueOnly: false); c.Speckle(r, 25, H("#f4f8fc"), opaqueOnly: false); c.Speckle(r, 6 + v * 2, H("#aebfd6"), opaqueOnly: false);
        }
        for (int v = 0; v < 2; v++)
        {
            var c = new Cv(img, (IceFloor + v) * 16, 0);
            c.Fill(H("#8fc4e0")); c.Speckle(r, 30, H("#7fb6d6"), opaqueOnly: false); c.Speckle(r, 18, H("#bfe6f8"), opaqueOnly: false);
            for (int i = 0; i < 3; i++) c.R(r.Next(0, 10), r.Next(1, 15), r.Next(3, 7), 1, H("#d8f2ff"));
            c.R(2 + v * 3, 4 + v * 5, 1, 4, H("#6aa8cc")); c.P(5, 9, H("#ffffff"));
        }
        for (int v = 0; v < 3; v++)
        {
            var c = new Cv(img, (HeartFloor + v) * 16, 0);
            c.Fill(H("#2a1f3a")); c.Speckle(r, 40, H("#3a2a52"), opaqueOnly: false); c.Speckle(r, 30, H("#1a1226"), opaqueOnly: false); c.Speckle(r, 3 + v, H("#8a6ac0"), opaqueOnly: false);
        }
        var vg = new Cv(img, VoidG * 16, 0);
        vg.Fill(H("#0a0612")); vg.Speckle(r, 6, H("#3a2a5a"), opaqueOnly: false); vg.Speckle(r, 3, H("#b8a0ff"), opaqueOnly: false);

        var c2 = new Cv(img, FrostOre * 16, 0);
        DrawRock(c2, H("#3a4a60"), H("#5a6e8c"), H("#242e40"));
        foreach (var p in new[] { (4, 5, 5), (8, 3, 7), (11, 6, 4) })
        {
            for (int k = 0; k < p.Item3; k++) { c2.P(p.Item1, p.Item2 + p.Item3 - k, H("#9fe8ff")); c2.P(p.Item1 + 1, p.Item2 + p.Item3 - k, H("#e8fcff")); }
            c2.P(p.Item1, p.Item2, H("#ffffff"));
        }
        c2.Outline(H("#101a28"));
        var pn = new Cv(img, Pine * 16, 0);
        pn.R(7, 11, 2, 4, H("#4a3320"));
        for (int i = 0; i < 3; i++) { int w = 3 + i * 2; int y = 2 + i * 3; pn.R(8 - w, y, w * 2, 3, H("#2a5a3a")); pn.R(8 - w, y, w * 2, 1, H("#f0f6fc")); pn.R(8 - w + 1, y + 1, 2, 1, H("#dfe8f2")); }
        pn.Outline(H("#10241a"));
        var ir = new Cv(img, IceRuin * 16, 0);
        ir.R(2, 7, 12, 8, H("#7a8aa0")); ir.R(2, 6, 12, 2, H("#eef4fa")); ir.R(3, 3, 3, 3, H("#7a8aa0")); ir.R(3, 2, 3, 1, H("#eef4fa")); ir.R(10, 4, 3, 2, H("#7a8aa0"));
        ir.R(4, 9, 2, 3, H("#4a566a")); ir.R(9, 10, 2, 2, H("#4a566a")); ir.P(5, 8, H("#bfe9ff")); ir.P(11, 8, H("#bfe9ff")); ir.R(6, 8, 1, 2, H("#bfe9ff"));
        ir.Outline(H("#1a2030"));
        var ib = new Cv(img, IceBlock * 16, 0);
        ib.R(1, 2, 14, 13, H("#9fd4f0")); ib.R(1, 2, 14, 2, H("#e8faff")); ib.R(1, 2, 2, 13, H("#d0f0ff")); ib.R(12, 4, 3, 11, H("#78b4d8"));
        ib.R(4, 6, 4, 1, H("#ffffff")); ib.P(4, 7, H("#ffffff")); ib.R(7, 9, 1, 4, H("#6aa8cc")); ib.Outline(H("#2a5a80"));
        var vo = new Cv(img, VoidObj * 16, 0);
        vo.Fill(H("#06030c")); vo.Speckle(r, 10, H("#2a1a4a"), opaqueOnly: false); vo.Speckle(r, 4, H("#9a80ff"), opaqueOnly: false); vo.R(0, 0, 16, 1, H("#1a1030")); vo.R(0, 15, 16, 1, H("#1a1030"));
        var mo = new Cv(img, Monolith * 16, 0);
        mo.R(4, 14, 8, 1, H("#2a2038")); mo.R(5, 1, 6, 13, H("#3a2e50")); mo.R(5, 1, 1, 13, H("#5a4a7a")); mo.R(7, 0, 2, 1, H("#3a2e50"));
        foreach (var p in new[] { (7, 3), (8, 5), (7, 7), (8, 9), (7, 11) }) { mo.P(p.Item1, p.Item2, H("#c8a8ff")); mo.P(p.Item1 + 1, p.Item2, H("#8a6ac0")); }
        mo.Outline(H("#0a0614"));
        var tb = new Cv(img, Tablet * 16, 0);
        tb.R(4, 13, 8, 2, H("#4a4a58")); tb.R(5, 3, 6, 10, H("#8a8a9a")); tb.R(5, 3, 1, 10, H("#b0b0c0")); tb.R(6, 2, 4, 1, H("#8a8a9a"));
        foreach (var p in new[] { (7, 5), (8, 6), (7, 8), (9, 9), (7, 10) }) tb.P(p.Item1, p.Item2, H("#ffd86b"));
        tb.Outline(H("#14141c"));
        var fl = new Cv(img, FalseLantern * 16, 0);
        fl.R(7, 7, 2, 8, H("#3a2a4a")); fl.R(5, 14, 6, 1, H("#241a30")); fl.R(5, 2, 6, 6, H("#c070ff")); fl.R(7, 3, 2, 4, H("#f0d0ff")); fl.R(4, 1, 8, 2, H("#3a2a4a")); fl.R(5, 7, 6, 1, H("#3a2a4a"));
        fl.P(8, 0, H("#3a2a4a")); fl.P(4, 4, H("#8a40c0")); fl.Outline(H("#10081a"));
        for (int on = 0; on < 2; on++)
        {
            var py = new Cv(img, (on == 0 ? Pylon : PylonOn) * 16, 0);
            py.R(4, 13, 8, 2, H("#4a5470")); py.R(6, 5, 4, 8, H("#6a7a9c")); py.R(6, 5, 1, 8, H("#8e9ec0"));
            py.E(8, 4, 3.4f, 3.4f, on == 0 ? H("#2a3a5a") : H("#ffe27a")); if (on == 1) { py.E(8, 4, 1.6f, 1.6f, H("#ffffff")); py.P(3, 3, H("#ffe27a")); py.P(13, 3, H("#ffe27a")); py.P(8, 0, H("#fffbe0")); }
            else py.P(8, 4, H("#6a7a9c"));
            py.Outline(H("#0e1220"));
        }
        var sp = new Cv(img, ShadowPillar * 16, 0);
        for (int x = 0; x < 3; x++) { int bx = 2 + x * 5; for (int y = 2 + x % 2 * 2; y < 14; y++) { int hw = Math.Max(0, 2 - (y - 2 - x % 2 * 2) / 4); sp.R(bx + 1 - hw, y, hw * 2 + 1, 1, y % 2 == 0 ? H("#3a1a5a") : H("#4a2a70")); } }
        sp.R(0, 13, 16, 2, H("#1a0a30")); sp.P(4, 5, H("#c080ff")); sp.P(9, 7, H("#c080ff")); sp.P(13, 4, H("#c080ff")); sp.Outline(H("#08020f"));
        var fr = new Cv(img, Fragment * 16, 0);
        fr.E(8, 8, 6, 4, H("#4a4458")); fr.E(8, 7, 5, 3, H("#6a6480")); fr.R(4, 11, 8, 3, H("#2a2438")); fr.R(5, 13, 6, 2, H("#18121e")); fr.P(6, 6, H("#9a90b8")); fr.P(10, 7, H("#9a90b8"));
        fr.Outline(H("#0a0610"));
        var rg = new Cv(img, RiftGate * 16, 0);
        rg.R(1, 2, 3, 13, H("#4a3e60")); rg.R(12, 2, 3, 13, H("#4a3e60")); rg.R(1, 1, 14, 2, H("#5a4e78")); rg.R(4, 3, 8, 12, H("#1a0a30"));
        for (int i = 0; i < 6; i++) rg.P(5 + (i * 3) % 6, 4 + i * 2, i % 2 == 0 ? H("#c080ff") : H("#e8c8ff")); rg.Outline(H("#0a0614"));
        var st = new Cv(img, Statue * 16, 0);
        st.R(4, 13, 8, 2, H("#6a6a78")); st.R(6, 6, 4, 7, H("#9a9aa8")); st.E(8, 4, 2.4f, 2.4f, H("#b0b0c0")); st.R(11, 6, 2, 3, H("#ffd86b")); st.P(11, 6, H("#fffbe0")); st.R(5, 8, 1, 4, H("#b0b0c0"));
        st.Outline(H("#14141c"));
        var fo = new Cv(img, Fountain * 16, 0);
        fo.E(8, 11, 7, 3.5f, H("#8a8a98")); fo.E(8, 10.5f, 5.5f, 2.6f, H("#4a90c0")); fo.R(7, 4, 2, 6, H("#9a9aa8")); fo.R(5, 4, 6, 1, H("#b0b0c0"));
        fo.P(4, 2, H("#9fe8ff")); fo.P(12, 2, H("#9fe8ff")); fo.P(8, 1, H("#ffffff")); fo.P(7, 10, H("#d8f4ff")); fo.Outline(H("#14141c"));
        var bn = new Cv(img, Banner * 16, 0);
        bn.R(3, 1, 1, 14, H("#6b4423")); bn.R(4, 2, 9, 7, H("#c04a4a")); bn.R(4, 2, 9, 1, H("#ffd86b")); bn.R(7, 4, 3, 3, H("#ffe27a")); bn.P(8, 5, H("#fffbe0"));
        bn.R(4, 9, 3, 2, H("#c04a4a")); bn.R(9, 9, 3, 2, H("#c04a4a")); bn.Outline(H("#14080a"));
        for (int lv = 1; lv <= 3; lv++)
        {
            var cr = new Cv(img, (Cryo + lv - 1) * 16, 0);
            cr.R(2, 9, 12, 6, H("#5a6a80")); cr.R(2, 9, 12, 1, H("#8aa0b8")); cr.R(4, 2, 5, 7, H("#9fd4f0")); cr.R(4, 2, 5, 1, H("#e8faff")); cr.R(5, 3, 1, 5, H("#ffffff"));
            cr.R(11, 3, 2, 6, H("#4a566a")); cr.R(10, 3, 4, 1, H("#8aa0b8")); cr.P(11, 12, H("#9fe8ff")); cr.P(5, 12, H("#9fe8ff"));
            if (lv >= 2) { cr.R(9, 5, 2, 1, H("#bfe9ff")); cr.P(12, 2, H("#e8faff")); }
            if (lv >= 3) { cr.P(3, 1, H("#ffffff")); cr.P(9, 1, H("#bff4ff")); cr.R(2, 14, 12, 1, H("#9fe8ff")); }
            cr.Outline(H("#101a28"));
        }
    }

    static void BridgeTile(Image img)
    {
        var c = new Cv(img, LightBridge * 16, 0);
        c.R(0, 4, 16, 8, H("#2a1a4a")); c.R(0, 4, 16, 1, H("#ffd86b")); c.R(0, 11, 16, 1, H("#ffd86b"));
        for (int x = 1; x < 16; x += 3) c.R(x, 5, 1, 6, H("#8a6ac0")); c.R(0, 7, 16, 2, H("#fff2b0")); c.P(4, 6, H("#ffffff")); c.P(11, 9, H("#ffffff"));
    }

    static void ExtraEntities4()
    {
        var img = NewImg(32);
        for (int f = 0; f < 2; f++)
        {
            var c = new Cv(img, f * 16, 0);
            c.E(8, 10, 6, 3.4f, H("#9fb8d0")); c.E(12, 8, 3, 2.6f, H("#b8d0e4")); c.R(14, 8, 2, 1, H("#6a88a8")); c.P(13, 7, H("#ff5a5a")); c.P(11, 5, H("#9fb8d0")); c.P(13, 5, H("#9fb8d0"));
            c.R(2, 8 + f, 3, 2, H("#9fb8d0")); c.R(4, 12, 1, 3 - f, H("#7a98b4")); c.R(7, 12, 1, 2 + f, H("#7a98b4")); c.R(10, 12, 1, 3 - f, H("#7a98b4")); c.R(12, 12, 1, 2 + f, H("#7a98b4"));
            c.Speckle(new Random(3 + f), 6, H("#ffffff"), 4, 7, 12, 11); c.Outline(H("#101c2a"));
        }
        WolfTex = ImageTexture.CreateFromImage(img);
        img = NewImg(32);
        for (int f = 0; f < 2; f++)
        {
            var c = new Cv(img, f * 16, 0);
            c.R(4, 7, 8, 8, H("#8ab4dc")); c.R(5, 8, 6, 6, H("#bfe0f8")); c.E(8, 5, 3.2f, 3.2f, H("#6a98c4")); c.R(6, 4, 4, 3, H("#1a2a40")); c.P(6, 5, H("#9fffff")); c.P(9, 5, H("#9fffff"));
            c.R(12, 3 + f, 1, 11, H("#6b4423")); c.E(12.5f, 2 + f, 2, 2.4f, H("#e8faff")); c.P(12, 2 + f, H("#ffffff"));
            c.R(5, 15 - f, 2, 1, H("#4a6a90")); c.R(9, 14 + f, 2, 1, H("#4a6a90")); c.Outline(H("#0c1828"));
        }
        PriestTex = ImageTexture.CreateFromImage(img);
        img = NewImg(32);
        for (int f = 0; f < 2; f++)
        {
            var c = new Cv(img, f * 16, 0);
            c.R(3, 4, 10, 10, H("#6a88a8")); c.R(4, 5, 8, 8, H("#8aa8c8")); c.R(3, 4, 10, 1, H("#bfe0f8")); c.R(5, 1, 6, 4, H("#7a98b8"));
            c.R(6, 2, 1, 1, H("#9fffff")); c.R(9, 2, 1, 1, H("#9fffff")); c.R(1, 5 + f, 3, 7, H("#bfe9ff")); c.R(1, 5 + f, 1, 7, H("#ffffff")); c.R(12, 6 + (1 - f), 3, 6, H("#4a6a88"));
            c.R(4, 14 - f, 3, 2, H("#4a6a88")); c.R(9, 14 - (1 - f), 3, 2, H("#4a6a88")); c.Outline(H("#0c1828"));
        }
        SentinelTex = ImageTexture.CreateFromImage(img);
        img = NewImg(32);
        for (int f = 0; f < 2; f++)
        {
            var c = new Cv(img, f * 16, 0);
            c.E(8, 9, 5.5f, 6, H("#7ab0d8")); c.E(8, 8, 4.2f, 4.8f, H("#bfe6fa")); c.E(8, 8, 2.6f, 2.6f, H("#ffffff")); c.E(8, 8, 1.2f, 1.4f, H("#3a7ad0"));
            c.R(1, 0 + f, 1, 6, H("#e8faff")); c.R(0, 2 + f, 3, 1, H("#e8faff")); c.R(14, 0 + f, 1, 6, H("#e8faff")); c.R(13, 2 + f, 3, 1, H("#e8faff"));
            c.R(4, 13, 2, 3 - f, H("#9fd4f0")); c.R(10, 13, 2, 2 + f, H("#9fd4f0")); c.Speckle(new Random(9 + f), 8, H("#ffffff"), 4, 5, 12, 12); c.Outline(H("#10284a"));
        }
        WinterTex = ImageTexture.CreateFromImage(img);
        img = NewImg(32);
        for (int f = 0; f < 2; f++)
        {
            var c = new Cv(img, f * 16, 0);
            c.R(4, 6, 8, 9, H("#4a2a70")); c.R(5, 7, 6, 7, H("#7a4ab0")); c.E(8, 5, 3.4f, 3.4f, H("#c8a0f0")); c.R(5, 4, 1, 3, H("#1a0a30")); c.R(10, 5, 1, 2, H("#1a0a30")); c.P(7, 6, H("#ffd86b")); c.P(9, 4, H("#ffd86b"));
            c.R(2, 9, 2, 1, H("#e8c8ff")); c.R(12, 8, 2, 1, H("#e8c8ff")); for (int x = 4; x < 12; x++) if ((x + f) % 2 == 0) c.P(x, 15, Clear);
            c.Outline(H("#12062a"));
        }
        MirageTex = ImageTexture.CreateFromImage(img);
        img = NewImg(32);
        for (int f = 0; f < 2; f++)
        {
            var c = new Cv(img, f * 16, 0);
            c.E(8, 8, 3.6f, 3f, H("#2a1a40")); c.E(8, 6, 2.6f, 2.2f, H("#3a2a58")); c.P(7, 6, H("#c080ff")); c.P(9, 6, H("#c080ff")); c.P(8, 7, H("#e8c8ff"));
            for (int i = 0; i < 4; i++) { c.R(1 + i, 8 + (i + f) % 2 * 2, 1, 4, H("#2a1a40")); c.R(14 - i, 8 + (i + f) % 2 * 2, 1, 4, H("#2a1a40")); }
            c.R(5, 11, 6, 2, H("#2a1a40")); c.Outline(H("#0a0414"));
        }
        WeaverTex = ImageTexture.CreateFromImage(img);
        img = NewImg(32);
        for (int f = 0; f < 2; f++)
        {
            var c = new Cv(img, f * 16, 0);
            c.R(6, 4, 4, 9, H("#1a1a2a")); c.R(6, 4, 4, 1, H("#3a3a58")); c.R(6, 5, 4, 1, H("#06060c")); c.P(7, 5, H("#9fffff")); c.P(8, 5, H("#9fffff")); c.P(9, 5, H("#9fffff"));
            c.R(2, 7 + f, 4, 1, H("#cfd6e2")); c.R(10, 8 - f, 4, 1, H("#cfd6e2")); c.R(5, 13, 2, 2 + f, H("#1a1a2a")); c.R(9, 13, 2, 3 - f, H("#1a1a2a"));
            c.Outline(H("#06060c"));
        }
        StalkerTex = ImageTexture.CreateFromImage(img);
        img = NewImg(32);
        for (int f = 0; f < 2; f++)
        {
            var c = new Cv(img, f * 16, 0);
            c.E(8, 8, 6.5f, 6.5f, H("#3a1a60")); c.E(8, 8, 5.2f, 5.2f, H("#12081e")); c.E(8, 8, 3.2f, 3.2f, H("#e8e0ff")); c.E(8, 8, 1.6f, 1.8f, H("#0a0414"));
            for (int i = 0; i < 8; i++) { float a = i * 6.2832f / 8 + f * .3f; c.P(8 + (int)(Math.Cos(a) * 7), 8 + (int)(Math.Sin(a) * 7), H("#c080ff")); }
            c.P(7, 7, H("#ffffff")); c.Outline(H("#0a0414"));
        }
        SourceTex = ImageTexture.CreateFromImage(img);
    }

    static void ExtraIcons4(Image img)
    {
        var c = new Cv(img, 19 * 16, 0);
        for (int y = 0; y < 16; y++) { int w = y < 8 ? 1 + y / 2 : 4 - (y - 8) / 2; c.R(8 - w, y, w * 2, 1, y % 2 == 0 ? H("#e8faff") : H("#9fd4f0")); }
        c.R(7, 2, 1, 9, H("#ffffff")); c.P(5, 8, H("#ffffff")); c.Outline(H("#10305a"));
        c = new Cv(img, 20 * 16, 0);
        for (int y = 1; y < 15; y++) { int w = y < 7 ? 1 + y / 2 : 4 - (y - 7) / 2; c.R(8 - w, y, w * 2, 1, y % 2 == 0 ? H("#c070ff") : H("#8a40c0")); }
        c.R(7, 3, 1, 8, H("#e8c8ff")); c.R(5, 7, 6, 1, H("#ffd86b")); c.Outline(H("#1a0a30"));
    }
}
