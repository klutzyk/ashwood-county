using AshwoodCounty.World;
using Godot;
namespace AshwoodCounty.Buildings;
[Tool]
public partial class OutpostVisual : Node2D
{
    public override void _Ready()=>QueueRedraw();
    public override void _Draw()
    {
        DrawPolygon([new Vector2(-82,4),new Vector2(0,-38),new Vector2(82,4),new Vector2(0,48)], [new Color("514b36")]);
        DrawRect(new Rect2(-48,-90,96,92),new Color("34382c"));
        DrawPolygon([new Vector2(-60,-88),new Vector2(0,-125),new Vector2(60,-88),new Vector2(0,-55)], [new Color("6b5434")]);
        DrawLine(new Vector2(40,-92),new Vector2(40,-180),new Color("b79a5d"),5);
        DrawPolygon([new Vector2(42,-176),new Vector2(92,-158),new Vector2(42,-142)],[new Color("a5533d")]);
        DrawString(ThemeDB.FallbackFont,new Vector2(-42,-16),"OUTPOST",HorizontalAlignment.Center,84,14,new Color("eee3c7"));
    }
}
