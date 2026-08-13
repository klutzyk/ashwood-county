using AshwoodCounty.World;
using Godot;
using AshwoodCounty.World.Regions;
namespace AshwoodCounty.UI;
public partial class CountyMapController : CanvasLayer
{
    private Control _panel=null!; private Label _detail=null!; private RegionManager _regions=null!;
    public bool IsOpen=>_panel.Visible;
    public override void _Ready(){ProcessMode=ProcessModeEnum.Always;_regions=GetNode<RegionManager>("../RegionManager");_panel=GetNode<Control>("Panel");_detail=GetNode<Label>("Panel/MapFrame/Detail");_panel.Visible=false;GetNode<Button>("Panel/MapFrame/Close").Pressed+=Toggle;var buttons=new[]{"Outskirts","Farm","Mill"};for(int i=0;i<buttons.Length;i++){int index=i;GetNode<Button>($"Panel/MapFrame/{buttons[i]}").Pressed+=()=>ShowRegion(index);}}
    public override void _UnhandledInput(InputEvent e){if(e is InputEventKey k&&k.Pressed&&!k.Echo&&(k.Keycode==Key.M||(IsOpen&&k.Keycode==Key.Escape))){Toggle();GetViewport().SetInputAsHandled();}}
    private void Toggle(){_panel.Visible=!_panel.Visible;if(_panel.Visible)ShowRegion(0);}
    private void ShowRegion(int i){var r=RegionCatalog.All[i];RegionState state=_regions.StateStore.GetOrCreate(r.Id);bool known=r.Availability!=RegionAvailability.Unknown||state.Discovered;if(!known){_detail.Text="UNKNOWN REGION\n\nExplore county routes or learn about this area from survivor knowledge.";return;}string current=_regions.CurrentRegionId==r.Id?" • CURRENT":"";string connected=state.ConnectedToSettlement?"CONNECTED":"ISOLATED";_detail.Text=$"{r.Name}\n{r.Description}\n\n{r.Environment}  •  Danger {r.Danger}/5\nStatus: {state.Control}{current}\nNetwork: {connected}\nVisits: {state.VisitCount}\n\nKnown resources\n{string.Join("  •  ",r.Resources)}\n\nKnown landmarks\n{string.Join("\n",r.Landmarks)}\n\nRoutes\n{string.Join("  •  ",r.Neighbors)}\n\nPress T to travel along an available adjacent route.";}
}
