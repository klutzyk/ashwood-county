using AshwoodCounty.Systems;
using AshwoodCounty.Resources;
using AshwoodCounty.World;
using Godot;
using System.Linq;
using AshwoodCounty.Threats;

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
    private Label _activityValue = null!;
    private Label _foodValue = null!;
    private Label _hungerValue = null!;
    private Label _timeValue = null!;
    private Label _speedValue = null!;
    private Label _healthValue = null!;
    private Label _threatValue = null!;
    private Systems.GameClock _clock = null!;
    private Systems.SimulationController _simulation = null!;

    public override void _Ready()
    {
        Visible = false;
        _world = GetNode<IsometricWorld>("../World");
        _gridValue = GetNode<Label>("Panel/Margin/Rows/GridValue");
        _zoomValue = GetNode<Label>("Panel/Margin/Rows/ZoomValue");
        _fpsValue = GetNode<Label>("Panel/Margin/Rows/FpsValue");
        _selectedValue = GetNode<Label>("Panel/Margin/Rows/SelectedValue");
        _selection = GetNode<SurvivorSelectionController>("../SelectionController");
        _inventory = GetNode<SettlementInventory>("../SettlementInventory");
        _woodValue = GetNode<Label>("Panel/Margin/Rows/WoodValue");
        _activityValue = GetNode<Label>("Panel/Margin/Rows/ActivityValue");
        _foodValue = GetNode<Label>("Panel/Margin/Rows/FoodValue");
        _hungerValue = GetNode<Label>("Panel/Margin/Rows/HungerValue");
        _timeValue = GetNode<Label>("Panel/Margin/Rows/TimeValue");
        _speedValue = GetNode<Label>("Panel/Margin/Rows/SpeedValue");
        _healthValue = GetNode<Label>("Panel/Margin/Rows/HealthValue");
        _threatValue = GetNode<Label>("Panel/Margin/Rows/ThreatValue");
        _clock = GetNode<Systems.GameClock>("../GameClock");
        _simulation = GetNode<Systems.SimulationController>("../SimulationController");
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (inputEvent is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.F3)
        {
            Visible = !Visible;
            GetViewport().SetInputAsHandled();
        }
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
        _activityValue.Text = _selection.SelectedCount == 1
            ? _selection.SelectedSurvivors[0].Activity
            : "--";
        _foodValue.Text = _inventory.DevUnlimitedResources ? "Unlimited" : _inventory.GetAmount(ResourceType.Food).ToString();
        _hungerValue.Text = _selection.SelectedCount == 1 ? $"{_selection.SelectedSurvivors[0].Hunger:0}%" : "--";
        _timeValue.Text = _clock.DisplayTime;
        _speedValue.Text = _simulation.IsPaused ? "Paused" : $"{_simulation.Speed}x";
        _healthValue.Text=_selection.SelectedCount==1?$"{_selection.SelectedSurvivors[0].Health:0}/{_selection.SelectedSurvivors[0].MaxHealth:0}":"--";
        _threatValue.Text=GetTree().GetNodesInGroup(Zombie.GroupName).OfType<Zombie>().Count(z=>z.IsAlive).ToString();
    }
}
