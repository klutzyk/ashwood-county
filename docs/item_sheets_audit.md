# Item Sheets Audit

All five PNG source sheets in `assets/art/sheets/items/` were audited at
their native 1536x1024 resolution. Source sheets were not modified,
overwritten, or used directly at runtime. Approved derivatives preserve the
original RGBA pixels, isolate the selected foreground component, retain its
antialiased edge, add a four-pixel transparent crop margin, and use stable
semantic filenames.

The deterministic extraction recipe lives in three scripts (grid-layout
sheets, the medicine sheet, and the irregular food sheet respectively; see
"Method" below). They produced **72 approved item icons** under
`assets/art/items/`. No aliases were created.

## 1. `tools01_sheet.png` (6x4 grid, 24 illustrated items)

23 of 24 retained under `assets/art/items/tools/` (14) and
`assets/art/items/materials/` (9):

- Tools: `hammer`, `screwdriver`, `wrench`, `pliers`, `multitool`,
  `duct_tape`, `electrical_tape`, `rope`, `flashlight`, `batteries`,
  `zip_ties`, `tarp`, `fuel_can`, `tool_kit`.
- Materials: `nails`, `screws`, `scrap_metal`, `metal_pipes_bundle`,
  `sheet_metal`, `bolts_and_nuts`, `gears`, `wire_coil`, `wood_planks`.

Rejected: the sheet's `CROWBAR` illustration was extracted successfully but
**not** copied into the final catalog — `melee_sheet.png` contains a second,
cleaner crowbar illustration used for the `MeleeWeapon` item instead, and
carrying both would create two competing `ItemDefinition`s for one real-world
object. The tools-sheet crowbar extraction remains in the working debug
output but is unused.

## 2. `melee_sheet.png` (6x2 grid, 12 illustrated items)

All 12 retained under `assets/art/items/melee/`: `baseball_bat`,
`kitchen_knife`, `hatchet`, `crowbar`, `metal_pipe`, `sledgehammer`,
`machete`, `spiked_bat`, `shovel`, `fire_axe`, `maul`, `chain`.

`metal_pipe` (a single wielded pipe) is kept distinct from the tools sheet's
`metal_pipes_bundle` (a stack of raw pipe stock) — they are visually and
functionally different objects (one a weapon, one a crafting material), not
a duplicate.

Rejected: none.

## 3. `survival_gear_sheet.png` (4x2 grid, 8 illustrated items)

All 8 retained under `assets/art/items/equipment/`: `small_backpack`,
`hiking_backpack`, `duffel_bag`, `tool_bag`, `fanny_pack`, `tactical_vest`,
`messenger_bag`, `water_canteen`.

Extraction note: `hiking_backpack`'s high-contrast camo weave initially
leaked through the tolerant background flood fill at the sheet-wide
tolerance (30), erasing large patches of intact fabric. Lowering tolerance
to 18 for this sheet fixed it cleanly; re-verified visually against the
source.

Known minor imperfection: `fanny_pack`'s waist strap has a small gap on its
right side where a soft highlight in the source art blends close to
background color. Verified against tolerance values from 6-30 — the gap
persists even at the strictest setting, confirming it is a genuine soft
blend in the source painting rather than an extraction artifact. Accepted;
the icon is still clearly and unambiguously a fanny pack.

Rejected: none.

## 4. `medicine01_sheet.png` (5x3 grid, 15 illustrated items)

All 15 retained under `assets/art/items/medical/`: `bandage`, `gauze_pads`,
`adhesive_bandage`, `medical_tape`, `antiseptic_solution`, `alcohol_wipes`,
`pain_relief_tablets`, `antibiotic_tablets`, `cough_syrup`,
`multivitamin_tablets`, `burn_gel`, `thermometer`, `disposable_gloves`,
`tourniquet`, `first_aid_kit`.

Extraction note: this sheet is not a perfectly uniform grid — several
captions wrap to two lines (`ANTISEPTIC SOLUTION`, `MULTIVITAMIN TABLETS`)
and overflow the nominal row boundary, bleeding into the row below when
cropped naively. Fixed by classifying same-column content runs by height
(icon art vs. one/two-line caption text) instead of assuming a fixed
per-row split point. Two items needed a small manual end-row adjustment
after a per-row pixel scan (`antibiotic_tablets`, where a faint drop-shadow
visually bridges the pill bottle to its caption with no true zero-content
gap row).

Rejected: none.

## 5. `food01_sheet.png` (irregular hand-placed layout, 14 illustrated items)

All 14 retained under `assets/art/items/food/`: `canned_beans`,
`canned_tomato_soup`, `canned_tuna`, `canned_spaghetti`, `canned_corn`,
`peanut_butter`, `bottled_water`, `sports_drink`, `apple_juice_box`,
`energy_bar`, `saltine_crackers`, `corn_flakes_box`, `instant_noodles`,
`beef_jerky`.

This sheet differs from the other four: no printed captions, an irregular
(non-grid) layout, and an opaque blurred color-gradient background instead
of a checkerboard. Manual window rectangles (calibrated against a 64px
reference grid overlay) bound each item; extraction reused the same
tolerant border flood fill.

Extraction note: a real bug was found and fixed during this sheet's work —
the flood fill's color-distance calculation subtracted `uint8` arrays
directly, which wraps around on unsigned underflow instead of going
negative, corrupting the distance metric. This produced a visible colored
halo/vignette around every item that did not shrink no matter how high the
tolerance was raised (the giveaway that tolerance wasn't the actual
variable at fault). Fixed by casting to a signed 16-bit type before the
subtraction; all sheets were re-extracted afterward and re-verified
visually, since the bug was latent in every extraction, not just this one
(the checkerboard sheets happened to look correct anyway due to their much
higher foreground/background contrast).

Rejected: none. `ENERGY BAR`'s printed label has a visible AI-generation
text glitch in the source art (distorted lettering below the main title);
it's retained as-is since it's a legible, load-bearing part of the object's
own printed packaging, and the sheet was not altered.

## Method

Three scripts under the working extraction toolkit (calibration and
extraction working files, not checked into the repo, mirroring the
throwaway-tool pattern already used by `tools/source_sheet_audit/`):

- Uniform-grid checkerboard sheets (tools/melee/survival_gear): scan each
  grid column's full pixel height and classify contiguous non-background
  row runs by height — tall runs are icon art, short runs are one or more
  caption text lines (discarded regardless of how many lines they wrap
  to). This replaced an earlier per-row "largest gap" heuristic that, on
  inspection, was silently baking full or partial caption text into a
  minority of icons (caught by deliberately raising a noise-filter
  threshold, which turned invisible baked-in captions into visible garbled
  fragments — proof the split point itself was wrong, not just noisy).
- `medicine01_sheet.png`: identical content-run classification, generalized
  per-column instead of per-row-slice, because its two-line captions break
  the uniform-row assumption.
- `food01_sheet.png`: manual per-item window rectangles (no captions, no
  grid to exploit) with the same underlying tolerant flood fill.

Background removal: a 4-connected BFS flood fill seeded from every window
border pixel, absorbing a neighbor into "background" when its Manhattan
color distance from the already-absorbed pixel is under a tolerance
(18-30 depending on sheet, see per-sheet notes above). This tracks slow
gradients and checkerboard noise while stopping hard at an item's painted
outline. Foreground connected components under 40px are dropped as dust/
antialiasing noise. Every accepted icon was cropped to its content
bounding box with a 4px transparent margin added back.

## Quality verification

- Visually inspected all five native source sheets before extraction.
- Visually inspected contact sheets of all 72 approved extractions,
  laid out at their original sheet positions, after every extraction pass.
- Iterated on two real defects found only through visual inspection (not
  assumed correct from clean-looking metadata): caption text baked into a
  majority of icons on three of five sheets, and a color-distance
  calculation bug masked by high-contrast checkerboard backgrounds.
- Confirmed transparent RGBA output, compact bounds, preserved
  antialiasing, and no baked checkerboard/gradient background remaining
  in the final set.
- Source PNGs remain present and unchanged (verified via `git status`;
  they are new untracked files this session never wrote to).
