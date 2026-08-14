#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using AshwoodCounty.UI;
using AshwoodCounty.Units;
using AshwoodCounty.World;
using Godot;

namespace AshwoodCounty.Buildings.Interiors;

/// <summary>
/// Lightweight durable building controller. The exterior remains available to
/// the county renderer, while room/furniture nodes are created only near living
/// survivors and can be rebuilt entirely from definition + runtime state.
/// </summary>
public partial class InteriorBuildingRuntime : Node
{
    public const string GroupName = "interior_buildings";
    private readonly List<CanvasItem> _interiorVisuals = [];
    private readonly Dictionary<string, InteriorRoomMaskVisual> _roomMasks = [];
    private readonly List<InteriorDoorRuntime> _doors = [];
    private readonly List<InteriorContainerRuntime> _containers = [];
    private readonly List<InteriorBedRuntime> _beds = [];
    private Node2D _objectsRoot = null!;
    private InteriorNavigationService _navigation = null!;
    private InteriorExteriorVisual _exterior = null!;
    private bool _interiorActive;
    private float _exteriorAlpha = 1f;
    private double _activationCheck;

    public InteriorBuildingDefinition Definition { get; private set; } = null!;
    public InteriorBuildingRuntimeState State { get; private set; } = null!;
    public Rect2 NavigationBounds => Definition.Footprint.Grow(.35f);
    public IReadOnlyList<Rect2> NavigationBlockers { get; private set; } = [];
    public bool HasSurvivorInside { get; private set; }
    public bool IsInteriorActive => _interiorActive;
    public float ExteriorAlpha => _exteriorAlpha;
    public int DiscoveredRoomCount => State.DiscoveredRooms.Count;
    public int ContainerCount => Definition.Containers.Count;
    public int SearchedContainerCount => State.Containers.Values.Count(state => state.Searched);
    public InteriorSearchState SearchState => State.DiscoveredRooms.Count == 0 ? InteriorSearchState.Unknown
        : State.DiscoveredRooms.Count < Definition.Rooms.Count ? InteriorSearchState.PartiallyExplored
        : SearchedContainerCount < ContainerCount ? InteriorSearchState.Searched : InteriorSearchState.Depleted;

    public void Initialize(InteriorBuildingDefinition definition, InteriorBuildingRuntimeState state, Node2D objectsRoot, InteriorNavigationService navigation)
    {
        Definition = definition;
        State = state;
        _objectsRoot = objectsRoot;
        _navigation = navigation;
        EnsureStateEntries();
        NavigationBlockers = BuildNavigationBlockers();
    }

    public override void _Ready()
    {
        AddToGroup(GroupName);
        _navigation.Register(this);
        _exterior = new InteriorExteriorVisual { Name = Definition.Id + "_Exterior" };
        _exterior.Initialize(Definition);
        _objectsRoot.AddChild(_exterior);
        SetProcess(true);
        RefreshActivation(force: true);
    }

    public override void _ExitTree()
    {
        _navigation.Unregister(this);
        DeactivateInterior();
        if (GodotObject.IsInstanceValid(_exterior)) _exterior.QueueFree();
    }

    public override void _Process(double delta)
    {
        _activationCheck -= delta;
        if (_activationCheck <= 0)
        {
            _activationCheck = .25;
            RefreshActivation(force: false);
        }

        if (!_interiorActive) return;
        List<Survivor> survivors = GetTree().GetNodesInGroup(Survivor.GroupName).OfType<Survivor>().Where(s => s.IsAlive).ToList();
        HasSurvivorInside = survivors.Any(s => Definition.Footprint.Grow(-.10f).HasPoint(s.SimulationPosition));
        float targetExterior = HasSurvivorInside ? 0f : 1f;
        _exteriorAlpha = Mathf.MoveToward(_exteriorAlpha, targetExterior, (float)delta * 3.6f);
        _exterior.Modulate = new Color(1,1,1,_exteriorAlpha);
        float interiorAlpha = 1f - _exteriorAlpha;
        foreach (CanvasItem visual in _interiorVisuals)
        {
            if (visual is InteriorRoomMaskVisual) continue;
            visual.Modulate = new Color(1,1,1,interiorAlpha);
            visual.Visible = interiorAlpha > .01f;
        }

        foreach (Survivor survivor in survivors)
        {
            RoomDefinition? room = Definition.Rooms.FirstOrDefault(candidate => candidate.Bounds.HasPoint(survivor.SimulationPosition));
            if (room is not null && State.DiscoveredRooms.Add(room.Id))
            {
                Notify($"ROOM DISCOVERED\n{room.DisplayName}");
                NotifyStateChanged();
            }
        }

        foreach ((string roomId, InteriorRoomMaskVisual mask) in _roomMasks)
        {
            bool concealed = interiorAlpha > .01f && !State.DiscoveredRooms.Contains(roomId);
            mask.Visible = concealed;
            mask.Modulate = new Color(1,1,1,interiorAlpha);
        }
    }

    public bool OpenDoorsAlong(Vector2 from, Vector2 to, Survivor survivor)
    {
        foreach (InteriorDoorRuntime door in _doors)
        {
            if (door.IsPassable) continue;
            float distance = DistanceToSegment(door.InteractionPosition, from, to);
            if (distance <= .48f && survivor.SimulationPosition.DistanceTo(door.InteractionPosition) <= .75f)
            {
                if (!door.TryOpenForTraversal(survivor)) return false;
            }
        }
        return true;
    }

    public bool IsWithinDoorway(Vector2 point) => Definition.Doors.Any(door => door.Position.DistanceTo(point) <= .48f);

    public void NotifyStateChanged() { }

    public string ContextSummary()
    {
        string state = SearchState switch
        {
            InteriorSearchState.Unknown => "UNKNOWN",
            InteriorSearchState.PartiallyExplored => "PARTIALLY EXPLORED",
            InteriorSearchState.Searched => "ROOMS EXPLORED",
            _ => "DEPLETED"
        };
        return $"{Definition.DisplayName.ToUpperInvariant()}\n{state}  •  {SearchedContainerCount}/{ContainerCount} CONTAINERS SEARCHED";
    }

    private void RefreshActivation(bool force)
    {
        Vector2 center = Definition.Footprint.GetCenter();
        float nearest = GetTree().GetNodesInGroup(Survivor.GroupName).OfType<Survivor>()
            .Where(s => s.IsAlive).Select(s => s.SimulationPosition.DistanceTo(center)).DefaultIfEmpty(float.MaxValue).Min();
        bool shouldBeActive = nearest <= 38f;
        if (shouldBeActive && !_interiorActive) ActivateInterior();
        else if (!shouldBeActive && _interiorActive && !HasSurvivorInside) DeactivateInterior();
        if (force && !_interiorActive && shouldBeActive) ActivateInterior();
    }

    private void ActivateInterior()
    {
        if (_interiorActive) return;
        _interiorActive = true;
        foreach (RoomDefinition room in Definition.Rooms)
        {
            InteriorFloorVisual floor = new() { Name = Definition.Id + "_Floor_" + room.Id };
            floor.Initialize(room); AddInteriorNode(floor);
            InteriorRoomMaskVisual mask = new() { Name = Definition.Id + "_Mask_" + room.Id };
            mask.Initialize(room.Bounds); AddInteriorNode(mask); _roomMasks[room.Id] = mask;
        }

        foreach (WallDefinition wall in Definition.Walls) CreateWallVisuals(wall);
        foreach (FurnitureDefinition furniture in Definition.Furniture)
        {
            InteriorSpriteVisual visual = new() { Name = Definition.Id + "_Furniture_" + furniture.Id };
            visual.Initialize(furniture.TexturePath,furniture.Position,furniture.TargetHeight,furniture.Tint);
            AddInteriorNode(visual);
        }

        foreach (DoorDefinition definition in Definition.Doors)
        {
            InteriorDoorRuntime door = new() { Name = Definition.Id + "_Door_" + definition.Id };
            door.Initialize(definition,State.Doors[definition.Id]); AddInteriorNode(door); _doors.Add(door);
        }
        foreach (ContainerDefinition definition in Definition.Containers)
        {
            InteriorContainerRuntime container = new() { Name = Definition.Id + "_Container_" + definition.Id };
            container.Initialize(definition,State.Containers[definition.Id],this); AddInteriorNode(container); _containers.Add(container);
        }
        foreach (BedDefinition definition in Definition.Beds)
        {
            InteriorBedRuntime bed = new() { Name = Definition.Id + "_Bed_" + definition.Id };
            bed.Initialize(definition,this); AddInteriorNode(bed); _beds.Add(bed);
        }
    }

    private void DeactivateInterior()
    {
        if (!_interiorActive) return;
        _interiorActive = false;
        foreach (CanvasItem item in _interiorVisuals) if (GodotObject.IsInstanceValid(item)) item.QueueFree();
        _interiorVisuals.Clear(); _roomMasks.Clear(); _doors.Clear(); _containers.Clear(); _beds.Clear();
        HasSurvivorInside = false; _exteriorAlpha = 1; if (GodotObject.IsInstanceValid(_exterior)) _exterior.Modulate = Colors.White;
    }

    private void AddInteriorNode<T>(T node) where T : CanvasItem
    {
        _objectsRoot.AddChild(node);
        node.Modulate = new Color(1,1,1,0);
        node.Visible = false;
        _interiorVisuals.Add(node);
    }

    private void CreateWallVisuals(WallDefinition wall)
    {
        InteriorWallVisual visual=new(){Name=$"{Definition.Id}_Wall_{_interiorVisuals.Count}"};
        visual.Initialize(wall);AddInteriorNode(visual);
    }

    private IReadOnlyList<Rect2> BuildNavigationBlockers()
    {
        List<Rect2> blockers=[];
        foreach(WallDefinition wall in Definition.Walls)
        {
            Vector2 min=new(Mathf.Min(wall.Start.X,wall.End.X),Mathf.Min(wall.Start.Y,wall.End.Y));
            Vector2 max=new(Mathf.Max(wall.Start.X,wall.End.X),Mathf.Max(wall.Start.Y,wall.End.Y));
            blockers.Add(new Rect2(min-new Vector2(.07f,.07f),(max-min)+new Vector2(.14f,.14f)));
        }
        blockers.AddRange(Definition.Furniture.Where(f=>f.BlocksMovement).Select(f=>f.Footprint));
        blockers.AddRange(Definition.Containers.Select(c=>c.Footprint));
        blockers.AddRange(Definition.Beds.Select(b=>b.Footprint));
        return blockers;
    }

    private void EnsureStateEntries()
    {
        foreach(DoorDefinition door in Definition.Doors)
            if(!State.Doors.ContainsKey(door.Id))State.Doors[door.Id]=new DoorRuntimeState(door.InitialState);
        foreach(ContainerDefinition container in Definition.Containers)
            if(!State.Containers.ContainsKey(container.Id))State.Containers[container.Id]=new ContainerRuntimeState();
    }

    private void Notify(string message) => (GetTree().GetFirstNodeInGroup(GameHud.GroupName) as GameHud)?.Notify(message);
    private static float DistanceToSegment(Vector2 point,Vector2 start,Vector2 end)
    {
        Vector2 segment=end-start;float lengthSquared=segment.LengthSquared();
        if(lengthSquared<=.00001f)return point.DistanceTo(start);
        float t=Mathf.Clamp((point-start).Dot(segment)/lengthSquared,0,1);return point.DistanceTo(start+segment*t);
    }
}

public partial class InteriorBuildingSystem : Node
{
    public override void _Ready()
    {
        InteriorNavigationService navigation=GetNode<InteriorNavigationService>("../InteriorNavigationService");
        Node2D objects=GetNode<Node2D>("../World/Objects");
        World.County.CountyWorld county=GetNode<World.County.CountyWorld>("../World/CountyWorld");
        InteriorBuildingDefinition definition=ResidentialInteriorCatalog.ReferenceHouse;
        World.County.CountyChunkState chunk=county.GetChunkState(definition.Footprint.GetCenter());
        if(!chunk.Buildings.TryGetValue(definition.Id,out InteriorBuildingRuntimeState? state))
        {
            state=new InteriorBuildingRuntimeState();chunk.Buildings[definition.Id]=state;
        }
        InteriorBuildingRuntime runtime=new(){Name="ReferenceResidentialInterior"};
        runtime.Initialize(definition,state,objects,navigation);AddChild(runtime);
    }
}
