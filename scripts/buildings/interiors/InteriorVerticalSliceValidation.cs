#nullable enable

using System;
using System.Linq;
using AshwoodCounty.Resources;
using AshwoodCounty.Threats;
using AshwoodCounty.Units;
using AshwoodCounty.Jobs;
using Godot;

namespace AshwoodCounty.Buildings.Interiors;

/// <summary>Opt-in physical interaction and persistence test; inert in normal play.</summary>
public partial class InteriorVerticalSliceValidation : Node
{
    private enum Phase { Waiting, Approach, SplitRooms, Search, Rest, Exit, FarAway, Return, DoorToggleClose, DoorToggleOpen, Complete }
    private Phase _phase;
    private InteriorBuildingRuntime _building = null!;
    private Survivor _first = null!;
    private Survivor _second = null!;
    private Survivor[] _otherSurvivors = [];
    private SettlementInventory _inventory = null!;
    private InteriorContainerRuntime _fridge = null!;
    private InteriorContainerRuntime _utility = null!;
    private InteriorBedRuntime _bed = null!;
    private InteriorDoorRuntime _frontDoor = null!;
    private Vector2 _lastFirst;
    private Vector2 _lastSecond;
    private double _elapsed;
    private double _phaseElapsed;
    private int _startingFood;
    private int _startingMaterials;

    public override void _Ready()
    {
        if(System.Environment.GetEnvironmentVariable("ASHWOOD_VALIDATE_INTERIOR")!="1"){SetProcess(false);return;}
        _phase=Phase.Waiting;
    }

    public override void _Process(double delta)
    {
        _elapsed+=delta;_phaseElapsed+=delta;
        if(_elapsed>110){Fail("timeout");return;}
        if(_phase==Phase.Waiting){TryBegin();return;}
        if(!ValidateMovementStep(_first,_lastFirst,delta,out string firstFailure)){Fail("first survivor "+firstFailure);return;}
        if(!ValidateMovementStep(_second,_lastSecond,delta,out string secondFailure)){Fail("second survivor "+secondFailure);return;}
        _lastFirst=_first.SimulationPosition;_lastSecond=_second.SimulationPosition;

        switch(_phase)
        {
            case Phase.Approach:
                if(!At(_first,new Vector2(218.7f,156.55f))||!At(_second,new Vector2(218.95f,152.95f)))return;
                if(!_building.State.DiscoveredRooms.Contains("living")||!_building.State.DiscoveredRooms.Contains("kitchen")){Fail("entry rooms not discovered");return;}
                if(_building.State.Doors["front_door"].State!=InteriorDoorState.Open){Fail("entrance did not auto-open");return;}
                if(_building.ExteriorAlpha>.15f)return;
                _first.IssueMoveOrder(new Vector2(222.4f,152.9f));_second.IssueMoveOrder(new Vector2(222.45f,156.88f));Next(Phase.SplitRooms);break;
            case Phase.SplitRooms:
                if(!At(_first,new Vector2(222.4f,152.9f))||!At(_second,new Vector2(222.45f,156.88f)))return;
                if(!_building.State.DiscoveredRooms.Contains("bedroom_one")||!_building.State.DiscoveredRooms.Contains("bedroom_two")){Fail("bedrooms not revealed independently");return;}
                RefreshInteractables();
                _first.IssueSearchContainerOrder(_fridge);
                if(_fridge.TryClaim(_second.GetInstanceId())){_fridge.ReleaseClaim(_second.GetInstanceId());Fail("container reservation allowed a second survivor");return;}
                _second.IssueSearchContainerOrder(_utility);Next(Phase.Search);break;
            case Phase.Search:
                if(!_fridge.IsSearched||!_utility.IsSearched)return;
                if(_inventory.GetAmount(ResourceType.Food)<=_startingFood&&_inventory.GetAmount(ResourceType.Materials)<=_startingMaterials){Fail("search produced no authoritative resource change");return;}
                _first.Energy=10;_first.IssueBedRestOrder(_bed);
                if(_bed.TryReserve(_second.GetInstanceId())){_bed.Release(_second.GetInstanceId());Fail("bed reservation allowed a second survivor");return;}
                _second.IssueMoveOrder(new Vector2(218.7f,156.55f));Next(Phase.Rest);break;
            case Phase.Rest:
                if(_first.Energy<90)return;
                if(!_building.State.UsedFurniture.Contains("bed_one")){Fail("bed use not persisted");return;}
                _first.IssueMoveOrder(new Vector2(215.6f,158.1f));_second.IssueMoveOrder(new Vector2(224.2f,158.1f));Next(Phase.Exit);break;
            case Phase.Exit:
                if(!At(_first,new Vector2(215.6f,158.1f))||!At(_second,new Vector2(224.2f,158.1f)))return;
                if(_building.HasSurvivorInside)return;
                if(_building.ExteriorAlpha<.95f)return;
                _first.IssueMoveOrder(new Vector2(180,155));_second.IssueMoveOrder(new Vector2(180,156));Next(Phase.FarAway);break;
            case Phase.FarAway:
                if(!At(_first,new Vector2(180,155))||!At(_second,new Vector2(180,156)))return;
                if(_building.IsInteriorActive)return;
                GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
                _first.IssueMoveOrder(new Vector2(218.7f,156.55f));_second.IssueMoveOrder(new Vector2(218.95f,152.95f));Next(Phase.Return);break;
            case Phase.Return:
                if(!At(_first,new Vector2(218.7f,156.55f))||!At(_second,new Vector2(218.95f,152.95f)))return;
                if(!_building.IsInteriorActive)return;
                RefreshInteractables();
                if(!_fridge.IsSearched||!_utility.IsSearched||!_building.State.DiscoveredRooms.Contains("bedroom_two")){Fail("state did not survive unload/return");return;}
                _first.IssueDoorOrder(_frontDoor);Next(Phase.DoorToggleClose);break;
            case Phase.DoorToggleClose:
                if(_frontDoor.State!=InteriorDoorState.Closed)return;
                _first.IssueDoorOrder(_frontDoor);Next(Phase.DoorToggleOpen);break;
            case Phase.DoorToggleOpen:
                if(_frontDoor.State!=InteriorDoorState.Open)return;
                Pass();break;
        }
    }

    private void TryBegin()
    {
        _building=GetTree().GetNodesInGroup(InteriorBuildingRuntime.GroupName).OfType<InteriorBuildingRuntime>().FirstOrDefault()!;
        Survivor[] survivors=GetTree().GetNodesInGroup(Survivor.GroupName).OfType<Survivor>().Where(s=>s.IsAlive).Take(2).ToArray();
        if(_building is null||survivors.Length<2)return;
        foreach(Zombie zombie in GetTree().GetNodesInGroup(Zombie.GroupName).OfType<Zombie>()){zombie.SetPhysicsProcess(false);zombie.RemoveFromGroup(Zombie.GroupName);}
        (GetTree().GetFirstNodeInGroup(SettlementJobSystem.GroupName) as SettlementJobSystem)?.SetProcess(false);
        _first=survivors[0];_second=survivors[1];_first.MovementSpeed=8;_second.MovementSpeed=8;
        _otherSurvivors=GetTree().GetNodesInGroup(Survivor.GroupName).OfType<Survivor>().Where(s=>s.IsAlive&&s!=_first&&s!=_second).ToArray();
        for(int i=0;i<_otherSurvivors.Length;i++){_otherSurvivors[i].MovementSpeed=8;_otherSurvivors[i].IssueMoveOrder(new Vector2(180,157+i));}
        _inventory=GetTree().GetFirstNodeInGroup(SettlementInventory.GroupName) as SettlementInventory ?? throw new InvalidOperationException("Inventory missing");
        _startingFood=_inventory.GetAmount(ResourceType.Food);_startingMaterials=_inventory.GetAmount(ResourceType.Materials);
        _lastFirst=_first.SimulationPosition;_lastSecond=_second.SimulationPosition;
        _first.IssueMoveOrder(new Vector2(218.7f,156.55f));_second.IssueMoveOrder(new Vector2(218.95f,152.95f));
        GD.Print("INTERIOR_VALIDATION: physical approach started");Next(Phase.Approach);
    }

    private void RefreshInteractables()
    {
        _fridge=GetTree().GetNodesInGroup(InteriorContainerRuntime.GroupName).OfType<InteriorContainerRuntime>().First(c=>c.Id=="fridge");
        _utility=GetTree().GetNodesInGroup(InteriorContainerRuntime.GroupName).OfType<InteriorContainerRuntime>().First(c=>c.Id=="utility_shelf");
        _bed=GetTree().GetNodesInGroup(InteriorBedRuntime.GroupName).OfType<InteriorBedRuntime>().First(b=>b.Id=="bed_one");
        _frontDoor=GetTree().GetNodesInGroup(InteriorDoorRuntime.GroupName).OfType<InteriorDoorRuntime>().First(d=>d.DisplayName=="Front Door");
    }

    private bool ValidateMovementStep(Survivor survivor,Vector2 previous,double delta,out string failure)
    {
        float permitted=survivor.MovementSpeed*(float)delta*1.45f+.08f;
        float travelled=survivor.SimulationPosition.DistanceTo(previous);
        if(travelled>Mathf.Max(.65f,permitted)){failure=$"moved {travelled:0.000} from {previous} to {survivor.SimulationPosition} (delta={delta:0.0000})";return false;}
        foreach(Rect2 blocker in _building.NavigationBlockers)
            if(blocker.Grow(-.025f).HasPoint(survivor.SimulationPosition)&&!_building.IsWithinDoorway(survivor.SimulationPosition)){failure=$"entered blocker {blocker} at {survivor.SimulationPosition}";return false;}
        failure=string.Empty;return true;
    }

    private static bool At(Survivor survivor,Vector2 target)=>survivor.SimulationPosition.DistanceTo(target)<.12f;
    private void Next(Phase phase){_phase=phase;_phaseElapsed=0;GD.Print($"INTERIOR_VALIDATION: {phase}");}
    private void Fail(string reason){GD.PrintErr($"INTERIOR_VALIDATION: FAIL ({reason}, phase={_phase}, active={_building?.IsInteriorActive}, first={_first?.SimulationPosition}, first_order={_first?.CurrentOrderType}, second={_second?.SimulationPosition}, second_order={_second?.CurrentOrderType})");_phase=Phase.Complete;SetProcess(false);}
    private void Pass()
    {
        GD.Print($"INTERIOR_VALIDATION: PASS (rooms={_building.DiscoveredRoomCount}/{_building.Definition.Rooms.Count}, containers={_building.SearchedContainerCount}/{_building.ContainerCount}, bed=True, door_toggle=True, reservations=True, exterior_restored=True, persistence=True, multi_survivor=True, gc=True)");
        _phase=Phase.Complete;SetProcess(false);
    }
}
