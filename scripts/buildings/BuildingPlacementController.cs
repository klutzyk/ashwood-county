using AshwoodCounty.Resources;
using AshwoodCounty.World;
using Godot;

namespace AshwoodCounty.Buildings;

public partial class BuildingPlacementController : CanvasLayer
{
    private IsometricWorld _world = null!;
    private GridOccupancy _occupancy = null!;
    private SettlementInventory _inventory = null!;
    private Node2D _objects = null!;
    private Node2D _effects = null!;
    private Button _shelterButton = null!;
    private Label _feedbackLabel = null!;
    private BuildingPlacementPreview _preview = null!;
    private BuildingDefinition _activeDefinition = null!;

    public bool IsPlacementActive { get; private set; }
    public Vector2 CurrentPosition { get; private set; }
    public bool IsCurrentPlacementValid { get; private set; }
    public string CurrentFeedback { get; private set; } = string.Empty;

    public override void _Ready()
    {
        _world = GetNode<IsometricWorld>("../World");
        _occupancy = GetNode<GridOccupancy>("../GridOccupancy");
        _inventory = GetNode<SettlementInventory>("../SettlementInventory");
        _objects = GetNode<Node2D>("../World/Objects");
        _effects = GetNode<Node2D>("../World/Effects");
        _shelterButton = GetNode<Button>("BuildPanel/Margin/Rows/ShelterButton");
        _feedbackLabel = GetNode<Label>("BuildPanel/Margin/Rows/FeedbackLabel");
        _shelterButton.Pressed += BeginShelterPlacement;
        SetFeedback("Shelter • Cost: 30 Wood");
    }

    public override void _Process(double delta)
    {
        if (!IsPlacementActive)
        {
            return;
        }

        UpdatePlacement(_world.ScreenToGridPosition(GetViewport().GetMousePosition()));
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (!IsPlacementActive)
        {
            return;
        }

        if (inputEvent is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo && keyEvent.Keycode == Key.Escape)
        {
            CancelPlacement();
            GetViewport().SetInputAsHandled();
        }
        else if (inputEvent is InputEventMouseButton mouseButton && mouseButton.Pressed)
        {
            if (mouseButton.ButtonIndex == MouseButton.Right)
            {
                CancelPlacement();
                GetViewport().SetInputAsHandled();
            }
            else if (mouseButton.ButtonIndex == MouseButton.Left)
            {
                TryConfirmPlacement();
                GetViewport().SetInputAsHandled();
            }
        }
    }

    public void BeginShelterPlacement()
    {
        BeginPlacement(BuildingCatalog.Shelter);
    }

    public void BeginPlacement(BuildingDefinition definition)
    {
        CancelPlacement(false);
        _activeDefinition = definition;
        IsPlacementActive = true;
        _preview = new BuildingPlacementPreview();
        _effects.AddChild(_preview);
        UpdatePlacement(CurrentPosition);
    }

    public void UpdatePlacement(Vector2 position)
    {
        if (!IsPlacementActive)
        {
            return;
        }

        CurrentPosition = position;
        WorldFootprint footprint = new(position, _activeDefinition.FootprintSize);
        PlacementFailure failure = _occupancy.Validate(footprint);
        if (failure == PlacementFailure.OutsideMap)
        {
            IsCurrentPlacementValid = false;
            SetFeedback("Cannot place: outside map boundary");
        }
        else if (failure == PlacementFailure.Occupied)
        {
            IsCurrentPlacementValid = false;
            SetFeedback("Cannot place: occupied cells");
        }
        else if (_inventory.GetAmount(_activeDefinition.CostResource) < _activeDefinition.ResourceCost)
        {
            IsCurrentPlacementValid = false;
            SetFeedback($"Need {_activeDefinition.ResourceCost} Wood");
        }
        else
        {
            IsCurrentPlacementValid = true;
            SetFeedback("Valid placement • Left-click to build");
        }

        _preview.UpdatePreview(_activeDefinition, position, IsCurrentPlacementValid);
    }

    public bool TryConfirmPlacement()
    {
        if (!IsPlacementActive)
        {
            return false;
        }

        UpdatePlacement(CurrentPosition);
        if (!IsCurrentPlacementValid || !_inventory.TrySpend(_activeDefinition.CostResource, _activeDefinition.ResourceCost))
        {
            return false;
        }

        PackedScene packedScene = GD.Load<PackedScene>(_activeDefinition.ConstructionSiteScenePath);
        ConstructionSite site = packedScene.Instantiate<ConstructionSite>();
        site.Initialize(_activeDefinition, CurrentPosition, _occupancy, _inventory);
        if (!_occupancy.TryOccupy(site, site.OccupancyFootprint))
        {
            _inventory.Add(_activeDefinition.CostResource, _activeDefinition.ResourceCost);
            site.QueueFree();
            UpdatePlacement(CurrentPosition);
            return false;
        }

        _objects.AddChild(site);
        CancelPlacement(false);
        SetFeedback("Shelter site placed • Right-click with survivors to build");
        return true;
    }

    public void CancelPlacement()
    {
        CancelPlacement(true);
    }

    public void ShowStatus(string message)
    {
        SetFeedback(message);
    }

    private void CancelPlacement(bool showMessage)
    {
        IsPlacementActive = false;
        IsCurrentPlacementValid = false;
        if (IsInstanceValid(_preview))
        {
            _preview.QueueFree();
        }

        _preview = null!;
        if (showMessage)
        {
            SetFeedback("Placement cancelled • No resources spent");
        }
    }

    private void SetFeedback(string message)
    {
        CurrentFeedback = message;
        if (IsInstanceValid(_feedbackLabel))
        {
            _feedbackLabel.Text = message;
        }
    }
}
