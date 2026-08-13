using System.Collections.Generic;
using System.Linq;
using AshwoodCounty.UI;
using AshwoodCounty.Units;
using AshwoodCounty.World.Regions;
using AshwoodCounty.Buildings;
using AshwoodCounty.Threats;
using Godot;

namespace AshwoodCounty.World.County;

/// <summary>Persistent district progression for the continuous county. It never moves actors or loads regions.</summary>
public partial class CountyProgress : Node
{
    public const string GroupName="county_progress";
    public RegionStateStore StateStore{get;}=new();
    private CountyWorld _county=null!;
    private double _controlUpdate;
    public override void _Ready(){AddToGroup(GroupName);_county=GetNode<CountyWorld>("../World/CountyWorld");_county.ActorEnteredRegion+=Entered;Callable.From(SeedKnowledge).CallDeferred();}
    public RegionState GetState(string id)=>StateStore.GetOrCreate(id);
    private void SeedKnowledge(){foreach(Survivor survivor in GetTree().GetNodesInGroup(Survivor.GroupName).OfType<Survivor>())foreach(string id in survivor.Profile.KnownRegions)GetState(id).Discovered=true;GetState("outskirts").Discovered=true;GetState("outskirts").Control=RegionControl.Contested;}
    private void Discover(string id){if(id==CountyMacroLayout.WildernessRegionId)return;RegionState state=GetState(id);bool first=!state.Discovered;state.Discovered=true;if(state.Control==RegionControl.Unknown)state.Control=RegionControl.Contested;if(first)(GetTree().GetFirstNodeInGroup(GameHud.GroupName) as GameHud)?.Notify($"REGION DISCOVERED\n{CountyMacroLayout.Find(id)?.Name??id}\nThe county map has been updated.");}
    private void Entered(Node actor,string previous,string current){Discover(current);RegionState state=GetState(current);state.VisitCount++;}
    public override void _Process(double delta){_controlUpdate-=delta;if(_controlUpdate>0)return;_controlUpdate=1;foreach(CountyLocationDefinition region in _county.Regions){RegionState state=GetState(region.Id);if(!state.Discovered)continue;bool threats=GetTree().GetNodesInGroup(Zombie.GroupName).OfType<Zombie>().Any(z=>z.IsAlive&&_county.GetRegionAt(z.SimulationPosition).Id==region.Id);bool outpost=GetTree().GetNodesInGroup(CompletedBuilding.GroupName).OfType<CompletedBuilding>().Any(b=>b.BuildingType==BuildingType.Outpost&&b.RegionId==region.Id);state.HasOutpost=outpost;if(!threats&&state.Control==RegionControl.Contested)state.Control=RegionControl.Secured;if(outpost&&!threats){state.Control=RegionControl.Settled;state.Reclaimed=true;}state.ConnectedToSettlement=region.Id=="outskirts"||state.Control==RegionControl.Settled;}}
}
