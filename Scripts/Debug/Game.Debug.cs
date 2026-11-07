using Godot;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lamplighter;

/// <summary>Отладочная панель в игре (F10): состояние, быстрая смена этапа развития, правка чисел боя (CombatTuning) на лету, утилиты.
/// Доступна в отладочной сборке, при включённой «Диагностике» в настройках или LAMP_DEBUG=1; у гостя в сети — недоступна (иначе рассинхрон).
/// Панель — отдельный CanvasLayer в пикселях окна, поэтому не зависит от масштаба интерфейса игры.</summary>
public partial class Game
{
    public bool DebugGod;                       // бессмертие (HurtPlayer возвращается раньше)
    public DebugPanel DebugUi;

    public static bool DebugAllowed => OS.IsDebugBuild() || Diag.Enabled || OS.GetEnvironment("LAMP_DEBUG") != "";
    public bool DebugBlocked => NetClient;      // у гостя состояние принадлежит хосту
    bool DebugTyping => DebugUi != null && DebugUi.Visible && DebugUi.HasTextFocus();

    void DebugInit()
    {
        DebugUi = GetNode<DebugPanel>("DebugInterface/DebugPanel");
        DebugUi.Bind(this);
    }

    /// <summary>true — событие поглощено панелью и игра его не обрабатывает.</summary>
    bool DebugInput(InputEvent e)
    {
        if (DebugUi == null) return false;
        if (e is InputEventKey k && k.Pressed && !k.Echo && k.Keycode == Key.F10)
        {
            if (!DebugAllowed) { Toast("Отладка выключена: включите «Диагностику» в настройках"); return true; }
            if (DebugBlocked) { Toast("Отладка недоступна гостю сетевой игры"); return true; }
            DebugUi.Toggle(); GetViewport().SetInputAsHandled(); return true;
        }
        if (!DebugUi.Visible) return false;
        if (e is InputEventMouse m)
        {
            bool over = DebugUi.Contains(m.Position);
            if (!over && e is InputEventMouseButton mb && mb.Pressed) DebugUi.DropFocus();      // клик по миру возвращает управление игре
            return over;                                                                      // клики и колесо над панелью не доходят до мира
        }
        if (e is InputEventKey ek && DebugTyping)
        {
            if (ek.Pressed && ek.Keycode == Key.Escape) { DebugUi.DropFocus(); GetViewport().SetInputAsHandled(); }
            return true;                                                                      // пока печатаем в поле, клавиши игре не нужны
        }
        return false;
    }

    // ─────────── живое состояние ───────────
    public string DebugInfo()
    {
        var sb = new StringBuilder(); int reg = RegionAt(PPos); var pt = ToTile(PPos);
        int elites = 0; float cost = 0; foreach (var en in Enemies) { if (en.Elite) elites++; cost += EnemyCost(en); }
        sb.AppendLine($"FPS {Engine.GetFramesPerSecond():F0}   время игры {PlaySecs / 3600f:F2} ч   скорость ×{Engine.TimeScale:F2}");
        sb.AppendLine($"Герой: ур. {Level}  опыт {Xp:F0}/{XpNeed:F0}  здоровье {Hp:F0}/{MaxHp:F0}  инструмент {Tier}");
        sb.AppendLine($"Урон {PlayerDamage:F1}   радиус света {PlayerLightR:F0}   убийств {Kills}");
        sb.AppendLine($"Маяк {BeaconLevel}   боссы {BossCount}/5 (маска {Convert.ToString(BossMask, 2).PadLeft(5, '0')})   фонарей {TotalLit}");
        sb.AppendLine($"Регион {reg}: {RegionNames[Math.Clamp(reg, 0, RegionNames.Length - 1)]}   клетка {pt.X},{pt.Y}   сложность {(Diff == 1 ? "сложная" : "обычная")}");
        sb.AppendLine($"Враги {Enemies.Count} (элитных {elites})   стоимость {cost:F1} / бюджет {EncounterBudget(reg, false, 1):F1}");
        sb.AppendLine($"Чанков {LoadedChunks}   узлов {(int)Performance.GetMonitor(Performance.Monitor.ObjectNodeCount)}   памяти {Performance.GetMonitor(Performance.Monitor.MemoryStatic) / 1048576.0:F0} МБ");
        sb.AppendLine($"Постройки {Buildings.Count}   жителей {Villagers.Count}   реликвий {OwnedRelics.Count}   оружия {Owned.Count}");
        return sb.ToString();
    }

    /// <summary>Снимок состояния для баг-репорта или заметок плейтеста.</summary>
    public string DebugSnapshotJson()
    {
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            play_hours = PlaySecs / 3600f, level = Level, tier = Tier, beacon = BeaconLevel, boss_mask = BossMask, lit = TotalLit, hp = Hp, max_hp = MaxHp,
            damage = PlayerDamage, light = PlayerLightR, kills = Kills, region = RegionAt(PPos), buildings = Buildings.Count, villagers = Villagers.Count,
            diff = Diff, enemies = Enemies.Count, fps = Engine.GetFramesPerSecond()
        }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }

    // ─────────── быстрая смена этапа ───────────
    /// <summary>Резервная копия сохранения перед прыжком (SavePath + ".debugbak"): отката в игре нет, файл можно вернуть вручную.</summary>
    string DebugBackup()
    {
        try
        {
            Save(); var src = ProjectSettings.GlobalizePath(SavePath);
            if (System.IO.File.Exists(src)) { System.IO.File.Copy(src, src + ".debugbak", true); return src + ".debugbak"; }
        }
        catch (Exception ex) { GD.PushWarning("debug backup: " + ex.Message); }
        return null;
    }

    void DebugAddB(Tl t, int dx, int dy, int lv)
    {
        int x = W.Spawn.X + dx, y = W.Spawn.Y + dy; if (Buildings.Exists(b => b.X == x && b.Y == y)) return;
        var b = new Building { Type = t, X = x, Y = y, Level = lv, Forced = true, Worker = true }; Buildings.Add(b); SetObj(x, y, t);
    }

    public void DebugSetBeacon(int n)
    {
        BeaconLevel = Math.Clamp(n, 1, 6);
        foreach (var s in new[] { W.Seal1, W.Seal2, W.Seal3, W.Seal4 }) { int need = s == W.Seal1 ? 2 : s == W.Seal2 ? 3 : s == W.Seal3 ? 4 : 5; if (BeaconLevel >= need) foreach (var t in s) SetObj(t.X, t.Y, Tl.None); }
        ApplyBeaconRadius(); RecomputeEffects();
    }

    public void DebugSetLevel(int lv)
    {
        Level = Math.Clamp(lv, 1, 60); Xp = 0; MaxHp = 60 + 10 * (Level - 1); Hp = MaxHp; RecomputeEffects();
    }

    public void DebugSetBoss(int id, bool down)
    {
        if (down) BossMask |= 1 << (id - 1); else BossMask &= ~(1 << (id - 1));
        RecomputeEffects();
    }

    public void DebugLight(int maxRegion, bool bosses) { LightAncients(maxRegion, bosses); RecomputeEffects(); }

    public void DebugAllTech() { TechMask = 0x0FFFFFFF & ~(BossDown(4) ? 0 : (1 << (int)Tech.Prism)); RecomputeEffects(); }

    public void DebugGiveAll(int n) { for (int i = 0; i < It.Count; i++) if (i != It.Core) Inv[i] = Math.Max(Inv[i], n); }

    public void DebugHeal() { Hp = MaxHp; Invuln = 0; }

    public void DebugKillAll() { foreach (var e in new List<Enemy>(Enemies)) e.Hp = 0; }

    public void DebugSpawn(EK kind, int n)
    {
        for (int i = 0; i < n; i++) SpawnEnemy(PPos + Vector2.FromAngle(rng.Randf() * Mathf.Tau) * rng.RandfRange(70, 130), kind).Age = 1;
    }

    public void DebugTeleport(Vector2 p) { PPos = p; cam.ResetSmoothing(); Enemies.Clear(); Projs.Clear(); }
    public void DebugTeleportAncient(Ancient a) { if (a != null) DebugTeleport(TileCenter(a.Tile.X, a.Tile.Y + 2)); }

    /// <summary>Пресеты этапов: только повышают состояние (уровень, инструмент, маяк, маски, предметы, постройки).</summary>
    public string DebugStage(string kind)
    {
        var bak = DebugBackup();
        bool late = kind != "mid";
        int lv = kind == "mid" ? 9 : kind == "late" ? 18 : 24, tier = kind == "mid" ? 2 : 4, beacon = kind == "mid" ? 3 : kind == "late" ? 5 : 6;
        if (Level < lv) DebugSetLevel(lv);
        ExtraVillagers = Math.Max(ExtraVillagers, 3); Tier = Math.Max(Tier, tier);
        BossMask |= kind == "mid" ? 3 : kind == "late" ? 15 : 31;
        DebugSetBeacon(Math.Max(BeaconLevel, beacon));
        LightAncients(kind == "mid" ? 1 : kind == "late" ? 3 : 4, kind != "mid");
        int tm = kind == "mid" ? 0b1111_1111_1111 : late ? 0x0FFFFFFF & ~(1 << (int)Tech.Prism) : 0x0FFFFFFF; TechMask |= tm;
        if (BossDown(4)) TechMask |= 1 << (int)Tech.Prism;
        DebugAddB(Tl.Warehouse, 2, 0, late ? 4 : 2); DebugAddB(Tl.Sawmill, 5, 0, late ? 4 : 2); DebugAddB(Tl.Mine, 8, 0, late ? 4 : 2); DebugAddB(Tl.Forge, 11, 0, 3);
        DebugAddB(Tl.Hub, 2, 3, 2); DebugAddB(Tl.Stall, 5, 3, 2); if (late) { DebugAddB(Tl.Refinery, 8, 3, 3); DebugAddB(Tl.Drill, 11, 3, 3); }
        for (int i = Villagers.Count; i < 3; i++) Villagers.Add(new Villager { Pos = TileCenter(W.Spawn.X + 3 + i, W.Spawn.Y + 2) });
        foreach (var w in late ? new[] { "sword1", "bow1", "staff1", "sword2", "staff2" } : new[] { "sword1", "bow1", "staff1" }) if (!Owned.Exists(o => o.Id == w)) GiveWeapon(w, false);
        foreach (var r in new[] { "r_embersp", "r_frostshard", "r_beaconwick", "r_dawnprism" }) if (!OwnedRelics.Contains(r)) GiveRelic(r, true);
        foreach (var b in new[] { "bp_hub", "bp_refinery", "bp_stall", "bp_amp" }) UnlockBp(b, false);
        DebugGiveAll(late ? 60 : 25); for (int i = 0; i < It.Count; i++) Stock[i] = Math.Max(Stock[i], late ? 120 : 40);
        PlaySecs = Math.Max(PlaySecs, late ? 3 * 3600 : 3600);
        if (kind == "post")
        {
            Finished = true; RiftUnlocked = Math.Max(RiftUnlocked, 4); LegendMask |= 0b00101; CosmMask |= 0b0111; LoreMask |= 0x7FFF;
            foreach (var a in W.Ancients) { a.Lit = true; a.Anim = 1; }
        }
        RecomputeEffects(); ApplyBeaconRadius(); DebugHeal(); Save();
        return bak == null ? "Этап применён" : "Этап применён. Копия сохранения: " + bak;
    }
}
