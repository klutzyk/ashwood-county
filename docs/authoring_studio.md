# Ashwood County Authoring Studio

Launch `tools/launch_authoring_studio.ps1`, or run
`scenes/tools/AuthoringStudio.tscn` from Godot.

The Studio is a development scene. It is not added to the player's normal HUD.
It uses the game's `IsometricGrid`, county renderer, camera, textures, interior
visuals and navigation definitions.

## World workflow

1. Choose a named county location or click the minimap.
2. Choose a 1 chunk, 3 x 3 chunk or 5 x 5 chunk editing window.
3. Filter/search the thumbnail library.
4. Choose gameplay type and an asset, then click to place.
5. Select, drag, box-select, duplicate or delete objects.
6. Use the inspector for exact position, scale, anchor, collision and gameplay
   metadata.
7. Save.

Building artwork placed with the Building gameplay type creates an editable
building definition rather than a decoration. Select it and choose
`EDIT INTERIOR`.

## Interior workflow

- `ROOM`: drag a room rectangle. The inspector controls its name and dimensions.
- `WALL`: drag a snapped horizontal or vertical wall. It creates the visual and
  navigation blocker together.
- `DOOR`: click a wall. The Studio splits a navigation opening and records the
  approach, interaction and arrival points.
- Interior art can be placed as furniture, a container or a bed. Large assets
  default to blocking; clutter defaults to non-blocking and can be overridden.
- Container presets (LOOT TABLE dropdown) roll real itemized loot -
  canned food, medical supplies, tools, materials, weapons and equipment -
  from curated per-archetype tables in `AshwoodCounty.Items.ItemLootPresets`.
  Consumable/junk items still contribute to the Food/Materials/Medicine
  economy once a survivor deposits them at a stockpile (see
  `docs/item_resource_relationship.md`); tools, weapons and equipment remain
  real stored items.
- The translucent exterior footprint and survivor-sized silhouette remain
  visible while editing.

Run `VALIDATE`, then `TEST ENTRANCE`. Red markers are invalid, yellow markers
are warnings, and green is valid. A usable exterior entrance is required before
the entrance test passes.

`PLAYTEST` saves and switches to the normal game scene. The survivor is staged
outside the authored entrance and all ordinary gameplay controls remain active.
Press F10 or the return button to reopen the Studio with the previous area and
interior restored.

## Shortcuts

- Delete: delete selection
- Ctrl+Z: undo
- Ctrl+Y: redo
- Ctrl+D: duplicate
- Esc: cancel placement/current drawing tool
- Middle mouse/WASD: pan camera
- Mouse wheel: zoom

## Spline road workflow

Open `PAINT`, choose one of the eight road profiles, set its width and press
`START DRAWING ROAD`. Click county-space control points and press Enter or
`FINISH PATH`. Highway, county and rural profiles use smoothed Catmull-Rom
sampling; town roads remain intentionally angular. Roads are saved as compact
control points, not placed sprite segments.

- Drag a selected gold control point to reshape a road.
- Ctrl-click a selected road segment to insert a control point.
- Select a control point and press Delete to remove it (a two-point road is
  protected; use E to delete the whole road).
- Select an endpoint, then use `EXTEND SELECTED ROAD` to continue its profile.
- Select an internal point and use `SPLIT AT SELECTED POINT` to create two
  connected authored roads.
- Double-click places the final point and finishes the road.
- Endpoint, existing-road and bridge-socket snapping uses the active profile's
  tolerance. Bridge snaps also create a short aligned approach control so the
  spline enters the fixed bridge deck along its axis.
- The inspector changes profile and width after placement.
- `SHOW ROAD GRAPH / SPLINE DEBUG` reveals spline samples, inferred graph nodes,
  tangents, profile/width labels, junction degree and bridge sockets.
- Validation results are clickable and center the relevant county location.

Normal runtime and the Studio use the same `RoadSplineGeometry`, road profiles,
graph and `AuthoredPathVisual`; there is no second editor-only road renderer.

Authored data is stored in
`data/authoring/ashwood_county.authored.json`. The normal runtime reads this
same file. World objects and building runtimes are instantiated only for active
county chunks; the minimap never instantiates county content.
