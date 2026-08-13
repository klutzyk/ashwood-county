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

The restored source is a transparent 1536×1024 isometric environment sheet. Ninety-three clean components were extracted and retained:

- Ground: `wildflower_grass_02`, `mushroom_meadow_01`, `mixed_grass_03`, `sparse_ground_02`, `meadow_flowers_03`, `dry_grass_rock_01`, `muddy_ground_02`, `forest_floor_02`, `ploughed_rows_02`, `ploughed_curve_01`, `rocky_ground_03`, `stone_outcrop_ground_01`.
- Roads: `dirt_ruts_02`, `wet_track_01`, `dirt_lane_02`, `gravel_road_02`, `asphalt_straight_02`, `forest_track_02`, `dirt_straight_03`, `dirt_crossroads_01`, `dirt_junction_01`, `dirt_curve_01`, `asphalt_intersection_01`, `asphalt_curve_01`, `asphalt_edge_01`, `asphalt_bend_01`, `muddy_curve_01`.
- Rail: `rail_straight_01`, `rail_grass_straight_01`, `rail_curve_01`.
- Water: `autumn_puddle_01`, `berry_puddle_01`, `mud_puddle_02`, `pond_reeds_01`, `pond_deep_01`, `pond_lilies_01`, `river_rapids_straight_01`, `river_rapids_rocks_01`, `river_rapids_curve_01`, `creek_rapids_01`, `marsh_pond_01`, `marsh_pond_02`.
- Rocks: `cliff_rock_01` through `cliff_rock_04`, `boulder_cluster_03`, `rock_slab_01`.
- Trees and undergrowth: `deciduous_autumn_01`, `birch_01`, `pine_02`, `birch_young_01`, `deciduous_02`, `dead_tree_02`, `pine_03`, `young_deciduous_02`, `young_pine_02`, `dead_tree_young_01`, `shrub_03`, `shrub_yellow_01`, `fern_03`, `reeds_01`, `reeds_02`, `flowers_blue_01`, `flowers_red_01`.
- Farm: `barbed_fence_01`, `gate_01`, `wood_gate_02`, `crop_rows_green_01`, `crop_rows_mixed_01`, `corn_rows_01`, `corn_rows_02`, `wheat_patch_01`, `hay_bale_round_01`, `hay_bale_square_01`.
- Roadside: `stop_sign_01`, `speed_sign_55_01`, `warning_sign_01`, `curve_sign_01`, `utility_pole_01`, `street_light_01`, `concrete_barrier_01`.
- Industrial/logging/story props: `crate_01`, `road_barrier_01`, `barrels_01`, `log_pile_03`, `concrete_pipes_01`, `watchtower_01`, `abandoned_pickup_01`, `scrap_pile_01`, `corrugated_shed_01`, `ruined_shed_01`, `timber_stack_03`.

Rejected from sheet 02:

- Snow ground tiles: inconsistent with the present county season.
- Merged multi-object source components whose alpha islands overlap adjacent props: rejected rather than shipping contaminated crops.
- Tiny individual weeds, stones, sticks, cones, tires and debris fragments: visually negligible at gameplay zoom and better represented within larger clusters.
- Repeated near-identical flowers and shrubs: retained a representative set to avoid noisy uniform scattering.
- Loose masonry fragments that are not readable independently: retained the coherent walls/cliffs and rejected fragments.

Currently integrated from sheet 02:

- Rail stamps and a continuous historical Mill Creek freight/logging corridor.
- Creek rapids, river/dam-outflow foam details and a reed-lined pond.
- Pine, birch, deciduous, young-tree and dead-tree variation in biome-aware forests.
- Farm crop rows, corn, hay, hedges, fencing and gates.
- Stop sign and utility pole at believable road locations.
- Mill logging shed, scrap, timber and rock remnants.
- Ashwood abandoned pickup and road barrier.
- Fire Lookout watchtower landmark.

## Extraction quality

Every retained derivative preserves source alpha, is cropped to its nontransparent bounds with a two-pixel antialiasing margin, and was checked for transparent corners and neighboring-object contamination. The canonical sheet 01 remains untouched.
