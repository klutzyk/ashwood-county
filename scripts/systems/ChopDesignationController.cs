using System.Linq;
using AshwoodCounty.Resources;
using Godot;

namespace AshwoodCounty.Systems;

public partial class ChopDesignationController : CanvasLayer
{
    private Button _chopButton = null!;
    private Button _forageButton = null!;
    private Label _status = null!;
    public bool IsDesignationActive { get; private set; }
    public ResourceType DesignatedResourceType { get; private set; } = ResourceType.Wood;

    public override void _Ready()
    {
        _chopButton = GetNode<Button>("Panel/Margin/Rows/ChopButton");
        _forageButton = GetNode<Button>("Panel/Margin/Rows/ForageButton");
        _status = GetNode<Label>("Panel/Margin/Rows/Status");
        _chopButton.Pressed += () => ToggleMode(ResourceType.Wood);
        _forageButton.Pressed += () => ToggleMode(ResourceType.Food);
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (!IsDesignationActive) return;
        if (inputEvent is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
        { EndDesignation(); GetViewport().SetInputAsHandled(); }
        else if (inputEvent is InputEventMouseButton mouse && mouse.Pressed)
        {
            if (mouse.ButtonIndex == MouseButton.Right) EndDesignation();
            else if (mouse.ButtonIndex == MouseButton.Left) ToggleResourceAt(mouse.Position);
            GetViewport().SetInputAsHandled();
        }
    }

    public void ToggleDesignation() => ToggleMode(ResourceType.Wood);
    public void ToggleForageDesignation() => ToggleMode(ResourceType.Food);

    private void ToggleMode(ResourceType resourceType)
    {
        bool turnOff = IsDesignationActive && DesignatedResourceType == resourceType;
        IsDesignationActive = !turnOff;
        DesignatedResourceType = resourceType;
        _chopButton.ButtonPressed = IsDesignationActive && resourceType == ResourceType.Wood;
        _forageButton.ButtonPressed = IsDesignationActive && resourceType == ResourceType.Food;
        string noun = resourceType == ResourceType.Wood ? "trees" : "food bushes";
        _status.Text = IsDesignationActive ? $"Click {noun} to designate • Esc/right-click exits" : "Designate settlement work";
    }

    public bool ToggleTreeAt(Vector2 screenPosition) => ToggleResourceAt(screenPosition);
    public bool ToggleResourceAt(Vector2 screenPosition)
    {
        HarvestableResource resource = GetTree().GetNodesInGroup(HarvestableResource.GroupName).OfType<HarvestableResource>()
            .Where(item => item.ResourceType == DesignatedResourceType && item.IsHarvestable && item.ContainsScreenPoint(screenPosition))
            .OrderBy(item => item.Position.Y).LastOrDefault();
        if (resource is null) return false;
        resource.SetHarvestDesignated(!resource.IsDesignatedForHarvest);
        string action = DesignatedResourceType == ResourceType.Wood ? "chopping" : "foraging";
        _status.Text = resource.IsDesignatedForHarvest ? $"Resource designated for {action}" : "Resource designation removed";
        return true;
    }

    public void EndDesignation()
    {
        IsDesignationActive = false;
        _chopButton.ButtonPressed = false;
        _forageButton.ButtonPressed = false;
        _status.Text = "Designate settlement work";
    }
}
