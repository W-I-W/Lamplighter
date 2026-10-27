using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Тест Ледяного монастыря (LAMP_STAGE=ice): враги, скольжение, Хранитель Зимы, награды.</summary>
public partial class Game
{
    public int TestPrisms;

    void BuildIceScript()
    {
        S(.3f, () =>
        {
            BossMask |= 7; BeaconLevel = 4; ApplyBeaconRadius(); foreach (var t in W.Seal3) SetObj(t.X, t.Y, Tl.None);
            foreach (var a in W.Ancients) if (a.Region < 3 && !a.Boss) { a.Lit = true; a.Anim = 1; }
            Inv[It.Core] = 0; Hp = MaxHp = 400;
            var start = new Vector2I(World.IX0 + 8, W.IceGateY);
            var reach = W.Reach(start); int ok = 0, bad = 0;
            foreach (var a in W.Ancients) if (a.Region == 3) { var tt = a.Tile + new Vector2I(0, a.Boss ? 3 : 2); if (reach[World.Key(tt.X, tt.Y)]) ok++; else bad++; }
            Log($"ice ancients reachable={ok} unreachable={bad} arena={W.IceArena} gateY={W.IceGateY}");
            Tp(TileCenter(start.X + 6, start.Y)); noHeal = false;
        });
        S(.6f, () => { Shot("i1_gate"); Log($"region={World.RegionOf(ToTile(PPos).X)}"); });
        S(.2f, () =>
        {
            foreach (var k in new[] { EK.IceWolf, EK.FrostPriest, EK.Sentinel, EK.Spitter }) SpawnEnemy(PPos + new Vector2(rng.RandfRange(-60, 60), rng.RandfRange(-50, -20)), k).Age = 1;
            Log($"ice enemies spawned={Enemies.Count}");
        });
        S(4f, () => { Log($"after fight enemies={Enemies.Count} chill={ChillT:F1} hp={Hp:F0}"); Shot("i2_enemies"); Enemies.Clear(); });
        S(.2f, () =>
        {
            // скольжение: идём по льду и проверяем, что скорость затухает не мгновенно
            var t = FindTile(Art.IceFloor); Log($"ice floor tile found={t.X >= 0}");
            if (t.X >= 0) { Tp(TileCenter(t.X, t.Y)); Log($"onIce={OnIce(PPos)}"); }
        });
        S(.4f, () => Shot("i3_ice"));
        S(.2f, () =>
        {
            var bl = BossLantern(4); if (bl == null) { Log("NO BOSS LANTERN"); return; }
            aimOverride = null; Tp(bl.Pos + new Vector2(-90, 50)); Log($"ice boss lantern {bl.Tile}");
        });
        S(2.5f, () => { Log($"boss alive={Boss != null} kind={Boss?.Kind} hp={Boss?.Hp}"); Shot("i4_boss"); if (Boss != null) { Boss.State = 1; Boss.T2 = .05f; } });
        S(1.6f, () => { Log($"pending blocks={PendingBlocks.Count} temp={TempBlocks.Count}"); Shot("i5_blocks"); if (Boss != null) { Boss.State = 2; Boss.T2 = .05f; } });
        S(1f, () => { Log($"summons={Enemies.Count}"); if (Boss != null) { Boss.State = 3; Boss.T2 = .05f; } });
        S(.6f, () => { Log($"projs={Projs.Count}"); Shot("i6_fan"); if (Boss != null) { Boss.Hp = Boss.MaxHp * .5f; Boss.State = 4; Boss.T2 = .05f; } });
        S(1.2f, () => { Log($"phase2 shield={Boss?.Shield} zones={Zones.Count}"); Shot("i7_phase2"); if (Boss != null) Hurt(Boss, 10, Vector2.Right); });
        S(.3f, () => { Log($"hp after hit on shield={Boss?.Hp} (should be unchanged if shielded)"); TestPrisms = 2; });
        S(.8f, () => { Log($"shield after prisms={Boss?.Shield} vuln={Boss?.VulnT:F1}"); Shot("i8_broken"); TestPrisms = 0; if (Boss != null) { Boss.Hp = Boss.MaxHp * .2f; } });
        S(1f, () => { Log($"phase3 shield={Boss?.Shield}"); TestPrisms = 3; });
        S(1f, () => { if (Boss != null) Hurt(Boss, 99999, Vector2.Right); });
        S(1.2f, () => { Log($"boss4 defeated={BossDown(4)} prismTech={Has(Tech.Prism)} cores={Inv[It.Core]} weapon={HasWeapon("staff_w")}"); Shot("i9_victory"); });
    }

    Vector2I FindTile(int ground)
    {
        for (int y = 0; y < World.Ht; y++) for (int x = World.IX0 + 10; x < World.IX1; x++) if (W.Ground[World.Key(x, y)] == ground && W.Get(x, y) == Tl.None) return new Vector2I(x, y);
        return new Vector2I(-1, -1);
    }
}
