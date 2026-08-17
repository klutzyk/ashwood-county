"""Cut the three high-resolution tree sheets into individual sprites.

These sheets carry correct per-sprite alpha already, so there is no colour
keying and therefore no halo or matte to remove. Each holds four well separated
trees, which are taken left to right.

Nothing is resampled. The crop is the source pixels inside the sprite's own
bounding box, so the sheets' resolution advantage survives intact; the only
change is that colour under fully transparent pixels is replaced with the
nearest opaque colour, which is invisible at alpha zero but stops the sheet's
dark background bleeding into the silhouette once the renderer filters it.
"""

from __future__ import annotations

from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "assets/art/trees"

ALPHA_FLOOR = 12
CLOSE_RADIUS = 3
SEED_AREA = 9000
MAX_ADOPT_DISTANCE = 40

# Names follow what each tree actually looks like, ordered left to right.
NAMES = {
    "trees01_sheet.png": [
        "oak_grand_01",          # huge twin-trunk green oak, boulders and flowers
        "birch_slender_01",      # medium white birch, light canopy
        "fir_tall_01",           # dark spruce, rock and ferns at the base
        "maple_autumn_01",       # orange and yellow autumn maple
    ],
    "trees02_sheet.png": [
        "oak_spreading_01",      # broad low canopy, deadwood log at the base
        "fir_full_01",           # full-skirted spruce with cones
        "birch_weeping_tall_01", # tall weeping birch
        "maple_autumn_02",       # orange and red autumn maple
    ],
    "trees03_sheet.png": [
        "maple_autumn_grand_01", # large twin-trunk red maple
        "dead_hollow_01",        # bare dead trunk, two hollows, moss and fungi
        "birch_weeping_02",      # birch with lupins at the base
        "pine_scots_tall_01",    # tall Scots pine, clear lower trunk
    ],
}


def disc(radius: int) -> np.ndarray:
    span = np.arange(-radius, radius + 1)
    y, x = np.meshgrid(span, span, indexing="ij")
    return (x * x + y * y) <= radius * radius


def bleed_colour(rgba: np.ndarray) -> np.ndarray:
    opaque = rgba[:, :, 3] > 0
    if not opaque.any():
        return rgba
    _, (iy, ix) = ndimage.distance_transform_edt(~opaque, return_indices=True)
    out = rgba.copy()
    out[:, :, :3] = rgba[iy, ix, :3]
    out[:, :, 3] = rgba[:, :, 3]
    return out


def extract(sheet: str, names: list[str]) -> list[tuple[str, tuple[int, int]]]:
    source = np.asarray(Image.open(ROOT / "assets/art/sheets" / sheet).convert("RGBA"))
    mask = source[:, :, 3] >= ALPHA_FLOOR

    closed = ndimage.binary_closing(mask, structure=disc(CLOSE_RADIUS))
    raw, count = ndimage.label(closed, structure=np.ones((3, 3), bool))
    sizes = ndimage.sum_labels(closed, raw, index=np.arange(1, count + 1))
    seeds = np.isin(raw, [i + 1 for i, size in enumerate(sizes) if size >= SEED_AREA])

    # Detached leaf clusters join their nearest tree rather than becoming
    # sprites of their own or being lost.
    distance, (iy, ix) = ndimage.distance_transform_edt(~seeds, return_indices=True)
    labels = np.where(seeds, raw, 0)
    orphan = closed & ~seeds & (distance <= MAX_ADOPT_DISTANCE)
    labels[orphan] = raw[iy[orphan], ix[orphan]]

    boxes = []
    for index in range(1, count + 1):
        keep = (labels == index) & mask
        if keep.sum() < SEED_AREA:
            continue
        ys, xs = np.where(keep)
        boxes.append((int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1, keep))
    boxes.sort(key=lambda b: b[0])

    if len(boxes) != len(names):
        raise SystemExit(f"{sheet}: found {len(boxes)} trees, expected {len(names)}")

    OUT.mkdir(parents=True, exist_ok=True)
    written = []
    for (x0, y0, x1, y1, keep), name in zip(boxes, names):
        tile = source[y0:y1, x0:x1].copy()
        local = keep[y0:y1, x0:x1]
        tile[:, :, 3] = np.where(local, tile[:, :, 3], 0)
        tile = bleed_colour(tile)
        Image.fromarray(tile, "RGBA").save(OUT / f"{name}.png")
        written.append((name, (x1 - x0, y1 - y0)))
    return written


def main() -> None:
    for sheet, names in NAMES.items():
        for name, size in extract(sheet, names):
            print(f"  {sheet:18s} {name:24s} {size[0]}x{size[1]}")


if __name__ == "__main__":
    main()
