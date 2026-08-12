using AshwoodCounty.World;
using Godot;
namespace AshwoodCounty.UI;
public partial class CountyMapController : CanvasLayer
{
    private Control _panel=null!; private Label _detail=null!;
    public bool IsOpen=>_panel.Visible;
    public override void _Ready(){ProcessMode=ProcessModeEnum.Always;_panel=GetNode<Control>("Panel");_detail=GetNode<Label>("Panel/MapFrame/Detail");_panel.Visible=false;GetNode<Button>("Panel/MapFrame/Close").Pressed+=Toggle;var buttons=new[]{"Outskirts","Farm","Mill"};for(int i=0;i<buttons.Length;i++){int index=i;GetNode<Button>($"Panel/MapFrame/{buttons[i]}").Pressed+=()=>ShowRegion(index);}}
    public override void _UnhandledInput(InputEvent e){if(e is InputEventKey k&&k.Pressed&&!k.Echo&&(k.Keycode==Key.M||(IsOpen&&k.Keycode==Key.Escape))){Toggle();GetViewport().SetInputAsHandled();}}
    private void Toggle(){_panel.Visible=!_panel.Visible;if(_panel.Visible)ShowRegion(0);}
    private void ShowRegion(int i){var r=RegionCatalog.All[i];_detail.Text=$"{r.Name}\n{r.Description}\n\nStatus: {(r.Availability==RegionAvailability.Current?"CURRENT REGION":"NOT YET AVAILABLE")}";}
}
