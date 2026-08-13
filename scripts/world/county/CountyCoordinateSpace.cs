using System;
using System.Collections.Generic;
using Godot;

namespace AshwoodCounty.World.County;

/// <summary>
/// The one finite simulation coordinate space used by the continuous county.
/// Coordinates are gameplay grid coordinates; IsometricGrid remains the only
/// projection between gameplay space and the rendered canvas.
/// </summary>
public static class CountyCoordinateSpace
{
    public const int Width = 384;
    public const int Height = 320;
    public const int ChunkSize = 32;

    public static readonly Rect2 GridBounds = new(Vector2.Zero, new Vector2(Width, Height));
    public static readonly Vector2 StartingCamp = new(203, 157);

    public static Vector2 ClampToCounty(Vector2 gridPosition)
    {
        const float edgeEpsilon = 0.001f;
        return new Vector2(
            Mathf.Clamp(gridPosition.X, 0, Width - edgeEpsilon),
            Mathf.Clamp(gridPosition.Y, 0, Height - edgeEpsilon));
    }

    public static bool Contains(Vector2 gridPosition) => GridBounds.HasPoint(gridPosition);

    public static Vector2I GridToChunk(Vector2 gridPosition)
    {
        Vector2 clamped = ClampToCounty(gridPosition);
        return new Vector2I(
            Mathf.FloorToInt(clamped.X / ChunkSize),
            Mathf.FloorToInt(clamped.Y / ChunkSize));
    }

    public static Rect2 ChunkGridBounds(Vector2I chunk)
    {
        Vector2 position = new(chunk.X * ChunkSize, chunk.Y * ChunkSize);
        Vector2 end = new(
            Mathf.Min(position.X + ChunkSize, Width),
            Mathf.Min(position.Y + ChunkSize, Height));
        return new Rect2(position, end - position);
    }

    public static bool IsValidChunk(Vector2I chunk) =>
        chunk.X >= 0 && chunk.Y >= 0 &&
        chunk.X * ChunkSize < Width && chunk.Y * ChunkSize < Height;

    public static IEnumerable<Vector2I> ChunksAround(Vector2 gridPosition, int radius)
    {
        Vector2I center = GridToChunk(gridPosition);
        int safeRadius = Mathf.Max(0, radius);
        for (int y = center.Y - safeRadius; y <= center.Y + safeRadius; y++)
        {
            for (int x = center.X - safeRadius; x <= center.X + safeRadius; x++)
            {
                Vector2I chunk = new(x, y);
                if (IsValidChunk(chunk))
                    yield return chunk;
            }
        }
    }

    public static Rect2 ProjectedCanvasBounds()
    {
        Vector2[] corners = IsometricGrid.ProjectRectangle(Vector2.Zero, GridBounds.Size);
        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;
        foreach (Vector2 point in corners)
        {
            minX = Mathf.Min(minX, point.X);
            minY = Mathf.Min(minY, point.Y);
            maxX = Mathf.Max(maxX, point.X);
            maxY = Mathf.Max(maxY, point.Y);
        }

        return new Rect2(minX, minY, maxX - minX, maxY - minY);
    }
}
