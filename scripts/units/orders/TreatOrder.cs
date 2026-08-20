using System;
using AshwoodCounty.Resources;
using Godot;

namespace AshwoodCounty.Units.Orders;

/// <summary>The caller supplies the health mutation so health remains encapsulated by Survivor.</summary>
public sealed class TreatOrder(Survivor patient, SettlementInventory inventory, Action<Survivor, float> applyTreatment, float treatmentDuration = 3f) : ISurvivorOrder
{
    private float _elapsed;
    private bool _arrived;

    public SurvivorOrderType Type => SurvivorOrderType.Treat;
    public bool IsComplete { get; private set; }

    public void Start(Survivor survivor)
    {
        if (!GodotObject.IsInstanceValid(patient) || !patient.IsAlive || !inventory.CanAfford(ResourceType.Medicine, 1)) IsComplete = true;
    }

    public void Tick(Survivor survivor, double delta)
    {
        if (IsComplete) return;
        if (!GodotObject.IsInstanceValid(patient) || !patient.IsAlive) { IsComplete = true; return; }
        if (!_arrived) { _arrived = survivor.MoveTowardsGridPositionNavigated(patient.SimulationPosition, delta); return; }

        survivor.StopMovement();
        _elapsed += (float)delta;
        if (_elapsed < treatmentDuration) return;
        if (inventory.TrySpend(ResourceType.Medicine, 1))
        {
            applyTreatment(patient, 28f * survivor.SkillMultiplier(SurvivorSkill.Medical));
            survivor.GainSkillExperience(SurvivorSkill.Medical, 1.5f);
        }
        IsComplete = true;
    }

    public void Cancel(Survivor survivor) => IsComplete = true;
}
