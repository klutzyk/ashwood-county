using System.Linq;
using AshwoodCounty.Resources;
using Godot;

namespace AshwoodCounty.Systems;

public partial class ChopDesignationController : CanvasLayer
{
    private Button _button = null!;
    private Label _status = null!;
    public bool IsDesignationActive { get; private set; }

    public override void _Ready()
    {
        _button = GetNode<Button>("Panel/Margin/Rows/ChopButton");
        _status = GetNode<Label>("Panel/Margin/Rows/Status");
        _button.Pressed += ToggleDesignation;
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (!IsDesignationActive)
        {
            return;
        }

        if (inputEvent is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
        {
            EndDesignation();
            GetViewport().SetInputAsHandled();
        }
        else if (inputEvent is InputEventMouseButton mouse && mouse.Pressed)
        {
            if (mouse.ButtonIndex == MouseButton.Right)
            {
                EndDesignation();
            }
            else if (mouse.ButtonIndex == MouseButton.Left)
            {
                ToggleTreeAt(mouse.Position);
            }

            GetViewport().SetInputAsHandled();
        }
    }

    public void ToggleDesignation()
    {
        IsDesignationActive = !IsDesignationActive;
        _button.ButtonPressed = IsDesignationActive;
        _status.Text = IsDesignationActive ? "Click trees to designate • Esc/right-click exits" : "Designate harvestable trees";
    }

    public bool ToggleTreeAt(Vector2 screenPosition)
    {
        HarvestableResource tree = GetTree().GetNodesInGroup(HarvestableResource.GroupName)
            .OfType<HarvestableResource>()
            .Where(resource => resource.IsHarvestable && resource.ContainsScreenPoint(screenPosition))
            .OrderBy(resource => resource.Position.Y)
            .LastOrDefault();
        if (tree is null)
        {
            return false;
        }

        tree.SetChopDesignated(!tree.IsDesignatedForChop);
        _status.Text = tree.IsDesignatedForChop ? "Tree designated for chopping" : "Tree designation removed";
        return true;
    }

    public void EndDesignation()
    {
        IsDesignationActive = false;
        _button.ButtonPressed = false;
        _status.Text = "Designate harvestable trees";
    }
}
