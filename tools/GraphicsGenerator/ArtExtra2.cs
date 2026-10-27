using Godot;
using System;

namespace Lamplighter;

/// <summary>Графика этапа 3: шахты и пустоши.</summary>
public static partial class Art
{
    public const int CaveFloor = 40, WaterG = 43, AshFloor = 44, Scorch = 47, HotAsh = 48,
        CaveWall = 49, CrystalOre = 50, SilverOre = 51, Machine = 52, MachineOn = 53, Mirror1 = 54, Mirror2 = 55,
        Receiver = 56, ReceiverOn = 57, Emitter = 58, CrystalLantern = 59, CombatLantern = 60, Rubble = 61,
        BridgeBroken = 62, Bridge = 63, Deep = 64, Seal = 65, SecretDoor = 66, Drill = 67, Collector = 71,
        Wh4 = 75, Saw4 = 76, Mine4 = 77, Forge4 = 78, DeadTree = 79, Boulder = 80, Obsidian = 81, Ember = 82, AshRuin = 83, Vent = 84;

    public static Texture2D EelTex, LeechTex, BeetleTex, DeepTex, ImpTex, GolemTex, SpiritTex, AshBossTex;

    static void ExtraTiles2(Image img, Random r)
    {
        // ── земля шахт ──
        for (int v = 0; v < 3; v++)
        {
            var c = new Cv(img, (CaveFloor + v) * 16, 0);
            c.Fill(H("#3a3f4d")); c.Speckle(r, 40, H("#444a5a"), opaqueOnly: false); c.Speckle(r, 30, H("#2f333f"), opaqueOnly: false); c.Speckle(r, 5 + v * 2, H("#5a6072"), opaqueOnly: false);
        }
        var w = new Cv(img, WaterG * 16, 0);
        w.Fill(H("#2a5d8a")); w.Speckle(r, 30, H("#3d79ab"), opaqueOnly: false); w.Speckle(r, 20, H("#1f4a70"), opaqueOnly: false);
        for (int i = 0; i < 4; i++) { int x = r.Next(1, 11), y = r.Next(1, 15); w.R(x, y, 4, 1, H("#5aa0d0")); }
        w.P(5, 6, H("#bfe6ff"));
        // ── земля пустошей ──
        for (int v = 0; v < 3; v++)
        {
            var c = new Cv(img, (AshFloor + v) * 16, 0);
            c.Fill(H("#5b5754")); c.Speckle(r, 40, H("#6a6562"), opaqueOnly: false); c.Speckle(r, 30, H("#4a4745"), opaqueOnly: false); c.Speckle(r, 6 + v * 2, H("#8a8680"), opaqueOnly: false);
        }
        var sc = new Cv(img, Scorch * 16, 0);
        sc.Fill(H("#2b2624")); sc.Speckle(r, 50, H("#1a1514"), opaqueOnly: false); sc.Speckle(r, 8, H("#7a3a1a"), opaqueOnly: false);
        var hot = new Cv(img, HotAsh * 16, 0);
        hot.Fill(H("#4a3a34")); hot.Speckle(r, 30, H("#3a2c28"), opaqueOnly: false);
        for (int i = 0; i < 5; i++) { int x = r.Next(1, 12), y = r.Next(1, 14); hot.R(x, y, r.Next(3, 7), 1, H("#ff7a2a")); hot.P(x, y + 1, H("#ffb04a")); }

        // ── стена пещеры ──
        var cw = new Cv(img, CaveWall * 16, 0);
        cw.Fill(H("#2e3548")); cw.Speckle(r, 50, H("#3a4258"), opaqueOnly: false); cw.Speckle(r, 40, H("#1f2433"), opaqueOnly: false);
        cw.R(0, 0, 16, 1, H("#4a5470")); cw.R(0, 8, 16, 1, H("#252b3b")); cw.R(8, 0, 1, 8, H("#252b3b")); cw.R(4, 8, 1, 8, H("#252b3b"));
        // ── руды ──
        var co = new Cv(img, CrystalOre * 16, 0);
        co.Fill(H("#2e3548")); co.Speckle(r, 40, H("#3a4258"), opaqueOnly: false); co.R(0, 0, 16, 1, H("#4a5470"));
        foreach (var p in new[] { (4, 5, 6), (8, 3, 8), (11, 6, 5), (6, 9, 4) })
        {
            for (int k = 0; k < p.Item3; k++) { co.P(p.Item1, p.Item2 + p.Item3 - k, H("#8ab8ff")); co.P(p.Item1 + 1, p.Item2 + p.Item3 - k, H("#e4f4ff")); }
            co.P(p.Item1, p.Item2, H("#ffffff"));
        }
        var so = new Cv(img, SilverOre * 16, 0);
        so.Fill(H("#2e3548")); so.Speckle(r, 40, H("#3a4258"), opaqueOnly: false); so.R(0, 0, 16, 1, H("#4a5470"));
        foreach (var p in new[] { (3, 4), (8, 3), (11, 7), (5, 10), (9, 11), (13, 12), (2, 12) })
        { so.R(p.Item1, p.Item2, 3, 2, H("#e8eef6")); so.P(p.Item1, p.Item2, H("#ffffff")); so.P(p.Item1 + 2, p.Item2 + 1, H("#9aa6bf")); }

        // ── заброшенный механизм ──
        for (int on = 0; on < 2; on++)
        {
            var m = new Cv(img, (on == 0 ? Machine : MachineOn) * 16, 0);
            m.R(2, 7, 12, 8, H("#5a5f6e")); m.R(2, 7, 12, 1, H("#7a8094")); m.R(10, 2, 2, 6, H("#4a4f5e")); m.R(9, 1, 4, 1, H("#7a8094"));
            m.E(7, 11, 3.2f, 3.2f, H("#8a8f9c")); m.E(7, 11, 1.4f, 1.4f, on == 0 ? H("#1a1e2b") : H("#8ff0ff"));
            for (int i = 0; i < 4; i++) { m.P(7 + (int)(Math.Cos(i * 1.57) * 4), 11 + (int)(Math.Sin(i * 1.57) * 4), H("#8a8f9c")); }
            if (on == 0) { m.Speckle(r, 10, H("#8a4a2a"), 2, 7, 14, 15); m.P(5, 9, Clear); m.P(6, 8, Clear); m.P(12, 12, Clear); }
            else { m.P(11, 0, H("#cfd6e2")); m.P(12, 1, H("#cfd6e2")); m.R(3, 8, 2, 1, H("#8ff0ff")); }
            m.Outline(H("#14161f"));
        }
        // ── зеркала ──
        for (int k = 0; k < 2; k++)
        {
            var m = new Cv(img, (k == 0 ? Mirror1 : Mirror2) * 16, 0);
            m.R(3, 13, 10, 2, H("#4a5470")); m.R(5, 12, 6, 1, H("#5f6f94"));
            for (int i = 0; i < 11; i++)
            {
                int x = k == 0 ? 2 + i : 13 - i, y = 12 - i;
                m.P(x, y, H("#bfe9ff")); m.P(x + 1, y, H("#e8fbff")); m.P(x - 1, y, H("#7ac8ff")); m.P(x, y + 1, H("#8ab8ff"));
            }
            m.Outline(H("#10162b"));
        }
        // ── приёмник ──
        for (int on = 0; on < 2; on++)
        {
            var m = new Cv(img, (on == 0 ? Receiver : ReceiverOn) * 16, 0);
            m.R(3, 5, 10, 10, H("#3a3f4d")); m.R(3, 5, 10, 1, H("#5a6072")); m.E(8, 10, 3.6f, 3.6f, H("#1a1e2b"));
            if (on == 1) { m.E(8, 10, 2.6f, 2.6f, H("#ffe27a")); m.E(8, 10, 1.2f, 1.2f, H("#ffffff")); m.P(8, 3, H("#ffe27a")); m.P(2, 10, H("#ffe27a")); m.P(13, 10, H("#ffe27a")); }
            else { m.E(8, 10, 1.2f, 1.2f, H("#4a5060")); }
            m.Outline(H("#0e1018"));
        }
        // ── излучатель ──
        var em = new Cv(img, Emitter * 16, 0);
        em.R(3, 6, 10, 9, H("#4a5470")); em.R(3, 6, 10, 1, H("#7a88b0")); em.E(8, 10, 3.2f, 3.2f, H("#ffe27a")); em.E(8, 10, 1.6f, 1.6f, H("#ffffff"));
        em.Outline(H("#0e1018"));
        // ── фонари: кристальный и боевой ──
        var cl = new Cv(img, CrystalLantern * 16, 0);
        cl.R(7, 9, 2, 6, H("#4a5470")); cl.R(4, 14, 8, 1, H("#3a4258"));
        for (int y = 1; y < 10; y++) { int hw = y < 5 ? 1 + y / 2 : 3 - (y - 5) / 2; cl.R(8 - hw, y, hw * 2, 1, y % 2 == 0 ? H("#8ab8ff") : H("#bfe9ff")); }
        cl.R(7, 3, 1, 4, H("#ffffff")); cl.Outline(H("#0e1430"));
        var bl = new Cv(img, CombatLantern * 16, 0);
        bl.R(7, 8, 2, 7, H("#5a2a2a")); bl.R(5, 14, 6, 1, H("#3a1a1a"));
        bl.R(4, 2, 8, 6, H("#ff7a3a")); bl.R(6, 3, 4, 4, H("#ffe27a")); bl.R(4, 1, 8, 1, H("#9aa0ae")); bl.R(4, 8, 8, 1, H("#9aa0ae"));
        bl.P(3, 3, H("#cfd6e2")); bl.P(12, 3, H("#cfd6e2")); bl.P(3, 6, H("#cfd6e2")); bl.P(12, 6, H("#cfd6e2")); bl.P(8, 0, H("#cfd6e2"));
        bl.Outline(H("#1a0a0a"));
        // ── завал ──
        var ru = new Cv(img, Rubble * 16, 0);
        ru.E(5, 11.5f, 3.2f, 2.6f, H("#7a7f8c")); ru.E(10.5f, 11, 3.8f, 3.2f, H("#6a6f7c")); ru.E(8, 7.5f, 3.2f, 2.6f, H("#8a8f9c"));
        ru.P(4, 10, H("#a0a6b4")); ru.P(9, 6, H("#a0a6b4")); ru.P(11, 10, H("#4a4f5c")); ru.Outline(H("#1a1c24"));
        // ── мосты ──
        var bb = new Cv(img, BridgeBroken * 16, 0);
        bb.R(0, 4, 16, 1, H("#6b4423")); bb.R(0, 11, 16, 1, H("#6b4423"));
        foreach (int x in new[] { 0, 1, 2, 5, 6, 9, 13, 14, 15 }) bb.R(x, 5, 1, 6, x % 2 == 0 ? H("#8a5a2e") : H("#a8743d"));
        bb.P(7, 5, H("#6b4423")); bb.P(11, 10, H("#6b4423")); bb.Outline(H("#1a1008"));
        var br = new Cv(img, Bridge * 16, 0);
        br.R(0, 3, 16, 10, H("#a8743d")); br.R(0, 3, 16, 1, H("#6b4423")); br.R(0, 12, 16, 1, H("#6b4423"));
        for (int x = 2; x < 16; x += 4) br.R(x, 4, 1, 8, H("#8a5a2e"));
        br.R(0, 3, 16, 1, H("#4a3320")); br.R(0, 12, 16, 1, H("#4a3320"));
        // ── глубокая вода ──
        var dp = new Cv(img, Deep * 16, 0);
        dp.Fill(H("#17365a")); dp.Speckle(r, 40, H("#245a8a"), opaqueOnly: false); dp.Speckle(r, 30, H("#102845"), opaqueOnly: false);
        dp.R(2, 5, 5, 1, H("#3d79ab")); dp.R(8, 11, 5, 1, H("#3d79ab"));
        // ── печать и тайная дверь ──
        var se = new Cv(img, Seal * 16, 0);
        se.Fill(H("#241a40")); se.R(0, 0, 16, 1, H("#d58cff")); se.R(0, 15, 16, 1, H("#d58cff")); se.R(0, 0, 1, 16, H("#8a4ac0")); se.R(15, 0, 1, 16, H("#8a4ac0"));
        foreach (var p in new[] { (4, 3), (9, 5), (6, 8), (11, 11), (3, 12), (8, 2) }) { se.R(p.Item1, p.Item2, 3, 1, H("#d58cff")); se.P(p.Item1, p.Item2 + 1, H("#f0c8ff")); }
        var sd = new Cv(img, SecretDoor * 16, 0);
        sd.Fill(H("#4a5060")); sd.R(0, 0, 16, 1, H("#7a8094")); sd.R(7, 0, 2, 16, H("#3a3f4d")); sd.R(0, 7, 16, 2, H("#3a3f4d"));
        sd.R(6, 6, 4, 4, H("#1a1e2b")); sd.P(7, 7, H("#8ff0ff")); sd.P(8, 8, H("#8ff0ff"));
        // ── бур и коллектор по уровням ──
        for (int lv = 1; lv <= 4; lv++)
        {
            var d = new Cv(img, (Drill + lv - 1) * 16, 0);
            d.R(2, 10, 12, 5, H("#5a5f6e")); d.R(2, 10, 12, 1, H("#7a8094")); d.R(6, 3, 4, 7, H("#8a8f9c"));
            for (int y = 3; y < 10; y++) d.P(y % 2 == 0 ? 6 : 9, y, H("#cfd6e2"));
            d.R(7, 0, 2, 3, H("#cfd6e2")); d.P(8, 0, H("#ffffff"));
            if (lv >= 2) { d.R(10, 4, 3, 6, H("#4a4f5e")); d.R(10, 4, 3, 1, H("#7a8094")); }
            if (lv >= 3) { d.P(4, 8, H("#8ff0ff")); d.P(12, 12, H("#8ff0ff")); d.R(3, 12, 2, 2, H("#bfe9ff")); }
            if (lv >= 4) { d.R(2, 14, 12, 1, H("#ffd86b")); d.P(8, 1, H("#ffd86b")); }
            d.Outline(H("#14161f"));
            var k = new Cv(img, (Collector + lv - 1) * 16, 0);
            for (int y = 4; y < 15; y++) { int hw = 6 - (y - 4) / 3; k.R(8 - hw, y, hw * 2, 1, y % 3 == 0 ? H("#4a3a34") : H("#5a4a44")); }
            k.R(1, 3, 14, 2, H("#3a2c28")); k.R(3, 4, 10, 1, H("#ff7a2a"));
            if (lv >= 2) { k.R(7, 8, 2, 4, H("#ff9a2e")); }
            if (lv >= 3) { k.P(5, 10, H("#ffb04a")); k.P(10, 7, H("#ffb04a")); k.R(7, 1, 2, 2, H("#ffe27a")); }
            if (lv >= 4) { k.R(1, 3, 14, 1, H("#c9a24a")); k.P(8, 0, H("#fffbe0")); }
            k.Outline(H("#14090a"));
        }
        // ── здания 4 уровня ──
        int[] baseIdx = { Warehouse, Sawmill, Mine, Forge }; int[] l4 = { Wh4, Saw4, Mine4, Forge4 };
        for (int k = 0; k < 4; k++) DrawBuilding(new Cv(img, l4[k] * 16, 0), k, 4, r);
        ExtraTiles3(img, r);
        ExtraTiles4(img, r);

        // ── пустоши: объекты ──
        var dt = new Cv(img, DeadTree * 16, 0);
        dt.R(7, 5, 2, 10, H("#2a1f1a")); dt.R(7, 5, 1, 10, H("#3a2c26"));
        dt.R(4, 4, 4, 1, H("#2a1f1a")); dt.R(3, 2, 1, 3, H("#2a1f1a")); dt.R(9, 3, 4, 1, H("#2a1f1a")); dt.R(12, 1, 1, 3, H("#2a1f1a")); dt.R(8, 1, 1, 4, H("#2a1f1a"));
        dt.P(5, 8, H("#2a1f1a")); dt.P(10, 7, H("#2a1f1a")); dt.Outline(H("#0d0807"));
        var bo = new Cv(img, Boulder * 16, 0); DrawRock(bo, H("#4a4542"), H("#6a6460"), H("#2a2624")); bo.Outline(H("#0f0c0b"));
        var ob = new Cv(img, Obsidian * 16, 0);
        DrawRock(ob, H("#1a1624"), H("#3a3050"), H("#0e0b16"));
        foreach (var p in new[] { (5, 7), (9, 8), (7, 11), (11, 11) }) { ob.R(p.Item1, p.Item2, 2, 3, H("#5a4a8a")); ob.P(p.Item1, p.Item2, H("#c0a8ff")); }
        ob.Outline(H("#08060c"));
        var emb = new Cv(img, Ember * 16, 0);
        DrawRock(emb, H("#3a2a26"), H("#5a4038"), H("#1f1512"));
        foreach (var p in new[] { (4, 7), (8, 6), (6, 10), (10, 9), (12, 11) }) { emb.R(p.Item1, p.Item2, 3, 1, H("#ff7a2a")); emb.P(p.Item1 + 1, p.Item2 + 1, H("#ffd04a")); emb.P(p.Item1, p.Item2 - 1, H("#ff9a2e")); }
        emb.Outline(H("#140a08"));
        var ar = new Cv(img, AshRuin * 16, 0);
        ar.R(2, 7, 12, 8, H("#4a4542")); ar.R(2, 7, 12, 1, H("#6a6460")); ar.R(3, 4, 3, 3, H("#4a4542")); ar.R(10, 5, 3, 2, H("#4a4542"));
        ar.R(5, 10, 2, 3, H("#1a1514")); ar.R(9, 9, 2, 2, H("#1a1514")); ar.Speckle(r, 12, H("#2a2624"), 2, 4, 14, 15); ar.P(7, 3, H("#ff7a2a"));
        ar.Outline(H("#0f0c0b"));
        var vt = new Cv(img, Vent * 16, 0);
        vt.E(8, 10, 6.5f, 4.5f, H("#3a3330")); vt.E(8, 10, 4.5f, 2.8f, H("#100c0a")); vt.E(8, 10, 2.2f, 1.2f, H("#7a2a10"));
        vt.P(7, 10, H("#ff7a2a")); vt.P(9, 9, H("#ffb04a")); vt.Outline(H("#0f0c0b"));
    }

    static void ExtraEntities2()
    {
        // угорь
        var img = NewImg(32);
        for (int f = 0; f < 2; f++)
        {
            var c = new Cv(img, f * 16, 0);
            for (int x = 1; x < 15; x++) { int y = 9 + (int)(Math.Sin(x * .9 + f * 1.6) * 2); c.R(x, y, 1, 3, H("#2a6a7a")); c.P(x, y, H("#4aa0b0")); }
            c.E(12.5f, 8, 3, 2.6f, H("#2a6a7a")); c.R(13, 9, 3, 1, H("#0a2a30")); c.P(14, 9, H("#ffffff")); c.R(11, 7, 2, 1, H("#ffe27a")); c.P(12, 7, H("#ff4a4a"));
            c.P(2, 8 + f, H("#2a6a7a")); c.Outline(H("#08181c"));
        }
        EelTex = ImageTexture.CreateFromImage(img);
        // светоед
        img = NewImg(32);
        for (int f = 0; f < 2; f++)
        {
            var c = new Cv(img, f * 16, 0);
            c.E(8, 9, 5.5f, 4f, H("#201a30")); c.E(8, 8, 4f, 2.8f, H("#2e2646"));
            c.R(5, 8, 2, 2, H("#8ff0ff")); c.R(9, 8, 2, 2, H("#8ff0ff")); c.R(6, 11, 4, 1, H("#ffffff")); c.P(6, 12, H("#ffffff")); c.P(9, 12, H("#ffffff"));
            c.P(2, 6 - f, H("#201a30")); c.P(13, 6 - f, H("#201a30")); c.P(3, 13, H("#201a30")); c.P(12, 13 - f, H("#201a30"));
            c.Outline(H("#0a0812"));
        }
        LeechTex = ImageTexture.CreateFromImage(img);
        // кристальный жук
        img = NewImg(32);
        for (int f = 0; f < 2; f++)
        {
            var c = new Cv(img, f * 16, 0);
            c.E(8, 9, 6.5f, 5f, H("#3a5a78")); c.E(8, 8, 5.2f, 3.8f, H("#5a8ab0"));
            c.R(7, 4, 2, 9, H("#2a4660")); foreach (var p in new[] { (4, 6), (11, 6), (5, 10), (10, 10) }) { c.P(p.Item1, p.Item2, H("#e4f4ff")); c.P(p.Item1 + 1, p.Item2 - 1, H("#bfe9ff")); }
            c.R(12, 8, 3, 2, H("#2a4660")); c.P(14, 8, H("#ffe27a")); c.P(14, 10, H("#ffe27a"));
            c.R(3, 14 + f, 2, 1, H("#2a4660")); c.R(8, 14 + (1 - f), 2, 1, H("#2a4660")); c.R(12, 14 + f, 2, 1, H("#2a4660"));
            c.Outline(H("#0a1620"));
        }
        BeetleTex = ImageTexture.CreateFromImage(img);
        // хранитель глубин
        img = NewImg(32);
        for (int f = 0; f < 2; f++)
        {
            var c = new Cv(img, f * 16, 0);
            c.E(8, 8, 6.5f, 6f, H("#143a58")); c.E(8, 7, 5f, 4.5f, H("#1f5a80"));
            c.E(8, 7, 2.6f, 2.6f, H("#f0fcff")); c.E(8, 7, 1.2f, 1.6f, H("#0a1a2a")); c.P(7, 6, H("#ffffff"));
            for (int i = 0; i < 5; i++) { int x = 2 + i * 3; c.R(x, 12, 2, 3 + ((i + f) % 2), H("#143a58")); c.P(x, 14 + ((i + f) % 2), H("#8fd0ff")); }
            c.Speckle(new Random(31 + f), 10, H("#3d79ab"), 3, 3, 13, 11);
            c.Outline(H("#06121e"));
        }
        DeepTex = ImageTexture.CreateFromImage(img);
        // бес
        img = NewImg(32);
        for (int f = 0; f < 2; f++)
        {
            var c = new Cv(img, f * 16, 0);
            c.E(8, 9, 4.5f, 5f, H("#8a2a10")); c.E(8, 9, 3f, 3.6f, H("#ff7a2a")); c.E(8, 10, 1.6f, 2f, H("#ffd04a"));
            c.P(5, 3, H("#ff7a2a")); c.P(5, 4, H("#8a2a10")); c.P(11, 3, H("#ff7a2a")); c.P(11, 4, H("#8a2a10"));
            c.R(5, 7, 2, 1, H("#ffffff")); c.R(9, 7, 2, 1, H("#ffffff")); c.P(6, 7, H("#1a0a0a")); c.P(10, 7, H("#1a0a0a"));
            c.P(8, 1 - f, H("#ffb04a")); c.P(7, 2, H("#ff9a2e")); c.P(9, 2, H("#ff9a2e"));
            c.R(4, 14 + f, 2, 1, H("#8a2a10")); c.R(10, 14 + (1 - f), 2, 1, H("#8a2a10"));
            c.Outline(H("#1a0805"));
        }
        ImpTex = ImageTexture.CreateFromImage(img);
        // голем
        img = NewImg(32);
        for (int f = 0; f < 2; f++)
        {
            var c = new Cv(img, f * 16, 0);
            c.R(3, 6, 10, 8, H("#5a5450")); c.R(4, 2, 8, 5, H("#6a6460")); c.R(1, 7 + f, 3, 6, H("#4a4542")); c.R(12, 7 + (1 - f), 3, 6, H("#4a4542"));
            c.Speckle(new Random(61 + f), 14, H("#3a3532"), 2, 3, 14, 14); c.Speckle(new Random(71 + f), 8, H("#8a8680"), 3, 3, 13, 12);
            c.R(5, 4, 2, 2, H("#ff7a2a")); c.R(9, 4, 2, 2, H("#ff7a2a")); c.R(6, 9, 4, 1, H("#ff7a2a")); c.P(8, 10, H("#ff7a2a"));
            c.R(3, 14, 3, 1 + f, H("#4a4542")); c.R(10, 14, 3, 1 + (1 - f), H("#4a4542"));
            c.Outline(H("#100c0a"));
        }
        GolemTex = ImageTexture.CreateFromImage(img);
        // дух пепла
        img = NewImg(32);
        for (int f = 0; f < 2; f++)
        {
            var c = new Cv(img, f * 16, 0);
            c.E(8, 6, 4f, 4.5f, H("#8a8680")); c.E(8, 6, 2.8f, 3.2f, H("#b5b0a8"));
            for (int y = 9; y < 15; y++) { int hw = 4 - (y - 9) / 2; c.R(8 - hw + ((y + f) % 2), y, hw * 2, 1, H("#6a6460")); }
            c.R(6, 5, 1, 2, H("#ff7a2a")); c.R(9, 5, 1, 2, H("#ff7a2a")); c.R(7, 8, 2, 1, H("#1a1514"));
            c.P(8, 1 - f, H("#ff9a2e")); c.P(7, 2, H("#ff7a2a"));
            c.Outline(H("#14110f"));
        }
        SpiritTex = ImageTexture.CreateFromImage(img);
        // пепельный страж
        img = NewImg(32);
        for (int f = 0; f < 2; f++)
        {
            var c = new Cv(img, f * 16, 0);
            c.R(2, 5, 12, 9, H("#4a4542")); c.R(3, 2, 10, 4, H("#5a5450")); c.R(0, 6 + f, 3, 7, H("#3a3532")); c.R(13, 6 + (1 - f), 3, 7, H("#3a3532"));
            c.R(3, 0, 2, 3, H("#ff7a2a")); c.R(11, 0, 2, 3, H("#ff7a2a")); c.R(7, 0, 2, 2, H("#ffb04a"));
            c.R(4, 4, 3, 2, H("#ffd04a")); c.R(9, 4, 3, 2, H("#ffd04a")); c.R(5, 9, 6, 2, H("#ff7a2a")); c.R(6, 9, 4, 1, H("#ffd04a"));
            c.Speckle(new Random(81 + f), 18, H("#2a2624"), 3, 6, 13, 14);
            c.R(3, 14, 3, 1 + f, H("#3a3532")); c.R(10, 14, 3, 1 + (1 - f), H("#3a3532"));
            c.Outline(H("#0f0a08"));
        }
        AshBossTex = ImageTexture.CreateFromImage(img);
    }

    static void ExtraIcons2(Image img)
    {
        // 9 серебряная руда 10 серебро 11 кристалл 12 обсидиан 13 уголь 14 ядро света
        var c = new Cv(img, 9 * 16, 0); DrawRock(c, H("#5a6072"), H("#8a92a8"), H("#3a4054"));
        foreach (var p in new[] { (5, 8), (9, 7), (8, 11), (11, 10) }) { c.R(p.Item1, p.Item2, 3, 2, H("#e8eef6")); c.P(p.Item1, p.Item2, H("#ffffff")); }
        c.Outline(H("#1c1e2c"));
        c = new Cv(img, 10 * 16, 0);
        c.R(3, 6, 10, 5, H("#e8eef6")); c.R(4, 5, 8, 1, H("#ffffff")); c.R(3, 11, 10, 1, H("#9aa6bf")); c.R(5, 7, 4, 1, H("#ffffff")); c.P(11, 8, H("#bfe9ff"));
        c.Outline(H("#1c2438"));
        c = new Cv(img, 11 * 16, 0);
        for (int y = 1; y < 15; y++) { int w = y < 7 ? 1 + y / 2 : 4 - (y - 7) / 2; c.R(8 - w, y, w * 2, 1, y % 2 == 0 ? H("#bfe9ff") : H("#8ab8ff")); }
        c.R(7, 3, 1, 7, H("#ffffff")); c.Outline(H("#10305a"));
        c = new Cv(img, 12 * 16, 0);
        for (int y = 2; y < 14; y++) { int w = y < 8 ? 1 + (y - 2) / 2 : 3 - (y - 8) / 2; c.R(8 - w, y, w * 2, 1, H("#1a1624")); }
        c.R(6, 4, 1, 6, H("#5a4a8a")); c.P(7, 6, H("#c0a8ff")); c.Outline(H("#0a0810"));
        c = new Cv(img, 13 * 16, 0);
        c.E(8, 10, 5.5f, 4.5f, H("#3a2a26")); c.E(8, 10, 3.5f, 2.8f, H("#ff7a2a")); c.E(8, 10, 1.8f, 1.4f, H("#ffd04a"));
        c.R(7, 2, 2, 3, H("#ff9a2e")); c.P(8, 1, H("#ffd04a")); c.Outline(H("#140a08"));
        c = new Cv(img, 14 * 16, 0);
        c.E(8, 8, 6, 6, H("#c9a24a")); c.E(8, 8, 4.2f, 4.2f, H("#fff2b0")); c.E(8, 8, 2.2f, 2.2f, H("#ffffff"));
        c.R(7, 0, 2, 2, H("#ffe27a")); c.R(7, 14, 2, 2, H("#ffe27a")); c.R(0, 7, 2, 2, H("#ffe27a")); c.R(14, 7, 2, 2, H("#ffe27a"));
        c.Outline(H("#3a2a08"));
        // 15 древний кристалл 16 монеты 17 шестерня 18 линза
        c = new Cv(img, 15 * 16, 0);
        for (int y = 0; y < 16; y++) { int w = y < 8 ? 1 + y : 16 - y; c.R(8 - w / 2 - 1, y, w + 1, 1, y % 3 == 0 ? H("#ffd0ff") : H("#e07aff")); }
        c.R(7, 3, 1, 8, H("#ffffff")); c.P(10, 6, H("#ffffff")); c.Outline(H("#3a0c4a"));
        c = new Cv(img, 16 * 16, 0);
        c.E(8, 11, 5.5f, 2.6f, H("#c9a24a")); c.E(8, 9.5f, 5.5f, 2.6f, H("#ffd86b")); c.E(8, 7.5f, 5.5f, 2.6f, H("#c9a24a")); c.E(8, 6, 5.5f, 2.6f, H("#ffe27a"));
        c.R(7, 5, 2, 2, H("#fff2b0")); c.Outline(H("#3a2a08"));
        c = new Cv(img, 17 * 16, 0);
        c.E(8, 8, 6, 6, H("#9aa0ae")); c.E(8, 8, 2.6f, 2.6f, Clear);
        foreach (var p in new[] { (7, 0), (7, 14), (0, 7), (14, 7), (2, 2), (12, 2), (2, 12), (12, 12) }) c.R(p.Item1, p.Item2, 2, 2, H("#b8bfce"));
        c.R(6, 6, 4, 4, H("#6a7080")); c.Outline(H("#1c2030"));
        c = new Cv(img, 18 * 16, 0);
        c.E(8, 8, 6, 6, H("#5aa8d0")); c.E(8, 8, 4.5f, 4.5f, H("#9fe8ff")); c.E(8, 8, 2.2f, 2.2f, H("#e8ffff")); c.P(5, 5, H("#ffffff")); c.P(6, 5, H("#ffffff"));
        c.R(7, 14, 2, 2, H("#3a4a6a")); c.Outline(H("#10203a"));
    }
}
