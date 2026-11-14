"""Художественный проход: враги и боссы (Assets/Sprites/Enemies/*.png), жители, торговец и путник (Assets/Sprites/Npc/*.png). Лист 32x16 = два кадра 16x16, как и раньше (Resources/VisualConfigs/Entities/*.tres не меняются).
Запуск из корня проекта: python tools/GraphicsGenerator/make_art_pass_chars.py
Рисунок задаётся строками по 14 символов: по одному пустому пикселю слева и справа и пустая строка сверху гарантированы, поэтому при растяжении,
сжатии и вспышке попадания (Game.Visual.EnemyAnim.cs) спрайт не обрезается краем кадра и не «цепляет» соседний кадр листа.
Второй кадр — «дыхание»: верх опускается на пиксель (squash), плюс замены отдельных строк (шаг лап, мерцание, щупальца)."""
from artlib import art, squash, strip, save, margin_report

SHEETS = {}


def rows16(rows14):
    assert len(rows14) <= 15, f'{len(rows14)} rows: the top row must stay empty'
    for r in rows14: assert len(r) == 14, f'row "{r}" is {len(r)} wide instead of 14'
    return ['.' * 16] * (16 - len(rows14)) + ['.' + r + '.' for r in rows14]


def enemy(name, rows14, pal, k=8, repl=None, swap=None, shift=None):
    """k — строка сжатия второго кадра (0 — без сжатия); repl — {номер строки 0..15: новая строка из 14 символов};
    swap — {символ: символ} для мерцания; shift — (x0, x1, dy): сдвиг столбцов (изгиб тела)."""
    a = rows16(rows14)
    b = squash(a, k) if k else list(a)
    if shift:
        x0, x1, dy = shift; g = [list(r) for r in b]
        for x in range(x0, x1):
            col = [b[y][x] for y in range(16)]
            for y in range(16): g[y][x] = col[y - dy] if 0 <= y - dy < 16 else '.'
        b = [''.join(r) for r in g]
    for y, r in (repl or {}).items():
        assert len(r) == 14, f'{name}: repl row {y} is {len(r)} wide'
        b[y] = '.' + r + '.'
    if swap: b = [''.join(swap.get(c, c) for c in r) for r in b]
    sheet = strip([art(name, a, pal), art(name + ' f2', b, pal)])
    margin_report(name, sheet)
    SHEETS[name] = sheet


# ───────────────────────── тени и лес ─────────────────────────
enemy('shade', [
    '....oooooo....',
    '..oo122222oo..',
    '.o1122222222o.',
    '.o1222222223o.',
    'o122222222233o',
    'o2ee2222ee233o',
    'o2EE2222EE233o',
    'o222222222333o',
    'o222233222333o',
    'o222333333333o',
    'o233333333334o',
    'o33o3333o3334o',
    '.oo.o33o.o33o.',
    '.....oo...oo..'], {'1': '7a62b4', '2': '4c3a86', '3': '32245e', '4': '221844', 'e': 'ffb0ee', 'E': 'ff48c8', 'o': '0e0a20'},
    k=11, repl={14: '.o.oo33o.o3o..', 15: '....oo....o...'})

enemy('wraith', [
    '.....oooo.....',
    '...oo1122oo...',
    '..o11222223o..',
    '..o12kkkk23o..',
    '..o1kekkek3o..',
    '..o12kkkk23o..',
    '.oo12222223oo.',
    'o112222222233o',
    'oco22222223oco',
    '..o12222223o..',
    '..o12222233o..',
    '..o12323233o..',
    '..oo2o33o23o..',
    '....o.oo.oo...'], {'1': '5a7cc0', '2': '34508c', '3': '20325e', 'k': '080c1c', 'e': '8ff6ff', 'c': '60ccff', 'o': '0a1024'},
    k=12, repl={14: '..o2o33o2o3o..', 15: '...o..oo..o...'})

enemy('spitter', [
    '..oo......oo..',
    '.oyyo....oyyo.',
    '.oykoooooykoo.',
    '.o1122222223o.',
    'o112222222233o',
    'o122222222233o',
    'o12oooooooo33o',
    'o2omwmmmwmmo3o',
    'o2ommmammmmo3o',
    'o22oooaoooo33o',
    'o222222a22333o',
    '.o2233333333o.',
    '.oo33o33o33oo.',
    '..oo.oooo.oo..'], {'1': 'b4e868', '2': '70b444', '3': '427c32', 'y': 'ffec68', 'k': '201808', 'm': '5a1424', 'w': 'fff4d0', 'a': 'dcff58', 'o': '14280f'},
    k=13)

enemy('bomber', [
    '........oo....',
    '.......osf....',
    '......of......',
    '....oooooo....',
    '..oo112222oo..',
    '.o1122222223o.',
    '.o1222222223o.',
    'o12yy2222yy23o',
    'o22yY2222yY33o',
    'o222222222233o',
    'o22y2y2y2y233o',
    'o222y2y2y2333o',
    '.o2233333333o.',
    '..oo333333oo..',
    '...oo....oo...'], {'1': 'f48468', '2': 'c43c36', '3': '821c2a', 'y': 'ffe468', 'Y': 'ff9a2e', 'f': '8a6a40', 's': 'fff6a0', 'S': 'ff9a2e', 'o': '2a0a10'},
    k=0, repl={1: '.......oso....', 2: '......oSf.....', 15: '..oo......oo..'})

# ───────────────────────── пещеры ─────────────────────────
enemy('eel', [
    '..........oooo',
    '.........o112o',
    '..oo....o1y22o',
    '.o12o..o122r3o',
    'o1223oo12233o.',
    'o22233122333o.',
    '.o333322333o..',
    '..oooo3333o...',
    '......oooo....'], {'1': '7ce0d0', '2': '3ca8a8', '3': '22707c', 'y': 'ffe060', 'r': 'ff5a48', 'o': '0a2834'},
    k=0, shift=(0, 7, -1))

enemy('leech', [
    '...oooooooo...',
    '..o11222222o..',
    '.o1122222223o.',
    'o112222222233o',
    'o12ee2222ee23o',
    'o12eE2222eE23o',
    'o122222222233o',
    'o222oooooo233o',
    'o22owwmmwwo33o',
    'o223ommmmo333o',
    '.o233oooo333o.',
    'o.o33333333o.o',
    '.o.oooooooo.o.'], {'1': '5a5290', '2': '343060', '3': '201c3e', 'e': '8ff6ff', 'E': 'ffffff', 'w': 'ffffff', 'm': '5a1030', 'o': '0a0818'},
    k=10, repl={14: '.oo33333333oo.', 15: 'o..oooooooo..o'})

enemy('beetle', [
    '...ooooooo....',
    '.oo1112o22oo..',
    'o111122o2223o.',
    'o1w122o22233oo',
    'o1122o222233ho',
    'o1222o22223hyo',
    'o2222o22233hho',
    'o2223o2333ohoo',
    '.o333o3333oo..',
    '..ooooooooo...',
    '.o.o..o..o.o..'], {'1': 'b0cceA'.lower(), '2': '6c94c4', '3': '3e5c90', 'w': 'eef8ff', 'h': '2c3c5c', 'y': 'ffd24a', 'o': '101c34'},
    k=0, repl={15: '..o.o.o.o.o...'})

enemy('depthwarden', [
    '....oooooo....',
    '..oo112222oo..',
    '.o1122222223o.',
    '.o12wwwwww23o.',
    'o12wwwkkwww23o',
    'o12wwkkckww23o',
    'o12wwwkkwww23o',
    'o122wwwwww233o',
    'o222222222233o',
    'o2s222s222s33o',
    '.o22o222o223o.',
    'o2o.o23o.o2o3o',
    'oso.oso..oso..',
    '.o...o....o...'], {'1': '64aae8', '2': '2e70b8', '3': '1a4680', 'w': 'eef8ff', 'k': '0a1c3c', 'c': '8ff6ff', 's': '9cd8ff', 'o': '081830'},
    k=11, repl={13: '.o2o.o23o.o23o', 14: '.oso..oso.oso.', 15: '..o....o...o..'})

# ───────────────────────── пепельные земли ─────────────────────────
enemy('imp', [
    '.....o..o.....',
    '....oao.oao...',
    '...oabaoabo...',
    '...oabbabbao..',
    '..oabbbbbbbao.',
    '..obbkbbbkbbo.',
    '..obbkbbbkbbo.',
    '..ocbbbkkbbco.',
    '...occbbbcco..',
    '..oocyyyyycoo.',
    '.ocoocyyycooco',
    '.oo.occccco.oo',
    '....oco.oco...',
    '....oo...oo...'], {'a': 'fff2a8', 'b': 'ffa432', 'c': 'e2481a', 'y': 'ffdc68', 'k': '401008', 'o': '3a0c0c'},
    k=0, repl={2: '....o..o......', 3: '...oao.oao....', 4: '...oabaoabao..'})

enemy('golem', [
    '....oooooo....',
    '...o112223o...',
    '...o1e22e3o...',
    '...o122223o...',
    '.ooo2llll3ooo.',
    'o11o222223o23o',
    'o12o22l223o23o',
    'o12o2ll233o23o',
    'o22o22l333o33o',
    'o23o223333o33o',
    '.oo.o2333o.oo.',
    '...o23oo33o...',
    '...o22o.o33o..',
    '...ooo...ooo..'], {'1': 'a89890', '2': '74655f', '3': '4a4141', 'l': 'ff9a2e', 'L': 'ffe27a', 'e': 'ffdc68', 'o': '181214'},
    k=11, swap={'l': 'L'})

enemy('spirit', [
    '......oo......',
    '.....ofo......',
    '....oooooo....',
    '...o112222o...',
    '..o11222222o..',
    '..o1e222e23o..',
    '..o1e222e23o..',
    '..o12222223o..',
    '.oo12222223oo.',
    'o1o2222223o23o',
    '.oo2222233oo..',
    '..o2223233o...',
    '...o23o23o....',
    '....oo.oo.....'], {'1': 'd8d4d0', '2': 'a09a95', '3': '6c6664', 'e': 'ff8a2e', 'f': 'ffb040', 'F': 'fff2a8', 'o': '2a2424'},
    k=10, swap={'f': 'F'}, repl={14: '..o23o23o.....', 15: '...oo.oo......'})

enemy('ashwarden', [
    '.oo........oo.',
    '.oho......oho.',
    '.ohhoooooohho.',
    '..oo122223oo..',
    '..o11222223o..',
    '..o1yy22yy3o..',
    '..o12222223o..',
    '.oo2llllll3oo.',
    'o11o2l2l2l3o3o',
    'o12o2222233o3o',
    'o22o22ll233o3o',
    'o23o2llll33o3o',
    '.oo.o233333oo.',
    '...o22o.o33o..',
    '...ooo...ooo..'], {'1': '927a6c', '2': '604f48', '3': '3c3030', 'h': 'ff8a2e', 'y': 'ffe468', 'l': 'ff9a2e', 'L': 'ffe27a', 'o': '140c0e'},
    k=0, swap={'l': 'L'})

# ───────────────────────── ледяной край ─────────────────────────
enemy('icewolf', [
    '........o1oo1o',
    '.o......o1111o',
    'o1o....o11r11o',
    'o12ooo111112ko',
    '.o2111111122oo',
    '.o1111111122o.',
    '.o1111112222o.',
    '.o2211122223o.',
    '.o2o2222o23o..',
    '.o2o.ooo.o2o..',
    '.o3o.....o3o..',
    '.oo......oo...'], {'1': 'eaf3fc', '2': 'acc0dc', '3': '748cb4', 'r': 'ff4444', 'k': '202838', 'o': '1c2a48'},
    k=0, repl={12: '.o2o2222o23o..', 13: '..o2ooooo2o...', 14: '..o3o...o3o...', 15: '..oo....oo....'})

enemy('frostpriest', [
    '....oooo...s..',
    '...o1122o.sws.',
    '..o112222o.s..',
    '..o1kkkk3o.t..',
    '..o1EkkE3o.t..',
    '..o12kk23o.t..',
    '.oo1222222oto.',
    'o112222222hto.',
    'o122222223ot..',
    '.o12222223ot..',
    '.o12222233ot..',
    '.o12222233ot..',
    'o122222233oto.',
    'o222233333oto.',
    '.oooooooooo.o.'], {'1': 'e4f4ff', '2': 'a0ccf0', '3': '5e90c8', 'k': '0c1830', 'E': '8ff6ff', 's': 'aee6ff', 'S': 'ffffff', 'w': 'ffffff', 't': '7a5a3c', 'h': 'e8c8a8', 'o': '14284c'},
    k=0, swap={'s': 'S'})

enemy('sentinel', [
    '....oooooo....',
    '...o112223o...',
    '...o1e22e3o...',
    '.o.o122223o.o.',
    'oao.oooooo.oao',
    'oaaoo1122ooaao',
    'o1ao112222oa3o',
    'o12o122222o23o',
    'o12o122223o23o',
    'o22o222233o33o',
    '.oo.o22333o.oo',
    '...o22oo33o...',
    '...o22o.o33o..',
    '...ooo...ooo..'], {'1': 'ecfaff', '2': 'a4d2f2', '3': '5e98d0', 'a': 'ffffff', 'e': '0c3464', 'o': '16305c'},
    k=11)

enemy('winterwarden', [
    '..o..o..o..o..',
    '.oaooaooaooao.',
    '.oaaaaaaaaaao.',
    '.o1122222223o.',
    'o11wwwwwww223o',
    'o1wwwkkkwww23o',
    'o1wwkkbkkww23o',
    'o1wwwkkkwww23o',
    'o12wwwwwww233o',
    '.o1222222233o.',
    '.o122a222a33o.',
    '..o12o22o23o..',
    '..o2o.oo.o3o..',
    '...o......o...'], {'1': 'dceeff', '2': '8fc0ec', '3': '5488c4', 'a': 'ffffff', 'w': 'ffffff', 'k': '10284c', 'b': '48ccff', 'o': '1a3a6c'},
    k=12)

# ───────────────────────── сердце тьмы ─────────────────────────
enemy('mirage', [
    '....oooooo....',
    '...o112222o...',
    '..o11222222o..',
    '..o1kkkkkk3o..',
    '..o1kykkyk3o..',
    '..o12kkkk23o..',
    '.oo122222233oo',
    'oa1222222223ao',
    '.oo122222233oo',
    '..o12222233o..',
    '.o1222222333o.',
    '.o2222223333o.',
    '.o2o232o233oo.',
    '..o.ooo.ooo...'], {'1': 'cc9cf4', '2': '945ccc', '3': '5e3294', 'k': '180828', 'y': 'ffdc68', 'a': 'ecccfa', 'o': '200c3c'},
    k=12, repl={14: '.oo232o2o33o..', 15: '...ooo.o.oo...'})

enemy('weaver', [
    '....oooooo....',
    'o..o112223o..o',
    'po.o1e22e3o.op',
    'opoo122223oopo',
    '.oo11222233oo.',
    'ooo122m22333oo',
    'opo12mmm2333po',
    'po.o22m233o.op',
    'o...o23333o..o',
    'o....oooo....o'], {'1': '9068d0', '2': '603ca0', '3': '3e266c', 'e': 'ff68d8', 'm': 'ccA4ff'.lower(), 'p': '7a56b8', 'o': '160a30'},
    k=0, repl={7: '...o112223o...', 8: 'oo.o1e22e3o.oo', 9: 'ppoo122223oopp', 14: '.o..o23333o.o.', 15: '.o...oooo...o.'})

enemy('stalker', [
    '.....oooo.....',
    '....o1222o....',
    '....occcco....',
    '....o2223o....',
    '....o2223o....',
    '.o33o1223o33o.',
    'obo.o1223o.obo',
    'obo.o1223o.obo',
    'obo.o2223o.obo',
    '.o..o2233o..o.',
    '....o2o23o....',
    '....o2o.o3o...',
    '...o2o..o3o...',
    '...ooo..ooo...'], {'1': '525878', '2': '2e3250', '3': '1c1e34', 'c': '8ff6ff', 'b': 'dce8fa', 'o': '08080f'},
    k=10)

enemy('source', [
    '....oooooo....',
    '..oop1111poo..',
    '.op11222211po.',
    '.o112wwww211o.',
    'op12wwwwww21po',
    'o112wwkkww211o',
    'o112wkkkkw211o',
    'o112wkkvkw211o',
    'o112wwkkww211o',
    'op12wwwwww21po',
    '.o112wwww211o.',
    '.op11222211po.',
    '..oop1111poo..',
    '....oooooo....'], {'1': '6440ac', '2': '2e1a64', 'w': 'f2eaff', 'k': '1a0a30', 'v': 'c468ff', 'V': 'ffffff', 'p': 'c888ff', 'P': 'ffffff', 'o': '0c0420'},
    k=0, swap={'p': 'P', 'v': 'V'})


# ───────────────────────── жители, торговец, путник ─────────────────────────
# Кадры 16x16: 0 — стоит, 1 — шаг. Цвета f/F (рубаха и её тень), b/B/y (шляпа), g/G (фартук) — ключевые: make_npc_professions.py перекрашивает их в цвета профессий.
NPCS = {}


def npc(name, f0, f1, pal):
    sheet = strip([art(name, f0, pal), art(name + ' f2', f1, pal)])
    margin_report(name, sheet)
    NPCS[name] = sheet


VILLAGER = {'a': '14110c', 'b': 'd9b84a', 'B': 'c9a24a', 'y': 'f0d878', 'd': 'f0c8a0', 'D': 'd4a47c', 'e': '1a1a2a', 'f': '4a8a4a', 'F': '386c3c',
            'g': 'd9d2b8', 'G': 'b4ac90', 'h': '4a3320'}
VHEAD = [
    '................',
    '.....aaaaaa.....',
    '....aybbbbBa....',
    '...aBbbbbbbBa...',
    '....aDDDDDDa....',
    '....adddddDa....',
    '....addedeDa....',
    '.....adddDa.....']
npc('villager', VHEAD + [
    '...aafffffFa....',
    '..addfffffFaa...',
    '..addfggggFdDa..',
    '..adDfgggGFdDa..',
    '...aafgGGGFdDa..',
    '....ahhaahhaa...',
    '....ahha.aa.....',
    '.....aa.........'], VHEAD + [
    '....afffffFaa...',
    '...aafffffFdDa..',
    '..addfggggFdDa..',
    '..adDfgggGFdDa..',
    '..adDfgGGGFaa...',
    '...aahhaahha....',
    '.....aa.ahha....',
    '.........aa.....'], VILLAGER)

TRADER = {'a': '140a1e', 'b': '5a3a7a', 'y': '7c58a4', 'B': '46286a', 'c': '4a2a6a', 'd': 'e8c8a0', 'D': 'd0a880', 'e': '1a1a2a', 'f': '6a3a8a',
          'g': 'c9a24a', 'G': 'a8843a', 'h': '8a5aaa', 'H': '6e4490', 'i': 'ffd86b', 'j': '3a2a4a'}
THEAD = [   # шляпа стала на строку ниже: раньше её верх упирался в край кадра
    '................',
    '....aaaaaaaa....',
    '..aaybbbbbBBaa..',
    '.accbbbbbbbbcca.',
    '..aaaDDDDDDaaa..',
    '....addedeDa....',
    '....adddddDa....',
    '...affdddDffa...']
npc('trader', THEAD + [
    '..agghhhhhHfa...',
    '..agghhhhhHdDa..',
    '..aGghhhhhHdDa..',
    '..aGghhhihHdDa..',
    '..aGghhhhHHfa...',
    '..aGGfffffffa...',
    '...aajjaaaaa....',
    '.....aa.ajja....'], THEAD + [
    '..agghhhhhHfa...',
    '..agghhhhhHfa...',
    '..aGghhhhhHdDa..',
    '..aGghhhihHdDa..',
    '..aGghhhhHHdDa..',
    '..aGGfffffffa...',
    '...aaaaaajja....',
    '....ajja.aa.....'], TRADER)

TRAVELER = {'a': '101418', 'b': '4a505c', 'y': '6c7486', 'B': '3a404c', 'c': 'e8c8a0', 'C': 'cfa884', 'd': '1a1a2a', 'e': 'c9a24a', 'E': 'a8843a',
            'f': '6a707c', 'g': '8a909c', 'G': '6c7280', 'h': '3a3f4a'}
RHEAD = [
    '................',
    '....aaaaaaaa....',
    '...ayybbbbbBa...',
    '...aybbbbbbBa...',
    '...abbbbbbBBa...',
    '....aCcdcdCaaa..',
    '....acccccCaeEa.',
    '...affcccCffeEa.',
    '...afgggggGfeEa.']
npc('traveler', RHEAD + [
    '..accgggggGfeEa.',
    '..acCgggggGceEa.',
    '..acCggggGGcCa..',
    '...afgggGGGcCa..',
    '...affffffffa...',
    '....ahhaaaaa....',
    '.....aa.ahha....'], RHEAD + [
    '...afgggggGceEa.',
    '..acCgggggGceEa.',
    '..acCggggGGcCa..',
    '..acCgggGGGfa...',
    '...affffffffa...',
    '....aaaaahha....',
    '....ahha.aa.....'], TRAVELER)

if __name__ == '__main__':
    for name, sheet in SHEETS.items(): save('Enemies', name + '.png', sheet)
    # жители, торговец, путник и профессии теперь рисует make_characters.py (8 кадров); NPCS здесь — прежний 2-кадровый вариант, не сохраняется
    print('art pass: enemy sheets written:', len(SHEETS), 'npc sheets:', len(NPCS))
