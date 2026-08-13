#nullable enable

using Godot;

namespace AshwoodCounty.World.Fog;

/// <summary>Draws one isolated section so visibility changes do not redraw the whole county.</summary>
internal sealed partial class CountyFogChunk : Node2D
{
    private CountyFogOfWar _fog = null!;
    private Vector2I _origin;
    private Vector2I _size;
    private ImageTexture? _texture;

    public void Configure(CountyFogOfWar fog, Vector2I origin, Vector2I size)
    {
        _fog = fog;
        _origin = origin;
        _size = size;
        TextureFilter = TextureFilterEnum.Linear;
    }

    public override void _Draw()
    {
        FogDebugMode debugMode = _fog.DebugMode;
        if (debugMode == FogDebugMode.RevealAll)
            return;

        if (debugMode == FogDebugMode.StateColors)
        {
            for (int y = _origin.Y; y < _origin.Y + _size.Y; y++)
            {
                for (int x = _origin.X; x < _origin.X + _size.X; x++)
                {
                    Vector2I cell = new(x, y);
                    FogCellVisibility state = _fog.GetCellVisibility(cell);
                    DrawColoredPolygon(IsometricGrid.CellDiamond(cell), _fog.GetDrawColor(state, debugMode));
                }
            }
            return;
        }

        // One tiny filtered texture replaces up to 400 per-cell polygons. Its
        // pixels are the same shared feather samples used previously, mapped
        // through the canonical isometric projection onto this chunk diamond.
        int width = _size.X + 1;
        int height = _size.Y + 1;
        Image image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        bool hasFog = false;
        for (int localY = 0; localY < height; localY++)
        {
            for (int localX = 0; localX < width; localX++)
            {
                Color color = _fog.GetFeatheredDrawColor(_origin + new Vector2I(localX, localY));
                image.SetPixel(localX, localY, color);
                hasFog |= color.A > .001f;
            }
        }

        if (!hasFog)
            return;

        if (_texture is null || _texture.GetWidth() != width || _texture.GetHeight() != height)
            _texture = ImageTexture.CreateFromImage(image);
        else
            _texture.Update(image);

        Vector2 origin = _origin;
        Vector2 size = _size;
        Vector2[] diamond = IsometricGrid.ProjectRectangle(origin, size);
        Color[] colors = [Colors.White, Colors.White, Colors.White, Colors.White];
        Vector2[] uvs = [Vector2.Zero, Vector2.Right, Vector2.One, Vector2.Down];
        DrawPolygon(diamond, colors, uvs, _texture);
    }
}
