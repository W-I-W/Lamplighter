"""Upgrade window UI -> Assets/UI/Upgrades/*.png (8x8 state icons), Resources/UI/UpgradeTheme.tres (pixel card styles),
Scenes/UI/UpgradeEntry.tscn (one card) and Scenes/UI/UpgradeWindow.tscn (tabs, card list, details).
Run from the project root: python tools/GraphicsGenerator/make_upgrade_ui.py
Everything visual lives here and in the generated files; the game logic only fills the nodes. Replace any PNG/colour with your own to restyle the window."""
from PIL import Image

ICONS = {
    'state_check': ['........', '......#.', '.....##.', '#...##..', '##.##...', '.###....', '..#.....', '........'],
    'state_lock': ['..####..', '.#....#.', '.#....#.', '########', '########', '###..###', '###..###', '########'],
    'state_dot': ['........', '..##....'.replace('..##....', '...##...'), '..####..', '.######.', '.######.', '..####..', '...##...', '........'],
    'state_star': ['...##...', '...##...', '#######.'.replace('#######.', '########'), '.######.', '..####..', '..#..#..', '.#....#.', '........'],
}
for name, rows in ICONS.items():
    im = Image.new('RGBA', (8, 8), (0, 0, 0, 0))
    for y, r in enumerate(rows):
        for x, ch in enumerate(r):
            if ch == '#': im.putpixel((x, y), (255, 255, 255, 255))
    im.save(f'Assets/UI/Upgrades/{name}.png')

# colours: dark blue panels, thin 1px pixel frames
STYLES = {
    'CardNormal': ('#141c30', '#2c3858', 1),
    'CardAvail': ('#18223a', '#8a7430', 1),
    'CardMissing': ('#221a24', '#7a3b3b', 1),
    'CardLocked': ('#0f131f', '#1f2638', 1),
    'CardOwned': ('#13241f', '#4fae5a', 1),
    'CardSelected': ('#1f2944', '#ffd23f', 2),
}
def col(h):
    h = h.lstrip('#'); return 'Color(%.4f, %.4f, %.4f, 1)' % tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))


theme = ['[gd_resource type="Theme" load_steps=%d format=3]\n' % (len(STYLES) + 1)]
for name, (bg, border, w) in STYLES.items():
    theme.append(f'[sub_resource type="StyleBoxFlat" id="{name}"]\nbg_color = {col(bg)}\nborder_width_left = {w}\nborder_width_top = {w}\nborder_width_right = {w}\nborder_width_bottom = {w}\n'
                 f'border_color = {col(border)}\ncontent_margin_left = 5.0\ncontent_margin_top = 3.0\ncontent_margin_right = 5.0\ncontent_margin_bottom = 3.0\nanti_aliasing = false\n')
theme.append('[resource]')
for name in STYLES:
    theme.append(f'{name}/base_type = &"PanelContainer"\n{name}/styles/panel = SubResource("{name}")')
open('Resources/UI/UpgradeTheme.tres', 'w', encoding='utf-8', newline='\n').write('\n'.join(theme) + '\n')

ENTRY = '''[gd_scene load_steps=4 format=3]

[ext_resource type="Script" path="res://Scripts/UI/Upgrades/UpgradeEntryUI.cs" id="1"]
[ext_resource type="Theme" path="res://Resources/UI/UpgradeTheme.tres" id="2"]
[ext_resource type="Texture2D" path="res://Assets/UI/Upgrades/state_dot.png" id="3"]

[node name="UpgradeEntry" type="PanelContainer"]
custom_minimum_size = Vector2(0, 24)
theme = ExtResource("2")
theme_type_variation = &"CardNormal"
script = ExtResource("1")

[node name="H" type="HBoxContainer" parent="."]
layout_mode = 2
mouse_filter = 2
theme_override_constants/separation = 5

[node name="Icon" type="TextureRect" parent="H"]
unique_name_in_owner = true
custom_minimum_size = Vector2(16, 16)
layout_mode = 2
size_flags_vertical = 4
mouse_filter = 2
texture_filter = 1
expand_mode = 1
stretch_mode = 5

[node name="Name" type="Label" parent="H"]
unique_name_in_owner = true
layout_mode = 2
size_flags_horizontal = 3
size_flags_vertical = 4
mouse_filter = 2
text = "name"
clip_text = true
text_overrun_behavior = 3

[node name="State" type="TextureRect" parent="H"]
unique_name_in_owner = true
custom_minimum_size = Vector2(8, 8)
layout_mode = 2
size_flags_vertical = 4
mouse_filter = 2
texture_filter = 1
texture = ExtResource("3")
expand_mode = 1
stretch_mode = 5
'''
open('Scenes/UI/UpgradeEntry.tscn', 'w', encoding='utf-8', newline='\n').write(ENTRY)

WINDOW = '''[gd_scene load_steps=4 format=3]

[ext_resource type="Script" path="res://Scripts/UI/Upgrades/UpgradeWindowUI.cs" id="1"]
[ext_resource type="Theme" path="res://Resources/UI/LanternTheme.tres" id="2"]
[ext_resource type="Script" path="res://Scripts/UI/Upgrades/UpgradeDetailsUI.cs" id="3"]

[node name="UpgradeWindow" type="Control"]
visible = false
layout_mode = 3
anchors_preset = 15
anchor_right = 1.0
anchor_bottom = 1.0
mouse_filter = 2
theme = ExtResource("2")
script = ExtResource("1")

[node name="Dim" type="ColorRect" parent="."]
layout_mode = 1
anchors_preset = 15
anchor_right = 1.0
anchor_bottom = 1.0
mouse_filter = 2
color = Color(0.02, 0.03, 0.06, 0.62)

[node name="Panel" type="PanelContainer" parent="."]
unique_name_in_owner = true
layout_mode = 1
anchors_preset = 8
anchor_left = 0.5
anchor_top = 0.5
anchor_right = 0.5
anchor_bottom = 0.5
offset_left = -300.0
offset_top = -172.0
offset_right = 300.0
offset_bottom = 172.0
grow_horizontal = 2
grow_vertical = 2

[node name="V" type="VBoxContainer" parent="Panel"]
layout_mode = 2
theme_override_constants/separation = 4

[node name="Head" type="HBoxContainer" parent="Panel/V"]
layout_mode = 2

[node name="Title" type="Label" parent="Panel/V/Head"]
unique_name_in_owner = true
layout_mode = 2
theme_type_variation = &"GoldLabel"
text = "Улучшения"

[node name="Count" type="Label" parent="Panel/V/Head"]
unique_name_in_owner = true
layout_mode = 2
size_flags_horizontal = 3
theme_type_variation = &"DimLabel"
text = "0/0"
horizontal_alignment = 2

[node name="Tabs" type="HBoxContainer" parent="Panel/V"]
unique_name_in_owner = true
custom_minimum_size = Vector2(0, 22)
layout_mode = 2
theme_override_constants/separation = 2

[node name="Body" type="HBoxContainer" parent="Panel/V"]
layout_mode = 2
size_flags_vertical = 3
theme_override_constants/separation = 4

[node name="ListPanel" type="PanelContainer" parent="Panel/V/Body"]
custom_minimum_size = Vector2(262, 0)
layout_mode = 2
theme_type_variation = &"PanelDark"

[node name="Scroll" type="ScrollContainer" parent="Panel/V/Body/ListPanel"]
unique_name_in_owner = true
layout_mode = 2
horizontal_scroll_mode = 0

[node name="List" type="VBoxContainer" parent="Panel/V/Body/ListPanel/Scroll"]
unique_name_in_owner = true
layout_mode = 2
size_flags_horizontal = 3
theme_override_constants/separation = 2

[node name="Details" type="PanelContainer" parent="Panel/V/Body"]
unique_name_in_owner = true
layout_mode = 2
size_flags_horizontal = 3
theme_type_variation = &"PanelDark"
script = ExtResource("3")

[node name="D" type="VBoxContainer" parent="Panel/V/Body/Details"]
layout_mode = 2
theme_override_constants/separation = 4

[node name="Head" type="HBoxContainer" parent="Panel/V/Body/Details/D"]
layout_mode = 2
theme_override_constants/separation = 8

[node name="Icon" type="TextureRect" parent="Panel/V/Body/Details/D/Head"]
unique_name_in_owner = true
custom_minimum_size = Vector2(32, 32)
layout_mode = 2
size_flags_vertical = 0
texture_filter = 1
expand_mode = 1
stretch_mode = 5

[node name="Titles" type="VBoxContainer" parent="Panel/V/Body/Details/D/Head"]
layout_mode = 2
size_flags_horizontal = 3
theme_override_constants/separation = 2

[node name="Name" type="Label" parent="Panel/V/Body/Details/D/Head/Titles"]
unique_name_in_owner = true
layout_mode = 2
theme_type_variation = &"GoldLabel"
text = "name"
autowrap_mode = 3

[node name="State" type="Label" parent="Panel/V/Body/Details/D/Head/Titles"]
unique_name_in_owner = true
layout_mode = 2
text = "state"

[node name="Desc" type="RichTextLabel" parent="Panel/V/Body/Details/D"]
unique_name_in_owner = true
layout_mode = 2
bbcode_enabled = true
fit_content = true
scroll_active = false

[node name="ReqTitle" type="Label" parent="Panel/V/Body/Details/D"]
unique_name_in_owner = true
layout_mode = 2
theme_type_variation = &"DimLabel"
text = "Требование"

[node name="Req" type="RichTextLabel" parent="Panel/V/Body/Details/D"]
unique_name_in_owner = true
layout_mode = 2
bbcode_enabled = true
fit_content = true
scroll_active = false

[node name="CostTitle" type="Label" parent="Panel/V/Body/Details/D"]
unique_name_in_owner = true
layout_mode = 2
theme_type_variation = &"DimLabel"
text = "Стоимость"

[node name="Cost" type="RichTextLabel" parent="Panel/V/Body/Details/D"]
unique_name_in_owner = true
layout_mode = 2
bbcode_enabled = true
fit_content = true
scroll_active = false

[node name="Spacer" type="Control" parent="Panel/V/Body/Details/D"]
layout_mode = 2
size_flags_vertical = 3

[node name="Learn" type="Button" parent="Panel/V/Body/Details/D"]
unique_name_in_owner = true
custom_minimum_size = Vector2(0, 22)
layout_mode = 2
focus_mode = 0
text = "Изучить"

[node name="Keys" type="RichTextLabel" parent="Panel/V"]
unique_name_in_owner = true
layout_mode = 2
bbcode_enabled = true
fit_content = true
scroll_active = false
'''
open('Scenes/UI/UpgradeWindow.tscn', 'w', encoding='utf-8', newline='\n').write(WINDOW)
print('upgrade ui written')
