using System.Linq;
using AshwoodCounty.UI;
using AshwoodCounty.Units;
using AshwoodCounty.World;
using Godot;
namespace AshwoodCounty.World.Regions;
[Tool]
public partial class Landmark : Node2D
{
    [Export] public string LandmarkId{get;set;}="landmark"; [Export] public string DisplayName{get;set;}="Abandoned Site"; [Export(PropertyHint.MultilineText)]public string DiscoveryText{get;set;}="A survivor has found a place worth remembering.";
    [Export]public Vector2 GridPosition{get;set;}=new(12,12);[Export]public float DiscoveryRadius{get;set;}=2.5f; private bool _discovered;
    public override void _Ready(){Position=IsometricGrid.GridToScreen(GridPosition);QueueRedraw();if(Engine.IsEditorHint())SetProcess(false);}
    public override void _Process(double delta){if(_discovered)return;RegionManager manager=GetTree().Root.FindChild("RegionManager",true,false) as RegionManager;if(manager is null)return;RegionState state=manager.StateStore.GetOrCreate(manager.CurrentRegionId);if(state.DiscoveredLandmarks.Contains(LandmarkId)){_discovered=true;QueueRedraw();return;}if(!GetTree().GetNodesInGroup(Survivor.GroupName).OfType<Survivor>().Any(s=>s.IsAlive&&s.SimulationPosition.DistanceSquaredTo(GridPosition)<=DiscoveryRadius*DiscoveryRadius))return;_discovered=true;state.DiscoveredLandmarks.Add(LandmarkId);state.Discovered=true;(GetTree().GetFirstNodeInGroup(GameHud.GroupName) as GameHud)?.Notify($"LANDMARK DISCOVERED\n{DisplayName}\n{DiscoveryText}");QueueRedraw();}
    public override void _Draw(){Color c=_discovered?new Color("d2b66c"):new Color("b9a36a");DrawCircle(Vector2.Zero,9,c);DrawCircle(Vector2.Zero,4,new Color("262a20"));DrawString(ThemeDB.FallbackFont,new Vector2(-70,-16),DisplayName,HorizontalAlignment.Center,140,14,new Color("f0e6ce"));}
}
