#nullable enable

using System;
using System.Linq;
using AshwoodCounty.Buildings;
using AshwoodCounty.Buildings.Interiors;
using AshwoodCounty.Resources;
using AshwoodCounty.Units;
using Godot;

namespace AshwoodCounty.Systems;

/// <summary>
/// Focused automated check for the early-game survival loop. It verifies the
/// authored starting scenario and drives the contextual director through
/// shelter, food, home-search, night, and dawn transitions without relying on
/// real-time input. Set ASHWOOD_VALIDATE_EARLY_GAME=1; inert in normal play.
/// </summary>
public partial class EarlyGameCoreLoopValidation : Node
{
    private enum Phase
    {
        Waiting,
        ScenarioCheck,
        ShelterCheck,
        FoodCheck,
        HomeCheck,
        NightCheck,
        DawnCheck,
        Complete
    }

    private Phase _phase;
    private SurvivalObjectives _objectives = null!;
    private SettlementInventory _inventory = null!;
    private GameClock _clock = null!;
    private InteriorBuildingRuntime? _home;
    private CompletedBuilding? _shelter;
    private int _frames;
    private double _elapsed;
    private double _phaseElapsed;

    public override void _Ready()
    {
        if (System.Environment.GetEnvironmentVariable("ASHWOOD_VALIDATE_EARLY_GAME") != "1")
        {
            SetProcess(false);
            return;
        }

        _phase = Phase.Waiting;
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        if (_elapsed > 70)
        {
            Fail("timeout");
            return;
        }

        if (_phase == Phase.Waiting)
        {
            TryBegin();
            return;
        }

        _frames++;
        _phaseElapsed += delta;
        switch (_phase)
        {
            case Phase.ScenarioCheck:
                RunScenarioCheck();
                break;
            case Phase.ShelterCheck:
                if (!_objectives.HasCompleted("shelter_secured"))
                {
                    if (_phaseElapsed > 4) Fail("shelter milestone not reached");
                    return;
                }
                if (!_objectives.CurrentPriority.Contains("FOOD", StringComparison.OrdinalIgnoreCase))
                {
                    if (_phaseElapsed > 6) Fail($"post-shelter priority should seek food, got: {_objectives.CurrentPriority}");
                    return;
                }
                _inventory.Add(ResourceType.Food, SurvivalTuning.FoodStockpileGoal - _inventory.GetAmount(ResourceType.Food));
                Next(Phase.FoodCheck);
                break;
            case Phase.FoodCheck:
                if (!_objectives.HasCompleted("food_secured"))
                {
                    if (_phaseElapsed > 4) Fail("food milestone not reached");
                    return;
                }
                _home = GetTree().GetNodesInGroup(InteriorBuildingRuntime.GroupName)
                    .OfType<InteriorBuildingRuntime>()
                    .FirstOrDefault(building => building.Definition.Id == SurvivalTuning.AbandonedHomeId);
                if (_home is null || _home.State.Containers.Count == 0)
                {
                    Fail("abandoned home or its containers are unavailable");
                    return;
                }
                _home.State.Containers.Values.First().Searched = true;
                Next(Phase.HomeCheck);
                break;
            case Phase.HomeCheck:
                if (!_objectives.HasCompleted("home_searched"))
                {
                    if (_phaseElapsed > 4) Fail("home-search milestone not reached");
                    return;
                }
                SurvivalCycle? cycle = SurvivalCycle.Active;
                if (cycle is null)
                {
                    Fail("survival cycle missing");
                    return;
                }
                _clock.SetTotalMinutes(cycle.NightStartMinute + 10);
                Next(Phase.NightCheck);
                break;
            case Phase.NightCheck:
                if (!_objectives.CurrentPriority.Contains("NIGHT", StringComparison.OrdinalIgnoreCase))
                {
                    if (_phaseElapsed > 4) Fail("night priority did not appear");
                    return;
                }
                _clock.SetTotalMinutes(1440 + SurvivalTuning.StartingGameMinutes);
                Next(Phase.DawnCheck);
                break;
            case Phase.DawnCheck:
                if (_clock.Day < 2 || !_objectives.HasCompleted("first_night_survived"))
                {
                    if (_phaseElapsed > 4) Fail($"day 2 / first-night milestone missing (day={_clock.Day})");
                    return;
                }
                Pass();
                break;
        }
    }

    private void TryBegin()
    {
        _objectives = GetTree().GetFirstNodeInGroup(SurvivalObjectives.GroupName) as SurvivalObjectives
            ?? throw new InvalidOperationException("SurvivalObjectives missing.");
        _inventory = GetTree().GetFirstNodeInGroup(SettlementInventory.GroupName) as SettlementInventory
            ?? throw new InvalidOperationException("SettlementInventory missing.");
        _clock = GetTree().GetFirstNodeInGroup(GameClock.GroupName) as GameClock
            ?? throw new InvalidOperationException("GameClock missing.");

        if (!_objectives.IsInsideTree() || !_inventory.IsInsideTree() || !_clock.IsInsideTree())
        {
            return;
        }

        _frames = 0;
        Next(Phase.ScenarioCheck);
    }

    private void RunScenarioCheck()
    {
        if (_phaseElapsed < 0.6f)
        {
            return;
        }

        if (_inventory.DevUnlimitedResources)
        {
            Fail("settlement inventory still has unlimited developer resources");
            return;
        }

        if (_inventory.GetAmount(ResourceType.Food) < SurvivalTuning.StartingFood
            || _inventory.GetAmount(ResourceType.Materials) < SurvivalTuning.StartingMaterials
            || _inventory.GetAmount(ResourceType.Medicine) < SurvivalTuning.StartingMedicine
            || _inventory.GetAmount(ResourceType.Wood) != SurvivalTuning.StartingWood)
        {
            Fail("starting stock does not match SurvivalTuning");
            return;
        }

        if (Math.Abs(_clock.TotalMinutes - SurvivalTuning.StartingGameMinutes) > 5)
        {
            Fail($"starting clock is {_clock.TotalMinutes}, expected about {SurvivalTuning.StartingGameMinutes}");
            return;
        }

        Survivor[] survivors = GetTree().GetNodesInGroup(Survivor.GroupName).OfType<Survivor>().Where(s => s.IsAlive).ToArray();
        if (survivors.Length == 0)
        {
            Fail("no living survivors found");
            return;
        }

        if (survivors.Any(s => s.Hunger >= 100f || s.Energy >= 100f))
        {
            Fail("starting needs are already fully satisfied");
            return;
        }

        if (!survivors.Any(s => s.Inventory.EquippedBackpackId is not null)
            || !survivors.Any(s => s.Inventory.EquippedWeaponId is not null)
            || !survivors.Any(s => s.Inventory.EquippedLightId is not null))
        {
            Fail("starting equipment grants did not apply");
            return;
        }

        if (!_objectives.CurrentPriority.Contains("SHELTER", StringComparison.OrdinalIgnoreCase))
        {
            if (_phaseElapsed > 5)
                Fail($"opening priority should seek shelter, got: {_objectives.CurrentPriority}");
            return;
        }

        CreateStartingShelter();
        Next(Phase.ShelterCheck);
    }

    private void CreateStartingShelter()
    {
        PackedScene scene = GD.Load<PackedScene>("res://scenes/buildings/Shelter.tscn");
        _shelter = scene.Instantiate<CompletedBuilding>();
        _shelter.Initialize(BuildingCatalog.Shelter, new Vector2(207f, 154f));
        GetNode<Node2D>("../World/Objects").AddChild(_shelter);
    }

    private void Next(Phase phase)
    {
        _phase = phase;
        _frames = 0;
        _phaseElapsed = 0;
        GD.Print($"EARLY_GAME_VALIDATION: {phase}");
    }

    private void Fail(string reason)
    {
        GD.PrintErr($"EARLY_GAME_VALIDATION: FAIL ({reason}, phase={_phase}, priority={_objectives?.CurrentPriority})");
        _phase = Phase.Complete;
        SetProcess(false);
    }

    private void Pass()
    {
        GD.Print($"EARLY_GAME_VALIDATION: PASS (scenario=True, shelter=True, food=True, home_search=True, night=True, dawn=True, milestones=[{string.Join(",", _objectives.CompletedMilestones)}])");
        _phase = Phase.Complete;
        SetProcess(false);
    }
}
