using System.Linq;
using AshwoodCounty.Units;
using AshwoodCounty.World;
using AshwoodCounty.World.County;
using AshwoodCounty.World.Fog;
using AshwoodCounty.World.Regions;
using Godot;
namespace AshwoodCounty.UI;
public partial class CountyMapController : CanvasLayer
{
    private Control _panel=null!;private Label _detail=null!;private CountyProgress _progress=null!;private CountyWorld _county=null!;private CountyFogOfWar _fog=null!;private CountyMapFogOverlay _fogOverlay=null!;
    public bool IsOpen=>_panel.Visible;
    public override void _Ready(){ProcessMode=ProcessModeEnum.Always;_progress=GetNode<CountyProgress>("../CountyProgress");_county=GetNode<CountyWorld>("../World/CountyWorld");_fog=GetNode<CountyFogOfWar>("../World/CountyFog");_panel=GetNode<Control>("Panel");_detail=GetNode<Label>("Panel/MapFrame/Detail");_panel.Visible=false;GetNode<Button>("Panel/MapFrame/Close").Pressed+=Toggle;string[] buttons=["Outskirts","Farm","Mill"];for(int i=0;i<buttons.Length;i++){int index=i;GetNode<Button>($"Panel/MapFrame/{buttons[i]}").Pressed+=()=>ShowRegion(index);}_fogOverlay=new CountyMapFogOverlay{Fog=_fog,Position=new Vector2(20,20),Size=new Vector2(710,580),MouseFilter=Control.MouseFilterEnum.Ignore};GetNode<Control>("Panel/MapFrame").AddChild(_fogOverlay);GetNode<Control>("Panel/MapFrame").MoveChild(_fogOverlay,1);}
    public override void _UnhandledInput(InputEvent e){if(e is InputEventKey k&&k.Pressed&&!k.Echo&&(k.Keycode==Key.M||(IsOpen&&k.Keycode==Key.Escape))){Toggle();GetViewport().SetInputAsHandled();}}
    public void Toggle(){_panel.Visible=!_panel.Visible;if(_panel.Visible){ShowRegion(0);_fogOverlay.QueueRedraw();}}
    private void ShowRegion(int i){RegionDefinition r=RegionCatalog.All[i];RegionState state=_progress.GetState(r.Id);CountyLocationDefinition location=CountyMacroLayout.Find(r.Id);bool physicallyExplored=location is not null&&_fog.IsExplored(location.Center);bool known=state.Discovered||r.Availability!=RegionAvailability.Unknown;if(!known){_detail.Text="UNKNOWN REGION\n\nLearn of this area through survivor knowledge or physical exploration.";return;}Survivor lead=GetTree().GetNodesInGroup(Survivor.GroupName).OfType<Survivor>().FirstOrDefault();string current=lead is not null&&_county.GetRegionAt(lead.SimulationPosition).Id==r.Id?"  •  CURRENT":"";string landmarks=state.DiscoveredLandmarks.Count>0?string.Join("\n",state.DiscoveredLandmarks):"None discovered";_detail.Text=$"{r.Name.ToUpperInvariant()}\n{r.Environment}  •  Danger {r.Danger}/5\n\nControl   {state.Control}{current}\nTerrain   {(physicallyExplored?"Explored":"Not explored")}\nCounty    {_fog.ExploredRatio:P1} explored\n\nRESOURCES\n{string.Join("  •  ",r.Resources)}\n\nLANDMARKS\n{landmarks}\n\nOverview only — survivors travel physically.";}
}
