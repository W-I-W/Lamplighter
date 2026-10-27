"""Generates the PNG primitives used by world effects (instead of code-drawn shapes) -> Assets/Sprites/Vfx/*.png.
Run from the project root: python tools/GraphicsGenerator/make_vfx_prims.py
All images are white/grey masks: the game tints them (modulate), so replacing a PNG with your own art restyles every effect that uses it."""
import os, zlib, struct, math

OUT = 'Assets/Sprites/Vfx'
W = (255, 255, 255, 255); N = (0, 0, 0, 0)

def png(name, w, h, px):
    raw = b''.join(b'\x00' + b''.join(bytes(px[y][x]) for x in range(w)) for y in range(h))
    def ch(t, d): c = struct.pack('>I', len(d)) + t + d; return c + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    data = b'\x89PNG\r\n\x1a\n' + ch(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0)) + ch(b'IDAT', zlib.compress(raw, 9)) + ch(b'IEND', b'')
    os.makedirs(OUT, exist_ok=True); open(os.path.join(OUT, name), 'wb').write(data)

def blank(w, h): return [[N for _ in range(w)] for _ in range(h)]

# px: solid square (rectangles, particles, bar fills)
png('px.png', 4, 4, [[W] * 4 for _ in range(4)])
# line: segment body (beams, lines, arcs are chains of these)
png('line.png', 4, 2, [[W] * 4 for _ in range(2)])
# disc: hard-edged filled circle (projectiles, markers, shadows, area fills)
n = 32; d = blank(n, n)
for y in range(n):
    for x in range(n):
        if (x + .5 - n / 2) ** 2 + (y + .5 - n / 2) ** 2 <= (n / 2) ** 2: d[y][x] = W
png('disc.png', n, n, d)
# arrow: small triangle pointing right (edge markers, direction hints)
a = blank(8, 8)
for y in range(8):
    for x in range(8):
        if x <= 7 - abs(y - 3.5) * 2 + .5 and x >= 0: a[y][x] = W
png('arrow.png', 8, 8, a)
# crack_0..3: dark crack overlays for the block being mined (16x16, drawn over the tile; more cracks = more progress)
CRACK = (12, 10, 14, 200)
def crack(level):
    g = blank(16, 16)
    segs = [[(8, 8), (7, 6), (7, 4), (6, 2), (6, 0)],
            [(8, 8), (10, 9), (12, 9), (13, 11), (15, 12)],
            [(8, 8), (6, 10), (5, 12), (3, 13), (2, 15)],
            [(8, 8), (5, 8), (3, 7), (1, 7), (0, 5)],
            [(7, 4), (9, 3), (11, 3), (12, 1)],
            [(12, 9), (12, 7), (14, 6), (15, 4)]]
    for s in segs[:2 + level * 2 if level < 3 else len(segs)]:
        for (x0, y0), (x1, y1) in zip(s, s[1:]):
            steps = max(abs(x1 - x0), abs(y1 - y0)) or 1
            for i in range(steps + 1):
                x = round(x0 + (x1 - x0) * i / steps); y = round(y0 + (y1 - y0) * i / steps)
                if 0 <= x < 16 and 0 <= y < 16: g[y][x] = CRACK
    return g
for i in range(4): png(f'crack_{i}.png', 16, 16, crack(i))
# bar_back / bar_fill: 3x3 9-patch-like strips for progress bars in the world (back is tinted dark, fill by colour)
png('bar_back.png', 4, 4, [[W] * 4 for _ in range(4)])
# diamond / diamond_line: 9x9 filled and outlined diamonds (HUD markers, relic gems, map boss marks)
df = blank(9, 9); dl = blank(9, 9)
for y in range(9):
    for x in range(9):
        dd = abs(x - 4) + abs(y - 4)
        if dd <= 4: df[y][x] = W
        if dd == 4: dl[y][x] = W
png('diamond.png', 9, 9, df); png('diamond_line.png', 9, 9, dl)
print('vfx primitives written to', OUT)
