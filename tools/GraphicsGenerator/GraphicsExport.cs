using Godot;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Lamplighter;

/// <summary>Однократный экспорт процедурной графики в PNG и ресурсы Godot. Запуск: godot --path . (переменная LAMP_STAGE=gengfx), затем godot --headless --import.
/// После экспорта игра берёт графику из Assets/Sprites; процедурные генераторы (Art*.cs в этой папке) нужны только как запасной вариант.</summary>
public static class GraphicsExport
{
    static readonly string[] ItemSlugs = { "wood", "stone", "copper_ore", "iron_ore", "light_shard", "copper", "iron", "aether", "relic", "silver_ore", "silver", "crystal", "obsidian", "ash_coal", "light_core", "ancient_crystal", "coins", "gear", "lens", "frost_crystal", "rift_shard" };
    static readonly HashSet<string> Ground = new() { "Dirt", "CaveFloor", "WaterG", "AshFloor", "Scorch", "HotAsh", "Snow", "IceFloor", "HeartFloor", "VoidG", "Deep", "Grass" };
    static readonly HashSet<string> Buildings = new() { "Furnace", "Lantern", "House", "Tent", "Warehouse", "Sawmill", "Mine", "Forge", "Drill", "Collector", "Wh4", "Saw4", "Mine4", "Forge4", "Stall", "Hub", "Refinery", "Cryo", "CrystalLantern", "CombatLantern", "Statue", "Fountain", "Banner", "RiftGate", "Machine", "MachineOn" };

    static string Abs(string res) => ProjectSettings.GlobalizePath(res);
    static void Dir(string res) => DirAccess.MakeDirRecursiveAbsolute(Abs(res));
    static void Text(string res, string text) { Dir(res.Substring(0, res.LastIndexOf('/'))); using var f = FileAccess.Open(res, FileAccess.ModeFlags.Write); f.StoreString(text); }

    public static void Run()
    {
        Art.BuildProcedural();
        // ── имена тайлов из констант Art ──
        var named = new SortedDictionary<int, string>();
        foreach (var fld in typeof(Art).GetFields(BindingFlags.Public | BindingFlags.Static))
            if (fld.IsLiteral && fld.FieldType == typeof(int) && fld.Name != "TileCount" && fld.Name != "IconCount") { int v = (int)fld.GetRawConstantValue(); if (v >= 0 && v < Art.TileCount && !named.ContainsKey(v)) named[v] = fld.Name; }
        named[0] = "Grass";
        int tiles = 0; string last = "Grass"; int sub = 0;
        for (int i = 0; i < Art.TileCount; i++)
        {
            string name; if (named.TryGetValue(i, out var n)) { name = n; last = n; sub = 0; } else name = $"{last}_v{++sub}";
            string folder = Ground.Contains(last) ? "Tilesets" : Buildings.Contains(last) ? "Buildings" : "Environment";
            var path = $"{SpriteLibrary.Root}{folder}/{i:D3}_{name}.png"; Dir($"{SpriteLibrary.Root}{folder}");
            Art.ProcTiles.GetRegion(new Rect2I(i * 16, 0, 16, 16)).SavePng(Abs(path)); tiles++;
        }
        // ── иконки ──
        int icons = 0;
        for (int i = 0; i < Art.IconCount; i++)
        {
            string name = i < ItemSlugs.Length ? ItemSlugs[i] : i == 23 ? "pickaxe" : "misc";
            var path = $"{SpriteLibrary.Root}{SpriteLibrary.IconFolder}/icon_{i:D3}_{name}.png"; Dir($"{SpriteLibrary.Root}{SpriteLibrary.IconFolder}");
            Art.ProcIcons.GetRegion(new Rect2I(i * 16, 0, 16, 16)).SavePng(Abs(path)); icons++;
        }
        // ── листы существ ──
        foreach (var s in SpriteLibrary.Sheets) { var p = SpriteLibrary.SheetPath(s.id, s.folder); Dir(p.Substring(0, p.LastIndexOf('/'))); s.get().GetImage().SavePng(Abs(p)); }
        // ── описания внешнего вида ──
        var entities = new List<(string id, string sheet, string folder, float scale, float lift, Vector2 off, float shadow)>
        {
            ("player", "player", "Player", 1, 0, new(-8, -11), 5), ("villager", "villager", "Npc", 1, 0, new(-8, -11), 4), ("trader", "trader", "Npc", 1, 0, new(-8, -11), 5), ("traveler", "traveler", "Npc", 1, 0, new(-8, -11), 5),
        };
        var kinds = new (string id, string sheet, float scale, float lift)[]
        {
            ("shade","shade",1,0), ("wraith","wraith",1,2), ("spitter","spitter",1,0), ("swarm","shade",.6f,0), ("bomber","bomber",1,0), ("warden","shade",2.6f,0), ("eel","eel",1,0), ("leech","leech",.8f,0), ("beetle","beetle",1.15f,0),
            ("depthwarden","depthwarden",2.8f,0), ("imp","imp",1,0), ("golem","golem",1.5f,0), ("spirit","spirit",1,2), ("ashwarden","ashwarden",2.8f,0), ("icewolf","icewolf",1,0), ("frostpriest","frostpriest",1,0),
            ("sentinel","sentinel",1.2f,0), ("winterwarden","winterwarden",2.8f,0), ("mirage","mirage",1,0), ("decoy","mirage",1,0), ("weaver","weaver",1,0), ("stalker","stalker",1,0), ("source","source",3.2f,0),
        };
        foreach (var k in kinds) entities.Add((k.id, k.sheet, "Enemies", k.scale, k.lift, new(-8, -10), 5 * k.scale));
        var ids = new List<string>();
        foreach (var e in entities)
        {
            var sb = new StringBuilder();
            sb.Append("[gd_resource type=\"Resource\" script_class=\"EntityVisualDef\" load_steps=3 format=3]\n\n");
            sb.Append("[ext_resource type=\"Script\" path=\"res://Scripts/Visuals/EntityVisualDef.cs\" id=\"1\"]\n");
            sb.Append($"[ext_resource type=\"Texture2D\" path=\"{SpriteLibrary.SheetPath(e.sheet, e.folder)}\" id=\"2\"]\n\n[resource]\nscript = ExtResource(\"1\")\n");
            sb.Append($"Id = \"{e.id}\"\nSheet = ExtResource(\"2\")\nFrames = 2\nFrameSize = 16\nFps = 4.0\nScale = {e.scale.ToString(System.Globalization.CultureInfo.InvariantCulture)}\n");
            sb.Append($"Offset = Vector2({e.off.X}, {e.off.Y})\nBob = 1.2\nLift = {e.lift.ToString(System.Globalization.CultureInfo.InvariantCulture)}\nShadowRadius = {e.shadow.ToString(System.Globalization.CultureInfo.InvariantCulture)}\n");
            Text($"res://Resources/VisualConfigs/Entities/{e.id}.tres", sb.ToString()); ids.Add(e.id);
        }
        // ── визуальные события ──
        var fx = new (string id, int n, float speed, float size, string tint, float ring, float ringT, string ringCol, string sfx, float db, float pitch, float shake, float flash)[]
        {
            ("mine_hit", 3, 40, 1.5f, "#ffffff", 0, .6f, "#ffe27a", "mine", -3, 1.1f, 0, 0),
            ("tile_break", 10, 70, 1.5f, "#ffffff", 0, .6f, "#ffe27a", "break", -2, 1f, 0, 0),
            ("rare_pickup", 8, 50, 2f, "#ffffff", 22, .6f, "#ffffff", "chest", -8, 1.3f, 0, 0),
            ("rare_pickup_big", 8, 50, 2f, "#ffffff", 40, .6f, "#ffffff", "chest", -8, .8f, 0, 0),
            ("level_up", 16, 90, 1.5f, "#ffe27a", 50, .8f, "#ffe27a", "levelup", 0, 1f, 0, 0),
            ("building_upgrade", 22, 90, 1.5f, "#ffe27a", 44, 1f, "#ffe27a", "upgrade", 0, 1f, 2, 0),
            ("enemy_hit", 6, 70, 1.5f, "#c070ff", 0, .6f, "#ffffff", "", 0, 1f, 0, 0),
            ("player_hurt", 8, 80, 1.5f, "#ff5050", 0, .6f, "#ffffff", "", 0, 1f, 0, 0),
        };
        var fxIds = new List<string>();
        foreach (var f in fx)
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture; var t = Color.FromHtml(f.tint); var rc = Color.FromHtml(f.ringCol);
            var sb = new StringBuilder();
            sb.Append("[gd_resource type=\"Resource\" script_class=\"VfxDef\" load_steps=2 format=3]\n\n[ext_resource type=\"Script\" path=\"res://Scripts/Visuals/VfxDef.cs\" id=\"1\"]\n\n[resource]\nscript = ExtResource(\"1\")\n");
            sb.Append($"Id = \"{f.id}\"\nParticles = {f.n}\nSpeed = {f.speed.ToString(ci)}\nParticleSize = {f.size.ToString(ci)}\nTint = Color({t.R.ToString(ci)}, {t.G.ToString(ci)}, {t.B.ToString(ci)}, 1)\n");
            sb.Append($"RingRadius = {f.ring.ToString(ci)}\nRingTime = {f.ringT.ToString(ci)}\nRingColor = Color({rc.R.ToString(ci)}, {rc.G.ToString(ci)}, {rc.B.ToString(ci)}, 1)\nSfx = \"{f.sfx}\"\nSfxDb = {f.db.ToString(ci)}\nSfxPitch = {f.pitch.ToString(ci)}\nShake = {f.shake.ToString(ci)}\nFlash = {f.flash.ToString(ci)}\n");
            Text($"res://Resources/VisualConfigs/Vfx/{f.id}.tres", sb.ToString()); fxIds.Add(f.id);
        }
        // ── каталог ──
        var cat = new StringBuilder();
        int load = 5 + ids.Count + fxIds.Count;
        cat.Append($"[gd_resource type=\"Resource\" script_class=\"VisualCatalog\" load_steps={load} format=3]\n\n");
        cat.Append("[ext_resource type=\"Script\" path=\"res://Scripts/Visuals/VisualCatalog.cs\" id=\"cat\"]\n[ext_resource type=\"Script\" path=\"res://Scripts/Visuals/EntityVisualDef.cs\" id=\"edef\"]\n[ext_resource type=\"Script\" path=\"res://Scripts/Visuals/VfxDef.cs\" id=\"vdef\"]\n");
        cat.Append("[ext_resource type=\"Material\" path=\"res://Assets/Materials/darkness.tres\" id=\"dark\"]\n[ext_resource type=\"Material\" path=\"res://Assets/Materials/glow.tres\" id=\"glow\"]\n");
        cat.Append("[ext_resource type=\"Resource\" path=\"res://Resources/VisualConfigs/HighlightStyle.tres\" id=\"hl\"]\n");
        for (int i = 0; i < ids.Count; i++) cat.Append($"[ext_resource type=\"Resource\" path=\"res://Resources/VisualConfigs/Entities/{ids[i]}.tres\" id=\"e{i}\"]\n");
        for (int i = 0; i < fxIds.Count; i++) cat.Append($"[ext_resource type=\"Resource\" path=\"res://Resources/VisualConfigs/Vfx/{fxIds[i]}.tres\" id=\"v{i}\"]\n");
        cat.Append("\n[resource]\nscript = ExtResource(\"cat\")\n");
        var el = new List<string>(); for (int i = 0; i < ids.Count; i++) el.Add($"ExtResource(\"e{i}\")");
        var vl = new List<string>(); for (int i = 0; i < fxIds.Count; i++) vl.Add($"ExtResource(\"v{i}\")");
        cat.Append($"Entities = Array[ExtResource(\"edef\")]([{string.Join(", ", el)}])\nEffects = Array[ExtResource(\"vdef\")]([{string.Join(", ", vl)}])\nDarkness = ExtResource(\"dark\")\nGlow = ExtResource(\"glow\")\nHighlight = ExtResource(\"hl\")\n");
        Text(VisualCatalog.Path, cat.ToString());
        Text($"{SpriteLibrary.Root}README.md", Readme());
        GD.Print($"[gengfx] exported tiles={tiles} icons={icons} sheets={SpriteLibrary.Sheets.Length} entityDefs={ids.Count} vfx={fxIds.Count}");
    }

    static string Readme() => @"# Assets/Sprites — how to replace the placeholder graphics

All images are 16x16 pixel tiles (Nearest filter). Replace a PNG with your own pixel art **of the same size and name**, then run the game
(open the project in the editor once, or run `godot --headless --import`, so Godot re-imports the file). No C# changes are needed.

* `Tilesets/`, `Environment/`, `Buildings/` — one 16x16 PNG per map tile, named `NNN_Name.png`. The three-digit number is the tile index and must stay;
  the name part is free. Tiles with several variants (e.g. building levels) continue as `NNN_Name_vK.png`.
* `Items/icon_NNN_name.png` — 16x16 item/inventory icons (index 0-20 = items in game order, 23 = pickaxe).
* `Player/`, `Npc/`, `Enemies/` — one sheet per creature: frames side by side (32x16 = 2 idle frames, 16x16 each).
  Size, scale, offset, bobbing and shadow are set in `Resources/VisualConfigs/Entities/<id>.tres` (EntityVisualDef) — edit them in the Inspector.
* Visual events (particles, rings, sounds, shake) are in `Resources/VisualConfigs/Vfx/*.tres` (VfxDef). Shaders: `Assets/Shaders/`, materials: `Assets/Materials/`.
* The generator that made these placeholders is `Tools/GraphicsGenerator/` (run with `LAMP_STAGE=gengfx`). If a PNG is missing, the game falls back to the procedural version.
";
}
