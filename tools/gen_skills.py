"""Генератор данных дерева навыков: Resources/Skills/*.tres (SkillDef) и иконки Assets/UI/Skills/*.png (32x32, Pixel Art).
Запуск из корня проекта: python tools/gen_skills.py. Иконки рисуются программно здесь, в игре ничего не генерируется.
Данные (позиции, уровни, кривые стоимости, родители, формулы эффекта) — единственный источник для .tres; имена, описания и условия — в Scripts/Gameplay/Game.Tech.cs."""
import os, math
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ICON_DIR = os.path.join(ROOT, 'Assets', 'UI', 'Skills')
RES_DIR = os.path.join(ROOT, 'Resources', 'Skills')
os.makedirs(ICON_DIR, exist_ok=True); os.makedirs(RES_DIR, exist_ok=True)

OUT = (18, 16, 30, 255)
GOLD, GOLD2, GOLD3 = (255, 210, 63, 255), (255, 236, 150, 255), (190, 130, 30, 255)
CYAN, CYAN2, BLUE = (143, 240, 255, 255), (220, 250, 255, 255), (70, 150, 230, 255)
VIO, VIO2 = (170, 110, 255, 255), (220, 190, 255, 255)
RED, ORG, ORG2 = (230, 70, 50, 255), (255, 150, 40, 255), (255, 210, 90, 255)
SIL, SIL2, SIL3 = (200, 210, 225, 255), (240, 246, 255, 255), (120, 130, 150, 255)
BRN, BRN2 = (120, 80, 45, 255), (170, 120, 70, 255)
GRN = (120, 220, 110, 255)
BLK = (40, 36, 50, 255)


class Ic:
    def __init__(self):
        self.im = Image.new('RGBA', (32, 32), (0, 0, 0, 0)); self.d = ImageDraw.Draw(self.im)

    def px(self, x, y, c):
        if 0 <= x < 32 and 0 <= y < 32: self.im.putpixel((int(x), int(y)), c)

    def line(self, a, b, c, w=1): self.d.line([a, b], fill=c, width=w)
    def rect(self, a, b, c): self.d.rectangle([a, b], fill=c)
    def ell(self, a, b, c, o=None): self.d.ellipse([a, b], fill=c, outline=o)
    def poly(self, pts, c): self.d.polygon(pts, fill=c)
    def arc(self, box, s, e, c, w=1): self.d.arc(box, s, e, fill=c, width=w)

    def save(self, name):
        # тёмный контур вокруг силуэта
        src = self.im.copy(); out = self.im.copy(); W, H = src.size
        for y in range(H):
            for x in range(W):
                if src.getpixel((x, y))[3] == 0:
                    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                        nx, ny = x + dx, y + dy
                        if 0 <= nx < W and 0 <= ny < H and src.getpixel((nx, ny))[3] > 0: out.putpixel((x, y), OUT); break
        out.save(os.path.join(ICON_DIR, name + '.png'))


def rays(ic, cx, cy, r0, r1, n, c, off=0.0):
    for i in range(n):
        a = off + i * 2 * math.pi / n
        ic.line((cx + math.cos(a) * r0, cy + math.sin(a) * r0), (cx + math.cos(a) * r1, cy + math.sin(a) * r1), c)


def lens(ic, tier):
    ic.ell((7, 7), (24, 24), GOLD3); ic.ell((9, 9), (22, 22), GOLD); ic.ell((11, 11), (20, 20), CYAN2 if tier > 1 else GOLD2)
    ic.line((12, 13), (14, 12), (255, 255, 255, 255))
    if tier > 1: rays(ic, 15.5, 15.5, 11, 15, 8, GOLD2)
    if tier > 2: ic.arc((3, 3, 28, 28), 0, 360, GOLD2)
    ic.rect((14, 25), (17, 29), BRN2) if tier == 1 else ic.rect((14, 26), (17, 29), BRN2)


def lantern(ic, flame, glow=None, star=False, tall=False):
    ic.rect((14, 3), (17, 5), SIL3); ic.rect((11, 6), (20, 7), GOLD3)
    ic.rect((11, 8), (20, 22), (60, 50, 70, 255)); ic.rect((12, 9), (19, 21), glow or GOLD2)
    ic.rect((13, 12), (18, 20), flame)
    ic.rect((10, 23), (21, 25), GOLD3); ic.rect((13, 26), (18, 29), BRN2)
    if star:
        ic.rect((15, 13), (16, 19), (255, 255, 255, 255)); ic.rect((13, 15), (18, 16), (255, 255, 255, 255))


def eye(ic):
    ic.poly([(2, 16), (10, 8), (22, 8), (30, 16), (22, 24), (10, 24)], SIL2); ic.ell((10, 9), (21, 22), CYAN); ic.ell((13, 12), (18, 19), BLK); ic.px(14, 13, (255, 255, 255, 255)); ic.px(15, 13, (255, 255, 255, 255))
    rays(ic, 16, 16, 15, 17, 0, GOLD)


def sword(ic, blade, glow=None, diag=True):
    ic.line((8, 24), (24, 8), blade, 3); ic.line((7, 25), (23, 9), SIL2 if blade != SIL2 else SIL, 1)
    ic.line((6, 20), (12, 26), GOLD3, 3); ic.line((5, 27), (8, 24), BRN, 3)
    if glow: rays(ic, 24, 8, 3, 6, 6, glow)


def flame(ic, cx, cy, s, c1, c2):
    ic.poly([(cx, cy - 10 * s), (cx + 5 * s, cy - 2 * s), (cx + 4 * s, cy + 6 * s), (cx, cy + 8 * s), (cx - 4 * s, cy + 6 * s), (cx - 5 * s, cy - 2 * s)], c1)
    ic.poly([(cx, cy - 4 * s), (cx + 3 * s, cy + 2 * s), (cx + 2 * s, cy + 6 * s), (cx - 2 * s, cy + 6 * s), (cx - 3 * s, cy + 2 * s)], c2)


def snow(ic, cx, cy, r, c):
    for a in range(0, 180, 60):
        t = math.radians(a)
        ic.line((cx - math.cos(t) * r, cy - math.sin(t) * r), (cx + math.cos(t) * r, cy + math.sin(t) * r), c, 1)
    ic.px(cx, cy, (255, 255, 255, 255))


def shield(ic, col, rim):
    ic.poly([(6, 5), (26, 5), (26, 16), (16, 28), (6, 16)], rim); ic.poly([(8, 7), (24, 7), (24, 15), (16, 25), (8, 15)], col)


def boot(ic, col, spikes=False, water=False):
    ic.poly([(9, 5), (17, 5), (17, 16), (27, 21), (27, 27), (7, 27), (7, 21), (9, 20)], col); ic.rect((7, 25), (27, 28), BRN)
    ic.rect((9, 5), (17, 8), BRN2)
    if spikes:
        for x in (9, 14, 19, 24): ic.poly([(x, 28), (x + 2, 28), (x + 1, 31)], SIL2)
    if water:
        for x, y in ((5, 29), (12, 30), (22, 30), (28, 29)): ic.px(x, y, CYAN); ic.px(x + 1, y, BLUE)


def gear(ic, c1, c2):
    for a in range(0, 360, 45):
        t = math.radians(a); ic.rect((15.5 + math.cos(t) * 11 - 2, 15.5 + math.sin(t) * 11 - 2), (15.5 + math.cos(t) * 11 + 2, 15.5 + math.sin(t) * 11 + 2), c1)
    ic.ell((6, 6), (25, 25), c1); ic.ell((9, 9), (22, 22), c2); ic.ell((13, 13), (18, 18), BLK)


def bow(ic, col, glow=None, speed=False):
    ic.arc((4, 3, 24, 29), 270, 90, col, 3); ic.line((14, 4), (14, 28), SIL2, 1)
    ic.line((8, 16), (28, 16), BRN2, 2); ic.poly([(28, 13), (31, 16), (28, 19)], SIL2)
    if glow: rays(ic, 29, 16, 2, 4, 6, glow)
    if speed:
        for y in (9, 23): ic.line((1, y), (8, y), CYAN, 1)


ICONS = {}


def reg(name):
    def deco(f): ICONS[name] = f; return f
    return deco


@reg('Lens')
def _(i): lens(i, 1)
@reg('LensII')
def _(i): lens(i, 2)


@reg('FarLanterns')
def _(i): lantern(i, GOLD); rays(i, 15.5, 16, 12, 15, 8, GOLD2)
@reg('FarLanternsII')
def _(i): lantern(i, GOLD, CYAN2); rays(i, 15.5, 16, 11, 15, 16, GOLD2); i.arc((1, 1, 30, 30), 0, 360, GOLD3)
@reg('FireLens')
def _(i): lens(i, 2); i.ell((11, 11), (20, 20), ORG); flame(i, 15.5, 15, .6, RED, ORG2)
@reg('HeroLamp')
def _(i): lantern(i, GOLD, GOLD2, star=True); rays(i, 15.5, 16, 12, 15, 6, GOLD2)
@reg('Searing')
def _(i): lantern(i, ORG, ORG2); flame(i, 15.5, 5, .55, RED, ORG2)


@reg('Dash')
def _(i):
    boot(i, SIL); [i.line((1, y), (8, y), CYAN, 1) for y in (10, 15, 20)]
@reg('Reaper')
def _(i):
    i.line((6, 28), (22, 6), BRN2, 2); i.arc((8, 2, 28, 22), 200, 330, SIL2, 3); i.poly([(24, 6), (30, 12), (24, 10)], SIL2)
    i.ell((5, 20), (11, 26), RED); i.px(7, 22, (255, 200, 200, 255))
@reg('Regen')
def _(i):
    i.poly([(16, 28), (3, 14), (3, 9), (8, 4), (13, 4), (16, 8), (19, 4), (24, 4), (29, 9), (29, 14)], RED)
    i.rect((14, 10), (18, 20), (255, 255, 255, 255)); i.rect((10, 13), (22, 17), (255, 255, 255, 255)); i.px(7, 8, (255, 200, 200, 255)); i.px(8, 7, (255, 200, 200, 255))
@reg('Spin')
def _(i):
    i.arc((3, 3, 28, 28), 20, 300, CYAN, 2); i.poly([(25, 4), (30, 12), (21, 11)], CYAN); sword(i, SIL)
@reg('WideSwing')
def _(i):
    i.line((9, 26), (22, 8), BRN2, 3); i.poly([(12, 5), (30, 9), (24, 14), (16, 9)], SIL2); i.arc((2, 6, 30, 34), 190, 270, GOLD, 1)
@reg('Flare')
def _(i):
    i.line((6, 27), (20, 13), BRN2, 3); i.ell((19, 4), (28, 13), ORG2); i.ell((21, 6), (26, 11), (255, 255, 255, 255)); rays(i, 23.5, 8.5, 6, 9, 8, ORG)
@reg('Sight')
def _(i): eye(i)
@reg('Pulse')
def _(i):
    i.arc((2, 2, 29, 29), 0, 360, GOLD3, 2); i.arc((7, 7, 24, 24), 0, 360, GOLD, 2); i.ell((12, 12), (19, 19), GOLD2); i.px(15, 15, (255, 255, 255, 255))
    rays(i, 15.5, 15.5, 13, 15, 8, GOLD2)
@reg('SilverEdge')
def _(i): sword(i, SIL2, SIL)
@reg('Optics')
def _(i):
    i.poly([(16, 4), (27, 24), (5, 24)], CYAN); i.poly([(16, 9), (22, 21), (10, 21)], CYAN2); i.line((1, 16), (9, 16), GOLD2, 1); i.line((22, 14), (31, 9), VIO2, 1); i.line((22, 18), (31, 22), GOLD, 1)
@reg('CombatLamp')
def _(i): lantern(i, RED, ORG); flame(i, 15.5, 5, .55, RED, ORG2)
@reg('SilverGears')
def _(i): gear(i, SIL3, SIL2)
@reg('AutoDrill')
def _(i):
    i.rect((11, 3), (20, 11), SIL3); i.poly([(11, 11), (20, 11), (16, 29)], SIL2)
    for y in (14, 19, 24): i.line((12, y), (20, y - 3), SIL3, 1)
@reg('Boots')
def _(i): boot(i, BRN2, water=True)
@reg('Beam')
def _(i):
    i.ell((2, 10), (11, 22), GOLD); i.ell((4, 12), (9, 20), CYAN2); i.rect((11, 14), (30, 17), CYAN2); i.rect((11, 15), (30, 16), (255, 255, 255, 255)); rays(i, 30, 15.5, 1, 2, 4, CYAN)
@reg('FlameEdge')
def _(i): sword(i, SIL2, ORG); flame(i, 24, 9, .7, RED, ORG2)
@reg('HeatShield')
def _(i): shield(i, (110, 60, 50, 255), ORG); flame(i, 16, 14, .7, ORG, ORG2)
@reg('Collector')
def _(i):
    i.poly([(5, 10), (27, 10), (24, 28), (8, 28)], BRN2); i.rect((4, 8), (28, 11), BRN)
    for x, y in ((11, 16), (17, 20), (21, 15), (13, 24)): i.ell((x, y), (x + 3, y + 3), BLK)
@reg('ForgeHeat')
def _(i):
    i.rect((6, 17), (26, 21), SIL3); i.rect((9, 21), (23, 27), BLK); i.poly([(4, 17), (10, 12), (26, 12), (28, 17)], SIL); flame(i, 16, 6, .6, ORG, ORG2)
@reg('Fireproof')
def _(i): shield(i, (200, 70, 40, 255), GOLD3); flame(i, 16, 14, .8, ORG2, (255, 255, 255, 255))
@reg('Prism')
def _(i):
    i.poly([(16, 2), (28, 12), (16, 30), (4, 12)], CYAN); i.poly([(16, 2), (22, 12), (16, 30), (10, 12)], CYAN2); i.line((4, 12), (28, 12), BLUE, 1); i.px(14, 6, (255, 255, 255, 255))
@reg('SpikeBoots')
def _(i): boot(i, SIL3, spikes=True)
@reg('FrostEdge')
def _(i): sword(i, CYAN2, CYAN); snow(i, 24, 8, 5, (255, 255, 255, 255))
@reg('FrostArmor')
def _(i): shield(i, (70, 120, 190, 255), CYAN); snow(i, 16, 15, 7, (255, 255, 255, 255))
@reg('Marksman')
def _(i): bow(i, BRN2)
@reg('QuickDraw')
def _(i): bow(i, BRN2, speed=True)
@reg('Piercing')
def _(i):
    i.line((2, 16), (29, 16), BRN2, 2); i.poly([(26, 12), (31, 16), (26, 20)], SIL2); i.ell((12, 7), (19, 25), (180, 60, 60, 255)); i.line((2, 16), (29, 16), BRN2, 2); i.poly([(26, 12), (31, 16), (26, 20)], SIL2)
@reg('Overcharge')
def _(i): bow(i, VIO, glow=VIO2); i.arc((3, 2, 25, 30), 270, 90, VIO2, 1)

# ───────────────────────── данные навыков ─────────────────────────
# id: (страница, макс.уровень, (x,y), тип стоимости, родители, подпись эффекта, единица, база, шаг)
# единицы: % (+X%), cut (-X%), hp, s (секунды), tiles, dps, abs (абсолютный %), n
CURVES = {
    'u5': [1.0, .3, .35, .4, .5], 's5': [.15, .2, .3, .4, .55], 'h5': [.2, .25, .35, .45, .6], 's4': [.25, .35, .45, .6], 's1': [1.0],
    'p4': [0.0, .4, .6, .8], 's3': [.4, .5, .7],
}
S = {
    'Lens': (0, 5, (1, 0), 's5', [], 'Радиус света героя', '%', 0, .02),
    'LensII': (0, 5, (0, 1), 'h5', ['Lens:max'], 'Радиус света героя', '%', 0, .04),
    'FarLanterns': (0, 5, (1, 1), 's5', ['Lens:3'], 'Радиус фонарей', '%', 0, .04),
    'FarLanternsII': (0, 5, (1, 2), 'h5', ['FarLanterns:max'], 'Радиус фонарей', '%', 0, .06),
    'Searing': (0, 5, (2, 1), 's5', ['Lens:3'], 'Урон светом по врагам в секунду', 'dps', 1.5, .2),
    'Flare': (0, 5, (2, 2), 'u5', ['FarLanterns:3'], 'Длительность ракеты', 's', -1, 1),
    'Dash': (0, 5, (4, 0), 'u5', [], 'Перезарядка рывка', 'cut', -.06, .06),
    'Reaper': (0, 5, (3, 1), 's5', ['Dash:2'], 'Лечение за убийство', 'hp', .375, .225),
    'Spin': (0, 5, (4, 1), 'u5', ['Dash:2'], 'Радиус и урон круга', '%', -.08, .08),
    'Marksman': (0, 5, (6, 0), 's5', [], 'Урон дальнего боя', '%', 0, .05),
    'QuickDraw': (0, 5, (6, 1), 's5', ['Marksman:2'], 'Скорострельность', '%', 0, .04),
    'Piercing': (0, 1, (5, 1), 's1', ['Marksman:3'], '', '', 0, 0),
    'Overcharge': (0, 5, (5, 2), 's5', ['Piercing:1'], 'Скорость снарядов', '%', 0, .06),
    'WideSwing': (0, 1, (3, 3), 's1', [], '', '', 0, 0),
    'Sight': (0, 5, (4, 3), 's5', [], 'Радиус обнаружения мест (клеток)', 'tiles', 12, 2),
    'Pulse': (0, 4, (5, 3), 'p4', [], 'Перезарядка импульса', 'cut', -.05, .05),
    'Regen': (0, 5, (3, 0), 's5', [], 'Регенерация здоровья вне света (в секунду)', 'hp', 0, .25),

    'SilverEdge': (1, 5, (0, 0), 's5', [], 'Урон по теням', '%', 0, .1),
    'Optics': (1, 1, (2, 0), 's1', [], '', '', 0, 0),
    'CombatLamp': (1, 1, (1, 1), 's1', ['Optics:1'], '', '', 0, 0),
    'HeroLamp': (1, 5, (4, 0), 's5', ['Lens:3'], 'Свет героя', '%', 0, .05),
    'SilverGears': (1, 5, (6, 0), 'h5', [], 'Скорость производства', '%', 0, .1),
    'AutoDrill': (1, 1, (6, 1), 's1', ['SilverGears:1'], '', '', 0, 0),
    'Boots': (1, 5, (3, 2), 's5', [], 'Скорость в воде', 'abs', .55, .07),
    'Beam': (1, 1, (5, 2), 's1', [], '', '', 0, 0),

    'FlameEdge': (2, 5, (1, 0), 's5', ['SilverEdge:3'], 'Длительность поджога', 's', 1.0, .4),
    'HeatShield': (2, 5, (0, 1), 's5', [], 'Защита от огня и дыма', 'cut', 0, .12),
    'Collector': (2, 1, (3, 0), 's1', [], '', '', 0, 0),
    'ForgeHeat': (2, 5, (5, 0), 'h5', ['SilverGears:3'], 'Скорость кузницы', '%', 0, .2),
    'FireLens': (2, 5, (4, 1), 's5', ['Optics:1'], 'Радиус фонарей', '%', 0, .06),
    'Fireproof': (2, 1, (2, 2), 's1', [], '', '', 0, 0),

    'Prism': (3, 1, (0, 0), 's1', [], '', '', 0, 0),
    'SpikeBoots': (3, 1, (2, 0), 's1', [], '', '', 0, 0),
    'FrostEdge': (3, 5, (3, 1), 's5', ['FlameEdge:3'], 'Замедление врагов', 's', .4, .4),
    'FrostArmor': (3, 5, (1, 1), 's5', ['HeatShield:3'], 'Защита от холода', 'cut', 0, .12),
}


def tres(i, v):
    page, mx, pos, curve, par, lab, unit, base, per = v
    arr = lambda xs: ', '.join(str(x) for x in xs)
    pars = ', '.join('"%s"' % p for p in par)
    return f'''[gd_resource type="Resource" script_class="SkillDef" load_steps=2 format=3]

[ext_resource type="Script" path="res://Scripts/Gameplay/SkillDef.cs" id="1"]

[resource]
script = ExtResource("1")
Id = "{i}"
Page = {page}
MaxLevel = {mx}
Pos = Vector2({pos[0]}, {pos[1]})
Icon = "res://Assets/UI/Skills/{i}.png"
CostCurve = PackedFloat32Array({arr(CURVES[curve])})
Parents = PackedStringArray({pars})
EffLabel = "{lab}"
Unit = "{unit}"
EffBase = {base}
EffPer = {per}
'''


if __name__ == '__main__':
    for k, f in ICONS.items():
        ic = Ic(); f(ic); ic.save(k)
    missing = [k for k in S if k not in ICONS]
    assert not missing, missing
    for k, v in S.items():
        open(os.path.join(RES_DIR, k + '.tres'), 'w', encoding='utf-8', newline='\n').write(tres(k, v))
    print('skills', len(S), 'icons', len(ICONS))
