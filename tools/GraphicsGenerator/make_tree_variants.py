"""Новые виды деревьев (кадр 0, 32x32) на основе палитры и ствола базовых деревьев. Затем make_tree_sway.py делает ленту из 4 кадров.
Результат: 188_Oak, 190_Poplar (дерево), 192_FatPine, 194_TallPine (сосна), 196_DeadFlip, 198_DeadThick (мёртвое дерево) в Assets/Sprites/Environment.
Запуск: python tools/GraphicsGenerator/make_tree_variants.py && python tools/GraphicsGenerator/make_tree_sway.py"""
from PIL import Image
import os, random

D = os.path.join(os.path.dirname(__file__), '..', '..', 'Assets', 'Sprites', 'Environment')
def load0(name): return Image.open(os.path.join(D, name)).convert('RGBA').crop((0, 0, 32, 32))

def crown(base, rows_keep_from, w, y0, y1, pal, seed):
    """Эллиптическая крона шириной w в строках y0..y1 поверх ствола базового дерева (строки с rows_keep_from и ниже берутся из base)."""
    out = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    out.paste(base.crop((0, rows_keep_from, 32, 32)), (0, rows_keep_from))
    ol, dk, md, bs, lt, hl = pal
    h = y1 - y0 + 1; cy = (y0 + y1 + 1) / 2; rnd = random.Random(seed)
    inside = set()
    for y in range(y0, y1 + 1):
        for x in range(32):
            nx = (x + .5 - 16) / (w / 2); ny = (y + .5 - cy) / (h / 2)
            if nx * nx + ny * ny <= 1.0: inside.add((x, y))
    for (x, y) in inside:
        edge = any((x + dx, y + dy) not in inside for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
        if edge: out.putpixel((x, y), ol); continue
        nx = (x + .5 - 16) / (w / 2); ny = (y + .5 - cy) / (h / 2); l = -(nx * .6 + ny * .8)
        c = hl if l > .62 else lt if l > .3 else bs if l > -.15 else md if l > -.5 else dk
        if rnd.random() < .09: c = dk if c != dk else md
        out.putpixel((x, y), c)
    return out

def tri(out, cx, ytop, ybot, hw, pal, snow):
    ol, dk, md, sn1, sn2 = pal
    for y in range(ytop, ybot + 1):
        t = (y - ytop) / max(1, ybot - ytop); half = max(1, round(1 + (hw - 1) * t))
        for x in range(cx - half, cx + half):
            edge = x == cx - half or x == cx + half - 1 or y == ybot
            c = ol if edge else (md if x < cx else dk)
            if snow and not edge and (y - ytop) < 2 + (1 if hw > 8 else 0): c = sn1 if x < cx else sn2
            out.putpixel((x, y), c)

def pine(base, rows_keep_from, tiers, hws, pal):
    out = Image.new('RGBA', (32, 32), (0, 0, 0, 0)); out.paste(base.crop((0, rows_keep_from, 32, 32)), (0, rows_keep_from))
    for (yt, yb), hw in zip(tiers, hws): tri(out, 16, yt, yb, hw, pal, True)
    return out

tree = load0('182_Tree_big.png'); pn = load0('184_Pine_big.png'); dead = load0('186_DeadTree_big.png')
TP = [(20, 40, 26, 255), (36, 90, 40, 255), (45, 107, 47, 255), (59, 138, 58, 255), (74, 160, 68, 255), (98, 187, 90, 255)]
PP = [(16, 36, 26, 255), (30, 66, 44, 255), (42, 90, 58, 255), (240, 246, 252, 255), (223, 232, 242, 255)]

crown(tree, 22, 22, 4, 21, TP, 11).save(os.path.join(D, '188_Oak_big.png'))               # широкий дуб
crown(tree, 22, 10, 1, 21, TP, 12).save(os.path.join(D, '190_Poplar_big.png'))            # высокий тонкий тополь
pine(pn, 23, [(4, 11), (9, 17), (14, 22)], [6, 9, 11], PP).save(os.path.join(D, '192_FatPine_big.png'))        # пышная ель, три яруса
pine(pn, 23, [(1, 7), (5, 11), (9, 15), (13, 19), (17, 22)], [3, 5, 6, 7, 8], PP).save(os.path.join(D, '194_TallPine_big.png'))   # высокая тонкая ель, пять ярусов

# мёртвые деревья: зеркальное и «толстое» с дополнительными ветвями
dead.transpose(Image.FLIP_LEFT_RIGHT).save(os.path.join(D, '196_DeadFlip_big.png'))
th = Image.new('RGBA', (32, 32), (0, 0, 0, 0)); th.paste(dead, (0, 0))
for y in range(10, 32):
    row = dead.crop((0, y, 32, y + 1)); xs = [x for x in range(32) if row.getpixel((x, 0))[3] > 0]
    if xs and y >= 12:
        for x in range(min(xs), min(xs) + 2): pass
        edge = max(xs) + 1
        if edge < 32: th.putpixel((edge, y), dead.getpixel((max(xs), y)))          # на пиксель толще справа
rnd = random.Random(5); col = (42, 31, 26, 255)
for (x0, y0, dx, n) in [(14, 12, -1, 5), (17, 8, 1, 6), (15, 16, 1, 4)]:           # дополнительные ветви
    x, y = x0, y0
    for i in range(n):
        th.putpixel((x, y), col); x += dx; y -= 1 if i % 2 == 0 else 0
th.save(os.path.join(D, '198_DeadThick_big.png'))
print('ok')
