"""Giant guardian tree landmark (Assets/Sprites/Landmarks/giant_tree.png), 80x96, pixel art.
The trunk base is 48 px wide and sits on the bottom 16 px row (three solid tiles); the crown is drawn over the player when they stand behind the tree.
Run: python tools/GraphicsGenerator/make_giant_tree.py"""
import math, os, random
from PIL import Image

W, H = 80, 96
img = Image.new('RGBA', (W, H), (0, 0, 0, 0))
px = img.load()
rnd = random.Random(7)

def put(x, y, c):
    if 0 <= x < W and 0 <= y < H:
        px[x, y] = c

DARK, MID, LIGHT, SUN = (22, 58, 38, 255), (36, 96, 52, 255), (74, 150, 70, 255), (150, 210, 110, 255)
BARK, BARK_D, BARK_L = (92, 60, 36, 255), (58, 38, 24, 255), (130, 90, 54, 255)

# trunk: wide roots at the bottom, narrowing upwards
for y in range(40, H):
    t = (y - 40) / (H - 41)
    half = 12 + 12 * (t ** 2.2)
    for x in range(W):
        d = abs(x - 40 + .5)
        if d <= half:
            c = BARK
            if int(x * 0.7 + y * 0.15) % 7 == 0: c = BARK_D
            if d < half * .35 and (x + y // 2) % 9 == 0: c = BARK_L
            if d > half - 1.5: c = BARK_D
            put(x, y, c)
# roots spreading on the ground line
for side in (-1, 1):
    for i in range(10):
        put(40 + side * (24 + i // 2), H - 1 - (i // 3), BARK_D)

# crown: overlapping blobs, lit from the top-left
blobs = [(40, 26, 30, 22), (20, 36, 18, 15), (60, 36, 18, 15), (32, 14, 18, 13), (50, 14, 18, 13), (40, 40, 24, 12)]
for x in range(W):
    for y in range(0, 56):
        best = None
        for (cx, cy, rx, ry) in blobs:
            e = ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2
            if e < 1 and (best is None or e < best): best = e
        if best is None: continue
        shade = best + (y - 20) / 80 - (x - 40) / 200 + rnd.random() * .25
        c = LIGHT if shade < .42 else MID if shade < .85 else DARK
        if best < .06 and rnd.random() < .5: c = SUN
        put(x, y, c)
# outline darkening on the lower edge of the crown
for x in range(W):
    for y in range(1, 56):
        if px[x, y][3] and not px[x, y + 1][3] if y + 1 < H else False:
            put(x, y, DARK)
# fireflies (the tree keeps the old light)
for _ in range(14):
    x, y = rnd.randrange(8, 72), rnd.randrange(6, 50)
    if px[x, y][3]: put(x, y, (255, 226, 122, 255))

out = os.path.join(os.path.dirname(__file__), '..', '..', 'Assets', 'Sprites', 'Landmarks')
os.makedirs(out, exist_ok=True)
img.save(os.path.join(out, 'giant_tree.png'))
print('saved', os.path.join(out, 'giant_tree.png'))
