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

# ───────────── карты (кадр 0), чиби-пропорции: большая голова (строки 1–8), короткое тело (9–13), ноги (14–15). Герой смотрит вправо.
# символы: s кожа/шерсть, k тёмная шерсть (уши собаки), e глаз, a/b одежда 1/2, h волосы, m ноги, f ручка фонаря, l корпус, i свет
HEAD = {  # верхняя строка 2: над ней остаётся ряд 0-1 для причёски/ушей и контура (иначе верхний пиксель обрезается рамкой кадра)
    2: '.....ssssss.....',
    3: '....ssssssss....',
    4: '...ssssssssss...',
    5: '...ssssssssss...',
    6: '...ssssssssss...',
    7: '...ssssssssss...',
    8: '....ssssssss....',
    9: '......ssss......',
}
EYES = [(7, 6), (7, 7), (10, 6), (10, 7)]
HANDS = [(4, 11), (11, 11)]
# виды: 0 человек, 1 кот (острые уши, хвост), 2 собака (висячие уши, хвост)
SPECIES_EXTRA = {
    0: {},
    1: {(4, 1): 's', (11, 1): 's', (4, 2): 's', (11, 2): 's', (4, 13): 's', (3, 13): 's', (2, 12): 's', (2, 11): 's', (2, 10): 's', (3, 9): 's'},
    2: {(2, 3): 'k', (3, 3): 'k', (2, 4): 'k', (2, 5): 'k', (2, 6): 'k', (2, 7): 'k', (12, 3): 'k', (13, 3): 'k', (13, 4): 'k', (13, 5): 'k', (13, 6): 'k', (13, 7): 'k',
        (4, 12): 's', (3, 11): 's', (2, 10): 's', (2, 9): 's'},
}
# мордочки (не тонируются): нос, усы, мордочка, язык, розовое в ушах
FACE = {
    1: {(4, 2): (255, 160, 180), (11, 2): (255, 160, 180), (9, 8): (255, 140, 165), (13, 7): (235, 235, 245), (13, 8): (235, 235, 245), (12, 8): (235, 235, 245)},
    2: {(9, 8): (245, 232, 205), (10, 8): (245, 232, 205), (11, 8): (245, 232, 205), (11, 7): (245, 232, 205), (12, 7): (30, 24, 28), (9, 9): (240, 110, 130)},
}
LANTERN = {  # в передней руке: ручка и лампа справа от тела
    10: {12: 'f', 13: 'f'},
    11: {12: 'l', 13: 'l'},
    12: {12: 'i', 13: 'l'},
    13: {12: 'l', 13: 'l'},
}
LANTERN_COMPACT = {  # фасон «Разведчик»: маленькая лампа на ремне
    11: {12: 'f'},
    12: {12: 'i', 13: 'l'},
}
VARIANTS = {
    0: [  # куртка: рукава и пояс — второй цвет
        '....baaaaaab....',
        '....baaaaaab....',
        '.....aaaaaa.....',
        '.....bbbbbb.....',
        '.....aaaaaa.....',
    ],
    1: [  # плащ: второй цвет — накидка за спиной
        '...bbaaaaaab....',
        '...bbaaaaaab....',
        '...bbaaaaaa.....',
        '...bbaaaaaa.....',
        '...bbbaaaaa.....',
    ],
    2: [  # туника: воротник и подол — второй цвет
        '....aabbbbaa....',
        '....aaaaaaaa....',
        '.....aaaaaa.....',
        '.....aaaaaa.....',
        '.....bbbbbb.....',
    ],
    3: [  # разведчик: шарф и ремень
        '....bbbbbbbb....',
        '....baaaaaab....',
        '.....aaaaaa.....',
        '.....bbbbbb.....',
        '.....aaaaaa.....',
    ],
}
CAPE_EXTRA = {14: {3: 'b', 4: 'b'}}  # плащ свисает до ног

HAIR = {
    0: ['.....hhhhhh.....', '...hhhhhhhhhh...', '..hhhhhhhhhhhh..', '..hhhhhhhhhhhh..', '..hhhh.hh.h.hh..', '..hh............', '..hh............'],   # короткая
    1: ['..h.h.h.h.h.h...', '..hhhhhhhhhhhh..', '.hhhhhhhhhhhhhh.', '..hhhhhhhhhhhh..', '..hhh.h..h..hh..', '..hh............'],                         # ёжик
    2: ['.....hhhhhh.....', '...hhhhhhhhhh...', '..hhhhhhhhhhhh..', '..hhhhhhhhhhhh..', '..hhhh.hh.h.hh..', '..hhh.......hh..', '..hhh.......hh..', '..hhh.......hh..',
        '..hhh...........', '..hh............', '..hh............'],                                                                                       # длинная
    3: ['.....hhhhhh.....', '...hhhhhhhhhh...', '..hhhhhhhhhhhh..', '.hhhhhhhhhhhhh..', 'hhhhhh.hh.h.hh..', 'hh.hh...........', 'h...............', 'h...............'],  # хвост
    4: ['....hhhhhhh.....', '..hhhhhhhhhhh...', '.hhhhhhhhhhhhh..', '.hhhhhhhhhhhhhh.', '.hhhh.......hhh.', '.hhh.........hh.', '.hhh.........hh.', '..hhh.......hh..',
        '....hhh..hhh....'],                                                                                                                                 # капюшон
    5: ['................', '....hhhhhhh.....', '...hhhhhhhhh....', '..hhhhhhhhhhhhh.', '..hh............'],                                               # кепка с козырьком
    6: [],                                                                                                                                                   # лысый
}
HAIR_Y0 = {h: 1 for h in HAIR}

SHADE = 0.72  # доля яркости для теней в тонируемых слоях


def blank():
    return [[None] * W for _ in range(H)]


def put_rows(grid, rows, y0):
    for dy, r in enumerate(rows):
        for x, ch in enumerate(r):
            if ch != '.':
                grid[y0 + dy][x] = ch


def put_head(g, species=0):
    for y, r in HEAD.items():
        for x, ch in enumerate(r):
            if ch != '.':
                g[y][x] = ch
    for (x, y), ch in SPECIES_EXTRA[species].items():
        g[y][x] = ch
    for x, y in HANDS:
        g[y][x] = 's'


part_legs = False


def frame_grid(variant, hair, frame, species=0):
    g = blank()
    put_head(g, species)
    put_rows(g, VARIANTS[variant], 9)
    for x, y in HANDS:
        g[y][x] = 's'
    for x, y in EYES:
        g[y][x] = 'e'
    for y, row in (LANTERN_COMPACT if variant == 3 else LANTERN).items():
        for x, ch in row.items():
            g[y][x] = ch
    if variant == 1:
        for x, ch in CAPE_EXTRA[14].items():
            g[14][x] = ch
    # ноги: кадр 1 — шаг
    for x in (5, 6, 9, 10):                       # ноги растут из-под одежды: строка 13 закрыта телом, слой ног рисуется позади
        g[14][x] = 'm'
        if g[13][x] is None or g[13][x] in ('a', 'b'):
            g[13][x] = 'm' if part_legs else g[13][x]
    if frame == 0:
        for x in (5, 6, 9, 10):
            g[15][x] = 'm'
    else:
        for x in (4, 5, 10, 11):
            g[15][x] = 'm'
    if hair in HAIR and HAIR[hair]:
        put_rows(g, HAIR[hair], HAIR_Y0[hair])
    return g


BASE = {'k': 0.62}


def shaded(grid, ch_set, tone_fn):
    """Изображение слоя: пиксели из ch_set в оттенках серого (нижний край — тень, k — тёмная шерсть)."""
    im = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    for y in range(H):
        for x in range(W):
            c = grid[y][x]
            if c in ch_set:
                below = grid[y + 1][x] if y + 1 < H else None
                v = BASE.get(c, 1.0) * (SHADE if (below not in ch_set) else 1.0)
                if x > 0 and grid[y][x - 1] not in ch_set and c in ('a', 'b'):
                    v = min(v, 0.86)
                g = int(255 * v)
                im.putpixel((x, y), (g, g, g, 255))
    return im


def make_sheet(fn):
    sheet = Image.new('RGBA', (W * 2, H), (0, 0, 0, 0))
    for f in range(2):
        sheet.paste(fn(f), (f * W, 0))
    return sheet


def skin_layer(species):
    def fn(f):
        g = blank(); put_head(g, species)
        return shaded(g, {'s', 'k'}, None)
    return fn


def face_layer(species):
    def fn(f):
        im = Image.new('RGBA', (W, H), (0, 0, 0, 0))
        for (x, y), col in FACE[species].items():
            im.putpixel((x, y), col + (255,))
        return im
    return fn


def cloth_layer(variant, chars):
    def fn(f):
        g = blank(); put_rows(g, VARIANTS[variant], 9)
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
    """part: base — глаза (с бликом), ноги; lamp — горящая лампа; lampoff — потухшая."""
    return lambda f: _detail(variant, f, part)


def _detail(variant, f, part):
    global part_legs
    im = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    part_legs = part == 'legs'; g = frame_grid(variant, 6, f); part_legs = False
    pals = {
        'base': {'e': (22, 26, 44)},                                              # глаза — поверх тела
        'legs': {'m': (42, 47, 69)},                                              # ноги — позади тела: верх прячется под одеждой
        'lamp': {'f': (58, 63, 77), 'l': (255, 216, 107), 'i': (255, 251, 224)},
        'lampoff': {'f': (58, 63, 77), 'l': (72, 78, 104), 'i': (112, 120, 150)},
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
                if c == 'e' and g[y - 1][x] != 'e':
                    col = (236, 240, 255)          # блик сверху: большие живые глаза
                im.putpixel((x, y), col + (255,))
    return im


IDLE = os.path.join(LOOK, 'Idle'); WALK = os.path.join(LOOK, 'Walk')
os.makedirs(IDLE, exist_ok=True); os.makedirs(WALK, exist_ok=True)


def breathe(frame):
    """Кадр покоя: верхняя часть (голова, корпус, лампа) на пиксель ниже, ноги стоят — лёгкое дыхание."""
    out = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    out.alpha_composite(frame.crop((0, 0, W, 14)), (0, 1))
    out.alpha_composite(frame.crop((0, 14, W, H)), (0, 14))
    return out


def save(name, img):
    """Каждый слой — два листа по две плитки 16×16: Walk/ (кадр 0 стоя, кадр 1 шаг) и Idle/ (кадр 0 стоя, кадр 1 вдох). Игра берёт папку по состоянию героя."""
    img.save(os.path.join(WALK, name))
    f0 = img.crop((0, 0, W, H)); idle = Image.new('RGBA', (W * 2, H), (0, 0, 0, 0)); idle.paste(f0, (0, 0)); idle.paste(breathe(f0), (W, 0))
    idle.save(os.path.join(IDLE, name))


save('skin.png', make_sheet(skin_layer(0)))
for sp in (1, 2):
    save(f'skin_s{sp}.png', make_sheet(skin_layer(sp)))
    save(f'face_s{sp}.png', make_sheet(face_layer(sp)))
for v in VARIANTS:
    save(f'detail_v{v}.png', make_sheet(detail_layer(v)))
    save(f'legs_v{v}.png', make_sheet(detail_layer(v, 'legs')))
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
