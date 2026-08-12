using AshwoodCounty.Systems;
using AshwoodCounty.Resources;
using AshwoodCounty.World;
using Godot;

namespace AshwoodCounty.UI;

public partial class DebugHud : CanvasLayer
{
    private IsometricWorld _world = null!;
    private Label _gridValue = null!;
    private Label _zoomValue = null!;
    private Label _fpsValue = null!;
    private Label _selectedValue = null!;
    private SurvivorSelectionController _selection = null!;
    private SettlementInventory _inventory = null!;
    private Label _woodValue = null!;

    public override void _Ready()
    {
        _world = GetNode<IsometricWorld>("../World");
        _gridValue = GetNode<Label>("Panel/Margin/Rows/GridValue");
        _zoomValue = GetNode<Label>("Panel/Margin/Rows/ZoomValue");
        _fpsValue = GetNode<Label>("Panel/Margin/Rows/FpsValue");
        _selectedValue = GetNode<Label>("Panel/Margin/Rows/SelectedValue");
        _selection = GetNode<SurvivorSelectionController>("../SelectionController");
        _inventory = GetNode<SettlementInventory>("../SettlementInventory");
        _woodValue = GetNode<Label>("Panel/Margin/Rows/WoodValue");
    }

    public override void _Process(double delta)
    {
        Vector2I cell = _world.HoveredCell;
        _gridValue.Text = cell.X >= 0 ? $"{cell.X}, {cell.Y}" : "Outside map";
        _zoomValue.Text = $"{_world.CameraZoom:0.00}x";
        _fpsValue.Text = Engine.GetFramesPerSecond().ToString();
        _selectedValue.Text = _selection.SelectedCount.ToString();
        _woodValue.Text = _inventory.DevUnlimitedResources
            ? "Unlimited"
            : _inventory.GetAmount(ResourceType.Wood).ToString();
    }
}
