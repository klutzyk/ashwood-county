namespace AshwoodCounty.Units.Orders;

public enum SurvivorOrderType
{
    None,
    Move,
    HarvestResource,
    Build,
    Eat,
    Scavenge,
    Rest,
    Treat,
    AttackZombie,
    SearchContainer,
    UseBed,
    UseDoor
}

public interface ISurvivorOrder
{
    SurvivorOrderType Type { get; }
    bool IsComplete { get; }
    void Start(Survivor survivor);
    void Tick(Survivor survivor, double delta);
    void Cancel(Survivor survivor);
}
