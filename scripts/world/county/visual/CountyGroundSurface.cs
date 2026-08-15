#nullable enable

using System.Collections.Generic;
using System.Linq;
using Godot;

namespace AshwoodCounty.World.County.Visual;

/// <summary>
/// The county floor, in two levels of detail.
///
/// Every chunk always carries a cheap slice of the baked macro colour bitmap so
/// the whole county has a continuous, correctly tinted base. Chunks the camera
/// can actually see additionally receive a layer of authored isometric ground
/// diamonds plus sparse detail scatter, which is what turns a flat colour field
/// into painted ground.
///
/// Detail chunks follow the visible rectangle rather than a survivor, so panning
/// and zooming behave, and they are torn down as soon as they leave view.
/// </summary>
public partial class CountyGroundSurface : Node2D
{
    private const string GroundTexturePath = "res://assets/art/terrain/county_ground.png";

    /// <summary>Beyond this zoom-out the tiled layer stops paying for itself.</summary>
    private const float MinimumDetailZoom = .30f;

    private const float DetailRefreshInterval = .2f;

    private readonly Dictionary<Vector2I, CountyGroundDetailChunk> _detailChunks = [];
    private Node2D _detailRoot = null!;
    private double _elapsed = DetailRefreshInterval;

    public override void _Ready()
    {
        ZAsRelative = false;
        ZIndex = -120;
        // See CountyVisualLayer: panning while paused must still build ground.
        ProcessMode = ProcessModeEnum.Always;

        Texture2D countyTexture = TextureRegistry.Get(GroundTexturePath);
        int columns = Mathf.CeilToInt(CountyCoordinateSpace.Width / (float)CountyCoordinateSpace.ChunkSize);
        int rows = Mathf.CeilToInt(CountyCoordinateSpace.Height / (float)CountyCoordinateSpace.ChunkSize);
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                CountyGroundChunk chunk = new() { Name = $"Ground_{x}_{y}" };
                chunk.Initialize(new Vector2I(x, y), countyTexture);
                AddChild(chunk);
            }
        }

        _detailRoot = new Node2D { Name = "GroundDetail", ZAsRelative = false, ZIndex = -118 };
        AddChild(_detailRoot);

        // Loading every diamond up front keeps the first detail chunk from
        // stuttering while it streams a dozen textures mid-pan.
        foreach (string path in GroundTilePalette.AllTextures())
            TextureRegistry.Get(path);
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        if (_elapsed < DetailRefreshInterval)
            return;
        _elapsed = 0;
        RefreshDetailChunks();
    }

    private void RefreshDetailChunks()
    {
        VisibleChunkTracker.Reconcile(
            _detailChunks,
            VisibleChunkTracker.Visible(this, 1, MinimumDetailZoom, 40),
            _detailRoot,
            coordinate =>
            {
                CountyGroundDetailChunk chunk = new() { Name = $"GroundDetail_{coordinate.X}_{coordinate.Y}" };
                chunk.Initialize(coordinate);
                return chunk;
            });
    }
}

/// <summary>Cheap always-on colour base: one textured quad per county chunk.</summary>
internal partial class CountyGroundChunk : Node2D
{
    private Rect2 _gridBounds;
    private Vector2 _canvasOrigin;
    private Texture2D? _texture;

    public void Initialize(Vector2I coordinate, Texture2D texture)
    {
        _gridBounds = CountyCoordinateSpace.ChunkGridBounds(coordinate);
        _canvasOrigin = IsometricGrid.GridToScreen(_gridBounds.Position);
        _texture = texture;
        Position = _canvasOrigin;
        ZAsRelative = false;
        ZIndex = -120;
    }

    public override void _Ready() => QueueRedraw();

    public override void _Draw()
    {
        if (_texture is null) return;
        Vector2[] points = IsometricGrid.ProjectRectangle(_gridBounds.Position, _gridBounds.Size)
            .Select(point => point - _canvasOrigin).ToArray();
        Vector2 boundsSize = CountyCoordinateSpace.GridBounds.Size;
        Vector2[] uv =
        [
            _gridBounds.Position / boundsSize,
            new Vector2(_gridBounds.End.X, _gridBounds.Position.Y) / boundsSize,
            _gridBounds.End / boundsSize,
            new Vector2(_gridBounds.Position.X, _gridBounds.End.Y) / boundsSize
        ];
        Color[] colors = [Colors.White, Colors.White, Colors.White, Colors.White];
        DrawPolygon(points, colors, uv, _texture);
    }

    public override void _ExitTree()
    {
        // Draw commands store the texture RID, but a local managed reference
        // is only needed while this chunk is alive. TextureRegistry remains
        // the single long-lived owner.
        _texture = null;
    }
}

/// <summary>
/// Painted ground for one visible chunk: authored isometric diamonds laid on a
/// two-cell lattice, then a thin pass of detail scatter.
///
/// Draws are grouped by texture so the renderer can batch them, and each
/// diamond is tinted a little towards the region's colour so tiles from
/// different sources still read as one landscape.
/// </summary>
internal partial class CountyGroundDetailChunk : Node2D
{
    /// <summary>Ground lattice pitch in cells. Two keeps the art near 1:1.</summary>
    private const int Block = 2;

    /// <summary>Diamonds are drawn oversized so the lattice never shows through.</summary>
    private const float Bleed = 1.22f;

    private Rect2 _gridBounds;
    private Vector2 _canvasOrigin;

    public void Initialize(Vector2I coordinate)
    {
        _gridBounds = CountyCoordinateSpace.ChunkGridBounds(coordinate);
        _canvasOrigin = IsometricGrid.GridToScreen(_gridBounds.Position);
        Position = _canvasOrigin;
        ZAsRelative = false;
        ZIndex = -118;
    }

    public override void _Ready() => QueueRedraw();

    public override void _Draw()
    {
        DrawBaseDiamonds();
        DrawDetailScatter();
    }

    private void DrawBaseDiamonds()
    {
        // Collected per texture first: the renderer batches consecutive
        // commands that share a texture, and ground diamonds are flat, so
        // grouping costs nothing visually and saves a lot of state changes.
        Dictionary<string, List<(Vector2 Center, Color Tint, float Scale)>> byTexture = [];

        int startX = Mathf.FloorToInt(_gridBounds.Position.X);
        int startY = Mathf.FloorToInt(_gridBounds.Position.Y);
        int endX = Mathf.CeilToInt(_gridBounds.End.X);
        int endY = Mathf.CeilToInt(_gridBounds.End.Y);

        for (int y = startY; y < endY; y += Block)
        {
            for (int x = startX; x < endX; x += Block)
            {
                Vector2 lattice = new(x + Block * .5f, y + Block * .5f);
                GroundSurface surface = CountyTerrain.SurfaceAt(lattice);
                string? path = GroundTilePalette.Select(surface, x, y);
                if (path is null)
                    continue;

                // Jittering position and size breaks the perfectly regular
                // diamond lattice that would otherwise read as a tile grid.
                // The generous bleed above absorbs the offset without gaps.
                Vector2 center = lattice + new Vector2(
                    (CountyTerrain.Hash01(x, y, 811) - .5f) * .34f,
                    (CountyTerrain.Hash01(x, y, 823) - .5f) * .34f);
                float scale = .96f + CountyTerrain.Hash01(x, y, 829) * .16f;

                Color region = CountyTerrain.RegionColor(lattice);
                // A firm pull towards the regional colour plus a small value
                // jitter; enough to unify sources without washing out the art.
                Color tint = Colors.White.Lerp(region.Lightened(.50f), .40f);
                float shade = (CountyTerrain.Hash01(x, y, 233) - .5f) * .09f;
                tint = shade >= 0 ? tint.Lightened(shade) : tint.Darkened(-shade);

                if (!byTexture.TryGetValue(path, out List<(Vector2, Color, float)>? list))
                {
                    list = [];
                    byTexture[path] = list;
                }
                list.Add((center, tint, scale));
            }
        }

        Vector2 unit = new(Block * IsometricGrid.TileWidth * Bleed, Block * IsometricGrid.TileHeight * Bleed);
        foreach ((string path, List<(Vector2 Center, Color Tint, float Scale)> entries) in byTexture)
        {
            Texture2D texture = TextureRegistry.Get(path);
            if (texture is null)
                continue;
            foreach ((Vector2 center, Color tint, float scale) in entries)
            {
                Vector2 size = unit * scale;
                DrawTextureRect(texture, new Rect2(P(center) - size * .5f, size), false, tint);
            }
        }
    }

    private void DrawDetailScatter()
    {
        int startX = Mathf.FloorToInt(_gridBounds.Position.X);
        int startY = Mathf.FloorToInt(_gridBounds.Position.Y);
        int endX = Mathf.CeilToInt(_gridBounds.End.X);
        int endY = Mathf.CeilToInt(_gridBounds.End.Y);

        Dictionary<string, List<(Vector2 Point, float Scale, Color Tint)>> byTexture = [];

        for (int y = startY; y < endY; y += 3)
        {
            for (int x = startX; x < endX; x += 3)
            {
                if (CountyTerrain.Hash01(x, y, 401) > .34f)
                    continue;
                Vector2 point = new(
                    x + 1.5f + (CountyTerrain.Hash01(x, y, 403) - .5f) * 2.4f,
                    y + 1.5f + (CountyTerrain.Hash01(x, y, 407) - .5f) * 2.4f);
                if (!_gridBounds.HasPoint(point))
                    continue;

                GroundSurface surface = CountyTerrain.SurfaceAt(point);
                string[]? family = GroundTilePalette.DetailFor(surface);
                if (family is null)
                    continue;

                string path = family[(int)(CountyTerrain.Hash01(x, y, 409) * family.Length) % family.Length];
                float scale = .30f + CountyTerrain.Hash01(x, y, 411) * .26f;
                Color tint = new(1, 1, 1, .42f + CountyTerrain.Hash01(x, y, 413) * .24f);
                if (!byTexture.TryGetValue(path, out List<(Vector2, float, Color)>? list))
                {
                    list = [];
                    byTexture[path] = list;
                }
                list.Add((point, scale, tint));
            }
        }

        foreach ((string path, List<(Vector2 Point, float Scale, Color Tint)> entries) in byTexture)
        {
            Texture2D texture = TextureRegistry.Get(path);
            if (texture is null)
                continue;
            Vector2 source = texture.GetSize();
            foreach ((Vector2 point, float scale, Color tint) in entries)
            {
                Vector2 size = source * scale;
                DrawTextureRect(texture, new Rect2(P(point) - size * .5f, size), false, tint);
            }
        }
    }

    private Vector2 P(Vector2 gridPoint) => IsometricGrid.GridToScreen(gridPoint) - _canvasOrigin;
}
