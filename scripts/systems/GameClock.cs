using Godot;
namespace AshwoodCounty.Systems;
public partial class GameClock : Node
{
    [Export] public float GameMinutesPerSecond{get;set;}=1.5f; public double TotalMinutes{get;private set;}=480; public int Day=>(int)(TotalMinutes/1440)+1;
    public string DisplayTime=>$"Day {Day}  {((int)(TotalMinutes%1440))/60:00}:{((int)TotalMinutes)%60:00}";
    public override void _Process(double delta)=>TotalMinutes+=delta*GameMinutesPerSecond;
}
