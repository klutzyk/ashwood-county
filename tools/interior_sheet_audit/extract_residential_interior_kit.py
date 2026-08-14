"""Deterministically extract approved residential-interior components.

The source sheet remains immutable.  Each derivative is selected from the
high-alpha foreground component nearest a semantic seed, expanded only enough
to retain its antialiased edge, and padded with a transparent outer ring.
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
import numpy as np
from PIL import Image
from scipy import ndimage


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "assets/art/sheets/interiors/residential_interior_kit_01.png"


@dataclass(frozen=True)
class Item:
    name: str
    seed: tuple[int, int]
    window: tuple[int, int, int, int]
    category: str


def item(name: str, seed: tuple[int, int], window: tuple[int, int, int, int], category: str) -> Item:
    return Item(name, seed, window, category)


ITEMS = [
    # Structure and surfaces.
    item("wall_damaged_green_01", (65, 85), (0, 0, 125, 175), "structure"),
    item("wall_window_01", (175, 85), (115, 0, 238, 175), "structure"),
    item("wall_plain_cream_01", (290, 85), (230, 0, 345, 175), "structure"),
    item("wall_plain_blue_01", (400, 85), (340, 0, 455, 175), "structure"),
    item("wall_damaged_cream_01", (510, 85), (450, 0, 570, 175), "structure"),
    item("wall_barricaded_01", (620, 85), (560, 0, 680, 175), "structure"),
    item("wall_radiator_01", (725, 85), (670, 0, 785, 175), "structure"),
    item("door_closed_brown_01", (828, 80), (785, 0, 870, 165), "structure"),
    item("door_frame_open_01", (925, 80), (880, 0, 970, 165), "structure"),
    item("door_green_01", (1015, 80), (975, 0, 1055, 165), "structure"),
    item("door_blue_01", (1108, 80), (1065, 0, 1150, 165), "structure"),
    item("door_closed_light_01", (1195, 80), (1155, 0, 1240, 165), "structure"),
    item("door_louvered_01", (1285, 80), (1245, 0, 1330, 165), "structure"),
    item("door_broken_01", (1380, 80), (1335, 0, 1425, 170), "structure"),
    item("door_barricaded_01", (1470, 80), (1425, 0, 1536, 170), "structure"),
    item("window_shuttered_01", (60, 230), (0, 145, 120, 310), "structure"),
    item("window_broken_01", (808, 230), (760, 155, 865, 300), "structure"),
    item("floor_wood_light_01", (70, 325), (0, 275, 140, 375), "surfaces"),
    item("floor_wood_dark_01", (195, 325), (125, 275, 270, 375), "surfaces"),
    item("floor_parquet_01", (330, 325), (255, 275, 400, 375), "surfaces"),
    item("floor_checker_01", (705, 325), (635, 275, 780, 375), "surfaces"),
    item("floor_tile_cream_01", (835, 325), (760, 275, 905, 375), "surfaces"),
    item("floor_tile_damaged_01", (1450, 330), (1360, 275, 1536, 390), "surfaces"),
    item("rug_blue_01", (85, 405), (0, 345, 170, 455), "surfaces"),
    item("rug_red_01", (565, 405), (490, 345, 640, 455), "surfaces"),

    # Living/dining/bedroom furniture.
    item("sofa_plaid_01", (145, 475), (75, 415, 220, 530), "living"),
    item("sofa_blue_01", (315, 475), (235, 405, 395, 535), "living"),
    item("armchair_green_01", (490, 475), (430, 415, 545, 540), "living"),
    item("sofa_brown_01", (645, 490), (565, 425, 725, 560), "living"),
    item("armchair_worn_01", (725, 560), (670, 505, 780, 620), "living"),
    item("coffee_table_01", (220, 500), (170, 440, 270, 555), "living"),
    item("side_table_01", (410, 490), (375, 430, 445, 535), "living"),
    item("bookcase_tall_01", (45, 505), (0, 430, 85, 575), "living"),
    item("bookcase_medium_01", (850, 430), (800, 355, 900, 505), "living"),
    item("television_01", (935, 440), (885, 370, 990, 505), "living"),
    item("dining_table_01", (1155, 515), (1065, 430, 1245, 590), "living"),
    item("bed_blue_01", (895, 545), (800, 465, 985, 615), "bedroom"),
    item("bed_single_blue_01", (1170, 430), (1110, 355, 1230, 500), "bedroom"),
    item("dresser_01", (1060, 405), (1010, 350, 1110, 445), "bedroom"),

    # Kitchen, bathroom and utility.
    item("refrigerator_white_01", (290, 585), (240, 505, 335, 670), "kitchen"),
    item("refrigerator_green_01", (380, 585), (335, 510, 425, 665), "kitchen"),
    item("refrigerator_open_01", (465, 585), (410, 500, 515, 670), "kitchen"),
    item("stove_01", (555, 620), (500, 545, 605, 685), "kitchen"),
    item("counter_sink_01", (650, 630), (585, 555, 715, 710), "kitchen"),
    item("washer_sink_unit_01", (765, 665), (700, 595, 825, 735), "utility"),
    item("washing_machine_01", (855, 660), (810, 595, 900, 720), "utility"),
    item("water_heater_01", (915, 665), (885, 595, 945, 725), "utility"),
    item("supply_shelf_01", (1010, 675), (935, 575, 1085, 770), "utility"),
    item("toilet_01", (40, 760), (0, 700, 80, 815), "bathroom"),
    item("bathroom_sink_01", (110, 650), (75, 610, 145, 700), "bathroom"),
    item("bathtub_01", (245, 720), (145, 625, 335, 815), "bathroom"),

    # Restrained household/abandonment storytelling.
    item("plant_01", (45, 660), (5, 600, 80, 715), "clutter"),
    item("coat_stand_01", (650, 760), (610, 690, 690, 840), "clutter"),
    item("wall_clock_01", (580, 720), (545, 680, 615, 760), "clutter"),
    item("radio_01", (970, 770), (920, 725, 1010, 810), "clutter"),
    item("tool_chest_01", (1120, 720), (1070, 655, 1165, 780), "clutter"),
    item("storage_boxes_01", (675, 865), (625, 810, 725, 915), "clutter"),
    item("safe_01", (335, 930), (285, 860, 385, 995), "clutter"),
    item("blue_storage_bin_01", (245, 905), (200, 850, 290, 960), "clutter"),
    item("vacuum_01", (145, 905), (95, 830, 205, 975), "clutter"),
    item("bloodied_rug_01", (780, 925), (710, 875, 850, 980), "clutter"),
    item("cardboard_boxes_01", (1290, 950), (1235, 890, 1340, 1000), "clutter"),
]


def extract(entry: Item, source: np.ndarray) -> None:
    x0, y0, x1, y1 = entry.window
    crop = source[y0:y1, x0:x1].copy()
    alpha = crop[:, :, 3]
    labels, _ = ndimage.label(alpha >= 160)
    sx, sy = entry.seed[0] - x0, entry.seed[1] - y0
    if not (0 <= sx < crop.shape[1] and 0 <= sy < crop.shape[0]):
        raise ValueError(f"Seed outside window: {entry.name}")
    label = int(labels[sy, sx])
    if label == 0:
        ys, xs = np.where(labels > 0)
        nearest = np.argmin((xs - sx) ** 2 + (ys - sy) ** 2)
        label = int(labels[ys[nearest], xs[nearest]])
    selected = ndimage.binary_dilation(labels == label, iterations=8)
    crop[:, :, 3] = np.where(selected, alpha, 0).astype(np.uint8)
    ys, xs = np.where(crop[:, :, 3] > 0)
    if len(xs) == 0:
        raise ValueError(f"Empty extraction: {entry.name}")
    output = crop[int(ys.min()):int(ys.max()) + 1, int(xs.min()):int(xs.max()) + 1]
    output = np.pad(output, ((3, 3), (3, 3), (0, 0)), mode="constant")
    destination = ROOT / "assets/art/interiors/residential" / entry.category / f"{entry.name}.png"
    destination.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(output, "RGBA").save(destination, optimize=True)


def main() -> None:
    source = np.asarray(Image.open(SOURCE).convert("RGBA"))
    for entry in ITEMS:
        extract(entry, source)
    print(f"Extracted {len(ITEMS)} approved residential-interior assets.")


if __name__ == "__main__":
    main()
