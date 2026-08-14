"""Rebuild terrain-sheet-02 derivatives from the immutable source artwork.

The legacy pass used rectangular crops.  This pass locates each derivative in
the original sheet, selects its high-confidence foreground component, restores
the source antialiasing around that component, and writes back to the same
stable path.  Explicit recipes cover multi-part assets and old mislabeled
rectangles.  No source sheet is modified.
"""

from __future__ import annotations

from collections import Counter
from pathlib import Path
import shutil

import numpy as np
from PIL import Image
from scipy import ndimage


ROOT = Path(__file__).resolve().parents[2]
SOURCE_PATH = ROOT / "assets/art/terrain/terrain_asset_sheet_02.png"
SOURCE = np.asarray(Image.open(SOURCE_PATH).convert("RGBA"))
WEIGHTS = np.array([1, 256, 65536, 16777216], dtype=np.uint32)

NAMES = set("""wildflower_grass_02 mushroom_meadow_01 mixed_grass_03 sparse_ground_02 meadow_flowers_03 dry_grass_rock_01 muddy_ground_02 forest_floor_02 ploughed_rows_02 ploughed_curve_01 rocky_ground_03 stone_outcrop_ground_01 dirt_ruts_02 wet_track_01 dirt_lane_02 gravel_road_02 asphalt_straight_02 forest_track_02 dirt_straight_03 dirt_crossroads_01 dirt_junction_01 dirt_curve_01 asphalt_intersection_01 asphalt_curve_01 asphalt_edge_01 asphalt_bend_01 muddy_curve_01 rail_straight_01 rail_grass_straight_01 rail_curve_01 autumn_puddle_01 berry_puddle_01 mud_puddle_02 pond_reeds_01 pond_deep_01 pond_lilies_01 river_rapids_straight_01 river_rapids_rocks_01 river_rapids_curve_01 creek_rapids_01 marsh_pond_01 marsh_pond_02 cliff_rock_01 cliff_rock_02 cliff_rock_03 cliff_rock_04 boulder_cluster_03 rock_slab_01 deciduous_autumn_01 birch_01 pine_02 birch_young_01 deciduous_02 dead_tree_02 pine_03 young_deciduous_02 young_pine_02 dead_tree_young_01 shrub_03 shrub_yellow_01 fern_03 reeds_01 reeds_02 flowers_blue_01 flowers_red_01 barbed_fence_01 gate_01 wood_gate_02 crop_rows_green_01 crop_rows_mixed_01 corn_rows_01 corn_rows_02 wheat_patch_01 hay_bale_round_01 hay_bale_square_01 stop_sign_01 speed_sign_55_01 warning_sign_01 curve_sign_01 utility_pole_01 street_light_01 concrete_barrier_01 crate_01 road_barrier_01 barrels_01 log_pile_03 concrete_pipes_01 watchtower_01 abandoned_pickup_01 scrap_pile_01 corrugated_shed_01 ruined_shed_01 timber_stack_03""".split())

# Windows and semantic seeds for assets that the old files did not actually
# point at, or whose intended art contains several independent pieces.
EXPLICIT = {
    "assets/art/props/farm/barbed_fence_01.png": ((15, 685, 175, 845), [(55, 730), (135, 725)]),
    "assets/art/props/industrial/crate_01.png": ((825, 770, 915, 860), [(865, 815)]),
    "assets/art/props/industrial/barrels_01.png": ((1075, 790, 1160, 885), [(1100, 825), (1130, 845)]),
    "assets/art/props/industrial/concrete_pipes_01.png": ((1275, 720, 1410, 830), [(1325, 770), (1380, 790)]),
    "assets/art/props/industrial/road_barrier_01.png": ((1170, 830, 1290, 935), [(1225, 875)]),
    "assets/art/props/industrial/corrugated_shed_01.png": ((1250, 875, 1410, 1010), [(1330, 900)]),
    "assets/art/props/industrial/watchtower_01.png": ((1380, 670, 1536, 960), [(1460, 750), (1460, 880)]),
    "assets/art/props/logging/log_pile_03.png": ((1210, 700, 1380, 820), [(1295, 750)]),
    "assets/art/props/roadside/concrete_barrier_01.png": ((650, 770, 870, 885), [(740, 825)]),
    "assets/art/props/roadside/street_light_01.png": ((850, 680, 940, 850), [(910, 715)]),
}

ALIASES = {
    "assets/art/props/industrial/ruined_shed_01.png": "assets/art/props/industrial/corrugated_shed_01.png",
    "assets/art/props/industrial/scrap_pile_01.png": "assets/art/props/industrial/abandoned_pickup_01.png",
    "assets/art/props/logging/timber_stack_03.png": "assets/art/props/logging/log_pile_03.png",
    "assets/art/props/roadside/rock_slab_01.png": "assets/art/props/roadside/boulder_cluster_03.png",
    "assets/art/props/roadside/curve_sign_01.png": "assets/art/props/roadside/warning_sign_01.png",
    "assets/art/vegetation/fern_03.png": "assets/art/vegetation/fern_02.png",
    "assets/art/vegetation/reeds_02.png": "assets/art/vegetation/reeds_01.png",
    "assets/art/vegetation/shrub_03.png": "assets/art/vegetation/bush_dense_02.png",
    "assets/art/vegetation/shrub_yellow_01.png": "assets/art/vegetation/bush_dense_02.png",
}
EXPLICIT_THRESHOLDS = {"assets/art/props/industrial/corrugated_shed_01.png": 245}
EXPLICIT_CUTOUTS = {"assets/art/props/industrial/corrugated_shed_01.png": [(1350, 950, 1420, 1024)]}


def source_unique_positions() -> dict[int, tuple[int, int]]:
    packed = (SOURCE.astype(np.uint32) * WEIGHTS).sum(2)
    values, counts = np.unique(packed, return_counts=True)
    unique_values = values[counts == 1]
    locations = np.where(np.isin(packed, unique_values))
    return {int(packed[y, x]): (int(x), int(y)) for y, x in zip(*locations)}


def locate_existing(path: Path, unique: dict[int, tuple[int, int]]) -> tuple[int, int, int, int] | None:
    image = np.asarray(Image.open(path).convert("RGBA"))
    packed = (image.astype(np.uint32) * WEIGHTS).sum(2)
    offsets: list[tuple[int, int]] = []
    for y, x in zip(*np.where(image[:, :, 3] > 245)):
        location = unique.get(int(packed[y, x]))
        if location is not None:
            offsets.append((location[0] - int(x), location[1] - int(y)))
    if not offsets:
        return None
    (left, top), hits = Counter(offsets).most_common(1)[0]
    if hits < 3:
        return None
    return left, top, left + image.shape[1], top + image.shape[0]


def isolate(rect: tuple[int, int, int, int], seeds: list[tuple[int, int]] | None = None, threshold: int = 190, cutouts: list[tuple[int, int, int, int]] | None = None) -> np.ndarray:
    left, top, right, bottom = rect
    left, top = max(0, left), max(0, top)
    right, bottom = min(SOURCE.shape[1], right), min(SOURCE.shape[0], bottom)
    window = SOURCE[top:bottom, left:right].copy()
    alpha = window[:, :, 3]
    labels, count = ndimage.label(alpha >= threshold)
    selected = np.zeros(alpha.shape, dtype=bool)

    if seeds:
        for source_x, source_y in seeds:
            x, y = source_x - left, source_y - top
            label = int(labels[y, x]) if 0 <= y < labels.shape[0] and 0 <= x < labels.shape[1] else 0
            if label == 0:
                ys, xs = np.where(labels > 0)
                if len(xs):
                    nearest = int(np.argmin((xs - x) ** 2 + (ys - y) ** 2))
                    label = int(labels[ys[nearest], xs[nearest]])
            if label:
                selected |= labels == label
    else:
        center_y, center_x = (np.array(alpha.shape) - 1) / 2
        best_label, best_score = 0, -1.0
        for label in range(1, count + 1):
            ys, xs = np.where(labels == label)
            if not len(xs):
                continue
            distance = float(np.hypot(xs.mean() - center_x, ys.mean() - center_y))
            score = len(xs) / (1 + distance / max(alpha.shape))
            if score > best_score:
                best_label, best_score = label, score
        selected = labels == best_label

    selected = ndimage.binary_dilation(selected, iterations=6)
    for cut_left, cut_top, cut_right, cut_bottom in cutouts or []:
        selected[max(0,cut_top-top):max(0,cut_bottom-top),max(0,cut_left-left):max(0,cut_right-left)]=False
    window[:, :, 3] = np.where(selected, alpha, 0).astype(np.uint8)
    ys, xs = np.where(window[:, :, 3] > 0)
    if not len(xs):
        raise ValueError(f"Empty extraction for {rect}")
    crop = window[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    return np.pad(crop, ((3, 3), (3, 3), (0, 0)), mode="constant")


def save(path: Path, pixels: np.ndarray) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(pixels, "RGBA").save(path, optimize=True)


def semantic_cleanup(relative: str, pixels: np.ndarray) -> np.ndarray:
    """Remove visually reviewed neighbors that overlap the source haze."""
    output = pixels.copy()
    height, width = output.shape[:2]
    if relative.endswith("abandoned_pickup_01.png"):
        output[:min(52,height), :min(122,width), 3] = 0
        output[:min(55,height), max(0,width-62):, 3] = 0
    ys, xs = np.where(output[:, :, 3] > 0)
    return np.pad(output[ys.min():ys.max()+1, xs.min():xs.max()+1], ((3,3),(3,3),(0,0)), mode="constant")


def main() -> None:
    unique = source_unique_positions()
    explicit_paths = {ROOT / path for path in EXPLICIT}
    alias_paths = {ROOT / path for path in ALIASES}
    repaired = 0
    for path in (ROOT / "assets/art").rglob("*.png"):
        if path.stem not in NAMES or path in explicit_paths or path in alias_paths or path == SOURCE_PATH:
            continue
        rect = locate_existing(path, unique)
        if rect is None:
            continue
        relative=path.relative_to(ROOT).as_posix();save(path, semantic_cleanup(relative,isolate(rect)))
        repaired += 1

    for relative, (rect, seeds) in EXPLICIT.items():
        save(ROOT / relative, semantic_cleanup(relative,isolate(rect, seeds, EXPLICIT_THRESHOLDS.get(relative,190), EXPLICIT_CUTOUTS.get(relative))))
        repaired += 1
    for destination, source in ALIASES.items():
        shutil.copyfile(ROOT / source, ROOT / destination)
        repaired += 1
    print(f"Re-extracted {repaired} terrain-sheet-02 derivatives from the original source.")


if __name__ == "__main__":
    main()
