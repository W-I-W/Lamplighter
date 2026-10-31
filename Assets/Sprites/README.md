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

## Effect primitives (`Vfx/`)

Nothing in the world is drawn from shapes in code any more: bars, rings, beams, particles, markers and the block-crack overlay are built from these PNGs (white masks that the game tints):
`px.png` (rectangles, particles, bar fills), `line.png` (beams, lines, arcs), `disc.png` (projectiles, shadows, areas), `arrow.png`, `diamond.png` / `diamond_line.png` (markers, relic gems).
Replace any of them with your own art of the same size. Regenerate the defaults with `python tools/GraphicsGenerator/make_vfx_prims.py`.

## Terrain transitions (drawn by code)

Visual-only blending between ground materials (grass, dirt, cave floor, ash, scorch, ice, snow, heart floor; water and hot ash only receive a narrow shore). Nothing here touches collisions, resources or world data.

* There are **no transition PNGs to maintain**: `Scripts/Rendering/TerrainBlendArt.cs` draws the autotile edges, dithering, scattered specks and the decor sheet at game start from the ground tiles in `Tilesets/` (000-003 grass, 004 dirt, 040-042 cave, 044-046 ash, 047 scorch, 101-103 snow, 104-105 ice, 106-108 heart). Redraw those PNGs and the transitions follow.
* Rules and tile selection: `Scripts/Rendering/TerrainBlend.cs` (pure functions of world Seed + coordinates, so chunk load order never matters); chunk layers (X1/X2 transitions, D decor): `Scripts/Rendering/Game.Blend.cs`.
* Tuning in the Inspector: `Resources/VisualConfigs/TerrainBlend.tres` (blend width, width jitter, dither intensity, roughness, scatter and decor density, seed salt, on/off). Check: `LAMP_STAGE=blend`.
