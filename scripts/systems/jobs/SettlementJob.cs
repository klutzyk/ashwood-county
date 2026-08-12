using Godot;

namespace AshwoodCounty.Jobs;

public enum SettlementJobType
{
    ChopTree,
    BuildConstruction
}

public sealed class SettlementJob(SettlementJobType type, GodotObject target)
{
    public SettlementJobType Type { get; } = type;
    public GodotObject Target { get; } = target;
}
