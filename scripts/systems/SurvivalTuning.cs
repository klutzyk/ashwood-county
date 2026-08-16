#nullable enable

using AshwoodCounty.Resources;

namespace AshwoodCounty.Systems;

/// <summary>
/// Centralized starting-scenario and survival-loop tuning. Keeping these
/// values in one place makes the opening day readable as a single authored
/// state instead of a collection of magic numbers scattered across scripts.
/// </summary>
public static class SurvivalTuning
{
    public const double StartingGameMinutes = 480; // Day 1, 08:00

    public const int StartingWood = 0;
    public const int StartingFood = 6;
    public const int StartingMaterials = 3;
    public const int StartingMedicine = 2;

    public const float StartingHunger = 82f;
    public const float StartingEnergy = 92f;
    public const float StartingMorale = 70f;

    public const int FoodStockpileGoal = 12;
    public const int MedicineStockpileGoal = 4;

    public const string AbandonedHomeId = "ashwood_house_220_155";

    /// <summary>
    /// Gear assigned to specific survivor indices. Keep this sparse on purpose:
    /// one scout light, one small pack, one defensive weapon, and a little
    /// first aid gives the opening group believable capability without making
    /// any one starting need disappear.
    /// </summary>
    public static readonly (int SurvivorIndex, string ItemId, int Quantity, bool Equip)[] StartingSurvivorGear =
    [
        (0, "bandage", 2, false),
        (1, "small_backpack", 1, true),
        (1, "flashlight", 1, true),
        (3, "baseball_bat", 1, true),
        (4, "bandage", 1, false),
    ];

    public static readonly (ResourceType Type, int Amount)[] StartingStock =
    [
        (ResourceType.Wood, StartingWood),
        (ResourceType.Food, StartingFood),
        (ResourceType.Materials, StartingMaterials),
        (ResourceType.Medicine, StartingMedicine),
    ];
}
