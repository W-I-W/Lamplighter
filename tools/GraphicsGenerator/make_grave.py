"""Grave tile -> Assets/Sprites/Environment/177_Grave.png (16x16, solid object: a headstone with a mound).
Run from the project root: python tools/GraphicsGenerator/make_grave.py. Replace the PNG with your own art of the same size to restyle it."""
from PIL import Image

OUT, STONE, STONE_L, STONE_D = (22, 22, 30, 255), (150, 154, 168, 255), (186, 190, 204, 255), (104, 108, 124, 255)
DIRT, DIRT_L, DIRT_D = (92, 66, 44, 255), (124, 92, 60, 255), (62, 44, 30, 255)
CROSS = (210, 214, 230, 255)
im = Image.new('RGBA', (16, 16), (0, 0, 0, 0))


def put(x, y, c):
    if 0 <= x < 16 and 0 <= y < 16: im.putpixel((x, y), c)


for y in range(10, 15):                                   # burial mound
    half = 7 - (14 - y) // 2 if y < 14 else 7
    for x in range(8 - half - 1, 8 + half + 1):
        edge = x in (8 - half - 1, 8 + half)
        put(x, y, OUT if edge or y == 14 else DIRT_D if y == 13 else DIRT_L if y == 10 and x < 8 else DIRT)
for x in range(2, 14): put(x, 14, OUT)
for y in range(1, 12):                                    # headstone
    top = y < 4
    w = 3 if not top else (1 + y // 2) if y >= 1 else 1
    for x in range(8 - w - 1, 8 + w + 1):
        edge = x == 8 - w - 1 or x == 8 + w
        put(x, y, OUT if edge or y == 1 else STONE_D if x >= 8 + w - 1 else STONE_L if x == 8 - w else STONE)
for y in range(4, 8): put(8, y, CROSS)                    # engraved cross
for x in (7, 9): put(x, 5, CROSS)
im.save('Assets/Sprites/Environment/177_Grave.png')
print('grave tile written')
