"""Grove Devourer (Пожиратель Рощи) — the first boss: a huge infected tree, pixel art.
Writes Assets/Sprites/Bosses/:
  grove_devourer.png  21 frames 128x128: 0-3 idle, 4-6 root attack, 7-9 spores, 10-12 branch swing, 13 hurt, 14-16 phase change, 17-20 death
  grove_core.png      4 frames 24x24: closed (bark plates), smouldering, open (vulnerable glow), cracked
  grove_root.png      6 frames 24x40: ground crack (warning), root breaking out x3, full root, crumbling
  grove_spore.png     4 frames 32x32: poison spore cloud loop
  grove_ground.png    4 frames 32x32: infected ground x3, revived ground (grass, flowers)
  grove_bits.png      12 frames 8x8: dark leaves x4, bark chips x4, spore motes x2, sprouts/fireflies x2
  grove_branch.png    104x24: thorny branch for the sweep (base on the left)
  grove_node.png      3 frames 16x16: green root node glowing x2, drained
Hero and tiles are 16x16; the tree stands on (64,120) of its frame, the core sits at (64,66).
Run: python tools/GraphicsGenerator/make_grove_devourer.py"""
import math, os, random
from PIL import Image

OUT = os.path.join(os.path.dirname(__file__), '..', '..', 'Assets', 'Sprites', 'Bosses')

# ── palette ──
OUTL = (14, 9, 16, 255)
BARK = [(30, 20, 28, 255), (48, 33, 40, 255), (68, 48, 52, 255), (92, 66, 64, 255), (122, 92, 80, 255)]
BARK_GREY = [(30, 30, 34, 255), (50, 50, 54, 255), (72, 70, 72, 255), (96, 94, 92, 255), (124, 120, 114, 255)]
LEAF = [(13, 18, 22, 255), (21, 30, 32, 255), (32, 44, 40, 255), (48, 60, 46, 255), (72, 82, 54, 255)]
LEAF_P = [(30, 18, 40, 255), (54, 30, 66, 255)]
VEIN = [(86, 34, 110, 255), (140, 60, 180, 255), (200, 110, 240, 255), (244, 200, 255, 255)]
SICK = [(96, 120, 40, 255), (150, 176, 60, 255), (210, 230, 120, 255)]
EYE = [(120, 40, 160, 255), (220, 120, 255, 255), (255, 236, 255, 255)]
GOLD = [(120, 70, 20, 255), (220, 150, 40, 255), (255, 226, 122, 255), (255, 250, 220, 255)]
GRASS = [(36, 70, 36, 255), (58, 104, 46, 255), (96, 150, 64, 255), (150, 200, 96, 255)]
SOIL_I = [(30, 22, 34, 255), (46, 30, 52, 255), (64, 40, 72, 255)]


def h2(x, y, s=0):
    v = (x * 374761393 + y * 668265263 + s * 2147483647) & 0xffffffff
    v = ((v ^ (v >> 13)) * 1274126177) & 0xffffffff
    return (v ^ (v >> 16)) & 0xffff


class Canvas:
    def __init__(s, w, h):
        s.w, s.h = w, h
        s.img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
        s.px = s.img.load()

    def put(s, x, y, c):
        x, y = int(round(x)), int(round(y))
        if 0 <= x < s.w and 0 <= y < s.h and c is not None:
            s.px[x, y] = c

    def get(s, x, y):
        if 0 <= x < s.w and 0 <= y < s.h:
            return s.px[x, y]
        return (0, 0, 0, 0)

    def solid(s, x, y):
        return s.get(x, y)[3] > 0

    def outline(s, col=OUTL):
        pts = []
        for y in range(s.h):
            for x in range(s.w):
                if s.px[x, y][3] == 0 and any(s.solid(x + dx, y + dy) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                    pts.append((x, y))
        for x, y in pts:
            s.px[x, y] = col


def bark_col(x, y, pal, light, seed=0):
    """Bark: vertical grooves, noise, light from the left."""
    groove = (x + int(2 * math.sin(y * .23 + seed))) % 7 == 0
    n = h2(x, y, seed) % 100
    lvl = 2 + light
    if groove: lvl -= 2
    elif n < 14: lvl -= 1
    elif n > 90: lvl += 1
    return pal[max(0, min(4, lvl))]


def stroke(cv, pts, w0, w1, colfn, step=.5):
    """Thick polyline: discs stamped along the points, width w0 -> w1."""
    total = sum(math.dist(pts[i], pts[i + 1]) for i in range(len(pts) - 1)) or 1
    acc = 0
    for i in range(len(pts) - 1):
        (ax, ay), (bx, by) = pts[i], pts[i + 1]
        L = math.dist((ax, ay), (bx, by)); n = max(1, int(L / step))
        for k in range(n + 1):
            t = k / n; x, y = ax + (bx - ax) * t, ay + (by - ay) * t
            f = (acc + L * t) / total; r = (w0 + (w1 - w0) * f) / 2
            nx, ny = (by - ay) / (L or 1), -(bx - ax) / (L or 1)
            for yy in range(int(y - r - 1), int(y + r + 2)):
                for xx in range(int(x - r - 1), int(x + r + 2)):
                    d = math.hypot(xx - x, yy - y)
                    if d <= r:
                        side = ((xx - x) * nx + (yy - y) * ny) / (r or 1)   # -1..1 across the limb
                        cv.put(xx, yy, colfn(xx, yy, side, f))
        acc += L


def limb_col(pal, vein_glow=0, sick=False):
    def f(x, y, side, t):
        light = 1 if side < -.35 else (-1 if side > .45 else 0)
        c = bark_col(x, y, pal, light, 3)
        if vein_glow > 0 and abs(side) < .18 and h2(x, y, 9) % 3 == 0:
            c = VEIN[min(3, vein_glow)]
        if sick and h2(x, y, 4) % 23 == 0:
            c = SICK[0]
        return c
    return f


# ───────────────────────── the tree ─────────────────────────
CX, BASE = 64, 120


def draw_roots(cv, pose, pal, glow, broken=0):
    """Ground roots spreading from the trunk base. pose: 0 rest, 1 tense, 2 surging out."""
    spread = [0, 4, 10][pose]
    roots = [(-1, 26, -2), (1, 26, -2), (-1, 40, 3), (1, 40, 3), (-1, 14, 6), (1, 16, 6)]
    for i, (sd, L, dy) in enumerate(roots):
        if broken and i % 2 == broken % 2 and i > 1: L = L * .5
        L += spread * (1 if i < 4 else .6)
        x0, y0 = CX + sd * 10, BASE - 6
        pts = [(x0, y0), (x0 + sd * L * .45, y0 + 2 + dy * .3), (x0 + sd * L * .8, y0 + 4 + dy * .6), (x0 + sd * L, y0 + 5 + dy)]
        if pose == 1: pts[1] = (pts[1][0], pts[1][1] - 3)
        stroke(cv, pts, 8, 2, limb_col(pal, glow))
        if glow >= 2 and not broken:
            tx, ty = pts[-1]
            cv.put(tx, ty, VEIN[3]); cv.put(tx - sd, ty, VEIN[2])


def draw_trunk(cv, sway, pal, glow, cracks=0, sick=True):
    top = 40
    for y in range(top, BASE + 1):
        t = (y - top) / (BASE - top)
        half = 12 + 10 * (t ** 2.4) + (2 if y > BASE - 8 else 0)
        cx = CX + sway * (1 - t) * 1.5 + math.sin(y * .15) * .8
        for x in range(int(cx - half - 1), int(cx + half + 2)):
            d = (x - cx) / half
            if abs(d) > 1: continue
            light = 1 if d < -.45 else (-1 if d > .5 else 0)
            if d > .82: light = -2
            c = bark_col(x, y, pal, light)
            if sick and h2(x, y, 11) % 37 == 0 and d < .3: c = SICK[0]
            cv.put(x, y, c)
    # infection veins climbing the trunk
    rnd = random.Random(5)
    for v in range(5):
        x = CX - 10 + v * 5 + rnd.randint(-1, 1); y = BASE - 2
        while y > top + 6:
            if glow > 0 or h2(x, y, 2) % 3:
                cv.put(x, y, VEIN[min(3, max(0, glow))] if glow > 0 else VEIN[0])
            y -= 1
            if h2(x, y, v) % 5 == 0: x += 1 if (v + y) % 2 else -1
    # cavity for the core (the core sprite is drawn over it)
    for y in range(56, 77):
        for x in range(52, 77):
            e = ((x - 64) / 11.5) ** 2 + ((y - 66) / 10.5) ** 2
            if e < 1: cv.put(x, y, (12, 6, 16, 255) if e < .7 else pal[0])
            elif e < 1.35: cv.put(x, y, pal[3] if (y < 66 and x < 64) else pal[1])
    # cracks of the dying tree
    if cracks:
        rnd = random.Random(17)
        for k in range(cracks * 3):
            x, y = CX + rnd.randint(-14, 14), rnd.randint(top + 4, BASE - 6)
            for s in range(rnd.randint(6, 14)):
                c = GOLD[3] if s < 3 else GOLD[2]
                cv.put(x, y, c); cv.put(x + 1, y, GOLD[1])
                y += 1; x += rnd.choice((-1, 0, 0, 1))


def draw_face(cv, glow, mood=0, sway=0):
    """Two glowing eye slits above the core. mood: 0 normal, 1 squint (hurt), 2 wide (phase), 3 dead."""
    for sd in (-1, 1):
        ex = CX + sd * 8 + sway; ey = 49
        for dx in range(-3, 4):
            yy = ey + (abs(dx) // 2 if sd * dx > 0 else 0) - (1 if mood == 2 else 0)
            if mood == 1:
                cv.put(ex + dx, ey + (dx * sd) // 2, EYE[1]); continue
            if mood == 3:
                cv.put(ex + dx, ey, (20, 14, 20, 255)); continue
            cv.put(ex + dx, yy, EYE[2] if abs(dx) < 2 and glow > 0 else EYE[1])
            cv.put(ex + dx, yy + 1, EYE[0])
            if mood == 2: cv.put(ex + dx, yy - 1, EYE[1])
        if glow > 1 and mood != 3:
            for dx in (-4, 4): cv.put(ex + dx, ey, EYE[0])


def draw_arm(cv, sd, ang, length, pal, glow, droop=0, thorny=True):
    """Big branch arm from the shoulder; ang in degrees (0 = straight out, positive = up)."""
    sx, sy = CX + sd * 11, 61
    a = math.radians(ang)
    pts = [(sx, sy)]
    for i in range(1, 5):
        t = i / 4; ca = a - math.radians(droop) * t * t
        px = pts[-1][0] + sd * math.cos(ca) * length / 4
        py = pts[-1][1] - math.sin(ca) * length / 4 + (2 if i % 2 else -1)
        pts.append((px, py))
    stroke(cv, pts, 10, 3, limb_col(pal, glow))
    # twigs and thorns
    for i in range(1, 4):
        x, y = pts[i]
        for k in range(1, 6 + i):
            tx = x + sd * k * .7; ty = y - k * (1 if i % 2 else -.6)
            cv.put(tx, ty, pal[1] if k < 4 else pal[2])
        if thorny:
            cv.put(x, y - 4, pal[3]); cv.put(x + sd, y - 5, pal[2])
    # claw tip
    tx, ty = pts[-1]
    for k in range(4):
        cv.put(tx + sd * (k + 1), ty - 1 + (k // 2), pal[3] if k < 2 else pal[2])
        cv.put(tx + sd * (k + 1), ty + 2 + k, pal[2])
    return pts[-1]


def draw_crown(cv, sway, puff, density, purple, pods=0, bristle=0, grey=False):
    """Dark twisted canopy: overlapping blobs with dithered shading."""
    blobs = [(64, 24, 40, 17), (33, 31, 21, 12), (95, 31, 21, 12), (47, 12, 19, 10), (81, 12, 19, 10), (64, 33, 26, 9), (19, 40, 11, 7), (109, 40, 11, 7)]
    pal = BARK_GREY[:] if grey else LEAF
    for y in range(0, 56):
        for x in range(cv.w):
            best = None; bc = None
            for (bx, by, rx, ry) in blobs:
                rx2, ry2 = rx + puff, ry + puff * .6
                e = ((x - bx - sway) / rx2) ** 2 + ((y - by) / ry2) ** 2
                rag = (h2(x // 2, y // 2, 7) % 100) / 100 * .32 + (.22 if (h2(x // 3, y // 3, 12) % 9 == 0) else 0)     # рваный край, дыры
                if e < 1 - rag and (best is None or e < best): best, bc = e, (bx, by)
            if best is None: continue
            if density < 1 and h2(x // 3, y // 3, 21) % 100 > density * 100 + (h2(x, y, 22) % 20 - 10): continue      # крона осыпается кусками
            shade = best * .9 + (y - bc[1]) / 40 - (x - bc[0] - sway) / 90 + (h2(x, y, 3) % 100) / 400
            lvl = 4 if shade < .05 else 3 if shade < .35 else 2 if shade < .7 else 1 if shade < 1.0 else 0
            c = pal[lvl]
            if purple and not grey and h2(x, y, 5) % 100 < purple: c = LEAF_P[0 if lvl < 2 else 1]
            cv.put(x, y, c)
    # hanging tendrils with drops of infection
    if density > .5:
        for i, vx in enumerate((24, 33, 45, 54, 74, 83, 95, 104)):
            vy = 55
            while vy > 10 and not cv.solid(vx + sway, vy): vy -= 1
            vy += 1
            L = 5 + h2(i, 3) % 9 + puff
            for k in range(L):
                c = LEAF[1] if k < L - 2 else LEAF_P[1]
                cv.put(vx + sway + (1 if (k // 3) % 2 and i % 2 else 0), vy + k, c)
            cv.put(vx + sway, vy + L, VEIN[1] if not grey else BARK_GREY[2])
    # dead thorny branches poking out of the canopy
    for (x0, y0, dx, dy) in ((28, 18, -1, -1), (100, 18, 1, -1), (64, 4, 0, -1), (12, 36, -1, 0), (116, 36, 1, 0), (44, 4, -1, -1), (84, 4, 1, -1), (56, 2, -1, -1), (72, 2, 1, -1)):
        L = 7 + bristle * 3
        for k in range(L):
            cv.put(x0 + dx * k + sway, y0 + dy * k + (k // 3 if dx else 0), BARK[1] if k < L - 2 else BARK[2])
    # spore pods (spore frames)
    if pods:
        rnd = random.Random(31)
        for i in range(9):
            px, py = rnd.randint(22, 106), rnd.randint(10, 48)
            r = 2 + (pods > 1)
            for yy in range(-r, r + 1):
                for xx in range(-r, r + 1):
                    if xx * xx + yy * yy <= r * r:
                        cv.put(px + xx + sway, py + yy, SICK[2] if xx < 0 and yy < 0 else SICK[1] if xx * xx + yy * yy < r * r - 1 else SICK[0])


def tree_frame(kind, k):
    cv = Canvas(128, 128)
    pal, glow, sway, puff, density, purple = BARK, 0, 0, 0, 1.0, 22
    arms = [(-1, 18, 36, 0), (1, 18, 36, 0)]            # (side, angle, length, droop)
    roots_pose, mood, pods, bristle, cracks, grey, broken = 0, 0, 0, 0, 0, False, 0
    eye_glow = 1
    if kind == 'idle':
        sway = [0, 1, 0, -1][k]; arms = [(-1, 8 + 2 * sway, 36, 6), (1, 8 - 2 * sway, 36, 6)]
    elif kind == 'roots':
        arms = [[(-1, 50, 36, 0), (1, 50, 36, 0)], [(-1, -35, 38, 10), (1, -35, 38, 10)], [(-1, -22, 36, 6), (1, -22, 36, 6)]][k]
        roots_pose = [1, 2, 2][k]; glow = [1, 2, 2][k]; eye_glow = 2
    elif kind == 'spores':
        puff = [1, 3, 2][k]; pods = [1, 2, 2][k]; arms = [(-1, 24, 34, 0), (1, 24, 34, 0)]; purple = 30
    elif kind == 'branch':
        arms = [[(-1, 6, 34, 4), (1, 62, 40, -10)], [(-1, 14, 34, 4), (1, 0, 50, 0)], [(-1, 12, 34, 4), (1, -45, 40, 10)]][k]
        sway = [1, -2, -1][k]
    elif kind == 'hurt':
        sway = 2; mood = 1; arms = [(-1, 32, 34, 0), (1, 32, 34, 0)]; pal = [tuple(min(255, int(v * 1.25 + 14)) for v in c[:3]) + (255,) for c in BARK]
    elif kind == 'phase':
        glow = [2, 3, 3][k]; mood = 2; bristle = k + 1; roots_pose = [1, 2, 1][k]; eye_glow = 2; purple = 30 + 10 * k
        arms = [[(-1, 40, 38, 0), (1, 40, 38, 0)], [(-1, 62, 40, -6), (1, 62, 40, -6)], [(-1, 28, 38, 0), (1, 28, 38, 0)]][k]
    elif kind == 'death':
        cracks = k + 1; density = [1.0, .8, .55, .3][k]; purple = 8
        arms = [[(-1, 25, 30, 6), (1, 25, 30, 6)], [(-1, 5, 30, 20), (1, 10, 30, 18)], [(-1, -15, 26, 40), (1, -10, 26, 36)], [(-1, -30, 20, 60), (1, -28, 18, 55)]][k]
        mood = 2 if k < 2 else 3; grey = k >= 3; broken = 1 if k >= 2 else 0
        if k >= 3: pal = BARK_GREY
    # back layer: crown, then arms, trunk, roots, face
    draw_crown(cv, sway, puff, density, purple, pods, bristle, grey)
    for (sd, ang, L, droop) in arms:
        draw_arm(cv, sd, ang, L, pal, glow, droop)
    draw_roots(cv, roots_pose, pal, glow, broken)
    draw_trunk(cv, sway, pal, glow, cracks, sick=not grey)
    draw_face(cv, eye_glow, mood, 0)
    # floating spore motes / phase aura / falling leaves
    rnd = random.Random(100 + k)
    if kind == 'spores':
        for i in range(10 + 6 * k):
            x, y = rnd.randint(10, 118), rnd.randint(0, 70)
            if not cv.solid(x, y): cv.put(x, y, SICK[rnd.randint(1, 2)])
    if kind == 'phase':
        for i in range(14 * (k + 1)):
            a = rnd.random() * math.tau; r = 40 + rnd.random() * 20
            x, y = 64 + math.cos(a) * r, 64 + math.sin(a) * r * .9
            if not cv.solid(x, y): cv.put(x, y, VEIN[rnd.randint(1, 3)])
    if kind in ('hurt', 'death'):
        for i in range(8 + 6 * k):
            x, y = rnd.randint(14, 114), rnd.randint(30, 110)
            if not cv.solid(x, y): cv.put(x, y, LEAF[rnd.randint(1, 3)] if not grey else BARK_GREY[2])
    cv.outline()
    return cv.img


# ───────────────────────── core ─────────────────────────
def core_frame(k):
    cv = Canvas(24, 24)
    for y in range(24):
        for x in range(24):
            d = math.hypot(x - 11.5, y - 11.5)
            if d > 10.5: continue
            if k == 0:      # closed: bark plates with thorns
                plate = (int(math.atan2(y - 11.5, x - 11.5) / (math.tau / 6)) + 6) % 6
                c = BARK[3 - (plate % 2)] if d < 9.5 else BARK[1]
                if abs(math.atan2(y - 11.5, x - 11.5) % (math.tau / 6)) < .18: c = BARK[0]
                if d < 3: c = VEIN[0]
            elif k == 3:    # cracked
                c = BARK_GREY[1] if d > 6 else GOLD[1]
                if (x + y) % 5 == 0 and d < 9: c = GOLD[3]
                if d < 3: c = GOLD[3]
            else:           # smouldering (1) / open and glowing (2)
                if k == 1: c = VEIN[0] if d > 7 else VEIN[1] if d > 4 else VEIN[2]
                else: c = GOLD[1] if d > 8 else GOLD[2] if d > 5 else GOLD[3]
                if k == 2 and d > 9: c = VEIN[1]
                if (h2(x, y, 40 + k) % 9 == 0) and d < 8: c = VEIN[3] if k == 1 else (255, 255, 255, 255)
            cv.put(x, y, c)
    cv.outline()
    return cv.img


# ───────────────────────── root from the ground ─────────────────────────
def root_frame(k):
    cv = Canvas(24, 40)
    gy = 36
    # ground crack (all frames; the warning frame is only the crack)
    rnd = random.Random(3)
    for i in range(4):
        x, y = 12, gy
        for s in range(5 + i):
            cv.put(x, y, SOIL_I[0] if s % 2 else SOIL_I[1]); cv.put(x, y - 1, VEIN[0] if k == 0 and s < 2 else SOIL_I[0])
            x += (-1, 1, -1, 1)[i]; y += rnd.choice((0, 0, 1, -1)) if abs(y - gy) < 2 else 0
    if k == 0:
        cv.put(12, gy - 1, VEIN[2]); cv.put(11, gy, VEIN[1])
        return cv.img
    height = [0, 10, 20, 28, 30, 18][k]
    if k == 5:      # crumbling stump
        for y in range(gy - height, gy):
            for x in range(9, 16):
                if h2(x, y, 8) % 3: cv.put(x, y, BARK[1 + h2(x, y) % 3])
        for i in range(10): cv.put(rnd.randint(4, 20), rnd.randint(gy - 22, gy), BARK[2])
        cv.outline(); return cv.img
    pts = [(12, gy), (11, gy - height * .35), (13.5, gy - height * .7), (12, gy - height)]
    stroke(cv, pts, 7, 1.5, limb_col(BARK, 1))
    # thorns and a glowing tip
    for i in range(1, 3):
        x, y = pts[i]; cv.put(x - 4, y - 1, BARK[3]); cv.put(x + 4, y - 2, BARK[3])
    tx, ty = pts[-1]
    cv.put(tx, ty, VEIN[3]); cv.put(tx, ty + 1, VEIN[2])
    # soil chunks thrown up
    for i in range(4 if k < 3 else 2):
        cv.put(rnd.randint(4, 20), gy - rnd.randint(2, 6 + k * 3), SOIL_I[2])
    cv.outline()
    return cv.img


# ───────────────────────── spores ─────────────────────────
def spore_frame(k):
    cv = Canvas(32, 32)
    rnd = random.Random(60)
    puffs = [(16, 17, 9), (10, 15, 6), (22, 14, 6), (16, 11, 6), (11, 21, 5), (22, 21, 5)]
    for y in range(32):
        for x in range(32):
            best = None
            for i, (px, py, r) in enumerate(puffs):
                ox = math.sin(k * math.pi / 2 + i) * 1.2; oy = math.cos(k * math.pi / 2 + i * 1.7) * 1.0
                e = math.hypot(x - px - ox, y - py - oy) / r
                if e < 1 and (best is None or e < best): best = e
            if best is None: continue
            if best > .75 and (x + y + k) % 2: continue                       # dithered soft edge
            c = SICK[2] if best < .3 and y < 17 else SICK[1] if best < .6 else (110, 70, 140, 255) if best < .85 else SICK[0]
            if h2(x, y, k) % 13 == 0: c = VEIN[1]
            cv.put(x, y, c)
    for i in range(6):
        a = rnd.random() * math.tau + k * .4; r = 11 + rnd.random() * 3
        cv.put(16 + math.cos(a) * r, 16 + math.sin(a) * r, SICK[2])
    return cv.img


# ───────────────────────── ground ─────────────────────────
def ground_frame(k):
    cv = Canvas(32, 32)
    rnd = random.Random(80 + k)
    if k < 3:       # infected patch: purple rot, black veins, sick moss
        for y in range(32):
            for x in range(32):
                d = math.hypot(x - 15.5, y - 15.5) / 15 + (h2(x // 2, y // 2, k) % 100) / 300
                if d > 1: continue
                if d > .82 and (x + y) % 2: continue
                n = h2(x, y, 50 + k) % 100
                c = SOIL_I[0] if n < 40 else SOIL_I[1] if n < 80 else SOIL_I[2]
                if d > .82: c = SOIL_I[0]
                cv.put(x, y, (c[0], c[1], c[2], 200 if d > .82 else 235))
        for v in range(3):
            x, y = rnd.randint(6, 26), rnd.randint(6, 26)
            for s in range(rnd.randint(5, 10)):
                cv.put(x, y, VEIN[0] if s % 3 else VEIN[1]); x += rnd.choice((-1, 1, 0)); y += rnd.choice((-1, 1, 0))
        for i in range(4):
            cv.put(rnd.randint(4, 28), rnd.randint(4, 28), SICK[0])
        if k == 1:
            for i in range(3): x, y = rnd.randint(8, 24), rnd.randint(8, 24); cv.put(x, y, LEAF_P[1]); cv.put(x + 1, y, LEAF_P[0])
        if k == 2:
            for i in range(2): x, y = rnd.randint(8, 24), rnd.randint(10, 24); cv.put(x, y, BARK[2]); cv.put(x + 1, y - 1, BARK[1]); cv.put(x + 2, y - 1, BARK[2])
    else:           # revived: fresh grass tufts and small flowers on transparent ground
        for i in range(16):
            x, y = rnd.randint(3, 28), rnd.randint(6, 29)
            for b in range(rnd.randint(2, 4)):
                cv.put(x + b - 1, y, GRASS[1]); cv.put(x + b - 1, y - 1 - (b % 2), GRASS[2])
            if rnd.random() < .3: cv.put(x, y - 3, GRASS[3])
        for i in range(5):
            x, y = rnd.randint(4, 27), rnd.randint(5, 27)
            col = rnd.choice(((255, 226, 122, 255), (255, 170, 200, 255), (220, 240, 255, 255)))
            cv.put(x, y, col); cv.put(x, y + 1, GRASS[0])
    return cv.img


# ───────────────────────── particles ─────────────────────────
def bits_sheet():
    cv = Canvas(96, 8)
    leaves = [[(3, 1), (4, 1), (2, 2), (3, 2), (4, 2), (5, 2), (2, 3), (3, 3), (4, 3), (5, 3), (3, 4), (4, 4), (4, 5), (5, 6)],
              [(1, 3), (2, 2), (3, 2), (4, 2), (2, 3), (3, 3), (4, 3), (5, 3), (3, 4), (4, 4), (5, 4), (6, 4), (6, 5)],
              [(2, 1), (3, 2), (4, 2), (2, 3), (3, 3), (4, 3), (5, 3), (3, 4), (4, 4), (5, 4), (4, 5), (5, 5), (6, 6)],
              [(3, 1), (3, 2), (4, 2), (2, 3), (3, 3), (4, 3), (5, 3), (2, 4), (3, 4), (4, 4), (3, 5), (3, 6)]]
    for f, pts in enumerate(leaves):
        for i, (x, y) in enumerate(pts):
            c = LEAF[3] if i < 3 else LEAF_P[1] if (f + i) % 5 == 0 else LEAF[2]
            if i == len(pts) - 1: c = BARK[2]
            cv.put(f * 8 + x, y, c)
    chips = [[(2, 2), (3, 2), (4, 2), (2, 3), (3, 3), (4, 3), (5, 3), (3, 4), (4, 4)], [(3, 2), (4, 2), (3, 3), (4, 3), (3, 4), (4, 4), (3, 5)],
             [(2, 3), (3, 3), (4, 3), (5, 3), (6, 3), (3, 4), (4, 4)], [(3, 3), (4, 3), (3, 4), (4, 4), (5, 4)]]
    for f, pts in enumerate(chips):
        for i, (x, y) in enumerate(pts):
            cv.put(32 + f * 8 + x, y, BARK[3] if i < 2 else BARK[2] if i % 2 else BARK[1])
    for f in range(2):      # spore motes
        ox = 64 + f * 8
        for (x, y, c) in ((3, 3, SICK[2]), (4, 3, SICK[1]), (3, 4, SICK[1]), (4, 4, SICK[0])) if f == 0 else ((4, 3, SICK[2]), (3, 4, SICK[1]), (4, 4, VEIN[1])):
            cv.put(ox + x, y, c)
    # sprout and firefly (revival)
    for (x, y, c) in ((4, 6, GRASS[0]), (4, 5, GRASS[1]), (3, 4, GRASS[2]), (5, 4, GRASS[2]), (2, 3, GRASS[3]), (6, 3, GRASS[3])): cv.put(80 + x, y, c)
    for (x, y, c) in ((3, 3, GOLD[3]), (4, 3, GOLD[2]), (3, 4, GOLD[2]), (4, 4, GOLD[1])): cv.put(88 + x, y, c)
    return cv.img


def branch_img():
    cv = Canvas(104, 24)
    pts = [(2, 12), (30, 10), (60, 13), (86, 11), (101, 12)]
    stroke(cv, pts, 10, 3, limb_col(BARK, 2))
    rnd = random.Random(9)
    for i in range(1, 4):
        x, y = pts[i]
        for k in range(1, 7):
            cv.put(x + k * .8, y - k, BARK[2] if k < 4 else LEAF[2])
            cv.put(x + k * .6, y + k * .8, BARK[1] if k < 3 else LEAF[1])
        for t in range(3): cv.put(x - 6 + t * 5, y - 5, BARK[3]); cv.put(x - 4 + t * 5, y + 5, BARK[3])
    for i in range(14):     # dark leaves along the branch
        x = rnd.randint(14, 96); y = 12 + rnd.choice((-1, 1)) * rnd.randint(4, 7)
        cv.put(x, y, LEAF[rnd.randint(1, 3)]); cv.put(x + 1, y, LEAF[1])
    cv.put(101, 11, VEIN[3]); cv.put(102, 12, VEIN[2])
    cv.outline()
    return cv.img


def node_frame(k):
    cv = Canvas(16, 16)
    # mound
    for y in range(11, 16):
        for x in range(2, 14):
            if ((x - 7.5) / 6) ** 2 + ((y - 14) / 3) ** 2 < 1: cv.put(x, y, SOIL_I[1] if k < 2 else BARK_GREY[1])
    if k < 2:       # glowing green sprout pulled by the tree
        stem = [(7, 12), (7, 11), (8, 10), (8, 9), (7, 8)]
        for x, y in stem: cv.put(x, y, GRASS[1])
        for (x, y) in ((5, 7), (6, 7), (6, 6), (9, 7), (10, 7), (10, 6), (7, 5), (8, 5), (8, 4)): cv.put(x, y, GRASS[3] if k else GRASS[2])
        glow = SICK[2] if k else SICK[1]
        for (x, y) in ((4, 9), (11, 9), (7, 2), (3, 5), (12, 5)): cv.put(x, y, glow)
    else:           # drained: withered grey stalk
        for (x, y) in ((7, 12), (7, 11), (8, 10), (8, 9), (9, 8), (10, 8)): cv.put(x, y, BARK_GREY[2])
        cv.put(6, 10, BARK_GREY[1])
    cv.outline()
    return cv.img


def sheet(frames, w, h):
    img = Image.new('RGBA', (w * len(frames), h), (0, 0, 0, 0))
    for i, f in enumerate(frames): img.paste(f, (i * w, 0))
    return img


def main():
    os.makedirs(OUT, exist_ok=True)
    frames = [tree_frame('idle', k) for k in range(4)] + [tree_frame('roots', k) for k in range(3)] + [tree_frame('spores', k) for k in range(3)] \
        + [tree_frame('branch', k) for k in range(3)] + [tree_frame('hurt', 0)] + [tree_frame('phase', k) for k in range(3)] + [tree_frame('death', k) for k in range(4)]
    assert len(frames) == 21
    sheet(frames, 128, 128).save(os.path.join(OUT, 'grove_devourer.png'))
    sheet([core_frame(k) for k in range(4)], 24, 24).save(os.path.join(OUT, 'grove_core.png'))
    sheet([root_frame(k) for k in range(6)], 24, 40).save(os.path.join(OUT, 'grove_root.png'))
    sheet([spore_frame(k) for k in range(4)], 32, 32).save(os.path.join(OUT, 'grove_spore.png'))
    sheet([ground_frame(k) for k in range(4)], 32, 32).save(os.path.join(OUT, 'grove_ground.png'))
    bits_sheet().save(os.path.join(OUT, 'grove_bits.png'))
    branch_img().save(os.path.join(OUT, 'grove_branch.png'))
    sheet([node_frame(k) for k in range(3)], 16, 16).save(os.path.join(OUT, 'grove_node.png'))
    print('grove devourer sprites ->', os.path.abspath(OUT))


if __name__ == '__main__':
    main()
