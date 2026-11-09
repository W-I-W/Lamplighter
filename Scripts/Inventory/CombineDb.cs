using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Lamplighter;

/// <summary>Рецепт комбинирования двух предметов. Компоненты A и B равноправны (A+B = B+A) и могут совпадать (A+A).
/// Результат — ресурс (ResItem) или постройка (Build). У построек без явных компонентов цена берётся из меню построек.</summary>
public sealed class CombineRecipe
{
    public string Id, Build; public int A = -1, NA, B = -1, NB, ResItem = -1, ResN = 1; public bool Known; public float Xp = 3;
    public bool Derived => A < 0;                                                   // компоненты берутся из цены постройки
}

/// <summary>Таблица рецептов комбинирования: читается из Resources/Data/combine_recipes.json; новые рецепты — новыми записями данных.</summary>
public static class CombineDb
{
    public const string Path = "res://Resources/Data/combine_recipes.json";
    static List<CombineRecipe> all;
    public static IReadOnlyList<CombineRecipe> All { get { if (all == null) Load(); return all; } }

    public static void Load(string json = null)
    {
        all = new List<CombineRecipe>();
        try
        {
            json ??= FileAccess.FileExists(Path) ? FileAccess.GetFileAsString(Path) : "{}";
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("recipes", out var list)) return;
            foreach (var e in list.EnumerateArray())
            {
                var r = new CombineRecipe { Id = e.GetProperty("id").GetString() };
                if (e.TryGetProperty("known", out var k)) r.Known = k.GetBoolean();
                if (e.TryGetProperty("xp", out var xp)) r.Xp = xp.GetSingle();
                if (e.TryGetProperty("a", out var a)) (r.A, r.NA) = Part(a);
                if (e.TryGetProperty("b", out var b)) (r.B, r.NB) = Part(b);
                var res = e.GetProperty("result");
                if (res.TryGetProperty("build", out var bk)) r.Build = bk.GetString();
                else (r.ResItem, r.ResN) = Part(res);
                if (r.Build == null && (r.ResItem < 0 || r.A < 0 || r.B < 0)) { GD.PushWarning("combine recipe skipped: " + r.Id); continue; }
                if (r.Build != null && (r.A < 0) != (r.B < 0)) { GD.PushWarning("combine recipe skipped: " + r.Id); continue; }
                all.Add(r);
            }
        }
        catch (Exception ex) { GD.PushWarning("combine recipes failed: " + ex.Message); }
    }

    static (int item, int n) Part(JsonElement e)
    {
        string name = e.GetProperty("item").GetString();
        var f = typeof(It).GetField(name);
        if (f == null) throw new Exception("unknown item " + name);
        return ((int)f.GetRawConstantValue(), e.TryGetProperty("n", out var n) ? Math.Max(1, n.GetInt32()) : 1);
    }

    /// <summary>Тестовое добавление рецепта.</summary>
    public static void Add(CombineRecipe r) { _ = All; all.Add(r); }
    public static void Remove(string id) { _ = All; all.RemoveAll(r => r.Id == id); }
}
