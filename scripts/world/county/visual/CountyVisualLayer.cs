#nullable enable

using System.Collections.Generic;
using Godot;
using AshwoodCounty.World.County.Visual.Authoring;

namespace AshwoodCounty.World.County.Visual;

/// <summary>
/// County art is retained in one CanvasItem per coordinate chunk. This is a
/// deliberate rendering boundary: Godot can cull remote art while the world
/// remains one continuous gameplay space.
///
/// Landscape chunks are now built on demand from the visible rectangle rather
/// than all at once, because the composition passes they run are far richer
/// than they used to be and the county is 120 chunks wide.
/// </summary>
public partial class CountyVisualLayer : Node2D
{
    private const float MinimumLandscapeZoom = .22f;
    private const float RefreshInterval = .2f;

    public bool DrawLocationLabels { get; init; }

    private readonly Dictionary<Vector2I, CountyVisualChunk> _chunks = [];
    private Node2D _landscapeRoot = null!;
    private double _elapsed = RefreshInterval;

    public override void _Ready()
    {
        ZAsRelative = false;
        ZIndex = -100;
        // Terrain must keep streaming while the simulation is paused: the
        // player can still pan and zoom, and GetTree().Paused is how pausing
        // is implemented.
        ProcessMode = ProcessModeEnum.Always;

        AddChild(new CountyGroundSurface { Name = "CountyGround" });
        AddChild(new CountyWaterLayer { Name = "AnimatedWater" });

        _landscapeRoot = new Node2D { Name = "Landscape", ZAsRelative = false, ZIndex = -100 };
        AddChild(_landscapeRoot);

        // Road materials are shared by every chunk, so loading them once here
        // keeps the first pan from streaming nine textures mid-frame.
        foreach (string path in RoadSurfacePalette.AllTextures)
            TextureRegistry.Get(path);

        // Structures sit above ground, roads and water. Their chunk draw
        // commands remain cullable, while actors continue to render on the
        // world's normal foreground layer.
        AddChild(new CountyAuthoredStructuresLayer { Name = "AuthoredStructures" });

        RefreshChunks();
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        if (_elapsed < RefreshInterval)
            return;
        _elapsed = 0;
        RefreshChunks();
    }

    private void RefreshChunks()
    {
        VisibleChunkTracker.Reconcile(
            _chunks,
            VisibleChunkTracker.Visible(this, 1, MinimumLandscapeZoom, 64),
            _landscapeRoot,
            coordinate =>
            {
                CountyVisualChunk chunk = new()
                {
                    Name = $"Landscape_{coordinate.X}_{coordinate.Y}",
                    DrawLocationLabels = DrawLocationLabels
                };
                chunk.Initialize(coordinate);
                return chunk;
            });
    }
}
