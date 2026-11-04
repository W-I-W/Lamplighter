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
        int tileFiles = 0; foreach (var kv in tiles) tileFiles += kv.Value.Contains(PosesTag) ? 4 : 1;
        if (tileFiles < Art.TileCount || icons.Count < Art.IconCount || !entsOk) GD.PushError($"Не хватает PNG: тайлы {tileFiles}/{Art.TileCount}, иконки {icons.Count}/{Art.IconCount}, листы существ {(entsOk ? "ok" : "нет")} (см. Assets/Sprites/README.md)");
        TileFrames = new int[Art.TileCount]; IconFrames = new int[Art.IconCount];
        Art.TileTex = ImageTexture.CreateFromImage(Compose(tiles, Art.TileCount, TileFrames));
        Art.IconTex = ImageTexture.CreateFromImage(Compose(icons, Art.IconCount, IconFrames));
        if (cat != null) foreach (var s in Sheets) { var sh = cat.Entity(s.id)?.Sheet; if (sh != null) s.set(sh); }
    }

    /// <summary>Кадров анимации у тайла/иконки (1 — статичный). Лента кадров в PNG (N×16 по ширине) кладётся в атлас столбиком: кадр k — строка k под основным тайлом.</summary>
    public static int[] TileFrames = System.Array.Empty<int>(), IconFrames = System.Array.Empty<int>();
    public const int MaxFrames = 4;

    /// <summary>Файл NNN_имя_poses.png — лента из 4 поз покачивания (N×16): занимает 4 подряд идущих индекса NNN..NNN+3.</summary>
    public const string PosesTag = "_poses.png";

    static Image Compose(Dictionary<int, string> files, int count, int[] frames)
    {
        var imgs = new Dictionary<int, Image>(); var poses = new List<(int idx, Image im)>(); int rows = 1;
        foreach (var kv in files)
        {
            if (kv.Key < 0 || kv.Key >= count) continue;
            var t = LoadImage(kv.Value); if (t == null) continue;
            if (kv.Value.EndsWith(PosesTag)) { poses.Add((kv.Key, t)); continue; }
            int n = t.GetHeight() == 16 && t.GetWidth() % 16 == 0 ? Math.Clamp(t.GetWidth() / 16, 1, MaxFrames) : 1;
            if (n == 1 && (t.GetWidth() != 16 || t.GetHeight() != 16)) t.Resize(16, 16, Image.Interpolation.Nearest);
            imgs[kv.Key] = t; frames[kv.Key] = n; rows = Math.Max(rows, n);
        }
        var img = Image.CreateEmpty(16 * count, 16 * rows, false, Image.Format.Rgba8);
        foreach (var (idx, im) in poses) for (int k = 0; k < 4 && idx + k < count; k++) img.BlitRect(im, new Rect2I(k * 16, 0, 16, 16), new Vector2I((idx + k) * 16, 0));
        foreach (var kv in imgs) for (int k = 0; k < frames[kv.Key]; k++) img.BlitRect(kv.Value, new Rect2I(k * 16, 0, 16, 16), new Vector2I(kv.Key * 16, k * 16));
        return img;
    }
}
