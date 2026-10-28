"""Курсор мыши: крупная пиксельная стрелка с тёмной обводкой и тёплым светящимся краем. Рисуется на сетке 12x18 и увеличивается в 2 раза (24x36)."""
from PIL import Image

ART = [
    "X...........",
    "XX..........",
    "XOX.........",
    "XOOX........",
    "XOOOX.......",
    "XOOOOX......",
    "XOOOOOX.....",
    "XOOOOOOX....",
    "XOOOOOOOX...",
    "XOOOOOOOOX..",
    "XOOOOOXXXXX.",
    "XOOXOOX.....",
    "XOX.XOOX....",
    "XX..XOOX....",
    "X....XOOX...",
    ".....XOOX...",
    "......XX....",
]
OUT = (14, 12, 24, 255)
FILL = (255, 236, 150, 255)
SHINE = (255, 255, 235, 255)
GLOW = (255, 190, 70, 140)
S = 2
W, H = 12, len(ART)
pad = 1
img = Image.new("RGBA", ((W + pad * 2) * S, (H + pad * 2) * S), (0, 0, 0, 0))
px = img.load()

def put(x, y, c):
    for dy in range(S):
        for dx in range(S):
            px[(x + pad) * S + dx, (y + pad) * S + dy] = c

cells = {(x, y): ch for y, row in enumerate(ART) for x, ch in enumerate(row) if ch != "."}
for (x, y) in cells:                                    # мягкое свечение вокруг
    for ox in (-1, 0, 1):
        for oy in (-1, 0, 1):
            nx, ny = x + ox, y + oy
            if (nx, ny) not in cells and -pad <= nx < W + pad and -pad <= ny < H + pad:
                put(nx, ny, GLOW)
for (x, y), ch in cells.items():
    c = OUT if ch == "X" else FILL
    if ch == "O" and (x == 1 or y == 2 and x == 2): c = SHINE   # блик вдоль левого края
    put(x, y, c)
img.save("Assets/UI/cursor.png")
print(img.size, "hotspot", pad * S, pad * S)
