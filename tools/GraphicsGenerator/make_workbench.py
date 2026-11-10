"""Рабочий стол (верстак): один PNG 16x16 в Assets/Sprites/Buildings, индекс атласа 206.
Запуск: python tools/GraphicsGenerator/make_workbench.py
Палитра общая с остальными постройками (контур #14110c, дерево #8a5a2e, металл #74747f)."""
import os
from PIL import Image

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..')
OUT = os.path.join(ROOT, 'Assets', 'Sprites', 'Buildings')


def C(h, a=255):
    h = h.lstrip('#'); return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)


K = C('14110c')
WL, WM, WD, WX = C('a8743d'), C('8a5a2e'), C('6b4423'), C('3a2512')
ML, MM, MD, MW = C('9a9aa6'), C('74747f'), C('5d5d68'), C('cfd6e2')
RED, REDD = C('a8452e'), C('6e2a1c')
PA = C('e8dcc0')

im = Image.new('RGBA', (16, 16), (0, 0, 0, 0))


def px(x, y, c):
    if 0 <= x < 16 and 0 <= y < 16: im.putpixel((x, y), c)


def rect(x0, y0, x1, y1, fill, edge=None):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            px(x, y, edge if edge and (x in (x0, x1) or y in (y0, y1)) else fill)


# столешница и ножки
rect(0, 8, 15, 10, WM, K); [px(x, 9, WL) for x in range(1, 15)]
rect(1, 11, 14, 11, WD, K)
for x in (1, 12):
    rect(x, 12, x + 2, 15, WD, K); px(x + 1, 13, WX)
rect(4, 13, 11, 13, WX)                                  # нижняя перекладина
# тиски слева
rect(1, 5, 4, 7, MM, K); px(2, 5, ML); px(3, 6, MD); rect(2, 3, 3, 4, MD, K); px(2, 3, MW)
# доска с заготовкой и пила справа
rect(6, 6, 10, 7, PA, K); px(7, 6, WL); px(9, 6, WL)
rect(12, 4, 14, 5, MM, K); px(12, 4, MW); [px(x, 6, ML) for x in (12, 13, 14)]; px(14, 7, REDD)
rect(13, 6, 13, 7, RED, K)                               # рукоять
# молоток
rect(6, 3, 8, 4, MM, K); px(6, 3, MW); px(7, 5, WD); px(7, 6, WD)

os.makedirs(OUT, exist_ok=True)
im.save(os.path.join(OUT, '206_Workbench.png'))
print('206_Workbench.png')
