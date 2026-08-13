#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace AshwoodCounty.World.County.Visual;

/// <summary>Reusable animated water surfaces, drawn independently from gameplay collision.</summary>
public partial class CountyWaterLayer : Node2D
{
    private static readonly Vector2[] MillCreek =
    [
        new(190, 214), new(181, 220), new(176, 230), new(166, 238),
        new(159, 248), new(151, 257), new(142, 269), new(129, 277)
    ];

    private static readonly Vector2[] OldMillTributary =
    [
        new(150, 111), new(158, 116), new(166, 121), new(174, 126), new(183, 130)
    ];

    public override void _Ready()
    {
        ZAsRelative = false;
        ZIndex = -88;
        BuildLake();
        BuildFlow("BlackwaterRiver", CountyMacroLayout.BlackwaterRiver, .95f, WaterMaterialLibrary.River);
        BuildFlow("MillCreek", MillCreek, .48f, WaterMaterialLibrary.Creek);
        BuildFlow("OldMillTributary", OldMillTributary, .38f, WaterMaterialLibrary.Creek);
        BuildPonds();
    }

    private void BuildLake()
    {
        Polygon2D lake = new()
        {
            Name = "BlackwaterLakeSurface",
            Polygon = CountyMacroLayout.BlackwaterLake.Select(IsometricGrid.GridToScreen).ToArray(),
            Color = Colors.White,
            Material = WaterMaterialLibrary.Lake
        };
        AddChild(lake);
    }

    private void BuildFlow(string name, Vector2[] points, float halfWidth, Material material)
    {
        Node2D root = new() { Name = name };
        AddChild(root);

        for (int index = 0; index < points.Length - 1; index++)
        {
            Vector2 start = points[index];
            Vector2 end = points[index + 1];
            float length = start.DistanceTo(end);
            int subdivisions = Mathf.Max(1, Mathf.CeilToInt(length / 5f));
            for (int piece = 0; piece < subdivisions; piece++)
            {
                Vector2 a = start.Lerp(end, piece / (float)subdivisions);
                Vector2 b = start.Lerp(end, (piece + 1f) / subdivisions);
                Vector2 canvasA = IsometricGrid.GridToScreen(a);
                Vector2 canvasB = IsometricGrid.GridToScreen(b);
                Vector2 tangent = canvasB - canvasA;
                if (tangent.IsZeroApprox()) continue;
                Vector2 normal = new(-tangent.Y, tangent.X);
                normal = normal.Normalized() * halfWidth * IsometricGrid.TileHeight * .52f;
                Polygon2D ribbon = new()
                {
                    Polygon = [
                        canvasA + normal, canvasB + normal,
                        canvasB - normal, canvasA - normal],
                    Color = Colors.White,
                    Material = material
                };
                root.AddChild(ribbon);
            }
        }
    }

    private void BuildPonds()
    {
        foreach ((Vector2 center, Vector2 radius) in new[]
        {
            (new Vector2(146, 242), new Vector2(2.2f, 1.4f)),
            (new Vector2(137, 263), new Vector2(1.6f, 1.1f))
        })
        {
            Polygon2D pond = new()
            {
                Name = "StillPond",
                Polygon = ProjectEllipse(center, radius, 20),
                Color = Colors.White,
                Material = WaterMaterialLibrary.Pond
            };
            AddChild(pond);
        }
    }

    private static Vector2[] ProjectEllipse(Vector2 center, Vector2 radius, int segments)
    {
        Vector2[] points = new Vector2[segments];
        for (int index = 0; index < segments; index++)
        {
            float angle = Mathf.Tau * index / segments;
            points[index] = IsometricGrid.GridToScreen(center + new Vector2(Mathf.Cos(angle) * radius.X, Mathf.Sin(angle) * radius.Y));
        }
        return points;
    }
}
