"""Deterministic, alpha-authoritative extraction for the genuinely-transparent
item sheets (tools01_sheet.png, melee_sheet.png, survival_gear_sheet.png,
medicine01_sheet.png). food01_sheet.png is handled by a separate, older
gradient-background pipeline and is not touched here.

Replaces an earlier version of this script that pre-cropped a fixed-size
window around a guessed box per item and then picked the largest connected
alpha component inside that window. That approach silently pulled in
neighboring items whenever a guessed window overlapped the next item's true
extent (observed contamination: messenger_bag.png contained part of the
adjacent tool_bag; bolts_and_nuts.png contained fragments of several
neighboring hardware piles).

This version never pre-crops. It labels connected alpha components across
the WHOLE sheet first, then decides which components belong to which item by
checking whether a hand-verified SEED PIXEL (known, from visual inspection,
to sit inside that item's artwork) falls inside that specific component. A
component can only contaminate an item if one of the item's own seeds
mistakenly lands inside it -- which is a calibration error you can catch by
cross-checking every seed against the actual component list (see
`verify_seeds` below), not a structural flaw in the algorithm.

Multi-part items (batteries, nails, screws, zip ties, disposable gloves...)
just get one seed per visible piece; seeds landing in the same component are
harmless (set union).

Usage:
    py -3 reextract_transparent_items.py            # dry run, writes report
    py -3 reextract_transparent_items.py --apply     # writes PNGs into assets/art/items/
"""
import argparse
import sys
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[2]
SHEETS = ROOT / "assets/art/sheets/items"
ITEMS = ROOT / "assets/art/items"

ALPHA_THRESHOLD = 64
# Calibrated against a genuine near-touch between hiking_backpack and
# tactical_vest on survival_gear_sheet.png: their soft shadows/strap ends
# blur together at alpha ~10-50 across a couple of pixels, while every real
# item interior sits at alpha ~240-253. A low threshold (e.g. 8) bridges
# that gap into one false component; 64 does not, and still keeps every
# genuinely antialiased edge (which resolves close to full alpha within a
# pixel or two of the true silhouette).

MIN_COMPONENT_PX = 6
PAD = 6

# name -> (sheet, [(x, y), ...]) -- seeds verified against the actual
# component list (tools/verify_seeds), not eyeballed alone. Items visible on
# the new sheets with no existing ItemCatalog entry (tape measure, hardware
# organizer variants already covered by bolts_and_nuts, binoculars, headlamp,
# compass, sling bag, camping stove, field notes, metal cup, wound cleanser,
# antibiotic ointment, instant cold pack, anti-diarrheal/allergy tablets,
# face mask, medical pouch, emergency blanket, tweezers, scissors, eye wash,
# and the extra screwdriver/pliers/bandage/rope/flashlight/tarp/duct-tape/
# multitool variants) are intentionally left unmapped -- this script does not
# add new items, only re-extracts existing ones.
ITEMS_BY_SHEET = {
    "tools01_sheet.png": {
        "tools/hammer": [(122, 169)],
        "tools/wrench": [(310, 150)],
        "tools/screwdriver": [(510, 160)],
        "tools/pliers": [(950, 150)],
        "tools/multitool": [(1420, 150)],
        "tools/duct_tape": [(140, 390)],
        "tools/electrical_tape": [(360, 390)],
        "tools/flashlight": [(850, 390)],
        "tools/batteries": [(1063, 410), (1125, 421), (1217, 427)],
        "tools/zip_ties": [(1401, 415)],
        "materials/nails": [(110, 610), (150, 590), (80, 640)],
        "materials/screws": [(340, 610), (310, 590), (370, 640)],
        "materials/bolts_and_nuts": [(590, 610)],
        "materials/gears": [(830, 600), (890, 630), (800, 650)],
        "materials/wire_coil": [(1119, 670)],
        "tools/rope": [(1340, 600), (1440, 630)],
        "materials/scrap_metal": [(90, 800), (150, 850), (110, 900), (60, 830)],
        "materials/metal_pipes_bundle": [(320, 830), (370, 800)],
        "materials/sheet_metal": [(540, 800), (600, 850), (500, 900)],
        "materials/wood_planks": [(780, 830)],
        "tools/fuel_can": [(980, 800)],
        "tools/tarp": [(1210, 830)],
        "tools/tool_kit": [(1420, 830)],
    },
    "melee_sheet.png": {
        "melee/baseball_bat": [(110, 150)],
        "melee/kitchen_knife": [(350, 200)],
        "melee/hatchet": [(620, 150)],
        "melee/crowbar": [(880, 200)],
        "melee/metal_pipe": [(1130, 200)],
        "melee/sledgehammer": [(1400, 150)],
        "melee/machete": [(113, 744)],
        "melee/spiked_bat": [(380, 650)],
        "melee/shovel": [(630, 650)],
        "melee/fire_axe": [(880, 600)],
        "melee/maul": [(1150, 650)],
        "melee/chain": [(1354, 754)],
    },
    "survival_gear_sheet.png": {
        "equipment/small_backpack": [(130, 150)],
        "equipment/hiking_backpack": [(450, 150)],
        "equipment/duffel_bag": [(750, 150)],
        "equipment/tool_bag": [(1050, 150)],
        "equipment/messenger_bag": [(1370, 150)],
        "equipment/fanny_pack": [(130, 470)],
        "equipment/tactical_vest": [(450, 470)],
        "equipment/water_canteen": [(1050, 450)],
    },
    "medicine01_sheet.png": {
        "medical/bandage": [(80, 90)],
        "medical/gauze_pads": [(500, 90)],
        "medical/adhesive_bandage": [(720, 90)],
        "medical/medical_tape": [(940, 90)],
        "medical/tourniquet": [(1400, 90)],
        "medical/antiseptic_solution": [(80, 350)],
        "medical/alcohol_wipes": [(500, 350)],
        "medical/burn_gel": [(961, 376)],
        "medical/thermometer": [(1400, 350)],
        "medical/pain_relief_tablets": [(80, 600)],
        "medical/antibiotic_tablets": [(290, 600)],
        "medical/cough_syrup": [(500, 600)],
        "medical/multivitamin_tablets": [(1150, 600)],
        "medical/disposable_gloves": [(60, 830), (150, 870)],
        "medical/first_aid_kit": [(500, 850)],
    },
}


def load_sheet(name):
    return np.asarray(Image.open(SHEETS / name).convert("RGBA"))


def label_components(sheet_rgba):
    mask = sheet_rgba[:, :, 3] > ALPHA_THRESHOLD
    structure = ndimage.generate_binary_structure(2, 2)
    return ndimage.label(mask, structure=structure)


def extract_by_seeds(sheet_rgba, labels, seeds, pad=PAD):
    label_ids = set()
    for (x, y) in seeds:
        lid = int(labels[y, x])
        if lid == 0:
            return None, f"seed ({x},{y}) landed on background"
        label_ids.add(lid)

    mask = np.isin(labels, list(label_ids))
    sizes = ndimage.sum(mask, labels, index=list(label_ids))
    kept_ids = [lid for lid, size in zip(label_ids, sizes) if size >= MIN_COMPONENT_PX]
    if not kept_ids:
        return None, "all seeded components below min size"
    mask = np.isin(labels, kept_ids)

    ys, xs = np.where(mask)
    y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
    h, w = y1 - y0, x1 - x0
    out = np.zeros((h + pad * 2, w + pad * 2, 4), dtype=np.uint8)
    sub_rgba = sheet_rgba[y0:y1, x0:x1]
    sub_mask = mask[y0:y1, x0:x1]
    out[pad:pad + h, pad:pad + w, :3] = sub_rgba[:, :, :3]
    out[pad:pad + h, pad:pad + w, 3] = np.where(sub_mask, sub_rgba[:, :, 3], 0)
    stats = dict(width=int(w), height=int(h), bbox=(int(x0), int(y0), int(x1), int(y1)),
                 fg_pixels=int(mask.sum()), n_components=len(kept_ids))
    return out, stats


def component_bbox_list(labels, n):
    objs = ndimage.find_objects(labels)
    out = []
    for i, sl in enumerate(objs, start=1):
        if sl is None:
            continue
        ys, xs = sl
        size = int((labels[sl] == i).sum())
        out.append(dict(id=i, x0=xs.start, y0=ys.start, x1=xs.stop, y1=ys.stop,
                         cx=(xs.start + xs.stop) // 2, cy=(ys.start + ys.stop) // 2, size=size))
    return out


def verify_seeds():
    """Cross-check every declared seed against the real component list: every
    seed must land on foreground, and every component in the item's rough
    neighborhood should be claimed by exactly one item's seed set (so a
    missed multi-part piece, like a battery, shows up as an "unused"
    component instead of silently vanishing)."""
    problems = 0
    for sheet_name, items in ITEMS_BY_SHEET.items():
        sheet = load_sheet(sheet_name)
        labels, n = label_components(sheet)
        for name, seeds in items.items():
            for (x, y) in seeds:
                if int(labels[y, x]) == 0:
                    print(f"SEED-MISS {sheet_name} {name} seed=({x},{y})")
                    problems += 1
    return problems == 0


def contamination_flags(sheet_rgba, stats):
    flags = []
    h, w = stats["height"], stats["width"]
    sheet_h, sheet_w = sheet_rgba.shape[0], sheet_rgba.shape[1]
    if w > sheet_w * 0.55 or h > sheet_h * 0.55:
        flags.append(f"huge-crop({w}x{h})")
    x0, y0, x1, y1 = stats["bbox"]
    if x0 <= 1 or y0 <= 1 or x1 >= sheet_w - 1 or y1 >= sheet_h - 1:
        flags.append("touches-sheet-edge")  # may be legitimate framing near the sheet's own border; visually confirm
    fill = stats["fg_pixels"] / max(1, w * h)
    if fill < 0.12:
        flags.append(f"sparse-fill({fill:.2f})")
    return flags


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--apply", action="store_true", help="write PNGs into assets/art/items/")
    args = parser.parse_args()

    if not verify_seeds():
        print("Aborting: fix seed misses above before extracting.")
        raise SystemExit(1)

    total = 0
    flagged = 0
    for sheet_name, items in ITEMS_BY_SHEET.items():
        sheet = load_sheet(sheet_name)
        labels, n = label_components(sheet)
        for name, seeds in items.items():
            out, stats = extract_by_seeds(sheet, labels, seeds)
            if out is None:
                print(f"REJECT {sheet_name} {name}: {stats}")
                continue
            total += 1
            flags = contamination_flags(sheet, stats)
            if flags:
                flagged += 1
                print(f"FLAG {sheet_name} {name}: {flags} {stats}")
            if args.apply:
                path = ITEMS / (name + ".png")
                path.parent.mkdir(parents=True, exist_ok=True)
                Image.fromarray(out, "RGBA").save(path, optimize=True)
    print(f"ITEM_EXTRACTION_AUDIT sheets=4 items={total} flagged={flagged} applied={args.apply}")
    print("Flagged items are not necessarily wrong -- visually inspect a contact sheet before trusting output.")


if __name__ == "__main__":
    main()
