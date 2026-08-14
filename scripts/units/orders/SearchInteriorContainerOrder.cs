#nullable enable

using System.Linq;
using AshwoodCounty.Buildings.Interiors;
using AshwoodCounty.Resources;
using AshwoodCounty.UI;
using Godot;

namespace AshwoodCounty.Units.Orders;

public sealed class SearchInteriorContainerOrder(InteriorContainerRuntime container) : ISurvivorOrder
{
    private enum Phase { Moving, Searching }
    private readonly InteriorPathFollower _path = new();
    private ulong _survivorId;
    private Phase _phase;
    private float _elapsed;
    private bool _claimed;

    public SurvivorOrderType Type => SurvivorOrderType.SearchContainer;
    public bool IsComplete { get; private set; }

    public void Start(Survivor survivor)
    {
        _survivorId = survivor.GetInstanceId();
        if (!GodotObject.IsInstanceValid(container) || !container.TryClaim(_survivorId)) { IsComplete = true; return; }
        _claimed = true;
        _elapsed = container.SearchProgress * container.SearchDuration;
        _path.Plan(survivor, container.InteractionPosition);
    }

    public void Tick(Survivor survivor, double delta)
    {
        if (IsComplete || !GodotObject.IsInstanceValid(container)) { Complete(); return; }
        if (_phase == Phase.Moving)
        {
            if (_path.Tick(survivor, delta)) _phase = Phase.Searching;
            return;
        }
        survivor.StopMovement();
        _elapsed += (float)delta * survivor.WorkSpeedMultiplier * survivor.SkillMultiplier(SurvivorSkill.Scavenging);
        container.ReportProgress(_survivorId, _elapsed / Mathf.Max(.1f, container.SearchDuration));
        if (_elapsed < container.SearchDuration) return;
        SettlementInventory? inventory = survivor.GetTree().GetFirstNodeInGroup(SettlementInventory.GroupName) as SettlementInventory;
        if (inventory is null) { Complete(); return; }
        var found = container.CompleteSearch(_survivorId, inventory);
        survivor.GainSkillExperience(SurvivorSkill.Scavenging, 3f + found.Sum(stack => stack.Amount));
        string result = found.Count == 0 ? "Nothing useful" : string.Join("  •  ", found.Select(stack => $"{stack.Resource} +{stack.Amount}"));
        (survivor.GetTree().GetFirstNodeInGroup(GameHud.GroupName) as GameHud)?.Notify($"{container.DisplayName.ToUpperInvariant()} SEARCHED\n{result}");
        _claimed = false;
        IsComplete = true;
    }

    public void Cancel(Survivor survivor) => Complete();

    private void Complete()
    {
        if (_claimed && GodotObject.IsInstanceValid(container)) container.ReleaseClaim(_survivorId);
        _claimed = false; IsComplete = true;
    }
}
