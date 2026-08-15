using System.Linq;
using AshwoodCounty.Buildings;
using AshwoodCounty.Items;
using AshwoodCounty.Jobs;
using AshwoodCounty.Resources;
using AshwoodCounty.Units.Orders;
using AshwoodCounty.World;
using Godot;
using AshwoodCounty.Threats;
using AshwoodCounty.Buildings.Interiors;

namespace AshwoodCounty.Units;

[Tool]
public partial class Survivor : Node2D
{
    public const string GroupName = "survivors";

    private Vector2 _simulationPosition;

    [Export]
    public Vector2 SimulationPosition
    {
        get => _simulationPosition;
        set
        {
            _simulationPosition = value;
            if (Engine.IsEditorHint())
            {
                UpdateRenderedPosition();
            }
        }
    }

    [Export] public float MovementSpeed { get; set; } = 2.5f;
    [Export] public float ArrivalThreshold { get; set; } = 0.05f;
    [Export] public int CarryCapacityWood { get; set; } = 10;
    [Export] public int CarryCapacityFood { get; set; } = 6;
    [Export] public int CarryCapacityMaterials { get; set; } = 8;
    [Export] public int CarryCapacityMedicine { get; set; } = 4;
    [Export] public float Hunger { get; set; } = 82.0f;
    [Export] public float HungerDepletionPerSecond { get; set; } = 0.22f;
    [Export] public float HungryThreshold { get; set; } = 55.0f;
    [Export] public float CriticalHungerThreshold { get; set; } = 25.0f;
    [Export] public float MealRestoration { get; set; } = 48.0f;
    [Export] public float MaxHealth { get; set; } = 100.0f;
    [Export] public float MeleeDamage { get; set; } = 18.0f;
    [Export] public float AutoDefenseRange { get; set; } = 2.2f;
    [Export] public float Energy { get; set; } = 100.0f;
    [Export] public float Morale { get; set; } = 72.0f;
    [Export] public float BaseCarryCapacityKg { get; set; } = 20.0f;

    private CanvasItem _selectionIndicator = null!;
    private CanvasItem _visual = null!;
    private Vector2 _destination;
    private ISurvivorOrder _currentOrder = null!;
    private bool _isAutonomousOrder;
    private float _health;
    private bool _dead;
    private float _defenseScanElapsed;

    public bool IsSelected { get; private set; }
    public bool HasMoveOrder => CurrentOrderType == SurvivorOrderType.Move;
    public Vector2 Destination => _destination;
    public SurvivorOrderType CurrentOrderType => _currentOrder?.Type ?? SurvivorOrderType.None;
    public int CarriedAmount { get; private set; }
    public ResourceType CarriedResourceType { get; private set; } = ResourceType.Wood;
    public ResourceType LastCarriedResourceType { get; private set; } = ResourceType.Wood;
    public Vector2 MovementVector { get; private set; }
    public bool IsMoving => MovementVector.LengthSquared() > 0.000001f;
    public bool IsAutonomousOrder => _isAutonomousOrder && _currentOrder is not null;
    public bool IsAvailableForAutonomousWork => _currentOrder is null;
    public bool IsAlive => !_dead;
    public float Health => _health;
    public SurvivorProfile Profile { get; private set; } = new();
    public SurvivorInventory Inventory { get; private set; } = new();
    public bool NeedsMeal => Hunger <= HungryThreshold;
    public bool IsCriticallyHungry => Hunger <= CriticalHungerThreshold;
    public float WorkSpeedMultiplier => (IsCriticallyHungry ? .65f : NeedsMeal ? .85f : 1.0f) * Mathf.Lerp(.65f, 1.05f, Energy / 100f);
    public string Activity => CurrentOrderType switch
    {
        SurvivorOrderType.Move => "Moving",
        SurvivorOrderType.HarvestResource => CarriedAmount > 0 ? "Carrying / Delivering" : "Chopping",
        SurvivorOrderType.Build => "Building",
        SurvivorOrderType.Eat => "Eating",
        SurvivorOrderType.Scavenge => CarriedAmount > 0 ? "Delivering salvage" : "Scavenging",
        SurvivorOrderType.Rest => "Resting",
        SurvivorOrderType.Treat => "Providing treatment",
        SurvivorOrderType.AttackZombie => "Fighting",
        SurvivorOrderType.SearchContainer => "Searching a container",
        SurvivorOrderType.UseBed => "Resting in bed",
        SurvivorOrderType.UseDoor => "Using a door",
        _ when _dead => "Dead",
        _ => NeedsMeal ? "Idle • Hungry" : "Idle"
    };

    public override void _Ready()
    {
        if (Engine.IsEditorHint())
        {
            SetPhysicsProcess(false);
            UpdateRenderedPosition();
            return;
        }

        AddToGroup(GroupName);
        int profileIndex = int.TryParse(new string(Name.ToString().Where(char.IsDigit).ToArray()), out int parsed) ? Mathf.Max(0, parsed - 1) : GetIndex();
        Profile = SurvivorProfile.ForIndex(profileIndex);
        Inventory.BaseCapacityKg = BaseCarryCapacityKg;
        _health = MaxHealth;
        _selectionIndicator = GetNode<CanvasItem>("SelectionIndicator");
        _visual = GetNode<CanvasItem>("Visual");
        SetSelected(false);
        UpdateRenderedPosition();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Engine.IsEditorHint())
        {
            return;
        }

        Hunger = Mathf.Max(0, Hunger - HungerDepletionPerSecond * (float)delta);
        bool working = _currentOrder is not null && CurrentOrderType is not SurvivorOrderType.Eat;
        Energy = Mathf.Clamp(Energy + (working ? -0.32f : 0.20f) * (float)delta, 0, 100);
        Morale = Mathf.Clamp(Morale + (Hunger < 25 ? -0.08f : Hunger > 60 ? 0.015f : 0) * (float)delta, 0, 100);
        if(_dead)return;
        _defenseScanElapsed-=(float)delta;
        if(_defenseScanElapsed<=0){_defenseScanElapsed=.3f;TryAutoDefend();}
        if (_currentOrder is null)
        {
            return;
        }

        _currentOrder.Tick(this, delta);
        if (_currentOrder.IsComplete)
        {
            ReleaseAutonomousClaim();
            _currentOrder = null!;
        }
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        if (IsInstanceValid(_selectionIndicator))
        {
            _selectionIndicator.Visible = selected;
        }
    }

    public void IssueMoveOrder(Vector2 destination)
    {
        AssignOrder(new MoveOrder(destination), false);
    }

    public void IssueHarvestOrder(HarvestableResource target, Stockpile stockpile, Vector2 interactionPosition, Vector2 deliveryPosition)
    {
        AssignOrder(new HarvestResourceOrder(target, stockpile, interactionPosition, deliveryPosition), false);
    }

    public void IssueBuildOrder(ConstructionSite target, Vector2 interactionPosition)
    {
        AssignOrder(new BuildOrder(target, interactionPosition), false);
    }
    public void IssueAttackOrder(Zombie target) => AssignOrder(new AttackZombieOrder(target), false);
    public void IssueAutonomousAttackOrder(Zombie target) => AssignOrder(new AttackZombieOrder(target), true);
    public void TakeDamage(float amount,Zombie attacker)
    {
        if(_dead||amount<=0)return;_health=Mathf.Max(0,_health-amount);RefreshVisual();if(_health<=0){_dead=true;_currentOrder?.Cancel(this);_currentOrder=null!;MovementVector=Vector2.Zero;SetSelected(false);RemoveFromGroup(GroupName);}else if(_currentOrder is null||_isAutonomousOrder)IssueAutonomousAttackOrder(attacker);
    }
    public void StopMovement(){MovementVector=Vector2.Zero;}

    public void IssueAutonomousHarvestOrder(HarvestableResource target, Stockpile stockpile, Vector2 interactionPosition, Vector2 deliveryPosition)
    {
        AssignOrder(new HarvestResourceOrder(target, stockpile, interactionPosition, deliveryPosition), true);
    }

    public void IssueAutonomousBuildOrder(ConstructionSite target, Vector2 interactionPosition)
    {
        AssignOrder(new BuildOrder(target, interactionPosition), true);
    }

    public void IssueAutonomousScavengeOrder(ScavengeSource target, Stockpile stockpile, Vector2 interactionPosition, Vector2 deliveryPosition)
    {
        AssignOrder(new ScavengeOrder(target, stockpile, interactionPosition, deliveryPosition), true);
    }

    public void IssueAutonomousRestOrder(CompletedBuilding shelter) => AssignOrder(new RestOrder(shelter), true);
    public void IssueSearchContainerOrder(InteriorContainerRuntime container) => AssignOrder(new SearchInteriorContainerOrder(container), false);
    public void IssueBedRestOrder(InteriorBedRuntime bed) => AssignOrder(new UseInteriorBedOrder(bed), false);
    public void IssueDoorOrder(InteriorDoorRuntime door) => AssignOrder(new UseInteriorDoorOrder(door), false);
    public void IssueAutonomousTreatOrder(Survivor patient, SettlementInventory inventory) => AssignOrder(new TreatOrder(patient, inventory, (target, amount) => target.ReceiveTreatment(amount)), true);
    public void ReceiveTreatment(float amount) { if (!_dead) { _health = Mathf.Min(MaxHealth, _health + Mathf.Max(0, amount)); RefreshVisual(); } }

    /// <summary>Consumes one unit of a carried usable item (food restores Hunger, medical restores Health). Instant; no travel required since the item is already carried.</summary>
    public bool UseItem(string itemId)
    {
        if (!ItemCatalog.TryGet(itemId, out ItemDefinition definition) || !definition.Usable || _dead) return false;
        if (!Inventory.TryRemove(itemId, 1)) return false;
        if (definition.NutritionValue > 0) Hunger = Mathf.Min(100, Hunger + definition.NutritionValue);
        if (definition.HealValue > 0) { _health = Mathf.Min(MaxHealth, _health + definition.HealValue); RefreshVisual(); }
        return true;
    }

    public bool EquipItem(string itemId) => Inventory.Equip(itemId);
    public bool UnequipSlot(EquipmentSlot slot) => Inventory.Unequip(slot);

    public bool MoveTowardsGridPosition(Vector2 destination, double delta)
    {
        _destination = destination;
        Vector2 toDestination = destination - SimulationPosition;
        float distance = toDestination.Length();
        if (distance <= ArrivalThreshold)
        {
            MovementVector = Vector2.Zero;
            SimulationPosition = destination;
            UpdateRenderedPosition();
            return true;
        }

        float travelDistance = Mathf.Min(MovementSpeed * (float)delta, distance);
        MovementVector = toDestination / distance;
        SimulationPosition += MovementVector * travelDistance;
        UpdateRenderedPosition();
        bool arrived = travelDistance >= distance;
        if (arrived)
        {
            MovementVector = Vector2.Zero;
        }

        return arrived;
    }

    public int GetRemainingCarryCapacity(ResourceType resourceType)
    {
        if (CarriedAmount > 0 && CarriedResourceType != resourceType)
        {
            return 0;
        }

        int capacity = resourceType switch { ResourceType.Wood => CarryCapacityWood, ResourceType.Food => CarryCapacityFood, ResourceType.Materials => CarryCapacityMaterials, ResourceType.Medicine => CarryCapacityMedicine, _ => 0 };
        return Mathf.Max(0, capacity - CarriedAmount);
    }

    public bool AllowsWork(WorkCategory category) => Profile.Priority(category) != WorkPriority.Disabled;
    public float SkillMultiplier(SurvivorSkill skill) => 1f + (Profile.Skill(skill) - 1) * .06f;
    public void GainSkillExperience(SurvivorSkill skill, float amount) => Profile.AddExperience(skill, amount);

    public void EatMeal()
    {
        Hunger = Mathf.Min(100, Hunger + MealRestoration);
    }

    public void IssueAutonomousEatOrder(SettlementInventory inventory, Stockpile stockpile, Vector2 interactionPosition)
    {
        AssignOrder(new EatOrder(inventory, stockpile, interactionPosition), true);
    }

    public bool TryAddCarriedResource(ResourceType resourceType, int amount)
    {
        if (amount <= 0 || amount > GetRemainingCarryCapacity(resourceType))
        {
            return false;
        }

        CarriedResourceType = resourceType;
        LastCarriedResourceType = resourceType;
        CarriedAmount += amount;
        RefreshVisual();
        return true;
    }

    public int RemoveCarriedResource()
    {
        int amount = CarriedAmount;
        LastCarriedResourceType = CarriedResourceType;
        CarriedAmount = 0;
        RefreshVisual();
        return amount;
    }

    public bool ContainsScreenPoint(Vector2 screenPoint)
    {
        Vector2 localPoint = GetGlobalTransformWithCanvas().AffineInverse() * screenPoint;
        return GetLocalSelectionBounds().HasPoint(localPoint);
    }

    public Rect2 GetScreenSelectionBounds()
    {
        Rect2 localBounds = GetLocalSelectionBounds();
        Transform2D toScreen = GetGlobalTransformWithCanvas();
        Vector2 topLeft = toScreen * localBounds.Position;
        Vector2 topRight = toScreen * new Vector2(localBounds.End.X, localBounds.Position.Y);
        Vector2 bottomRight = toScreen * localBounds.End;
        Vector2 bottomLeft = toScreen * new Vector2(localBounds.Position.X, localBounds.End.Y);

        float minX = Mathf.Min(Mathf.Min(topLeft.X, topRight.X), Mathf.Min(bottomRight.X, bottomLeft.X));
        float maxX = Mathf.Max(Mathf.Max(topLeft.X, topRight.X), Mathf.Max(bottomRight.X, bottomLeft.X));
        float minY = Mathf.Min(Mathf.Min(topLeft.Y, topRight.Y), Mathf.Min(bottomRight.Y, bottomLeft.Y));
        float maxY = Mathf.Max(Mathf.Max(topLeft.Y, topRight.Y), Mathf.Max(bottomRight.Y, bottomLeft.Y));
        return new Rect2(minX, minY, maxX - minX, maxY - minY);
    }

    private static Rect2 GetLocalSelectionBounds()
    {
        return new Rect2(-30, -118, 60, 122);
    }

    private void AssignOrder(ISurvivorOrder order, bool autonomous)
    {
        if(_dead)return;
        ReleaseAutonomousClaim();
        _currentOrder?.Cancel(this);
        _currentOrder = order;
        _isAutonomousOrder = autonomous;
        _currentOrder.Start(this);
        if (_currentOrder.IsComplete)
        {
            ReleaseAutonomousClaim();
            _currentOrder = null!;
        }
    }
    private void TryAutoDefend()
    {
        if(_currentOrder is not null&&!_isAutonomousOrder)return;
        Zombie nearest=GetTree().GetNodesInGroup(Zombie.GroupName).OfType<Zombie>().Where(z=>z.IsAlive&&z.SimulationPosition.DistanceSquaredTo(SimulationPosition)<=AutoDefenseRange*AutoDefenseRange).MinBy(z=>z.SimulationPosition.DistanceSquaredTo(SimulationPosition));
        if(nearest is not null&&(_currentOrder is null||CurrentOrderType!=SurvivorOrderType.AttackZombie))IssueAutonomousAttackOrder(nearest);
    }

    private void ReleaseAutonomousClaim()
    {
        if (_isAutonomousOrder && IsInsideTree())
        {
            (GetTree().GetFirstNodeInGroup(SettlementJobSystem.GroupName) as SettlementJobSystem)?.ReleaseClaim(this);
        }

        _isAutonomousOrder = false;
    }

    private void RefreshVisual()
    {
        if (IsInstanceValid(_visual))
        {
            _visual.QueueRedraw();
        }
    }

    private void UpdateRenderedPosition()
    {
        Vector2 projectedPosition = IsometricGrid.GridToScreen(SimulationPosition);
        if (!Position.IsEqualApprox(projectedPosition))
        {
            Position = projectedPosition;
        }
    }
}
