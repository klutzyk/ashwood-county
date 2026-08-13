using Godot;

namespace AshwoodCounty.World.Fog;

/// <summary>Draws one isolated section so visibility changes do not redraw the whole county.</summary>
internal sealed partial class CountyFogChunk : Node2D
{
    private CountyFogOfWar _fog = null!;
    private Vector2I _origin;
    private Vector2I _size;

    public void Configure(CountyFogOfWar fog, Vector2I origin, Vector2I size)
    {
        _fog = fog;
        _origin = origin;
        _size = size;
    }

    public override void _Draw()
    {
        FogDebugMode debugMode = _fog.DebugMode;
        if (debugMode == FogDebugMode.RevealAll)
            return;

        for (int y = _origin.Y; y < _origin.Y + _size.Y; y++)
        {
            for (int x = _origin.X; x < _origin.X + _size.X; x++)
            {
                Vector2I cell = new(x, y);
                FogCellVisibility state = _fog.GetCellVisibility(cell);
                Color color = _fog.GetDrawColor(state, debugMode);
                if (color.A <= 0.001f)
                    continue;

                DrawColoredPolygon(IsometricGrid.CellDiamond(cell), color);
            }
        }
    }
}
