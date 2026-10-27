"""Генератор исходных сцен интерфейса (.tscn). После генерации сцены правятся в редакторе Godot вручную; повторный запуск перезапишет правки!
Запуск из корня проекта: python Tools/GraphicsGenerator/make_ui_scenes.py"""
import os

THEME = '[ext_resource type="Theme" path="res://Resources/UI/LanternTheme.tres" id="theme"]'

def w(path, text):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    open(path, 'w', encoding='utf-8', newline='\n').write(text)

# ───────────── Slot ─────────────
w('Scenes/UI/Slot.tscn', '''[gd_scene load_steps=2 format=3]

[ext_resource type="Script" path="res://Scripts/UI/UiSlot.cs" id="1"]

[node name="UiSlot" type="PanelContainer"]
custom_minimum_size = Vector2(24, 24)
theme_type_variation = &"Slot"
script = ExtResource("1")

[node name="Overlay" type="Control" parent="."]
layout_mode = 2
mouse_filter = 2

[node name="Icon" type="TextureRect" parent="Overlay"]
unique_name_in_owner = true
layout_mode = 1
anchors_preset = 8
anchor_left = 0.5
anchor_top = 0.5
anchor_right = 0.5
anchor_bottom = 0.5
offset_left = -8.0
offset_top = -8.0
offset_right = 8.0
offset_bottom = 8.0
mouse_filter = 2
expand_mode = 1

[node name="Letter" type="Label" parent="Overlay"]
unique_name_in_owner = true
layout_mode = 1
anchors_preset = 8
anchor_left = 0.5
anchor_top = 0.5
anchor_right = 0.5
anchor_bottom = 0.5
offset_left = -8.0
offset_top = -5.0
offset_right = 8.0
offset_bottom = 5.0
theme_type_variation = &"GoldLabel"
horizontal_alignment = 1
vertical_alignment = 1

[node name="Cooldown" type="ColorRect" parent="Overlay"]
unique_name_in_owner = true
visible = false
layout_mode = 1
anchors_preset = 15
anchor_right = 1.0
anchor_bottom = 1.0
mouse_filter = 2
color = Color(0, 0, 0, 0.6)

[node name="Count" type="Label" parent="Overlay"]
unique_name_in_owner = true
layout_mode = 1
anchors_preset = 3
anchor_left = 1.0
anchor_top = 1.0
anchor_right = 1.0
anchor_bottom = 1.0
offset_left = -22.0
offset_top = -10.0
offset_right = 1.0
offset_bottom = 1.0
grow_horizontal = 0
grow_vertical = 0
theme_type_variation = &"CountLabel"
horizontal_alignment = 2

[node name="Key" type="Label" parent="Overlay"]
unique_name_in_owner = true
layout_mode = 1
offset_left = 0.0
offset_top = -3.0
offset_right = 14.0
offset_bottom = 8.0
theme_type_variation = &"GoldLabel"
''')

# ───────────── GameHud ─────────────
slots = ''.join(f'\n[node name="Slot{i}" parent="Bottom/Row/Hotbar/Slots" instance=ExtResource("slot")]\nlayout_mode = 2\n' for i in range(8))
abil = ''.join(f'\n[node name="Ability{i}" parent="Abilities" instance=ExtResource("slot")]\nlayout_mode = 2\n' for i in range(5))
chips = ''.join(f'''
[node name="Chip{i}" type="HBoxContainer" parent="TopRight/World/V/Res"]
unique_name_in_owner = true
layout_mode = 2
theme_override_constants/separation = 1

[node name="Icon" type="TextureRect" parent="TopRight/World/V/Res/Chip{i}"]
layout_mode = 2
custom_minimum_size = Vector2(12, 12)
expand_mode = 1
stretch_mode = 5

[node name="Label" type="Label" parent="TopRight/World/V/Res/Chip{i}"]
layout_mode = 2
''' for i in range(5))

w('Scenes/UI/GameHud.tscn', f'''[gd_scene load_steps=6 format=3]

[ext_resource type="Script" path="res://Scripts/UI/GameHud.cs" id="1"]
{THEME}
[ext_resource type="PackedScene" path="res://Scenes/UI/Slot.tscn" id="slot"]
[ext_resource type="Texture2D" path="res://Assets/UI/ui_heart.png" id="heart"]
[ext_resource type="Texture2D" path="res://Assets/UI/ui_lantern.png" id="lantern"]

[node name="GameHud" type="Control"]
layout_mode = 3
anchors_preset = 15
anchor_right = 1.0
anchor_bottom = 1.0
mouse_filter = 2
theme = ExtResource("theme")
script = ExtResource("1")

[node name="TopLeft" type="VBoxContainer" parent="."]
layout_mode = 1
offset_left = 4.0
offset_top = 4.0
offset_right = 176.0
offset_bottom = 60.0
mouse_filter = 2
theme_override_constants/separation = 2

[node name="Status" type="PanelContainer" parent="TopLeft"]
unique_name_in_owner = true
layout_mode = 2

[node name="H" type="HBoxContainer" parent="TopLeft/Status"]
layout_mode = 2
theme_override_constants/separation = 4

[node name="LvBox" type="PanelContainer" parent="TopLeft/Status/H"]
custom_minimum_size = Vector2(28, 0)
layout_mode = 2
size_flags_vertical = 4
theme_type_variation = &"Keycap"

[node name="LvM" type="MarginContainer" parent="TopLeft/Status/H/LvBox"]
layout_mode = 2
theme_override_constants/margin_top = 3

[node name="LvH" type="HBoxContainer" parent="TopLeft/Status/H/LvBox/LvM"]
layout_mode = 2
alignment = 1
theme_override_constants/separation = 2

[node name="LvCap" type="Label" parent="TopLeft/Status/H/LvBox/LvM/LvH"]
unique_name_in_owner = true
layout_mode = 2
theme_type_variation = &"DimLabel"
text = "LV"
vertical_alignment = 1
size_flags_vertical = 4

[node name="Level" type="Label" parent="TopLeft/Status/H/LvBox/LvM/LvH"]
unique_name_in_owner = true
layout_mode = 2
theme_type_variation = &"GoldLabel"
text = "3"
vertical_alignment = 1
size_flags_vertical = 4

[node name="V" type="VBoxContainer" parent="TopLeft/Status/H"]
layout_mode = 2
size_flags_horizontal = 3
theme_override_constants/separation = 1

[node name="HpRow" type="HBoxContainer" parent="TopLeft/Status/H/V"]
layout_mode = 2
theme_override_constants/separation = 2

[node name="Heart" type="TextureRect" parent="TopLeft/Status/H/V/HpRow"]
layout_mode = 2
custom_minimum_size = Vector2(8, 8)
texture = ExtResource("heart")
expand_mode = 1
stretch_mode = 5

[node name="HpBar" type="ProgressBar" parent="TopLeft/Status/H/V/HpRow"]
unique_name_in_owner = true
layout_mode = 2
size_flags_horizontal = 3
size_flags_vertical = 4
custom_minimum_size = Vector2(0, 8)
theme_type_variation = &"HpBar"
max_value = 1.0
step = 0.001
show_percentage = false

[node name="HpLabel" type="Label" parent="TopLeft/Status/H/V/HpRow"]
unique_name_in_owner = true
layout_mode = 2
text = "60/60"

[node name="XpRow" type="HBoxContainer" parent="TopLeft/Status/H/V"]
layout_mode = 2
theme_override_constants/separation = 2

[node name="XpBar" type="ProgressBar" parent="TopLeft/Status/H/V/XpRow"]
unique_name_in_owner = true
layout_mode = 2
size_flags_horizontal = 3
size_flags_vertical = 4
custom_minimum_size = Vector2(0, 4)
theme_type_variation = &"XpBar"
max_value = 1.0
step = 0.001
show_percentage = false

[node name="Goal" type="PanelContainer" parent="TopLeft"]
unique_name_in_owner = true
layout_mode = 2

[node name="V" type="VBoxContainer" parent="TopLeft/Goal"]
layout_mode = 2
theme_override_constants/separation = 1

[node name="Title" type="RichTextLabel" parent="TopLeft/Goal/V"]
unique_name_in_owner = true
layout_mode = 2
bbcode_enabled = true
fit_content = true
scroll_active = false
autowrap_mode = 3
custom_minimum_size = Vector2(160, 0)

[node name="Needs" type="RichTextLabel" parent="TopLeft/Goal/V"]
unique_name_in_owner = true
layout_mode = 2
bbcode_enabled = true
fit_content = true
scroll_active = false
autowrap_mode = 3
custom_minimum_size = Vector2(160, 0)

[node name="Bar" type="ProgressBar" parent="TopLeft/Goal/V"]
unique_name_in_owner = true
layout_mode = 2
custom_minimum_size = Vector2(0, 4)
theme_type_variation = &"LightBar"
max_value = 1.0
step = 0.001
show_percentage = false

[node name="More" type="RichTextLabel" parent="TopLeft/Goal/V"]
unique_name_in_owner = true
layout_mode = 2
bbcode_enabled = true
fit_content = true
scroll_active = false

[node name="TopRight" type="VBoxContainer" parent="."]
layout_mode = 1
anchors_preset = 1
anchor_left = 1.0
anchor_right = 1.0
offset_left = -122.0
offset_top = 4.0
offset_right = -4.0
offset_bottom = 60.0
grow_horizontal = 0
mouse_filter = 2

[node name="World" type="PanelContainer" parent="TopRight"]
unique_name_in_owner = true
layout_mode = 2

[node name="V" type="VBoxContainer" parent="TopRight/World"]
layout_mode = 2
theme_override_constants/separation = 1

[node name="Biome" type="Label" parent="TopRight/World/V"]
unique_name_in_owner = true
layout_mode = 2
text = "Biome"

[node name="LRow" type="HBoxContainer" parent="TopRight/World/V"]
layout_mode = 2
theme_override_constants/separation = 2

[node name="LIcon" type="TextureRect" parent="TopRight/World/V/LRow"]
layout_mode = 2
custom_minimum_size = Vector2(8, 8)
texture = ExtResource("lantern")
expand_mode = 1
stretch_mode = 5

[node name="LBar" type="ProgressBar" parent="TopRight/World/V/LRow"]
unique_name_in_owner = true
layout_mode = 2
size_flags_horizontal = 3
size_flags_vertical = 4
custom_minimum_size = Vector2(0, 6)
theme_type_variation = &"LightBar"
max_value = 1.0
step = 0.001
show_percentage = false

[node name="LLabel" type="Label" parent="TopRight/World/V/LRow"]
unique_name_in_owner = true
layout_mode = 2

[node name="Beacon" type="Label" parent="TopRight/World/V"]
unique_name_in_owner = true
layout_mode = 2
theme_type_variation = &"DimLabel"

[node name="Res" type="HBoxContainer" parent="TopRight/World/V"]
layout_mode = 2
theme_override_constants/separation = 4
{chips}
[node name="Bottom" type="VBoxContainer" parent="."]
layout_mode = 1
anchors_preset = 7
anchor_left = 0.5
anchor_top = 1.0
anchor_right = 0.5
anchor_bottom = 1.0
offset_left = -120.0
offset_top = -60.0
offset_right = 120.0
offset_bottom = -4.0
grow_horizontal = 2
grow_vertical = 0
mouse_filter = 2
alignment = 2

[node name="Hint" type="PanelContainer" parent="Bottom"]
unique_name_in_owner = true
layout_mode = 2
size_flags_horizontal = 4
theme_type_variation = &"PanelDark"

[node name="HintLabel" type="Label" parent="Bottom/Hint"]
unique_name_in_owner = true
layout_mode = 2
horizontal_alignment = 1

[node name="Row" type="HBoxContainer" parent="Bottom"]
layout_mode = 2
size_flags_horizontal = 4
theme_override_constants/separation = 3

[node name="Weapon" parent="Bottom/Row" instance=ExtResource("slot")]
unique_name_in_owner = true
layout_mode = 2

[node name="Hotbar" type="PanelContainer" parent="Bottom/Row"]
layout_mode = 2

[node name="Slots" type="HBoxContainer" parent="Bottom/Row/Hotbar"]
layout_mode = 2
theme_override_constants/separation = 1
{slots}
[node name="Abilities" type="HBoxContainer" parent="."]
unique_name_in_owner = true
layout_mode = 1
anchors_preset = 2
anchor_top = 1.0
anchor_bottom = 1.0
offset_left = 4.0
offset_top = -30.0
offset_right = 130.0
offset_bottom = -4.0
grow_vertical = 0
mouse_filter = 2
theme_override_constants/separation = 1
{abil}
[node name="Detail" type="PanelContainer" parent="."]
unique_name_in_owner = true
visible = false
layout_mode = 1
anchors_preset = 7
anchor_left = 0.5
anchor_top = 1.0
anchor_right = 0.5
anchor_bottom = 1.0
offset_left = -80.0
offset_top = -150.0
offset_right = 80.0
offset_bottom = -64.0
grow_horizontal = 2
grow_vertical = 0
mouse_filter = 2

[node name="V" type="VBoxContainer" parent="Detail"]
layout_mode = 2
theme_override_constants/separation = 1

[node name="DTitle" type="Label" parent="Detail/V"]
unique_name_in_owner = true
layout_mode = 2
theme_type_variation = &"GoldLabel"

[node name="DBody" type="Label" parent="Detail/V"]
unique_name_in_owner = true
layout_mode = 2
autowrap_mode = 3
custom_minimum_size = Vector2(152, 0)
''')
print('scenes ok')

# ───────────── BuildMenu ─────────────
tabs = ''.join(f'''
[node name="Tab{i}" type="Button" parent="Panel/V/Tabs"]
unique_name_in_owner = true
layout_mode = 2
theme_type_variation = &"Tab"
text = "tab"
focus_mode = 0
''' for i in range(4))
w('Scenes/UI/BuildMenu.tscn', f'''[gd_scene load_steps=4 format=3]

[ext_resource type="Script" path="res://Scripts/UI/BuildMenu.cs" id="1"]
{THEME}
[ext_resource type="PackedScene" path="res://Scenes/UI/Slot.tscn" id="slot"]

[node name="BuildMenu" type="Control"]
visible = false
layout_mode = 3
anchors_preset = 15
anchor_right = 1.0
anchor_bottom = 1.0
mouse_filter = 2
theme = ExtResource("theme")
script = ExtResource("1")

[node name="Panel" type="PanelContainer" parent="."]
unique_name_in_owner = true
layout_mode = 1
anchors_preset = 4
anchor_top = 0.5
anchor_bottom = 0.5
offset_left = 6.0
offset_top = -110.0
offset_right = 266.0
offset_bottom = 110.0
grow_vertical = 2

[node name="V" type="VBoxContainer" parent="Panel"]
layout_mode = 2
theme_override_constants/separation = 3

[node name="Head" type="HBoxContainer" parent="Panel/V"]
layout_mode = 2

[node name="Title" type="Label" parent="Panel/V/Head"]
layout_mode = 2
theme_type_variation = &"GoldLabel"
text = "Строительство"

[node name="Keys" type="RichTextLabel" parent="Panel/V/Head"]
layout_mode = 2
size_flags_horizontal = 3
bbcode_enabled = true
fit_content = true
scroll_active = false
text = "[right][color=#ffd23f][lb]Tab][/color] [color=#ffd23f][lb]Enter][/color] [color=#ffd23f][lb]Esc][/color][/right]"

[node name="Tabs" type="HBoxContainer" parent="Panel/V"]
layout_mode = 2
theme_override_constants/separation = 2
{tabs}
[node name="Body" type="HBoxContainer" parent="Panel/V"]
layout_mode = 2
theme_override_constants/separation = 6

[node name="Grid" type="GridContainer" parent="Panel/V/Body"]
unique_name_in_owner = true
layout_mode = 2
theme_override_constants/h_separation = 1
theme_override_constants/v_separation = 1
columns = 5

[node name="Info" type="PanelContainer" parent="Panel/V/Body"]
layout_mode = 2
size_flags_horizontal = 3
theme_type_variation = &"PanelDark"

[node name="V" type="VBoxContainer" parent="Panel/V/Body/Info"]
layout_mode = 2
theme_override_constants/separation = 2

[node name="Name" type="Label" parent="Panel/V/Body/Info/V"]
unique_name_in_owner = true
layout_mode = 2
theme_type_variation = &"GoldLabel"
autowrap_mode = 3
custom_minimum_size = Vector2(100, 0)

[node name="Preview" type="TextureRect" parent="Panel/V/Body/Info/V"]
unique_name_in_owner = true
layout_mode = 2
custom_minimum_size = Vector2(32, 32)
size_flags_horizontal = 0
expand_mode = 1
stretch_mode = 5

[node name="Hint" type="Label" parent="Panel/V/Body/Info/V"]
unique_name_in_owner = true
layout_mode = 2
theme_type_variation = &"DimLabel"
autowrap_mode = 3
custom_minimum_size = Vector2(100, 0)

[node name="Status" type="Label" parent="Panel/V/Body/Info/V"]
unique_name_in_owner = true
layout_mode = 2
autowrap_mode = 3
custom_minimum_size = Vector2(100, 0)

[node name="Cost" type="Label" parent="Panel/V/Body/Info/V"]
unique_name_in_owner = true
layout_mode = 2
autowrap_mode = 3
custom_minimum_size = Vector2(100, 0)

[node name="Place" type="Label" parent="Panel/V"]
unique_name_in_owner = true
layout_mode = 2
theme_type_variation = &"DimLabel"
text = "Выберите постройку и нажмите Enter, затем кликните или наведите курсор на свободную клетку"
autowrap_mode = 3
''')

# ───────────── Inventory ─────────────
w('Scenes/UI/InventoryWindow.tscn', f'''[gd_scene load_steps=4 format=3]

[ext_resource type="Script" path="res://Scripts/UI/InventoryWindow.cs" id="1"]
{THEME}
[ext_resource type="PackedScene" path="res://Scenes/UI/Slot.tscn" id="slot"]

[node name="InventoryWindow" type="Control"]
visible = false
layout_mode = 3
anchors_preset = 15
anchor_right = 1.0
anchor_bottom = 1.0
mouse_filter = 2
theme = ExtResource("theme")
script = ExtResource("1")

[node name="Panel" type="PanelContainer" parent="."]
unique_name_in_owner = true
layout_mode = 1
anchors_preset = 8
anchor_left = 0.5
anchor_top = 0.5
anchor_right = 0.5
anchor_bottom = 0.5
offset_left = -110.0
offset_top = -80.0
offset_right = 110.0
offset_bottom = 80.0
grow_horizontal = 2
grow_vertical = 2

[node name="V" type="VBoxContainer" parent="Panel"]
layout_mode = 2
theme_override_constants/separation = 3

[node name="Head" type="HBoxContainer" parent="Panel/V"]
layout_mode = 2

[node name="Title" type="Label" parent="Panel/V/Head"]
layout_mode = 2
theme_type_variation = &"GoldLabel"
text = "Инвентарь"

[node name="Keys" type="RichTextLabel" parent="Panel/V/Head"]
layout_mode = 2
size_flags_horizontal = 3
bbcode_enabled = true
fit_content = true
scroll_active = false
text = "[right][color=#ffd23f][lb]I][/color] / [color=#ffd23f][lb]Esc][/color] — закрыть[/right]"

[node name="Grid" type="GridContainer" parent="Panel/V"]
unique_name_in_owner = true
layout_mode = 2
size_flags_horizontal = 4
theme_override_constants/h_separation = 1
theme_override_constants/v_separation = 1
columns = 7

[node name="Info" type="PanelContainer" parent="Panel/V"]
layout_mode = 2
theme_type_variation = &"PanelDark"

[node name="V" type="VBoxContainer" parent="Panel/V/Info"]
layout_mode = 2

[node name="Name" type="Label" parent="Panel/V/Info/V"]
unique_name_in_owner = true
layout_mode = 2
theme_type_variation = &"GoldLabel"

[node name="Desc" type="Label" parent="Panel/V/Info/V"]
unique_name_in_owner = true
layout_mode = 2
theme_type_variation = &"DimLabel"
autowrap_mode = 3
custom_minimum_size = Vector2(200, 0)
''')
print('build/inventory scenes ok')
