using Godot;
using AshwoodCounty.World;
namespace AshwoodCounty.Resources;
[Tool]
public partial class ForageResourceVisual : Node2D
{
    private HarvestableResource _resource = null!;
    public override void _Ready() { _resource = GetParent<HarvestableResource>(); QueueRedraw(); }
    public override void _Process(double delta) { if (!Engine.IsEditorHint() && (_resource.IsTargeted || _resource.IsDesignatedForHarvest || _resource.IsHovered || _resource.IsWorkHighlighted)) QueueRedraw(); }
    public override void _Draw()
    {
        if (!Engine.IsEditorHint() && _resource.IsDepleted) return;
        Texture2D texture=TextureRegistry.Get("res://assets/art/environment/vegetation/bush_01.png");
        if(!Engine.IsEditorHint()&&(_resource.IsHovered||(_resource.IsWorkHighlighted&&_resource.IsHarvestable)))
        {
            float alpha = _resource.IsWorkHighlighted ? 0.34f : 0.20f;
            float pulse = _resource.IsWorkHighlighted ? 0.86f + 0.14f * Mathf.Sin((float)Time.GetTicksMsec() / 520.0f) : 1f;
            DrawBush(texture, .34f * 1.12f, new Color(1f,1f,1f,alpha*pulse));
            DrawBush(texture, .34f * 1.05f, new Color(1f,1f,1f,alpha*0.5f*pulse));
        }
        DrawBush(texture, .34f, new Color(.92f,1,.92f));
        for(int i=0;i<5;i++) DrawCircle(new Vector2(-15+i*7,-25-(i%2)*5),2.5f,new Color("#bd3046"));
        if(!Engine.IsEditorHint()&&_resource.IsDesignatedForHarvest) DrawPolyline(Ellipse(),new Color("#efb74d"),2,true);
    }
    private void DrawBush(Texture2D texture, float scale, Color tint)
    {
        Vector2 size=texture.GetSize()*scale;
        DrawTextureRect(texture,new Rect2(-size*new Vector2(.5f,1),size),false,tint);
    }
    private static Vector2[] Ellipse(){Vector2[] p=new Vector2[33];for(int i=0;i<p.Length;i++){float a=Mathf.Tau*i/32;p[i]=new(Mathf.Cos(a)*31,Mathf.Sin(a)*12);}return p;}
}
