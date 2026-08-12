using Godot;
namespace AshwoodCounty.Buildings;
[Tool] public partial class ProvisionsShedVisual : Node2D
{
    public override void _Draw(){DrawColoredPolygon([new(-48,-28),new(0,-52),new(48,-28),new(0,-4)],new Color("#536646"));DrawColoredPolygon([new(-42,-25),new(0,-4),new(0,28),new(-42,7)],new Color("#ad8050"));DrawColoredPolygon([new(0,-4),new(42,-25),new(42,7),new(0,28)],new Color("#805d3d"));DrawRect(new Rect2(-9,2,18,26),new Color("#41352a"));DrawCircle(new Vector2(-20,4),6,new Color("#b23b48"));}
}
