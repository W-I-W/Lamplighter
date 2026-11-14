"""Природное окружение художественного прохода: кусты, трава, цветы, руины, ледяной блок, хвойные и мёртвые деревья.
Запускается из make_art_pass.py (TILES — кадры 16x16, write_trees — большие тайлы деревьев 32x32 с 4 кадрами качания)."""
import os
from PIL import Image
from artlib import art, overlay, rgba, ROOT

TILES = {}


def pad(rows14):
    return ['.' + r + '.' for r in rows14]


GREEN = {'1': 'b4ea6c', '2': '6cc24a', '3': '3e8c3c', '4': '26622f', 'o': '143a22'}

# куст из трёх лиственных шапок, с ягодами; по 1 px запаса по бокам, чтобы позы ветра не упирались в край
TILES['131_Bush'] = art('bush', ['................', '................'] + pad([
    '...oooo..ooo..',
    '..o1122oo122o.',
    '.o112223o1223o',
    'o1122223o2223o',
    'o122223o32233o',
    'o222231122333o',
    'o222311222333o',
    'o22r3122223r4o',
    'o223312222334o',
    'o333432223344o',
    '.o33443333444o',
    '.o4444434444o.',
    '..oo444444oo..',
    '....oooooo....']), dict(GREEN, r='ff6a78'))

TILES['130_GrassTuft'] = art('tuft', [
    '................',
    '................',
    '................',
    '................',
    '................',
    '................',
    '................',
    '................',
    '................',
    '................',
    '...1...1....1...',
    '...2...2...12...',
    '..12..12.1.22.1.',
    '..22.122.2123.2.',
    '.123222332233223',
    '..3343343343343.'], GREEN)

TILES['133_TallGrass'] = art('tallgrass', [
    '................',
    '................',
    '................',
    '........1.......',
    '...1....1...1...',
    '...1...12...1...',
    '...2...12..12...',
    '..12.1.22..12.1.',
    '..12.1.22.122.1.',
    '..22.2122.122.2.',
    '.122.2122.22212.',
    '.122122221222122',
    '.223122322223223',
    '.223223322323223',
    '.233233323323233',
    '..3343343343343.'], GREEN)

TILES['132_Flowers'] = art('flowers', [
    '................',
    '................',
    '................',
    '................',
    '................',
    '................',
    '................',
    '...p.......w....',
    '..pcp.....wcw...',
    '...p...y...w....',
    '...2..yky..2....',
    '...2...y...2..1.',
    '..12.1.2..12..2.',
    '..22.2.2.122.12.',
    '.123222322232232',
    '..3343343343343.'], dict(GREEN, p='ff8fb4', c='ffe27a', w='f6f6ff', y='ffd24a', k='e8862a'))

# ───────────────────────── руины ─────────────────────────
COLUMN = [  # обломок колонны на плите, поросший мхом (g, G)
    '................',
    '................',
    '....oo...oo.....',
    '...o11ooo12o....',
    '...o1122223o....',
    '...o1222233o....',
    '....o12233o.....',
    '....o1g233o.....',
    '....ogG233o.....',
    '....o1g233o.....',
    '....o12233o.....',
    '....o1223Go.....',
    '...oo12gGGoo....',
    '..o1122gG3333o..',
    '..o2223333344o..',
    '...oooooooooo...']
WALL = [    # остаток кирпичной стены с обвалившимся верхом
    '................',
    '................',
    '................',
    '................',
    '..ooo.....oo....',
    '.o112o...o12ooo.',
    '.o1222ooo122223o',
    '.o1222o122222o3o',
    '.o2223o222223o3o',
    '.ooooooooooooooo',
    '.o12o122223o123o',
    '.o23o222233o233o',
    '.ooooooooooooooo',
    '.o1223o12223o23o',
    '.o2333o23333o33o',
    '.ooooooooooooooo']
TILES['017_Ruin'] = art('ruin', COLUMN, {'1': 'd9dce6', '2': 'aeb2c2', '3': '82869a', '4': '5d6077', 'o': '1d1d2e', 'g': '7ccc52', 'G': '3e8c3c'})
TILES['083_AshRuin'] = art('ashruin', overlay(WALL, [
    '................', '................', '................', '................', '................', '................',
    '................', '.........e......', '................', '................', '.......e........', '..............e.',
    '................', '...e............', '..........e.....', '................']),
    {'1': '9a8478', '2': '6f5c54', '3': '4d3f3c', 'o': '160e10', 'e': 'ff9a2e'})
TILES['112_IceRuin'] = art('iceruin', overlay(WALL, [
    '................', '................', '................', '................', '..sss.....ss....', '.ssss....sss.ss.',
    '.s.......s......', '................', '................', '................', '................', '................',
    '................', '................', '................', '................']),
    {'1': 'cfe0f2', '2': '93a9c8', '3': '63799e', 'o': '16223c', 's': 'ffffff'})


def ice_block():
    """Ледяная глыба: светлые верхняя и левая грани, тёмный низ, диагональные блики."""
    pal = {'1': 'ecf9ff', '2': 'aadef8', '3': '70b6e2', '4': '4a8ec6', 'w': 'ffffff', 'o': '285a96'}
    glint = {(2, 2), (3, 3), (4, 4), (3, 2), (10, 5), (11, 6), (4, 9), (5, 10), (10, 11)}
    rows = []
    for y in range(16):
        r = ''
        for x in range(16):
            edge = x in (0, 15) or y in (0, 15)
            if edge: c = '.' if (x in (0, 15) and y in (0, 15)) else 'o'
            elif (x, y) in glint: c = 'w'
            elif x >= 13 and y >= 13: c = '4'
            elif y >= 13 or x >= 13: c = '3'
            elif x == 1 or y == 1: c = '1'
            else: c = '2'
            r += c
        rows.append(r)
    return art('iceblock', rows, pal)


TILES['113_IceBlock'] = ice_block()


# ───────────────────────── деревья 32x32 ─────────────────────────
def outline(g, ch='o', w=32, h=32):
    add = []
    for y in range(h):
        for x in range(w):
            if g[y][x] != '.': continue
            if any(0 <= x + dx < w and 0 <= y + dy < h and g[y + dy][x + dx] not in ('.', ch) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))): add.append((x, y))
    for x, y in add: g[y][x] = ch


def grid_img(name, g, pal, w=32, h=32):
    return art(name, [''.join(r) for r in g], pal, w, h)


PINE = {'1': '5fa873', '2': '3f8458', '3': '2c6446', '4': '1f4a36', '5': '153428', 'o': '0c2018',
        's': 'ffffff', 'S': 'c9d8ea', 't': '8a5f3c', 'T': '634127', 'u': '452b1c'}


def pine(tiers, cut=23, trunk=2):
    """Ель: ярусы хвои (верх, низ, полуширина) с зубчатым краем, снегом на верхушках ярусов и тенью под каждым ярусом; ствол с объёмом."""
    g = [['.'] * 32 for _ in range(32)]; cx = 16
    for y in range(cut - 1, 32):                                   # ствол (нижний ярус его частично закрывает)
        half = trunk + (1 if y >= 30 else 0)
        for x in range(cx - half, cx + half): g[y][x] = 't' if x < cx - half + 1 else 'u' if x >= cx + half - 1 else 'T'
    for ti in range(len(tiers) - 1, -1, -1):
        yt, yb, hw = tiers[ti]
        for y in range(yt, yb + 1):
            t = (y - yt) / max(1, yb - yt); half = max(1, round(1 + (hw - 1) * t))
            for x in range(cx - half, cx + half):
                if y == yb and (x - cx) % 3 == 0 and abs(x - cx) > 1: continue          # зубцы хвои по нижнему краю
                rel = (x - cx + .5) / half
                c = '2' if rel < -.45 else '3' if rel < .3 else '4'
                if x == cx - half and y % 2 == 0: c = '1'                                 # блик на левой кромке
                if y >= yb - 1 and c != '1': c = '4' if c in ('2', '3') else '5'          # низ яруса темнее
                if y - yt < 2 + (1 if hw > 8 else 0):                                     # снежная шапка
                    c = 's' if rel < .25 else 'S'
                elif y - yt == 2 + (1 if hw > 8 else 0) and (x + y) % 3 == 0 and rel < .2: c = 's'
                g[y][x] = c
        if ti > 0:                                                                        # тень верхнего яруса на нижнем уже учтена порядком отрисовки
            pass
    # тень под каждым ярусом (кроме нижнего): строка сразу под ним; ниже — снег на кончиках лап следующего яруса
    for yt, yb, hw in tiers[:-1]:
        for x in range(cx - hw, cx + hw):
            if g[yb + 1][x] in ('2', '3', '4', 's', 'S', '1'): g[yb + 1][x] = '5'
        for y in (yb + 3, yb + 4):
            xs = [x for x in range(32) if g[y][x] != '.']
            if len(xs) < 6: continue
            g[y][xs[0]] = 's'; g[y][xs[0] + 1] = 's'
            if y == yb + 3: g[y][xs[0] + 2] = 'S'; g[y][xs[-1]] = 'S'
    outline(g)
    return grid_img('pine', g, PINE)


DEAD = {'1': '6e5648', '2': '4a372e', '3': '30231e', 'o': '120b0a'}


def dead(branches, flip=False):
    """Мёртвое дерево: ствол и ветви — ломаные линии с толщиной; свет слева, тень справа, тёмный контур."""
    mask = [[False] * 32 for _ in range(32)]
    for pts in branches:
        for (x0, y0, w0), (x1, y1, w1) in zip(pts, pts[1:]):
            n = max(abs(x1 - x0), abs(y1 - y0)) * 2 + 1
            for i in range(n + 1):
                t = i / n; x = x0 + (x1 - x0) * t; y = y0 + (y1 - y0) * t; w = w0 + (w1 - w0) * t
                for k in range(max(1, round(w))):
                    xi = int(round(x - w / 2 + k + .5)) if w > 1 else int(round(x)); yi = int(round(y))
                    if flip: xi = 31 - xi
                    if 2 <= xi < 30 and 1 <= yi < 32: mask[yi][xi] = True
    g = [['.'] * 32 for _ in range(32)]
    for y in range(32):
        for x in range(32):
            if not mask[y][x]: continue
            left = x > 0 and mask[y][x - 1]; right = x < 31 and mask[y][x + 1]
            g[y][x] = '1' if not left and right else '3' if not right and left else '2' if left else ('1' if (x + y) % 2 == 0 else '2')
    for y in range(31, 0, -1):                                                            # редкие борозды коры на толстом стволе
        for x in range(32):
            if g[y][x] == '2' and mask[y][x - 1] and mask[y][x + 1] and (x * 7 + y * 3) % 11 == 0: g[y][x] = '3'
    outline(g)
    return grid_img('dead', g, DEAD)


TRUNK = [(16, 31, 6), (16, 28, 4.4), (15.5, 21, 4), (15, 15, 3), (15.5, 9, 2), (15, 5, 1)]
DEAD_A = [TRUNK, [(14, 19, 2), (10, 14, 2), (8, 9, 1), (9, 5, 1)], [(10, 14, 1), (6, 12, 1)], [(17, 16, 2), (21, 11, 1.6), (23, 6, 1), (22, 3, 1)], [(21, 11, 1), (25, 10, 1)],
          [(15, 9, 1), (12, 5, 1), (12, 3, 1)], [(16, 23, 1), (19, 20, 1)]]
DEAD_THICK = [[(16, 31, 8), (16, 28, 6), (15.5, 21, 5), (15, 14, 4), (15.5, 9, 2.4), (16, 4, 1)], [(13, 20, 2.4), (8, 15, 2), (5, 10, 1.4), (6, 6, 1)], [(8, 15, 1), (4, 14, 1)],
              [(18, 17, 2.4), (23, 12, 2), (26, 7, 1), (25, 4, 1)], [(23, 12, 1), (27, 12, 1)], [(15, 10, 1.4), (11, 6, 1), (10, 3, 1)], [(16, 9, 1), (20, 5, 1), (20, 3, 1)], [(13, 25, 1), (10, 23, 1)]]

SMAX = [0, 2, 0, -2]       # размах верхушки по кадрам (как в make_tree_sway.py)


def sway_strip(src, cut):
    strip = Image.new('RGBA', (32 * len(SMAX), 32), (0, 0, 0, 0))
    for k, smax in enumerate(SMAX):
        for y in range(32):
            s = round(smax * ((cut - y) / cut) ** 1.2) if y < cut else round(smax * .15) if y < cut + 2 else 0
            strip.paste(src.crop((0, y, 32, y + 1)), (32 * k + s, y))
    return strip


def trees():
    return {
        '184_Pine_big': (pine([(2, 9, 5), (7, 15, 8), (13, 22, 10)]), 23),
        '192_FatPine_big': (pine([(3, 10, 6), (8, 16, 9), (13, 22, 12)]), 23),
        '194_TallPine_big': (pine([(1, 6, 3), (4, 10, 5), (8, 14, 6), (12, 18, 7), (16, 22, 8)]), 23),
        '186_DeadTree_big': (dead(DEAD_A), 21),
        '196_DeadFlip_big': (dead(DEAD_A, flip=True), 21),
        '198_DeadThick_big': (dead(DEAD_THICK), 21),
    }


def write_trees():
    for name, (im, cut) in trees().items():
        bb = im.getbbox(); assert bb[0] >= 2 and bb[2] <= 30, f'{name}: no room for the sway {bb}'      # качание сдвигает крону до 2 px: запас по бокам обязателен
        sway_strip(im, cut).save(os.path.join(ROOT, 'Environment', name + '.png'))
