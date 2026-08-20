using System.Linq;
using AshwoodCounty.Combat;
using AshwoodCounty.Systems;
using AshwoodCounty.Units;
using AshwoodCounty.World;
using Godot;

namespace AshwoodCounty.Threats;

[Tool]
public partial class Zombie : Node2D
{
    public const string GroupName="zombies";
    [Export] public Vector2 SimulationPosition{get=>_position;set{_position=value;UpdateRenderedPosition();}}
    [Export] public float MaxHealth{get;set;}=60;
    [Export] public float MovementSpeed{get;set;}=1.35f;
    [Export] public float DetectionRange{get;set;}=7.5f;
    [Export] public float AttackRange{get;set;}=.72f;
    [Export] public float AttackCooldown{get;set;}=1.4f;
    [Export] public float Damage{get;set;}=9;
    private Vector2 _position; private Survivor _target=null!; private float _health; private double _scan; private float _cooldown; private float _wanderTime; private Vector2 _wanderTarget; private bool _dead;
    public float Health=>_health; public bool IsAlive=>!_dead; public Survivor CurrentTarget=>_target;
    public override void _Ready(){UpdateRenderedPosition();if(Engine.IsEditorHint()){SetPhysicsProcess(false);return;} _health=MaxHealth;AddToGroup(GroupName);PickWanderTarget();}
    public override void _PhysicsProcess(double delta)
    {
        if(_dead)return; _scan-=delta;_cooldown=Mathf.Max(0,_cooldown-(float)delta);
        float threatScale = SurvivalCycle.GetThreatScale();
        if(_scan<=0){_scan=.4;AcquireTarget(threatScale);}
        if(IsInstanceValid(_target)&&_target.IsAlive){float distance=SimulationPosition.DistanceTo(_target.SimulationPosition);if(distance<=AttackRange){MovementVector=Vector2.Zero;if(_cooldown<=0)Attack();}else MoveTowards(_target.SimulationPosition,delta,threatScale);}
        else Wander(delta,threatScale);
    }
    public Vector2 MovementVector{get;private set;}
    public void AlertTo(Vector2 position){if(_dead||IsInstanceValid(_target))return;_wanderTarget=position;_wanderTime=5;}
    public void TakeDamage(float amount,Survivor attacker)
    {
        if(_dead||amount<=0)return;_health=Mathf.Max(0,_health-amount);_target=attacker;GetNode<CanvasItem>("Visual").QueueRedraw();SpawnFeedback($"-{amount:0}",new Color("#ffd36a"));if(_health<=0)Die();
    }
    public bool ContainsScreenPoint(Vector2 screen){if(_dead)return false;Vector2 local=GetGlobalTransformWithCanvas().AffineInverse()*screen;return new Rect2(-30,-82,60,86).HasPoint(local);}
    private void AcquireTarget(float threatScale)
    {
        float detection = DetectionRange * DetectionMultiplier(threatScale);
        if(IsInstanceValid(_target)&&_target.IsAlive&&SimulationPosition.DistanceTo(_target.SimulationPosition)<=detection*1.35f)return;
        _target=GetTree().GetNodesInGroup(Survivor.GroupName).OfType<Survivor>()
            .Where(s=>s.IsAlive&&s.SimulationPosition.DistanceSquaredTo(SimulationPosition)<=detection*detection)
            .MinBy(EffectiveDistanceSquared);
    }
    private void Attack(){if(!IsInstanceValid(_target)||!_target.IsAlive)return;_cooldown=AttackCooldown;_target.TakeDamage(Damage,this);SpawnFeedback("HIT",new Color("#e45b52"));}
    private void Wander(double delta,float threatScale){_wanderTime-=(float)delta;if(_wanderTime<=0||SimulationPosition.DistanceTo(_wanderTarget)<.15f)PickWanderTarget();MoveTowards(_wanderTarget,delta*.45,threatScale);}
    private void PickWanderTarget(){_wanderTime=(float)GD.RandRange(2.5,6.0);_wanderTarget=SimulationPosition+new Vector2((float)GD.RandRange(-2.5,2.5),(float)GD.RandRange(-2.5,2.5));_wanderTarget.X=Mathf.Clamp(_wanderTarget.X,.3f,IsometricWorld.MapWidth-.3f);_wanderTarget.Y=Mathf.Clamp(_wanderTarget.Y,.3f,IsometricWorld.MapHeight-.3f);}
    private void MoveTowards(Vector2 target,double delta,float threatScale){Vector2 difference=target-SimulationPosition;float distance=difference.Length();if(distance<.01f){MovementVector=Vector2.Zero;return;}MovementVector=difference/distance;SimulationPosition+=MovementVector*Mathf.Min(MovementSpeed*SpeedMultiplier(threatScale)*(float)delta,distance);}
    private float EffectiveDistanceSquared(Survivor survivor)
    {
        float distanceSquared = SimulationPosition.DistanceSquaredTo(survivor.SimulationPosition);
        if (SurvivalCycle.IsNightActive() && survivor.IsSheltered()) distanceSquared *= 4f;
        return distanceSquared;
    }
    private static float SpeedMultiplier(float threatScale)=>1f+(threatScale-1f)*0.5f;
    private static float DetectionMultiplier(float threatScale)=>1f+(threatScale-1f)*0.55f;
    private void Die(){if(_dead)return;_dead=true;_target=null!;MovementVector=Vector2.Zero;GetNode<ZombieVisual>("Visual").SetDead();RemoveFromGroup(GroupName);GetTree().CreateTimer(20).Timeout+=QueueFree;}
    private void SpawnFeedback(string text,Color color){CombatFeedback feedback=new();GetParent().AddChild(feedback);feedback.Initialize(Position,text,color);}
    private void UpdateRenderedPosition(){Position=IsometricGrid.GridToScreen(SimulationPosition);}
}
