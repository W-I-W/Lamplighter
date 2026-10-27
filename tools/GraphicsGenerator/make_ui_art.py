"""Генератор пиксельных элементов интерфейса Lanternreach -> Assets/UI/*.png и Resources/UI/LanternTheme.tres.
Запуск из корня проекта: python Tools/GraphicsGenerator/make_ui_art.py
Палитра ограничена; все текстуры 9-patch, рисуются без сглаживания (Nearest)."""
import os, zlib, struct

def hexc(h, a=255): h = h.lstrip('#'); return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)
INK, DEEP, PANEL, PANEL_HI = hexc('#090c14'), hexc('#10151f'), hexc('#171f31'), hexc('#212c45')
LINE, LINE_HI = hexc('#33415e'), hexc('#52638a')
GOLD, GOLD_HI, GOLD_DK = hexc('#e8b84a'), hexc('#ffe08a'), hexc('#8f6120')
RED, RED_HI, RED_DK = hexc('#c8423f'), hexc('#f06a5e'), hexc('#7a2024')
GREEN, GREEN_HI, GREEN_DK = hexc('#5fbf4a'), hexc('#9be07a'), hexc('#2f6a2a')
BLUE, BLUE_HI, BLUE_DK = hexc('#4aa0e0'), hexc('#9fd8ff'), hexc('#25507a')
CLEAR = (0, 0, 0, 0)

def save_png(path, w, h, px):
    raw = b''.join(b'\x00' + bytes(sum((list(px[y][x]) for x in range(w)), [])) for y in range(h))
    def ch(t, d):
        c = struct.pack('>I', len(d)) + t + d
        return c + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    open(path, 'wb').write(b'\x89PNG\r\n\x1a\n' + ch(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0)) + ch(b'IDAT', zlib.compress(raw)) + ch(b'IEND', b''))

class Img:
    def __init__(s, w, h, fill=CLEAR): s.w, s.h = w, h; s.p = [[fill] * w for _ in range(h)]
    def set(s, x, y, c):
        if 0 <= x < s.w and 0 <= y < s.h: s.p[y][x] = c
    def rect(s, x, y, w, h, c):
        for j in range(y, y + h):
            for i in range(x, x + w): s.set(i, j, c)
    def frame(s, x, y, w, h, c):
        for i in range(x, x + w): s.set(i, y, c); s.set(i, y + h - 1, c)
        for j in range(y, y + h): s.set(x, j, c); s.set(x + w - 1, j, c)
    def save(s, name): save_png('Assets/UI/' + name, s.w, s.h, s.p)

def box(w, h, fill, border, hi=None, lo=None, outer=INK, corners=None, notch=True):
    """Пиксельная рамка: внешний тёмный контур со срезанными углами, цветная линия, подсветка сверху-слева, тень снизу-справа, уголки."""
    im = Img(w, h)
    im.rect(0, 0, w, h, outer)
    im.rect(1, 1, w - 2, h - 2, border)
    im.rect(2, 2, w - 4, h - 4, fill)
    if hi:
        for i in range(2, w - 2): im.set(i, 2, hi)
        for j in range(2, h - 2): im.set(2, j, hi)
    if lo:
        for i in range(2, w - 2): im.set(i, h - 3, lo)
        for j in range(2, h - 2): im.set(w - 3, j, lo)
    if notch:                                  # срезанные углы (пиксельное «скругление»)
        for (x, y) in ((0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1)): im.set(x, y, CLEAR)
    if corners:                                # золотые уголки
        c1, c2 = corners
        for (x, y, dx, dy) in ((1, 1, 1, 1), (w - 2, 1, -1, 1), (1, h - 2, 1, -1), (w - 2, h - 2, -1, -1)):
            im.set(x, y, c1); im.set(x + dx, y, c2); im.set(x, y + dy, c2)
    return im

# ── панели ──
box(16, 16, PANEL, LINE, PANEL_HI, DEEP, corners=(GOLD, GOLD_DK)).save('panel.png')
box(16, 16, DEEP, LINE, None, None, corners=(GOLD_DK, LINE)).save('panel_dark.png')
box(16, 16, PANEL, GOLD_DK, PANEL_HI, DEEP, corners=(GOLD_HI, GOLD)).save('panel_gold.png')
# ── слоты ──
box(20, 20, DEEP, LINE, None, PANEL, notch=False).save('slot.png')
box(20, 20, DEEP, GOLD, None, GOLD_DK, notch=False, corners=(GOLD_HI, GOLD)).save('slot_selected.png')
box(20, 20, hexc('#0b0f18'), hexc('#232c42'), None, None, notch=False).save('slot_disabled.png')
box(20, 20, hexc('#14231c'), GREEN_DK, None, hexc('#0e1a14'), notch=False).save('slot_ok.png')
# ── кнопки ──
box(12, 12, PANEL_HI, LINE_HI, hexc('#2c3a5c'), DEEP).save('button_normal.png')
box(12, 12, hexc('#2c3a5c'), GOLD_DK, hexc('#3a4a72'), DEEP).save('button_hover.png')
box(12, 12, DEEP, GOLD, None, hexc('#1b2438')).save('button_pressed.png')
box(12, 12, hexc('#141a2a'), hexc('#2a3550'), None, None).save('button_disabled.png')
box(12, 12, hexc('#3a3320'), GOLD, hexc('#5a4a24'), DEEP, corners=(GOLD_HI, GOLD)).save('button_selected.png')
# ── вкладки ──
box(12, 12, DEEP, LINE, None, None).save('tab_normal.png')
box(12, 12, PANEL_HI, GOLD, hexc('#2c3a5c'), None).save('tab_selected.png')
# ── клавиша-подсказка ──
box(10, 10, PANEL_HI, LINE_HI, hexc('#34456c'), DEEP, notch=True).save('keycap.png')
# ── полоски ──
def bar_bg():
    im = Img(8, 8); im.rect(0, 0, 8, 8, INK); im.rect(1, 1, 6, 6, DEEP); im.set(0, 0, CLEAR); im.set(7, 0, CLEAR); im.set(0, 7, CLEAR); im.set(7, 7, CLEAR); return im
bar_bg().save('bar_bg.png')
def bar_fill(c, hi, lo):
    im = Img(6, 6); im.rect(0, 0, 6, 6, c)
    for i in range(6): im.set(i, 0, hi); im.set(i, 5, lo)
    return im
bar_fill(RED, RED_HI, RED_DK).save('bar_hp.png'); bar_fill(GREEN, GREEN_HI, GREEN_DK).save('bar_xp.png')
bar_fill(GOLD, GOLD_HI, GOLD_DK).save('bar_light.png'); bar_fill(BLUE, BLUE_HI, BLUE_DK).save('bar_cd.png')
# ── разделитель и затемнение перезарядки ──
d = Img(8, 3); d.rect(0, 1, 8, 1, LINE); d.save('divider.png')
cd = Img(4, 4, (0, 0, 0, 150)); cd.save('cooldown.png')
# ── значки интерфейса 8x8 ──
def icon(name, rows, c=GOLD):
    im = Img(8, 8)
    for y, r in enumerate(rows):
        for x, ch in enumerate(r):
            if ch == '#': im.set(x, y, c)
            elif ch == '+': im.set(x, y, GOLD_HI)
    im.save(name)
icon('ui_arrow_right.png', ['..#.....', '..##....', '..###...', '..####..', '..###...', '..##....', '..#.....', '........'])
icon('ui_heart.png', ['.##..##.', '########', '########', '########', '.######.', '..####..', '...##...', '........'], RED_HI)
icon('ui_bolt.png', ['...##...', '..##....', '.##.....', '.#####..', '...##...', '..##....', '..#.....', '........'], GOLD_HI)
icon('ui_lantern.png', ['..####..', '.#++++#.', '.#+##+#.', '.#+##+#.', '.#++++#.', '..####..', '...##...', '........'], GOLD)
print('ui art ok')

# ── тема ──
def sb(id_, tex, m, extra=''):
    return f'[sub_resource type="StyleBoxTexture" id="{id_}"]\ntexture = ExtResource("{tex}")\ntexture_margin_left = {m}\ntexture_margin_top = {m}\ntexture_margin_right = {m}\ntexture_margin_bottom = {m}\n{extra}'
tex = ['panel', 'panel_dark', 'panel_gold', 'slot', 'slot_selected', 'slot_disabled', 'slot_ok', 'button_normal', 'button_hover', 'button_pressed', 'button_disabled', 'button_selected',
       'tab_normal', 'tab_selected', 'keycap', 'bar_bg', 'bar_hp', 'bar_xp', 'bar_light', 'bar_cd', 'divider']
ids = {n: str(i + 2) for i, n in enumerate(tex)}
L = ['[gd_resource type="Theme" load_steps=60 format=3]', '', '[ext_resource type="FontFile" path="res://Assets/UI/Fonts/lantern.fnt" id="1"]']
for n in tex: L.append(f'[ext_resource type="Texture2D" path="res://Assets/UI/{n}.png" id="{ids[n]}"]')
L.append('')
M = {'panel': 6, 'panel_dark': 5, 'panel_gold': 6, 'slot': 5, 'slot_selected': 5, 'slot_disabled': 5, 'slot_ok': 5, 'button_normal': 4, 'button_hover': 4, 'button_pressed': 4, 'button_disabled': 4,
     'button_selected': 4, 'tab_normal': 4, 'tab_selected': 4, 'keycap': 3, 'bar_bg': 2, 'bar_hp': 1, 'bar_xp': 1, 'bar_light': 1, 'bar_cd': 1, 'divider': 0}
for n in tex:
    if n == 'divider': continue
    L.append(sb('sb_' + n, ids[n], M[n]) )
L.append('')
L.append('[resource]')
L.append('default_font = ExtResource("1")\ndefault_font_size = 9')
L.append('''Label/colors/font_color = Color(0.847, 0.878, 0.941, 1)
Label/colors/font_shadow_color = Color(0.035, 0.047, 0.078, 1)
Label/constants/shadow_offset_x = 1
Label/constants/shadow_offset_y = 1
Label/constants/line_spacing = 1
Panel/styles/panel = SubResource("sb_panel")
PanelContainer/styles/panel = SubResource("sb_panel")
PanelDark/base_type = &"PanelContainer"
PanelDark/styles/panel = SubResource("sb_panel_dark")
PanelGold/base_type = &"PanelContainer"
PanelGold/styles/panel = SubResource("sb_panel_gold")
Slot/base_type = &"PanelContainer"
Slot/styles/panel = SubResource("sb_slot")
SlotSelected/base_type = &"PanelContainer"
SlotSelected/styles/panel = SubResource("sb_slot_selected")
SlotDisabled/base_type = &"PanelContainer"
SlotDisabled/styles/panel = SubResource("sb_slot_disabled")
SlotOk/base_type = &"PanelContainer"
SlotOk/styles/panel = SubResource("sb_slot_ok")
Keycap/base_type = &"PanelContainer"
Keycap/styles/panel = SubResource("sb_keycap")
Button/styles/normal = SubResource("sb_button_normal")
Button/styles/hover = SubResource("sb_button_hover")
Button/styles/pressed = SubResource("sb_button_pressed")
Button/styles/disabled = SubResource("sb_button_disabled")
Button/styles/focus = SubResource("sb_button_selected")
Button/colors/font_color = Color(0.847, 0.878, 0.941, 1)
Button/colors/font_hover_color = Color(1, 0.878, 0.541, 1)
Button/colors/font_pressed_color = Color(1, 0.878, 0.541, 1)
Button/colors/font_disabled_color = Color(0.4, 0.44, 0.52, 1)
ButtonSelected/base_type = &"Button"
ButtonSelected/styles/normal = SubResource("sb_button_selected")
ButtonSelected/styles/hover = SubResource("sb_button_selected")
ButtonSelected/styles/pressed = SubResource("sb_button_selected")
Tab/base_type = &"Button"
Tab/styles/normal = SubResource("sb_tab_normal")
Tab/styles/hover = SubResource("sb_button_hover")
Tab/styles/pressed = SubResource("sb_tab_selected")
TabSelected/base_type = &"Button"
TabSelected/styles/normal = SubResource("sb_tab_selected")
TabSelected/styles/hover = SubResource("sb_tab_selected")
TabSelected/styles/pressed = SubResource("sb_tab_selected")
TabSelected/colors/font_color = Color(1, 0.878, 0.541, 1)
ProgressBar/styles/background = SubResource("sb_bar_bg")
ProgressBar/styles/fill = SubResource("sb_bar_xp")
ProgressBar/constants/outline_size = 0
HpBar/base_type = &"ProgressBar"
HpBar/styles/background = SubResource("sb_bar_bg")
HpBar/styles/fill = SubResource("sb_bar_hp")
XpBar/base_type = &"ProgressBar"
XpBar/styles/background = SubResource("sb_bar_bg")
XpBar/styles/fill = SubResource("sb_bar_xp")
LightBar/base_type = &"ProgressBar"
LightBar/styles/background = SubResource("sb_bar_bg")
LightBar/styles/fill = SubResource("sb_bar_light")
CdBar/base_type = &"ProgressBar"
CdBar/styles/background = SubResource("sb_bar_bg")
CdBar/styles/fill = SubResource("sb_bar_cd")
GoldLabel/base_type = &"Label"
GoldLabel/colors/font_color = Color(1, 0.878, 0.541, 1)
CountLabel/base_type = &"Label"
CountLabel/colors/font_outline_color = Color(0.043, 0.059, 0.102, 1)
CountLabel/constants/outline_size = 1
DimLabel/base_type = &"Label"
DimLabel/colors/font_color = Color(0.541, 0.588, 0.69, 1)
WarnLabel/base_type = &"Label"
WarnLabel/colors/font_color = Color(1, 0.7, 0.42, 1)
GoodLabel/base_type = &"Label"
GoodLabel/colors/font_color = Color(0.608, 0.878, 0.478, 1)
HSeparator/constants/separation = 3
BtnNormal/base_type = &"PanelContainer"
BtnNormal/styles/panel = SubResource("sb_button_normal")
BtnSelected/base_type = &"PanelContainer"
BtnSelected/styles/panel = SubResource("sb_button_selected")
BtnDisabled/base_type = &"PanelContainer"
BtnDisabled/styles/panel = SubResource("sb_button_disabled")
TabNormal/base_type = &"PanelContainer"
TabNormal/styles/panel = SubResource("sb_tab_normal")
TabActive/base_type = &"PanelContainer"
TabActive/styles/panel = SubResource("sb_tab_selected")
PlainLabel/base_type = &"Label"
PlainLabel/colors/font_color = Color(1, 1, 1, 1)
BigLabel/base_type = &"Label"
BigLabel/colors/font_color = Color(1, 1, 1, 1)
BigLabel/constants/shadow_offset_x = 2
BigLabel/constants/shadow_offset_y = 2
BigLabel/font_sizes/font_size = 18''')
L.append('Lantern/colors/gold = Color(0.91, 0.722, 0.29, 1)\nLantern/colors/gold_hi = Color(1, 0.878, 0.541, 1)\nLantern/colors/text = Color(0.847, 0.878, 0.941, 1)\nLantern/colors/dim = Color(0.541, 0.588, 0.69, 1)\nLantern/colors/warn = Color(1, 0.7, 0.42, 1)\nLantern/colors/good = Color(0.608, 0.878, 0.478, 1)\nLantern/colors/bad = Color(0.94, 0.42, 0.37, 1)\nLantern/colors/ink = Color(0.035, 0.047, 0.078, 1)')
# sub_resources must precede [resource]: reorder
text = '\n'.join(L)
head, res = text.split('\n[resource]\n')
os.makedirs('Resources/UI', exist_ok=True)
open('Resources/UI/LanternTheme.tres', 'w', encoding='utf-8', newline='\n').write(head + '\n\n[resource]\n' + res + '\n')
print('theme ok')

# ── значки способностей 12x12 ──
def icon12(name, rows, c=GOLD_HI):
    im = Img(12, 12)
    for y, r in enumerate(rows):
        for x, ch in enumerate(r):
            if ch == '#': im.set(x, y, c)
    for y in range(12):                       # тёмный контур-тень снизу-справа
        for x in range(12):
            if im.p[y][x] == CLEAR and x > 0 and y > 0 and im.p[y - 1][x - 1] == c: im.p[y][x] = INK
    im.save(name)
icon12('ability_dash.png', ['............', '..#.....#...', '..##....##..', '...##....##.', '....##....##', '.....##....#', '....##....##', '...##....##.', '..##....##..', '..#.....#...', '............', '............'])
icon12('ability_spin.png', ['....####....', '..##....##..', '.#........#.', '.#.......###', '#.........#.', '#.........#.', '#.........#.', '#.........#.', '.#........#.', '.#........#.', '..##....##..', '....####....'])
icon12('ability_flare.png', ['.....##.....', '.....##.....', '.....##.....', '..#..##..#..', '...#.##.#...', '##########..', '...#.##.#...', '..#..##..#..', '.....##.....', '.....##.....', '.....##.....', '............'])
icon12('ability_pulse.png', ['...######...', '..#......#..', '.#..####..#.', '#..#....#..#', '#.#..##..#.#', '#.#..##..#.#', '#..#....#..#', '.#..####..#.', '..#......#..', '...######...', '............', '............'])
icon12('ability_beam.png', ['............', '.......#....', '......##....', '.####.###...', '#########...', '#########...', '.####.###...', '......##....', '.......#....', '............', '............', '............'])
print('ability icons ok')
