#nullable enable

using System.Collections.Generic;
using Godot;
using AshwoodCounty.World;

namespace AshwoodCounty.World.Regions;

public enum RegionVisualKind
{
    Outskirts,
    FarmEdge,
    MillCreek
}

/// <summary>
/// Tool-safe authored environment preview. It draws through IsometricGrid and
/// creates no editor-time children or gameplay simulation.
/// </summary>
[Tool]
public partial class RegionEnvironment : Node2D
{
    [Export] public string RegionId { get; set; } = RegionIds.Outskirts;
    [Export] public string DisplayName { get; set; } = "Ashwood Outskirts";
    [Export] public RegionVisualKind VisualKind { get; set; }
    [Export] public Vector2I MapSize { get; set; } = new(42, 38);
    [Export] public Vector2 ArrivalCell { get; set; } = new(20, 20);

    public RegionState? State { get; private set; }

    public override void _Ready()
    {
        YSortEnabled = true;
        QueueRedraw();
    }

    public void RestoreState(RegionState state)
    {
        State = state;
        QueueRedraw();
    }

    public RegionState CaptureState()
    {
        return State ?? new RegionState { RegionId = RegionId };
    }

    public override void _Draw()
    {
        Color ground = VisualKind switch
        {
            RegionVisualKind.FarmEdge => new Color("#71854b"),
            RegionVisualKind.MillCreek => new Color("#4f765f"),
            _ => new Color("#62754a")
        };

        DrawColoredPolygon(IsometricGrid.ProjectRectangle(Vector2.Zero, MapSize), ground);
        DrawGrid();
        DrawLandmarks();
    }

    private void DrawGrid()
    {
        Color grid = new(0.09f, 0.12f, 0.08f, 0.22f);
        for (int x = 0; x <= MapSize.X; x++)
            DrawLine(IsometricGrid.GridToScreen(new Vector2(x, 0)), IsometricGrid.GridToScreen(new Vector2(x, MapSize.Y)), grid, 1f);
        for (int y = 0; y <= MapSize.Y; y++)
            DrawLine(IsometricGrid.GridToScreen(new Vector2(0, y)), IsometricGrid.GridToScreen(new Vector2(MapSize.X, y)), grid, 1f);
    }

    private void DrawLandmarks()
    {
        List<(Vector2 Cell, string Kind)> objects = VisualKind switch
        {
            RegionVisualKind.FarmEdge =>
            [
                (new Vector2(10, 10), "barn"), (new Vector2(12, 11), "silo"),
                (new Vector2(26, 9), "field"), (new Vector2(27, 20), "field"),
                (new Vector2(8, 27), "tree"), (new Vector2(33, 25), "tree")
            ],
            RegionVisualKind.MillCreek =>
            [
                (new Vector2(18, 12), "mill"), (new Vector2(20, 13), "shed"),
                (new Vector2(7, 8), "tree"), (new Vector2(31, 9), "tree"),
                (new Vector2(9, 28), "tree"), (new Vector2(29, 26), "tree")
            ],
            _ =>
            [
                (new Vector2(10, 8), "house"), (new Vector2(14, 10), "house"),
                (new Vector2(28, 11), "warehouse"), (new Vector2(7, 26), "tree"),
                (new Vector2(31, 25), "tree"), (new Vector2(23, 29), "tree")
            ]
        };

        if (VisualKind == RegionVisualKind.MillCreek)
            DrawCreek();
        if (VisualKind == RegionVisualKind.FarmEdge)
            DrawFarmRows();
        if (VisualKind == RegionVisualKind.Outskirts)
            DrawRoad();

        objects.Sort((a, b) => IsometricGrid.GridToScreen(a.Cell).Y.CompareTo(IsometricGrid.GridToScreen(b.Cell).Y));
        foreach ((Vector2 cell, string kind) in objects)
            DrawObject(cell, kind);
    }

    private void DrawRoad()
    {
        Vector2[] road = IsometricGrid.ProjectRectangle(new Vector2(0, 18), new Vector2(MapSize.X, 4));
        DrawColoredPolygon(road, new Color("#777066"));
        DrawPolyline([road[0], road[1], road[2], road[3], road[0]], new Color("#b5a46d"), 2f);
    }

    private void DrawFarmRows()
    {
        for (int y = 7; y < 28; y += 3)
            DrawLine(IsometricGrid.GridToScreen(new Vector2(19, y)), IsometricGrid.GridToScreen(new Vector2(35, y)), new Color("#9c8748"), 5f);
    }

    private void DrawCreek()
    {
        Vector2[] creek = IsometricGrid.ProjectRectangle(new Vector2(0, 19), new Vector2(MapSize.X, 5));
        DrawColoredPolygon(creek, new Color("#39758a"));
        DrawPolyline([creek[0], creek[1], creek[2], creek[3], creek[0]], new Color("#7eb0ae"), 2f);
    }

    private void DrawObject(Vector2 cell, string kind)
    {
        Vector2 basePoint = IsometricGrid.GridToScreen(cell);
        if (kind == "tree")
        {
            DrawRect(new Rect2(basePoint + new Vector2(-4, -28), new Vector2(8, 29)), new Color("#55412c"));
            DrawCircle(basePoint + new Vector2(0, -38), 17, new Color("#243e29"));
            DrawCircle(basePoint + new Vector2(-8, -34), 11, new Color("#365b32"));
            return;
        }

        Vector2 size = kind == "field" ? new Vector2(5, 4) : new Vector2(3, 3);
        Vector2[] footprint = IsometricGrid.ProjectRectangle(cell, size);
        Color wall = kind switch
        {
            "barn" => new Color("#74473a"), "mill" => new Color("#6d6653"),
            "silo" => new Color("#8f9386"), "field" => new Color("#8a793f"),
            "warehouse" => new Color("#5c6260"), _ => new Color("#7a6f5c")
        };
        DrawColoredPolygon(footprint, wall);
        if (kind != "field")
        {
            Vector2 roofTop = basePoint + new Vector2(0, -50);
            DrawColoredPolygon([footprint[0], footprint[1], roofTop, footprint[3]], wall.Lightened(0.24f));
            DrawPolyline([footprint[0], footprint[1], footprint[2], footprint[3], footprint[0]], new Color("#292a23"), 2f);
        }
    }
}
