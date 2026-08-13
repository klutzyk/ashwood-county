# Source Sheets Audit

All six PNG source sheets present in `assets/art/sheets/` were audited at their
native 1536×1024 resolution. Source sheets were not modified, overwritten, or
used directly at runtime. Approved derivatives preserve the original RGBA
pixels, isolate the selected foreground alpha component, retain its antialiased
edge, add three transparent pixels of crop padding, and use semantic filenames.

The deterministic extraction recipe is
`tools/source_sheet_audit/extract_source_sheets.py`. It produced **84 approved
source components** and **36 byte-identical semantic aliases** used by the
county authoring layer. Aliases do not count as additional source artwork.

## 1. `houses.png`

Eight of eight illustrated residences were retained:

- `assets/art/buildings/residential/house_01.png`
- `assets/art/buildings/residential/house_02.png`
- `assets/art/buildings/residential/house_03.png`
- `assets/art/buildings/residential/house_04.png`
- `assets/art/buildings/residential/house_05.png`
- `assets/art/buildings/residential/house_06.png`
- `assets/art/buildings/residential/house_07.png`
- `assets/art/buildings/residential/house_08.png`

Usage: Ashwood detached-house neighborhoods, with variants also suitable for
Farm District homesteads and rural properties. These are gameplay-scale visual
structures; they do not make the buildings enterable or add gameplay logic.

Rejected: none. All eight silhouettes are independent, transparent, visually
coherent, and contain no signage.

## 2. `houses (abandoned).png`

Eight of eight abandoned residences were retained:

- `assets/art/buildings/residential/abandoned_house_01.png`
- `assets/art/buildings/residential/abandoned_house_02.png`
- `assets/art/buildings/residential/abandoned_house_03.png`
- `assets/art/buildings/residential/abandoned_house_04.png`
- `assets/art/buildings/residential/abandoned_house_05.png`
- `assets/art/buildings/residential/abandoned_house_06.png`
- `assets/art/buildings/residential/abandoned_house_07.png`
- `assets/art/buildings/residential/abandoned_house_08.png`

Usage: selectively overgrown or damaged Ashwood lots, plus isolated abandoned
rural homes. These are gameplay-scale visual structures.

Rejected: none. The source contains low-alpha haze bridges between some
neighbours; those bridges were excluded by seeded foreground-component
extraction. The underlying eight house silhouettes remain complete and clean.

## 3. `rural_structures.png`

All thirteen illustrated compositions were retained:

- Buildings: `outhouse_01`, `outhouse_02`, `garden_shed_01`, `tool_shed_01`,
  `wood_shelter_01`, `farm_shelter_01`, `work_cabin_01`, `small_cabin_01`,
  `greenhouse_01`, and `trailer_01` under `assets/art/buildings/rural/`.
- Rural props: `garden_plot_01`, `laundry_yard_01`, and `mailbox_tree_01`
  under `assets/art/props/rural/`.

Usage: Farm District and South Farmland yards, Outskirts storage, Mill Creek
and Logging Camp cabins/sheds, Trailer Park lots, and residential backyards.
Buildings are gameplay-scale visual structures; yard compositions are
decoration.

Rejected: none. The small stray source-sheet specks around components are not
part of the selected alpha components and were not exported.

## 4. `urban props.png`

All twenty-two useful illustrated components were retained:

- Lighting/utilities: `street_light_01`, `banner_lamp_01`,
  `utility_pole_01`, `traffic_light_01`, `hvac_unit_01`, and
  `electrical_cabinet_01`.
- Street furniture/services: `bus_shelter_01`, `mailbox_01`, `bench_01`,
  `fire_hydrant_01`, `bicycle_rack_01`, `trash_bin_01`, `dumpster_01`, and
  `trash_pile_01`.
- Civic/roadside: `road_sign_01`, `county_planter_01`, `newspaper_box_01`,
  `road_barrier_01`, `traffic_barrel_01`, `traffic_cone_01`,
  `chainlink_fence_01`, and `graffiti_barrier_01`.

All paths are under `assets/art/props/urban/`. Usage: Ashwood streets and civic
blocks, hospital and sheriff grounds, service/loading areas, intersections,
parking lots, and deliberate Highway 16 obstruction clusters. These are
decoration and infrastructure visuals only.

Text review:

- Retained: `Main St`, `Pine Ave`, `Ashwood`, `Ashwood County`, `U.S. Mail`,
  `Stay Safe`, and common safety labels. They are readable and contextually
  appropriate.
- Rejected as a unique role: the newspaper’s tiny body copy is not legible at
  gameplay zoom and is treated only as texture detail; no claim is made that
  its article body is meaningful.
- No malformed AI shop or building signage was extracted.

Rejected: none of the 22 coherent components. Tiny disconnected source specks
and clipped edge fragments were excluded automatically.

## 5. `landmarks.png`

All nine landmark compositions were retained under
`assets/art/props/landmarks/`:

- `welcome_sign_01.png`
- `fire_lookout_tower_01.png`
- `picnic_table_01.png`
- `campfire_01.png`
- `trail_board_01.png`
- `communications_tower_01.png`
- `ruined_cabin_01.png`
- `ridge_viewpoint_01.png`
- `dock_rowboat_01.png`

Usage: Highway 16 county entry, Fire Lookout, Blackwater shore access, Pine
Ridge trails/viewpoint, wilderness camps, and communications infrastructure.
These are unique or sparsely repeated decoration/landmark visuals.

Text review: `Welcome to Ashwood County`, `Ashwood County Trails`, and `Pine
Ridge Lookout` are readable and geographically correct. Fine-print map/rules
content is not relied upon for gameplay. No malformed text was knowingly kept
as world signage.

Rejected: none. The dock and rowboat are a single deliberately integrated
shoreline composition; they were not falsely split into overlapping assets.

## 6. `vehicles (abandoned).png`

Twenty vehicles/coherent clusters were retained under
`assets/art/props/vehicles/`:

- `sedan_01`, `sedan_02`, `abandoned_sedan_01`, `abandoned_sedan_02`
- `suv_01`, `logging_suv_01`, `station_wagon_01`, `jeep_01`
- `pickup_01`, `pickup_02`
- `police_car_01`, `ambulance_01`
- `van_01`, `van_02`
- `school_bus_01`, `box_truck_01`, `motorcycle_01`
- `overturned_vehicle_01`, `utility_trailer_01`, `boat_01`

Four coherent road-debris clusters were retained under
`assets/art/props/roadside/`: `cones_barrier_01`, `rusty_barrel_01`,
`traffic_cone_02`, and `tire_pile_01`.

Usage: authored traffic/crash clusters along Highway 16; police and ambulance
context near the Sheriff and Hospital; work vehicles in farm/logging areas;
older vehicles around the Service Station and Trailer Park; the boat as
shoreline debris. All are static decoration and do not add vehicle gameplay.

Text review: the police, ambulance, school bus, and emergency symbols are
recognizable. The box truck contains an abstract graffiti mark; it is treated
as graffiti, not readable signage. No generated route or business text is
relied upon.

Rejected components:

- Individual loose tires, hubcaps, isolated cones, tiny pipes, and tiny debris
  fragments where they were not part of a coherent retained cluster: too small
  or visually noisy at normal gameplay zoom.
- A separate `boat`/`dock` split from any overlapping composition: rejected;
  complete coherent compositions were retained instead.

## Semantic aliases

Thirty-six byte-identical aliases support clear regional roles without
re-cropping or inventing variants. They include `suburban_house_01/02`,
`ranch_house_01`, `two_storey_house_01`, `house_abandoned_01`, rural
`shed_01`/`cabin_01`/`mobile_home_01`, vehicle-role aliases such as
`farm_pickup_01`, `work_pickup_01`, `logging_truck_01`, `sheriff_car_01`, and
landmark-role aliases such as `ashwood_welcome_sign_01`,
`trail_information_board_01`, `watchtower_01`, `dock_01`, and `rowboat_01`.
They exist to keep authored placements semantic and replaceable.

## Quality verification

- Visually inspected the six native source sheets.
- Visually inspected numbered contact sheets of all 84 selected source
  components.
- Visually inspected post-extraction contact sheets by semantic folder.
- Confirmed transparent RGBA output, compact bounds, preserved antialiasing,
  and no baked checkerboard/background.
- Source PNGs remain present and unchanged.
