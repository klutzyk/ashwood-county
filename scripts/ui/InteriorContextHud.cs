#nullable enable

using System.Linq;
using AshwoodCounty.Buildings.Interiors;
using AshwoodCounty.World;
using Godot;

namespace AshwoodCounty.UI;

/// <summary>Small hover-only building readout; it never occupies the main HUD permanently.</summary>
public partial class InteriorContextHud : CanvasLayer
{
    private PanelContainer _panel = null!;
    private Label _label = null!;
    private IsometricWorld _world = null!;
    private double _refresh;

    public override void _Ready()
    {
        Layer = 11;
        _world = GetNode<IsometricWorld>("../World");
        _panel = new PanelContainer
        {
            Theme = AshwoodTheme.Create(),
            ThemeTypeVariation = "HudToastPanel",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        AddChild(_panel);
        _panel.AnchorLeft = 1;
        _panel.AnchorRight = 1;
        _panel.OffsetLeft = -330;
        _panel.OffsetTop = 82;
        _panel.OffsetRight = -20;
        _panel.OffsetBottom = 144;
        MarginContainer margin = new();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        _panel.AddChild(margin);
        _label = new Label
        {
            ThemeTypeVariation = "HudTiny",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        margin.AddChild(_label);
        _panel.Visible = false;
    }

    public override void _Process(double delta)
    {
        _refresh -= delta;
        if (_refresh > 0) return;
        _refresh = .12;
        Vector2 mouseGrid = _world.ScreenToGridPosition(GetViewport().GetMousePosition());
        InteriorBuildingRuntime? building = GetTree().GetNodesInGroup(InteriorBuildingRuntime.GroupName)
            .OfType<InteriorBuildingRuntime>().FirstOrDefault(candidate => candidate.Definition.Footprint.Grow(.35f).HasPoint(mouseGrid));
        _panel.Visible = building is not null;
        if (building is not null)
            _label.Text = building.ContextSummary();
    }
}
