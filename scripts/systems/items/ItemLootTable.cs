#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace AshwoodCounty.Items;

/// <summary>One weighted entry in an <see cref="ItemLootTableDefinition"/>. A null ItemId is a valid "nothing" outcome.</summary>
public sealed record ItemLootOption(string? ItemId, int Minimum, int Maximum, float Weight);

/// <summary>
/// A weighted, seeded item loot roller; the itemized counterpart of the
/// resource-based LootTableDefinition this replaces for interior containers.
/// Deterministic per seed so a container's contents can be rolled once and
/// never rerolled (see InteriorContainerRuntime.CompleteSearch).
/// </summary>
public sealed class ItemLootTableDefinition(string id, int rolls, params ItemLootOption[] options)
{
    public string Id { get; } = id;
    public int Rolls { get; } = Mathf.Max(1, rolls);
    public IReadOnlyList<ItemLootOption> Options { get; } = options;

    public IReadOnlyList<ItemStack> Roll(ulong seed)
    {
        RandomNumberGenerator random = new() { Seed = seed };
        Dictionary<string, int> totals = [];
        float totalWeight = 0;
        foreach (ItemLootOption option in Options) totalWeight += Mathf.Max(0, option.Weight);
        if (totalWeight <= 0) return [];

        for (int roll = 0; roll < Rolls; roll++)
        {
            float choice = random.RandfRange(0, totalWeight);
            ItemLootOption selected = Options[^1];
            foreach (ItemLootOption option in Options)
            {
                choice -= Mathf.Max(0, option.Weight);
                if (choice <= 0) { selected = option; break; }
            }
            if (selected.ItemId is not string itemId) continue;
            int amount = random.RandiRange(Mathf.Max(0, selected.Minimum), Mathf.Max(selected.Minimum, selected.Maximum));
            if (amount > 0) totals[itemId] = totals.GetValueOrDefault(itemId) + amount;
        }

        return totals.Select(pair => new ItemStack(pair.Key, pair.Value)).ToList();
    }
}

/// <summary>
/// Curated container loot presets, selectable in the Authoring Studio's
/// container inspector without hand-tuning per-item percentages. Names are
/// preserved from the original resource-based presets where a residential
/// test house already authors containers against them (Kitchen Refrigerator,
/// Kitchen Cupboard, Bathroom Cabinet, Bedroom Storage, Garage Shelf); three
/// more were added to broaden coverage per container archetype.
/// </summary>
public static class ItemLootPresets
{
    private const string None = null!;

    private static readonly Dictionary<string, ItemLootTableDefinition> Presets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Kitchen Refrigerator"] = new("kitchen_refrigerator", 3,
            new("canned_beans", 1, 2, .18f), new("canned_tomato_soup", 1, 2, .14f), new("canned_tuna", 1, 1, .10f),
            new("canned_corn", 1, 2, .12f), new("bottled_water", 1, 2, .16f), new("apple_juice_box", 1, 1, .08f),
            new("peanut_butter", 1, 1, .07f), new(None, 0, 0, .15f)),

        ["Kitchen Cupboard"] = new("kitchen_cupboard", 3,
            new("canned_spaghetti", 1, 1, .15f), new("saltine_crackers", 1, 2, .16f), new("canned_corn", 1, 1, .12f),
            new("corn_flakes_box", 1, 1, .10f), new("instant_noodles", 1, 2, .14f), new("duct_tape", 1, 1, .06f),
            new(None, 0, 0, .27f)),

        ["Bathroom Cabinet"] = new("bathroom_cabinet", 2,
            new("bandage", 1, 2, .18f), new("adhesive_bandage", 1, 3, .14f), new("pain_relief_tablets", 1, 1, .14f),
            new("alcohol_wipes", 1, 2, .12f), new("antiseptic_solution", 1, 1, .09f), new("disposable_gloves", 1, 1, .08f),
            new(None, 0, 0, .25f)),

        ["Bedroom Storage"] = new("bedroom_storage", 2,
            new("small_backpack", 1, 1, .05f), new("batteries", 1, 2, .14f), new("flashlight", 1, 1, .06f),
            new("zip_ties", 1, 1, .08f), new("energy_bar", 1, 2, .12f), new("multivitamin_tablets", 1, 1, .08f),
            new(None, 0, 0, .47f)),

        ["Garage Shelf"] = new("garage_shelf", 3,
            new("hammer", 1, 1, .05f), new("duct_tape", 1, 1, .12f), new("scrap_metal", 1, 2, .14f),
            new("nails", 1, 3, .12f), new("screws", 1, 3, .10f), new("wire_coil", 1, 1, .08f),
            new("tool_kit", 1, 1, .03f), new(None, 0, 0, .36f)),

        ["Farm Shed"] = new("farm_shed", 3,
            new("rope", 1, 1, .14f), new("wood_planks", 1, 2, .14f), new("shovel", 1, 1, .05f),
            new("fuel_can", 1, 1, .06f), new("tarp", 1, 1, .10f), new("wire_coil", 1, 1, .10f),
            new("beef_jerky", 1, 1, .08f), new(None, 0, 0, .33f)),

        ["Medical Cabinet"] = new("medical_cabinet", 2,
            new("first_aid_kit", 1, 1, .06f), new("bandage", 1, 2, .16f), new("antibiotic_tablets", 1, 1, .12f),
            new("cough_syrup", 1, 1, .10f), new("burn_gel", 1, 1, .10f), new("thermometer", 1, 1, .05f),
            new("tourniquet", 1, 1, .04f), new(None, 0, 0, .37f)),

        ["Grocery Shelf"] = new("grocery_shelf", 3,
            new("canned_beans", 1, 2, .13f), new("canned_tomato_soup", 1, 1, .11f), new("canned_corn", 1, 1, .11f),
            new("saltine_crackers", 1, 2, .12f), new("corn_flakes_box", 1, 1, .09f), new("sports_drink", 1, 1, .09f),
            new("instant_noodles", 1, 1, .10f), new("peanut_butter", 1, 1, .07f), new(None, 0, 0, .18f)),
    };

    public const string DefaultPresetName = "Bedroom Storage";

    public static IReadOnlyList<string> Names => Presets.Keys.OrderBy(name => name).ToArray();

    public static ItemLootTableDefinition Get(string name) => Presets.GetValueOrDefault(name, Presets[DefaultPresetName]);
}
