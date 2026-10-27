# Lanternreach — release notes for developers

Version: **1.0.0** (`Game.VersionStr` in `Scripts/UI/Game.Menu.cs`, also `application/file_version` in `export_presets.cfg`).

## Build and export (Windows)

```
dotnet build Lamplighter.csproj -c ExportRelease       # release compile check
godot --headless --path . --export-release "Windows Desktop" build/Lanternreach.exe
```

* The preset is in `export_presets.cfg`. `lang/*.json` and `audio/*` are included through `include_filter`.
* Output: `build/Lanternreach.exe` + `.pck` + `data_Lamplighter_windows_x86_64/` (keep them together). `build/` is git-ignored.
* The export was run on the dev machine with the Godot install that is used here; code signing is disabled.

## Automated checks

`powershell -File tools\run_checks.ps1` builds (Debug and ExportRelease) and runs scripted stages in the real engine:

| Stage (`LAMP_STAGE`) | What it verifies |
|---|---|
| `world` | 10 seeds: every ancient lantern / boss / POI reachable (with seals opened and Heart bridges lit); every blueprint is obtainable |
| `ice` | Ice Monastery reachability, ice enemies, slippery ice, Winter Warden phases and rewards |
| `heart` | Heart of Darkness riddles/traps/enemies, Source of Darkness (3 phases), 5th core, finale cinematic, credits |
| `end` | Rift Gate, waves, temporary relics, rewards/unlocks, legendary upgrades, decorations |
| `menu` | menus, save slots, atomic save + `save.bak` recovery, key remapping |
| `loc` (+`LAMP_LANG=en`) | every window/menu rendered in English; reports any untranslated Russian string |
| `perf` | stress scene (30 enemies, lamps, buildings) — fails below 60 fps |
| `final` / `reload` | chunk borders, production while chunks are unloaded, resource regrowth, death + save, no duplicate unique rewards, finished-save reload (no finale re-trigger, bosses stay dead) |

Migration of pre-stage-5 saves (world width 384 → 640): `LAMP_STAGE=makev4` writes a save, `.patch/crop4.py` crops it to the old format, `LAMP_LOAD=1` loads it.

## Saves

`user://save.json` (current), `user://save.bak` (last valid previous save), `user://slot1..3.json` (manual), `save_prev.json` (created by *New game* / loading a slot).
Writes are atomic (temp file → replace) and the previous file is copied to `save.bak` only if it parses. A damaged `save.json` falls back to `save.bak`.
Versions 2–4 load and migrate to 5 (new regions are generated from the seed; unlocked seals are re-applied from the beacon level).

## Steam

No Steamworks code, no AppID and no `steam_appid.txt` are shipped. Achievements are stored locally (`user://achievements.json`);
to forward them to Steam, set `Ach.Backend = id => /* SteamUserStats.SetAchievement(id) */;` after initialising your Steamworks wrapper
(e.g. GodotSteam) and map the 19 ids in `Achievements.cs` to the achievement API names configured in Steamworks.
Use your own AppID only in the release build pipeline — never commit a test AppID (480).

## Audio replacement

See `Assets/Audio/README.txt`. Music: `Assets/Audio/Music/{menu,forest,mines,ash,ice,heart,boss,finale}.ogg|mp3|wav`; SFX: `Assets/Audio/Sfx/NAME.ogg|wav`.
Without files the game synthesizes everything (placeholders).

## Localization

Source texts are Russian; `lang/en.json` maps them to English (exact strings and `{0}`-templates). Text is translated at draw time (`Loc.T`).
To add a language: copy `lang/en.json` to `lang/xx.json`, translate the values, add the code to the language toggle in `Scripts/UI/Game.Menu.cs`.
`.patch/extract_strings.py` lists all Russian literals in the code; the `loc` stage finds anything that still slips through.

## Known limits (be honest with players)

* Balance of the full 5-biome path was tuned analytically and with scripted runs; it has **not** been play-tested end-to-end by a human.
* Key remapping covers movement and main actions; digit/Z/X build keys and F5 are fixed. On-screen hints keep the default key names.
* Resolution/fullscreen changes are applied immediately but were not exercised on multiple monitors.

## Stage 6 notes (gameplay design pass)

* Test saves for four stages live in `testsaves/{early,mid,late,post}/save.json` (seed 4242). Use: `LAMP_AUTOTEST=<copy of the folder> LAMP_LOAD=1` + any stage, or `LAMP_STAGE=savesmoke`. Regenerate with `LAMP_STAGE=mksave LAMP_SAVE=<name>`. They never touch the player's `user://` saves.
* Diagnostics: Settings → "Diagnostics (local only)" or `LAMP_DIAG=1`. Writes `user://diagnostics.json` (first-event times, mining/dark time, deaths, ability counts, fps, nodes, chunks, memory). Nothing is sent anywhere; off by default.
* These are technical checks, not playtests. Questions about fun/pacing need real players: watch the first 30 minutes with diagnostics on and compare `first_events_secs` with the 5/10/20/30-minute targets.

## Project layout and graphics pipeline (refactor)

```
Assets/Sprites/{Tilesets,Environment,Buildings,Items,Player,Npc,Enemies}/  PNG art (16x16, Nearest) — replace in place, see Assets/Sprites/README.md
Assets/Shaders/{Darkness,Lighting}/   .gdshader + shared .gdshaderinc        Assets/Materials/   ShaderMaterial .tres
Assets/Audio/{Music,Sfx}/             optional replacement sounds
Resources/VisualConfigs/              VisualCatalog.tres, Entities/*.tres (EntityVisualDef), Vfx/*.tres (VfxDef)
Resources/Items/{Relics,Synergies}/   relic and synergy definitions
Scenes/Main.tscn
Scripts/Core | Gameplay | World | Combat | Building | Inventory | Rendering | Visuals | UI | Tests
Tools/GraphicsGenerator/              procedural art generators (fallback) + GraphicsExport (LAMP_STAGE=gengfx)
```

* Game logic never loads image files: `SpriteLibrary` (Scripts/Rendering) builds the tile/icon atlases from the PNGs and assigns creature sheets from `EntityVisualDef` resources. Procedural generation runs only if PNGs are missing.
* Drawing code lives in `Scripts/Visuals` (`Game.Visual.*.cs`) and `Scripts/Rendering` (light data for the darkness shader); effects are launched by name with `Fx("tile_break", pos, color)`.
* Regenerate placeholders (only needed if you change the procedural generators): `LAMP_STAGE=gengfx godot --path .` then `godot --headless --path . --import`.
* The `graphics` check in `tools/run_checks.ps1` verifies the PNG-loaded textures equal the procedural ones and that nothing is regenerated at startup.

## Interface (pixel-art UI)

- Theme and styles: `Resources/UI/LanternTheme.tres` (9-patch `StyleBoxTexture`, type variations: Slot, Keycap, Button, Tab, bars, labels); textures and the 5x7 bitmap font in `Assets/UI/` (`Fonts/lantern.fnt`). All UI textures use nearest filtering and integer scaling.
- Scenes (editable in the Godot editor): `Scenes/UI/Slot.tscn`, `GameHud.tscn`, `BuildMenu.tscn`, `InventoryWindow.tscn`. Logic: `Scripts/UI/GameHud.cs`, `BuildMenu.cs`, `InventoryWindow.cs`, `UiSlot.cs`.
- Other windows (equipment, journal, production, trade, upgrades, map, story, rift, menus) are still drawn in `Hud*.cs` but only through the shared theme/font helpers - not yet converted to scenes.
- Regenerate art: `python tools/GraphicsGenerator/make_ui_font.py`, `make_ui_art.py`, `make_ui_scenes.py`.
- UI scale: Settings -> "Масштаб интерфейса" (Auto, x1..x4). Logical grid is 640x360; Auto uses floor(window height / 360), so 1280x720 and 1920x1080 give identical layouts.
- Check: `LAMP_STAGE=ui` writes screenshots of every window (also part of `tools/run_checks.ps1`; it verifies no exceptions, not visual quality).

## No code-drawn graphics

- World effects: `Scripts/Visuals/Gfx.cs` places and tints the PNGs from `Assets/Sprites/Vfx/` (px, line, disc, arrow, diamond, crack_0..3). No shapes are drawn from code.
- Windows and menus are real scenes: `Scenes/UI/Window.tscn` (frame, title, tabs, list, detail pane, key hints, map area), `Row.tscn`, `Tab.tscn`, `Chip.tscn`, `Menu.tscn` (main menu, pause, credits, finale). Layout is done by containers; `Scripts/UI/Hud*.cs` only fills them with data. Regenerate or edit them: `python tools/GraphicsGenerator/make_ui_windows.py`, or in the Godot editor.
- Overlays that follow the world (markers, floating numbers, toasts, banners, tooltip, boss bar) are pooled Label/PanelContainer/ProgressBar/TextureRect nodes created by `HudNodes.cs` (still positioned from code because they track world positions).
- The world map is a `TileMapLayer` whose colours come from `Assets/UI/Map/map_palette.png` (`tools/GraphicsGenerator/make_map_palette.py`); `Game.MapIdx` picks the palette index per tile.
- Sprites are loaded only from PNG. The old procedural sprite generator was moved out of the build (`tools/GraphicsGenerator/*.cs` is excluded in `Lamplighter.csproj`); missing PNGs are reported as an error instead of being drawn from code. `LAMP_STAGE=texcheck` verifies every sprite sheet and effect primitive loads.

## Living world (stage 7)

- **Wind**: `Assets/Shaders/Vegetation/sway.gdshader` + `Assets/Materials/sway.tres` sway only the tiles listed in `Resources/VisualConfigs/VisualFx.tres` (trees, pines, dead trees, foliage); the top of a sprite moves most, the bottom stays; shifts are whole pixels. One shared material and one set of uniforms for every chunk. Axe/pickaxe hits shake the tile (`Game.TileShake`), grass bends away from the player. Rare ores pulse (`GlowTiles`).
- **Foliage**: visual-only `TileMapLayer` per chunk with tufts, tall grass, flowers and bushes (tiles 130-133, `tools/GraphicsGenerator/make_foliage.py`); never saved, no collision.
- **Water**: `Assets/Shaders/Water/water.gdshader` on the ground layer animates only the water tile (3-step pixel waves, sparkles).
- **Combat visuals** (`Scripts/Visuals/Game.Visual.EnemyAnim.cs`, `...WeaponFx.cs`): every enemy has a style (squash, lunge, shoot, caster, stomp, bomb, boss) with anticipation, attack and recovery poses derived from existing `Tele/Dash/Atk` fields (damage and timings unchanged), lunge trails, aim dots, caster particles. Hit feedback: additive white flash layer (`Assets/Shaders/Combat/flash_white.gdshader`), impact sparks, shake, pixel-dissolve deaths; normal/strong/boss profiles and crit scaling in `VisualFx.tres`. Player: sword sweep trail, arrow/orb trails, muzzle puffs, dash afterimages, light-wave rings.
- **Lanterns and buildings** (`Game.Visual.Ambient.cs`): flames/dust on lamps, ignition sequence (flash, wave, golden motes) for ancient lanterns and the Central Beacon, smoke/sparks/chips for working buildings.
- **Biomes**: ambient particle layer above the darkness (leaves, fireflies, pollen / drops / ash and embers / snow / dark motes), capped at 90 particles and spawned only around the camera.
- **Settings**: wind, particles, flashes, extra effects (0-150%) in Settings; screen shake already existed. `Settings.Particles` also scales `Game.Burst`.
- Not done: moving parts of machines (tiles are single sprites), heat-haze distortion, tree-fall tilt.
