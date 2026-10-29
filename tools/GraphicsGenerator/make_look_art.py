"""Слои внешности героя Lanther (Assets/Sprites/Player/Look) и иконки/фон экранов выбора (Assets/UI/Flow).
Запуск: python tools/GraphicsGenerator/make_look_art.py
Слой — лист 32x16 (два кадра по 16x16, кадр 1 — шаг). Тонируемые слои (кожа, волосы, одежда) нарисованы в оттенках серого и умножаются на цвет палитры в игре;
контур не рисуется здесь: LookArt.cs строит его в коде по общему силуэту слоёв.
"""
import os
from PIL import Image

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..')
LOOK = os.path.join(ROOT, 'Assets', 'Sprites', 'Player', 'Look')
FLOW = os.path.join(ROOT, 'Assets', 'UI', 'Flow')
os.makedirs(LOOK, exist_ok=True)
os.makedirs(FLOW, exist_ok=True)

W = H = 16

# ───────────── карты (кадр 0): символы s кожа, e глаз, a/b одежда 1/2, h волосы, m ноги, f ручка фонаря, l корпус, i свет
HEAD = [
    '................',
    '................',
    '.....ssssss.....',
    '....ssssssss....',
    '....ssssssss....',
    '....sssssess....',
    '....ssssssss....',
    '.....ssssss.....',
]
LANTERN = {  # x 12..14, y 7..13
    8: {12: 'f', 13: 'f', 14: 'f'},
    9: {12: 'l', 13: 'l', 14: 'l'},
    10: {12: 'l', 13: 'i', 14: 'l'},
    11: {12: 'l', 13: 'l', 14: 'l'},
    12: {12: 'l', 13: 'l', 14: 'l'},
}
LANTERN_COMPACT = {  # фасон «Разведчик»: маленькая лампа на плечевом ремне, x 12..13, y 9..11
    9: {12: 'f', 13: 'f'},
    10: {12: 'l', 13: 'i'},
    11: {12: 'l', 13: 'l'},
}
VARIANTS = {
    0: [  # куртка: рукава и пояс — второй цвет
        '....bbaaaabb....',
        '....bbaaaabb....',
        '....baaaaaab....',
        '....bbbbbbbb....',
        '....baaaaaab....',
        '.....aaaaaa.....',
    ],
    1: [  # плащ: второй цвет — широкая накидка за спиной
        '...bbbaaaabb....',
        '...bbbaaaabb....',
        '...bbaaaaaab....',
        '...bbaaaaaab....',
        '...bbaaaaaab....',
        '...bbbaaaab.....',
    ],
    2: [  # туника: подол и воротник — второй цвет
        '....aabbbbaa....',
        '....aaaaaaaa....',
        '....baaaaaab....',
        '....aaaaaaaa....',
        '....aaaaaaaa....',
        '.....bbbbbb.....',
    ],
    3: [  # разведчик: жилет с шарфом и широким ремнём, лампа маленькая на плече
        '....bbbbbbbb....',
        '....baaaaaab....',
        '....baaaaaab....',
        '....bbbbbbbb....',
        '....aaaaaaaa....',
        '.....aaaaaa.....',
    ],
}
CAPE_EXTRA = {14: {3: 'b', 4: 'b'}}  # плащ свисает на втором ряду ног

HAIR = {
    0: ['.....hhhhhh.....', '....hhhhhhhh....', '....hh..........', '....hh..........'],                       # короткая
    1: ['......h..h.h....', '.....hhhhhhh....', '....hhhhhhhh....', '....hh..........'],                       # ёжик
    2: ['.....hhhhhh.....', '....hhhhhhhh....', '...hhh..........', '...hhh..........', '...hhh..........', '...hhh..........', '...hh...........', '...hh...........', '...h............'],   # длинная
    3: ['.....hhhhhh.....', '....hhhhhhhh....', '....hh..........', '...hh...........', '..hh............', '..h.............'],   # хвост
    4: ['.....hhhhhh.....', '....hhhhhhhh....', '....hhhhhhhh....', '....hhh....h....', '....hhh....h....', '....hh..........'],  # капюшон
    5: ['.....hhhhhh.....', '....hhhhhhhhhh..', '....h...........'],                                                    # шапка с козырьком
    6: [],                                                                                                          # лысый
}
HAIR_Y0 = {0: 2, 1: 1, 2: 2, 3: 2, 4: 2, 5: 2, 6: 0}

SHADE = 0.72  # доля яркости для теней в тонируемых слоях


def blank():
    return [[None] * W for _ in range(H)]


def put_rows(grid, rows, y0):
    for dy, r in enumerate(rows):
        for x, ch in enumerate(r):
            if ch != '.':
                grid[y0 + dy][x] = ch


def frame_grid(variant, hair, frame):
    g = blank()
    put_rows(g, HEAD, 0)
    put_rows(g, VARIANTS[variant], 8)
    for y, row in (LANTERN_COMPACT if variant == 3 else LANTERN).items():
        for x, ch in row.items():
            g[y][x] = ch
    if variant == 1:
        for x, ch in CAPE_EXTRA[14].items():
            g[14][x] = ch
    # ноги и сапоги: кадр 1 — шаг (задняя нога отведена назад, передняя вперёд)
    for x in (5, 6, 9, 10):
        g[14][x] = 'm'
    if frame == 0:
        for x in (5, 6, 9, 10):
            g[15][x] = 'm'
    else:
        for x in (4, 5):
            g[15][x] = 'm'
        for x in (10, 11):
            g[15][x] = 'm'
    if hair in HAIR and HAIR[hair]:
        put_rows(g, HAIR[hair], HAIR_Y0[hair])
    return g


def shaded(grid, ch_set, tone_fn):
    """Возвращает изображение слоя: пиксели из ch_set в оттенках серого (нижний край — тень)."""
    im = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    for y in range(H):
        for x in range(W):
            c = grid[y][x]
            if c in ch_set:
                below = grid[y + 1][x] if y + 1 < H else None
                v = SHADE if (below not in ch_set) else 1.0
                if x > 0 and grid[y][x - 1] not in ch_set and c in ('a', 'b'):
                    v = min(v, 0.86)
                g = int(255 * v)
                im.putpixel((x, y), (g, g, g, 255))
    return im


def layer_for(variant, hair, frame, chars):
    g = frame_grid(variant, hair, frame)
    # слой одежды/кожи не должен включать пиксели, закрытые волосами
    return shaded(g, chars, None)


def make_sheet(fn):
    sheet = Image.new('RGBA', (W * 2, H), (0, 0, 0, 0))
    for f in range(2):
        sheet.paste(fn(f), (f * W, 0))
    return sheet


def skin_layer(f):
    g = blank(); put_rows(g, HEAD, 0)
    # руки не видны: кожа только на лице
    return shaded(g, {'s'}, None)


def cloth_layer(variant, chars):
    def fn(f):
        g = blank(); put_rows(g, VARIANTS[variant], 8)
        if variant == 1:
            for x, ch in CAPE_EXTRA[14].items():
                g[14][x] = ch
        return shaded(g, chars, None)
    return lambda f: fn(f)


def hair_layer(hair):
    def fn(f):
        g = blank()
        if HAIR[hair]:
            put_rows(g, HAIR[hair], HAIR_Y0[hair])
        return shaded(g, {'h'}, None)
    return fn


def detail_layer(variant, part='base'):
    """part: base — глаза, ноги, ручка лампы; lamp — горящая лампа (жёлтая); lampoff — потухшая (серо-синяя, без свечения)."""
    return lambda f: _detail(variant, f, part)


def _detail(variant, f, part):
    im = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    g = frame_grid(variant, 6, f)
    pals = {
        'base': {'e': (22, 26, 44), 'm': (42, 47, 69), 'f': (58, 63, 77)},
        'lamp': {'l': (255, 216, 107), 'i': (255, 251, 224)},
        'lampoff': {'l': (72, 78, 104), 'i': (112, 120, 150)},
    }
    pal = pals[part]
    for y in range(H):
        for x in range(W):
            c = g[y][x]
            if c in pal:
                below = g[y + 1][x] if y + 1 < H else None
                col = pal[c]
                if c == 'l' and below != 'l':
                    col = (232, 176, 72) if part == 'lamp' else (52, 57, 78)
                im.putpixel((x, y), col + (255,))
    return im


def save(name, img):
    img.save(os.path.join(LOOK, name))


save('skin.png', make_sheet(skin_layer))
for v in VARIANTS:
    save(f'detail_v{v}.png', make_sheet(detail_layer(v)))
    save(f'lamp_v{v}.png', make_sheet(detail_layer(v, 'lamp')))
    save(f'lampoff_v{v}.png', make_sheet(detail_layer(v, 'lampoff')))
    save(f'cloth1_v{v}.png', make_sheet(cloth_layer(v, {'a'})))
    save(f'cloth2_v{v}.png', make_sheet(cloth_layer(v, {'b'})))
for h in HAIR:
    save(f'hair_{h}.png', make_sheet(hair_layer(h)))

# ───────────── иконки 16x16 для экранов выбора
GOLD = (255, 210, 63, 255); GOLD_D = (190, 140, 40, 255); INK = (16, 20, 42, 255); WHITE = (232, 238, 255, 255)
BLUE = (74, 109, 192, 255); BLUE_D = (47, 71, 127, 255); RED = (214, 84, 72, 255); RED_D = (140, 44, 44, 255); GREEN = (120, 210, 120, 255); GREEN_D = (60, 130, 70, 255)
GREY = (140, 150, 170, 255); GREY_D = (80, 90, 110, 255)


def icon_from(rows, pal):
    im = Image.new('RGBA', (16, 16), (0, 0, 0, 0))
    for y, r in enumerate(rows):
        for x, ch in enumerate(r):
            if ch in pal and x < 16 and y < 16:
                im.putpixel((x, y), pal[ch])
    return im


ICONS = {
    'play': (['................', '....oo..........', '....oGo.........', '....oGGo........', '....oGGGo.......', '....oGGGGo......', '....oGGGGGo.....', '....oGGGGGGo....', '....oGGGGGo.....', '....oGGGGo......', '....oGGGo.......', '....oGGo........', '....oGo.........', '....oo..........', '................', '................'], {'o': INK, 'G': GOLD}),
    'plus': (['................', '................', '......oooo......', '......oGGo......', '......oGGo......', '..oooooGGoooo...', '..oGGGGGGGGGo...', '..oGGGGGGGGGo...', '..oooooGGoooo...', '......oGGo......', '......oGGo......', '......oooo......', '................', '................', '................', '................'], {'o': INK, 'G': GREEN}),
    'trash': (['................', '......oooo......', '....oooRRooo....', '...oRRRRRRRRo...', '...oooooooooo...', '....oRoRoRoo....', '....oRoRoRoo....', '....oRoRoRoo....', '....oRoRoRoo....', '....oRoRoRoo....', '....oRoRoRoo....', '....oRRRRRRo....', '.....oooooo.....', '................', '................', '................'], {'o': INK, 'R': RED}),
    'dice': (['................', '..oooooooooooo..', '..oWWWWWWWWWWo..', '..oWKKWWWWKKWo..', '..oWKKWWWWKKWo..', '..oWWWWWWWWWWo..', '..oWWWWKKWWWWo..', '..oWWWWKKWWWWo..', '..oWWWWWWWWWWo..', '..oWKKWWWWKKWo..', '..oWKKWWWWKKWo..', '..oWWWWWWWWWWo..', '..oooooooooooo..', '................', '................', '................'], {'o': INK, 'W': WHITE, 'K': BLUE_D}),
    'edit': (['................', '..........oo....', '.........oGGo...', '........oGGGGo..', '.......oGGGGo...', '......oGGGGo....', '.....oBBGGo.....', '....oBBBBo......', '...oBBBBo.......', '..oBBBBo........', '..oWWBo.........', '..oWWo..........', '..ooo...........', '................', '................', '................'], {'o': INK, 'G': GOLD, 'B': BLUE, 'W': WHITE}),
    'world': (['................', '.....oooooo.....', '...ooGGGGBBoo...', '..oGGGGGBBBBBo..', '..oGGGGBBBBGGo..', '.oGGGBBBBGGGGGo.', '.oGGBBBBGGGGGBo.', '.oGBBBBGGGGGBBo.', '.oBBBBGGGGGBBBo.', '..oBBBGGGGBBBo..', '..oBBGGGGBBBBo..', '...ooBBBBBBoo...', '.....oooooo.....', '................', '................', '................'], {'o': INK, 'G': GREEN, 'B': BLUE}),
    'skull': (['................', '....oooooooo....', '...oWWWWWWWWo...', '..oWWWWWWWWWWo..', '..oWWWWWWWWWWo..', '..oWKKKWWKKKWo..', '..oWKKKWWKKKWo..', '..oWWWWKKWWWWo..', '...oWWWWWWWWo...', '....oWoWoWoWo...', '....oWoWoWoWo...', '.....oooooooo...', '................', '................', '................', '................'], {'o': INK, 'W': WHITE, 'K': INK}),
    'shield': (['................', '..oooooooooooo..', '..oBBBBBBBBBBo..', '..oBWWBBBBWWBo..', '..oBWBBBBBBWBo..', '..oBBBBGGBBBBo..', '..oBBBGGGGBBBo..', '...oBBBGGBBBo...', '...oBBBBBBBBo...', '....oBBBBBBo....', '.....oBBBBo.....', '......oBBo......', '.......oo.......', '................', '................', '................'], {'o': INK, 'B': BLUE, 'W': WHITE, 'G': GOLD}),
    'clock': (['................', '.....oooooo.....', '...ooWWWWWWoo...', '..oWWWWKWWWWWo..', '..oWWWWKWWWWWo..', '.oWWWWWKWWWWWWo.', '.oWWWWWKKKKWWWo.', '.oWWWWWWWWWWWWo.', '..oWWWWWWWWWWo..', '..oWWWWWWWWWWo..', '...ooWWWWWWoo...', '.....oooooo.....', '................', '................', '................', '................'], {'o': INK, 'W': WHITE, 'K': BLUE_D}),
    'users': (['................', '....oo....oo....', '...oWWo..oWWo...', '...oWWo..oWWo...', '....oo....oo....', '..oooooo.oooooo.', '.oBBBBBBooBBBBBo', '.oBBBBBBooBBBBBo', '.oBBBBBBooBBBBBo', '.oooooooooooooo.', '................', '................', '................', '................', '................', '................'], {'o': INK, 'W': WHITE, 'B': BLUE}),
    'heart': (['................', '..ooo....ooo....', '.oRRRo..oRRRo...', 'oRRWRRooRRRRRo..', 'oRRRRRRRRRRRRo..', 'oRRRRRRRRRRRRo..', '.oRRRRRRRRRRo...', '..oRRRRRRRRo....', '...oRRRRRRo.....', '....oRRRRo......', '.....oRRo.......', '......oo........', '................', '................', '................', '................'], {'o': INK, 'R': RED, 'W': WHITE}),
    'sword': (['................', '............oo..', '...........oWWo.', '..........oWWo..', '.........oWWo...', '........oWWo....', '.......oWWo.....', '..o...oWWo......', '..oo.oWWo.......', '...ooWWo........', '...oGGo.........', '..oGGooo........', '.oGo..oGo.......', '.oo...oo........', '................', '................'], {'o': INK, 'W': WHITE, 'G': GOLD}),
    'check': (['................', '................', '..............oo', '.............oGGo', '............oGGo.', '...oo......oGGo..', '..oGGo....oGGo...', '...oGGo..oGGo....', '....oGGooGGo.....', '.....oGGGGo......', '......oGGo.......', '.......oo........', '................', '................', '................', '................'], {'o': INK, 'G': GREEN}),
    'flip': (['................', '................', '...oooooo.......', '..oBBBBBBoo.....', '.oBBooooBBBo....', '.oBo....oBBBo...', '.oBo...oBBBBBo..', '..o.....ooooo...', '..........oBo...', '.....oBo..oBo...', '......oBBooBo...', '.......oBBBBo...', '........oooo....', '................', '................', '................'], {'o': INK, 'B': BLUE}),
    'walk': (['................', '.....oo.........', '....oWWo........', '....oWWo........', '.....oo.........', '....oBBBo.......', '...oBBBBBo......', '...oBoBBBo......', '....o.oBBo......', '.....oBBo.......', '.....oBo.oo.....', '....oBBo.oBo....', '....ooo..ooo....', '................', '................', '................'], {'o': INK, 'W': WHITE, 'B': BLUE}),
    'right': (['................', '................', '.....oo.........', '.....oGoo.......', '.....oGGGoo.....', '.....oGGGGGoo...', '.....oGGGGGGGo..', '.....oGGGGGGGo..', '.....oGGGGGoo...', '.....oGGGoo.....', '.....oGoo.......', '.....oo.........', '................', '................', '................', '................'], {'o': INK, 'G': GOLD}),
    'left': (['................', '................', '.........oo.....', '.......ooGo.....', '.....ooGGGo.....', '...ooGGGGGo.....', '..oGGGGGGGo.....', '..oGGGGGGGo.....', '...ooGGGGGo.....', '.....ooGGGo.....', '.......ooGo.....', '.........oo.....', '................', '................', '................', '................'], {'o': INK, 'G': GOLD}),
    'lobby': (['................', '................', '...oo..oo..oo...', '..oGGooGGooGGo..', '..oGGGGGGGGGGo..', '...oGGGGGGGGo...', '....oGGGGGGo....', '.....oGGGGo.....', '....oGWWGGGo....', '.....oWWGGo.....', '......oooo......', '................', '................', '................', '................', '................'], {'o': INK, 'G': GOLD, 'W': WHITE}),
}
for name, (rows, pal) in ICONS.items():
    icon_from(rows, pal).save(os.path.join(FLOW, f'icon_{name}.png'))

# ───────────── фон экранов выбора: ночной пейзаж 640x360
import random
rnd = random.Random(7)
bg = Image.new('RGBA', (640, 360), (0, 0, 0, 255))
for y in range(360):
    t = y / 359
    c = (int(10 + 22 * t), int(14 + 26 * t), int(34 + 40 * t))
    for x in range(640):
        bg.putpixel((x, y), c + (255,))
for _ in range(150):
    x, y = rnd.randint(0, 639), rnd.randint(0, 200)
    b = rnd.choice([(120, 140, 190), (170, 190, 235), (255, 240, 200)])
    bg.putpixel((x, y), b + (255,))
    if rnd.random() < .15:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            bg.putpixel((x + dx, y + dy), tuple(int(v * .5) for v in b) + (255,))


def hills(base, amp, col, seed):
    r = random.Random(seed)
    ph = [r.random() * 6.28 for _ in range(3)]
    import math
    for x in range(640):
        h = base + amp * (math.sin(x / 70 + ph[0]) * .5 + math.sin(x / 31 + ph[1]) * .3 + math.sin(x / 13 + ph[2]) * .12)
        for y in range(int(h), 360):
            bg.putpixel((x, y), col + (255,))
    return


hills(250, 22, (14, 22, 44), 1)
hills(280, 18, (10, 17, 34), 2)


def pine(cx, base, h, col):
    for i in range(h):
        w = int((h - i) * .35) + (1 if i % 3 == 0 else 0)
        for x in range(cx - w, cx + w + 1):
            if 0 <= x < 640 and 0 <= base - h + i < 360:
                bg.putpixel((x, base - h + i), col + (255,))
    for y in range(base, base + 5):
        bg.putpixel((cx, y), col + (255,))


bg.save(os.path.join(FLOW, 'flow_bg.png'))
print('look layers and flow art written')
