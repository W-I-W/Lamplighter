using Godot;
using System;
using System.Linq;
using Lamplighter.Balance;

namespace Lamplighter;

/// <summary>Real AI A/B regression against pre-feedback tuning; no simulated campaign policy.</summary>
public partial class Game
{
    void BuildCombatPressureScript()
    {
        S(.1f, () =>
        {
            int failures = 0;
            void Check(bool ok, string text) { if (!ok) failures++; GD.Print($"[check] {(ok ? "PASS" : "FAIL")} {text}"); }
            bool Same(float a, float b) => Mathf.Abs(a - b) < .01f;
            Menu = Mn.None; noHeal = true; TestNoSpawn = true; EventsEnabled = false; Diff = 0; TestPlayers = 0;
            Array.Clear(SkillLv); Owned.Clear(); CurW = -1; EquippedRelics.Clear(); OwnedRelics.Clear(); TempRelics.Clear(); RecomputeEffects();
            Check(Same(CombatMath.BaseHealth(1), 60) && Same(CombatMath.BaseHealth(20), 136) && Same(CombatMath.BaseHealth(80), 180), "pressure: health growth is bounded, 60 / 136 / 180");
            var legacy = new CharacterSave { Id = "migration_probe", Level = 20, MaxHp = 270, Hp = 155, Equipped = new[] { "r_titanheart" }, Relics = new[] { "r_titanheart" } };
            ApplyCharacter(legacy, null);
            Check(Same(MaxHp, 196) && Same(Hp, 98), "pressure: legacy health preserves shrine bonus, relic and half-health injury");
            Check(SaveManager.SaveCharacter(legacy), "pressure: upgraded health can be saved in the isolated test directory");
            legacy = SaveManager.LoadCharacter(legacy.Id);
            EquippedRelics.Clear(); OwnedRelics.Clear(); ApplyCharacter(legacy, null);
            Check(legacy.HealthModel == CombatMath.HealthModel && Same(MaxHp, 196) && Same(Hp, 98), "pressure: loading an upgraded character does not reduce health twice");
            EquippedRelics.Clear(); OwnedRelics.Clear(); RecomputeEffects();
            Level = 1; Xp = 0; MaxHp = 60; Hp = 10; GiveXp(XpNeed, true);
            Check(Level == 2 && Same(MaxHp, 64) && Same(Hp, 14), "pressure: a level grants four health, not a free full heal");
            var m = BalanceModel.FromGame(); var player = new SimPlayer(m) { Hp = 10 }; player.GiveXp(player.XpNeed, true);
            Check(Same(player.Hp, Hp) && Same(player.MaxHp, MaxHp), "pressure: injured level-up matches the simulator");
            var spot = FindDarkArena(); PPos = spot; PProjs.Clear(); Enemies.Clear(); Projs.Clear(); Zones.Clear();
            GiveWeapon("bow1", false); CurW = 0; AtkCd = 0; RangedRecovery = 0;
            float freeSpeed = PlayerWalkSpeed; FireRanged(CurDef, Vector2.Right);
            Check(Same(PlayerWalkSpeed, freeSpeed * CombatMath.RangedMoveMul), "pressure: shooting commits movement for 0.28 seconds");
            UpdatePlayer(.3f);
            Check(Same(PlayerWalkSpeed, freeSpeed), "pressure: normal movement returns after shot recovery");
            AtkCd = 0; FireRanged(CurDef, Vector2.Right); DashT = .2f; DashDir = Vector2.Right; UpdatePlayer(.01f);
            Check(Same(pVel.Length(), 260), "pressure: dash still escapes during shot recovery");
            DashT = 0; RangedRecovery = 0; PPos = spot; PProjs.Clear();
            var enemy = SpawnEnemy(spot + new Vector2(20, 0), EK.Shade, null, 1); enemy.Hp = enemy.MaxHp = 1000;
            Hurt(enemy, 1, Vector2.Right, true); float meleeKb = enemy.Kb.Length(); enemy.Kb = Vector2.Zero; Hurt(enemy, 1, Vector2.Right, false);
            Check(Same(enemy.Kb.Length(), meleeKb * CombatMath.RangedKnockbackMul), "pressure: ranged attacks cannot push enemies back like melee");
            var tune = Tune.T;
            float speed = tune.EnemySpeed, damage = tune.EnemyDamage, hp = tune.EnemyHp, pause = tune.EnemyAtkPause;
            float Incoming(bool old)
            {
                tune.EnemySpeed = old ? 1.15f : speed; tune.EnemyDamage = old ? 1.6f : damage;
                tune.EnemyHp = old ? 1.75f : hp; tune.EnemyAtkPause = old ? 1.7f : pause;
                Enemies.Clear(); Projs.Clear(); Zones.Clear(); PProjs.Clear(); PPos = spot; pVel = Vector2.Zero;
                Hp = MaxHp = 10000; Invuln = 0; rng.Seed = 424242; aiRespawn = null;
                foreach (var offset in new[] { new Vector2(22, 0), new Vector2(0, 22), new Vector2(-22, 0) }) SpawnEnemy(spot + offset, EK.Shade, null, 1);
                SpawnEnemy(spot + new Vector2(80, 0), EK.Spitter, null, 1);
                for (int i = 0; i < 1200; i++) { Time += .05f; Invuln = Mathf.Max(0, Invuln - .05f); UpdateEnemies(.05f); UpdateProjectiles(.05f); }
                return 10000 - Hp;
            }
            try
            {
                float before = Incoming(true), after = Incoming(false);
                Check(before > 0 && after > before * 1.2f, $"pressure: real AI damage over 60s rises {before:F0} -> {after:F0}");
                Check(CombatMath.BaseHealth(20) / after < 250f / before * .6f, "pressure: level-20 passive survival is less than sixty percent of the old duration");
            }
            finally { tune.EnemySpeed = speed; tune.EnemyDamage = damage; tune.EnemyHp = hp; tune.EnemyAtkPause = pause; }
            Enemies.Clear(); Projs.Clear(); Hp = MaxHp = CombatMath.BaseHealth(Level);
            GD.Print($"[check] SUMMARY failures={failures}"); GD.Print("[check] COMBAT PRESSURE DONE");
        });
    }
}
