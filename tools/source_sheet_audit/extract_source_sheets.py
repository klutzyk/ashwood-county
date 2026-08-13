"""Deterministically extract the approved assets from assets/art/sheets.

The source sheets are immutable inputs.  Every derivative is rebuilt from a
named source rectangle, assigned to its nearest foreground alpha component,
and cropped to clean transparent bounds.  This avoids the faint low-alpha
bridges present between a few AI-generated neighbours without hard-clipping
their antialiased silhouettes.
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
import shutil

import numpy as np
from PIL import Image
from scipy import ndimage


ROOT = Path(__file__).resolve().parents[2]
SHEETS = ROOT / "assets/art/sheets"


@dataclass(frozen=True)
class Extraction:
    source: str
    output: str
    seed: tuple[int, int]
    window: tuple[int, int, int, int]
    threshold: int = 4


def e(source: str, output: str, seed: tuple[int, int], window: tuple[int, int, int, int], threshold: int = 4) -> Extraction:
    return Extraction(source, output, seed, window, threshold)


ITEMS = [
    # Maintained residences, left-to-right across the two rows.
    e("houses.png", "assets/art/buildings/residential/house_01.png", (205, 320), (0, 70, 415, 535)),
    e("houses.png", "assets/art/buildings/residential/house_02.png", (585, 320), (350, 70, 820, 535)),
    e("houses.png", "assets/art/buildings/residential/house_03.png", (970, 330), (730, 80, 1200, 535)),
    e("houses.png", "assets/art/buildings/residential/house_04.png", (1350, 330), (1090, 80, 1536, 535)),
    e("houses.png", "assets/art/buildings/residential/house_05.png", (205, 760), (0, 530, 420, 1024)),
    e("houses.png", "assets/art/buildings/residential/house_06.png", (585, 760), (350, 530, 820, 1024)),
    e("houses.png", "assets/art/buildings/residential/house_07.png", (970, 760), (730, 530, 1200, 1024)),
    e("houses.png", "assets/art/buildings/residential/house_08.png", (1350, 760), (1090, 530, 1536, 1024)),

    # Abandoned residences.  Threshold 240 separates opaque silhouettes that
    # are joined only by unintended semi-transparent background haze.
    e("houses (abandoned).png", "assets/art/buildings/residential/abandoned_house_01.png", (200, 330), (0, 70, 410, 535), 240),
    e("houses (abandoned).png", "assets/art/buildings/residential/abandoned_house_02.png", (580, 330), (350, 70, 805, 535), 240),
    e("houses (abandoned).png", "assets/art/buildings/residential/abandoned_house_03.png", (960, 330), (735, 80, 1180, 535), 240),
    e("houses (abandoned).png", "assets/art/buildings/residential/abandoned_house_04.png", (1340, 330), (1090, 70, 1536, 535), 240),
    e("houses (abandoned).png", "assets/art/buildings/residential/abandoned_house_05.png", (200, 760), (0, 520, 420, 1000), 240),
    e("houses (abandoned).png", "assets/art/buildings/residential/abandoned_house_06.png", (580, 760), (350, 520, 805, 1000), 240),
    e("houses (abandoned).png", "assets/art/buildings/residential/abandoned_house_07.png", (960, 760), (735, 520, 1180, 1024), 253),
    e("houses (abandoned).png", "assets/art/buildings/residential/abandoned_house_08.png", (1340, 760), (1090, 520, 1536, 1024), 253),

    # Rural structures, source reading order.
    e("rural_structures.png", "assets/art/buildings/rural/outhouse_01.png", (790, 185), (650, 10, 920, 340)),
    e("rural_structures.png", "assets/art/buildings/rural/garden_shed_01.png", (520, 200), (330, 20, 710, 370)),
    e("rural_structures.png", "assets/art/buildings/rural/outhouse_02.png", (1010, 190), (870, 20, 1150, 360)),
    e("rural_structures.png", "assets/art/buildings/rural/wood_shelter_01.png", (1320, 210), (1080, 20, 1536, 390)),
    e("rural_structures.png", "assets/art/buildings/rural/tool_shed_01.png", (190, 210), (0, 20, 380, 370)),
    e("rural_structures.png", "assets/art/buildings/rural/farm_shelter_01.png", (250, 520), (0, 320, 515, 720)),
    e("rural_structures.png", "assets/art/buildings/rural/work_cabin_01.png", (690, 520), (470, 330, 890, 720)),
    e("rural_structures.png", "assets/art/buildings/rural/small_cabin_01.png", (1010, 520), (810, 340, 1200, 720)),
    e("rural_structures.png", "assets/art/buildings/rural/greenhouse_01.png", (1350, 520), (1130, 330, 1536, 720)),
    e("rural_structures.png", "assets/art/props/rural/mailbox_tree_01.png", (1370, 830), (1210, 630, 1536, 1024)),
    e("rural_structures.png", "assets/art/props/rural/laundry_yard_01.png", (660, 830), (470, 660, 850, 1024)),
    e("rural_structures.png", "assets/art/buildings/rural/trailer_01.png", (1040, 835), (790, 650, 1290, 1024)),
    e("rural_structures.png", "assets/art/props/rural/garden_plot_01.png", (260, 835), (0, 650, 545, 1024)),

    # Urban props, source reading order.
    e("urban props.png", "assets/art/props/urban/banner_lamp_01.png", (620, 210), (520, 0, 720, 420)),
    e("urban props.png", "assets/art/props/urban/traffic_light_01.png", (880, 210), (700, 0, 1060, 430)),
    e("urban props.png", "assets/art/props/urban/utility_pole_01.png", (380, 210), (220, 0, 550, 420)),
    e("urban props.png", "assets/art/props/urban/street_light_01.png", (110, 220), (0, 0, 285, 420)),
    e("urban props.png", "assets/art/props/urban/road_sign_01.png", (1120, 210), (1020, 10, 1210, 410)),
    e("urban props.png", "assets/art/props/urban/bus_shelter_01.png", (1360, 230), (1170, 30, 1536, 430)),
    e("urban props.png", "assets/art/props/urban/mailbox_01.png", (285, 485), (170, 350, 390, 640)),
    e("urban props.png", "assets/art/props/urban/county_planter_01.png", (1130, 500), (930, 350, 1340, 660)),
    e("urban props.png", "assets/art/props/urban/trash_bin_01.png", (500, 500), (360, 350, 640, 640)),
    e("urban props.png", "assets/art/props/urban/newspaper_box_01.png", (1400, 500), (1270, 350, 1536, 680)),
    e("urban props.png", "assets/art/props/urban/fire_hydrant_01.png", (95, 500), (0, 360, 190, 630)),
    e("urban props.png", "assets/art/props/urban/bench_01.png", (760, 500), (570, 350, 970, 650)),
    e("urban props.png", "assets/art/props/urban/road_barrier_01.png", (160, 680), (0, 550, 330, 825)),
    e("urban props.png", "assets/art/props/urban/traffic_barrel_01.png", (440, 680), (300, 550, 580, 825)),
    e("urban props.png", "assets/art/props/urban/chainlink_fence_01.png", (900, 690), (700, 550, 1080, 825)),
    e("urban props.png", "assets/art/props/urban/traffic_cone_01.png", (635, 690), (520, 560, 735, 815)),
    e("urban props.png", "assets/art/props/urban/graffiti_barrier_01.png", (1330, 690), (1070, 550, 1536, 840)),
    e("urban props.png", "assets/art/props/urban/dumpster_01.png", (500, 900), (300, 740, 700, 1024)),
    e("urban props.png", "assets/art/props/urban/bicycle_rack_01.png", (1380, 900), (1160, 740, 1536, 1024)),
    e("urban props.png", "assets/art/props/urban/hvac_unit_01.png", (800, 900), (620, 740, 970, 1024)),
    e("urban props.png", "assets/art/props/urban/electrical_cabinet_01.png", (1080, 900), (930, 740, 1200, 1024)),
    e("urban props.png", "assets/art/props/urban/trash_pile_01.png", (150, 900), (0, 740, 370, 1024)),

    # Landmarks, source reading order.
    e("landmarks.png", "assets/art/props/landmarks/welcome_sign_01.png", (250, 230), (0, 0, 535, 470)),
    e("landmarks.png", "assets/art/props/landmarks/fire_lookout_tower_01.png", (1370, 260), (1160, 0, 1536, 570)),
    e("landmarks.png", "assets/art/props/landmarks/picnic_table_01.png", (650, 215), (430, 20, 850, 390)),
    e("landmarks.png", "assets/art/props/landmarks/campfire_01.png", (1010, 220), (790, 20, 1230, 410)),
    e("landmarks.png", "assets/art/props/landmarks/trail_board_01.png", (710, 560), (420, 300, 1010, 810)),
    e("landmarks.png", "assets/art/props/landmarks/communications_tower_01.png", (1220, 660), (940, 320, 1500, 1024)),
    e("landmarks.png", "assets/art/props/landmarks/ruined_cabin_01.png", (230, 570), (0, 340, 500, 810)),
    e("landmarks.png", "assets/art/props/landmarks/ridge_viewpoint_01.png", (760, 880), (480, 680, 1040, 1024)),
    e("landmarks.png", "assets/art/props/landmarks/dock_rowboat_01.png", (260, 870), (0, 680, 550, 1024)),

    # Abandoned vehicles and larger coherent roadside debris clusters.
    e("vehicles (abandoned).png", "assets/art/props/vehicles/suv_01.png", (600, 125), (370, 0, 805, 250)),
    e("vehicles (abandoned).png", "assets/art/props/vehicles/sedan_01.png", (200, 125), (0, 0, 420, 240)),
    e("vehicles (abandoned).png", "assets/art/props/vehicles/pickup_01.png", (1000, 125), (760, 0, 1210, 250)),
    e("vehicles (abandoned).png", "assets/art/props/vehicles/police_car_01.png", (1360, 140), (1150, 0, 1536, 270)),
    e("vehicles (abandoned).png", "assets/art/props/vehicles/ambulance_01.png", (200, 330), (0, 170, 420, 485)),
    e("vehicles (abandoned).png", "assets/art/props/vehicles/logging_suv_01.png", (980, 335), (780, 200, 1180, 480)),
    e("vehicles (abandoned).png", "assets/art/props/vehicles/van_01.png", (1360, 340), (1140, 200, 1536, 490)),
    e("vehicles (abandoned).png", "assets/art/props/vehicles/sedan_02.png", (600, 340), (390, 210, 830, 480)),
    e("vehicles (abandoned).png", "assets/art/props/vehicles/school_bus_01.png", (220, 550), (0, 380, 500, 710)),
    e("vehicles (abandoned).png", "assets/art/props/vehicles/box_truck_01.png", (670, 550), (430, 380, 900, 730)),
    e("vehicles (abandoned).png", "assets/art/props/vehicles/pickup_02.png", (1060, 555), (850, 430, 1250, 690)),
    e("vehicles (abandoned).png", "assets/art/props/vehicles/station_wagon_01.png", (1380, 555), (1190, 430, 1536, 690)),
    e("vehicles (abandoned).png", "assets/art/props/vehicles/motorcycle_01.png", (1400, 755), (1230, 620, 1536, 870)),
    e("vehicles (abandoned).png", "assets/art/props/vehicles/van_02.png", (1100, 755), (920, 620, 1280, 870)),
    e("vehicles (abandoned).png", "assets/art/props/vehicles/abandoned_sedan_01.png", (170, 755), (0, 630, 330, 870)),
    e("vehicles (abandoned).png", "assets/art/props/vehicles/jeep_01.png", (500, 755), (290, 630, 650, 890)),
    e("vehicles (abandoned).png", "assets/art/props/vehicles/abandoned_sedan_02.png", (800, 755), (610, 630, 970, 880)),
    e("vehicles (abandoned).png", "assets/art/props/vehicles/overturned_vehicle_01.png", (180, 920), (0, 790, 385, 1024)),
    e("vehicles (abandoned).png", "assets/art/props/vehicles/utility_trailer_01.png", (500, 920), (300, 800, 680, 1024)),
    e("vehicles (abandoned).png", "assets/art/props/vehicles/boat_01.png", (800, 920), (600, 820, 970, 1024)),
    e("vehicles (abandoned).png", "assets/art/props/roadside/cones_barrier_01.png", (1390, 915), (1260, 820, 1500, 1024)),
    e("vehicles (abandoned).png", "assets/art/props/roadside/rusty_barrel_01.png", (1475, 925), (1380, 820, 1536, 1024)),
    e("vehicles (abandoned).png", "assets/art/props/roadside/traffic_cone_02.png", (1280, 910), (1200, 820, 1360, 1024)),
    e("vehicles (abandoned).png", "assets/art/props/roadside/tire_pile_01.png", (1100, 920), (900, 800, 1250, 1024)),
]


ALIASES = {
    "assets/art/buildings/residential/suburban_house_01.png": "assets/art/buildings/residential/house_02.png",
    "assets/art/buildings/residential/suburban_house_02.png": "assets/art/buildings/residential/house_07.png",
    "assets/art/buildings/residential/ranch_house_01.png": "assets/art/buildings/residential/house_05.png",
    "assets/art/buildings/residential/two_storey_house_01.png": "assets/art/buildings/residential/house_01.png",
    "assets/art/buildings/residential/house_abandoned_01.png": "assets/art/buildings/residential/abandoned_house_07.png",
    "assets/art/buildings/residential/house_abandoned_02.png": "assets/art/buildings/residential/abandoned_house_02.png",
    "assets/art/buildings/residential/house_abandoned_03.png": "assets/art/buildings/residential/abandoned_house_03.png",
    "assets/art/buildings/residential/house_abandoned_04.png": "assets/art/buildings/residential/abandoned_house_04.png",
    "assets/art/buildings/rural/shed_01.png": "assets/art/buildings/rural/tool_shed_01.png",
    "assets/art/buildings/rural/cabin_01.png": "assets/art/buildings/rural/work_cabin_01.png",
    "assets/art/buildings/rural/mobile_home_01.png": "assets/art/buildings/rural/trailer_01.png",
    "assets/art/props/vehicles/abandoned_ambulance_01.png": "assets/art/props/vehicles/ambulance_01.png",
    "assets/art/props/vehicles/abandoned_sedan_03.png": "assets/art/props/vehicles/abandoned_sedan_01.png",
    "assets/art/props/vehicles/sheriff_car_01.png": "assets/art/props/vehicles/police_car_01.png",
    "assets/art/props/vehicles/farm_pickup_01.png": "assets/art/props/vehicles/pickup_01.png",
    "assets/art/props/vehicles/work_pickup_01.png": "assets/art/props/vehicles/pickup_02.png",
    "assets/art/props/vehicles/logging_truck_01.png": "assets/art/props/vehicles/box_truck_01.png",
    "assets/art/props/vehicles/old_van_01.png": "assets/art/props/vehicles/van_02.png",
    "assets/art/props/vehicles/trailer_01.png": "assets/art/props/vehicles/utility_trailer_01.png",
    "assets/art/props/vehicles/wreck_01.png": "assets/art/props/vehicles/overturned_vehicle_01.png",
    "assets/art/props/vehicles/bus_01.png": "assets/art/props/vehicles/school_bus_01.png",
    "assets/art/props/vehicles/truck_01.png": "assets/art/props/vehicles/box_truck_01.png",
    "assets/art/props/landmarks/ashwood_welcome_sign_01.png": "assets/art/props/landmarks/welcome_sign_01.png",
    "assets/art/props/landmarks/watchtower_01.png": "assets/art/props/landmarks/fire_lookout_tower_01.png",
    "assets/art/props/landmarks/trail_information_board_01.png": "assets/art/props/landmarks/trail_board_01.png",
    "assets/art/props/landmarks/dock_01.png": "assets/art/props/landmarks/dock_rowboat_01.png",
    "assets/art/props/landmarks/wooden_dock_01.png": "assets/art/props/landmarks/dock_rowboat_01.png",
    "assets/art/props/landmarks/rowboat_01.png": "assets/art/props/landmarks/dock_rowboat_01.png",
    "assets/art/props/landmarks/boat_01.png": "assets/art/props/landmarks/dock_rowboat_01.png",
    "assets/art/props/landmarks/lookout_viewpoint_01.png": "assets/art/props/landmarks/ridge_viewpoint_01.png",
    "assets/art/props/rural/picnic_table_01.png": "assets/art/props/landmarks/picnic_table_01.png",
    "assets/art/props/rural/campfire_01.png": "assets/art/props/landmarks/campfire_01.png",
    "assets/art/props/urban/service_station_sign_01.png": "assets/art/props/urban/road_sign_01.png",
    "assets/art/props/urban/communications_tower_01.png": "assets/art/props/landmarks/communications_tower_01.png",
    "assets/art/props/urban/planter_01.png": "assets/art/props/urban/county_planter_01.png",
    "assets/art/props/urban/traffic_cones_01.png": "assets/art/props/urban/traffic_cone_01.png",
}


def extract(item: Extraction) -> None:
    source = np.asarray(Image.open(SHEETS / item.source).convert("RGBA"))
    x0, y0, x1, y1 = item.window
    window = source[y0:y1, x0:x1].copy()
    alpha = window[:, :, 3]
    labels, _ = ndimage.label(alpha >= item.threshold)
    sx, sy = item.seed[0] - x0, item.seed[1] - y0
    if not (0 <= sx < window.shape[1] and 0 <= sy < window.shape[0]):
        raise ValueError(f"Seed outside window for {item.output}")
    label = int(labels[sy, sx])
    if label == 0:
        # Select the foreground component nearest the stated semantic seed.
        ys, xs = np.where(labels > 0)
        nearest = np.argmin((xs - sx) ** 2 + (ys - sy) ** 2)
        label = int(labels[ys[nearest], xs[nearest]])

    selected = labels == label
    if item.threshold > 4:
        # Restore the source's antialiasing/haze around the opaque component,
        # but never reach another opaque component.
        selected = ndimage.binary_dilation(selected, iterations=10)
    output = window.copy()
    output[:, :, 3] = np.where(selected, alpha, 0).astype(np.uint8)

    ys, xs = np.where(output[:, :, 3] > 0)
    if len(xs) == 0:
        raise ValueError(f"No retained pixels for {item.output}")
    pad = 3
    left, top = int(xs.min()), int(ys.min())
    right, bottom = int(xs.max()) + 1, int(ys.max()) + 1
    # Padding is added outside the cropped component, not clamped back to the
    # source window. This guarantees transparent outer bounds even where art
    # legitimately reaches a sheet edge.
    output = np.pad(output[top:bottom, left:right], ((pad, pad), (pad, pad), (0, 0)), mode="constant")

    destination = ROOT / item.output
    destination.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(output, "RGBA").save(destination, optimize=True)


def main() -> None:
    for item in ITEMS:
        extract(item)
    for destination_name, source_name in ALIASES.items():
        destination = ROOT / destination_name
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(ROOT / source_name, destination)
    print(f"Extracted {len(ITEMS)} approved source components and {len(ALIASES)} semantic aliases.")


if __name__ == "__main__":
    main()
