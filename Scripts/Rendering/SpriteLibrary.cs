using Godot;
using System;
using System.Collections.Generic;

namespace Lamplighter;

/// <summary>Загрузка графики из PNG (res://Assets/Sprites/...) в атласы и текстуры игры. Если PNG нет — используется процедурный запасной вариант
/// (Tools/GraphicsGenerator), он запускается только при отсутствии файлов. Заменяйте PNG на свои — код менять не нужно.</summary>
public static class SpriteLibrary
{
    public const string Root = "res://Assets/Sprites/";
    public const string TileFolders = "Tilesets,Environment,Buildings";
    public const string IconFolder = "Items";

    /// <summary>Листы существ: id → (папка, геттер, сеттер). Один PNG = один лист кадров 16×16 по горизонтали.</summary>
    public static readonly (string id, string folder, Func<Texture2D> get, Action<Texture2D> set)[] Sheets =
    {
        ("player", "Player", () => Art.PlayerTex, t => Art.PlayerTex = t), ("villager", "Npc", () => Art.VillagerTex, t => Art.VillagerTex = t),
        ("trader", "Npc", () => Art.TraderTex, t => Art.TraderTex = t), ("traveler", "Npc", () => Art.TravelerTex, t => Art.TravelerTex = t),
        ("shade", "Enemies", () => Art.ShadeTex, t => Art.ShadeTex = t), ("wraith", "Enemies", () => Art.WraithTex, t => Art.WraithTex = t),
        ("spitter", "Enemies", () => Art.SpitterTex, t => Art.SpitterTex = t), ("bomber", "Enemies", () => Art.BomberTex, t => Art.BomberTex = t),
        ("eel", "Enemies", () => Art.EelTex, t => Art.EelTex = t), ("leech", "Enemies", () => Art.LeechTex, t => Art.LeechTex = t),
        ("beetle", "Enemies", () => Art.BeetleTex, t => Art.BeetleTex = t), ("depthwarden", "Enemies", () => Art.DeepTex, t => Art.DeepTex = t),
        ("imp", "Enemies", () => Art.ImpTex, t => Art.ImpTex = t), ("golem", "Enemies", () => Art.GolemTex, t => Art.GolemTex = t),
        ("spirit", "Enemies", () => Art.SpiritTex, t => Art.SpiritTex = t), ("ashwarden", "Enemies", () => Art.AshBossTex, t => Art.AshBossTex = t),
        ("icewolf", "Enemies", () => Art.WolfTex, t => Art.WolfTex = t), ("frostpriest", "Enemies", () => Art.PriestTex, t => Art.PriestTex = t),
        ("sentinel", "Enemies", () => Art.SentinelTex, t => Art.SentinelTex = t), ("winterwarden", "Enemies", () => Art.WinterTex, t => Art.WinterTex = t),
        ("mirage", "Enemies", () => Art.MirageTex, t => Art.MirageTex = t), ("weaver", "Enemies", () => Art.WeaverTex, t => Art.WeaverTex = t),
        ("stalker", "Enemies", () => Art.StalkerTex, t => Art.StalkerTex = t), ("source", "Enemies", () => Art.SourceTex, t => Art.SourceTex = t),
    };

    public static string SheetPath(string id, string folder) => $"{Root}{folder}/{id}.png";

    // ───────────────────────── чтение PNG ─────────────────────────
    public static Image LoadImage(string path)
    {
        try
        {
            if (ResourceLoader.Exists(path)) { var t = ResourceLoader.Load<Texture2D>(path); var im = t?.GetImage(); if (im != null) { if (im.IsCompressed()) im.Decompress(); im.Convert(Image.Format.Rgba8); return im; } }
            if (FileAccess.FileExists(path)) { var im = Image.LoadFromFile(ProjectSettings.GlobalizePath(path)); if (im != null) { im.Convert(Image.Format.Rgba8); return im; } }
        }
        catch (Exception e) { GD.PushWarning($"sprite load failed {path}: {e.Message}"); }
        return null;
    }

    /// <summary>Файлы тайлов: индекс (первые три цифры имени NNN_name.png) → путь.</summary>
    public static Dictionary<int, string> ScanIndexed(string folders, string prefixFilter = "")
    {
        var d = new Dictionary<int, string>();
        foreach (var f in folders.Split(','))
        {
            var dir = DirAccess.Open(Root + f); if (dir == null) continue;
            foreach (var raw in dir.GetFiles())
            {
                var name = raw.Replace(".import", "").Replace(".remap", ""); if (!name.EndsWith(".png") || name.Length < 4) continue;
                if (prefixFilter != "" && !name.StartsWith(prefixFilter)) continue;
                var digits = name.Substring(prefixFilter.Length, 3); if (!int.TryParse(digits, out int idx)) continue;
                d[idx] = $"{Root}{f}/{name}";
            }
        }
        return d;
    }

    // ───────────────────────── сборка ─────────────────────────
    public static void Load()
    {
        var cat = VisualCatalog.Get();
        var tiles = ScanIndexed(TileFolders, "");
        var icons = ScanIndexed(IconFolder, "icon_");
        bool entsOk = cat != null;
        if (entsOk) foreach (var s in Sheets) if (cat.Entity(s.id)?.Sheet == null) { entsOk = false; break; }
        bool tilesOk = tiles.Count >= Art.TileCount, iconsOk = icons.Count >= Art.IconCount;
        if (!(tilesOk && iconsOk && entsOk)) Art.BuildProcedural();        // запасной вариант: рисуем кодом только недостающее
        if (tiles.Count > 0) Art.TileTex = ImageTexture.CreateFromImage(Compose(tilesOk ? null : Art.ProcTiles, tiles, Art.TileCount));
        if (icons.Count > 0) Art.IconTex = ImageTexture.CreateFromImage(Compose(iconsOk ? null : Art.ProcIcons, icons, Art.IconCount));
        if (cat != null) foreach (var s in Sheets) { var sh = cat.Entity(s.id)?.Sheet; if (sh != null) s.set(sh); }
    }

    static Image Compose(Image baseImg, Dictionary<int, string> files, int count)
    {
        var img = baseImg ?? Image.CreateEmpty(16 * count, 16, false, Image.Format.Rgba8);
        foreach (var kv in files)
        {
            if (kv.Key < 0 || kv.Key >= count) continue;
            var t = LoadImage(kv.Value); if (t == null) continue;
            if (t.GetWidth() != 16 || t.GetHeight() != 16) t.Resize(16, 16, Image.Interpolation.Nearest);
            img.BlitRect(t, new Rect2I(0, 0, 16, 16), new Vector2I(kv.Key * 16, 0));
        }
        return img;
    }
}
