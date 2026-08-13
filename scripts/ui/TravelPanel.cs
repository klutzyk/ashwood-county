using System.Linq;
using AshwoodCounty.Systems;
using AshwoodCounty.Units;
using AshwoodCounty.World;
using AshwoodCounty.World.Regions;
using Godot;

namespace AshwoodCounty.UI;
public partial class TravelPanel : CanvasLayer
{
    private RegionManager _regions=null!; private GameClock _clock=null!; private PanelContainer _panel=null!; private VBoxContainer _rows=null!; private Label _heading=null!;
    public override void _Ready(){ProcessMode=ProcessModeEnum.Always;_regions=GetNode<RegionManager>("../RegionManager");_clock=GetNode<GameClock>("../GameClock");Build();_regions.RegionChanged+=OnChanged;}
    private void Build(){_panel=new PanelContainer(){Theme=AshwoodTheme.Create(),AnchorsPreset=(int)Control.LayoutPreset.Center,CustomMinimumSize=new Vector2(410,0),Visible=false};AddChild(_panel);_rows=new VBoxContainer();_panel.AddChild(_rows);_heading=new Label();_heading.AddThemeFontSizeOverride("font_size",21);_rows.AddChild(_heading);_rows.AddChild(new Label(){Text="Choose an adjacent playable region. Survivors, needs, time and inventory persist.",AutowrapMode=TextServer.AutowrapMode.WordSmart});Button close=new(){Text="CANCEL  [Esc]"};close.Pressed+=()=>_panel.Visible=false;_rows.AddChild(close);}
    public override void _UnhandledInput(InputEvent e){if(e is not InputEventKey k||!k.Pressed||k.Echo)return;if(k.Keycode==Key.T){Toggle();GetViewport().SetInputAsHandled();}else if(k.Keycode==Key.Escape&&_panel.Visible){_panel.Visible=false;GetViewport().SetInputAsHandled();}}
    private void Toggle(){_panel.Visible=!_panel.Visible;if(_panel.Visible)Refresh();}
    public void TogglePanel()=>Toggle();
    private void Refresh(){while(_rows.GetChildCount()>3)_rows.GetChild(2).Free();RegionDefinition current=RegionCatalog.Find(_regions.CurrentRegionId)??RegionCatalog.All[0];_heading.Text=$"TRAVEL FROM {current.Name.ToUpperInvariant()}";foreach(string id in current.Neighbors){RegionDefinition target=RegionCatalog.Find(id);if(target is null||!_regions.CanTravelTo(id))continue;Button b=new(){Text=$"{target.Name}   •   Danger {target.Danger}/5",TooltipText=target.Description};b.Pressed+=()=>Travel(id);_rows.AddChild(b);_rows.MoveChild(b,_rows.GetChildCount()-2);}}
    private void Travel(string id){_regions.TravelTo(id,_clock.TotalMinutes);_panel.Visible=false;}
    private void OnChanged(string id,Vector2 arrival){int index=0;foreach(Survivor s in GetTree().GetNodesInGroup(Survivor.GroupName).OfType<Survivor>()){s.IssueMoveOrder(arrival+new Vector2(index%3,(index/3)%2));index++;}}
}
