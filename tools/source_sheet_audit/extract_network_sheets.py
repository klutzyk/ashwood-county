"""Deterministically extract the 2026-08 road-network source sheets.

The five source PNGs are immutable.  Each output is selected from one named
alpha component inside an explicit window, then padded with transparent pixels.
This prevents neighbouring art from leaking into a derivative even when two
objects have overlapping rectangular bounds.
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage


ROOT = Path(__file__).resolve().parents[2]
SHEETS = ROOT / "assets/art/sheets"


@dataclass(frozen=True)
class Item:
    source: str
    output: str
    seed: tuple[int, int]
    window: tuple[int, int, int, int]


def item(source: str, output: str, seed: tuple[int, int], window: tuple[int, int, int, int]) -> Item:
    return Item(source, output, seed, window)


ITEMS = [
    # Asphalt sheet: road-layout references plus independent roadside props.
    item("asphalt_sheet.png", "assets/art/roads/asphalt/reference/highway_straight.png", (250, 170), (0, 0, 478, 348)),
    item("asphalt_sheet.png", "assets/art/roads/asphalt/reference/town_straight.png", (650, 170), (475, 0, 835, 320)),
    item("asphalt_sheet.png", "assets/art/roads/asphalt/reference/crossroad.png", (1030, 180), (830, 0, 1230, 320)),
    item("asphalt_sheet.png", "assets/art/roads/asphalt/reference/quarter_curve.png", (1380, 180), (1235, 0, 1536, 320)),
    item("asphalt_sheet.png", "assets/art/roads/asphalt/reference/t_junction.png", (220, 510), (0, 345, 440, 680)),
    item("asphalt_sheet.png", "assets/art/roads/asphalt/reference/crosswalk.png", (625, 510), (400, 345, 845, 680)),
    item("asphalt_sheet.png", "assets/art/roads/asphalt/reference/parking_bay.png", (1020, 520), (820, 375, 1225, 680)),
    item("asphalt_sheet.png", "assets/art/roads/asphalt/reference/culdesac.png", (1360, 525), (1175, 390, 1536, 680)),
    item("asphalt_sheet.png", "assets/art/props/roadside/guardrail_clean.png", (230, 850), (20, 720, 440, 1024)),
    item("asphalt_sheet.png", "assets/art/props/roadside/traffic_cone_large.png", (605, 830), (535, 735, 670, 910)),
    item("asphalt_sheet.png", "assets/art/props/roadside/traffic_cone_small.png", (725, 850), (655, 770, 790, 930)),
    item("asphalt_sheet.png", "assets/art/props/roadside/traffic_cone_fallen.png", (560, 920), (490, 855, 630, 985)),
    item("asphalt_sheet.png", "assets/art/props/urban/street_light_clean.png", (955, 850), (835, 665, 1090, 1024)),
    item("asphalt_sheet.png", "assets/art/props/roadside/manhole_clean.png", (1180, 850), (1080, 770, 1300, 955)),
    item("asphalt_sheet.png", "assets/art/props/roadside/storm_drain_clean.png", (1390, 870), (1275, 775, 1536, 985)),

    # Dirt-road sheet: all twelve intentionally separated topology/style samples.
    item("dirt_road_sheet.png", "assets/art/roads/dirt/reference/dirt_straight.png", (230, 155), (0, 0, 435, 305)),
    item("dirt_road_sheet.png", "assets/art/roads/dirt/reference/farm_track_straight.png", (625, 165), (430, 0, 840, 315)),
    item("dirt_road_sheet.png", "assets/art/roads/dirt/reference/logging_road_straight.png", (1010, 165), (785, 0, 1220, 320)),
    item("dirt_road_sheet.png", "assets/art/roads/dirt/reference/dirt_quarter_curve.png", (1390, 170), (1215, 0, 1536, 320)),
    item("dirt_road_sheet.png", "assets/art/roads/dirt/reference/dirt_s_curve.png", (205, 475), (0, 295, 390, 655)),
    item("dirt_road_sheet.png", "assets/art/roads/dirt/reference/dirt_t_junction.png", (560, 455), (355, 305, 770, 610)),
    item("dirt_road_sheet.png", "assets/art/roads/dirt/reference/dirt_crossroad.png", (980, 475), (765, 330, 1180, 615)),
    item("dirt_road_sheet.png", "assets/art/roads/dirt/reference/dirt_y_junction.png", (1360, 475), (1145, 330, 1536, 615)),
    item("dirt_road_sheet.png", "assets/art/roads/dirt/reference/dirt_turnaround.png", (205, 810), (0, 635, 410, 990)),
    item("dirt_road_sheet.png", "assets/art/roads/dirt/reference/footpath_winding.png", (590, 805), (425, 600, 745, 1000)),
    item("dirt_road_sheet.png", "assets/art/roads/dirt/reference/muddy_logging_road.png", (980, 810), (765, 615, 1185, 990)),
    item("dirt_road_sheet.png", "assets/art/roads/dirt/reference/two_track_road.png", (1360, 810), (1155, 625, 1536, 990)),

    # Waterside sheet: isolated surfaces/banks and props. The vertically touching
    # waterfall column is deliberately omitted; it is not safely separable.
    item("waterside_sheet.png", "assets/art/water/surfaces/calm_water_tile.png", (180, 130), (0, 0, 345, 255)),
    item("waterside_sheet.png", "assets/art/water/surfaces/rough_water_tile.png", (490, 130), (345, 0, 635, 255)),
    item("waterside_sheet.png", "assets/art/water/surfaces/pond_water_tile.png", (780, 130), (635, 0, 925, 250)),
    item("waterside_sheet.png", "assets/art/water/effects/foam_patch.png", (1015, 135), (920, 35, 1120, 225)),
    item("waterside_sheet.png", "assets/art/water/effects/ripple_patch.png", (1215, 135), (1115, 30, 1325, 230)),
    item("waterside_sheet.png", "assets/art/water/effects/rock_outflow.png", (1420, 130), (1315, 0, 1536, 250)),
    item("waterside_sheet.png", "assets/art/water/banks/bank_straight.png", (170, 365), (0, 240, 325, 480)),
    item("waterside_sheet.png", "assets/art/water/banks/bank_rocky.png", (445, 365), (300, 235, 580, 485)),
    item("waterside_sheet.png", "assets/art/water/banks/bank_inlet.png", (710, 365), (565, 240, 855, 490)),
    item("waterside_sheet.png", "assets/art/water/banks/bank_curve.png", (930, 365), (815, 235, 1045, 500)),
    item("waterside_sheet.png", "assets/art/water/banks/bank_cove.png", (1160, 365), (1045, 245, 1270, 485)),
    item("waterside_sheet.png", "assets/art/water/props/reeds_tall.png", (115, 885), (0, 755, 295, 1024)),
    item("waterside_sheet.png", "assets/art/water/props/reeds_short.png", (370, 890), (285, 795, 455, 980)),
    item("waterside_sheet.png", "assets/art/water/props/lily_pads.png", (555, 890), (425, 780, 705, 1000)),
    item("waterside_sheet.png", "assets/art/water/props/shore_rocks.png", (710, 900), (615, 805, 815, 1010)),

    # Railway sheet.
    item("railway_sheet.png", "assets/art/rail/reference/rail_straight.png", (190, 180), (0, 0, 385, 360)),
    item("railway_sheet.png", "assets/art/rail/reference/rail_curve.png", (530, 180), (345, 0, 715, 360)),
    item("railway_sheet.png", "assets/art/rail/reference/rail_switch.png", (820, 190), (655, 0, 985, 370)),
    item("railway_sheet.png", "assets/art/rail/reference/rail_t_junction.png", (1090, 220), (970, 65, 1230, 360)),
    item("railway_sheet.png", "assets/art/rail/reference/rail_crossing.png", (1390, 185), (1225, 35, 1536, 335)),
    item("railway_sheet.png", "assets/art/rail/reference/road_rail_crossing.png", (170, 520), (0, 360, 355, 670)),
    item("railway_sheet.png", "assets/art/rail/reference/overgrown_rail.png", (490, 525), (325, 375, 655, 685)),
    item("railway_sheet.png", "assets/art/rail/reference/broken_rail.png", (800, 525), (620, 375, 970, 690)),
    item("railway_sheet.png", "assets/art/rail/reference/rail_buffer.png", (1070, 540), (915, 410, 1225, 685)),
    item("railway_sheet.png", "assets/art/rail/bridges/steel_rail_bridge.png", (1390, 520), (1210, 335, 1536, 680)),
    item("railway_sheet.png", "assets/art/rail/props/ballast_pile.png", (110, 855), (0, 710, 225, 1000)),
    item("railway_sheet.png", "assets/art/rail/props/sleeper_stack.png", (350, 855), (215, 730, 490, 985)),
    item("railway_sheet.png", "assets/art/rail/props/rail_signal.png", (555, 850), (470, 655, 640, 1024)),
    item("railway_sheet.png", "assets/art/rail/props/crossing_signal.png", (805, 850), (680, 675, 930, 1024)),
    item("railway_sheet.png", "assets/art/rail/props/electrical_box.png", (1020, 880), (905, 785, 1130, 1024)),
    item("railway_sheet.png", "assets/art/rail/props/utility_pole.png", (1180, 835), (1115, 660, 1250, 1024)),
    item("railway_sheet.png", "assets/art/rail/props/chainlink_fence.png", (1400, 850), (1250, 695, 1536, 1024)),

    # Bridge sheet: all ten large isolated bridge components.
    item("bridge_sheet.png", "assets/art/bridges/highway_concrete_wide.png", (285, 200), (0, 0, 580, 390)),
    item("bridge_sheet.png", "assets/art/bridges/county_concrete.png", (795, 190), (570, 35, 1020, 340)),
    item("bridge_sheet.png", "assets/art/bridges/steel_truss_green.png", (1290, 190), (1035, 0, 1536, 385)),
    item("bridge_sheet.png", "assets/art/bridges/steel_truss_red.png", (280, 510), (0, 335, 555, 705)),
    item("bridge_sheet.png", "assets/art/bridges/stone_arch.png", (805, 510), (535, 335, 1080, 660)),
    item("bridge_sheet.png", "assets/art/bridges/timber_road.png", (1290, 510), (1040, 335, 1536, 680)),
    item("bridge_sheet.png", "assets/art/bridges/highway_damaged.png", (235, 820), (0, 645, 460, 995)),
    item("bridge_sheet.png", "assets/art/bridges/steel_truss_destroyed.png", (650, 830), (435, 650, 850, 1005)),
    item("bridge_sheet.png", "assets/art/bridges/culvert_road.png", (1015, 835), (835, 675, 1205, 990)),
    item("bridge_sheet.png", "assets/art/bridges/timber_footbridge.png", (1360, 845), (1155, 645, 1536, 1024)),
]


def extract(entry: Item) -> None:
    source = np.asarray(Image.open(SHEETS / entry.source).convert("RGBA"))
    x0, y0, x1, y1 = entry.window
    window = source[y0:y1, x0:x1].copy()
    labels, _ = ndimage.label(window[:, :, 3] > 4)
    sx, sy = entry.seed[0] - x0, entry.seed[1] - y0
    label_id = int(labels[sy, sx]) if 0 <= sy < labels.shape[0] and 0 <= sx < labels.shape[1] else 0
    if label_id == 0:
        ys, xs = np.where(labels > 0)
        nearest = np.argmin((xs - sx) ** 2 + (ys - sy) ** 2)
        label_id = int(labels[ys[nearest], xs[nearest]])
    selected = labels == label_id
    output = window.copy()
    output[:, :, 3] = np.where(selected, window[:, :, 3], 0).astype(np.uint8)
    ys, xs = np.where(output[:, :, 3] > 0)
    if not len(xs):
        raise RuntimeError(f"No alpha component retained for {entry.output}")
    crop = output[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    crop = np.pad(crop, ((3, 3), (3, 3), (0, 0)), mode="constant")
    destination = ROOT / entry.output
    destination.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(crop, "RGBA").save(destination, optimize=True)


def material_sample(source_name: str, output_name: str, box: tuple[int, int, int, int], kind: str) -> None:
    """Create a small opaque surface sample used by along-spline UVs.

    Samples are deliberately taken from the unmarked centre of a clean straight
    reference. Markings and shoulders are generated as separate spline bands.
    """
    image = Image.open(SHEETS / source_name).convert("RGBA").crop(box)
    rgba = np.asarray(image).copy()
    rgb = rgba[:, :, :3].astype(np.float32)
    high = rgb.max(axis=2)
    low = rgb.min(axis=2)
    saturation = (high - low) / np.maximum(1, high)
    if kind.startswith("asphalt"):
        valid = (rgba[:, :, 3] > 180) & (high < 92) & (saturation < .12)
    else:
        valid = (rgba[:, :, 3] > 180) & (rgb[:, :, 0] > rgb[:, :, 1] + 14) & (rgb[:, :, 1] > rgb[:, :, 2] + 10) & (rgb[:, :, 0] < 205)
    # Nearest valid source texel removes painted lines, transparent verge and
    # vegetation without inventing a second visual source.
    _, indices = ndimage.distance_transform_edt(~valid, return_indices=True)
    rgba[:, :, :3] = rgba[indices[0], indices[1], :3]
    # Preserve the source grain. Only cross-fade a narrow border so repeated UVs
    # meet cleanly; the previous whole-image mirror average caused the soft,
    # airbrushed dirt seen in the Studio.
    rgb = rgba[:, :, :3].astype(np.float32)
    edge = max(4, min(rgb.shape[0], rgb.shape[1]) // 10)
    for i in range(edge):
        t = (1-i / max(1, edge - 1)) * .5
        top, bottom = rgb[i].copy(), rgb[-1-i].copy()
        rgb[i] = top * (1-t) + bottom * t
        rgb[-1-i] = bottom * (1-t) + top * t
        left, right = rgb[:, i].copy(), rgb[:, -1-i].copy()
        rgb[:, i] = left * (1-t) + right * t
        rgb[:, -1-i] = right * (1-t) + left * t
    rgba[:, :, :3] = np.clip(rgb,0,255).astype(np.uint8)
    rgba[:, :, 3] = 255
    destination = ROOT / output_name
    destination.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(rgba, "RGBA").resize((256, 256), Image.Resampling.LANCZOS).save(destination, optimize=True)


def aligned_road_sample(source_name: str, output_name: str,
                        box: tuple[int, int, int, int], angle: float,
                        width: int, kind: str, include_verge: bool = False) -> None:
    """Rotate a supplied straight road so its long axis matches spline UV V.

    The source sheets present straight roads diagonally. Feeding that diagonal
    square directly into a spline made straight authored roads look slanted and
    produced rectangular seams on bends. This keeps the source pixels but first
    normalizes their direction, then crops a continuous axial strip.
    """
    source = Image.open(SHEETS / source_name).convert("RGBA").crop(box)
    rotated = source.rotate(angle,Image.Resampling.BICUBIC,expand=True)
    cx, cy = rotated.width // 2, rotated.height // 2
    height = min(300,rotated.height-8)
    sample = rotated.crop((cx-width//2,cy-height//2,cx+width//2,cy+height//2))
    rgba = np.asarray(sample).copy()
    rgb = rgba[:, :, :3].astype(np.float32)
    if not include_verge:
        high, low = rgb.max(axis=2), rgb.min(axis=2)
        valid = ((rgba[:, :, 3] > 180) &
                 (rgb[:, :, 0] > rgb[:, :, 1] + 10) &
                 (rgb[:, :, 1] > rgb[:, :, 2] + 7) &
                 (rgb[:, :, 0] < 215))
        _, indices = ndimage.distance_transform_edt(~valid,return_indices=True)
        rgba[:, :, :3] = rgba[indices[0],indices[1],:3]
    else:
        valid = rgba[:, :, 3] > 180
        _, indices = ndimage.distance_transform_edt(~valid,return_indices=True)
        rgba[:, :, :3] = rgba[indices[0],indices[1],:3]
    # Build a palindromic V tile. Its first/last rows and midpoint meet exactly,
    # eliminating the rectangular repeat bands without averaging away detail or
    # mirroring across U (which would cross the tire tracks).
    rgba[:, :, 3]=255
    destination=ROOT/output_name;destination.parent.mkdir(parents=True,exist_ok=True)
    half=np.asarray(Image.fromarray(rgba,"RGBA").resize((256,128),Image.Resampling.LANCZOS))
    tile=np.concatenate([half,half[::-1]],axis=0)
    Image.fromarray(tile,"RGBA").save(destination,optimize=True)


def main() -> None:
    for entry in ITEMS:
        extract(entry)
    material_sample("asphalt_sheet.png", "assets/art/roads/materials/asphalt_surface.png", (112, 142, 192, 222), "asphalt")
    material_sample("asphalt_sheet.png", "assets/art/roads/materials/asphalt_worn_surface.png", (965, 135, 1045, 215), "asphalt")
    material_sample("asphalt_sheet.png", "assets/art/roads/materials/asphalt_shoulder.png", (75, 125, 155, 205), "asphalt")
    aligned_road_sample("dirt_road_sheet.png", "assets/art/roads/materials/dirt_surface.png", (0, 0, 435, 305), 62, 150, "dirt")
    aligned_road_sample("dirt_road_sheet.png", "assets/art/roads/materials/farm_track_surface.png", (430, 0, 840, 315), 62, 145, "dirt")
    aligned_road_sample("dirt_road_sheet.png", "assets/art/roads/materials/mud_surface.png", (765, 615, 1185, 990), 62, 150, "dirt")
    aligned_road_sample("dirt_road_sheet.png", "assets/art/roads/materials/two_track_surface.png", (1155, 625, 1536, 990), 62, 135, "dirt")
    aligned_road_sample("dirt_road_sheet.png", "assets/art/roads/materials/footpath_surface.png", (0, 0, 435, 305), 62, 54, "dirt")
    aligned_road_sample("dirt_road_sheet.png", "assets/art/roads/materials/dirt_shoulder.png", (0, 0, 435, 305), 62, 230, "dirt", True)
    print(f"Extracted {len(ITEMS)} clean components and 9 detailed spline material samples from five immutable sheets.")


if __name__ == "__main__":
    main()
