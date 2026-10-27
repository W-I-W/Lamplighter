using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Lamplighter;

public sealed class AchDef { public string Id, Name, Desc; public Func<Game, bool> Check; }

/// <summary>Достижения: собственная система (user://achievements.json, общая для всех сохранений) с точкой подключения Steam.
/// При добавлении Steamworks (GodotSteam) достаточно назначить Ach.Backend = id => Steam.SetAchievement(id) — AppID задаётся ТОЛЬКО в релизной сборке пользователем, в проекте его нет.</summary>
public static class Ach
{
    public static readonly AchDef[] All =
    {
        new() { Id = "first_light", Name = "Первый свет", Desc = "Зажгите первый древний фонарь", Check = g => g.LitCount >= 1 },
        new() { Id = "ten_lanterns", Name = "Десять территорий", Desc = "Зажгите десять древних фонарей", Check = g => g.TotalLit >= 10 },
        new() { Id = "forge", Name = "Кузнец света", Desc = "Постройте кузницу в деревне", Check = g => g.HasBuilding(Tl.Forge) },
        new() { Id = "auto_mine", Name = "Работа без рук", Desc = "Запустите автоматическую добычу", Check = g => g.HasBuilding(Tl.Mine) || g.HasBuilding(Tl.Drill) },
        new() { Id = "guardian1", Name = "Страж леса", Desc = "Победите Хранителя Тёмного леса", Check = g => g.BossDown(1) },
        new() { Id = "guardian2", Name = "Страж глубин", Desc = "Победите Хранителя глубин", Check = g => g.BossDown(2) },
        new() { Id = "guardian3", Name = "Страж пепла", Desc = "Победите Пепельного стража", Check = g => g.BossDown(3) },
        new() { Id = "guardian4", Name = "Страж зимы", Desc = "Победите Хранителя Зимы", Check = g => g.BossDown(4) },
        new() { Id = "source", Name = "Источник погашен", Desc = "Победите Источник Тьмы", Check = g => g.BossDown(5) },
        new() { Id = "beacon_full", Name = "Маяк восстановлен", Desc = "Полностью восстановите Центральный маяк", Check = g => g.BeaconLevel >= 6 },
        new() { Id = "complete", Name = "Свет вернулся", Desc = "Завершите игру", Check = g => g.Finished },
        new() { Id = "level10", Name = "Опытный фонарщик", Desc = "Достигните 10 уровня", Check = g => g.Level >= 10 },
        new() { Id = "rescue", Name = "Не один", Desc = "Освободите жителя", Check = g => g.Villagers.Count >= 1 },
        new() { Id = "relics", Name = "Коллекционер", Desc = "Соберите 8 реликвий", Check = g => g.OwnedRelics.Count >= 8 },
        new() { Id = "lore", Name = "Летописец", Desc = "Прочтите все записки", Check = g => g.LoreCount >= Story.Lore.Length },
        new() { Id = "rift1", Name = "Разлом пройден", Desc = "Пройдите разлом Тьмы", Check = g => g.RiftClears >= 1 },
        new() { Id = "rift8", Name = "Самый глубокий разлом", Desc = "Пройдите разлом 8 уровня", Check = g => (g.RiftFirst & (1 << 8)) != 0 },
        new() { Id = "legends", Name = "Легенда", Desc = "Получите все легендарные улучшения", Check = g => g.LegendMask == (1 << Game.Legends.Length) - 1 },
        new() { Id = "decor", Name = "Хозяин земель", Desc = "Украсьте четыре освобождённых региона", Check = g => System.Numerics.BitOperations.PopCount((uint)g.DecorRegions) >= 4 },
    };

    public static readonly HashSet<string> Got = new();
    public static Action<string> Backend;                 // сюда подключается Steam API
    static bool loaded;
    const string Path = "user://achievements.json";

    public static void Load()
    {
        if (loaded) return; loaded = true;
        try { if (FileAccess.FileExists(Path)) { var l = JsonSerializer.Deserialize<string[]>(FileAccess.GetFileAsString(Path)); if (l != null) foreach (var s in l) Got.Add(s); } } catch { }
    }

    static void Save() { try { using var f = FileAccess.Open(Path, FileAccess.ModeFlags.Write); f?.StoreString(JsonSerializer.Serialize(new List<string>(Got))); } catch { } }

    public static bool Unlock(string id, Game g)
    {
        if (!Got.Add(id)) return false;
        Save(); Backend?.Invoke(id);
        var d = Array.Find(All, a => a.Id == id);
        if (d != null) g.Announce("Достижение: " + d.Name, d.Desc, new Color("#ffd86b"), "ancient");
        return true;
    }

    public static void Check(Game g) { foreach (var a in All) if (!Got.Contains(a.Id)) { try { if (a.Check(g)) Unlock(a.Id, g); } catch { } } }
}
