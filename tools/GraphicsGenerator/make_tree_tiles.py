"""Two-tile trees -> Assets/Sprites/Environment/005_Tree, 111_Pine, 079_DeadTree (trunk tile, 16x16: solid, bottom cell)
and 162_TreeTop, 163_PineTop, 164_DeadTop (crown tile, 16x16: drawn in the cell above the trunk, above the player, no collider).
Run from the project root: python tools/GraphicsGenerator/make_tree_tiles.py, then python tools/GraphicsGenerator/make_foliage_poses.py (bent crown poses).
Replace any PNG with your own art of the same size to restyle it."""
from PIL import Image
import math

ENV = 'Assets/Sprites/Environment/'
OUT, MID, BASE, LIGHT, HI = (20, 40, 26, 255), (36, 90, 40, 255), (45, 107, 47, 255), (74, 160, 68, 255), (98, 187, 90, 255)
G1, G2, G3 = (59, 138, 58, 255), (45, 107, 47, 255), (36, 90, 40, 255)
BARK, BARK_D, BARK_L = (107, 68, 35, 255), (79, 49, 24, 255), (140, 92, 50, 255)
PINE, PINE_D, PINE_O, SNOW, SNOW_D = (42, 90, 58, 255), (30, 66, 44, 255), (16, 36, 26, 255), (240, 246, 252, 255), (223, 232, 242, 255)
PBARK = (74, 51, 32, 255)
DEAD_O, DEAD, DEAD_L = (13, 8, 7, 255), (42, 31, 26, 255), (58, 44, 38, 255)


def canvas():
    return Image.new('RGBA', (16, 32), (0, 0, 0, 0))


def put(im, x, y, c):
    if 0 <= x < 16 and 0 <= y < 32: im.putpixel((x, y), c)


def deciduous():
    im = canvas()
    cx, cy, rx, ry = 7.5, 11.5, 7.6, 11.0
    for y in range(32):
        for x in range(16):
            d = ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2
            if d <= 1.0:
                edge = d > .72
                c = OUT if d > .86 else (MID if edge else BASE)
                if not edge and d <= .72:
                    lx, ly = (x - cx) / rx, (y - cy) / ry
                    c = HI if (lx + ly) < -.95 else LIGHT if (lx + ly) < -.45 else G1 if (lx + ly) < .35 else G2
                put(im, x, y, c)
    for x, y in [(5, 8), (9, 6), (6, 14), (10, 12), (4, 17), (11, 17), (8, 10), (7, 19)]: put(im, x, y, MID)       # leaf clumps
    for x, y in [(5, 5), (7, 4), (4, 9)]: put(im, x, y, HI)
    for y in range(22, 32):                                                                                         # trunk
        w = 2 if y < 29 else 3
        for x in range(8 - w, 8 + w):
            put(im, x, y, BARK_D if x == 8 - w else BARK_L if x == 8 - w + 1 and y < 29 else BARK)
        if y >= 29: put(im, 8 - w - 1, y, BARK_D); put(im, 8 + w, y, BARK_D)
    for x in range(6, 10): put(im, x, 22, OUT)
    return im


def pine():
    im = canvas()
    tiers = [(1, 9, 3), (7, 15, 5), (13, 22, 7)]            # (top row, bottom row, half width at the bottom)
    for top, bot, hw in tiers:
        for y in range(top, bot + 1):
            t = (y - top) / max(1, bot - top)
            w = max(0, round(hw * t) if y > top else 0)
            for x in range(8 - w - 1, 8 + w + 1):
                inner = abs(x - 7.5) <= w + .5
                if not inner: continue
                edge = abs(x - 7.5) > w - .5
                put(im, x, y, PINE_O if (edge or y == bot) else PINE_D if x >= 8 else PINE)
        for x in range(8 - hw // 2 - 1, 8 + hw // 2 + 1): put(im, x, top + 1, SNOW)
        put(im, 7, top, SNOW); put(im, 8, top, SNOW_D)
    for y in range(23, 32):
        for x in (7, 8): put(im, x, y, PBARK)
        if y >= 30: put(im, 6, y, PBARK); put(im, 9, y, PBARK)
    return im


def dead():
    im = canvas()
    for y in range(13, 32):                                  # trunk with a slight bend
        x = 7 if y > 18 else 8
        for dx in (0, 1): put(im, x + dx - 1, y, DEAD_L if dx == 1 and y % 3 else DEAD)
        put(im, x - 2, y, DEAD_O); put(im, x + 1, y, DEAD_O)
    for y in range(29, 32): put(im, 5, y, DEAD_O); put(im, 10, y, DEAD_O); put(im, 6, y, DEAD); put(im, 9, y, DEAD)
    branches = [((7, 17), (3, 12), (2, 9)), ((8, 14), (12, 10), (13, 7)), ((8, 10), (6, 6), (6, 3)), ((7, 22), (11, 19), (12, 17))]
    for pts in branches:
        for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
            n = max(abs(x1 - x0), abs(y1 - y0))
            for i in range(n + 1):
                x = round(x0 + (x1 - x0) * i / n); y = round(y0 + (y1 - y0) * i / n)
                put(im, x, y, DEAD); put(im, x, y - 0, DEAD)
    return im


def save(im, top_name, bottom_name):
    im.crop((0, 0, 16, 16)).save(ENV + top_name + '.png')
    im.crop((0, 16, 16, 32)).save(ENV + bottom_name + '.png')


save(deciduous(), '162_TreeTop', '005_Tree')
save(pine(), '163_PineTop', '111_Pine')
save(dead(), '164_DeadTop', '079_DeadTree')
print('tree tiles written')
