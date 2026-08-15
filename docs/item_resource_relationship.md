# Physical Items vs. Settlement Resources

Ashwood County has two parallel economies now: the four bulk settlement
resources (Wood, Food, Materials, Medicine — `AshwoodCounty.Resources.
ResourceType`) and physical, itemized objects (`AshwoodCounty.Items.
ItemDefinition`). This note is the authoritative rule for how they meet, so
neither system silently double-counts or destroys something interesting.

## The rule

Every `ItemDefinition` optionally carries a `ResourceRelationship(ResourceType
Type, int Amount)`.

- **Set** (food, medical consumables, raw hardware/scrap/lumber, and small
  consumable supplies like duct tape or batteries): depositing the item into
  `SettlementItemStorage` immediately converts it into `Amount * quantity` of
  the named bulk resource. The item stops existing as an item at that point —
  it was never "interesting" on its own, only as a stand-in for fungible
  value, and the existing Wood/Food/Materials/Medicine economy (hunger,
  construction costs, treatment) already knows how to consume it. This is why
  depositing 2 Canned Beans adds Food, not two `ItemStack` rows sitting next
  to the Food counter — that would be the same value counted twice.
- **Null** (real tools, melee weapons, and equipment/backpacks): the item is
  never converted. It sits in `SettlementItemStorage.Items` as a genuine,
  distinct stored item indefinitely, exactly as carried. A Hammer deposited
  at the settlement is still a Hammer a survivor can pick back up and equip
  later — it is not junk value.

## Why the split lands where it does

- Wood specifically only ever comes from harvesting trees (`HarvestResourceOrder`
  / `ScavengeOrder`, unchanged by this work) — no item in the new catalog maps
  to Wood, so that resource's only source stays exactly what it was.
- Food and Medicine map naturally: canned goods, drinks and snacks *are* what
  the existing Food resource represents; bandages and medicine *are* what the
  existing Medicine resource represents. Converting on deposit means
  `EatOrder`/`TreatOrder` and every other system that already spends those
  resources needed zero changes.
- Materials absorbs raw hardware (nails, screws, scrap metal, wood planks,
  wire, gears) and a handful of consumable tool-adjacent supplies (duct tape,
  electrical tape, rope, zip ties, batteries, tarp, fuel can) — junk/supply
  value with no reason to occupy a permanent inventory slot once it reaches
  the settlement.
- Proper reusable tools (hammer, screwdriver, wrench, pliers, multitool,
  flashlight, tool kit), all twelve melee weapons, and all eight backpacks/
  bags stay real items forever. These are the objects a survivor actually
  wants back — equips, carries, or hands to someone else — so collapsing them
  into a number would throw away exactly the gameplay this vertical slice
  was built to add.

## Where this is enforced

`SettlementItemStorage.Deposit(itemId, quantity)` (`scripts/systems/
resources/SettlementItemStorage.cs`) is the single place this rule runs. It
is the only path from a survivor's carried `ItemStack` into settlement-level
state, so there is exactly one place double-accounting could ever be
introduced, and exactly one place to check.
