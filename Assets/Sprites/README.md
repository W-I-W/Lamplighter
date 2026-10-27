# Assets/Sprites — how to replace the placeholder graphics

All images are 16x16 pixel tiles (Nearest filter). Replace a PNG with your own pixel art **of the same size and name**, then run the game
(open the project in the editor once, or run `godot --headless --import`, so Godot re-imports the file). No C# changes are needed.

* `Tilesets/`, `Environment/`, `Buildings/` — one 16x16 PNG per map tile, named `NNN_Name.png`. The three-digit number is the tile index and must stay;
  the name part is free. Tiles with several variants (e.g. building levels) continue as `NNN_Name_vK.png`.
* `Items/icon_NNN_name.png` — 16x16 item/inventory icons (index 0-20 = items in game order, 23 = pickaxe).
* `Player/`, `Npc/`, `Enemies/` — one sheet per creature: frames side by side (32x16 = 2 idle frames, 16x16 each).
  Size, scale, offset, bobbing and shadow are set in `Resources/VisualConfigs/Entities/<id>.tres` (EntityVisualDef) — edit them in the Inspector.
* Visual events (particles, rings, sounds, shake) are in `Resources/VisualConfigs/Vfx/*.tres` (VfxDef). Shaders: `Assets/Shaders/`, materials: `Assets/Materials/`.
* The generator that made these placeholders is `Tools/GraphicsGenerator/` (run with `LAMP_STAGE=gengfx`). If a PNG is missing, the game falls back to the procedural version.
