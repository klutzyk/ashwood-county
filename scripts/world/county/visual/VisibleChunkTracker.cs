#nullable enable

using System.Collections.Generic;
using Godot;

namespace AshwoodCounty.World.County.Visual;

/// <summary>
/// Works out which county chunks the camera can actually see.
///
/// County art is retained per chunk, so building all 120 of them up front means
/// paying for a landscape nobody is looking at. Both the ground and landscape
/// layers instead build only what is on screen plus a ring of margin, and drop
/// chunks again as they leave. Zooming far out returns nothing, which is the
/// natural level-of-detail cut: the baked macro ground still covers the county.
/// </summary>
public static class VisibleChunkTracker
{
    /// <summary>
    /// Chunks intersecting the visible rectangle of <paramref name="node"/>'s
    /// viewport, expanded by <paramref name="margin"/> chunks.
    /// </summary>
    public static HashSet<Vector2I> Visible(Node2D node, int margin, float minimumZoom, int maximumChunks)
    {
        HashSet<Vector2I> result = [];
        if (!node.IsInsideTree())
            return result;

        Viewport viewport = node.GetViewport();
        Transform2D canvas = viewport.GetCanvasTransform();
        if (Mathf.Abs(canvas.Scale.X) < minimumZoom)
            return result;

        Transform2D toLocal = (node.GetGlobalTransform() * canvas).AffineInverse();
        Vector2 size = viewport.GetVisibleRect().Size;
        Vector2[] corners =
        [
            toLocal * Vector2.Zero,
            toLocal * new Vector2(size.X, 0),
            toLocal * size,
            toLocal * new Vector2(0, size.Y)
        ];

        float minX = float.PositiveInfinity, minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;
        foreach (Vector2 corner in corners)
        {
            Vector2 grid = IsometricGrid.ScreenToGrid(corner);
            minX = Mathf.Min(minX, grid.X);
            minY = Mathf.Min(minY, grid.Y);
            maxX = Mathf.Max(maxX, grid.X);
            maxY = Mathf.Max(maxY, grid.Y);
        }

        int chunk = CountyCoordinateSpace.ChunkSize;
        int startX = Mathf.FloorToInt(minX / chunk) - margin;
        int startY = Mathf.FloorToInt(minY / chunk) - margin;
        int endX = Mathf.FloorToInt(maxX / chunk) + margin;
        int endY = Mathf.FloorToInt(maxY / chunk) + margin;

        // A pathological zoom-out is cut rather than allowed to build the whole
        // county's worth of retained art in a single frame.
        if ((endX - startX + 1) * (endY - startY + 1) > maximumChunks)
            return result;

        for (int y = startY; y <= endY; y++)
        {
            for (int x = startX; x <= endX; x++)
            {
                Vector2I coordinate = new(x, y);
                if (CountyCoordinateSpace.IsValidChunk(coordinate))
                    result.Add(coordinate);
            }
        }
        return result;
    }

    /// <summary>Reconcile a live chunk dictionary against a required set.</summary>
    public static void Reconcile<T>(
        Dictionary<Vector2I, T> live,
        HashSet<Vector2I> required,
        Node parent,
        System.Func<Vector2I, T> create) where T : Node
    {
        foreach (Vector2I coordinate in required)
        {
            if (live.ContainsKey(coordinate))
                continue;
            T node = create(coordinate);
            parent.AddChild(node);
            live[coordinate] = node;
        }

        List<Vector2I>? stale = null;
        foreach (Vector2I coordinate in live.Keys)
        {
            if (!required.Contains(coordinate))
                (stale ??= []).Add(coordinate);
        }
        if (stale is null)
            return;
        foreach (Vector2I coordinate in stale)
        {
            if (live.Remove(coordinate, out T? node))
                node.QueueFree();
        }
    }
}
