using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Проверки деревьев и кустов: крона в клетке над стволом, коллизии, ствол над игроком за деревом, время рубки, кусты, соседние деревья и граница чанков (LAMP_STAGE=trees).</summary>
public partial class Game
{
    void BuildTreesScript()
    {
        int fails = 0; Vector2I tree = default; Vector2I bush = new(-1, -1);
        void Check(bool ok, string what) { if (!ok) fails++; GD.Print($"[check] {(ok ? "PASS" : "FAIL")} {what}"); }
        int CrownAt(int x, int y) { var ch = ChunkAt(x, y); if (ch == null) return -2; var lc = new Vector2I(x % CS, y % CS); if (ch.B.GetCellSourceId(lc) >= 0) return ch.B.GetCellAtlasCoords(lc).X; return ch.T.GetCellSourceId(lc) < 0 ? -1 : ch.T.GetCellAtlasCoords(lc).X; }
        char CrownLayer(int x, int y) { var ch = ChunkAt(x, y); if (ch == null) return '?'; var lc = new Vector2I(x % CS, y % CS); return ch.B.GetCellSourceId(lc) >= 0 ? 'B' : ch.T.GetCellSourceId(lc) >= 0 ? 'T' : '-'; }
        bool TrunkCopyAt(int x, int y) { var ch = ChunkAt(x, y); return ch != null && ch.K.GetCellSourceId(new Vector2I(x % CS, y % CS)) >= 0; }
        float TimeToBreak(Vector2I t, string tool)
        {
            OwnedTools.Add(tool); CurTool = tool; EquipNone(); PPos = TileCenter(t.X, t.Y - 1); Aim = TileCenter(t.X, t.Y); AtkCd = 0;
            float sec = 0; while (W.Get(t.X, t.Y) == Tl.Tree && sec < 30f) { ActPick(.016f, true); sec += .016f; }
            return sec;
        }
        S(.3f, () =>
        {
            Menu = Mn.None; noHeal = true; Hp = MaxHp; Invuln = 99f; Enemies.Clear(); Hot[0] = "tool:pickaxe";
            bool found = false;
            for (int r = 3; r < 70 && !found; r++)
                for (int dy = -r; dy <= r && !found; dy++)
                    for (int dx = -r; dx <= r && !found; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                        int x = W.Spawn.X + dx, y = W.Spawn.Y + dy; if (!World.In(x - 2, y - 4) || !World.In(x + 2, y + 4) || y % CS < 4 || y % CS > CS - 5) continue;
                        if (W.Get(x, y) != Tl.Tree) continue;
                        bool clear = true; for (int ky = -4; ky <= 4; ky++) for (int kx = -2; kx <= 2; kx++) if ((kx != 0 || ky != 0) && (W.Get(x + kx, y + ky) != Tl.None || W.Ground[World.Key(x + kx, y + ky)] == Art.WaterG)) clear = false;
                        if (clear) { tree = new Vector2I(x, y); found = true; }
                    }
            Check(found, "trees: an isolated tree with free space around it exists near spawn");
            if (found) Tp(TileCenter(tree.X, tree.Y - 3));
        });
        S(.5f, () =>
        {
            Check(CrownAt(tree.X, tree.Y - 1) == Art.TreeTop, "trees: the crown tile sits in the cell above the trunk");
            Check(!World.Blocks(W.Get(tree.X, tree.Y - 1)), "trees: the crown cell has no collider");
            Check(World.Blocks(W.Get(tree.X, tree.Y)), "trees: the trunk cell is solid");
            PPos = TileCenter(tree.X, tree.Y - 3); for (int i = 0; i < 80; i++) PPos = Move(PPos, new Vector2(0, 2), 5f, false);
            Check(PPos.Y > (tree.Y - 1) * TS && PPos.Y < tree.Y * TS, $"trees: the player walks into the crown cell and is stopped by the trunk (y={PPos.Y:0} trunk top={tree.Y * TS})");
            UpdateTreeOcclusion(); Check(TrunkCopyAt(tree.X, tree.Y), "trees: standing behind the tree puts a trunk copy above the player");
            Check(CrownLayer(tree.X, tree.Y - 1) == 'T', "trees: standing behind the tree puts the crown above the player");
            PPos = TileCenter(tree.X, tree.Y + 2); UpdateTreeOcclusion(); Check(!TrunkCopyAt(tree.X, tree.Y), "trees: standing in front of the tree removes the trunk copy");
            Check(CrownLayer(tree.X, tree.Y - 1) == 'B', "trees: standing in front keeps the crown under the player (the tool is not covered)");
            PPos = TileCenter(tree.X, tree.Y + 1) + new Vector2(5, -2); UpdateTreeOcclusion(); Check(CrownLayer(tree.X, tree.Y - 1) == 'B' && !TrunkCopyAt(tree.X, tree.Y), "trees: standing slightly below the trunk, off to the side, is still in front");
            PPos = TileCenter(tree.X - 1, tree.Y + 1); UpdateTreeOcclusion(); Check(CrownLayer(tree.X, tree.Y - 1) == 'B', "trees: standing diagonally below is in front");
            PPos = TileCenter(tree.X + 1, tree.Y); UpdateTreeOcclusion(); Check(!TrunkCopyAt(tree.X, tree.Y), "trees: standing beside the trunk does not cover the player");
            PPos = TileCenter(tree.X - 1, tree.Y - 2); UpdateTreeOcclusion(); Check(TrunkCopyAt(tree.X, tree.Y), "trees: standing behind and to the side still shows the trunk copy");
            // рубка: клик по кроне бьёт ствол; кирка заметно медленнее топора
            int wood0 = Inv[It.Wood]; Check(true, "trees: setup chopping");
            float tp = TimeToBreak(tree, "pickaxe");
            Check(W.Get(tree.X, tree.Y) == Tl.None && CrownAt(tree.X, tree.Y - 1) == -1, "trees: a chopped tree loses its trunk and crown");
            Check(Inv[It.Wood] - wood0 >= 2 && Inv[It.Wood] - wood0 <= 3, $"trees: a tree gives 2-3 wood (got {Inv[It.Wood] - wood0})");
            SetObj(tree.X, tree.Y, Tl.Tree); Check(CrownAt(tree.X, tree.Y - 1) == Art.TreeTop, "trees: a regrown tree gets its crown back");
            float ta = TimeToBreak(tree, "axe");
            GD.Print($"[trees] chop time pickaxe={tp:0.00}s axe={ta:0.00}s");
            Check(tp >= 2.5f && tp <= 6f, $"trees: the pickaxe needs a few seconds per tree ({tp:0.0}s)");
            Check(ta >= .8f && ta < tp * .6f, $"trees: the axe is much faster than the pickaxe ({ta:0.0}s)");
            SetObj(tree.X, tree.Y, Tl.Tree);
            // клик по кроне рубит ствол под ней
            OwnedTools.Add("pickaxe"); CurTool = "pickaxe"; EquipNone(); PPos = TileCenter(tree.X, tree.Y - 2); Aim = TileCenter(tree.X, tree.Y - 1); MineProg = 0; AtkCd = 0;
            for (int i = 0; i < 20; i++) ActPick(.016f, true);
            Check(MineTile == tree, "trees: aiming at the crown mines the trunk below it");
            // соседние деревья: вертикально, рядом и на границе чанков
            SetObj(tree.X, tree.Y - 1, Tl.Tree);
            Check(CrownAt(tree.X, tree.Y - 1) == Art.TreeTop && CrownAt(tree.X, tree.Y - 2) == Art.TreeTop, "trees: two trees in a column both keep their crowns");
            SetObj(tree.X, tree.Y - 1, Tl.None);
            Check(CrownAt(tree.X, tree.Y - 1) == Art.TreeTop && CrownAt(tree.X, tree.Y - 2) == -1, "trees: removing the upper tree removes only its own crown");
            SetObj(tree.X + 1, tree.Y, Tl.Pine); Check(CrownAt(tree.X + 1, tree.Y - 1) == Art.PineTop && CrownAt(tree.X, tree.Y - 1) == Art.TreeTop, "trees: neighbouring trees keep separate crowns");
            SetObj(tree.X + 1, tree.Y, Tl.None);
            SetObj(tree.X, tree.Y + 1, Tl.Stone); Check(CrownAt(tree.X, tree.Y) == -1, "trees: a stone below a tree does not get a crown");
            SetObj(tree.X, tree.Y + 1, Tl.None);
            int yb = (tree.Y / CS + 1) * CS; var prev = W.Get(tree.X, yb);
            if (prev == Tl.None) { SetObj(tree.X, yb, Tl.Tree); Check(CrownAt(tree.X, yb - 1) == Art.TreeTop, "trees: the crown of a tree on a chunk border is drawn in the neighbouring chunk"); SetObj(tree.X, yb, Tl.None); Check(CrownAt(tree.X, yb - 1) == -1, "trees: and removed with it"); }
            // кусты: ломаются с одного удара и дают 1 дерево
            for (int r = 2; r < 40 && bush.X < 0; r++) for (int dy = -r; dy <= r && bush.X < 0; dy++) for (int dx = -r; dx <= r && bush.X < 0; dx++)
                { if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue; int x = tree.X + dx, y = tree.Y + dy; if (World.In(x, y) && ChunkAt(x, y) != null && IsBushAt(x, y)) bush = new Vector2I(x, y); }
            Check(bush.X >= 0, "trees: bushes exist in the forest");
        });
        S(.3f, () =>
        {
            if (bush.X >= 0)
            {
                Tp(TileCenter(bush.X, bush.Y - 1)); EquipNone(); CurTool = "pickaxe"; Aim = TileCenter(bush.X, bush.Y); int wd = Inv[It.Wood]; float sec = 0; AtkCd = 0;
                while (IsBushAt(bush.X, bush.Y) && sec < 5f) { ActPick(.016f, true); sec += .016f; }
                Check(!IsBushAt(bush.X, bush.Y) && Inv[It.Wood] == wd + 1 && CutBush.Contains(World.Key(bush.X, bush.Y)), $"trees: a bush breaks after {sec:0.0}s and gives exactly 1 wood");
                Check(sec < 1.2f, "trees: a bush breaks quickly");
            }
            // страж восстанавливает здоровье, когда игрок далеко от его дома, и не лечится рядом
            Enemies.Clear(); Tp(TileCenter(tree.X, tree.Y - 1)); var bossE = SpawnEnemy(PPos + new Vector2(40 * TS, 0), EK.Warden, null, 1f); bossE.Hp = bossE.MaxHp * .4f; float hp0 = bossE.Hp;
            for (int i = 0; i < 20; i++) UpdateEnemies(.5f);
            Check(bossE.Hp > hp0 + bossE.MaxHp * .3f, $"boss: heals while the player is far away ({hp0:0}→{bossE.Hp:0} of {bossE.MaxHp:0})");
            bossE.Hp = bossE.MaxHp * .4f; hp0 = bossE.Hp; bossE.Pos = PPos + new Vector2(80, 0); bossE.Home = PPos; bossE.Cd = 99; bossE.T1 = 99; bossE.T3 = 99;
            for (int i = 0; i < 6; i++) UpdateEnemies(.1f);
            Check(bossE.Hp <= hp0 + .01f, "boss: does not heal while the player is near");
            Enemies.Clear(); Boss = null;
            Tp(TileCenter(tree.X, tree.Y - 1)); SetObj(tree.X, tree.Y, Tl.Tree);
            GD.Print($"[check] SUMMARY failures={fails}");
        });
        // снимки: игрок за деревом с замахом и перед деревом
        S(.2f, () => { Invuln = 0; OwnedTools.Add("axe"); CurTool = "axe"; EquipNone(); Tp(TileCenter(tree.X, tree.Y - 1)); Aim = TileCenter(tree.X, tree.Y); Swing = .2f; MineTile = tree; MineProg = .5f; UpdateTreeOcclusion(); });
        S(.3f, () => { Swing = .2f; Shot("trees_behind"); });
        S(.15f, () => { Tp(TileCenter(tree.X, tree.Y + 1)); Aim = TileCenter(tree.X, tree.Y); Swing = .2f; UpdateTreeOcclusion(); });
        S(.3f, () => { Swing = .2f; Shot("trees_front"); });
        S(.15f, () => { Tp(TileCenter(tree.X - 1, tree.Y - 1) + new Vector2(6, 4)); Aim = TileCenter(tree.X, tree.Y); Swing = .2f; UpdateTreeOcclusion(); });
        S(.3f, () => { Swing = .2f; Shot("trees_side"); });
        S(.15f, () => { Tp(TileCenter(tree.X, tree.Y) + new Vector2(0, -12)); Aim = TileCenter(tree.X, tree.Y); Swing = .2f; Invuln = 0; UpdateTreeOcclusion(); });
        S(.3f, () => { Swing = .2f; Shot("trees_overlap"); });
        S(.15f, () => { Tp(TileCenter(tree.X, tree.Y + 1) + new Vector2(4, 2)); Aim = TileCenter(tree.X, tree.Y - 1); Swing = .2f; Invuln = 0; UpdateTreeOcclusion(); });
        S(.3f, () => { Swing = .2f; Shot("trees_below_up"); });
        S(.15f, () => { Tp(TileCenter(tree.X + 1, tree.Y) + new Vector2(0, 4)); Aim = TileCenter(tree.X, tree.Y - 1); Swing = .2f; UpdateTreeOcclusion(); });
        S(.3f, () => { Swing = .2f; Shot("trees_beside_up"); });
        S(.2f, () => GD.Print("[check] TREES DONE"));
    }
}
