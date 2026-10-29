using Godot;
using System;
using System.Linq;

namespace Lamplighter;

/// <summary>Сложность и элитные враги (этап 10). Числа — в CombatTuning (Resources/Combat/tuning.tres). Масштаб кооператива считается при появлении врага и больше не меняется,
/// поэтому выход или приход игрока в бою не восстанавливает здоровье врагов и не вызывает новых.</summary>
public partial class Game
{
    /// <summary>Игроков в сеансе (1 в одиночной игре).</summary>
    public int PlayerCount => NetService.Active ? Math.Clamp(NetService.Players.Values.Count(p => p.Handshaken), 1, 4) : 1;
    int CoopI => PlayerCount - 1;
    public int TestPlayers = 0;                       // только проверки: имитация числа игроков
    int CoopIdx => TestPlayers > 0 ? TestPlayers - 1 : CoopI;

    public static int BossIdOf(EK k) => k switch { EK.Warden => 1, EK.DepthWarden => 2, EK.AshWarden => 3, EK.WinterWarden => 4, EK.Source => 5, _ => 0 };

    /// <summary>Множители урона, здоровья и скорости для нового врага в данной точке. Босс: здоровье по номеру босса и числу игроков, урон применяется при попадании (ctxDmg).</summary>
    public (float dmg, float hp) EnemyMuls(EK k, Vector2 p)
    {
        var t = Tune.T; int reg = Math.Clamp(World.RegionOf((int)(p.X / TS)), 0, 4), ci = CoopIdx; int bid = BossIdOf(k);
        if (bid > 0) return (1f, CombatTuning.At(t.BossHp, bid - 1) * CombatTuning.At(t.CoopBossHp, ci));
        return (t.EnemyDamage * CombatTuning.At(t.BiomeDamage, reg), t.EnemyHp * CombatTuning.At(t.BiomeHp, reg) * CombatTuning.At(t.CoopEnemyHp, ci));
    }

    public float BossDamageMul(EK k) { int b = BossIdOf(k); return b > 0 ? CombatTuning.At(Tune.T.BossDamage, b - 1) : 1f; }
    public float BossTempoMul() => Tune.T.BossTempo * CombatTuning.At(Tune.T.CoopBossTempo, CoopIdx);

    // ───────────────────────── одновременные атаки ─────────────────────────
    int busyAtk;
    int MaxAtk => Tune.T.CoopMaxAttackers != null && Tune.T.CoopMaxAttackers.Length > CoopIdx ? Tune.T.CoopMaxAttackers[CoopIdx] : Tune.T.MaxAttackers;

    bool AtkSlot() { if (busyAtk >= MaxAtk) return false; busyAtk++; return true; }

    void CountBusyAttackers()
    {
        busyAtk = 0;
        foreach (var e in Enemies) if (!e.IsBoss && ((e.Kind == EK.Shade && (e.State == 1 || e.State == 2)) || (e.Kind == EK.Wraith && (e.Tele > 0 || e.Dash > 0)))) busyAtk++;
    }

    // ───────────────────────── элитные враги ─────────────────────────
    public const int EBrute = 1, ESwift = 2, EWarded = 3, ERallying = 4, EVolatile = 5;

    static bool Ranged(EK k) => k == EK.Spitter || k == EK.FrostPriest || k == EK.Weaver || k == EK.Mirage || k == EK.Imp || k == EK.Spirit;

    /// <summary>Шанс стать элитным зависит от биома и числа игроков, но элитные не встречаются у стартовой территории и их число ограничено. Один модификатор на врага, без сочетаний.</summary>
    public bool TryMakeElite(Enemy e, int reg, bool force = false)
    {
        var t = Tune.T; if (e.IsBoss || e.Kind == EK.Swarm || e.Kind == EK.Decoy || e.Kind == EK.Bomber || e.Elite) return false;
        if (!force)
        {
            if (reg == 0 && (LitCount < t.EliteMinLit || e.Pos.DistanceTo(TileCenter(W.Spawn.X, W.Spawn.Y)) < 28 * TS)) return false;
            int cur = 0; foreach (var o in Enemies) if (o.Elite && o.EMod > 0) cur++;
            if (cur >= t.EliteMax + CoopIdx / 2) return false;
            if (rng.Randf() > CombatTuning.At(t.EliteChance, reg) * CombatTuning.At(t.CoopElite, CoopIdx)) return false;
        }
        bool ranged = Ranged(e.Kind);
        int[] pool = ranged ? new[] { EWarded, ERallying, ESwift } : new[] { EBrute, ESwift, EVolatile, ERallying };
        e.EMod = pool[rng.RandiRange(0, pool.Length - 1)]; e.Elite = true;
        if (e.EMod == EBrute) { e.MaxHp *= t.EliteHp; e.Hp = e.MaxHp; e.Scale *= t.EliteDamage; }
        else { e.MaxHp *= 1.35f; e.Hp = e.MaxHp; }
        e.WardT = 3f; return true;
    }

    public static string EliteName(int m) => m switch { EBrute => "Громила", ESwift => "Проворный", EWarded => "Под щитом", ERallying => "Вожак", EVolatile => "Нестабильный", _ => "Элитный" };

    void EliteTick(Enemy e, float dt)
    {
        if (e.RallyT > 0) e.RallyT -= dt;
        if (!e.Elite || e.EMod == 0) return;
        switch (e.EMod)
        {
            case EWarded: e.WardT -= dt; if (e.WardT <= -4f) e.WardT = 3f; break;       // щит держится 3 с, затем 4 с враг уязвим
            case ERallying: foreach (var o in Enemies) if (o != e && !o.IsBoss && o.Pos.DistanceTo(e.Pos) < 70) o.RallyT = .6f; break;
            case EVolatile: if (e.Hp < e.MaxHp * .3f) e.Flash = ((int)(Time * 12) % 2 == 0) ? .05f : 0; break;
        }
    }

    float EnemySpeedMul(Enemy e)
    {
        if (e.IsBoss) return 1f; float m = Tune.T.EnemySpeed;
        if (e.EMod == ESwift) m *= 1.3f; if (e.RallyT > 0) m *= 1.12f; return m;
    }

    float EnemyCdMul(Enemy e)
    {
        if (e.IsBoss) return Tune.T.BossTempo * 0f;       // паузы боссов задаются их собственными таймерами и BossTempoMul
        float m = Tune.T.EnemyAttackRate - 1f; if (e.EMod == ESwift) m += .3f; if (e.RallyT > 0) m += .3f; return m;
    }

    void EliteKilled(Enemy e)
    {
        if (!e.Elite || e.EMod == 0) return;
        Give(It.Shard, 3, e.Pos + new Vector2(0, -6)); Inv[It.Coins] += 8 + 4 * RegionAt(e.Pos); GiveXp(30);
        if (rng.Randf() < .18f + .04f * RegionAt(e.Pos)) { ModInv[rng.RandiRange(0, ModInv.Length - 1)]++; Toast($"Элитный враг «{EliteName(e.EMod)}» оставил модификатор оружия"); }
        if (e.EMod == EVolatile) Explode(e);
        Burst(e.Pos, new Color("#ffd86b"), 16, 90);
    }
}
