"""Generates the window scenes -> Scenes/UI/Row.tscn, Tab.tscn, Chip.tscn, Window.tscn, Menu.tscn.
Run from the project root: python tools/GraphicsGenerator/make_ui_windows.py  (the .tscn files are normal Godot scenes: edit them in the editor afterwards).
All styling comes from Resources/UI/LanternTheme.tres (type variations RowNormal/RowSelected/TabNormal/TabActive/PanelDark/PlainLabel/BigLabel...)."""
import os

OUT = 'Scenes/UI'
FULL = 'layout_mode = 1\nanchors_preset = 15\nanchor_right = 1.0\nanchor_bottom = 1.0\ngrow_horizontal = 2\ngrow_vertical = 2\n'


def write(name, text):
    os.makedirs(OUT, exist_ok=True)
    open(os.path.join(OUT, name), 'w', encoding='utf-8', newline='\n').write(text)


# ── Row: одна строка списка/таблицы/кнопки меню ──
cols = ''
for i in range(6):
    first = i == 0
    cols += f'''
[node name="C{i}" type="Label" parent="Lines/Cols"]
unique_name_in_owner = true
{'' if first else 'visible = false' + chr(10)}layout_mode = 2
{'size_flags_horizontal = 3' + chr(10) if first else ''}theme_type_variation = &"PlainLabel"
clip_text = true
text_overrun_behavior = 3
'''
write('Row.tscn', f'''[gd_scene load_steps=2 format=3]

[ext_resource type="Script" path="res://Scripts/UI/WindowRow.cs" id="1"]

[node name="Row" type="PanelContainer"]
mouse_filter = 2
theme_type_variation = &"RowNormal"
script = ExtResource("1")

[node name="Lines" type="VBoxContainer" parent="."]
layout_mode = 2
mouse_filter = 2
theme_override_constants/separation = 0

[node name="Cols" type="HBoxContainer" parent="Lines"]
layout_mode = 2
mouse_filter = 2
theme_override_constants/separation = 4
{cols}
[node name="CR" type="Label" parent="Lines/Cols"]
unique_name_in_owner = true
layout_mode = 2
theme_type_variation = &"PlainLabel"
horizontal_alignment = 2

[node name="SubM" type="MarginContainer" parent="Lines"]
unique_name_in_owner = true
visible = false
layout_mode = 2
mouse_filter = 2
theme_override_constants/margin_left = 8

[node name="Sub" type="RichTextLabel" parent="Lines/SubM"]
unique_name_in_owner = true
layout_mode = 2
bbcode_enabled = true
fit_content = true
scroll_active = false
autowrap_mode = 3
''')

# ── Tab: вкладка ──
write('Tab.tscn', '''[gd_scene format=3]

[node name="Tab" type="PanelContainer"]
mouse_filter = 2
theme_type_variation = &"TabNormal"

[node name="Text" type="Label" parent="."]
unique_name_in_owner = true
layout_mode = 2
theme_type_variation = &"PlainLabel"
''')

# ── Chip: значок + число в шапке окна ──
write('Chip.tscn', '''[gd_scene format=3]

[node name="Chip" type="HBoxContainer"]
mouse_filter = 2
theme_override_constants/separation = 1

[node name="Icon" type="TextureRect" parent="."]
unique_name_in_owner = true
custom_minimum_size = Vector2(16, 16)
layout_mode = 2
mouse_filter = 2
expand_mode = 1
stretch_mode = 5

[node name="Text" type="Label" parent="."]
unique_name_in_owner = true
layout_mode = 2
theme_type_variation = &"PlainLabel"
size_flags_vertical = 4
''')

# ── Window: общее окно (рамка, заголовок, вкладки, список, панель подробностей, подсказка клавиш) ──
write('Window.tscn', f'''[gd_scene load_steps=2 format=3]

[ext_resource type="Script" path="res://Scripts/UI/WindowView.cs" id="1"]

[node name="Window" type="Control"]
visible = false
{FULL}mouse_filter = 2
script = ExtResource("1")

[node name="Dim" type="ColorRect" parent="."]
unique_name_in_owner = true
{FULL}mouse_filter = 2
color = Color(0, 0, 0.03, 0.55)

[node name="Frame" type="PanelContainer" parent="."]
unique_name_in_owner = true
layout_mode = 0
mouse_filter = 2

[node name="Box" type="VBoxContainer" parent="Frame"]
layout_mode = 2
mouse_filter = 2
theme_override_constants/separation = 2

[node name="Head" type="HBoxContainer" parent="Frame/Box"]
layout_mode = 2
mouse_filter = 2
theme_override_constants/separation = 4

[node name="Title" type="Label" parent="Frame/Box/Head"]
unique_name_in_owner = true
layout_mode = 2
size_flags_horizontal = 3
theme_type_variation = &"PlainLabel"

[node name="Chips" type="HBoxContainer" parent="Frame/Box/Head"]
unique_name_in_owner = true
layout_mode = 2
mouse_filter = 2
theme_override_constants/separation = 8

[node name="Rule" type="ColorRect" parent="Frame/Box"]
custom_minimum_size = Vector2(0, 2)
layout_mode = 2
mouse_filter = 2
color = Color(0.035, 0.047, 0.078, 1)

[node name="Tabs" type="HBoxContainer" parent="Frame/Box"]
unique_name_in_owner = true
layout_mode = 2
mouse_filter = 2
theme_override_constants/separation = 2

[node name="Body" type="HBoxContainer" parent="Frame/Box"]
unique_name_in_owner = true
layout_mode = 2
size_flags_vertical = 3
mouse_filter = 2
theme_override_constants/separation = 6

[node name="ListPanel" type="PanelContainer" parent="Frame/Box/Body"]
unique_name_in_owner = true
layout_mode = 2
size_flags_horizontal = 3
mouse_filter = 2
theme_type_variation = &"RowNormal"

[node name="ListBox" type="VBoxContainer" parent="Frame/Box/Body/ListPanel"]
unique_name_in_owner = true
layout_mode = 2
mouse_filter = 2
theme_override_constants/separation = 1

[node name="MapArea" type="Control" parent="Frame/Box/Body"]
unique_name_in_owner = true
visible = false
layout_mode = 2
size_flags_horizontal = 3
mouse_filter = 2

[node name="DetailPanel" type="PanelContainer" parent="Frame/Box/Body"]
unique_name_in_owner = true
layout_mode = 2
size_flags_horizontal = 3
mouse_filter = 2
theme_type_variation = &"PanelDark"

[node name="DetailBox" type="VBoxContainer" parent="Frame/Box/Body/DetailPanel"]
unique_name_in_owner = true
layout_mode = 2
mouse_filter = 2
theme_override_constants/separation = 1

[node name="Foot" type="Label" parent="Frame/Box"]
unique_name_in_owner = true
layout_mode = 2
theme_type_variation = &"DimLabel"
clip_text = true
text_overrun_behavior = 3
''')

# ── Menu: полноэкранный слой (главное меню / пауза / титры / финал) ──
write('Menu.tscn', f'''[gd_scene load_steps=3 format=3]

[ext_resource type="Script" path="res://Scripts/UI/MenuView.cs" id="1"]
[ext_resource type="Texture2D" path="res://Assets/Sprites/Vfx/disc.png" id="2"]

[node name="Menu" type="Control"]
visible = false
{FULL}mouse_filter = 2
script = ExtResource("1")

[node name="Dim" type="ColorRect" parent="."]
unique_name_in_owner = true
{FULL}mouse_filter = 2
color = Color(0.02, 0.03, 0.06, 0.9)

[node name="Flame" type="TextureRect" parent="."]
unique_name_in_owner = true
layout_mode = 1
anchors_preset = 5
anchor_left = 0.5
anchor_right = 0.5
offset_left = -3.0
offset_top = 24.0
offset_right = 3.0
offset_bottom = 30.0
mouse_filter = 2
texture = ExtResource("2")
expand_mode = 1

[node name="Box" type="VBoxContainer" parent="."]
unique_name_in_owner = true
{FULL}offset_top = 56.0
mouse_filter = 2
theme_override_constants/separation = 2

[node name="Title" type="Label" parent="Box"]
unique_name_in_owner = true
layout_mode = 2
theme_type_variation = &"BigLabel"
horizontal_alignment = 1

[node name="Sub" type="Label" parent="Box"]
unique_name_in_owner = true
layout_mode = 2
theme_type_variation = &"PlainLabel"
horizontal_alignment = 1

[node name="Gap" type="Control" parent="Box"]
custom_minimum_size = Vector2(0, 8)
layout_mode = 2
mouse_filter = 2

[node name="Center" type="CenterContainer" parent="Box"]
layout_mode = 2
mouse_filter = 2

[node name="Panel" type="PanelContainer" parent="Box/Center"]
unique_name_in_owner = true
custom_minimum_size = Vector2(276, 0)
layout_mode = 2
mouse_filter = 2

[node name="Items" type="VBoxContainer" parent="Box/Center/Panel"]
unique_name_in_owner = true
layout_mode = 2
mouse_filter = 2
theme_override_constants/separation = 2

[node name="Lines" type="Control" parent="."]
unique_name_in_owner = true
{FULL}mouse_filter = 2

[node name="Foot" type="Label" parent="."]
unique_name_in_owner = true
layout_mode = 1
anchors_preset = 7
anchor_left = 0.5
anchor_top = 1.0
anchor_right = 0.5
anchor_bottom = 1.0
offset_left = -160.0
offset_top = -14.0
offset_right = 160.0
offset_bottom = -3.0
grow_horizontal = 2
grow_vertical = 0
theme_type_variation = &"DimLabel"
horizontal_alignment = 1

[node name="Version" type="Label" parent="."]
unique_name_in_owner = true
layout_mode = 1
anchors_preset = 3
anchor_left = 1.0
anchor_top = 1.0
anchor_right = 1.0
anchor_bottom = 1.0
offset_left = -200.0
offset_top = -14.0
offset_right = -4.0
offset_bottom = -3.0
grow_horizontal = 0
grow_vertical = 0
theme_type_variation = &"DimLabel"
horizontal_alignment = 2
''')
print('window scenes ok')
