# Terrain Source Asset Audit

## Sheet 01 — `terrain_asset_sheet.png`

The source is a transparent 1536×1024 isometric environment sheet. Thirty-six clean components were extracted and retained:

- Ground: `lush_grass_flowers_01`, `sparse_grass_01`, `grass_dirt_edge_01`, `sparse_dirt_01`, `bare_dirt_01`, `rocky_dirt_01`, `farm_rows_muddy_01`, `gravel_ground_01`, `grass_scatter_02`, `dirt_scatter_02`, `gravel_scatter_02`, `leaf_litter_02`.
- Roads: `rural_path_grass_01`, `dirt_track_01`, `gravel_road_01`, `asphalt_cracked_01`, `forest_track_01`, `asphalt_wear_01`.
- Water: `muddy_pond_01`, `muddy_puddle_01`.
- Vegetation: `bush_dense_02`, `bush_flowers_02`, `bush_berries_01`, `fern_02`, `grass_clump_02`, `flowers_white_02`, `flowers_yellow_01`, `hedge_01`.
- Props: `rock_formation_02`, `mossy_boulder_02`, `fallen_log_02`, `stump_02`, `rotted_log_01`, `fence_overgrown_02`, `stone_wall_01`, `palisade_fence_01`.

Rejected from sheet 01:

- Very small detached leaves, cones, sticks, flowers, and individual stones: too small at gameplay zoom and costly/noisy as separate decoration.
- Near-duplicates of already extracted grass/dirt scatter: no material visual gain.
- Components touching adjacent fragments in the sheet: rejected rather than accepting contaminated crops.

Currently integrated from this extraction:

- Road wear, cracked asphalt, dirt/forest/gravel track overlays.
- Muddy farm rows and field breakup.
- Mill Creek ferns, logs, stumps, boulders, and rock formations.
- Farm hedges, berry bushes, and overgrown fencing.

## Sheet 02 — `terrain_asset_sheet_02.png`

Blocked: this file is not currently present in `assets/art/terrain/`. It must be restored before a truthful visual audit or extraction can be completed. No sheet-02 assets were invented or substituted.

## Extraction quality

Every retained derivative preserves source alpha, is cropped to its nontransparent bounds with a two-pixel antialiasing margin, and was checked for transparent corners and neighboring-object contamination. The canonical sheet 01 remains untouched.
