#nullable enable

using System.Collections.Generic;
using AshwoodCounty.Resources;

namespace AshwoodCounty.Items;

/// <summary>
/// Data-driven item registry. Every item is one row below; no per-item
/// branching or switch statement. Extending the catalog (firearms, ammo,
/// clothing, quest items, keys, crafting components) means adding rows and,
/// if a genuinely new concept shows up, a new field/tag on ItemDefinition;
/// it never means adding new code paths per item.
/// </summary>
public static class ItemCatalog
{
    private static readonly ItemDefinition[] Rows =
    [
        // ---- Food (Category.Food, Usable, restores Hunger, deposits into Food resource) ----
        new("canned_beans", "Canned Beans", "A dented tin of slow-cooked beans.", ItemCategory.Food, .45f, 6, ["canned"], Usable: true, ResourceRelationship: new(ResourceType.Food, 2), NutritionValue: 22),
        new("canned_tomato_soup", "Tomato Soup", "Condensed soup, ready to heat or eat cold.", ItemCategory.Food, .45f, 6, ["canned"], Usable: true, ResourceRelationship: new(ResourceType.Food, 2), NutritionValue: 18),
        new("canned_tuna", "Canned Tuna", "A small tin of preserved tuna.", ItemCategory.Food, .2f, 8, ["canned"], Usable: true, ResourceRelationship: new(ResourceType.Food, 2), NutritionValue: 16),
        new("canned_spaghetti", "Canned Spaghetti", "Pasta in tomato sauce, shelf-stable.", ItemCategory.Food, .45f, 6, ["canned"], Usable: true, ResourceRelationship: new(ResourceType.Food, 2), NutritionValue: 20),
        new("canned_corn", "Canned Corn", "Sweet corn kernels in brine.", ItemCategory.Food, .4f, 6, ["canned"], Usable: true, ResourceRelationship: new(ResourceType.Food, 2), NutritionValue: 16),
        new("peanut_butter", "Peanut Butter", "A dense, calorie-rich jar spread.", ItemCategory.Food, .5f, 4, ["jar"], Usable: true, ResourceRelationship: new(ResourceType.Food, 3), NutritionValue: 26),
        new("bottled_water", "Bottled Water", "Sealed drinking water.", ItemCategory.Food, .6f, 6, ["drink"], Usable: true, ResourceRelationship: new(ResourceType.Food, 1), NutritionValue: 6),
        new("sports_drink", "Sports Drink", "Electrolyte drink, orange flavor.", ItemCategory.Food, .6f, 6, ["drink"], Usable: true, ResourceRelationship: new(ResourceType.Food, 1), NutritionValue: 10),
        new("apple_juice_box", "Apple Juice", "A small juice carton with a straw.", ItemCategory.Food, .3f, 6, ["drink"], Usable: true, ResourceRelationship: new(ResourceType.Food, 1), NutritionValue: 10),
        new("energy_bar", "Energy Bar", "A dense oat and grain bar.", ItemCategory.Food, .1f, 10, ["snack"], Usable: true, ResourceRelationship: new(ResourceType.Food, 1), NutritionValue: 14),
        new("saltine_crackers", "Saltine Crackers", "A box of plain crackers.", ItemCategory.Food, .2f, 8, ["snack"], Usable: true, ResourceRelationship: new(ResourceType.Food, 1), NutritionValue: 10),
        new("corn_flakes_box", "Corn Flakes", "Breakfast cereal, best with milk.", ItemCategory.Food, .5f, 4, ["boxed"], Usable: true, ResourceRelationship: new(ResourceType.Food, 2), NutritionValue: 18),
        new("instant_noodles", "Instant Noodles", "Chicken-flavor instant noodles.", ItemCategory.Food, .15f, 10, ["snack"], Usable: true, ResourceRelationship: new(ResourceType.Food, 1), NutritionValue: 16),
        new("beef_jerky", "Beef Jerky", "Dried, salted strips of beef.", ItemCategory.Food, .15f, 10, ["snack"], Usable: true, ResourceRelationship: new(ResourceType.Food, 1), NutritionValue: 14),

        // ---- Medical (Category.Medical, most Usable, restore Health, deposit into Medicine resource) ----
        new("bandage", "Bandage", "A rolled elastic bandage.", ItemCategory.Medical, .1f, 10, ["first_aid"], Usable: true, ResourceRelationship: new(ResourceType.Medicine, 1), HealValue: 12),
        new("gauze_pads", "Gauze Pads", "Sterile wound dressing pads.", ItemCategory.Medical, .1f, 10, ["first_aid"], Usable: true, ResourceRelationship: new(ResourceType.Medicine, 1), HealValue: 10),
        new("adhesive_bandage", "Adhesive Bandage", "A basic self-stick bandage.", ItemCategory.Medical, .02f, 20, ["first_aid"], Usable: true, ResourceRelationship: new(ResourceType.Medicine, 1), HealValue: 6),
        new("medical_tape", "Medical Tape", "Cloth tape for securing dressings.", ItemCategory.Medical, .1f, 10, ["first_aid", "supply"], ResourceRelationship: new(ResourceType.Medicine, 1)),
        new("antiseptic_solution", "Antiseptic Solution", "Wound cleaning solution.", ItemCategory.Medical, .15f, 6, ["first_aid"], Usable: true, ResourceRelationship: new(ResourceType.Medicine, 1), HealValue: 8),
        new("alcohol_wipes", "Alcohol Wipes", "Sterile disposable wipes.", ItemCategory.Medical, .05f, 15, ["first_aid"], Usable: true, ResourceRelationship: new(ResourceType.Medicine, 1), HealValue: 4),
        new("pain_relief_tablets", "Pain Relief Tablets", "Over-the-counter pain relief.", ItemCategory.Medical, .1f, 8, ["medicine"], Usable: true, ResourceRelationship: new(ResourceType.Medicine, 1), HealValue: 10),
        new("antibiotic_tablets", "Antibiotic Tablets", "Prescription-strength antibiotics.", ItemCategory.Medical, .1f, 8, ["medicine"], Usable: true, ResourceRelationship: new(ResourceType.Medicine, 2), HealValue: 14),
        new("cough_syrup", "Cough Syrup", "Relieves cough and congestion.", ItemCategory.Medical, .2f, 6, ["medicine"], Usable: true, ResourceRelationship: new(ResourceType.Medicine, 1), HealValue: 8),
        new("multivitamin_tablets", "Multivitamin Tablets", "General health supplement.", ItemCategory.Medical, .1f, 8, ["medicine"], Usable: true, ResourceRelationship: new(ResourceType.Medicine, 1), HealValue: 6),
        new("burn_gel", "Burn Gel", "Cooling gel for burns.", ItemCategory.Medical, .1f, 8, ["first_aid"], Usable: true, ResourceRelationship: new(ResourceType.Medicine, 1), HealValue: 10),
        new("thermometer", "Thermometer", "A digital medical thermometer.", ItemCategory.Medical, .05f, 3, ["gear"]),
        new("disposable_gloves", "Disposable Gloves", "A pair of nitrile gloves.", ItemCategory.Medical, .02f, 20, ["supply"], ResourceRelationship: new(ResourceType.Medicine, 1)),
        new("tourniquet", "Tourniquet", "Emergency bleeding-control strap.", ItemCategory.Medical, .15f, 5, ["gear"]),
        new("first_aid_kit", "First Aid Kit", "A stocked emergency medical kit.", ItemCategory.Medical, .8f, 2, ["kit"], Usable: true, ResourceRelationship: new(ResourceType.Medicine, 4), HealValue: 35),

        // ---- Tools (Category.Tool; reusable tools stay physical items; small consumable supplies deposit into Materials) ----
        new("hammer", "Hammer", "A claw hammer.", ItemCategory.Tool, 1.1f, 1, ["tool"]),
        new("screwdriver", "Screwdriver", "A flathead/Phillips screwdriver.", ItemCategory.Tool, .15f, 2, ["tool"]),
        new("wrench", "Wrench", "An adjustable wrench.", ItemCategory.Tool, .6f, 1, ["tool"]),
        new("pliers", "Pliers", "A pair of gripping pliers.", ItemCategory.Tool, .3f, 1, ["tool"]),
        new("multitool", "Multitool", "A folding multitool with several blades.", ItemCategory.Tool, .25f, 1, ["tool"]),
        new("duct_tape", "Duct Tape", "A roll of heavy-duty tape.", ItemCategory.Tool, .2f, 3, ["supply"], ResourceRelationship: new(ResourceType.Materials, 1)),
        new("electrical_tape", "Electrical Tape", "A roll of insulating tape.", ItemCategory.Tool, .15f, 3, ["supply"], ResourceRelationship: new(ResourceType.Materials, 1)),
        new("rope", "Rope", "A coil of sturdy rope.", ItemCategory.Tool, .8f, 2, ["supply"], ResourceRelationship: new(ResourceType.Materials, 1)),
        new("flashlight", "Flashlight", "A battery-powered flashlight.", ItemCategory.Tool, .3f, 1, ["tool"]),
        new("batteries", "Batteries", "A pack of household batteries.", ItemCategory.Tool, .15f, 6, ["supply"], ResourceRelationship: new(ResourceType.Materials, 1)),
        new("zip_ties", "Zip Ties", "A bundle of cable ties.", ItemCategory.Tool, .1f, 5, ["supply"], ResourceRelationship: new(ResourceType.Materials, 1)),
        new("tarp", "Tarp", "A folded waterproof tarp.", ItemCategory.Tool, .9f, 2, ["supply"], ResourceRelationship: new(ResourceType.Materials, 1)),
        new("fuel_can", "Fuel Can", "A metal can of fuel.", ItemCategory.Tool, 3.5f, 1, ["supply"], ResourceRelationship: new(ResourceType.Materials, 2)),
        new("tool_kit", "Tool Kit", "A stocked case of hand tools.", ItemCategory.Tool, 2.0f, 1, ["kit"]),

        // ---- Materials (Category.Material; bulk junk value, always deposits into Materials) ----
        new("nails", "Nails", "A handful of assorted nails.", ItemCategory.Material, .3f, 20, ["hardware"], ResourceRelationship: new(ResourceType.Materials, 1)),
        new("screws", "Screws", "A handful of assorted screws.", ItemCategory.Material, .2f, 20, ["hardware"], ResourceRelationship: new(ResourceType.Materials, 1)),
        new("scrap_metal", "Scrap Metal", "Assorted sheet and bar scrap.", ItemCategory.Material, 1.5f, 5, ["scrap"], ResourceRelationship: new(ResourceType.Materials, 2)),
        new("metal_pipes_bundle", "Metal Pipes", "A bundle of salvaged pipe stock.", ItemCategory.Material, 2.0f, 3, ["scrap"], ResourceRelationship: new(ResourceType.Materials, 2)),
        new("sheet_metal", "Sheet Metal", "Corrugated scrap sheeting.", ItemCategory.Material, 2.5f, 3, ["scrap"], ResourceRelationship: new(ResourceType.Materials, 2)),
        new("bolts_and_nuts", "Bolts & Nuts", "A handful of assorted hardware.", ItemCategory.Material, .3f, 20, ["hardware"], ResourceRelationship: new(ResourceType.Materials, 1)),
        new("gears", "Gears", "Salvaged mechanical gears.", ItemCategory.Material, .6f, 8, ["scrap"], ResourceRelationship: new(ResourceType.Materials, 1)),
        new("wire_coil", "Wire", "A coil of salvaged wire.", ItemCategory.Material, .8f, 5, ["scrap"], ResourceRelationship: new(ResourceType.Materials, 1)),
        new("wood_planks", "Wood Planks", "A stack of cut lumber.", ItemCategory.Material, 2.0f, 5, ["lumber"], ResourceRelationship: new(ResourceType.Materials, 2)),

        // ---- Melee weapons (Category.MeleeWeapon, Equippable in Weapon slot; DamageValue is a hook for the future combat system) ----
        new("baseball_bat", "Baseball Bat", "A wooden baseball bat.", ItemCategory.MeleeWeapon, 1.0f, 1, ["blunt"], Equippable: true, EquipmentSlot: EquipmentSlot.Weapon, DamageValue: 14),
        new("kitchen_knife", "Kitchen Knife", "A sharp kitchen knife.", ItemCategory.MeleeWeapon, .3f, 1, ["blade"], Equippable: true, EquipmentSlot: EquipmentSlot.Weapon, DamageValue: 10),
        new("hatchet", "Hatchet", "A short-handled axe.", ItemCategory.MeleeWeapon, 1.2f, 1, ["blade"], Equippable: true, EquipmentSlot: EquipmentSlot.Weapon, DamageValue: 18),
        new("crowbar", "Crowbar", "A heavy steel pry bar.", ItemCategory.MeleeWeapon, 2.5f, 1, ["blunt"], Equippable: true, EquipmentSlot: EquipmentSlot.Weapon, DamageValue: 16),
        new("metal_pipe", "Metal Pipe", "A single length of pipe.", ItemCategory.MeleeWeapon, 1.8f, 1, ["blunt"], Equippable: true, EquipmentSlot: EquipmentSlot.Weapon, DamageValue: 15),
        new("sledgehammer", "Sledgehammer", "A heavy two-handed hammer.", ItemCategory.MeleeWeapon, 4.5f, 1, ["blunt"], Equippable: true, EquipmentSlot: EquipmentSlot.Weapon, DamageValue: 28),
        new("machete", "Machete", "A long single-edge blade.", ItemCategory.MeleeWeapon, .8f, 1, ["blade"], Equippable: true, EquipmentSlot: EquipmentSlot.Weapon, DamageValue: 17),
        new("spiked_bat", "Spiked Bat", "A bat studded with nails.", ItemCategory.MeleeWeapon, 1.3f, 1, ["blunt"], Equippable: true, EquipmentSlot: EquipmentSlot.Weapon, DamageValue: 20),
        new("shovel", "Shovel", "A digging shovel.", ItemCategory.MeleeWeapon, 2.2f, 1, ["blunt"], Equippable: true, EquipmentSlot: EquipmentSlot.Weapon, DamageValue: 14),
        new("fire_axe", "Fire Axe", "A red fire-rescue axe.", ItemCategory.MeleeWeapon, 2.0f, 1, ["blade"], Equippable: true, EquipmentSlot: EquipmentSlot.Weapon, DamageValue: 22),
        new("maul", "Maul", "A heavy splitting maul.", ItemCategory.MeleeWeapon, 5.0f, 1, ["blunt"], Equippable: true, EquipmentSlot: EquipmentSlot.Weapon, DamageValue: 30),
        new("chain", "Chain", "A length of heavy chain.", ItemCategory.MeleeWeapon, 1.5f, 1, ["blunt"], Equippable: true, EquipmentSlot: EquipmentSlot.Weapon, DamageValue: 12),

        // ---- Equipment (Category.Equipment; backpacks/bags equip into the Backpack slot and raise carry capacity) ----
        new("small_backpack", "Small Backpack", "A lightweight daypack.", ItemCategory.Equipment, .6f, 1, ["storage"], Equippable: true, EquipmentSlot: EquipmentSlot.Backpack, CapacityBonusKg: 8),
        new("hiking_backpack", "Hiking Backpack", "A large expedition backpack.", ItemCategory.Equipment, 1.2f, 1, ["storage"], Equippable: true, EquipmentSlot: EquipmentSlot.Backpack, CapacityBonusKg: 16),
        new("duffel_bag", "Duffel Bag", "A soft-sided carry bag.", ItemCategory.Equipment, 1.0f, 1, ["storage"], Equippable: true, EquipmentSlot: EquipmentSlot.Backpack, CapacityBonusKg: 12),
        new("tool_bag", "Tool Bag", "An open-top tool carrier.", ItemCategory.Equipment, 1.4f, 1, ["storage"], Equippable: true, EquipmentSlot: EquipmentSlot.Backpack, CapacityBonusKg: 10),
        new("fanny_pack", "Fanny Pack", "A small waist bag.", ItemCategory.Equipment, .3f, 1, ["storage"], Equippable: true, EquipmentSlot: EquipmentSlot.Backpack, CapacityBonusKg: 4),
        new("tactical_vest", "Tactical Vest", "A pouched carrier vest.", ItemCategory.Equipment, 1.8f, 1, ["storage"], Equippable: true, EquipmentSlot: EquipmentSlot.Backpack, CapacityBonusKg: 6),
        new("messenger_bag", "Messenger Bag", "A shoulder satchel.", ItemCategory.Equipment, .7f, 1, ["storage"], Equippable: true, EquipmentSlot: EquipmentSlot.Backpack, CapacityBonusKg: 7),
        new("water_canteen", "Water Canteen", "A reusable canteen.", ItemCategory.Equipment, .4f, 1, ["gear", "hydration"]),
    ];

    private static readonly Dictionary<string, ItemDefinition> ById = BuildIndex();

    public static IReadOnlyList<ItemDefinition> All => Rows;

    public static bool TryGet(string id, out ItemDefinition definition) => ById.TryGetValue(id, out definition!);

    public static ItemDefinition Get(string id) => ById[id];

    private static Dictionary<string, ItemDefinition> BuildIndex()
    {
        Dictionary<string, ItemDefinition> index = new(Rows.Length);
        foreach (ItemDefinition row in Rows) index[row.Id] = row;
        return index;
    }
}
