# World presentation

How Ashwood County's landscape is put on screen, and where to change it.

Everything here is deterministic: a county grid coordinate always produces the
same ground, the same road wear and the same tree. There is no world seed and no
saved terrain state, so chunks can be built, freed and rebuilt freely.

## Layers, bottom to top

| Z | Node | What it draws |
| --- | --- | --- |
| -120 | `CountyGroundChunk` | One quad per county chunk sampling the baked macro colour bitmap `assets/art/terrain/county_ground.png`. Always present, county-wide, ~120 quads total. |
| -118 | `CountyGroundDetailChunk` | Authored isometric ground diamonds on a two-cell lattice, plus sparse detail scatter. Built only for visible chunks. |
| -100 | `CountyVisualChunk` | Waterways, roads, railway, field structure, vegetation, clutter, authored dressing. Built only for visible chunks. |
| -88 | `CountyWaterLayer` | Animated lake, river, creek and pond surfaces (shader materials). |
| — | `CountyAuthoredStructuresLayer` | Districts, town blocks and landmark structures. |

Actors (survivors, zombies, buildings) render on the world's normal foreground
layer above all of this.

## The single source of truth

`CountyTerrain` is a static, allocation-free description of the county surface.
Ground tiling, vegetation and road dressing all query it, so they cannot
disagree about where a road, a yard or a waterline is.

- `BiomeAt(point)` — regional identity (`CountyBiome`).
- `SurfaceAt(point)` — the painted ground material (`GroundSurface`).
- `VegetationSuppression(point)` — 0 in open country, 1 where plants must not grow.
- `ClearingInfluence(point)` — inhabited, cleared ground around settlements.
- `AllRoads` — every road polyline, macro and local, with cached bounding boxes.
- `Fields` / `StateOfField(index)` — cultivated plots and their seasonal state.

`SurfaceAt` resolves in priority order: water margin, then road verge, then
inhabited clearing, then the regional default broken up by clustered
fertility/moisture noise. That ordering is what makes human traffic override
nature rather than the other way round.

## Ground diamonds

`GroundTilePalette` maps each `GroundSurface` to weighted variants of the
project's isometric ground artwork under `assets/art/terrain/`.

Two rules matter when editing it:

1. **Base variants must be full diamonds.** Several tiles in the library
   (`leaves_01`, `leaf_litter_02`, every `*_scatter_*`) only cover 40–65% of the
   diamond. Used as a base they punch holes through to the macro ground. They
   belong in the detail families instead.
2. **Weights are uneven on purpose.** Each surface has a dominant look with
   occasional relief. Equal weights read as a shuffled tile grid.

Diamonds are drawn at a two-cell pitch, oversized by `Bleed`, with position and
scale jitter so the lattice does not show, and tinted towards
`CountyTerrain.RegionColor` so tiles from different sources read as one place.

## Roads

`RoadSurfacePalette` gives each road class a surface and shoulder material from
`assets/art/roads/materials/` plus a wear stamp from
`assets/art/terrain/roads/`.

Those material textures include their own grass verges, so each profile selects
a V band covering just the carriageway — the ground layer already paints a
proper verge. Ribbons are mitred, UV-mapped, and drawn in grid-space widths so
they foreshorten with the projection. The shoulder pass fades to zero alpha at
its outer edge, which is what dissolves a road into the terrain instead of
ending it on a hard line.

`GridWidthScale` converts the authored canvas-flavoured half widths into grid
cells; changing it rescales the whole network.

## Vegetation

Placement comes from a clustered canopy field, not a flat per-cell probability:

```
canopy = baseDensity(biome) * (0.45 + fbm * 1.25) * (1 - suppression)
```

That yields closed woodland where the mass noise is high, thinning edges where
it is mid-range, and real clearings where it is low or where suppression from
roads, yards, fields and water pushes plants out.

Sizes are specified as **canvas heights**, not raw scales
(`MatureTreeHeight` and friends). The art library mixes resolutions badly —
`pine_01` is 288×491 while `pine_02` is 94×175 — so a shared scale factor would
produce trees differing by a factor of three.

## Authored content

`StartingAreaComposition` is explicit hand placement for the starting slice
(camp, family home, cabin terrace, lane frontage, woodland pocket). It is the
quality benchmark the rest of the county grows towards; procedural rules fill in
around it rather than replacing it.

## Streaming and cost

`VisibleChunkTracker` computes the chunks intersecting the camera's visible
rectangle plus a ring of margin. Both terrain layers build only those and free
the rest. Zooming far out returns nothing, which is the level-of-detail cut: the
baked macro ground still covers the county. Both layers run with
`ProcessMode.Always` because pausing is implemented as `GetTree().Paused` and
panning while paused must still build terrain.

Standing art is emitted in two passes. Low growth (ferns, flowers, rubble) is
grouped by texture with no depth sort, because it never meaningfully overlaps.
Tall art (trees, props, buildings) is strictly depth sorted. This keeps the
batching cost of density down without visible ordering errors.

## Capture and measurement

```
ASHWOOD_CAPTURE_LOCATION=camp ASHWOOD_CAPTURE_PNG=<abs path> godot --path . --quit-after 600
```

- `ASHWOOD_CAPTURE_HOUR=23` sets the wall clock, for night captures.
- Every capture prints `RENDER_COST: fps=… nodes=… objects=… draw_calls=…`.
- `StrategyCamera.SnapTo` is used rather than `CenterOnGridPosition`, because the
  camera smooths and would otherwise still be travelling when the frame is taken.

Location names are listed in `ContinuousWorldValidation.CaptureLocations`.
