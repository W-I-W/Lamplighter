"""Анимация тайлов окружения и иконок предметов: из статичного кадра 16x16 (первый кадр файла) собирается лента из 4 кадров 64x16.
Запуск: python tools/GraphicsGenerator/make_anim_tiles.py   (повторный запуск безопасен: берётся только первый кадр)
Игра кладёт кадры в строки ниже основного тайла атласа и проигрывает их (TileSetAtlasSource animation / UiIcons.Tick)."""
import os, glob
from PIL import Image

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', 'Sprites')
N = 4


def luma(p): return (p[0] * 299 + p[1] * 587 + p[2] * 114) / 255000.0


def mul(p, f): return (min(255, int(p[0] * f)), min(255, int(p[1] * f)), min(255, int(p[2] * f)), p[3])


def add(p, v): return (min(255, p[0] + v), min(255, p[1] + v), min(255, p[2] + v), p[3])


def pulse(base, k, amp=.28, th=.5):
    """Светящиеся пиксели (светлее порога) дышат: кадры 1.0 → 1+amp → 1 → ...  Тёмные не меняются."""
    f = [1.0, 1 + amp * .6, 1 + amp, 1 + amp * .6][k]
    im = base.copy(); px = im.load()
    for y in range(16):
        for x in range(16):
            p = px[x, y]
            if p[3] and luma(p) > th: px[x, y] = mul(p, f)
    return im


def sparkle(base, k, amp=.18, th=.55, n=2):
    """Дыхание + блик: самые яркие пиксели по очереди вспыхивают белым."""
    im = pulse(base, k, amp, th); px = im.load()
    pts = sorted([(luma(px[x, y]), x, y) for y in range(16) for x in range(16) if px[x, y][3]], reverse=True)[:max(n * N, 1)]
    for i, (_, x, y) in enumerate(pts):
        if i % N == k: px[x, y] = add(px[x, y], 70)
    return im


def flicker(base, k, th=.5):
    """Огонь: яркие пиксели верхней части на кадрах 1 и 3 прыгают на строку вверх, цвет мерцает."""
    out = Image.new('RGBA', (16, 16), (0, 0, 0, 0)); sp = base.load(); op = out.load()
    for y in range(16):
        for x in range(16):
            p = sp[x, y]
            if not p[3]: continue
            ny = y
            if luma(p) > th and k in (1, 3) and y > 0 and sp[x, y - 1][3] == 0: ny = y - 1
            elif luma(p) > th and k == 2: p = mul(p, 1.18)
            if luma(p) > th and k == 1: p = mul(p, 1.1)
            op[x, ny] = p if not op[x, ny][3] or ny == y else op[x, ny]
    for y in range(16):
        for x in range(16):
            p = sp[x, y]
            if p[3] and not op[x, y][3]: op[x, y] = p if luma(p) <= th else op[x, y]
    return out


def steam(base, k):
    """Пар/дым: полупрозрачные светлые пиксели плывут вверх по кругу на 1 строку за кадр."""
    out = base.copy(); op = out.load(); sp = base.load()
    for y in range(16):
        for x in range(16):
            p = sp[x, y]
            if p[3] and luma(p) > .55:
                op[x, y] = (p[0], p[1], p[2], 0) if False else p
    if k:
        for y in range(1, 16):
            for x in range(16):
                p = sp[x, y]
                if p[3] and luma(p) > .6 and not sp[x, y - 1][3]:
                    if (x + y + k) % 3 == 0: op[x, y - 1] = (p[0], p[1], p[2], 150)
    return out


MODES = {
    # окружение (индекс тайла → способ)
    9: sparkle, 11: sparkle, 12: flicker, 13: flicker, 22: sparkle, 25: sparkle, 50: sparkle, 53: pulse, 57: pulse, 58: pulse, 59: flicker, 60: flicker, 65: pulse,
    82: flicker, 84: steam, 88: pulse, 98: sparkle, 100: sparkle, 110: sparkle, 114: pulse, 115: pulse, 117: flicker, 119: pulse, 121: sparkle, 122: pulse, 124: sparkle, 129: pulse,
    48: pulse, 47: pulse,
}
ICONS = {4: sparkle, 7: sparkle, 11: sparkle, 14: pulse, 15: sparkle, 16: sparkle, 18: sparkle, 19: sparkle, 20: sparkle, 8: sparkle, 12: sparkle, 13: flicker}


def build(path, fn):
    base = Image.open(path).convert('RGBA').crop((0, 0, 16, 16))
    sheet = Image.new('RGBA', (16 * N, 16), (0, 0, 0, 0))
    for k in range(N): sheet.paste(fn(base, k), (16 * k, 0))
    sheet.save(path)


def idx_of(name, prefix=''):
    try: return int(name[len(prefix):len(prefix) + 3])
    except ValueError: return -1


done = 0
for folder in ('Environment', 'Buildings', 'Tilesets'):
    for f in glob.glob(os.path.join(ROOT, folder, '*.png')):
        i = idx_of(os.path.basename(f))
        if i in MODES: build(f, MODES[i]); done += 1
for f in glob.glob(os.path.join(ROOT, 'Items', 'icon_*.png')):
    i = idx_of(os.path.basename(f), 'icon_')
    if i in ICONS: build(f, ICONS[i]); done += 1
print('animated strips:', done)
