"""Общие помощники пиксельной графики для make_art_pass*.py: рисунок задаётся строками символов (16 символов на строку), палитра — словарём символ → цвет.
'.' — прозрачный пиксель. Проверяется размер каждой строки, чтобы кадр не «уехал» и не обрезался."""
import os
from PIL import Image

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', 'Sprites')


def rgba(c):
    if isinstance(c, str):
        c = c.lstrip('#'); c = tuple(int(c[i:i + 2], 16) for i in range(0, len(c), 2))
    return tuple(c) + (255,) if len(c) == 3 else tuple(c)


def check(name, rows, w=16, h=16):
    assert len(rows) == h, f'{name}: {len(rows)} rows instead of {h}'
    for i, r in enumerate(rows):
        assert len(r) == w, f'{name}: row {i} is {len(r)} wide instead of {w}: "{r}"'


def art(name, rows, pal, w=16, h=16):
    """Кадр из строк символов."""
    check(name, rows, w, h)
    im = Image.new('RGBA', (w, h), (0, 0, 0, 0)); px = im.load()
    for y, r in enumerate(rows):
        for x, ch in enumerate(r):
            if ch == '.' or ch == ' ': continue
            assert ch in pal, f'{name}: unknown symbol "{ch}" at {x},{y}'
            px[x, y] = rgba(pal[ch])
    return im


def overlay(rows, over):
    """Накладывает строки over поверх rows ('.' — оставить нижний пиксель)."""
    out = []
    for a, b in zip(rows, over):
        assert len(a) == len(b), f'overlay width: "{a}" / "{b}"'
        out.append(''.join(cb if cb != '.' else ca for ca, cb in zip(a, b)))
    return out


def squash(rows, k):
    """Второй кадр покоя: строки выше k опускаются на пиксель (строка k-1 пропадает), низ стоит на месте — существо «дышит» и не выходит за кадр."""
    w = len(rows[0])
    return ['.' * w] + rows[:k - 1] + rows[k:]


def replace(rows, repl):
    out = list(rows)
    for y, r in repl.items(): out[y] = r
    return out


def strip(frames):
    w, h = frames[0].size
    s = Image.new('RGBA', (w * len(frames), h), (0, 0, 0, 0))
    for i, f in enumerate(frames): s.paste(f, (i * w, 0))
    return s


def margin_report(name, im, fw=16, need=1):
    """Сообщает, если кадр касается края (спрайт может выглядеть обрезанным при растяжении/контуре)."""
    bad = []
    for f in range(im.width // fw):
        fr = im.crop((f * fw, 0, f * fw + fw, im.height)); bb = fr.getbbox()
        if bb and (bb[0] < need or bb[2] > fw - need or bb[1] < need): bad.append((f, bb))
    if bad: print(f'  ! {name}: frame touches the edge {bad}')
    return not bad


def save(folder, name, im):
    p = os.path.join(ROOT, folder, name)
    assert os.path.exists(p), f'no such sprite to replace: {p}'          # размеры и имена прежние: код менять не нужно
    old = Image.open(p)
    if folder != 'Environment' and folder != 'Tilesets':
        assert old.size == im.size, f'{name}: size {im.size} != old {old.size}'
    im.save(p)
