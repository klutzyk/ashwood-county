using System.Linq;
using AshwoodCounty.Systems;
using AshwoodCounty.Units;
using Godot;
namespace AshwoodCounty.Threats;
public partial class ZombieThreatSystem:Node
{
    [Export] public int MaximumZombies{get;set;}=10; [Export] public float SpawnInterval{get;set;}=180; [Export] public float MinimumSurvivorDistance{get;set;}=10;
    private float _elapsed; private Node2D _objects=null!; private PackedScene _scene=null!;
    private static readonly Vector2[] Entries=[new(187,142),new(224,145),new(225,168),new(190,172),new(187,164)];
    public override void _Ready(){_objects=GetNode<Node2D>("../World/Objects");_scene=GD.Load<PackedScene>("res://scenes/units/Zombie.tscn");}
    public override void _Process(double delta)
    {
        _elapsed+=(float)delta;
        float threatScale = SurvivalCycle.GetThreatScale();
        float effectiveInterval = SpawnInterval / System.MathF.Max(0.1f, threatScale);
        if(_elapsed<effectiveInterval)return;
        _elapsed=0;
        int effectiveMaximum = MaximumZombies + (SurvivalCycle.IsNightActive() ? 4 : 0);
        if(GetTree().GetNodesInGroup(Zombie.GroupName).Count>=effectiveMaximum)return;
        TrySpawn();
    }
    private void TrySpawn(){foreach(Vector2 entry in Entries.OrderBy(_=>GD.Randf())){bool safe=GetTree().GetNodesInGroup(Survivor.GroupName).OfType<Survivor>().All(s=>s.SimulationPosition.DistanceTo(entry)>=MinimumSurvivorDistance);if(!safe)continue;Zombie z=_scene.Instantiate<Zombie>();z.SimulationPosition=entry+new Vector2((float)GD.RandRange(-1,1),(float)GD.RandRange(-1,1));_objects.AddChild(z);return;}}
}
