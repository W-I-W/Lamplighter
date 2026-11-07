"""Стол исследований: пять PNG 16x16 (по уровню стола) в Assets/Sprites/Buildings, индексы атласа 201..205.
Запуск: python tools/GraphicsGenerator/make_research_table.py
Стадии: I - дерево, книги и простые инструменты; II - металлические детали; III - кристальный стол;
IV - стол с магией пепла (ядро-уголь и руны); V - стол с магией мороза. Лента N x 16 по ширине = N кадров анимации (атлас кладёт их столбиком).
Палитра общая с остальными постройками (контур #14110c, дерево #8a5a2e, металл #74747f)."""
import os
from PIL import Image

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..')
OUT = os.path.join(ROOT, 'Assets', 'Sprites', 'Buildings')

def C(h, a=255):
    h = h.lstrip('#'); return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)

K = C('14110c')
WL, WM, WD, WX = C('a8743d'), C('8a5a2e'), C('6b4423'), C('3a2512')
ML, MM, MD, MX, MW = C('9a9aa6'), C('74747f'), C('5d5d68'), C('3a3f4d'), C('cfd6e2')
PA, PQ = C('e8dcc0'), C('bfae88')
RED, REDD, BLU, BLUD, GRN, GRND = C('a8452e'), C('6e2a1c'), C('3f6fa8'), C('264570'), C('4f8a4a'), C('2f5a2d')
FL, OR = C('ffe27a'), C('ff9a2e')
CY, CYM, CYD, CYW = C('8ff0ff'), C('4fb8d8'), C('2a7a9a'), C('e6ffff')
PU, PUD = C('d58cff'), C('9a5ad0')
OBS, OBL = C('2b2330'), C('453a52')
FR, FRM, FRD, FRW = C('bfe8ff'), C('7fc0f0'), C('4a86c0'), C('f2fbff')


class Canvas:
    def __init__(self): self.im = Image.new('RGBA', (16, 16), (0, 0, 0, 0))
    def px(self, x, y, c):
        if 0 <= x < 16 and 0 <= y < 16: self.im.putpixel((x, y), c)
    def rect(self, x0, y0, x1, y1, fill, edge=None):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.px(x, y, edge if edge and (x in (x0, x1) or y in (y0, y1)) else fill)
    def hline(self, x0, x1, y, c):
        for x in range(x0, x1 + 1): self.px(x, y, c)
    def vline(self, x, y0, y1, c):
        for y in range(y0, y1 + 1): self.px(x, y, c)


def table(cv, top, face, leg, apron):
    """Общая основа: столешница (ряды 9-11), царга и ножки (12-15)."""
    cv.hline(1, 14, 9, K)
    cv.rect(0, 10, 15, 11, face, K); cv.hline(1, 14, 10, top)
    cv.hline(1, 14, 12, K); cv.hline(2, 13, 12, apron)
    for x in (2, 12):
        cv.rect(x, 13, x + 1, 15, leg, K)
    cv.hline(3, 11, 13, MX if leg in (MM, MD) else WX)


def book(cv, x0, y0, x1, y1, col, dark):
    cv.rect(x0, y0, x1, y1, col, K); cv.hline(x0 + 1, x1 - 1, y0 + 1 if y1 - y0 > 1 else y0, dark)


def level1(f):
    cv = Canvas(); table(cv, WL, WM, WD, WX)
    book(cv, 1, 7, 7, 8, GRN, GRND); book(cv, 2, 5, 7, 6, RED, REDD); book(cv, 2, 3, 6, 4, BLU, BLUD)
    cv.rect(9, 7, 12, 8, PA, K); cv.hline(10, 11, 7, PQ); cv.px(13, 8, K)                 # свиток
    cv.vline(10, 4, 6, WD); cv.rect(9, 3, 11, 4, MM, K); cv.px(10, 3, ML)                   # молоточек
    cv.rect(14, 6, 14, 8, PA, K); cv.px(14, 5, FL if f == 0 else OR); cv.px(14, 4, OR if f == 0 else K)   # свеча
    return cv.im


def level2(f):
    cv = Canvas(); table(cv, ML, MM, MD, MD)
    for x in (1, 14): cv.px(x, 10, MW)
    book(cv, 1, 7, 6, 8, BLU, BLUD)
    cv.rect(8, 3, 12, 8, MM, K); cv.rect(9, 4, 11, 7, MD, MX); cv.px(10, 5, ML); cv.px(10, 6, ML)       # шестерня
    for x in (7, 13): cv.px(x, 5, MM); cv.px(x, 6, MM)
    cv.hline(9, 11, 2, MM); cv.px(10, 2, MW if f else ML)
    cv.rect(13, 6, 14, 8, CYM, K); cv.px(13, 6, CYW if f else CY)                                   # линза
    return cv.im


def crystal(cv, x, y, h, c1, c2, c3):
    for i in range(h):
        cv.px(x, y + i, c2); cv.px(x + 1, y + i, c1)
    cv.px(x, y - 1, c3); cv.px(x + 1, y - 1, c3) if h > 3 else None
    cv.px(x + 1, y, CYW)


def level3(f):
    cv = Canvas(); table(cv, ML, MM, MD, MD)
    cv.hline(1, 14, 10, CYM)
    for x in (3, 7, 11): cv.px(x, 10, CY if (x + f) % 3 else CYW)
    crystal(cv, 7, 3, 5, CY, CYM, CYD); crystal(cv, 4, 5, 3, CYM, CYD, CYD); crystal(cv, 10, 5, 3, CY, CYM, CYD)
    for x, y in ((7, 2), (8, 2)): cv.px(x, y, CYD)
    cv.hline(5, 12, 8, K); cv.hline(6, 11, 8, MD)
    cv.px(6 + f * 2, 4, CYW)
    return cv.im


def orb(cv, cx, cy, core, mid, rim, f):
    r = [(0, -1), (-1, 0), (0, 0), (1, 0), (0, 1)]
    for dx, dy in r: cv.px(cx + dx, cy + dy, mid)
    cv.px(cx, cy, core)
    for dx, dy in ((-1, -1), (1, -1), (-1, 1), (1, 1), (-2, 0), (2, 0), (0, -2), (0, 2)): cv.px(cx + dx, cy + dy, rim if (dx + dy + f) % 2 else K)


def level4(f):
    cv = Canvas(); table(cv, OBL, OBS, MD, MX)
    cv.hline(1, 14, 10, OBL)
    for i, x in enumerate((2, 5, 8, 11, 13)): cv.px(x, 10, OR if (i + f) % 4 else FL)
    cv.hline(4, 11, 8, K); cv.hline(5, 10, 8, MD)                                    # подставка
    orb(cv, 8, 4 + (1 if f in (1, 2) else 0), FL, OR, PUD, f)
    cv.rect(2, 6, 3, 8, OBS, K); cv.px(2, 6, OR); cv.rect(12, 6, 13, 8, OBS, K); cv.px(13, 6, FL if f % 2 else OR)   # обсидиановые обелиски
    for x, y in ((6, 2 + f % 2), (10, 1 + (f + 1) % 2)): cv.px(x, y, PU)
    return cv.im


def level5(f):
    cv = Canvas(); table(cv, FR, FRM, FRD, FRD)
    cv.hline(1, 14, 10, FRW)
    for i, x in enumerate((2, 5, 8, 11, 13)): cv.px(x, 10, FRD if (i + f) % 4 else FRW)
    cv.hline(4, 11, 8, K); cv.hline(5, 10, 8, FRD)
    orb(cv, 8, 4 + (1 if f in (1, 2) else 0), FRW, FR, PUD, f)
    for x, h in ((2, 3), (12, 3)):                                                    # ледяные шипы
        cv.rect(x, 8 - h + 1, x + 1, 8, FRM, K); cv.px(x, 8 - h + 1, FRW)
    for x, y in ((6, 2 + f % 2), (10, 1 + (f + 1) % 2)): cv.px(x, y, FRW)
    return cv.im


def strip(fn, n):
    im = Image.new('RGBA', (16 * n, 16), (0, 0, 0, 0))
    for f in range(n): im.paste(fn(f), (16 * f, 0))
    return im


if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    spec = [(201, 'ResTable', level1, 2), (202, 'ResTable_v1', level2, 2), (203, 'ResTable_v2', level3, 3), (204, 'ResTable_v3', level4, 4), (205, 'ResTable_v4', level5, 4)]
    for idx, name, fn, n in spec:
        strip(fn, n).save(os.path.join(OUT, f'{idx}_{name}.png')); print('wrote', idx, name, n, 'frames')
