using Godot;
namespace AshwoodCounty.Threats;
[Tool] public partial class ZombieVisual:Node2D
{
    private bool _dead;
    public void SetDead(){_dead=true;QueueRedraw();}
    public override void _Draw()
    {
        Zombie z=GetParent<Zombie>();if(_dead){DrawEllipse(new(0,-3),26,8,new Color(.18f,.16f,.13f,.65f));DrawLine(new(-22,-8),new(20,2),new Color("#59634b"),12);return;}
        DrawEllipse(new(0,-2),20,7,new Color(0,0,0,.3f));DrawLine(new(-5,-18),new(-9,0),new Color("#343a35"),6);DrawLine(new(5,-18),new(9,0),new Color("#343a35"),6);DrawLine(new(0,-48),new(0,-17),new Color("#59634b"),19);DrawCircle(new(1,-59),10,new Color("#889071"));DrawLine(new(-8,-42),new(-18,-25),new Color("#697259"),5);DrawLine(new(8,-42),new(18,-28),new Color("#697259"),5);
        if(!Engine.IsEditorHint()&&z.Health<z.MaxHealth){DrawRect(new Rect2(-22,-78,44,6),new Color(0,0,0,.8f));DrawRect(new Rect2(-20,-76,40*z.Health/z.MaxHealth,2),new Color("#cf4d48"));}
    }
    private void DrawEllipse(Vector2 c,float rx,float ry,Color color){Vector2[] p=new Vector2[24];for(int i=0;i<p.Length;i++){float a=Mathf.Tau*i/p.Length;p[i]=c+new Vector2(Mathf.Cos(a)*rx,Mathf.Sin(a)*ry);}DrawColoredPolygon(p,color);}
}
