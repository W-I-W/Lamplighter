"""Icons of tool and weapon parts -> Assets/Sprites/Items/icon_034..044_*.png (16x16, transparent).
034 plank, 035 handle, 036..040 pickaxe heads (stone, copper, iron, crystal, obsidian), 041 axe blade, 042 sword blade, 043 bow limbs, 044 staff head.
Run from the project root: python tools/GraphicsGenerator/make_part_icons.py. Replace any PNG with your own 16x16 art to restyle it."""
import os
from PIL import Image

OUT = os.path.join(os.path.dirname(__file__), '..', '..', 'Assets', 'Sprites', 'Items')
K = (26, 26, 34, 255)
WOOD = {'W': (196, 140, 80, 255), 'w': (150, 100, 52, 255), 'v': (110, 70, 36, 255)}
TIERS = [((196, 190, 178, 255), (142, 136, 126, 255), (96, 92, 86, 255)), ((255, 204, 150, 255), (216, 128, 72, 255), (146, 78, 44, 255)),
         ((244, 247, 252, 255), (178, 188, 202, 255), (108, 118, 134, 255)), ((214, 255, 255, 255), (108, 214, 238, 255), (56, 126, 190, 255)),
         ((170, 122, 224, 255), (70, 52, 100, 255), (36, 28, 54, 255))]
STEEL = ((240, 244, 250, 255), (184, 192, 204, 255), (122, 132, 148, 255))
EMBER = (255, 140, 40, 255)


def draw(rows, pal):
    im = Image.new('RGBA', (16, 16), (0, 0, 0, 0)); fill = {}
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch != '.': fill[(x, y)] = pal[ch]
    out = dict(fill)
    for (x, y) in fill:                                                  # one-pixel dark outline
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            q = (x + dx, y + dy)
            if 0 <= q[0] < 16 and 0 <= q[1] < 16 and q not in fill: out[q] = K
    for p, c in out.items(): im.putpixel(p, c)
    return im


PLANK = ['................', '................', '................', '................', '................', '.WWWWWWWWWWWWWW.', '.WwWWWWWwWWWWWW.', '.wwwwwwwwwwwwww.',
         '................', '.WWWWWWWWWWWWWW.', '.WWWWWwWWWWWWwW.', '.wwwwwwwwwwwwww.', '................', '................', '................', '................']
HANDLE = ['................', '.............Ww.', '............Wwv.', '...........Wwv..', '..........Wwv...', '.........Wwv....', '........Wwv.....', '.......Wwv......',
          '......Wwv.......', '.....Wwv........', '....Wwv.........', '...Wwv..........', '..Wwv...........', '.vvv............', '................', '................']
HEAD = ['................', '................', '................', '................', '................', '..hhhhhhhhhhhh..', '.hbbbbbbbbbbbbh.', 'bbbbbbbbbbbbbbbb',
        'bbbsssssssssssbb', 'bs.....oo.....sb', 's......oo......s', '................', '................', '................', '................', '................']
AXE = ['................', '................', '.......hhh......', '......hbbbh.....', '.....hbbbbbh....', '....hbbbbbbb....', '....bbbbbbbo....', '....bbbbbbbo....',
       '....bbbbbbbo....', '....sbbbbbbb....', '.....sbbbbbs....', '......sbbbs.....', '.......sss......', '................', '................', '................']
BLADE = ['..............hh', '.............hbs', '............hbs.', '...........hbs..', '..........hbs...', '.........hbs....', '........hbs.....', '.......hbs......',
         '......hbs.......', '.....hbs........', '....gbs.........', '...ggg..........', '..gg.g..........', '................', '................', '................']
LIMBS = ['...WWW..........', '..Wwvv..........', '..Wv............', '..Wv............', '..Wv............', '..Wv............', '..Wv............', '..ggg...........',
         '..Wv............', '..Wv............', '..Wv............', '..Wv............', '..Wv............', '..Wwvv..........', '...WWW..........', '................']
STAFF = ['................', '................', '.....pPPp.......', '....pPWPPp......', '....PPPPPp......', '....pPPPpp......', '.....pppp.......', '......gg........',
         '.....g..g.......', '......gg........', '................', '................', '................', '................', '................', '................']


def head_pal(t, rows):
    hi, body, sh = TIERS[t]; return {'h': hi, 'b': body, 's': sh, 'o': (60, 40, 24, 255)}


if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    gold = {'g': (232, 184, 74, 255)}
    files = {'034_plank': draw(PLANK, WOOD), '035_handle': draw(HANDLE, WOOD)}
    names = ['stone', 'copper', 'iron', 'crystal', 'obsidian']
    for t in range(5):
        im = draw(HEAD, head_pal(t, HEAD))
        if t == 4:
            for p in ((0, 7), (15, 7)): im.putpixel(p, EMBER)
        files[f'0{36 + t}_pickhead_{names[t]}'] = im
    files['041_axehead'] = draw(AXE, {'h': STEEL[0], 'b': STEEL[1], 's': STEEL[2], 'o': (60, 40, 24, 255)})
    files['042_blade'] = draw(BLADE, {'h': STEEL[0], 'b': STEEL[1], 's': STEEL[2], **gold})
    files['043_bowlimbs'] = draw(LIMBS, {**WOOD, **gold})
    files['044_staffhead'] = draw(STAFF, {'p': (176, 112, 255, 255), 'P': (224, 192, 255, 255), 'W': (255, 255, 255, 255), **gold})
    for name, im in files.items(): im.save(os.path.join(OUT, f'icon_{name}.png'))
    print('wrote', len(files))
