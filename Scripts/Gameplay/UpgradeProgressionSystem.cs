using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Открытие категорий окна улучшений. Не знает об интерфейсе: хранит открытые категории, проверяет условия из данных (UpgradeCategoryData),
/// отправляет событие CategoryUnlocked и сохраняется вместе с игрой. Категория открывается при реальном обнаружении биома — постоянно зажжённом древнем фонаре в нём
/// (а не при генерации, загрузке чанков или приближении), и после открытия не закрывается никогда.</summary>
public sealed class UpgradeProgressionSystem
{
    public const string Dir = "res://Resources/Upgrades/Categories";
    public event Action<UpgradeCategoryData> CategoryUnlocked;

    readonly Game g; readonly List<UpgradeCategoryData> all = new();
    readonly HashSet<string> unlocked = new(), unseen = new();
    float tickT;

    public UpgradeProgressionSystem(Game game)
    {
        g = game;
        foreach (var f in DirAccess.GetFilesAt(Dir))
        {
            string name = f.EndsWith(".remap") ? f.Substring(0, f.Length - 6) : f;
            if (!name.EndsWith(".tres")) continue;
            var c = ResourceLoader.Load<UpgradeCategoryData>($"{Dir}/{name}"); if (c != null && c.Id != "") all.Add(c);
        }
        all.Sort((a, b) => a.Order.CompareTo(b.Order));
    }

    public IReadOnlyList<UpgradeCategoryData> All => all;
    public List<UpgradeCategoryData> Visible() => all.FindAll(c => unlocked.Contains(c.Id));
    public UpgradeCategoryData Get(string id) => all.Find(c => c.Id == id);
    public UpgradeCategoryData ByPage(int page) => all.Find(c => c.Page == page);
    public bool IsUnlocked(string id) => unlocked.Contains(id);
    public bool IsPageUnlocked(int page) { var c = ByPage(page); return c != null && unlocked.Contains(c.Id); }
    public bool IsNew(string id) => unseen.Contains(id);
    public void MarkSeen(string id) => unseen.Remove(id);

    /// <summary>Выполнено ли условие открытия: биом обнаружен либо уже изучено улучшение этой категории (для старых сохранений).</summary>
    public bool Satisfied(UpgradeCategoryData c)
    {
        if (c.Condition == UpgradeUnlock.Always) return true;
        if (c.Condition == UpgradeUnlock.BiomeDiscovered && g.BiomeDiscovered(c.BiomeId)) return true;
        for (int i = 0; i < Game.Techs.Length; i++) if (Game.Techs[i].Page == c.Page && g.Has((Tech)i)) return true;
        return false;
    }

    /// <summary>Проверка условий. announce = false — тихое восстановление (новая игра, загрузка): событие не отправляется.</summary>
    public void Update(bool announce)
    {
        foreach (var c in all)
        {
            if (unlocked.Contains(c.Id) || !Satisfied(c)) continue;
            unlocked.Add(c.Id);
            if (announce) { unseen.Add(c.Id); CategoryUnlocked?.Invoke(c); }
        }
    }

    public void Tick(float dt) { tickT -= dt; if (tickT > 0) return; tickT = .5f; Update(true); }

    /// <summary>Загрузка. Нет сохранённого списка (старое сохранение) — категории восстанавливаются из подтверждённого прогресса; тихо, без уведомлений.</summary>
    public void Restore(string[] cats, string[] fresh)
    {
        unlocked.Clear(); unseen.Clear();
        if (cats != null) foreach (var id in cats) if (Get(id) != null) unlocked.Add(id);
        if (fresh != null) foreach (var id in fresh) if (unlocked.Contains(id)) unseen.Add(id);
        Update(false);
    }

    public string[] SaveUnlocked() => new List<string>(unlocked).ToArray();
    public string[] SaveUnseen() => new List<string>(unseen).ToArray();
}
