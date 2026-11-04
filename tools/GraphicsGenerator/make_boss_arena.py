"""Boss arena tiles -> Assets/Sprites/Environment/180_BossTotem.png and 181_BossGate.png (16x16 solid objects).
Totem: a carved stone pillar with a glowing violet eye; breaking it wakes the guardian. Gate: a barrier of violet light bars that seals an arena entrance during the fight.
Run from the project root: python tools/GraphicsGenerator/make_boss_arena.py. Replace the PNGs with your own art of the same size to restyle them."""
from PIL import Image

OUT = (22, 20, 34, 255)
ST, ST_L, ST_D = (118, 112, 140, 255), (160, 154, 184, 255), (80, 76, 102, 255)
GLOW, GLOW_L, GLOW_D = (190, 120, 255, 255), (236, 200, 255, 255), (120, 60, 190, 255)


def new():
    return Image.new('RGBA', (16, 16), (0, 0, 0, 0))


def totem():
    im = new()
    put = lambda x, y, c: im.putpixel((x, y), c) if 0 <= x < 16 and 0 <= y < 16 else None
    for y in range(14, 16):                                   # base slab
        for x in range(2, 14): put(x, y, OUT if y == 15 or x in (2, 13) else ST_D)
    for y in range(1, 14):                                    # pillar
        w = 4 if y > 2 else 3
        for x in range(8 - w - 1, 8 + w + 1):
            edge = x == 8 - w - 1 or x == 8 + w or y == 1
            put(x, y, OUT if edge else ST_D if x >= 8 + w - 1 else ST_L if x == 8 - w else ST)
    for dx in (-1, 0, 1):                                     # glowing eye
        for dy in (0, 1): put(8 + dx, 5 + dy, GLOW)
    put(8, 5, GLOW_L); put(7, 5, GLOW_D); put(9, 6, GLOW_D)
    for y in (8, 10, 12):                                     # carved runes
        put(6, y, ST_D); put(7, y, GLOW_D); put(9, y, GLOW_D); put(10, y, ST_D)
    return im


def gate():
    im = new()
    put = lambda x, y, c: im.putpixel((x, y), c) if 0 <= x < 16 and 0 <= y < 16 else None
    for x in range(0, 16):                                    # top and bottom beams
        for y in (0, 1, 14, 15): put(x, y, OUT if y in (0, 15) else ST_D)
    for bx in (2, 6, 9, 13):                                  # light bars
        for y in range(2, 14):
            put(bx, y, GLOW_L if y % 4 == 0 else GLOW); put(bx - 1, y, GLOW_D) if bx > 0 else None; put(bx + 1, y, GLOW_D) if bx < 15 else None
    for y in (5, 10):                                         # cross glow
        for x in range(0, 16):
            if im.getpixel((x, y))[3] == 0: put(x, y, (150, 90, 220, 140))
    return im


totem().save('Assets/Sprites/Environment/180_BossTotem.png')
gate().save('Assets/Sprites/Environment/181_BossGate.png')
print('boss arena tiles written')
