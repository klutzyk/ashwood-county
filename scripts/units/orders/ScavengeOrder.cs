using AshwoodCounty.Resources;
using AshwoodCounty.UI;
using AshwoodCounty.World;
using Godot;

namespace AshwoodCounty.Units.Orders;

public sealed class ScavengeOrder(ScavengeSource target, Stockpile stockpile, Vector2 interactionPosition, Vector2 deliveryPosition, bool notifyOnComplete = false) : ISurvivorOrder
{
    private enum Phase { MovingToSource, Searching, Delivering }
    private readonly ScavengeSource _target = target;
    private readonly Stockpile _stockpile = stockpile;
    private readonly Vector2 _interactionPosition = interactionPosition;
    private readonly Vector2 _deliveryPosition = deliveryPosition;
    private Phase _phase;
    private float _elapsed;
    private ulong _workerId;
    private bool _claimed;
    private bool _notified;

    public SurvivorOrderType Type => SurvivorOrderType.Scavenge;
    public bool IsComplete { get; private set; }

    public void Start(Survivor survivor)
    {
        _workerId = survivor.GetInstanceId();
        if (!GodotObject.IsInstanceValid(_target) || !GodotObject.IsInstanceValid(_stockpile))
        {
            Notify(survivor, "Unavailable");
            IsComplete = true;
            return;
        }
        if (_target.IsDepleted)
        {
            Notify(survivor, "Already cleared");
            IsComplete = true;
            return;
        }
        if (!_target.TryClaim(_workerId))
        {
            Notify(survivor, "Already being searched");
            IsComplete = true;
            return;
        }
        _claimed = true;
        _phase = survivor.CarriedAmount > 0 ? Phase.Delivering : Phase.MovingToSource;
    }

    public void Tick(Survivor survivor, double delta)
    {
        if (IsComplete || !GodotObject.IsInstanceValid(_target) || !GodotObject.IsInstanceValid(_stockpile)) { Complete(); return; }
        if (_phase == Phase.MovingToSource)
        {
            if (_target.IsDepleted) { Complete(); return; }
            if (survivor.MoveTowardsGridPosition(_interactionPosition, delta)) { _elapsed = 0; _phase = Phase.Searching; }
        }
        else if (_phase == Phase.Searching)
        {
            _elapsed += (float)delta * survivor.WorkSpeedMultiplier * survivor.SkillMultiplier(SurvivorSkill.Scavenging);
            _target.ReportSearchProgress(_workerId, _elapsed / Mathf.Max(.1f, _target.SearchDuration));
            if (_elapsed < _target.SearchDuration) return;
            int found = _target.TakeLoot(_workerId, survivor.GetRemainingCarryCapacity(_target.LootType));
            if (found <= 0 || !survivor.TryAddCarriedResource(_target.LootType, found))
            {
                if (notifyOnComplete && !_notified) Notify(survivor, "No room to carry loot");
                Complete();
                return;
            }
            survivor.GainSkillExperience(SurvivorSkill.Scavenging, found * 1.5f);
            if (notifyOnComplete && !_notified)
            {
                _notified = true;
                SpawnResourceReveal(_target, found, _target.LootType);
            }
            _phase = Phase.Delivering;
        }
        else if (survivor.MoveTowardsGridPosition(_deliveryPosition, delta))
        {
            int delivered = survivor.RemoveCarriedResource();
            if (delivered > 0) _stockpile.Deposit(survivor.LastCarriedResourceType, delivered);
            if (_target.IsDepleted) Complete(); else { _elapsed = 0; _phase = Phase.MovingToSource; }
        }
    }

    public void Cancel(Survivor survivor) => Complete();

    private void Complete()
    {
        if (_claimed && GodotObject.IsInstanceValid(_target)) _target.ReleaseClaim(_workerId);
        _claimed = false;
        IsComplete = true;
    }

    private void Notify(Survivor survivor, string message)
    {
        if (!GodotObject.IsInstanceValid(_target)) return;
        (survivor.GetTree().GetFirstNodeInGroup(GameHud.GroupName) as GameHud)?.Notify($"{_target.ResolvedDisplayName.ToUpperInvariant()}\n{message}");
    }

    private static void SpawnResourceReveal(ScavengeSource source, int amount, ResourceType resourceType)
    {
        Node parent = source.GetParent();
        if (parent is null) return;
        string iconPath = resourceType switch
        {
            ResourceType.Wood => "res://assets/ui/icons/wood.svg",
            ResourceType.Food => "res://assets/ui/icons/food.svg",
            ResourceType.Medicine => "res://assets/ui/icons/medicine.svg",
            _ => "res://assets/ui/icons/materials.svg"
        };
        ItemRevealFeedback reveal = new();
        parent.AddChild(reveal);
        reveal.Initialize(
            source.Position + new Vector2(0f, -50f),
            [(TextureRegistry.Get(iconPath), amount.ToString())],
            "");
    }
}
