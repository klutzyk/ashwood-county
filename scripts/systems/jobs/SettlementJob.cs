using Godot;

namespace AshwoodCounty.Jobs;

public enum SettlementJobType
{
    HarvestResource,
    Scavenge,
    BuildConstruction,
    Eat,
    Rest,
    Treat,
    SearchContainer,
    EnterBuilding,
    Haul
}

public sealed class SettlementJob(SettlementJobType type, GodotObject target)
{
    public SettlementJobType Type { get; } = type;
    public GodotObject Target { get; } = target;
}
