"""Decorative foliage tiles (visual only, no gameplay) -> Assets/Sprites/Environment/130..133_*.png (16x16, transparent background).
They are placed on forest grass by the chunk loader and swayed by the wind shader (Assets/Shaders/Vegetation/sway.gdshader).
Run from the project root: python tools/GraphicsGenerator/make_foliage.py (replace the PNGs with your own art of the same size to restyle)."""
from PIL import Image

OUT = 'Assets/Sprites/Environment/'
D, M, L, H = (30, 78, 40, 255), (84, 160, 70, 255), (130, 205, 92, 255), (186, 236, 130, 255)
INK = (24, 52, 34, 255)
PINK, YEL, WHT = (240, 130, 170, 255), (255, 214, 90, 255), (236, 240, 250, 255)

def new(): return Image.new('RGBA', (16, 16), (0, 0, 0, 0))

def blade(im, x, y0, y1, col, lean=0):
    for i, y in enumerate(range(y0, y1 - 1, -1)):
        xx = x + (lean if i >= (y0 - y1) // 2 else 0)
        if 0 <= xx < 16: im.putpixel((xx, y), col)

def tuft(tall):
    im = new()
    top = 7 if tall else 10
    for x, y1, col, lean in [(4, top + 1, M, -1), (6, top, L, 0), (8, top - 1 if tall else top, H, 0), (10, top + 1, L, 1), (12, top + 2, M, 1), (7, top + 2, D, 0), (9, top + 2, D, 0)]:
        blade(im, x, 15, y1, col, lean)
    return im

def bush():
    im = new()
    for y in range(6, 15):
        for x in range(2, 14):
            cx, cy = 8, 10.5
            if ((x + .5 - cx) / 6.2) ** 2 + ((y + .5 - cy) / 4.6) ** 2 <= 1:
                im.putpixel((x, y), M if y > 9 else L)
    for x, y in [(5, 8), (6, 7), (9, 7), (10, 8), (8, 9), (4, 10)]: im.putpixel((x, y), H)
    for x in range(3, 13): im.putpixel((x, 14), D)
    for x, y in [(3, 13), (12, 13), (7, 12), (9, 13)]: im.putpixel((x, y), D)
    # outline
    px = im.load(); add = []
    for y in range(16):
        for x in range(16):
            if px[x, y][3] == 0 and any(0 <= x + dx < 16 and 0 <= y + dy < 16 and px[x + dx, y + dy][3] for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))): add.append((x, y))
    for x, y in add: px[x, y] = INK
    return im

def flowers():
    im = tuft(False)
    for x, y, c in [(5, 8, PINK), (9, 7, YEL), (12, 9, WHT)]:
        im.putpixel((x, y), c)
        im.putpixel((x, y + 1), L)
    return im

for name, img in [('130_GrassTuft', tuft(False)), ('131_Bush', bush()), ('132_Flowers', flowers()), ('133_TallGrass', tuft(True))]:
    img.save(OUT + name + '.png')
print('foliage tiles written')
