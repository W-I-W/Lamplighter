"""Sprites of items held in the hand -> Assets/Sprites/Held/held_<kind>.png (16x16, transparent).
Every sprite points RIGHT (grip on the left, working end on the right), so the game rotates all kinds the same way (no per-kind base angle).
Kinds: pickaxe, axe, shovel, hammer, sword, bow, staff. Run from the project root: python tools/GraphicsGenerator/make_held_sprites.py
Replace any PNG with your own 16x16 art that also points right to restyle it. A preview sheet is written next to the script when --preview is passed."""
import os, sys
from PIL import Image

OUT = os.path.join(os.path.dirname(__file__), '..', '..', 'Assets', 'Sprites', 'Held')
K = (36, 26, 20, 255)
C = {'w': (107, 68, 35, 255), 'W': (168, 116, 61, 255), 's': (184, 192, 204, 255), 'S': (232, 238, 246, 255), 'd': (122, 132, 148, 255),
     'g': (232, 184, 74, 255), 'G': (255, 224, 130, 255), 'c': (236, 224, 196, 255), 'p': (176, 112, 255, 255), 'P': (224, 192, 255, 255), 'r': (200, 66, 63, 255)}


class Canvas:
    def __init__(self): self.px = {}
    def set(self, x, y, c):
        if 0 <= x < 16 and 0 <= y < 16: self.px[(x, y)] = C[c]
    def rect(self, x0, y0, x1, y1, c):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1): self.set(x, y, c)
    def hline(self, x0, x1, y, c):
        for x in range(x0, x1 + 1): self.set(x, y, c)
    def vline(self, x, y0, y1, c):
        for y in range(y0, y1 + 1): self.set(x, y, c)
    def image(self):
        im = Image.new('RGBA', (16, 16), (0, 0, 0, 0)); fill = dict(self.px)
        for (x, y) in list(fill):                                        # one-pixel dark outline around the shape (4-neighbourhood)
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                q = (x + dx, y + dy)
                if 0 <= q[0] < 16 and 0 <= q[1] < 16 and q not in self.px and q not in fill: fill[q] = K
        for p, c in fill.items(): im.putpixel(p, c)
        return im


def handle(cv, x0, x1, y=7):
    cv.hline(x0, x1, y, 'W'); cv.hline(x0, x1, y + 1, 'w')


def pickaxe():
    cv = Canvas(); handle(cv, 1, 12)
    for dy in range(-5, 6):                                              # crescent head bent back toward the handle
        x = 13 - (dy * dy) // 8; y = 7 + dy
        cv.set(x, y, 'S' if dy < -1 else 's' if dy < 3 else 'd'); cv.set(x - 1, y, 's' if dy < 3 else 'd')
    cv.set(14, 7, 'S'); cv.set(14, 8, 's')                               # sharp tip pointing forward
    return cv


def axe():
    cv = Canvas(); handle(cv, 1, 11)
    for y in range(2, 11):                                               # one-sided bearded blade: edge bulges forward, mostly on the upper side
        xl = 9 if 5 <= y <= 8 else 10 if y in (4, 9) else 11
        xr = 15 - int((y - 5.5) ** 2) // 4
        for x in range(xl, xr + 1): cv.set(x, y, 'S' if x >= xr - 0 else 's' if y < 7 else 'd')
    cv.set(9, 6, 'd'); cv.set(9, 9, 'w')
    return cv


def shovel():
    cv = Canvas(); handle(cv, 1, 9)
    cv.rect(0, 6, 1, 9, 'W'); cv.set(0, 6, 'w'); cv.set(0, 9, 'w')       # D-grip
    for y in range(4, 12):                                               # rounded scoop
        r = 3 - abs(y - 7.5) / 2.0
        for x in range(10, 15):
            if x - 10 <= r + 1.5: cv.set(x, y, 'S' if y < 7 else 's' if y < 9 else 'd')
    return cv


def hammer():
    cv = Canvas(); handle(cv, 1, 11)
    cv.rect(10, 3, 14, 12, 's'); cv.rect(10, 3, 14, 5, 'S'); cv.rect(10, 10, 14, 12, 'd'); cv.vline(14, 4, 11, 'S')
    return cv


def sword():
    cv = Canvas()
    cv.hline(0, 3, 7, 'W'); cv.hline(0, 3, 8, 'w'); cv.set(0, 6, 'G'); cv.set(0, 9, 'g')   # grip and pommel
    cv.vline(4, 4, 11, 'g'); cv.vline(5, 5, 10, 'G')                                       # cross-guard
    cv.hline(6, 14, 7, 'S'); cv.hline(6, 14, 8, 's'); cv.set(15, 7, 'S'); cv.set(15, 8, 's'); cv.hline(6, 13, 6, 'd'); cv.hline(6, 13, 9, 'd')
    cv.set(14, 6, 'd'); cv.set(14, 9, 'd')
    return cv


def bow():
    cv = Canvas()
    for dy in range(-7, 8):
        x = 14 - (dy * dy) // 7; y = 8 + dy
        cv.set(x, y, 'W'); cv.set(x - 1, y, 'w')
    cv.vline(6, 2, 14, 'c')                                              # string
    cv.hline(2, 12, 8, 'c'); cv.set(13, 8, 'S'); cv.set(2, 7, 'r'); cv.set(2, 9, 'r')   # nocked arrow
    return cv


def staff():
    cv = Canvas(); handle(cv, 0, 11)
    cv.vline(11, 5, 10, 'g'); cv.vline(12, 5, 10, 'G')                   # collar holding the orb
    for y in range(4, 12):
        for x in range(12, 16):
            if (x - 13.5) ** 2 + (y - 7.5) ** 2 <= 7.5: cv.set(x, y, 'P' if (x + y) % 4 == 0 and y < 8 else 'p')
    cv.set(13, 6, 'P'); cv.set(14, 6, 'P'); cv.set(13, 7, 'P')
    return cv


KINDS = {'pickaxe': pickaxe, 'axe': axe, 'shovel': shovel, 'hammer': hammer, 'sword': sword, 'bow': bow, 'staff': staff}

if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    imgs = {}
    for name, fn in KINDS.items():
        im = fn().image(); imgs[name] = im; im.save(os.path.join(OUT, f'held_{name}.png')); print('wrote', name)
    if '--preview' in sys.argv:
        sheet = Image.new('RGBA', (16 * len(imgs) + 4 * (len(imgs) + 1), 24), (70, 90, 70, 255))
        for i, im in enumerate(imgs.values()): sheet.paste(im, (4 + i * 20, 4), im)
        sheet = sheet.resize((sheet.width * 8, sheet.height * 8), Image.NEAREST)
        sheet.save(sys.argv[sys.argv.index('--preview') + 1])
