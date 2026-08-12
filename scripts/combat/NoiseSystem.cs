using System.Linq;
using AshwoodCounty.Threats;
using Godot;

namespace AshwoodCounty.Combat;

public partial class NoiseSystem : Node
{
    public const string GroupName = "noise_system";
    public override void _Ready() => AddToGroup(GroupName);

    public void Emit(Vector2 gridPosition, float radius)
    {
        float radiusSquared = radius * radius;
        foreach (Zombie zombie in GetTree().GetNodesInGroup(Zombie.GroupName).OfType<Zombie>())
            if (zombie.IsAlive && zombie.SimulationPosition.DistanceSquaredTo(gridPosition) <= radiusSquared)
                zombie.AlertTo(gridPosition);
    }
}
