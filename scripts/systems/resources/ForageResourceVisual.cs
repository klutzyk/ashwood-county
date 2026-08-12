using Godot;
using AshwoodCounty.World;
namespace AshwoodCounty.Resources;
[Tool]
public partial class ForageResourceVisual : Node2D
{
    private HarvestableResource _resource = null!;
    public override void _Ready() { _resource = GetParent<HarvestableResource>(); QueueRedraw(); }
    public override void _Process(double delta) { if (!Engine.IsEditorHint() && (_resource.IsTargeted || _resource.IsDesignatedForHarvest)) QueueRedraw(); }
    public override void _Draw()
    {
        if (!Engine.IsEditorHint() && _resource.IsDepleted) return;
        Texture2D texture=TextureRegistry.Get("res://assets/art/environment/vegetation/bush_01.png"); Vector2 size=texture.GetSize()*.34f;
        DrawTextureRect(texture,new Rect2(-size*new Vector2(.5f,1),size),false,new Color(.92f,1,.92f));
        for(int i=0;i<5;i++) DrawCircle(new Vector2(-15+i*7,-25-(i%2)*5),2.5f,new Color("#bd3046"));
        if(!Engine.IsEditorHint()&&_resource.IsDesignatedForHarvest) DrawPolyline(Ellipse(),new Color("#efb74d"),2,true);
    }
    private static Vector2[] Ellipse(){Vector2[] p=new Vector2[33];for(int i=0;i<p.Length;i++){float a=Mathf.Tau*i/32;p[i]=new(Mathf.Cos(a)*31,Mathf.Sin(a)*12);}return p;}
}
