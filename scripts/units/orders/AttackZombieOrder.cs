using AshwoodCounty.Combat;
using AshwoodCounty.Threats;
using Godot;
namespace AshwoodCounty.Units.Orders;
public sealed class AttackZombieOrder(Zombie target):ISurvivorOrder
{
    private float _cooldown; private bool _winding; private float _windup;
    public SurvivorOrderType Type=>SurvivorOrderType.AttackZombie; public bool IsComplete{get;private set;}
    public void Start(Survivor survivor){if(!GodotObject.IsInstanceValid(target)||!target.IsAlive)IsComplete=true;}
    public void Tick(Survivor survivor,double delta)
    {
        if(IsComplete||!GodotObject.IsInstanceValid(target)||!target.IsAlive){IsComplete=true;return;}
        float distance=survivor.SimulationPosition.DistanceTo(target.SimulationPosition);if(distance>.75f){_winding=false;survivor.MoveTowardsGridPosition(target.SimulationPosition,delta);return;}
        survivor.StopMovement();_cooldown=Mathf.Max(0,_cooldown-(float)delta);if(_cooldown>0)return;
        if(!_winding){_winding=true;_windup=.28f;return;}_windup-=(float)delta;if(_windup>0)return;
        _winding=false;_cooldown=.85f;float damage=survivor.EffectiveMeleeDamage;target.TakeDamage(damage,survivor);survivor.GainSkillExperience(SurvivorSkill.Combat,Mathf.Max(2f,damage*.22f));(survivor.GetTree().GetFirstNodeInGroup(NoiseSystem.GroupName) as NoiseSystem)?.Emit(survivor.SimulationPosition,4.5f);
    }
    public void Cancel(Survivor survivor){IsComplete=true;}
}
