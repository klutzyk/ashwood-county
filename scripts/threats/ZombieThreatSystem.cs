using System.Linq;
using AshwoodCounty.Units;
using Godot;
namespace AshwoodCounty.Threats;
public partial class ZombieThreatSystem:Node
{
    [Export] public int MaximumZombies{get;set;}=10; [Export] public float SpawnInterval{get;set;}=180; [Export] public float MinimumSurvivorDistance{get;set;}=10;
    private float _elapsed; private Node2D _objects=null!; private PackedScene _scene=null!;
    private static readonly Vector2[] Entries=[new(2,5),new(39,8),new(40,31),new(5,35),new(2,27)];
    public override void _Ready(){_objects=GetNode<Node2D>("../World/Objects");_scene=GD.Load<PackedScene>("res://scenes/units/Zombie.tscn");}
    public override void _Process(double delta){_elapsed+=(float)delta;if(_elapsed<SpawnInterval)return;_elapsed=0;if(GetTree().GetNodesInGroup(Zombie.GroupName).Count>=MaximumZombies)return;TrySpawn();}
    private void TrySpawn(){foreach(Vector2 entry in Entries.OrderBy(_=>GD.Randf())){bool safe=GetTree().GetNodesInGroup(Survivor.GroupName).OfType<Survivor>().All(s=>s.SimulationPosition.DistanceTo(entry)>=MinimumSurvivorDistance);if(!safe)continue;Zombie z=_scene.Instantiate<Zombie>();z.SimulationPosition=entry+new Vector2((float)GD.RandRange(-1,1),(float)GD.RandRange(-1,1));_objects.AddChild(z);return;}}
}
