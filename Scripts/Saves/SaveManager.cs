using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Lamplighter;

/// <summary>Сохранения: user://characters/&lt;CharacterId&gt;.json и user://worlds/&lt;WorldId&gt;.json (+ .png миниатюра мира). Герои и миры независимы: ни один файл не ссылается на другой, кроме
/// личных данных героя в конкретном мире (CharacterSave.Worlds[WorldId]). Запись атомарная (временный файл, затем замена; прежний файл остаётся как .bak).</summary>
public static class SaveManager
{
    public static string Root = "user://";                      // в автотестах подменяется папкой теста
    static readonly JsonSerializerOptions Opt = new() { IncludeFields = true };
    public static string DirOf(string root, string name) => root.EndsWith("/") || root.EndsWith("\\") ? root + name : root + "/" + name;     // "user://" нельзя обрезать до "user:"
    public static string CharDir => DirOf(Root, "characters");
    public static string WorldDir => DirOf(Root, "worlds");
    public static string CharPath(string id) => $"{CharDir}/{id}.json";
    public static string WorldPath(string id) => $"{WorldDir}/{id}.json";
    public static string ThumbPath(string id) => $"{WorldDir}/{id}.png";

    public static string NewId() => Guid.NewGuid().ToString("N").Substring(0, 12);
    public static string Stamp() => DateTime.Now.ToString("dd.MM.yyyy HH:mm");

    public static bool ValidId(string id) => !string.IsNullOrEmpty(id) && id.Length <= 40 && id.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-');

    static string Abs(string p) => p.StartsWith("user://") || p.StartsWith("res://") ? ProjectSettings.GlobalizePath(p) : p;
    static void Ensure(string absDir) { if (!Directory.Exists(absDir)) Directory.CreateDirectory(absDir); }

    static bool Write(string path, string json)
    {
        try
        {
            string abs = Abs(path), tmp = abs + ".tmp"; Ensure(Path.GetDirectoryName(abs));
            File.WriteAllText(tmp, json);
            if (File.Exists(abs)) { try { using var _ = JsonDocument.Parse(File.ReadAllText(abs)); File.Copy(abs, abs + ".bak", true); } catch { } }
            File.Move(tmp, abs, true); return true;
        }
        catch (Exception e) { GD.PushWarning("save write failed: " + e.Message); return false; }
    }

    static T Read<T>(string path) where T : class
    {
        foreach (var p in new[] { Abs(path), Abs(path) + ".bak" })
            try { if (!File.Exists(p)) continue; var v = JsonSerializer.Deserialize<T>(File.ReadAllText(p), Opt); if (v != null) return v; } catch { }
        return null;
    }

    // ───────────────────────── герои ─────────────────────────
    public static CharacterSave LoadCharacter(string id) => ValidId(id) ? Read<CharacterSave>(CharPath(id)) : null;

    public static bool SaveCharacter(CharacterSave c)
    {
        if (c == null || !ValidId(c.Id)) return false; c.Stamp = Stamp(); c.Look?.Clamp();
        return Write(CharPath(c.Id), JsonSerializer.Serialize(c, Opt));
    }

    public static bool DeleteCharacter(string id) => ValidId(id) && DeleteFiles(CharPath(id), CharPath(id) + ".bak", CharPath(id) + ".tmp");

    public static List<CharInfo> ListCharacters()
    {
        var res = new List<CharInfo>();
        foreach (var f in Files(CharDir))
        {
            var id = Path.GetFileNameWithoutExtension(f); var c = LoadCharacter(id); if (c == null || c.Id != id) continue;
            var ci = new CharInfo { Id = c.Id, Name = c.Name, Stamp = c.Stamp, Look = c.Look ?? new Look(), PlaySecs = c.PlaySecs, Level = c.Level, Tier = c.Tier, MaxHp = c.MaxHp, Created = c.Created };
            if (c.SkV != null) foreach (var v in c.SkV) ci.SkillPts += v;
            if (c.SkK != null && c.SkV != null) for (int i = 0; i < c.SkK.Length && i < c.SkV.Length; i++) if (c.SkK[i] == "Spark" && c.SkV[i] > 0) ci.HasLamp = true;
            ci.Weapons = c.WId?.Length ?? 0; ci.Relics = c.Relics?.Length ?? 0;
            ci.Weapon = c.WId != null && c.WId.Length > 0 ? c.WId[Math.Clamp(c.CurW, 0, c.WId.Length - 1)] : ""; ci.Tool = c.CurTool ?? "pickaxe";
            res.Add(ci);
        }
        return res.OrderByDescending(x => LastWrite(CharPath(x.Id))).ToList();
    }

    // ───────────────────────── миры ─────────────────────────
    public static WorldSave LoadWorld(string id) => ValidId(id) ? Read<WorldSave>(WorldPath(id)) : null;

    public static bool SaveWorld(WorldSave w)
    {
        if (w == null || !ValidId(w.Id)) return false; w.Stamp = Stamp();
        return Write(WorldPath(w.Id), JsonSerializer.Serialize(w, Opt));
    }

    public static bool DeleteWorld(string id) => ValidId(id) && DeleteFiles(WorldPath(id), WorldPath(id) + ".bak", WorldPath(id) + ".tmp", ThumbPath(id));

    public static List<WorldInfo> ListWorlds()
    {
        var res = new List<WorldInfo>();
        foreach (var f in Files(WorldDir))
        {
            var id = Path.GetFileNameWithoutExtension(f); var w = LoadWorld(id); if (w == null || w.Id != id) continue;
            var wi = new WorldInfo { Id = w.Id, Name = w.Name, Stamp = w.Stamp, Seed = w.Seed, Difficulty = w.Difficulty, Beacon = Math.Max(1, w.Beacon), PlaySecs = w.PlaySecs, Created = w.Created, Finished = w.Finished };
            if (w.Explored != null) { try { foreach (var b in Convert.FromBase64String(w.Explored)) if (b != 0) wi.Explored++; } catch { } }
            if (w.Lit != null) foreach (var l in w.Lit) if (l) wi.Lit++;
            res.Add(wi);
        }
        return res.OrderByDescending(x => LastWrite(WorldPath(x.Id))).ToList();
    }

    /// <summary>Доля исследованной карты, 0..1.</summary>
    public static float ExploredFrac(WorldInfo w) => Math.Clamp(w.Explored / (float)((World.Wd / 4) * (World.Ht / 4)), 0f, 1f);

    /// <summary>Новый мир: сохраняются настройки и seed, стартовую безопасную территорию строит сам генератор; прогресс пуст. Миниатюра рисуется по сгенерированной карте.</summary>
    public static WorldSave CreateWorld(string name, int seed, int difficulty)
    {
        var w = new WorldSave { Id = NewId(), Name = CleanName(name, "Мир"), Seed = seed, Difficulty = Math.Clamp(difficulty, 0, 1), GenV = World.CurrentGen, Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
        SaveWorld(w); WriteThumb(w.Id, World.Generate(seed, w.GenV), new byte[(World.Wd / 4) * (World.Ht / 4)]); return w;      // новый мир: карта скрыта
    }

    public static CharacterSave CreateCharacter(string name, Look look)
    {
        var c = new CharacterSave { Id = NewId(), Name = CleanName(name, "Герой"), Look = look?.Clone() ?? new Look(), Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
        c.Look.Clamp(); SaveCharacter(c); return c;
    }

    public static string CleanName(string n, string fallback)
    {
        n = (n ?? "").Trim(); n = new string(n.Where(c => c >= ' ' && c != 0x7f && !char.IsControl(c)).ToArray()).Trim();
        if (n.Length > 20) n = n.Substring(0, 20).Trim(); return n == "" ? fallback : n;
    }

    // ───────────────────────── миниатюра ─────────────────────────
    /// <summary>Миниатюра 160×32: один пиксель — блок 4×4 клеток, цвета из палитры карты. Неисследованное скрыто полностью (в новом мире карта пуста).</summary>
    public static void WriteThumb(string id, World w, byte[] expl)
    {
        try
        {
            var pal = GD.Load<Texture2D>("res://Assets/UI/Map/map_palette.png")?.GetImage(); int cw = World.Wd / 4, ch = World.Ht / 4;
            var img = Image.CreateEmpty(cw, ch, false, Image.Format.Rgba8);
            for (int by = 0; by < ch; by++)
                for (int bx = 0; bx < cw; bx++)
                {
                    int idx = Game.MapIdxOf(w, bx * 4 + 2, by * 4 + 2); var c = pal != null ? pal.GetPixel(Math.Clamp(idx, 0, pal.GetWidth() - 1), 0) : new Color(.3f, .4f, .3f);
                    if (expl == null || expl[by * cw + bx] == 0) c = new Color(.03f, .04f, .08f);      // неисследованное скрыто полностью
                    img.SetPixel(bx, by, c);
                }
            Ensure(Path.GetDirectoryName(Abs(ThumbPath(id)))); img.SavePng(Abs(ThumbPath(id)));
        }
        catch (Exception e) { GD.PushWarning("thumb failed: " + e.Message); }
    }

    public static ImageTexture LoadThumb(string id)
    {
        try { var p = Abs(ThumbPath(id)); if (!File.Exists(p)) return null; var img = Image.LoadFromFile(p); return img == null ? null : ImageTexture.CreateFromImage(img); } catch { return null; }
    }

    // ───────────────────────── служебное ─────────────────────────
    static bool DeleteFiles(params string[] paths) { bool any = false; foreach (var p in paths) { var a = Abs(p); if (File.Exists(a)) { File.Delete(a); any = true; } } return any; }

    static IEnumerable<string> Files(string dir)
    {
        var d = Abs(dir); if (!Directory.Exists(d)) yield break;
        foreach (var f in Directory.GetFiles(d, "*.json")) yield return f;
    }

    static DateTime LastWrite(string p) { try { return File.GetLastWriteTimeUtc(Abs(p)); } catch { return DateTime.MinValue; } }
}
