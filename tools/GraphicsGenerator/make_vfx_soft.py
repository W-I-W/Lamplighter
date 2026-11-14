"""Мягкие текстуры частиц (Assets/Sprites/Vfx/fx_*.png): белые маски с плавной прозрачностью, игра красит их и рисует со сглаживанием (Gfx: CanvasTexture + mipmaps).
Так ни одна частица не выглядит квадратиком. Запуск из корня проекта: python tools/GraphicsGenerator/make_vfx_soft.py
  fx_soft  — круглое свечение с мягким спадом (вспышки, ореолы)
  fx_dot   — круглая точка: плотное ядро и мягкий край (обычные частицы)
  fx_star  — четырёхлучевая звезда с ореолом (блики, звёздочки)
  fx_spark — штрих искры: яркая головка справа, хвост тает влево
  fx_shard — осколок неправильной формы со светлой гранью
  fx_puff  — клуб дыма из нескольких мягких шаров
  fx_ray   — вертикальный луч, тающий к концам"""
import math, os, random
from PIL import Image

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', 'Sprites', 'Vfx')


def smooth(t):
    t = max(0.0, min(1.0, t)); return t * t * (3 - 2 * t)


def save(name, w, h, fn, grey=None):
    im = Image.new('RGBA', (w, h)); px = im.load()
    for y in range(h):
        for x in range(w):
            a = max(0.0, min(1.0, fn(x + .5, y + .5)))
            g = 255 if grey is None else int(255 * max(0.0, min(1.0, grey(x + .5, y + .5))))
            px[x, y] = (g, g, g, int(round(a * 255)))
    im.save(os.path.join(OUT, name + '.png'))


def r(x, y, cx, cy, rad): return math.hypot(x - cx, y - cy) / rad


save('fx_soft', 32, 32, lambda x, y: (1 - smooth(r(x, y, 16, 16, 16))) ** 1.6)
save('fx_dot', 16, 16, lambda x, y: 1 - smooth((r(x, y, 8, 8, 8) - .45) / .55))


def star(x, y):
    dx, dy = abs(x - 16) / 16, abs(y - 16) / 16
    arm = max((1 - smooth(dx)) * (1 - smooth(dy / .09)), (1 - smooth(dy)) * (1 - smooth(dx / .09)))       # тонкие лучи по осям
    core = 1 - smooth(math.hypot(dx, dy) / .32)
    halo = (1 - smooth(math.hypot(dx, dy))) ** 3 * .45
    return max(arm, core, halo)


save('fx_star', 32, 32, star)


def spark(x, y):
    t = x / 32                                       # 0 — хвост, 1 — головка
    width = .15 + .85 * t ** .7
    across = abs(y - 4) / 4 / width
    return (1 - smooth(across)) * smooth(t / .9) * (1 - smooth((t - .93) / .07))


save('fx_spark', 32, 8, spark)

rnd = random.Random(3)
SHARD = [(2, 6), (6, 2), (11, 3), (14, 8), (12, 13), (6, 14), (2, 11)]


def inside(px, py, poly):
    c = False
    for i in range(len(poly)):
        (x1, y1), (x2, y2) = poly[i], poly[i - 1]
        if (y1 > py) != (y2 > py) and px < (x2 - x1) * (py - y1) / (y2 - y1) + x1: c = not c
    return c


def shard_a(x, y):
    n = sum(inside(x - .5 + (i % 3) / 3 + 1 / 6, y - .5 + (i // 3) / 3 + 1 / 6, SHARD) for i in range(9))   # сглаженный край: 9 проб на пиксель
    return n / 9


def shard_g(x, y):
    l = ((16 - x) + (16 - y)) / 32                    # свет сверху-слева
    facet = 1.0 if (x + y) < 13 else .78 if (x - y) > 2 else .62
    return .55 + .45 * l * facet + (.15 if (x + y) < 10 else 0)


save('fx_shard', 16, 16, shard_a, shard_g)

BLOBS = [(16, 18, 12), (9, 15, 8.5), (23, 14, 9), (15, 9, 8), (22, 22, 8)]
save('fx_puff', 32, 32, lambda x, y: min(1.0, 1.4 * sum(1 - smooth(r(x, y, bx, by, br)) for bx, by, br in BLOBS)) * (1 - smooth((r(x, y, 16, 16, 16) - .7) / .3)) * .9,
     lambda x, y: .82 + .18 * (1 - y / 32))


def ray(x, y):
    across = abs(x - 4) / 4
    t = y / 64
    return (1 - smooth(across)) ** 1.5 * smooth(t / .35) * (1 - smooth((t - .75) / .25))


save('fx_ray', 8, 64, ray)
print('soft vfx textures written to', OUT)


# ───────────── удар по врагу (Game.Visual.HitFx.cs)
def slash(x, y):
    """Серп удара: дуга окружности, яркий наружный край, мягкий внутренний, концы сужаются. Центр дуги слева, выпуклость вправо."""
    cx, cy, rad = 13.0, 16.0, 14.5                      # дуга целиком внутри 32x32: по высоте ±13 px, наружный край у x≈28
    d = math.hypot(x - cx, y - cy); ang = math.atan2(y - cy, x - cx)
    span = 1.15                                          # полуугол дуги, рад
    t = abs(ang) / span
    if t >= 1: return 0.0
    thick = 4.5 * (1 - t ** 2) + .5                      # к концам серп тоньше
    u = (rad - d) / thick                                # 0 — наружный край, 1 — внутренний
    if u < -.25 or u > 1: return 0.0
    edge = 1 - smooth(-u / .25) if u < 0 else (1 - u) ** 1.6
    return edge * (1 - smooth((t - .55) / .45))


save('fx_slash', 32, 32, slash)
save('fx_ring', 32, 32, lambda x, y: (lambda d: (1 - smooth(abs(d - .82) / .16)))(r(x, y, 16, 16, 16)))


DROPS = [(16, 16, 7), (25, 11, 3.2), (8, 22, 3), (22, 23, 2.6), (9, 9, 2.2), (27, 20, 1.8), (17, 5, 1.6)]     # 32x32: капли крупнее 1 px, поэтому круглые
save('fx_splat', 32, 32, lambda x, y: min(1.0, max(1 - smooth((r(x, y, bx, by, br) - .55) / .45) for bx, by, br in DROPS)) * .95)
print('hit vfx textures written')
