"""Рукотворные объекты художественного прохода: древние фонари, святилища, обелиски, алтари, сундуки, зеркала, приёмники, мосты, пилоны, тотем и врата босса.
Запускается из make_art_pass.py (TILES — первый кадр 16x16; ленты свечения собирает make_anim_tiles.main для нужных индексов).
Пары «выключен/включён» рисуются одной формой с разной палитрой свечения, чтобы объект не «прыгал» при переключении."""
from PIL import Image
from artlib import art, overlay

TILES = {}

STONE = {'1': 'd9dce6', '2': 'aeb2c2', '3': '82869a', '4': '5d6077', '5': '3f4157', 'o': '1d1d2e'}
ANCIENT = {'1': 'c4ccde', '2': '909ab4', '3': '68728b', '4': '49526a', '5': '30374a', 'o': '151a2a'}
VIOLET = {'1': 'aa94d2', '2': '7662a2', '3': '50407a', '4': '36295a', '5': '231b3d', 'o': '120c24'}
NIGHT = {'1': '6c5c9a', '2': '423661', '3': '2a2044', '4': '1b1531', '5': '120e22', 'o': '0a0616'}
CYAN = {'a': 'f4ffff', 'b': '8ff0ff', 'c': '3cb4ec'}
GOLD = {'a': 'fffbe0', 'b': 'ffe27a', 'c': 'f0a030'}
OFF = {'a': '5c6888', 'b': '3c4664', 'c': '272f49'}


def P(base, **extra):
    p = dict(base); p.update(extra); return p


# ───────────────────────── древний фонарь ─────────────────────────
LANTERN = [
    '................',
    '.......oo.......',
    '.....oo11oo.....',
    '....o112223o....',
    '....oooooooo....',
    '.....o1gg3o.....',
    '.....o1gG3o.....',
    '.....o1gg3o.....',
    '....oooooooo....',
    '....o122233o....',
    '.....o1223o.....',
    '.....o1223o.....',
    '.....o1223o.....',
    '...ooo1223ooo...',
    '..o1122223334o..',
    '..oooooooooooo..']
TILES['010_AncOff'] = art('ancoff', LANTERN, P(ANCIENT, g='2b3145', G='3a425c'))
TILES['011_AncOn'] = art('ancon', overlay(LANTERN, [
    '................', '................', '................', '................', '................', '...y........y...',
    '..y..........y..', '...y........y...', '................', '................', '................', '................',
    '................', '................', '................', '................']), P(ANCIENT, g='ffe27a', G='fffbe0', y='ffd24a'))

# ───────────────────────── святилище ─────────────────────────
SHRINE = [
    '................',
    '.......ab.......',
    '......oabo......',
    '......obco......',
    '.....oooooo.....',
    '....o112223o....',
    '....o122233o....',
    '.....o1223o.....',
    '.....o1223o.....',
    '....oo1223oo....',
    '...o11222333o...',
    '...o12222333o...',
    '...oooooooooo...',
    '..o1122222333o..',
    '..o2223333344o..',
    '..oooooooooooo..']
TILES['020_Shrine'] = art('shrine', SHRINE, P(STONE, **CYAN))
TILES['021_ShrineUsed'] = art('shrineused', ['................', '................', '................', '......o44o......'] + SHRINE[4:], STONE)

# ───────────────────────── обелиск ─────────────────────────
OBELISK = [
    '.......oo.......',
    '......o12o......',
    '......o12o......',
    '.....o1223o.....',
    '.....o1r23o.....',
    '.....o1223o.....',
    '.....o12r3o.....',
    '.....o1223o.....',
    '.....o1r23o.....',
    '....o112233o....',
    '....o12r233o....',
    '....o122233o....',
    '....o122233o....',
    '...oo122233oo...',
    '..o1122223334o..',
    '..oooooooooooo..']
TILES['026_Obelisk'] = art('obelisk', OBELISK, P(ANCIENT, r='8ff0ff'))
TILES['027_ObeliskUsed'] = art('obeliskused', OBELISK, P(ANCIENT, r='49526a'))

# ───────────────────────── алтарь босса ─────────────────────────
BOSS = [
    '................',
    '..oo........oo..',
    '..o1o......o1o..',
    '..o12oooooo23o..',
    '...o12222223o...',
    '....o1gGGg3o....',
    '....o12gg23o....',
    '.....o1223o.....',
    '.....o1223o.....',
    '.....o1223o.....',
    '....oo1223oo....',
    '...o11222333o...',
    '...oooooooooo...',
    '..o1122222333o..',
    '..o2223333344o..',
    '..oooooooooooo..']
TILES['024_BossOff'] = art('bossoff', BOSS, P(VIOLET, g='2a2040', G='3a2c58'))
TILES['025_BossOn'] = art('bosson', BOSS, P(VIOLET, g='f0a8ff', G='ffffff'))

# ───────────────────────── сундуки ─────────────────────────
CHEST = [
    '................',
    '................',
    '................',
    '................',
    '..oooooooooooo..',
    '.o111222222223o.',
    '.om1222222223mo.',
    '.om2222222233mo.',
    '.oooooooooooooo.',
    '.ommmmmkkmmmmmo.',
    '.om2222kk2223mo.',
    '.om2222222233mo.',
    '.om2223333333mo.',
    '.om3333333333mo.',
    '.oooooooooooooo.',
    '................']
CHEST_OPEN = [
    '................',
    '................',
    '..oooooooooooo..',
    '.om1222222223mo.',
    '.om2222222233mo.',
    '.oooooooooooooo.',
    '.o555555555555o.',
    '.o55g5gg55g555o.',
    '.oooooooooooooo.',
    '.ommmmmmmmmmmmo.',
    '.om2222222223mo.',
    '.om2222222233mo.',
    '.om2223333333mo.',
    '.om3333333333mo.',
    '.oooooooooooooo.',
    '................']
WOOD = {'1': 'e6ae6c', '2': 'bc7e42', '3': '8c5629', '5': '2a160e', 'm': '7a8098', 'k': 'ffd24a', 'g': 'ffe27a', 'o': '2a1810'}
OAK = {'1': 'b98a58', '2': '8a5e38', '3': '5e3d24', '5': '1e100a', 'm': 'e0b040', 'k': 'fff2a8', 'g': 'ffe27a', 'o': '20120c'}
TILES['015_Chest'] = art('chest', CHEST, WOOD)
TILES['016_ChestOpen'] = art('chestopen', CHEST_OPEN, WOOD)
TILES['200_StorageChest'] = art('storagechest', CHEST, OAK)

# ───────────────────────── зеркала ─────────────────────────
MIRROR = [
    '................',
    '............oo..',
    '...........oabo.',
    '..........oabo..',
    '.........oabo...',
    '........oabo....',
    '.......oabo.....',
    '......oabo......',
    '.....oabo.......',
    '....oabo........',
    '...oabo.........',
    '...ooo..........',
    '.....oooooo.....',
    '....o122223o....',
    '...o12222333o...',
    '...oooooooooo...']
mirror = art('mirror', MIRROR, P(ANCIENT, a='f4ffff', b='8fd8ff'))
TILES['054_Mirror1'] = mirror
TILES['055_Mirror2'] = mirror.transpose(Image.FLIP_LEFT_RIGHT)

# ───────────────────────── приёмник и излучатель ─────────────────────────
DEVICE = [
    '................',
    '................',
    '..oooooooooooo..',
    '.o111111111112o.',
    '.o122222222223o.',
    '.o122oooooo223o.',
    '.o12ollllllo23o.',
    '.o12olLLLLlo23o.',
    '.o12olLLLLlo23o.',
    '.o12ollllllo23o.',
    '.o122oooooo223o.',
    '.o122222222223o.',
    '.o233333333334o.',
    '..oooooooooooo..',
    '...o4o....o4o...',
    '...ooo....ooo...']
TILES['056_Receiver'] = art('receiver', DEVICE, P(ANCIENT, l='1c2234', L='2a3248'))
TILES['057_ReceiverOn'] = art('receiveron', DEVICE, P(ANCIENT, l='ffd24a', L='fffbe0'))
TILES['058_Emitter'] = art('emitter', DEVICE, {'1': 'e6c890', '2': 'b89454', '3': '86683a', '4': '5c4628', 'o': '24180e', 'l': 'ffe27a', 'L': 'ffffff'})


# ───────────────────────── мост и потайная дверь ─────────────────────────
def bridge(broken):
    """Настил из досок: светлая кромка каждой доски, тёмный шов, поперечные брусья сверху и снизу; у сломанного — проломы."""
    pal = {'1': 'd09a5c', '2': 'a8743e', '3': '7e5229', '4': '553518', 'o': '2a1810', 'k': '0e0a10', 'n': 'e8d0a0'}
    holes = {(5, 4), (6, 4), (5, 5), (6, 5), (6, 6), (5, 6), (9, 9), (10, 9), (9, 10), (10, 10), (10, 11), (9, 8), (13, 5), (13, 6), (2, 11), (2, 12)} if broken else set()
    rows = []
    for y in range(16):
        r = ''
        for x in range(16):
            if (x, y) in holes: c = 'k'
            elif y in (0, 15): c = 'o'
            elif y in (1, 14): c = '4' if x % 4 == 3 else '3'
            elif x % 4 == 3: c = '4'
            elif x % 4 == 0: c = '1'
            elif (x % 4 == 1 and y in (3, 12)): c = 'n'                                   # шляпки гвоздей
            else: c = '3' if (x * 5 + y * 3) % 13 == 0 else '2'
            r += c
        rows.append(r)
    return art('bridge', rows, pal)


TILES['063_Bridge'] = bridge(False)
TILES['062_BridgeBroken'] = bridge(True)


def secret_door():
    """Четыре каменных блока с фаской и глазком-кристаллом в центре."""
    pal = P(ANCIENT, a='f4ffff', b='8ff0ff')
    rows = []
    for y in range(16):
        r = ''
        for x in range(16):
            bx, by = x % 8, y % 8
            if 6 <= x <= 9 and 6 <= y <= 9: c = 'o' if x in (6, 9) or y in (6, 9) else ('a' if (x, y) == (7, 7) else 'b')
            elif bx == 7 or by == 7: c = 'o'
            elif bx == 0 or by == 0: c = '2'
            elif bx == 6 or by == 6: c = '4'
            else: c = '3'
            r += c
        rows.append(r)
    return art('secretdoor', rows, pal)


TILES['066_SecretDoor'] = secret_door()

# ───────────────────────── техника и кристаллы ─────────────────────────
TILES['085_TechCache'] = art('techcache', [
    '......oooo......',
    '.....oaabbo.....',
    '....oaabbbco....',
    '....oabbbbco....',
    '....obbbbcco....',
    '.....obbcco.....',
    '...oooooooooo...',
    '...o11222233o...',
    '...oooooooooo...',
    '.....o1223o.....',
    '.....o1y23o.....',
    '.....o1223o.....',
    '....oo1223oo....',
    '...o11222333o...',
    '..o1122223334o..',
    '..oooooooooooo..'], P(ANCIENT, y='ffe27a', **CYAN))

TILES['086_Spire'] = art('spire', [
    '.......oo.......',
    '......oabo......',
    '......oabo......',
    '.....oaabco.....',
    '.....oaabco.....',
    '.....oabbco.....',
    '....oaabbcco....',
    '....oaabbcco....',
    '....oabbbcco....',
    '....oabbbccdo...',
    '...oaabbbccdo...',
    '..ooabbbcccdoo..',
    '.oaoabbccccdodo.',
    '.oabobbcccddocdo',
    '.obcdoooooooocdo',
    '..oooo......ooo.'], {'a': 'ffe8ff', 'b': 'ff9ae8', 'c': 'd858c8', 'd': '8a2c8a', 'o': '3a0c40'})

ALTAR = [
    '................',
    '................',
    '................',
    '................',
    '..oooooooooooo..',
    '.o1ffffffffff3o.',
    '.o12ffffffff23o.',
    '..o1222222223o..',
    '...oo122223oo...',
    '.....o1223o.....',
    '.....o1223o.....',
    '....oo1223oo....',
    '...o11222333o...',
    '..o1122222333o..',
    '.o112222333344o.',
    '.oooooooooooooo.']
TILES['087_Altar'] = art('altar', ALTAR, P(ANCIENT, f='252b3d'))
TILES['088_AltarOn'] = art('altaron', overlay(ALTAR, [
    '................', '.......y........', '......yYy.......', '.....yYYYy......', '....oyYYYYyo....', '...FFFFFFFFFF...',
    '....FFFFFFFF....', '................', '................', '................', '................', '................',
    '................', '................', '................', '................']), P(ANCIENT, f='ffe27a', F='fff2a8', y='ffd24a', Y='fffbe0'))

TILES['098_AmpCrystal'] = art('ampcrystal', [
    '................',
    '.......oo.......',
    '......oabo......',
    '.....oaabco.....',
    '.....oaabco.....',
    '....oaabbcco....',
    '....oaabbcco....',
    '....oabbbcco....',
    '.....obbcco.....',
    '......obco......',
    '....oooooooo....',
    '....o122233o....',
    '...oo122233oo...',
    '..o1122223334o..',
    '..o2223333444o..',
    '..oooooooooooo..'], P(ANCIENT, a='f4ffff', b='9af0ff', c='ff8fe0'))

TILES['099_BarrierPost'] = art('barrierpost', [
    '................',
    '.....oooooo.....',
    '....oaabbbco....',
    '....oabbbbco....',
    '....obbbbcco....',
    '.....oooooo.....',
    '......o12o......',
    '.....oo12oo.....',
    '.....o1223o.....',
    '.....o1223o.....',
    '.....o1223o.....',
    '.....o1223o.....',
    '....oo1223oo....',
    '...o11222333o...',
    '..o1122223334o..',
    '..oooooooooooo..'], P(ANCIENT, **CYAN))

TABLET = [
    '................',
    '....oooooooo....',
    '...o11222223o...',
    '...o12222223o...',
    '...o12yy2y23o...',
    '...o12222223o...',
    '...o12y2yy23o...',
    '...o12222223o...',
    '...o12yy2y23o...',
    '...o12222223o...',
    '...o12y2y223o...',
    '...o12222233o...',
    '...o12222233o...',
    '..oo22223333oo..',
    '..o1122333344o..',
    '..oooooooooooo..']
TILES['116_Tablet'] = art('tablet', TABLET, P(STONE, y='ffd24a'))
TILES['115_Monolith'] = art('monolith', TABLET, P(NIGHT, y='e8a8ff'))

PYLON = [
    '.......oo.......',
    '......oabo......',
    '.....oaabco.....',
    '.....oabbco.....',
    '.....obbcco.....',
    '......obco......',
    '.....oooooo.....',
    '.....o1223o.....',
    '......o12o......',
    '......o12o......',
    '......o12o......',
    '.....oo12oo.....',
    '....o112233o....',
    '...o11222333o...',
    '..o1122223334o..',
    '..oooooooooooo..']
TILES['118_Pylon'] = art('pylon', PYLON, P(ANCIENT, **OFF))
TILES['119_PylonOn'] = art('pylonon', PYLON, P(ANCIENT, **GOLD))

TILES['117_FalseLantern'] = art('falselantern', [
    '................',
    '....oooooooo....',
    '...o11222223o...',
    '....oooooooo....',
    '....oabbbbco....',
    '....oabbbbco....',
    '....obbbbcco....',
    '....oooooooo....',
    '......o12o......',
    '......o12o......',
    '......o12o......',
    '......o12o......',
    '.....oo12oo.....',
    '....o112233o....',
    '...o11222333o...',
    '...oooooooooo...'], P(NIGHT, a='ffe8ff', b='e070ff', c='9030c0'))

TILES['120_ShadowPillar'] = art('shadowpillar', [
    '..oooooooooooo..',
    '.o112222222233o.',
    '.oooooooooooooo.',
    '..o1222v22233o..',
    '..o122vv22233o..',
    '..o122v222v33o..',
    '..o1222222v33o..',
    '..o12v2222233o..',
    '..o12vv222233o..',
    '..o122v22v233o..',
    '..o122222vv33o..',
    '..o1222222v33o..',
    '..o12v2222233o..',
    '.oooooooooooooo.',
    '.o122222223333o.',
    '.oooooooooooooo.'], P(NIGHT, v='c468ff'))

TILES['121_Fragment'] = art('fragment', [
    '................',
    '................',
    '................',
    '...oooooooooo...',
    '.oo1111222222oo.',
    'o11122222223333o',
    'o1222222s233334o',
    'o22s22222333344o',
    '.o222223333344o.',
    '.o333333344445o.',
    '..o3334444455o..',
    '...o44445555o...',
    '....o445555o....',
    '.....o5555o.....',
    '......oooo......',
    '................'], {'1': '9c96b6', '2': '6e6a8a', '3': '4c4866', '4': '33304a', '5': '232035', 'o': '100e1c', 's': 'ffffff'})

TILES['084_Vent'] = art('vent', [
    '................',
    '................',
    '................',
    '................',
    '....oooooooo....',
    '..oo11222222oo..',
    '.o112oooooo233o.',
    'o112okkkkkko233o',
    'o12okkeEEekko33o',
    'o22okkkeekkko33o',
    'o223okkkkkko334o',
    '.o2233oooo3334o.',
    '..oo33333344oo..',
    '....oooooooo....',
    '................',
    '................'], {'1': '80695f', '2': '5d4a44', '3': '443531', '4': '302525', 'o': '0f090b', 'k': '160808', 'e': 'ff7a1e', 'E': 'ffd24a'})

# ───────────────────────── тотем и врата босса ─────────────────────────
TILES['180_BossTotem'] = art('bosstotem', [
    '.....oooooo.....',
    '....o112223o....',
    '...o11222233o...',
    '...o1ee22ee3o...',
    '...o1eE22eE3o...',
    '...o12222233o...',
    '...o12oooo23o...',
    '...o12222233o...',
    '....oooooooo....',
    '....o1v22v3o....',
    '....o12vv23o....',
    '....o1v22v3o....',
    '...oo122233oo...',
    '..o1122223334o..',
    '..o2223333444o..',
    '..oooooooooooo..'], P(ANCIENT, e='c468ff', E='ffffff', v='8a4cd0'))

BAR = 'kabkkabkkabkkabk'; GLINT = 'kAbkkAbkkAbkkAbk'
TILES['181_BossGate'] = art('bossgate', [
    'oooooooooooooooo',
    'o11222222222233o',
    'oooooooooooooooo',
    BAR, BAR, GLINT, BAR, BAR, BAR, GLINT, BAR, BAR, BAR,
    'oooooooooooooooo',
    'o12222222223333o',
    'oooooooooooooooo'], P(VIOLET, k='1a0c30', a='d890ff', b='8a40c8', A='ffffff'))
