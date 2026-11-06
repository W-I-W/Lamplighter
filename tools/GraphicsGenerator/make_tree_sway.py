"""Кадры качания деревьев: Assets/Sprites/Environment/18x_*_big.png — лента 4 кадра по 32x32 (кадр 0 — исходный рисунок дерева).
Крона гнётся плавно: верхние строки смещаются сильнее, у основания кроны смещение 0, ствол стоит на месте, а верхние две строки ствола
повторяют сдвиг основания кроны, чтобы не было зазора. Запуск: python tools/GraphicsGenerator/make_tree_sway.py (кадр 0 берётся из текущих файлов)."""
from PIL import Image
import os

D = os.path.join(os.path.dirname(__file__), '..', '..', 'Assets', 'Sprites', 'Environment')
TREES = {'182_Tree_big.png': 22, '184_Pine_big.png': 23, '186_DeadTree_big.png': 21,
         '188_Oak_big.png': 22, '190_Poplar_big.png': 22, '192_FatPine_big.png': 23, '194_TallPine_big.png': 23, '196_DeadFlip_big.png': 21, '198_DeadThick_big.png': 21}   # строка, с которой начинается ствол
SMAX = [0, 2, 0, -2]       # размах верхушки по кадрам: центр, вправо, центр, влево


def frame(base, cut, smax):
    out = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    for y in range(32):
        row = base.crop((0, y, 32, y + 1))
        if y < cut:
            s = round(smax * ((cut - y) / cut) ** 1.2)           # верх сильнее, основание кроны — 0
        elif y < cut + 2:
            s = round(smax * .15)                                # самый верх ствола чуть следует за кроной
        else:
            s = 0
        out.paste(row, (s, y))
    return out


for name, cut in TREES.items():
    path = os.path.join(D, name)
    src = Image.open(path).convert('RGBA').crop((0, 0, 32, 32))
    strip = Image.new('RGBA', (32 * len(SMAX), 32), (0, 0, 0, 0))
    for k, sm in enumerate(SMAX):
        strip.paste(frame(src, cut, sm), (32 * k, 0))
    strip.save(path)
    print(name, strip.size)
