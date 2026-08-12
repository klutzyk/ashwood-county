using AshwoodCounty.World;
using Godot;

namespace AshwoodCounty.Buildings;

public partial class BuildingPlacementPreview : Node2D
{
    private BuildingDefinition _definition = BuildingCatalog.Shelter;
    private Vector2I _origin;
    private bool _isValid;
    private ShelterPlaceholderVisual _ghost = null!;

    public override void _Ready()
    {
        _ghost = new ShelterPlaceholderVisual();
        AddChild(_ghost);
        RefreshGhost();
    }

    public void UpdatePreview(BuildingDefinition definition, Vector2I origin, bool isValid)
    {
        _definition = definition;
        _origin = origin;
        _isValid = isValid;
        RefreshGhost();
        QueueRedraw();
    }

    public override void _Draw()
    {
        Color fill = _isValid ? new Color(0.25f, 0.9f, 0.38f, 0.26f) : new Color(0.95f, 0.25f, 0.2f, 0.3f);
        Color outline = _isValid ? new Color("#69ed7d") : new Color("#ff6158");
        for (int y = 0; y < _definition.Footprint.Y; y++)
        {
            for (int x = 0; x < _definition.Footprint.X; x++)
            {
                Vector2[] diamond = IsometricGrid.CellDiamond(_origin + new Vector2I(x, y));
                DrawColoredPolygon(diamond, fill);
                DrawPolyline([diamond[0], diamond[1], diamond[2], diamond[3], diamond[0]], outline, 2, true);
            }
        }
    }

    private void RefreshGhost()
    {
        if (!IsInstanceValid(_ghost))
        {
            return;
        }

        _ghost.Position = BuildingGridProjection.GetRenderAnchor(_origin, _definition.Footprint);
        _ghost.Modulate = _isValid ? new Color(0.55f, 1, 0.62f, 0.72f) : new Color(1, 0.4f, 0.36f, 0.72f);
        _ghost.QueueRedraw();
    }
}
