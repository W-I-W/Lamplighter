using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Тестовые сохранения для разных стадий игры. Генерация: LAMP_STAGE=mksave LAMP_SAVE=early|mid|late|post LAMP_AUTOTEST=testsaves/NAME.
/// Проверка загрузки: LAMP_STAGE=savesmoke LAMP_LOAD=1 LAMP_AUTOTEST=testsaves/NAME. Обычные сохранения пользователя не затрагиваются.</summary>
public partial class Game
{
    void LightAncients(int maxRegion, bool bosses)
    {
        foreach (var a in W.Ancients) if (a.Region <= maxRegion && (!a.Boss || bosses)) { a.Lit = true; a.Anim = 1; SetObj(a.Tile.X, a.Tile.Y, a.Boss ? Tl.BossOn : Tl.AncOn); }
    }

    void AddB(Tl t, int dx, int dy, int lv)
    {
        var b = new Building { Type = t, X = W.Spawn.X + dx, Y = W.Spawn.Y + dy, Level = lv, Forced = true, Worker = true };
        Buildings.Add(b); SetObj(b.X, b.Y, t);
    }

    void BuildMkSaveScript()
    {
        string kind = OS.GetEnvironment("LAMP_SAVE");
        S(.3f, () =>
        {
            if (kind == "mid" || kind == "late" || kind == "post")
            {
                bool late = kind != "mid";
                ExtraVillagers = 3; Level = kind == "mid" ? 9 : kind == "late" ? 18 : 24; Tier = kind == "mid" ? 2 : 4; MaxHp = 60 + 10 * (Level - 1); Hp = MaxHp;
                BossMask = kind == "mid" ? 3 : kind == "late" ? 15 : 31; BeaconLevel = kind == "mid" ? 3 : kind == "late" ? 5 : 6;
                foreach (var s in new[] { W.Seal1, W.Seal2, W.Seal3, W.Seal4 }) { int need = s == W.Seal1 ? 2 : s == W.Seal2 ? 3 : s == W.Seal3 ? 4 : 5; if (BeaconLevel >= need) foreach (var t in s) SetObj(t.X, t.Y, Tl.None); }
                LightAncients(kind == "mid" ? 1 : kind == "late" ? 3 : 4, kind != "mid");
                TechMask = kind == "mid" ? 0b1111_1111_1111 : late ? 0x0FFFFFFF & ~(1 << (int)Tech.Prism) : 0x0FFFFFFF;
                if (BossDown(4)) TechMask |= 1 << (int)Tech.Prism;
                AddB(Tl.Warehouse, 2, 0, late ? 4 : 2); AddB(Tl.Sawmill, 5, 0, late ? 4 : 2); AddB(Tl.Mine, 8, 0, late ? 4 : 2); AddB(Tl.Forge, 11, 0, 3);
                AddB(Tl.Hub, 2, 3, 2); AddB(Tl.Stall, 5, 3, 2); if (late) { AddB(Tl.Refinery, 8, 3, 3); AddB(Tl.Drill, 11, 3, 3); }
                for (int i = 0; i < 3; i++) Villagers.Add(new Villager { Pos = TileCenter(W.Spawn.X + 3 + i, W.Spawn.Y + 2) });
                GiveWeapon("sword1", false); GiveWeapon("bow1", false); GiveWeapon("staff1", false); if (late) { GiveWeapon("sword2", false); GiveWeapon("staff2", false); }
                foreach (var r in new[] { "r_embersp", "r_frostshard", "r_beaconwick", "r_dawnprism" }) GiveRelic(r, true);
                foreach (var b in new[] { "bp_hub", "bp_refinery", "bp_stall", "bp_amp" }) UnlockBp(b, false);
                for (int i = 0; i < It.Count; i++) Inv[i] = i == It.Core ? (kind == "late" ? 1 : 0) : late ? 60 : 25;
                for (int i = 0; i < It.Count; i++) Stock[i] = late ? 120 : 40;
                PlaySecs = late ? 3 * 3600 : 3600;
            }
            if (kind == "post")
            {
                Finished = true; RiftUnlocked = 4; RiftFirst = 0b11110; RiftClears = 3; LegendMask = 0b00101; CosmMask = 0b0111; CosmSel = 1; LoreMask = 0x7FFF;
                foreach (var a in W.Ancients) { a.Lit = true; a.Anim = 1; }
            }
            if (kind == "early") { Level = 1; PlaySecs = 300; }
            RecomputeEffects(); ApplyBeaconRadius(); Save();
            Log($"save '{kind}' written: level={Level} beacon={BeaconLevel} bossMask={BossMask} buildings={Buildings.Count} finished={Finished}");
        });
    }

    void BuildSaveSmokeScript()
    {
        S(.3f, () =>
        {
            Log($"loaded: level={Level} beacon={BeaconLevel} bossMask={BossMask} buildings={Buildings.Count} villagers={Villagers.Count} weapons={Owned.Count} relics={OwnedRelics.Count} finished={Finished} chunks={LoadedChunks}");
            for (int i = 0; i < 12; i++) SpawnEnemy(PPos + new Vector2(rng.RandfRange(-140, 140), rng.RandfRange(-100, 100)), EK.Shade).Age = 1;
        });
        S(6f, () =>
        {
            Check("smoke: alive and sane", Hp > 0 && !float.IsNaN(PPos.X) && World.In(ToTile(PPos).X, ToTile(PPos).Y), $"hp={Hp:F0} pos={ToTile(PPos)}");
            Check("smoke: chunks bounded", LoadedChunks <= 16, $"chunks={LoadedChunks}");
            Save(); Check("smoke: resave valid", ValidSave(SavePath));
            GD.Print($"[check] SUMMARY failures={checkFails}");
        });
    }
}
