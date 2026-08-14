# Residential Interior Sheet Audit

`assets/art/sheets/interiors/residential_interior_kit_01.png` was audited at
its native 1536 x 1024 RGBA resolution. The source remains untouched and is not
used directly at runtime. The deterministic extraction recipe is
`tools/interior_sheet_audit/extract_residential_interior_kit.py`.

The approved set contains 62 components grouped under
`assets/art/interiors/residential/`: structural wall/door/window pieces,
flooring and rugs, living and dining furniture, bedroom furniture, kitchen,
bathroom and utility fixtures, and restrained household/abandonment clutter.

The reference image `residential_house_reference_01.png` is used only for
scale, room relationships and cutaway presentation; it is never rendered as a
flattened interior.

Rejected from this first production set:

- duplicate sofas, chairs, doors, windows and surface swatches that add no
  useful role to the reference home;
- stairs and bunk beds, because the first house is single-storey and should not
  imply inaccessible floors or excessive occupants;
- commercial counter, shopping cart and display freezer, which belong to a
  future shop interior rather than a residence;
- very small disconnected litter, cans, pills, nails, loose tools and scraps
  that would be visual noise at normal county zoom;
- merged debris clusters whose interaction footprint would be ambiguous;
- components carrying illegible generated writing as semantic content.

Every approved output preserves source antialiasing, has compact bounds and a
three-pixel transparent outer ring. No generated text is used for interaction
or gameplay meaning.
