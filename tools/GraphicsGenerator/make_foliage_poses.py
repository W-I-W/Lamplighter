"""Wind poses for the swaying tiles -> Assets/Sprites/Environment/134..161_*_p*.png (16x16).
Every plant gets four bent poses (strong left, left, right, strong right); the original tile is the upright pose. The wind shader picks a pose per tile from the wind phase,
so the sway moves in whole sprite pixels and looks hand-drawn. Run from the project root: python tools/GraphicsGenerator/make_foliage_poses.py
Rules that keep it clean pixel art: the root never moves, every row shifts at most 1 px more than the row below it (blades stay connected), nothing is pushed outside the tile.
Replace any pose PNG with your own art of the same size to restyle it."""
from PIL import Image

ENV = 'Assets/Sprites/Environment/'
# (tile index, file name stem, strength of the bend at the very top: pixels for the strong pose)
PLANTS = [(5, '005_Tree', 2), (111, '111_Pine', 2), (79, '079_DeadTree', 2), (130, '130_GrassTuft', 2), (131, '131_Bush', 1), (132, '132_Flowers', 2), (133, '133_TallGrass', 3)]
POSE_BASE = 134
POSES = [('L2', -2), ('L1', -1), ('R1', 1), ('R2', 2)]


def bounds(im):
    """Per row: (min x, max x) of opaque pixels, or None."""
    out = []
    for y in range(16):
        xs = [x for x in range(16) if im.getpixel((x, y))[3] > 0]
        out.append((min(xs), max(xs)) if xs else None)
    return out


def displacement(im, strong, level):
    """Pixel shift for every row for a pose of `level` (-2..2) in units of the plant's strong bend."""
    b = bounds(im)
    rows = [y for y in range(16) if b[y]]
    top, bottom = rows[0], 15
    s = abs(level) / 2.0 * strong
    sign = 1 if level > 0 else -1
    target = []
    for y in range(16):
        if y < top: target.append(0); continue
        h = (y - top) / max(1, bottom - top)               # 0 at the tip, 1 at the root
        target.append(round(s * (1.0 - h) ** 1.3 + 0.001))
    d = [0] * 16
    for y in range(15, -1, -1):                            # from the root upwards, at most +1 px per row
        t = target[y]
        d[y] = min(t, (d[y + 1] if y < 15 else 0) + 1)
        if b[y]:                                           # never leave the tile
            room = (15 - b[y][1]) if sign > 0 else b[y][0]
            d[y] = min(d[y], room)
    return [v * sign for v in d]


def shifted(im, d):
    out = Image.new('RGBA', (16, 16), (0, 0, 0, 0))
    for y in range(16):
        for x in range(16):
            p = im.getpixel((x, y))
            if p[3] > 0 and 0 <= x + d[y] < 16: out.putpixel((x + d[y], y), p)
    return out


for pi, (tile, stem, strong) in enumerate(PLANTS):
    base = Image.open(ENV + stem + '.png').convert('RGBA')
    for k, (tag, level) in enumerate(POSES):
        idx = POSE_BASE + pi * 4 + k
        name = stem.split('_', 1)[1]
        shifted(base, displacement(base, strong, level)).save(f'{ENV}{idx:03d}_{name}_p{tag}.png')
print('poses written:', len(PLANTS) * 4, 'tiles', POSE_BASE, '..', POSE_BASE + len(PLANTS) * 4 - 1)
