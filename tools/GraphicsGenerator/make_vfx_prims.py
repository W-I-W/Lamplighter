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
