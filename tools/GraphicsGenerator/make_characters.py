"""Персонажи Lanther в клетке 16x16: слои внешности героя (Assets/Sprites/Player/Look/{Idle,Walk}/*.png, 4 кадра = лист 64x16)
и листы жителей, торговца и путника (Assets/Sprites/Npc/*.png, 8 кадров = 128x16: 0-1 покой, 2-5 ходьба, 6-7 работа).
Запуск из корня проекта: python tools/GraphicsGenerator/make_characters.py

Все персонажи собираются из одного тела (вид 3/4, смотрит вправо):
  * голова 8x6 (строки 3-8), корпус 6x4 (9-12) с руками по бокам, ноги 2+2 px с просветом (13-15); силуэт не шире 10 px; ряды 0-1 пусты — запас на подъём корпуса при шаге и на контур;
  * ходьба: 4 кадра — шаг правой, проход (корпус на 1 px выше), шаг левой, проход; руки идут в противофазе ногам, фонарь качается;
  * покой: 4 кадра дыхания — корпус и голова опускаются на пиксель и возвращаются, ноги стоят;
  * свет сверху-слева: светлая кромка сверху и слева, тень справа и снизу, тень под подбородком и под поясом;
  * ближняя нога светлее дальней, дальняя рука темнее и уходит за корпус.
Тонируемые слои героя (кожа, волосы, одежда 1 и 2) нарисованы оттенками серого: игра умножает их на цвет палитры Look. Контур героя строит LookArt.cs."""
import os
from PIL import Image

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', 'Sprites')
LOOK = os.path.join(ROOT, 'Player', 'Look')
W = H = 16
N = 4

# ───────────────────────── позы ─────────────────────────
# корпус: dy (вверх — отрицательный); руки: смещение ближней (dxn) и дальней (dxf) по x; ноги: вариант
WALK = [dict(dy=0, arm=-1, legs='a'), dict(dy=-1, arm=0, legs='p'), dict(dy=0, arm=1, legs='b'), dict(dy=-1, arm=0, legs='p')]
IDLE = [dict(dy=0, arm=0, legs='s'), dict(dy=0, arm=0, legs='s'), dict(dy=1, arm=0, legs='s'), dict(dy=1, arm=0, legs='s')]
WORK = [dict(dy=0, arm=0, legs='s', work=0), dict(dy=1, arm=0, legs='s', work=1)]

HEAD = {3: (5, 10), 4: (4, 11), 5: (4, 11), 6: (4, 11), 7: (4, 11), 8: (5, 10)}     # голова 8x6 — уже прежней (10): силуэт стройнее
EYES = [(8, 6), (8, 7), (10, 6), (10, 7)]          # 3/4: оба глаза сдвинуты к передней (правой) стороне лица
MOUTH = (10, 8)
NOSE = (12, 7)

# корпус по вариантам одежды: строки 9-12, символы a — одежда 1, b — одежда 2 (x от 5 до 10, ширина 6)
TORSO = {
    0: ['aabbaa', 'aaabaa', 'bbbbbb', 'aaaaaa'],      # куртка: ворот, застёжка и пояс — второй цвет
    1: ['abaaba', 'aaaaaa', 'aaaaaa', 'aaaaaa'],      # плащ: застёжки, накидка за спиной (CAPE)
    2: ['aabbaa', 'aaaaaa', 'aaaaaa', 'bbbbbb'],      # туника: воротник и подол
    3: ['bbbbbb', 'abaaaa', 'bbbbbb', 'aaaaaa'],      # разведчик: шарф и ремень
}
SLEEVE = {0: 'a', 1: 'a', 2: 'a', 3: 'a'}
CAPE = {1: [(3, 9), (4, 9), (3, 10), (3, 11), (3, 12), (3, 13), (4, 13)]}
SCARF_TAIL = {3: [(4, 9), (3, 10), (3, 11)]}

# причёски: строки с y=2, x 0-15 (h — волосы); не шире головы больше чем на пиксель с каждой стороны
HAIR = {
    0: ['.....hhhhhh.....', '....hhhhhhhh....', '...hhhhhhhhhh...', '...hhhhhhhhh....', '...hh...........', '...h............'],
    1: ['....h.h.h.h.....', '....hhhhhhhh....', '...hhhhhhhhhh...', '...hhh.hh.hh....', '...h............'],
    2: ['.....hhhhhh.....', '....hhhhhhhh....', '...hhhhhhhhhh...', '...hhhhhhhhh....', '...hh......h....', '...hh......h....',
        '...hh...........', '...hh...........', '...h............', '...h............'],
    3: ['.....hhhhhh.....', '....hhhhhhhh....', '..hhhhhhhhhhh...', '..hhhhhhhhhh....', '..hh............', '..h.............', '..h.............'],
    4: ['.....hhhhhh.....', '....hhhhhhhh....', '...hhhhhhhhhh...', '...hhhhhhhhhh...', '...hh.......h...', '...hh...........', '...hhh..........',
        '....hhh.........'],
    5: ['.....hhhhh......', '....hhhhhhh.....', '...hhhhhhhhhhh..', '...hh...........'],
    6: [],
}
# виды: кот — уши и хвост, собака — висячие уши и хвост
SPECIES = {
    0: {},
    1: {(5, 2): 's', (10, 2): 's', (4, 3): 's', (11, 3): 's', (3, 12): 's', (2, 11): 's', (2, 10): 's', (3, 9): 's'},
    2: {(3, 4): 'k', (3, 5): 'k', (3, 6): 'k', (3, 7): 'k', (12, 4): 'k', (12, 5): 'k', (12, 6): 'k', (4, 4): 'k', (11, 4): 'k',
        (3, 11): 's', (3, 10): 's', (2, 9): 's'},
}
FACE = {
    1: {(5, 3): (255, 160, 180), (10, 3): (255, 160, 180), (9, 8): (255, 140, 165), (12, 7): (235, 235, 245), (12, 8): (235, 235, 245)},
    2: {(9, 7): (245, 232, 205), (10, 7): (245, 232, 205), (11, 7): (30, 24, 28), (9, 8): (245, 232, 205), (10, 8): (240, 110, 130)},
}


# ───────────────────────── сборка кадра ─────────────────────────
def frame(variant, hair, species, pose, lamp='hand'):
    """Слои кадра: словарь слой → {(x, y): символ}. Символы: s кожа, k тёмная шерсть, a/b одежда, h волосы, e глаз, m/n ноги (ближняя/дальняя), o/q сапоги, f/l/i фонарь."""
    dy = pose['dy']; arm = pose['arm']; work = pose.get('work')
    L = {k: {} for k in ('skin', 'cloth1', 'cloth2', 'detail', 'legs', 'lamp', 'hair')}

    def put(layer, x, y, ch):
        if 0 <= x < W and 0 <= y < H: L[layer][(x, y)] = ch

    # ноги: от низа корпуса до строки 14, сапоги — строка 15. Ноги по 2 px, между ними просвет.
    legs = pose['legs']
    # (x ближней ноги вверху, x внизу, x сапога), то же для дальней
    LEGS = {'s': ((8, 8, 8), (5, 5, 5)), 'p': ((7, 7, 7), (6, 6, 6)), 'a': ((8, 9, 9), (5, 4, 4)), 'b': ((7, 6, 6), (6, 7, 7))}
    (n0, n1, nb), (f0, f1, fb) = LEGS[legs]
    for y in range(13 + dy, 15):
        nx = n0 if y < 14 else n1; fx_ = f0 if y < 14 else f1
        for x in (fx_, fx_ + 1): put('legs', x, y, 'n')
        for x in (nx, nx + 1): put('legs', x, y, 'm')
    for x in (fb, fb + 1): put('legs', x, 15, 'q')
    for x in (nb, nb + 1, nb + 2): put('legs', x, 15, 'o')                              # ближний сапог с носком вперёд

    # дальняя рука (за корпусом), плащ и шарф
    for (x, y) in CAPE.get(variant, []): put('cloth2', x, y + dy, 'b')
    for (x, y) in SCARF_TAIL.get(variant, []): put('cloth2', x, y + dy - (1 if arm > 0 else 0), 'b')
    fax = 4 - arm
    put('cloth1', fax, 9 + dy, 'A'); put('cloth1', fax, 10 + dy, 'A'); put('skin', fax, 11 + dy, 'S')

    # корпус
    for i, row in enumerate(TORSO[variant]):
        for j, ch in enumerate(row):
            if ch != '.': put('cloth1' if ch == 'a' else 'cloth2', 5 + j, 9 + i + dy, ch)

    # ближняя рука и фонарь (или рабочий инструмент жителя)
    if work is None:
        nax = 11 + arm
        put('cloth1', nax, 9 + dy, SLEEVE[variant]); put('cloth1', nax, 10 + dy, SLEEVE[variant]); put('skin', nax, 11 + dy, 's')
        if lamp == 'hand':                                                               # маленький фонарь 2x3 висит под рукой и качается
            # фонарь качается вбок вслед за рукой и никогда не опускается ниже строки 14: под ним остаётся строка для контура (строка 15 — только сапоги),
            # и не заходит за корпус (x ≥ 11), иначе одежда, нарисованная поверх, его прячет
            ly = 12 + min(dy, 0); lx = nax + 1
            put('lamp', lx, ly, 'f'); put('lamp', lx + 1, ly, 'f')
            put('lamp', lx, ly + 1, 'i'); put('lamp', lx + 1, ly + 1, 'l')
            put('lamp', lx, ly + 2, 'l'); put('lamp', lx + 1, ly + 2, 'l')
        elif lamp == 'belt':
            put('lamp', 10, 11 + dy, 'f'); put('lamp', 10, 12 + dy, 'i'); put('lamp', 11, 12 + dy, 'l')
    else:
        up = work == 0                                                                   # поза работы: рука поднята / опущена
        put('cloth1', 11, 9 + dy, 'a')
        if up: put('skin', 11, 8 + dy, 's')
        else: put('cloth1', 11, 10 + dy, 'a'); put('skin', 11, 11 + dy, 's')

    # голова, глаза, рот, нос
    for y, (x0, x1) in HEAD.items():
        for x in range(x0, x1 + 1): put('skin', x, y + dy, 's')
    for (x, y), ch in SPECIES[species].items():
        put('skin', x, y + dy, ch)
    if species == 0: put('skin', NOSE[0], NOSE[1] + dy, 's')                             # нос выступает за контур головы: видно, куда смотрит персонаж
    for x, y in EYES: put('detail', x, y + dy, 'e')
    if species == 0: put('detail', MOUTH[0], MOUTH[1] + dy, 'u')
    for (x, y), col in FACE.get(species, {}).items(): L['detail'][(x, y + dy)] = col
    if species == 0 and hair in HAIR:
        for i, row in enumerate(HAIR[hair]):
            for x, ch in enumerate(row):
                if ch == 'h': put('hair', x, 2 + i + dy, 'h')
    return L


# ───────────────────────── светотень ─────────────────────────
def shade_layer(px, solid):
    """Оттенок серого для пикселя тонируемого слоя: 1.0 свет, .86 основа, .70 тень, .56 глубокая тень. solid — все непрозрачные пиксели кадра (для краёв)."""
    out = {}
    for (x, y), ch in px.items():
        up = (x, y - 1) in px; down = (x, y + 1) in px; left = (x - 1, y) in px; right = (x + 1, y) in px
        v = .9
        if ch in ('A', 'S'): v = .62                                                     # дальняя рука в тени
        elif ch == 'h':
            v = 1.0 if not up else .62 if not down else .86
            if not right and up: v = min(v, .7)
            if up and down and not up and (x + y) % 3 == 0: v = .94
        else:
            if not up: v = 1.0                                                           # верхняя кромка
            if not left and up: v = max(v, .94)                                          # левая кромка на свету
            if not right: v = .76                                                        # правая кромка в тени
            if not down: v = min(v, .78)                                                 # нижняя кромка
            if ch == 's' and (x, y - 1) in solid.get('hair', {}): v = .74                # тень от чёлки
            if ch == 's' and y >= 8 and not down: v = .76                                # подбородок
            if ch == 'k': v *= .62
        out[(x, y)] = v
    return out


PAL_FIXED = {
    'e': (24, 26, 44), 'u': (150, 84, 80),
    'm': (60, 66, 96), 'n': (40, 44, 66), 'o': (84, 58, 40), 'q': (58, 40, 30),
    'f': (66, 70, 86), 'l': (255, 210, 100), 'i': (255, 250, 222),
}
PAL_LAMPOFF = {'f': (66, 70, 86), 'l': (78, 84, 110), 'i': (118, 126, 156)}


def layer_image(L, name, frames, lampoff=False):
    im = Image.new('RGBA', (W * len(frames), H), (0, 0, 0, 0)); p = im.load()
    for k, F in enumerate(frames):
        px = F[name]
        if name in ('skin', 'cloth1', 'cloth2', 'hair'):
            shades = shade_layer(px, F)
            for (x, y), v in shades.items():
                g = int(255 * v); p[k * W + x, y] = (g, g, g, 255)
        else:
            for (x, y), ch in px.items():
                if isinstance(ch, tuple): p[k * W + x, y] = ch + (255,); continue
                col = (PAL_LAMPOFF if lampoff else PAL_FIXED).get(ch, PAL_FIXED.get(ch))
                if ch == 'e' and (x, y - 1) not in px: col = (52, 56, 86)                # верх глаза чуть светлее низа: без белого блика (на 16 px он читается как очки)
                if ch == 'l' and (x, y + 1) not in px: col = (226, 168, 70) if not lampoff else (58, 62, 84)
                if ch == 'm' and (x - 1, y) not in px: col = (78, 86, 120)               # свет на ближней ноге
                p[k * W + x, y] = col + (255,)
    return im


# ───────────────────────── слои героя ─────────────────────────
def hero():
    for folder, poses in (('Walk', WALK), ('Idle', IDLE)):
        d = os.path.join(LOOK, folder); os.makedirs(d, exist_ok=True)
        def F(v, h, s): return [frame(v, h, s, p, 'belt' if v == 3 else 'hand') for p in poses]
        base = F(0, 6, 0)
        layer_image(None, 'skin', base).save(os.path.join(d, 'skin.png'))
        for s in (1, 2):
            fr = F(0, 6, s)
            layer_image(None, 'skin', fr).save(os.path.join(d, f'skin_s{s}.png'))
            layer_image(None, 'detail', fr).save(os.path.join(d, f'face_s{s}.png'))
        for v in range(4):
            fr = F(v, 6, 0)
            for name, out in (('cloth1', f'cloth1_v{v}'), ('cloth2', f'cloth2_v{v}'), ('legs', f'legs_v{v}'), ('lamp', f'lamp_v{v}')):
                layer_image(None, name, fr).save(os.path.join(d, out + '.png'))
            layer_image(None, 'lamp', fr, lampoff=True).save(os.path.join(d, f'lampoff_v{v}.png'))
            det = layer_image(None, 'detail', fr); det.save(os.path.join(d, f'detail_v{v}.png'))
        for h in HAIR:
            layer_image(None, 'hair', F(0, h, 0)).save(os.path.join(d, f'hair_{h}.png'))


# ───────────────────────── жители, торговец, путник ─────────────────────────
def tint(v, col):
    return tuple(min(255, int(c * v * 1.08)) for c in col) + (255,)


OUTLINE = (20, 17, 22, 255)


def npc_sheet(look, extras=None, tool=None):
    """look: variant, hair, skin, c1, c2, hair_col; extras(frame_index, img_px, k) рисует шляпы и мелочи; tool(px, k, up) — инструмент в рабочих кадрах."""
    poses = IDLE[1:3] + WALK + WORK
    im = Image.new('RGBA', (W * 8, H), (0, 0, 0, 0)); p = im.load()
    for k, pose in enumerate(poses):
        F = frame(look['variant'], look['hair'], 0, pose, lamp='none')
        cols = {'skin': look['skin'], 'cloth1': look['c1'], 'cloth2': look['c2'], 'hair': look['hair_col']}
        sub = {}
        for name in ('legs', 'skin', 'cloth1', 'cloth2', 'detail', 'hair'):
            if name in cols:
                for (x, y), v in shade_layer(F[name], F).items(): sub[(x, y)] = tint(v, cols[name])
            else:
                for (x, y), ch in F[name].items():
                    col = PAL_FIXED[ch]
                    if ch == 'e' and (x, y - 1) not in F[name]: col = (52, 56, 86)
                    if ch == 'm' and (x - 1, y) not in F[name]: col = (78, 86, 120)
                    sub[(x, y)] = col + (255,)
        canvas = {}
        canvas.update(sub)
        if extras: extras(canvas, pose)
        if tool and 'work' in pose: tool(canvas, pose)
        # контур
        sil = set(canvas)
        for y in range(H):
            for x in range(W):
                if (x, y) in sil: continue
                if any((x + dx, y + dy) in sil for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))): canvas[(x, y)] = OUTLINE
        for (x, y), c in canvas.items():
            if 0 <= x < W and 0 <= y < H: p[k * W + x, y] = c
    return im


def C(h): h = h.lstrip('#'); return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def hat(rows, y0, col, shade=None):
    """Шляпа поверх головы: строки символов (h — основной цвет, d — тень, l — блик) с верхней строки y0; dy позы учитывается."""
    def draw(cv, pose):
        dy = pose['dy']
        for i, r in enumerate(rows):
            for x, ch in enumerate(r):
                if ch == '.': continue
                c = col if ch == 'h' else tuple(int(v * .72) for v in col) if ch == 'd' else tuple(min(255, int(v * 1.2)) for v in col)
                cv[(x, y0 + i + dy)] = c + (255,)
    return draw


def apron(col):
    def draw(cv, pose):
        dy = pose['dy']
        for y in (11, 12):
            for x in range(6, 10): cv[(x, y + dy)] = (col if y == 11 or x < 9 else tuple(int(v * .78) for v in col)) + (255,)
        cv[(6, 10 + dy)] = col + (255,); cv[(9, 10 + dy)] = col + (255,)
    return draw


def combo(*fns):
    def draw(cv, pose):
        for f in fns: f(cv, pose)
    return draw


def tool_fn(kind, handle, head):
    def draw(cv, pose):
        dy = min(pose['dy'], 0); up = pose['work'] == 0                                 # инструмент не опускается вместе с корпусом: иначе упирается в нижний край кадра
        def put(x, y, c):
            if 0 <= x < W and 1 <= y < H: cv[(x, y)] = c + (255,)
        if kind in ('axe', 'pick'):
            if up:
                for i in range(6): put(12, 8 + dy - i + 1, handle)
                if kind == 'axe': put(13, 3 + dy, head); put(13, 4 + dy, head); put(11, 3 + dy, head)
                else:
                    for x in range(10, 15): put(x, 3 + dy, head)
                    put(10, 4 + dy, head); put(14, 4 + dy, head)
            else:
                for i in range(4): put(12, 11 + dy + i - 1, handle)
                if kind == 'axe': put(13, 13 + dy, head); put(13, 14 + dy, head)
                else:
                    for x in range(10, 14): put(x, 14 + dy, head)
        elif kind == 'hammer':
            if up:
                for i in range(4): put(12, 7 + dy - i + 1, handle)
                for x in range(11, 14): put(x, 3 + dy, head); put(x, 4 + dy, head)
            else:
                for i in range(3): put(12, 11 + dy + i, handle)
                for x in range(11, 14): put(x, 13 + dy, head); put(x, 14 + dy, head)
                put(10, 14, (255, 190, 80)); put(14, 12 + dy, (255, 190, 80))             # искры от наковальни (внутри кадра)
        elif kind == 'can':
            y = 9 if up else 11
            for x in range(11, 14): put(x, y + dy, head); put(x, y + 1 + dy, head)
            put(14 - 1, y - 1 + dy, handle); put(13, y + 2 + dy, (150, 200, 230)) if not up else None
        else:   # wrench
            for i in range(4): put(12, (5 if up else 8) + dy + i, handle)
            put(11, (4 if up else 7) + dy, head); put(13, (4 if up else 7) + dy, head); put(12, (4 if up else 7) + dy, head)
    return draw


SKIN = C('f0c8a0')
NPCS = {
    'villager': (dict(variant=2, hair=0, skin=SKIN, c1=C('4a8a4a'), c2=C('3a6c3a'), hair_col=C('6a4a2a')),
                 combo(hat(['.....hhhhhh.....', '....lhhhhhhd....', '..hhhhhhhhhhhh..'], 2, C('d9b84a')), apron(C('d9d2b8'))), None),
    'trader': (dict(variant=1, hair=2, skin=C('e8c8a0'), c1=C('8a5aaa'), c2=C('5a3a7a'), hair_col=C('3a2a2a')),
               hat(['......hhh.......', '....lhhhhhhd....', '..dhhhhhhhhhhd..'], 2, C('6a4a8a')), None),
    'traveler': (dict(variant=3, hair=4, skin=C('e8c8a0'), c1=C('7a808c'), c2=C('c9a24a'), hair_col=C('6c7486')),
                 lambda cv, pose: [cv.__setitem__((x, y + pose['dy']), C('8a6a40') + (255,)) for (x, y) in ((3, 9), (3, 10), (3, 11), (2, 10), (2, 11))], None),
    'v_lumberjack': (dict(variant=0, hair=0, skin=SKIN, c1=C('a84034'), c2=C('6a2a24'), hair_col=C('7a4a2a')),
                     combo(hat(['.....hhhhhh.....', '....hhhhhhhh....'], 2, C('7a2a24')),
                           lambda cv, pose: [cv.__setitem__((x, 8 + pose['dy']), C('7a4a2a') + (255,)) for x in range(6, 11)]), tool_fn('axe', C('6e4a28'), C('b0b4c0'))),
    'v_miner': (dict(variant=0, hair=6, skin=SKIN, c1=C('546078'), c2=C('3a4258'), hair_col=C('3a3030')),
                combo(hat(['.....hhhhhh.....', '....hhhhhhhh....', '...hhhhhhhhhh...'], 2, C('9aa0ae')),
                      lambda cv, pose: cv.__setitem__((9, 3 + pose['dy']), (255, 226, 122, 255))), tool_fn('pick', C('6e4a28'), C('bec4ce'))),
    'v_smith': (dict(variant=2, hair=1, skin=C('d8a880'), c1=C('5c4634'), c2=C('3a2c24'), hair_col=C('2a2020')),
                combo(lambda cv, pose: [cv.__setitem__((x, 4 + pose['dy']), C('aa362c') + (255,)) for x in range(3, 12)], apron(C('3a2e26'))), tool_fn('hammer', C('5a3c24'), C('787e8c'))),
    'v_farmer': (dict(variant=2, hair=0, skin=SKIN, c1=C('c4a046'), c2=C('46649e'), hair_col=C('8a5a2c')),
                 combo(hat(['.....hhhhhh.....', '....lhhhhhhd....', '..hhhhhhhhhhhh..'], 2, C('d9b84a')), apron(C('46649e'))), tool_fn('can', C('3c78aa'), C('96c8e6'))),
    'v_engineer': (dict(variant=0, hair=1, skin=SKIN, c1=C('3c828c'), c2=C('e2e5ee'), hair_col=C('243c46')),
                   combo(lambda cv, pose: [cv.__setitem__((x, 5 + pose['dy']), (C('8ce6f0') if x in (8, 10) else C('3c4650')) + (255,)) for x in range(4, 12)],
                         lambda cv, pose: cv.__setitem__((8, 11 + pose['dy']), (230, 190, 70, 255))), tool_fn('wrench', C('aa823c'), C('d6d6e0'))),
}


def npcs():
    for name, (look, extras, tool) in NPCS.items():
        p = os.path.join(ROOT, 'Npc', name + '.png'); npc_sheet(look, extras, tool).save(p); check_npc(p)


def check():
    """Ни один кадр не должен касаться края: фонарь, руки и инструменты — не ниже строки 14 и не правее столбца 14 (контур рисуется снаружи силуэта)."""
    bad = []
    for name, poses in (('walk', WALK), ('idle', IDLE)):
        for k, pose in enumerate(poses):
            for v in range(4):
                F = frame(v, 0, 0, pose, 'belt' if v == 3 else 'hand')
                for layer, px in F.items():
                    for (x, y) in px:
                        if layer == 'legs' and y == 15: continue                       # сапоги стоят на нижней строке
                        if x < 1 or x > 14 or y < 1 or y > 14: bad.append((name, k, v, layer, x, y))
    assert not bad, f'frames touch the edge: {bad[:8]}'


def check_npc(path):
    """Листы жителей: ничего, кроме контура и сапог, не касается края кадра."""
    im = Image.open(path).convert('RGBA'); bad = []
    for k in range(im.width // W):
        for y in range(H):
            for x in range(W):
                p = im.getpixel((k * W + x, y))
                if p[3] == 0 or p == OUTLINE: continue
                if x in (0, W - 1) or y == 0 or (y == H - 1 and p[:3] not in (PAL_FIXED['o'], PAL_FIXED['q'])): bad.append((k, x, y))
    assert not bad, f'{os.path.basename(path)}: frames touch the edge {bad[:8]}'


if __name__ == '__main__':
    check(); hero(); npcs()
    print('characters written: hero look layers (4 frames) and', len(NPCS), 'npc sheets (8 frames)')
