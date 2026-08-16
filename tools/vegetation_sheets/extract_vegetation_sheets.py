"""Cut the new vegetation sheets into individual sprites.

Both source sheets already carry correct per-sprite alpha, so there is no
colour keying to do and therefore no risk of halos or a baked background. The
work is purely to find each sprite, crop it tightly and write it out with its
alpha intact.

Two details matter for quality:

Foliage is not one connected blob. Leaf clusters and grass blades break into
dozens of specks at the edges, so components are found on a slightly closed
mask; the crop then uses the original alpha, not the closed mask, so nothing is
dilated in the output.

The transparent pixels in the source are painted green. Left alone that green
bleeds into the sprite edge as soon as the renderer filters or mipmaps it, which
is exactly the coloured halo we are trying to avoid. Every fully transparent
pixel therefore has its colour replaced by the nearest opaque pixel's colour
before writing, which is invisible at alpha zero and correct once filtered.

No source sheet is modified.
"""

from __future__ import annotations

import json
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[2]

# Alpha at or above this counts as sprite. The sheets fade to zero cleanly, so
# a low threshold keeps soft leaf tips without picking up background noise.
ALPHA_FLOOR = 16

# Radius used only to knit a sprite's own edge specks together. It is kept
# small on purpose: a large closing bridges the gap between two neighbouring
# trees on the sheet and yields one crop containing several of them.
CLOSE_RADIUS = 2

# A component this large is treated as a sprite in its own right and becomes a
# seed. Everything smaller is a detached leaf cluster or grass tip and is given
# to whichever seed it is nearest, which reattaches foliage without ever
# bridging two separate sprites.
SEED_AREA = 2600

# Detached pieces further than this from any seed are discarded as strays.
MAX_ADOPT_DISTANCE = 34

MIN_AREA = 1400
MIN_SIDE = 24


def disc(radius: int) -> np.ndarray:
    span = np.arange(-radius, radius + 1)
    y, x = np.meshgrid(span, span, indexing="ij")
    return (x * x + y * y) <= radius * radius


def bleed_colour(rgba: np.ndarray) -> np.ndarray:
    """Replace colour under transparent pixels with the nearest opaque colour."""
    opaque = rgba[:, :, 3] > 0
    if not opaque.any():
        return rgba
    # Nearest opaque neighbour for every pixel.
    _, (iy, ix) = ndimage.distance_transform_edt(~opaque, return_indices=True)
    out = rgba.copy()
    out[:, :, :3] = rgba[iy, ix, :3]
    out[:, :, 3] = rgba[:, :, 3]
    return out


def find_row_gap(local: np.ndarray) -> int | None:
    """Row index of a clean horizontal gap separating two stacked sprites."""
    rows = local.sum(axis=1)
    height = len(rows)
    if height < 160:
        return None
    margin = max(40, height // 6)
    window = rows[margin:height - margin]
    if len(window) == 0:
        return None
    # A near-empty row counts: two stacked sprites often share a few stray
    # leaf pixels rather than a mathematically clean gap.
    empty = np.where(window <= max(1, int(rows.max() * .02)))[0]
    if len(empty) == 0:
        return None
    # Only trust a gap with real content on both sides.
    split = int(empty[len(empty) // 2]) + margin
    if rows[:split].sum() < MIN_AREA or rows[split:].sum() < MIN_AREA:
        return None
    return split


def crop(source: np.ndarray, keep: np.ndarray, x0: int, y0: int, x1: int, y1: int) -> dict:
    tile = source[y0:y1, x0:x1].copy()
    local = keep[y0:y1, x0:x1]
    tile[:, :, 3] = np.where(local, tile[:, :, 3], 0)
    tile = bleed_colour(tile)
    return {"index": 0, "x": x0, "y": y0, "w": x1 - x0, "h": y1 - y0,
            "area": int(local.sum()), "image": Image.fromarray(tile, "RGBA")}


def extract(sheet_name: str, out_dir: Path) -> list[dict]:
    source = np.asarray(Image.open(ROOT / f"assets/art/sheets/{sheet_name}").convert("RGBA"))
    alpha = source[:, :, 3]
    mask = alpha >= ALPHA_FLOOR

    closed = ndimage.binary_closing(mask, structure=disc(CLOSE_RADIUS))
    raw, raw_count = ndimage.label(closed, structure=np.ones((3, 3), bool))

    # Seeds are the substantial components; everything else gets adopted.
    sizes = ndimage.sum_labels(closed, raw, index=np.arange(1, raw_count + 1))
    seed_ids = {int(i + 1) for i, size in enumerate(sizes) if size >= SEED_AREA}
    seeds = np.isin(raw, list(seed_ids)) if seed_ids else np.zeros_like(closed)

    distance, (iy, ix) = ndimage.distance_transform_edt(~seeds, return_indices=True)
    labels = np.where(seeds, raw, 0)
    orphan = closed & ~seeds & (distance <= MAX_ADOPT_DISTANCE)
    labels[orphan] = raw[iy[orphan], ix[orphan]]
    count = raw_count

    out_dir.mkdir(parents=True, exist_ok=True)
    records: list[dict] = []
    for index in range(1, count + 1):
        component = labels == index
        # Crop from the true alpha inside this component, never the closed mask.
        keep = component & mask
        area = int(keep.sum())
        if area < MIN_AREA:
            continue
        ys, xs = np.where(keep)
        y0, y1 = int(ys.min()), int(ys.max()) + 1
        x0, x1 = int(xs.min()), int(xs.max()) + 1
        if (x1 - x0) < MIN_SIDE or (y1 - y0) < MIN_SIDE:
            continue

        # Two sprites stacked with a clear horizontal gap between them still
        # arrive as one component when their bounding boxes touch. Splitting on
        # an empty row keeps them as the separate assets they are.
        split = find_row_gap(keep[y0:y1, x0:x1])
        if split is not None:
            for lo, hi in ((0, split), (split, y1 - y0)):
                sub = keep[y0 + lo:y0 + hi, x0:x1]
                if sub.sum() < MIN_AREA:
                    continue
                sy, sx = np.where(sub)
                records.append(crop(source, keep,
                                    x0 + int(sx.min()), y0 + lo + int(sy.min()),
                                    x0 + int(sx.max()) + 1, y0 + lo + int(sy.max()) + 1))
            continue

        tile = source[y0:y1, x0:x1].copy()
        # Drop any pixels belonging to a neighbouring sprite that happens to
        # overlap this bounding box.
        local = keep[y0:y1, x0:x1]
        tile[:, :, 3] = np.where(local, tile[:, :, 3], 0)
        tile = bleed_colour(tile)

        records.append({
            "index": len(records),
            "x": x0, "y": y0, "w": x1 - x0, "h": y1 - y0,
            "area": area,
            "image": Image.fromarray(tile, "RGBA"),
        })

    # Reading order: top to bottom, then left to right, in loose rows.
    records.sort(key=lambda r: (r["y"] // 90, r["x"]))
    for position, record in enumerate(records):
        record["index"] = position
    return records


def main() -> None:
    plan = {
        "trees_sheet.png": ROOT / "assets/art/trees/_raw",
        "undergrowth_sheet.png": ROOT / "assets/art/undergrowth/_raw",
    }
    manifest: dict[str, list[dict]] = {}
    for sheet, out_dir in plan.items():
        records = extract(sheet, out_dir)
        entries = []
        for record in records:
            name = f"{record['index']:02d}.png"
            record["image"].save(out_dir / name)
            entries.append({k: record[k] for k in ("index", "x", "y", "w", "h", "area")} | {"file": name})
        manifest[sheet] = entries
        print(f"{sheet}: {len(entries)} sprites -> {out_dir.relative_to(ROOT)}")

    (ROOT / "tools/vegetation_sheets/manifest.json").write_text(
        json.dumps(manifest, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
