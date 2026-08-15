#nullable enable

using System.Linq;
using AshwoodCounty.Jobs;
using AshwoodCounty.Resources;
using AshwoodCounty.Systems;
using AshwoodCounty.Threats;
using AshwoodCounty.Units;
using AshwoodCounty.Units.Orders;
using Godot;

namespace AshwoodCounty.Buildings.Interiors;

/// <summary>
/// Opt-in interaction-loop smoke test for the scavenging vertical slice. It
/// synthesizes right-clicks on a searchable interior container and then on a
/// world scavenge source, and verifies claim, approach, search progress and
/// loot reveal. Set ASHWOOD_VALIDATE_SCAVENGE_UX=1; inert in normal play.
/// </summary>
public partial class ScavengeInteractionValidation : Node
{
    private enum Phase { Waiting, Approach, ClickContainer, SearchContainer, ExitBuilding, ClickSource, SearchSource, Complete }
    private Phase _phase;
    private InteriorBuildingRuntime _building = null!;
    private Survivor _first = null!;
    private InteriorContainerRuntime _fridge = null!;
    private ScavengeSource _source = null!;
    private SurvivorSelectionController _selection = null!;
    private double _elapsed;

    public override void _Ready()
    {
        if (System.Environment.GetEnvironmentVariable("ASHWOOD_VALIDATE_SCAVENGE_UX") != "1") { SetProcess(false); return; }
        _phase = Phase.Waiting;
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        if (_elapsed > 100) { Fail("timeout"); return; }
        if (_phase == Phase.Waiting) { TryBegin(); return; }

        switch (_phase)
        {
            case Phase.Approach:
                if (_building is null || !_building.IsInteriorActive) return;
                if (_first.SimulationPosition.DistanceTo(new Vector2(218.7f, 156.55f)) > .2f) return;
                InteriorContainerRuntime candidate = GetTree().GetNodesInGroup(InteriorContainerRuntime.GroupName)
                    .OfType<InteriorContainerRuntime>().FirstOrDefault(c => c.Id == "fridge")!;
                if (candidate is null || !candidate.Visible) return;
                if (candidate.GlowStrength <= 0.01f) { Fail("unsearched container has no glow"); return; }
                _fridge = candidate;
                _selection.DebugSelectOnly(_first);
                RightClick(_fridge);
                Next(Phase.ClickContainer);
                break;

            case Phase.ClickContainer:
                if (_first.CurrentOrderType != SurvivorOrderType.SearchContainer || !_fridge.IsClaimed) return;
                if (_fridge.SearchProgress <= 0) return;
                Next(Phase.SearchContainer);
                break;

            case Phase.SearchContainer:
                if (!_fridge.IsSearched) return;
                if (_fridge.GlowStrength > 0.01f) { Fail("searched container still glows"); return; }
                if (_fridge.RemainingLoot.Count == 0) { Fail("fridge revealed no itemized loot"); return; }
                _first.IssueMoveOrder(new Vector2(_building.Definition.Footprint.Position.X - .8f, _building.Definition.Footprint.End.Y + 1f));
                Next(Phase.ExitBuilding);
                break;

            case Phase.ExitBuilding:
                if (_first.CurrentOrderType != SurvivorOrderType.None) return;
                _source = GetTree().GetNodesInGroup(ScavengeSource.GroupName)
                    .OfType<ScavengeSource>().First(s => s.Name == "SupplyWreck");
                RightClick(_source);
                Next(Phase.ClickSource);
                break;

            case Phase.ClickSource:
                if (_first.CurrentOrderType != SurvivorOrderType.Scavenge || !_source.IsClaimed) return;
                if (_source.DisplayedSearchProgress <= 0) return;
                Next(Phase.SearchSource);
                break;

            case Phase.SearchSource:
                if (_source.AvailableAmount >= _source.StartingAmount) return;
                GD.Print($"SCAVENGE_UX_VALIDATION: source yielded {_source.StartingAmount - _source.AvailableAmount} {_source.LootType}, fridge loot [{string.Join(", ", _fridge.RemainingLoot.Select(s => $"{s.ItemId}x{s.Quantity}"))}]");
                Pass();
                break;
        }
    }

    private void TryBegin()
    {
        _building = GetTree().GetNodesInGroup(InteriorBuildingRuntime.GroupName).OfType<InteriorBuildingRuntime>().FirstOrDefault()!;
        Survivor[] survivors = GetTree().GetNodesInGroup(Survivor.GroupName).OfType<Survivor>().Where(s => s.IsAlive).Take(2).ToArray();
        if (_building is null || survivors.Length < 2) return;
        _selection = GetNode<SurvivorSelectionController>("../SelectionController");
        foreach (Zombie zombie in GetTree().GetNodesInGroup(Zombie.GroupName).OfType<Zombie>())
        {
            zombie.SetPhysicsProcess(false);
            zombie.RemoveFromGroup(Zombie.GroupName);
        }
        (GetTree().GetFirstNodeInGroup(SettlementJobSystem.GroupName) as SettlementJobSystem)?.SetProcess(false);
        _first = survivors[0];
        _first.MovementSpeed = 8;
        _first.IssueMoveOrder(new Vector2(218.7f, 156.55f));
        survivors[1].MovementSpeed = 8;
        survivors[1].IssueMoveOrder(new Vector2(218.95f, 152.95f));
        GD.Print("SCAVENGE_UX_VALIDATION: approach started");
        Next(Phase.Approach);
    }

    private void RightClick(Node2D target)
    {
        Vector2 screen = target.GetGlobalTransformWithCanvas().Origin;
        if (target is InteriorContainerRuntime container && !container.ContainsScreenPoint(screen))
        {
            Fail("container hit test missed its own origin");
            return;
        }
        if (target is ScavengeSource source && !source.ContainsScreenPoint(screen))
        {
            Fail("source hit test missed its own origin");
            return;
        }
        _selection._UnhandledInput(new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Right,
            Pressed = true,
            Position = screen
        });
    }

    private void Next(Phase phase)
    {
        _phase = phase;
        GD.Print($"SCAVENGE_UX_VALIDATION: {phase}");
    }

    private void Fail(string reason)
    {
        GD.PrintErr($"SCAVENGE_UX_VALIDATION: FAIL ({reason}, phase={_phase}, first={_first?.SimulationPosition}, first_order={_first?.CurrentOrderType}, fridge_searched={_fridge?.IsSearched}, source_claimed={_source?.IsClaimed})");
        _phase = Phase.Complete;
        SetProcess(false);
    }

    private void Pass()
    {
        GD.Print("SCAVENGE_UX_VALIDATION: PASS (right_click_container=True, claim=True, progress=True, loot_reveal=True, right_click_source=True, source_search=True)");
        _phase = Phase.Complete;
        SetProcess(false);
    }
}
