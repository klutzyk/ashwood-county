#nullable enable

using System;
using System.Linq;
using AshwoodCounty.Buildings.Interiors;
using AshwoodCounty.Jobs;
using AshwoodCounty.Resources;
using AshwoodCounty.Threats;
using AshwoodCounty.Units;
using AshwoodCounty.Units.Orders;
using AshwoodCounty.World;
using Godot;

namespace AshwoodCounty.Systems;

/// <summary>
/// Opt-in smoke test for the WORK loop, interaction gating, collision and
/// occlusion work in this pass. Set ASHWOOD_VALIDATE_WORK_UX=1; inert in
/// normal play. It drives the real input-facing systems (work mode toggles,
/// selection, job system, orders) and verifies the observable results.
/// </summary>
public partial class WorkLoopValidation : Node
{
    private enum Phase
    {
        Waiting,
        ChopAuto,
        ChopHarvest,
        ChopContinue,
        ChopCancel,
        Forage,
        ManualDesignation,
        TwoSurvivors,
        MoveFar,
        NoTargets,
        Haul,
        InteriorGateOutside,
        EnterBuilding,
        SearchInside,
        InteriorGateAfterExit,
        CollisionRoute,
        OcclusionBehind,
        OcclusionFront,
        Complete
    }

    private Phase _phase;
    private Survivor _first = null!;
    private Survivor _second = null!;
    private ChopDesignationController _designation = null!;
    private SurvivorSelectionController _selection = null!;
    private SettlementJobSystem _jobs = null!;
    private SettlementInventory _inventory = null!;
    private InteriorBuildingRuntime _building = null!;
    private InteriorContainerRuntime _fridge = null!;
    private HarvestableResource? _manualTree;
    private Vector2 _manualScreen;
    private int _woodBaseline;
    private int _materialsBaseline;
    private int _treesSnapshot;
    private double _elapsed;
    private double _phaseElapsed;
    private bool _phaseActivated;

    public override void _Ready()
    {
        if (System.Environment.GetEnvironmentVariable("ASHWOOD_VALIDATE_WORK_UX") != "1") { SetProcess(false); return; }
        _phase = Phase.Waiting;
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        _phaseElapsed += delta;
        if (_elapsed > 150) { Fail("timeout"); return; }
        if (_phase == Phase.Waiting) { TryBegin(); return; }

        switch (_phase)
        {
            case Phase.ChopAuto:
                if (!_phaseActivated) { _phaseActivated = true; _selection.DebugSelectOnly(_first); _designation.ToggleDesignation(); }
                if (!Check(() => _first.CurrentOrderType == SurvivorOrderType.HarvestResource && _first.CurrentWorkLabel == "Chopping", 8, "auto chop was not assigned")) return;
                if (!Check(() => GetTrees().Any(tree => tree.IsWorkHighlighted), 2, "chop mode did not highlight trees")) return;
                CaptureIfRequested("ASHWOOD_WORK_CAPTURE_PNG");
                _treesSnapshot = GetTrees().Sum(tree => tree.AvailableAmount);
                Next(Phase.ChopHarvest);
                break;

            case Phase.ChopHarvest:
                if (!Check(() => GetTrees().Sum(tree => tree.AvailableAmount) < _treesSnapshot || _inventory.GetAmount(ResourceType.Wood) > _woodBaseline, 20, "auto chop produced no wood")) return;
                Next(Phase.ChopContinue);
                break;

            case Phase.ChopContinue:
                if (!Check(() => _inventory.GetAmount(ResourceType.Wood) >= _woodBaseline + 2, 25, "chopped wood was not delivered to the stockpile")) return;
                Next(Phase.ChopCancel);
                break;

            case Phase.ChopCancel:
                _designation.EndDesignation();
                if (!Check(() => _first.CurrentOrderType == SurvivorOrderType.None, 3, "cancelling chop did not stop the worker")) return;
                if (!Check(() => !GetTrees().Any(tree => tree.IsWorkHighlighted), 2, "cancelling chop left highlights behind")) return;
                if (!Check(() => !_jobs.HasWorkMandate(_first), 2, "cancelling chop left a work mandate behind")) return;
                Next(Phase.Forage);
                break;

            case Phase.Forage:
                if (!_phaseActivated) { _phaseActivated = true; _designation.ToggleForageDesignation(); }
                if (!Check(() => GetTrees().Any(tree => tree.ResourceType == ResourceType.Food && tree.IsWorkHighlighted), 2, "forage mode did not highlight food resources")) return;
                if (!Check(() => _first.CurrentOrderType == SurvivorOrderType.HarvestResource && _first.CurrentWorkLabel == "Foraging", 8, "auto forage was not assigned")) return;
                _designation.EndDesignation();
                Next(Phase.ManualDesignation);
                break;

            case Phase.ManualDesignation:
                if (!_phaseActivated)
                {
                    _phaseActivated = true;
                    _selection.DebugSelectOnly(_first);
                    _designation.ToggleDesignation();
                    _manualTree = GetTrees().Where(t => t.ResourceType == ResourceType.Wood && t.IsHarvestable)
                        .OrderBy(t => t.WorldPosition.DistanceSquaredTo(_first.SimulationPosition)).FirstOrDefault();
                    if (_manualTree is not null)
                    {
                        _manualScreen = _manualTree.GetGlobalTransformWithCanvas().Origin;
                        _designation.ToggleResourceAt(_manualScreen);
                    }
                }
                if (!Check(() => GetTrees().Any(t => t.ResourceType == ResourceType.Wood && t.IsHarvestable), 2, "no tree for manual designation")) return;
                if (!Check(() => _manualTree is not null && _manualTree.IsDesignatedForHarvest, 1, "manual designation did not designate the clicked tree")) return;
                if (!Check(() => _first.CurrentOrderType == SurvivorOrderType.HarvestResource, 5, "manual designation did not prioritise the worker")) return;
                _designation.EndDesignation();
                Next(Phase.TwoSurvivors);
                break;

            case Phase.TwoSurvivors:
                if (!_phaseActivated)
                {
                    _phaseActivated = true;
                    _first.CancelCurrentOrder();
                    _second.CancelCurrentOrder();
                    _selection.DebugSelectOnly(_first);
                    _selection.DebugSelect(_second);
                    _designation.ToggleDesignation();
                }
                if (!Check(() => _selection.SelectedCount == 2, 2, "two-survivor selection failed")) return;
                if (!Check(() => _first.CurrentOrderType == SurvivorOrderType.HarvestResource && _second.CurrentOrderType == SurvivorOrderType.HarvestResource, 10, "two-survivor chop did not assign both")) return;
                ulong? firstTarget = _jobs.CurrentClaimTarget(_first)?.GetInstanceId();
                ulong? secondTarget = _jobs.CurrentClaimTarget(_second)?.GetInstanceId();
                if (!Check(() => firstTarget is not null && secondTarget is not null && firstTarget != secondTarget, 1, "two survivors claimed the same exclusive tree")) return;
                _designation.EndDesignation();
                Next(Phase.MoveFar);
                break;

            case Phase.MoveFar:
                if (!_phaseActivated) { _phaseActivated = true; _selection.DebugSelectOnly(_first); _first.IssueMoveOrder(new Vector2(320, 270)); }
                Next(Phase.NoTargets);
                break;

            case Phase.NoTargets:
                if (!At(_first, new Vector2(320, 270))) return;
                _phaseElapsed = 0;
                if (!_phaseActivated) { _phaseActivated = true; _designation.ToggleDesignation(); }
                if (!Check(() => _jobs.WorkStatusFor(_first) is string status && status.Contains("No trees nearby", StringComparison.OrdinalIgnoreCase), 12, "no-target feedback missing")) return;
                _designation.EndDesignation();
                _first.IssueMoveOrder(new Vector2(205, 158));
                Next(Phase.Haul);
                break;

            case Phase.Haul:
                if (!At(_first, new Vector2(205, 158), 4f)) return;
                _phaseElapsed = 0;
                if (!_phaseActivated)
                {
                    _phaseActivated = true;
                    _materialsBaseline = _inventory.GetAmount(ResourceType.Materials);
                    _designation.ToggleHaulDesignation();
                }
                if (!Check(() => GetDrops().Any(drop => drop.IsWorkHighlighted), 2, "haul mode did not highlight drops")) return;
                if (!Check(() => _first.CurrentOrderType == SurvivorOrderType.Haul, 10, "auto haul was not assigned")) return;
                if (!Check(() => _inventory.GetAmount(ResourceType.Materials) >= _materialsBaseline + 6, 25, "hauled items were not deposited as materials")) return;
                _designation.EndDesignation();
                _first.IssueMoveOrder(new Vector2(205, 158));
                Next(Phase.InteriorGateOutside);
                break;

            case Phase.InteriorGateOutside:
                if (!At(_first, new Vector2(205, 158), 4f)) return;
                _phaseElapsed = 0;
                if (!_phaseActivated)
                {
                    _phaseActivated = true;
                    _fridge = GetTree().GetNodesInGroup(InteriorContainerRuntime.GroupName).OfType<InteriorContainerRuntime>()
                        .FirstOrDefault(container => container.Id == "fridge")!;
                    _first.IssueSearchContainerOrder(_fridge);
                }
                if (_fridge is null) { Fail("fridge container missing"); return; }
                if (!Check(() => _first.CurrentOrderType == SurvivorOrderType.None, 4, "outside search order was accepted")) return;
                if (!Check(() => !_fridge.IsSearched && !_fridge.IsClaimed, 1, "outside search order touched the container")) return;
                _first.IssueEnterBuildingOrder(_building);
                Next(Phase.EnterBuilding);
                break;

            case Phase.EnterBuilding:
                if (!Check(() => _first.IsIndoors && _first.LocationState == SurvivorLocationState.Indoors, 20, "survivor never entered the building")) return;
                if (!Check(() => _building.HasSurvivorInside, 2, "building does not register the inside survivor")) return;
                if (!_phaseActivated) { _phaseActivated = true; _first.IssueSearchContainerOrder(_fridge); }
                Next(Phase.SearchInside);
                break;

            case Phase.SearchInside:
                if (!Check(() => _fridge.IsSearched, 15, "interior container search did not complete inside the building")) return;
                if (!Check(() => _fridge.RemainingLoot.Count > 0, 1, "interior search revealed no itemized loot")) return;
                _first.IssueMoveOrder(new Vector2(200, 162));
                Next(Phase.InteriorGateAfterExit);
                break;

            case Phase.InteriorGateAfterExit:
                if (!At(_first, new Vector2(200, 162), 3f)) return;
                if (_first.IsIndoors) return;
                _phaseElapsed = 0;
                if (!_phaseActivated) { _phaseActivated = true; _first.IssueSearchContainerOrder(_fridge); }
                if (!Check(() => _first.CurrentOrderType == SurvivorOrderType.None, 4, "post-exit search order was accepted")) return;
                Next(Phase.CollisionRoute);
                break;

            case Phase.CollisionRoute:
                if (GetTree().GetFirstNodeInGroup(WorldNavigationService.GroupName) is not WorldNavigationService navigation)
                {
                    Fail("world navigation service missing");
                    return;
                }

                if (!Check(() => navigation.IsBlocked(new Vector2(200, 155), new Vector2(240, 155)), 1, "house footprint is not blocking navigation")) return;
                if (!Check(() => navigation.Bypass(new Vector2(200, 155), new Vector2(240, 155)).Count >= 3, 1, "house footprint produced no bypass route")) return;
                Next(Phase.OcclusionBehind);
                break;

            case Phase.OcclusionBehind:
                if (!_phaseActivated) { _phaseActivated = true; _first.IssueMoveOrder(new Vector2(216.5f, 150.5f)); }
                if (!At(_first, new Vector2(216.5f, 150.5f))) return;
                _phaseElapsed = 0;
                if (!Check(() => _building.ExteriorOcclusionAlpha < 0.9f, 4, "building did not fade while survivor was behind it")) return;
                CaptureIfRequested("ASHWOOD_OCCLUSION_CAPTURE_PNG");
                Next(Phase.OcclusionFront);
                break;

            case Phase.OcclusionFront:
                if (!_phaseActivated) { _phaseActivated = true; _first.IssueMoveOrder(new Vector2(216.5f, 162f)); }
                if (!At(_first, new Vector2(216.5f, 162f))) return;
                _phaseElapsed = 0;
                if (!Check(() => _building.ExteriorOcclusionAlpha > 0.95f, 4, "building did not restore after survivor moved in front")) return;
                Pass();
                break;
        }
    }

    private void TryBegin()
    {
        Survivor[] survivors = GetTree().GetNodesInGroup(Survivor.GroupName).OfType<Survivor>().Where(s => s.IsAlive).Take(2).ToArray();
        _building = GetTree().GetNodesInGroup(InteriorBuildingRuntime.GroupName).OfType<InteriorBuildingRuntime>()
            .FirstOrDefault(building => building.ExteriorEntrance is not null
                && building.Definition.Containers.Any(container => container.Id == "fridge"))!;
        if (survivors.Length < 2 || _building is null) return;

        foreach (Zombie zombie in GetTree().GetNodesInGroup(Zombie.GroupName).OfType<Zombie>())
        {
            zombie.SetPhysicsProcess(false);
            zombie.RemoveFromGroup(Zombie.GroupName);
        }

        _first = survivors[0];
        _second = survivors[1];
        _first.MovementSpeed = 8;
        _second.MovementSpeed = 8;
        _designation = GetNode<ChopDesignationController>("../ChopDesignationController");
        _selection = GetNode<SurvivorSelectionController>("../SelectionController");
        _jobs = GetTree().GetFirstNodeInGroup(SettlementJobSystem.GroupName) as SettlementJobSystem
            ?? GetNode<SettlementJobSystem>("../SettlementJobSystem");
        _inventory = GetTree().GetFirstNodeInGroup(SettlementInventory.GroupName) as SettlementInventory
            ?? GetNode<SettlementInventory>("../SettlementInventory");
        _woodBaseline = _inventory.GetAmount(ResourceType.Wood);
        _selection.DebugSelectOnly(_first);
        GD.Print("WORK_UX_VALIDATION: begin");
        Next(Phase.ChopAuto);
    }

    /// <summary>Non-blocking phase gate: true once the condition holds; fails (and reports) once the phase times out.</summary>
    private bool Check(Func<bool> condition, double timeout, string failure)
    {
        if (condition()) return true;
        if (_phaseElapsed > timeout) Fail(failure);
        return false;
    }

    private HarvestableResource[] GetTrees()
    {
        return GetTree().GetNodesInGroup(HarvestableResource.GroupName).OfType<HarvestableResource>().ToArray();
    }

    private HaulableDrop[] GetDrops()
    {
        return GetTree().GetNodesInGroup(HaulableDrop.GroupName).OfType<HaulableDrop>().ToArray();
    }

    private static bool At(Survivor survivor, Vector2 position, float tolerance = 0.4f)
    {
        return survivor.SimulationPosition.DistanceTo(position) <= tolerance;
    }

    private void CaptureIfRequested(string envVar)
    {
        string? path = System.Environment.GetEnvironmentVariable(envVar);
        if (string.IsNullOrWhiteSpace(path)) return;
        CapturePngAfterFrames(path);
    }

    private async void CapturePngAfterFrames(string path)
    {
        for (int i = 0; i < 12; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Error error = GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print($"WORK_UX_CAPTURE: {error} {path}");
    }

    private void Next(Phase phase)
    {
        _phase = phase;
        _phaseElapsed = 0;
        _phaseActivated = false;
        GD.Print($"WORK_UX_VALIDATION: {phase}");
    }

    private void Fail(string reason)
    {
        GD.PrintErr($"WORK_UX_VALIDATION: FAIL ({reason}, phase={_phase}, first={_first?.SimulationPosition}, first_order={_first?.CurrentOrderType})");
        _phase = Phase.Complete;
        SetProcess(false);
    }

    private void Pass()
    {
        GD.Print("WORK_UX_VALIDATION: PASS (auto_chop=True, highlights=True, delivery=True, cancel=True, auto_forage=True, manual_designation=True, reservation_split=True, no_target_feedback=True, auto_haul=True, interior_gate=True, collision=True, occlusion=True)");
        _phase = Phase.Complete;
        SetProcess(false);
    }
}
