"""Seamless smooth noise texture for the darkness shader -> Assets/Shaders/Lighting/noise.png (256x256, grey).
The shader samples it with linear filtering, so shadow edges are smooth on any GPU (no hash/sin tricks). 16 noise cells span the texture.
Run from the project root: python tools/GraphicsGenerator/make_noise.py   (replace the PNG with your own seamless noise to restyle the fog)"""
import numpy as np
from PIL import Image

rng = np.random.default_rng(20240607)
CELLS, SIZE = 16, 256
grid = rng.random((CELLS, CELLS)).astype(np.float32)
# bicubic upsampling of a 3x3 tiling, then crop the centre tile: the result wraps seamlessly
tiled = np.tile(grid, (3, 3))
big = Image.fromarray((tiled * 255).astype(np.uint8)).resize((CELLS * 3 * (SIZE // CELLS),) * 2, Image.BICUBIC)
c = SIZE
out = np.asarray(big)[c:c + SIZE, c:c + SIZE].astype(np.float32)
out = (out - out.min()) / (out.max() - out.min())
Image.fromarray((out * 255).astype(np.uint8), 'L').save('Assets/Shaders/Lighting/noise.png')
print('noise.png written')
