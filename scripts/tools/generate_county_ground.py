"""Bake Ashwood County's deterministic, seamless macro ground texture."""

from pathlib import Path
import math
import numpy as np
from PIL import Image

WIDTH_CELLS, HEIGHT_CELLS, PIXELS_PER_CELL = 384, 320, 6
OUTPUT = Path(__file__).parents[2] / "assets" / "art" / "terrain" / "county_ground.png"


def hash01(x, y, salt):
    value = (x.astype(np.uint32) * np.uint32(374761393)
             + y.astype(np.uint32) * np.uint32(668265263)
             + np.uint32(salt * 69069))
    value = (value ^ (value >> np.uint32(13))) * np.uint32(1274126177)
    value ^= value >> np.uint32(16)
    return (value & np.uint32(0x00FFFFFF)).astype(np.float32) / 16777215.0


def influence(gx, gy, center, radius):
    distance = np.sqrt(((gx - center[0]) / radius[0]) ** 2 + ((gy - center[1]) / radius[1]) ** 2)
    value = np.clip(1.0 - distance, 0.0, 1.0)
    return value * value * (3.0 - 2.0 * value)


def value_noise(gx, gy, salt=223):
    ix, iy = np.floor(gx).astype(np.int32), np.floor(gy).astype(np.int32)
    fx, fy = gx - ix, gy - iy
    fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
    a, b = hash01(ix, iy, salt), hash01(ix + 1, iy, salt)
    c, d = hash01(ix, iy + 1, salt), hash01(ix + 1, iy + 1, salt)
    return (a + (b - a) * fx) + ((c + (d - c) * fx) - (a + (b - a) * fx)) * fy


def main():
    width, height = WIDTH_CELLS * PIXELS_PER_CELL, HEIGHT_CELLS * PIXELS_PER_CELL
    py, px = np.mgrid[0:height, 0:width]
    gx, gy = (px + .5) / PIXELS_PER_CELL, (py + .5) / PIXELS_PER_CELL
    terrain = np.empty((height, width, 3), dtype=np.float32)
    terrain[:] = np.array([0x49, 0x61, 0x3B], dtype=np.float32) / 255.0

    regions = [
        ((145, 54), (170, 76), "304735", .83), ((72, 37), (69, 39), "23382c", .82),
        ((105, 74), (31, 24), "604f35", .72), ((197, 157), (51, 43), "607848", .78),
        ((170, 204), (62, 51), "737747", .82), ((154, 250), (53, 48), "304e3b", .88),
        ((164, 268), (100, 66), "777849", .72), ((252, 145), (57, 48), "565a50", .84),
        ((279, 211), (32, 25), "5f5b4d", .73), ((246, 234), (41, 30), "78764a", .68),
        ((322, 193), (77, 105), "46593b", .58),
    ]
    for center, radius, color_hex, strength in regions:
        color = np.array([int(color_hex[i:i + 2], 16) for i in (0, 2, 4)], dtype=np.float32) / 255.0
        amount = influence(gx, gy, center, radius)[:, :, None] * strength
        terrain = terrain * (1 - amount) + color * amount

    variation = ((value_noise(gx * .105, gy * .105) - .5) * .16
                 + (value_noise(gx * .34 + 19.7, gy * .34 + 8.3) - .5) * .10
                 + (value_noise(gx * 2.2 + 4.1, gy * 2.2 + 31.8) - .5) * .07)
    terrain *= (1 + variation[:, :, None])

    fleck = hash01(np.floor(gx * 5).astype(np.int32), np.floor(gy * 5).astype(np.int32), 211)
    bright = fleck > .965
    terrain[bright] = terrain[bright] * .62 + terrain[bright] * np.array([1.18, 1.13, .86]) * .38
    terrain[fleck < .025] *= .82

    rgba = np.empty((height, width, 4), dtype=np.uint8)
    rgba[:, :, :3] = np.clip(terrain * 255, 0, 255).astype(np.uint8)
    rgba[:, :, 3] = 255
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(rgba, "RGBA").save(OUTPUT, optimize=True)
    print(f"Wrote {OUTPUT} ({width}x{height})")


if __name__ == "__main__":
    main()
