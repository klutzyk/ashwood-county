using Godot;
namespace AshwoodCounty.Combat;
public partial class CombatFeedback : Node2D
{
    private float _life=.45f; private string _text=""; private Color _color;
    public void Initialize(Vector2 position,string text,Color color){Position=position;_text=text;_color=color;QueueRedraw();}
    public override void _Process(double delta){_life-=(float)delta;Position+=Vector2.Up*22*(float)delta;Modulate=new Color(1,1,1,Mathf.Clamp(_life/.45f,0,1));if(_life<=0)QueueFree();}
    public override void _Draw(){DrawString(ThemeDB.FallbackFont,new Vector2(-12,-80),_text,HorizontalAlignment.Center,28,14,_color);DrawArc(new Vector2(0,-20),25,-2.6f,-.5f,12,_color,3);}
}
