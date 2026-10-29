"""Profession villager sheets (Assets/Sprites/Npc/v_<id>.png, 64x16 = 4 frames: idle, walk, work pose 1, work pose 2) and the village decor atlas
(Assets/Sprites/Village/decor.png, 16x16 tiles). Both are placeholders in the project's pixel style: replace the PNGs with your own art of the same size.
Run: python tools/GraphicsGenerator/make_npc_professions.py"""
import os
from PIL import Image

ROOT = os.path.join(os.path.dirname(__file__), '..', '..', 'Assets', 'Sprites')
base = Image.open(os.path.join(ROOT, 'Npc', 'villager.png')).convert('RGBA')   # 32x16: two frames of the plain villager
TUNIC, HAT = (74, 138, 74, 255), (217, 184, 74, 255)
OUT = (20, 17, 12, 255)

PROFS = {
    # id: (tunic, hat, tool colours: handle, head)
    'lumberjack': ((168, 64, 52, 255), (120, 40, 36, 255), (110, 74, 40, 255), (170, 176, 186, 255)),
    'miner':      ((84, 96, 120, 255), (150, 156, 170, 255), (110, 74, 40, 255), (190, 196, 206, 255)),
    'smith':      ((92, 70, 52, 255), (50, 40, 36, 255), (90, 60, 36, 255), (120, 126, 140, 255)),
    'farmer':     ((196, 160, 70, 255), (217, 184, 74, 255), (60, 120, 170, 255), (150, 200, 230, 255)),
    'engineer':   ((60, 130, 140, 255), (36, 70, 86, 255), (170, 130, 60, 255), (214, 214, 224, 255)),
}


APRON = (217, 210, 184, 255)
APRONS = {'lumberjack': (140, 48, 42, 255), 'miner': (110, 116, 130, 255), 'smith': (58, 46, 38, 255), 'farmer': (70, 100, 160, 255), 'engineer': (226, 229, 238, 255)}


def recolor(frame, tunic, hat, pid):
    f = frame.copy(); px = f.load()
    for y in range(16):
        for x in range(16):
            p = px[x, y]
            if p == TUNIC: px[x, y] = tunic
            elif p == HAT: px[x, y] = hat
            elif p == APRON: px[x, y] = APRONS[pid]
    # per-profession details so the five silhouettes read apart at 16x16
    def put(x, y, c): px[x, y] = c
    if pid == 'lumberjack':
        for x in range(6, 10): put(x, 7, (110, 74, 40, 255))                      # beard
        for y in range(8, 13):
            for x in range(6, 11):
                if px[x, y] == APRONS[pid] and (x + y) % 2 == 0: put(x, y, (96, 34, 32, 255))     # plaid shirt
    elif pid == 'miner':
        put(7, 2, (255, 226, 122, 255)); put(8, 2, (255, 226, 122, 255)); put(5, 5, (92, 82, 72, 255)); put(10, 7, (92, 82, 72, 255))     # helmet lamp, soot
    elif pid == 'smith':
        for x in range(5, 11): put(x, 3, (170, 54, 44, 255))                      # bandana
        put(4, 12, (92, 82, 72, 255))
    elif pid == 'farmer':
        put(3, 3, (217, 184, 74, 255)); put(12, 3, (217, 184, 74, 255)); put(3, 4, (217, 184, 74, 255)); put(12, 4, (217, 184, 74, 255))   # wide straw brim
        put(7, 9, (70, 100, 160, 255)); put(10, 9, (70, 100, 160, 255))           # overall straps
    elif pid == 'engineer':
        put(6, 3, (140, 230, 240, 255)); put(9, 3, (140, 230, 240, 255)); put(7, 3, (60, 70, 80, 255)); put(8, 3, (60, 70, 80, 255))    # goggles
        put(8, 11, (230, 190, 70, 255))                                            # gear badge
    return f


def tool(f, pid, pose, cols):
    px = f.load(); h, hd = cols[2], cols[3]
    def put(x, y, c):
        if 0 <= x < 16 and 0 <= y < 16: px[x, y] = c
    up = pose == 0
    # tool is held in front of the body on the right side; pose 0 raised, pose 1 lowered
    if pid in ('lumberjack', 'miner'):
        if up:
            for i in range(6): put(12, 9 - i, h)
            for x in range(10, 15): put(x, 3, hd)
            put(10, 4, hd); put(14, 4, hd) if pid == 'miner' else put(14, 2, hd)
        else:
            for i in range(6): put(12, 6 + i, h)
            for x in range(10, 15): put(x, 12, hd)
            put(10, 11, hd); put(14, 11, hd)
    elif pid == 'smith':
        if up:
            for i in range(5): put(12, 9 - i, h)
            for x in range(10, 15): put(x, 3, hd); put(x, 4, hd)
        else:
            for i in range(4): put(12, 8 + i, h)
            for x in range(10, 15): put(x, 12, hd); put(x, 13, hd)
            put(8, 14, (255, 190, 80, 255)); put(14, 14, (255, 190, 80, 255))
    elif pid == 'farmer':
        for x in range(10, 15): put(x, 10 if up else 11, hd)
        for y in range(10 if up else 11, 14): put(10, y, h)
        put(15, 9 if up else 12, (150, 200, 230, 255)); put(15, 10 if up else 13, (150, 200, 230, 255))
    else:  # engineer: wrench
        for i in range(5): put(12, (5 if up else 8) + i, h)
        put(11, 5 if up else 8, hd); put(13, 5 if up else 8, hd); put(12, 4 if up else 7, hd)
    return f


sheet_dir = os.path.join(ROOT, 'Npc')
for pid, cols in PROFS.items():
    f0 = recolor(base.crop((0, 0, 16, 16)), cols[0], cols[1], pid); f1 = recolor(base.crop((16, 0, 32, 16)), cols[0], cols[1], pid)
    w0 = tool(f0.copy(), pid, 0, cols); w1 = tool(f0.copy(), pid, 1, cols)
    sheet = Image.new('RGBA', (64, 16), (0, 0, 0, 0))
    for i, fr in enumerate((f0, f1, w0, w1)): sheet.paste(fr, (i * 16, 0))
    sheet.save(os.path.join(sheet_dir, f'v_{pid}.png'))

# ───────────── village decor atlas: 0 logs, 1 crates, 2 red flowers, 3 blue flowers, 4 bench, 5 campfire A, 6 campfire B, 7 lamp post ─────────────
dec = Image.new('RGBA', (128, 16), (0, 0, 0, 0)); d = dec.load()
def rect(t, x0, y0, x1, y1, c):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1): d[t * 16 + x, y] = c
BARK, BARKL, CUT, WOODD, WOOD = (74, 51, 32, 255), (110, 76, 46, 255), (214, 178, 120, 255), (90, 62, 38, 255), (150, 108, 64, 255)
# logs: three stacked
for (x, y) in ((2, 11), (8, 11), (5, 7)):
    rect(0, x, y, x + 5, y + 2, BARK); rect(0, x, y, x + 5, y, BARKL); rect(0, x, y + 1, x, y + 1, CUT); d[0 * 16 + x + 5, y + 1] = CUT
# crates
rect(1, 2, 8, 8, 14, WOOD); rect(1, 2, 8, 8, 8, WOODD); rect(1, 2, 11, 8, 11, WOODD); rect(1, 9, 11, 13, 14, WOOD); rect(1, 9, 11, 13, 11, WOODD)
# flowers
for t, col in ((2, (220, 80, 90, 255)), (3, (110, 150, 240, 255))):
    for (x, y) in ((3, 10), (7, 12), (11, 9), (5, 13), (12, 13)):
        d[t * 16 + x, y] = col; d[t * 16 + x, y + 1] = (60, 130, 60, 255)
# bench
rect(4, 2, 9, 13, 10, WOOD); rect(4, 2, 11, 3, 14, WOODD); rect(4, 12, 11, 13, 14, WOODD); rect(4, 2, 7, 13, 7, WOODD)
# campfire: logs + flame (two frames)
rect(5, 4, 13, 11, 14, BARK); rect(6, 4, 13, 11, 14, BARK)
for t, fl in ((5, ((7, 9), (8, 9), (7, 10), (8, 10), (8, 8))), (6, ((7, 8), (8, 9), (7, 10), (8, 10), (7, 9)))):
    for (x, y) in fl: d[t * 16 + x, y] = (255, 170, 60, 255)
    d[t * 16 + 7, 11] = (255, 226, 122, 255); d[t * 16 + 8, 11] = (255, 226, 122, 255)
# lamp post
rect(7, 7, 6, 8, 14, (60, 60, 72, 255)); rect(7, 5, 3, 10, 6, (255, 226, 122, 255)); rect(7, 5, 2, 10, 2, (60, 60, 72, 255)); rect(7, 5, 7, 10, 7, (60, 60, 72, 255))
vd = os.path.join(ROOT, 'Village'); os.makedirs(vd, exist_ok=True); dec.save(os.path.join(vd, 'decor.png'))
print('ok')
