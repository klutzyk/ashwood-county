#nullable enable

using System;
using System.Collections.Generic;
using AshwoodCounty.Items;
using AshwoodCounty.UI;
using AshwoodCounty.Units;
using AshwoodCounty.World;
using Godot;

namespace AshwoodCounty.Buildings.Interiors;

public interface IInteriorInteractable
{
    string DisplayName { get; }
    Vector2 InteractionPosition { get; }
    bool ContainsScreenPoint(Vector2 screenPoint);
}

public partial class InteriorDoorRuntime : Node2D, IInteriorInteractable
{
    public const string GroupName = "interior_doors";
    private DoorDefinition _definition = null!;
    private DoorRuntimeState _state = null!;
    private Texture2D _closed = null!;
    private Texture2D _open = null!;

    public string DisplayName => _definition.DisplayName;
    public Vector2 InteractionPosition => _definition.Position;
    public InteriorDoorState State => _state.State;
    public bool IsPassable => State is InteriorDoorState.Open or InteriorDoorState.Broken;

    public void Initialize(DoorDefinition definition, DoorRuntimeState state)
    {
        _definition = definition;
        _state = state;
        _closed = TextureRegistry.Get(definition.ClosedTexturePath);
        _open = TextureRegistry.Get(definition.OpenTexturePath);
        Position = IsometricGrid.GridToScreen(definition.Position);
        ZIndex = 0;
    }

    public override void _Ready() { AddToGroup(GroupName); QueueRedraw(); }

    public bool TryOpenForTraversal(Survivor survivor)
    {
        if (State == InteriorDoorState.Open || State == InteriorDoorState.Broken) return true;
        if (State is InteriorDoorState.Locked or InteriorDoorState.Barricaded) return false;
        _state.State = InteriorDoorState.Open;
        QueueRedraw();
        return true;
    }

    public bool Toggle()
    {
        if (State is InteriorDoorState.Locked or InteriorDoorState.Barricaded) return false;
        _state.State = State == InteriorDoorState.Open ? InteriorDoorState.Closed : InteriorDoorState.Open;
        QueueRedraw();
        return true;
    }

    public bool ContainsScreenPoint(Vector2 screenPoint)
    {
        Vector2 local = GetGlobalTransformWithCanvas().AffineInverse() * screenPoint;
        return new Rect2(-34,-104,68,110).HasPoint(local);
    }

    public override void _Draw()
    {
        Texture2D texture = IsPassable ? _open : _closed;
        float scale = 84f / texture.GetHeight();
        Vector2 size = texture.GetSize() * scale;
        Color tint = State == InteriorDoorState.Barricaded ? new Color(.78f,.67f,.55f) : Colors.White;
        DrawTextureRect(texture,new Rect2(-size.X*.5f,-size.Y,size.X,size.Y),false,tint);
    }
}

public partial class InteriorContainerRuntime : Node2D, IInteriorInteractable
{
    public const string GroupName = "interior_containers";
    private ContainerDefinition _definition = null!;
    private ContainerRuntimeState _state = null!;
    private InteriorBuildingRuntime _building = null!;
    private Texture2D _texture = null!;
    private ulong _claimingSurvivor;

    public string Id => _definition.Id;
    public string DisplayName => _definition.DisplayName;
    public Vector2 InteractionPosition => _definition.InteractionPosition;
    public float SearchDuration => _definition.SearchDuration;
    public bool IsSearched => _state.Searched;
    public bool IsClaimed => _claimingSurvivor != 0;
    public float SearchProgress => _state.SearchProgress;
    public IReadOnlyList<ItemStack> RemainingLoot => _state.RemainingLoot;

    public void Initialize(ContainerDefinition definition, ContainerRuntimeState state, InteriorBuildingRuntime building)
    {
        _definition = definition;
        _state = state;
        _building = building;
        _texture = TextureRegistry.Get(definition.TexturePath);
        Position = IsometricGrid.GridToScreen(definition.Position);
        ZIndex = 0;
    }

    public override void _Ready() { AddToGroup(GroupName); QueueRedraw(); }

    public bool TryClaim(ulong survivorId)
    {
        if (IsSearched || (_claimingSurvivor != 0 && _claimingSurvivor != survivorId)) return false;
        _claimingSurvivor = survivorId;
        return true;
    }

    public void ReportProgress(ulong survivorId, float progress)
    {
        if (_claimingSurvivor != survivorId) return;
        _state.SearchProgress = Mathf.Clamp(progress, 0, 1);
        QueueRedraw();
    }

    /// <summary>
    /// Reveals this container's contents. Rolled exactly once, on first
    /// completion, from a seed stable across the building+container id; a
    /// second search (or a chunk unload/reload in between) never rerolls it.
    /// Does NOT transfer anything into the survivor's inventory; that is a
    /// separate, player-driven step via <see cref="TakeItem"/>/<see cref="TakeAll"/>
    /// so a player can take some items and leave the rest for later.
    /// </summary>
    public IReadOnlyList<ItemStack> CompleteSearch(ulong survivorId)
    {
        if (_claimingSurvivor != survivorId || IsSearched) return _state.RemainingLoot;
        if (_state.RemainingLoot.Count == 0)
        {
            ulong seed = StableSeed(_building.Definition.Id + ":" + _definition.Id);
            _state.RemainingLoot.AddRange(_definition.ItemLootTable.Roll(seed));
        }
        _state.Searched = true;
        _state.SearchProgress = 1;
        _claimingSurvivor = 0;
        _building.NotifyStateChanged();
        QueueRedraw();
        return _state.RemainingLoot;
    }

    /// <summary>Moves up to <paramref name="quantity"/> of one revealed item into a survivor's inventory. Returns the amount actually taken (may be less, e.g. if the survivor is near capacity).</summary>
    public int TakeItem(string itemId, int quantity, SurvivorInventory into)
    {
        if (quantity <= 0) return 0;
        int index = _state.RemainingLoot.FindIndex(stack => stack.ItemId == itemId);
        if (index < 0) return 0;
        int available = _state.RemainingLoot[index].Quantity;
        int added = into.TryAdd(itemId, Mathf.Min(quantity, available));
        if (added <= 0) return 0;
        int left = available - added;
        if (left <= 0) _state.RemainingLoot.RemoveAt(index);
        else _state.RemainingLoot[index] = _state.RemainingLoot[index] with { Quantity = left };
        _building.NotifyStateChanged();
        return added;
    }

    /// <summary>Takes as much of every remaining stack as the survivor can carry. Whatever does not fit stays in the container.</summary>
    public int TakeAll(SurvivorInventory into)
    {
        int totalTaken = 0;
        foreach (ItemStack stack in _state.RemainingLoot.ToArray()) totalTaken += TakeItem(stack.ItemId, stack.Quantity, into);
        return totalTaken;
    }

    public void ReleaseClaim(ulong survivorId)
    {
        if (_claimingSurvivor == survivorId) _claimingSurvivor = 0;
    }

    public bool ContainsScreenPoint(Vector2 screenPoint)
    {
        Vector2 local = GetGlobalTransformWithCanvas().AffineInverse() * screenPoint;
        return new Rect2(-45,-112,90,120).HasPoint(local);
    }

    public override void _Draw()
    {
        float scale = _definition.TargetHeight / Mathf.Max(1,_texture.GetHeight());
        Vector2 size = _texture.GetSize()*scale;
        DrawTextureRect(_texture,new Rect2(-size.X*.5f,-size.Y,size.X,size.Y),false,IsSearched?new Color(.65f,.65f,.61f,.82f):Colors.White);
        if (IsClaimed && !IsSearched)
        {
            DrawRect(new Rect2(-24,5,48,4),new Color(.08f,.09f,.07f,.82f));
            DrawRect(new Rect2(-24,5,48*_state.SearchProgress,4),new Color("c8a45d"));
        }
    }

    private static ulong StableSeed(string text)
    {
        ulong hash = 14695981039346656037UL;
        foreach (char character in text) { hash ^= character; hash *= 1099511628211UL; }
        return hash;
    }
}

public partial class InteriorBedRuntime : Node2D, IInteriorInteractable
{
    public const string GroupName = "interior_beds";
    private BedDefinition _definition = null!;
    private InteriorBuildingRuntime _building = null!;
    private Texture2D _texture = null!;
    private ulong _claimingSurvivor;

    public string Id => _definition.Id;
    public string DisplayName => _definition.DisplayName;
    public Vector2 InteractionPosition => _definition.InteractionPosition;

    public void Initialize(BedDefinition definition, InteriorBuildingRuntime building)
    {
        _definition = definition;
        _building = building;
        _texture = TextureRegistry.Get(definition.TexturePath);
        Position = IsometricGrid.GridToScreen(definition.Position);
        ZIndex = 0;
    }

    public override void _Ready() { AddToGroup(GroupName); QueueRedraw(); }
    public bool TryReserve(ulong survivorId)
    {
        if (_claimingSurvivor != 0 && _claimingSurvivor != survivorId) return false;
        _claimingSurvivor = survivorId;
        return true;
    }
    public void Release(ulong survivorId)
    {
        if (_claimingSurvivor == survivorId) _claimingSurvivor = 0;
    }
    public void MarkUsed() { _building.State.UsedFurniture.Add(Id); _building.NotifyStateChanged(); }
    public bool ContainsScreenPoint(Vector2 screenPoint)
    {
        Vector2 local=GetGlobalTransformWithCanvas().AffineInverse()*screenPoint;
        return new Rect2(-70,-110,140,120).HasPoint(local);
    }
    public override void _Draw()
    {
        float scale=_definition.TargetHeight/Mathf.Max(1,_texture.GetHeight());Vector2 size=_texture.GetSize()*scale;
        DrawTextureRect(_texture,new Rect2(-size.X*.5f,-size.Y,size.X,size.Y),false);
    }
}
