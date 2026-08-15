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

    /// <summary>Detail chunks currently built. Used by streaming validation.</summary>
    public IReadOnlyCollection<Vector2I> DetailChunks => _detailChunks.Keys;
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
    private const float Bleed = 1.28f;

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

        int startX = CountyTerrain.LatticeStart(_gridBounds.Position.X, Block);
        int startY = CountyTerrain.LatticeStart(_gridBounds.Position.Y, Block);
        int endX = Mathf.CeilToInt(_gridBounds.End.X);
        int endY = Mathf.CeilToInt(_gridBounds.End.Y);

        for (int y = startY; y < endY; y += Block)
        {
            for (int x = startX; x < endX; x += Block)
            {
                Vector2 lattice = new(x + Block * .5f, y + Block * .5f);
                GroundSurface surface = CountyTerrain.SurfaceAt(lattice);
                string? path = GroundTilePalette.Select(surface, lattice, x, y);
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
                // A firm pull towards the regional colour unifies tiles from
                // different sources. Value variation comes from a broad noise
                // field rather than a per-tile hash, so the ground gains large
                // soft light and shade masses instead of a speckled lattice.
                Color tint = Colors.White.Lerp(region.Lightened(.52f), .46f);
                float variation = (CountyTerrain.Fbm(lattice, 1f / 26f, 233) - .5f) * .11f;
                tint = variation >= 0 ? tint.Lightened(variation) : tint.Darkened(-variation);

                // Canopy shade is the macro read: forest floors sit dark and
                // slightly cool, open country stays bright. Without it every
                // region is lit identically and the landscape has no hierarchy
                // above the level of individual tiles.
                float canopy = CountyTerrain.CanopyShade(lattice);
                if (canopy > .01f)
                    tint = tint.Darkened(canopy * .30f).Lerp(new Color(.62f, .70f, .64f, tint.A), canopy * .22f);

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

    /// <summary>
    /// High-frequency ground detail, deliberately rationed.
    ///
    /// Detail earns its place by being uncommon: it is gated behind a broad
    /// noise field so litter and scatter gather into occasional drifts, leaving
    /// most of the surface calm. Spraying it evenly over every surface is what
    /// made the ground read as texture soup.
    /// </summary>
    private const int DetailStep = 4;

    private void DrawDetailScatter()
    {
        int startX = CountyTerrain.LatticeStart(_gridBounds.Position.X, DetailStep);
        int startY = CountyTerrain.LatticeStart(_gridBounds.Position.Y, DetailStep);
        int endX = Mathf.CeilToInt(_gridBounds.End.X);
        int endY = Mathf.CeilToInt(_gridBounds.End.Y);

        Dictionary<string, List<(Vector2 Point, float Scale, Color Tint)>> byTexture = [];

        for (int y = startY; y < endY; y += DetailStep)
        {
            for (int x = startX; x < endX; x += DetailStep)
            {
                Vector2 point = new(
                    x + DetailStep * .5f + (CountyTerrain.Hash01(x, y, 403) - .5f) * 2.8f,
                    y + DetailStep * .5f + (CountyTerrain.Hash01(x, y, 407) - .5f) * 2.8f);
                if (!_gridBounds.HasPoint(point))
                    continue;

                // Drift field: high where litter would collect, low elsewhere.
                float drift = CountyTerrain.Fbm(point, 1f / 17f, 401);
                if (drift < .56f || CountyTerrain.Hash01(x, y, 417) > (drift - .56f) * 2.4f)
                    continue;

                GroundSurface surface = CountyTerrain.SurfaceAt(point);
                string[]? family = GroundTilePalette.DetailFor(surface);
                if (family is null)
                    continue;

                string path = family[(int)(CountyTerrain.Hash01(x, y, 409) * family.Length) % family.Length];
                float scale = .32f + CountyTerrain.Hash01(x, y, 411) * .30f;
                Color tint = new(1, 1, 1, .30f + CountyTerrain.Hash01(x, y, 413) * .20f);
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
