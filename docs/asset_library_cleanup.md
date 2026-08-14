# Asset Library Cleanup — 2026-08-14

## Scope and results

- 14 source/reference sheets audited: the 11 PNGs under `assets/art/sheets/`,
  `isometric_asset_sheet.png`, `terrain_asset_sheet.png`, and
  `terrain_asset_sheet_02.png`.
- 338 authoring-eligible extracted PNGs audited with contact sheets and alpha
  sanity checks.
- 90 defective terrain-sheet-02 derivatives repaired at their existing stable
  resource paths. The other three sheet-02 derivatives were already isolated.
- 17 paths are explicitly classified as intentional compositions, including
  the pickup/scrap scene, shed work areas, dock/rowboat aliases, camp/work
  areas, and roadside barrier clusters.
- 4 misleading/unusable derivatives are rejected from the Authoring Studio
  catalog: `barbed_fence_01`, the sheet-02 `street_light_01`,
  `flowers_blue_01`, and `shrub_yellow_01`. Clean alternatives remain in the
  library. The underlying files are retained so existing runtime references
  are not broken.

## Source mapping

- `houses.png` and `houses (abandoned).png` → residential buildings.
- `rural_structures.png` → rural buildings and yard compositions.
- `urban props.png` → urban infrastructure and street props.
- `landmarks.png` → landmark and outdoor compositions.
- `vehicles (abandoned).png` → vehicles and roadside compositions.
- `residential_interior_kit_01.png` → residential interior kit.
- `terrain_asset_sheet.png` → first terrain/environment extraction family.
- `terrain_asset_sheet_02.png` → ground, road, rail, water, vegetation, farm,
  roadside, industrial, and logging families rebuilt by
  `tools/asset_library_audit/reextract_terrain_sheet_02.py`.
- `farm props.png`, `town_props.png`, `town_shops.png`, and the residential
  reference sheet remain original source/reference sheets; they are not used
  as direct runtime textures.
- `isometric_asset_sheet.png` is the original project foundation sheet for the
  small legacy environment/resource set.

## QA behavior

The Authoring Studio QA tab displays the selected asset against a neutral
checkerboard and shows its dimensions, source, category, standalone/composite
classification, suggested anchor/scale, and blocking default. “Needs Cleanup”
flags persist in `user://asset_qa_flags.cfg` and flagged thumbnails receive an
exclamation marker.

Thumbnail cache entries are keyed by the source file modification timestamp,
so corrected PNGs are reloaded rather than retaining stale thumbnails.

`tools/asset_library_audit/audit_asset_library.py` records warning-only alpha
heuristics in `docs/asset_library_sanity_report.json`. Warnings include many
islands, tiny detached islands, touched crop boundaries, extreme aspect ratio,
and excessive empty canvas. They are deliberately not automatic deletion
rules; trees, fences, terrain scatter, and intentional compositions commonly
trigger them.

## Visual inspection

Contact sheets were reviewed for buildings/houses/sheds, vehicles/wrecks,
road and utility props, farm props, fences/signs/debris, vegetation, terrain,
water, roads/rail, landmarks, environment/resources, and residential
interiors. Crate, pipes, barrels, industrial barrier, shed work area,
watchtower, logs, signs, roads, ponds, and terrain corners were also inspected
individually after rebuilding.

No source sheet was overwritten. Existing authored resource paths remain
valid. No artwork was AI-generated.
