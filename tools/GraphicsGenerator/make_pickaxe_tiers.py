"""Pickaxe art per tier: stone, copper, iron, crystal, obsidian.
Writes Assets/Sprites/Items/icon_023_pickaxe.png (stone) + icon_030..033 (copper, iron, crystal, obsidian)
and Assets/Sprites/Held/held_pickaxe_0..4.png (+ held_pickaxe.png = stone). Run from the project root: python tools/GraphicsGenerator/make_pickaxe_tiers.py
The game swaps the pickaxe icon by tier (UiIcons.PickTier); replace any PNG with your own 16x16 art to restyle a tier."""
import os
from PIL import Image

ROOT = os.path.join(os.path.dirname(__file__), '..', '..', 'Assets', 'Sprites')
# (highlight, body, shade) of the head per tier
TIERS = [((196, 190, 178, 255), (142, 136, 126, 255), (96, 92, 86, 255)),      # stone
         ((255, 204, 150, 255), (216, 128, 72, 255), (146, 78, 44, 255)),      # copper
         ((244, 247, 252, 255), (178, 188, 202, 255), (108, 118, 134, 255)),   # iron
         ((214, 255, 255, 255), (108, 214, 238, 255), (56, 126, 190, 255)),    # crystal
         ((170, 122, 224, 255), (70, 52, 100, 255), (36, 28, 54, 255))]        # obsidian
ICON = ['................', '...aaaaaaaaaa...', '..abbbbbbbbbba..', '.acccccccccccca.', 'acccccccccccccca', 'accaaaaddaaaacca', '.aa...adda...aa.',
        '......adda......', '......adda......', '......adda......', '......adda......', '......adda......', '......adda......', '......adda......', '......adda......', '.......aa.......']
K = (26, 26, 34, 255); HANDLE = (138, 90, 46, 255); EMBER = (255, 140, 40, 255)
ICON_FILES = ['023_pickaxe', '030_pickaxe_copper', '031_pickaxe_iron', '032_pickaxe_crystal', '033_pickaxe_obsidian']


def icon(t):
    hi, body, sh = TIERS[t]; im = Image.new('RGBA', (16, 16), (0, 0, 0, 0))
    for y, row in enumerate(ICON):
        for x, ch in enumerate(row):
            if ch == '.': continue
            c = {'a': K, 'b': hi, 'c': body, 'd': HANDLE}[ch]
            if ch == 'c' and y == 5: c = sh                      # lower rim of the head in shadow
            if ch == 'c' and y == 4 and (x < 3 or x > 12): c = sh
            im.putpixel((x, y), c)
    if t == 4:
        for p in ((1, 4), (14, 4), (2, 3), (13, 3)): im.putpixel(p, EMBER)     # glowing tips of the obsidian head
    if t == 3:
        for p in ((5, 2), (10, 2)): im.putpixel(p, (255, 255, 255, 255))      # crystal glints
    return im


HELD = ['................', '.........aa.....', '........abca....', '.........abca...', '..........abca..', '...........abca.', '.aaaaaaaaaaabba.', 'adddddddddddbbca',
        'aeeeeeeeeeeebbba', '.aaaaaaaaaaabba.', '..........affa..', '.........affa...', '........affa....', '.........aa.....', '................', '................']
HK = (36, 26, 20, 255); HW = (168, 116, 61, 255); HW2 = (107, 68, 35, 255); HF = (122, 132, 148, 255)


def held(t):
    hi, body, sh = TIERS[t]; im = Image.new('RGBA', (16, 16), (0, 0, 0, 0)); m = {'a': HK, 'b': body, 'c': hi, 'd': HW, 'e': HW2, 'f': HF}
    for y, row in enumerate(HELD):
        for x, ch in enumerate(row):
            if ch != '.': im.putpixel((x, y), m[ch])
    if t == 4:
        for p in ((12, 6), (12, 9)): im.putpixel(p, EMBER)
    return im


if __name__ == '__main__':
    for t in range(5):
        icon(t).save(os.path.join(ROOT, 'Items', f'icon_{ICON_FILES[t]}.png'))
        held(t).save(os.path.join(ROOT, 'Held', f'held_pickaxe_{t}.png'))
    held(0).save(os.path.join(ROOT, 'Held', 'held_pickaxe.png'))
    print('ok')
