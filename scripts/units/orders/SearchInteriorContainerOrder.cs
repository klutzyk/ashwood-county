#nullable enable

using System.Collections.Generic;
using System.Linq;
using AshwoodCounty.Buildings.Interiors;
using AshwoodCounty.Items;
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
        IReadOnlyList<ItemStack> found = container.CompleteSearch(_survivorId);
        survivor.GainSkillExperience(SurvivorSkill.Scavenging, 3f + found.Sum(stack => stack.Quantity));
        string result = found.Count == 0 ? "Nothing useful" : string.Join("  -  ", found.Select(stack => $"{ItemCatalog.Get(stack.ItemId).DisplayName} x{stack.Quantity}"));
        (survivor.GetTree().GetFirstNodeInGroup(GameHud.GroupName) as GameHud)?.Notify($"{container.DisplayName.ToUpperInvariant()} SEARCHED\n{result}");
        (survivor.GetTree().GetFirstNodeInGroup(GameHud.GroupName) as GameHud)?.ShowContainerLoot(container, survivor);
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
