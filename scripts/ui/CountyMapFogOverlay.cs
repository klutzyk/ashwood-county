using AshwoodCounty.World.Fog;
using Godot;
namespace AshwoodCounty.UI;
public partial class CountyMapFogOverlay:Control
{
    public CountyFogOfWar Fog{get;set;}=null!;
    public override void _Process(double delta){if(Visible)QueueRedraw();}
    public override void _Draw(){if(Fog is null)return;const int step=8;for(int y=0;y<320;y+=step)for(int x=0;x<384;x+=step){Vector2 center=new(x+step*.5f,y+step*.5f);if(Fog.IsExplored(center))continue;Rect2 cell=new(new Vector2(x/384f*Size.X,y/320f*Size.Y),new Vector2(step/384f*Size.X+1,step/320f*Size.Y+1));DrawRect(cell,new Color("080a08dc"));}}
}
