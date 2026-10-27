"""Palette of the world map -> Assets/UI/Map/map_palette.png (one 1x1 pixel per colour).
Order matters: Game.MapIdx() returns the index of a colour in this strip. Edit the PNG to restyle the map (keep the width = number of colours)."""
import os, zlib, struct

COLORS = ['#1c2420', '#2a5a4a', '#9fb8d0', '#e8faff', '#0a0612', '#3a3050', '#8a6ac0', '#ffd86b', '#ffe27a', '#d9b27c', '#1a2030', '#d58cff',
          '#2d6a30', '#3a2c26', '#17365a', '#a8743d', '#ffffff', '#8ff0ff', '#e4f4ff', '#ff9a2e', '#8a8f98', '#2a5d8a', '#c0501e', '#3a3f4d',
          '#5b5754', '#8fc4e0', '#dfe8f2', '#06030c', '#3a2a52', '#6b4f33', '#3f7a3a']


def png(path, w, h, rows):
    raw = b''.join(b'\x00' + b''.join(bytes(p) for p in r) for r in rows)
    def ch(t, d): c = struct.pack('>I', len(d)) + t + d; return c + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    open(path, 'wb').write(b'\x89PNG\r\n\x1a\n' + ch(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0)) + ch(b'IDAT', zlib.compress(raw, 9)) + ch(b'IEND', b''))


rgba = [tuple(int(c[i:i + 2], 16) for i in (1, 3, 5)) + (255,) for c in COLORS]
png('Assets/UI/Map/map_palette.png', len(rgba), 1, [rgba])
print('map palette', len(rgba), 'colours')
