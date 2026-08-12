using AshwoodCounty.World;
using Godot;

namespace AshwoodCounty.Buildings;

public partial class BuildingPlacementPreview : Node2D
{
    private BuildingDefinition _definition = BuildingCatalog.Shelter;
    private Vector2 _position;
    private bool _isValid;
    private ShelterPlaceholderVisual _ghost = null!;

    public override void _Ready()
    {
        _ghost = new ShelterPlaceholderVisual();
        AddChild(_ghost);
        RefreshGhost();
    }

    public void UpdatePreview(BuildingDefinition definition, Vector2 position, bool isValid)
    {
        _definition = definition;
        _position = position;
        _isValid = isValid;
        RefreshGhost();
        QueueRedraw();
    }

    public override void _Draw()
    {
        Color fill = _isValid ? new Color(0.25f, 0.9f, 0.38f, 0.26f) : new Color(0.95f, 0.25f, 0.2f, 0.3f);
        Color outline = _isValid ? new Color("#69ed7d") : new Color("#ff6158");
        Vector2[] footprint = IsometricGrid.ProjectRectangle(_position, _definition.FootprintSize);
        DrawColoredPolygon(footprint, fill);
        DrawPolyline([footprint[0], footprint[1], footprint[2], footprint[3], footprint[0]], outline, 2, true);
    }

    private void RefreshGhost()
    {
        if (!IsInstanceValid(_ghost))
        {
            return;
        }

        _ghost.Position = BuildingGridProjection.GetRenderAnchor(_position, _definition.FootprintSize);
        _ghost.Modulate = _isValid ? new Color(0.55f, 1, 0.62f, 0.72f) : new Color(1, 0.4f, 0.36f, 0.72f);
        _ghost.QueueRedraw();
    }
}
