# Ashwood County — Visual & Asset Guide

## 1. Purpose

This document defines the visual language and technical asset standards for Ashwood County.

It exists to ensure that terrain, buildings, vegetation, survivors, zombies, vehicles, props, resources, effects, and UI created at different times still look like they belong in the same game.

This document should be followed when:

- generating artwork with AI
- sourcing external assets
- creating project-owned artwork
- implementing visual systems
- replacing placeholder graphics
- reviewing visual consistency

Primary visual reference:

`docs/planning/design/images/initial_concept.png`

The concept image establishes the overall mood and presentation, but it should not be copied literally. Gameplay readability and production feasibility take priority over tiny visual details.

---

# 2. Overall Art Direction

Ashwood County uses:

**Bright stylized realism with a modern isometric strategy-game presentation.**

The world should feel grounded and believable without attempting photorealism.

The intended visual balance is:

- realistic enough to create a convincing world
- stylized enough to remain readable from strategy-game camera distances
- detailed enough to reward zooming in
- simple enough that objects remain recognizable when zoomed out

The game should NOT look:

- pixel-art
- cartoonishly exaggerated
- hyper-realistic
- excessively gritty
- permanently dark
- heavily desaturated
- uniformly brown or grey
- visually cluttered

The apocalypse should come from the CONTENT of the world:

- zombies
- abandoned vehicles
- barricades
- damaged structures
- overgrown areas
- debris
- improvised survivor settlements
- ruined infrastructure
- environmental storytelling

Do not communicate "apocalypse" simply by making everything dark and brown.

---

# 3. Mood

Ashwood County should often be beautiful.

A zombie apocalypse does not mean sunlight, vegetation, weather, colour, and natural beauty disappeared.

Normal daytime scenes should contain:

- bright natural daylight
- green vegetation
- blue skies where visible
- warm sunlight
- readable shadows
- colourful but grounded materials
- strong visual separation

This contrast is intentional.

A peaceful-looking summer afternoon can contain a dangerous zombie-infested town.

That contrast is part of Ashwood County's identity.

Night, storms, fog, fires, hordes, and dangerous locations may create darker scenes dynamically.

The base art itself should not permanently bake the entire game into a gloomy appearance.

---

# 4. Camera and Projection

Ashwood County uses a fixed isometric camera orientation.

The player may:

- pan
- zoom

The player may NOT rotate or orbit the camera.

This is an intentional art and gameplay constraint.

All world artwork therefore targets ONE canonical viewing direction.

## Projection

The current logical isometric projection uses a 2:1 relationship.

Reference logical tile dimensions:

- Width: 96
- Height: 48

This logical grid is primarily an internal spatial/reference system.

The final game world is NOT intended to visually feel tile-locked.

Buildings and units may use continuous world positions.

The grid may be used internally for:

- spatial queries
- broad-phase occupancy
- pathfinding support
- resource lookup
- debugging
- editor tooling

Permanent grid lines should not normally be visible during gameplay.

---

# 5. Continuous World Philosophy

Although the game uses isometric projection, the world should feel continuous and organic.

Avoid making settlements look like rigid board-game layouts.

The intended long-term presentation supports:

- free survivor movement
- continuous building positioning
- organic object placement
- naturally scattered vegetation
- freeform roads
- freeform walls and fences
- irregular settlement layouts

Objects should not appear artificially centered on visible tiles unless there is a gameplay reason.

---

# 6. Asset Perspective

Every world asset must use the same apparent camera perspective.

Buildings, props, trees, vehicles, survivors, and zombies must appear as though they are being viewed by the same camera.

Do not mix:

- different isometric angles
- noticeably different camera elevations
- orthographic-looking assets with perspective-heavy assets
- front-facing assets with isometric assets

Perspective consistency is more important than individual asset detail.

An individually beautiful asset that does not match the game's perspective should not be used.

---

# 7. Visual Detail

Target:

**Medium detail with strong silhouettes.**

At normal gameplay zoom, the player should immediately recognize:

- survivor
- zombie
- tree
- shelter
- clinic
- workshop
- vehicle
- stockpile
- resource
- barricade

Do not rely on tiny details to communicate an object's identity.

Use:

- recognizable shapes
- clear roofs
- readable doors
- strong object silhouettes
- distinctive major materials
- sensible colour variation

Avoid excessive micro-detail.

A building should still read correctly when displayed substantially smaller than its source image.

---

# 8. Colour

Use a natural but appealing palette.

Preferred environmental colours include:

- lush greens
- earthy browns
- warm timber
- natural stone
- faded painted surfaces
- muted industrial colours
- warm sunlight
- cool shadow tones

Colours should have enough saturation to prevent the world becoming muddy.

Avoid:

- extreme saturation
- universal grey filters
- excessive brown grading
- crushed blacks
- excessive contrast

Important gameplay objects should remain visually separable from terrain.

---

# 9. Lighting Direction

Static/generated world artwork should assume one consistent primary daylight direction.

Default:

**Sunlight from the upper-left / north-west visual direction.**

This means highlights and baked form shading should remain broadly consistent between assets.

Do not generate one building lit from the left and another strongly lit from the right.

Lighting baked into sprites should remain relatively soft.

Avoid extremely strong baked shadows because Godot may later add:

- dynamic lighting
- weather
- day/night tinting
- ambient effects
- additional shadows

Assets must remain usable under different environmental conditions.

---

# 10. Shadows

Where practical, separate major ground shadows from the core sprite.

Preferred structure:

asset.png
asset_shadow.png

This provides greater control over:

- opacity
- day/night
- weather
- lighting direction
- visual tuning

However, subtle ambient contact shading may remain baked into the base asset when necessary.

Do not bake enormous dark ground shadows permanently into every sprite.

---

# 11. Asset Format

Final raster world artwork should normally use:

**PNG with transparent background.**

Do not include:

- white backgrounds
- generated scenery backgrounds
- text labels
- UI elements
- decorative borders

unless specifically required by that asset type.

Maintain clean transparency around the object.

---

# 12. Source Resolution

Do NOT generate artwork directly at logical tile resolution.

Artwork should be produced at higher resolution and scaled appropriately in Godot.

Suggested starting source sizes:

### Small props

256×256 to 512×512

Examples:

- crate
- barrel
- wood pile
- toolbox
- road sign

### Vegetation

512×512 or larger where appropriate.

Examples:

- trees
- large bushes
- dead trees

### Small buildings

512×512 to 1024×1024

### Medium / large buildings

1024×1024 or larger where required.

These are guidelines rather than strict dimensions.

Preserving consistent WORLD SCALE is more important than forcing every asset into the same image dimensions.

---

# 13. Ground Anchor Standard

Every world object must have a predictable ground anchor.

The object's Node2D/world position represents its logical contact position with the ground.

For buildings this should normally correspond to a defined footprint reference point.

For characters:

the ground point between/below the feet.

For trees:

the base of the trunk.

For props:

the center/base of the object's ground contact area where appropriate.

Y-sorting must use ground position rather than the visual center of the image.

Never position sprites based purely on the center of their PNG canvas.

---

# 14. World Scale

Asset scale must remain consistent.

A survivor establishes a useful human-scale reference.

Approximate visual relationships should remain believable:

- doors slightly taller than survivors
- vehicles appropriately sized relative to people
- houses significantly larger than survivors
- mature trees taller than houses where appropriate
- crates/barrels clearly smaller than people

Do not independently eyeball every asset's scale.

Once the first approved survivor, tree, vehicle, and building exist, treat them as scale references for future artwork.

---

# 15. Buildings

Buildings should immediately communicate their function.

Examples:

Shelter:
- improvised residential structure
- modest size
- survival modifications

Workshop:
- tools
- work area
- industrial/storage characteristics

Clinic:
- recognizable medical cues
- clean functional shape

Warehouse:
- large footprint
- loading/storage characteristics

Avoid making every building a generic house with different decorations.

## Building Footprints

Building artwork must correspond to a defined ground footprint.

The visual may extend beyond the footprint vertically because of:

- walls
- roofs
- signs
- chimneys
- antennas

Placement/collision should use the logical ground footprint rather than opaque sprite pixels.

---

# 16. Building Orientation

The camera itself remains fixed.

For the initial production phase, buildings may use one canonical visual orientation.

The architecture should permit discrete alternate orientations in the future if required.

Do NOT simply rotate an isometric building PNG arbitrarily.

Doing so produces incorrect perspective.

If alternate building orientations are eventually required, they should use separately authored/rendered views.

---

# 17. Construction States

Buildings may eventually have visual construction stages.

Preferred concept:

Stage 1 — foundation

Stage 2 — structural frame

Stage 3 — partial walls/roof

Stage 4 — completed building

These should represent the SAME building and footprint.

Construction variants must preserve:

- perspective
- scale
- anchor
- footprint
- architectural identity

Do not generate construction stages that appear to be different buildings.

---

# 18. Vegetation

Vegetation is critical to Ashwood County's visual identity.

Trees should have:

- readable trunk/base
- clear silhouette
- natural but controlled canopy
- moderate detail
- consistent lighting
- transparent background

Avoid rendering every individual leaf.

Create variations rather than repeating one identical tree hundreds of times.

Example library:

oak_01
oak_02
oak_03
pine_01
pine_02
dead_tree_01
young_tree_01

Variations should still clearly belong to the same visual style.

---

# 19. Terrain

Terrain should NOT ultimately be represented by one enormous static image.

Use reusable terrain layers/tiles/textures with variation.

Expected terrain types eventually include:

- healthy grass
- dry grass
- dirt
- mud
- forest floor
- farmland
- damaged ground
- roads
- paths

Terrain should contain subtle variation so large areas do not appear perfectly uniform.

Avoid obvious repeating patterns.

Avoid excessive terrain detail that competes with units/buildings.

---

# 20. Roads

Future roads should visually support organic settlement layouts.

The eventual system should support curved/freeform paths rather than forcing every road onto rigid tile directions.

Road artwork should blend naturally into surrounding terrain.

Expected types may include:

- dirt paths
- gravel roads
- damaged asphalt
- surviving county roads
- improvised settlement paths

Road implementation is outside the scope of this document.

---

# 21. Survivors

Survivors must be readable at normal gameplay zoom.

Character artwork should emphasize:

- silhouette
- clothing colour
- equipment
- posture
- role-identifying features where useful

Do not attempt tiny facial detail that cannot be seen during gameplay.

Characters should remain grounded and proportionate rather than exaggerated cartoon figures.

Final character animation pipeline will be defined separately.

Until that pipeline is proven, placeholder survivors are acceptable.

---

# 22. Zombies

Zombies must visually differ from survivors immediately.

Use:

- posture
- movement
- damaged/dirty clothing
- silhouette
- colour/value differences

Do not depend on graphic gore for readability.

Zombie variants should eventually create population diversity without destroying visual consistency.

---

# 23. Props

Props should reinforce environmental storytelling and gameplay function.

Examples:

- timber stacks
- crates
- barrels
- pallets
- tools
- shopping carts
- generators
- abandoned furniture
- road signs
- barricades
- rubbish
- survivor supplies

Avoid filling every available space with props.

Readable negative space is important for:

- navigation
- combat
- selection
- visual clarity

---

# 24. Vehicles

Vehicles should use the same fixed isometric perspective.

They should have believable scale relative to survivors and buildings.

Future variations may include:

- intact
- abandoned
- damaged
- burned-out
- survivor-owned

Do not rotate a single vehicle sprite arbitrarily if doing so breaks perspective.

Directional variants may be required later.

---

# 25. Environmental Storytelling

Use environmental storytelling with restraint.

A location should communicate its history through a few meaningful elements rather than dozens of random objects.

Examples:

- abandoned evacuation vehicle
- improvised barricade
- half-finished construction
- survivor grave
- looted storefront
- abandoned campsite
- collapsed fence
- emergency supplies

Avoid visual noise for its own sake.

---

# 26. Visual Readability Hierarchy

At normal gameplay zoom, visual importance should roughly follow:

1. Selected units / immediate threats
2. Survivors and zombies
3. Buildings and interactable resources
4. Roads and major terrain features
5. Decorative vegetation and props
6. Minor ground detail

Decoration must never make important gameplay information difficult to read.

---

# 27. Selection and Gameplay Feedback

Selection indicators, build previews, interaction markers, health/status indicators, and resource feedback should remain visually distinct from world artwork.

Avoid permanently baking gameplay indicators into sprites.

These should generally be rendered separately by Godot.

---

# 28. Grid Presentation

The logical isometric grid is primarily a gameplay implementation detail.

Normal gameplay:

**grid hidden**

Debug/editor:

**grid may be visible**

Special placement modes may optionally display subtle placement information where useful.

Do not make the entire final game permanently look like a board of diamond tiles.

---

# 29. AI-Generated Asset Rules

When generating artwork with AI, prompts must specify the established Ashwood visual standard.

Every generation request should reinforce:

- fixed isometric view
- same camera elevation
- stylized realism
- bright natural daylight
- consistent upper-left lighting
- medium detail
- strong readable silhouette
- transparent background
- no text unless explicitly required
- no people unless explicitly required
- no surrounding scene unless explicitly required
- appropriate Ashwood County rural/small-town American visual identity

Do not accept an asset simply because it looks attractive in isolation.

Evaluate:

1. perspective consistency
2. scale
3. lighting
4. silhouette
5. style
6. transparency
7. gameplay readability
8. compatibility with existing approved assets

Consistency is more important than maximizing detail.

---

# 30. External Asset Rules

External assets may be used according to AGENTS.md.

Before integrating an external visual asset, evaluate whether it matches:

- perspective
- style
- scale
- lighting
- quality
- licence requirements

An external asset should not be used merely because it is free.

Minor adaptation may be acceptable when licensing permits it, but avoid building the visual identity from many obviously unrelated packs.

---

# 31. Approved Reference Assets

As production continues, successful assets should be added to an approved reference set.

Initially this should eventually contain:

- one survivor
- one mature tree
- one Shelter
- one vehicle
- one resource pile

These become scale/style references for everything created afterward.

Once approved, new assets should be visually compared against them.

---

# 32. First Art Milestone

Do NOT immediately replace every placeholder.

The first production-art test should contain only:

1. terrain/grass
2. one tree type
3. wood/resource pile
4. stockpile
5. completed Shelter

Integrate those assets into the existing playable scene.

Then evaluate them together at:

- normal gameplay zoom
- maximum intended zoom-in
- useful zoom-out

Only expand asset production after this small set proves that:

- perspective matches
- scale matches
- colours work together
- gameplay remains readable
- the style looks attractive in the actual game

Do not judge assets only from isolated PNG previews.

The game viewport is the final test.