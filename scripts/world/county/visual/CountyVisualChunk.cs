#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace AshwoodCounty.World.County.Visual;

/// <summary>
/// Batched painterly landscape for one county chunk. Decorative sprites are
/// draw commands rather than scene nodes, and generation is deterministic.
/// </summary>
public partial class CountyVisualChunk : Node2D
{
    private enum Biome { Meadow, Forest, Outskirts, Farm, Mill, SouthFarm, Urban, Scrub, Water }

    private readonly record struct Field(Rect2 Bounds, Color Soil, bool RowsAlongX);
    private readonly record struct Prop(string Texture, Vector2 Position, float Scale, Color Tint);

    private const string TerrainRoot = "res://assets/art/terrain/";
    private const string VegetationRoot = "res://assets/art/environment/vegetation/";
    private const string PropsRoot = "res://assets/art/environment/props/";
    private const string RocksRoot = "res://assets/art/environment/rocks/";
    private const string ResourcesRoot = "res://assets/art/resources/";
    private const string Ground02Root = "res://assets/art/terrain/ground/";
    private const string RoadArtRoot = "res://assets/art/terrain/roads/";
    private const string Vegetation02Root = "res://assets/art/vegetation/";
    private const string FarmPropsRoot = "res://assets/art/props/farm/";
    private const string LoggingPropsRoot = "res://assets/art/props/logging/";
    private const string RoadsidePropsRoot = "res://assets/art/props/roadside/";

    private static readonly Vector2[] MillCreek =
    [
        new(190, 214), new(181, 220), new(176, 230), new(166, 238),
        new(159, 248), new(151, 257), new(142, 269), new(129, 277)
    ];

    private static readonly CountyRoadDefinition[] FarmTracks =
    [
        new("farm_north_track", "North Field Track", .52f,
            [new(145, 180), new(157, 184), new(170, 188), new(184, 192)]),
        new("farm_west_track", "West Field Track", .46f,
            [new(151, 180), new(151, 197), new(148, 216), new(151, 232)]),
        new("farmyard_track", "Farmyard Track", .56f,
            [new(183, 191), new(173, 198), new(164, 204), new(155, 211)]),
        new("mill_logging_track", "Mill Logging Track", .48f,
            [new(154, 250), new(143, 246), new(132, 247), new(122, 254)])
    ];

    private static readonly Field[] Fields =
    [
        new(new Rect2(134, 174, 19, 27), new Color("#766b3d"), true),
        new(new Rect2(156, 176, 28, 18), new Color("#8a7945"), true),
        new(new Rect2(135, 205, 23, 25), new Color("#6f7542"), false),
        new(new Rect2(161, 211, 29, 19), new Color("#857541"), true),
        new(new Rect2(174, 195, 18, 13), new Color("#8f8050"), false),
        new(new Rect2(104, 238, 30, 31), new Color("#85814e"), true),
        new(new Rect2(177, 241, 31, 30), new Color("#8d8650"), false)
    ];

    private static readonly Prop[] AuthoredProps =
    [
        // Outskirts to Farm transition: broken rural edge rather than a biome seam.
        P(VegetationRoot + "bush_01.png", 193, 178, .31f),
        P(VegetationRoot + "young_tree_01.png", 189, 181, .30f),
        P(VegetationRoot + "flowers_01.png", 187, 184, .27f),
        P(PropsRoot + "fence_01.png", 184, 186, .31f),
        P(PropsRoot + "fence_01.png", 181, 188, .31f),
        P(RocksRoot + "rock_cluster_01.png", 178, 190, .29f),

        // Farmyard and abandoned equipment traces.
        P(PropsRoot + "fence_01.png", 166, 197, .34f),
        P(ResourcesRoot + "wood_stack_02.png", 163, 201, .31f),
        P(RocksRoot + "rock_cluster_01.png", 176, 201, .30f),
        P(VegetationRoot + "dead_tree_01.png", 179, 211, .28f),
        P(VegetationRoot + "flowers_01.png", 159, 209, .25f),

        // Farm to Mill transition and logging debris.
        P(ResourcesRoot + "fallen_log_01.png", 166, 226, .39f),
        P(ResourcesRoot + "stump_01.png", 163, 229, .34f),
        P(VegetationRoot + "fern_01.png", 160, 232, .34f),
        P(ResourcesRoot + "fallen_log_01.png", 147, 243, .41f),
        P(ResourcesRoot + "stump_01.png", 143, 247, .34f),
        P(ResourcesRoot + "wood_stack_01.png", 151, 249, .28f),
        P(ResourcesRoot + "fallen_log_01.png", 135, 253, .40f),
        P(ResourcesRoot + "stump_01.png", 139, 259, .32f),
        P(RocksRoot + "mossy_rock_01.png", 158, 257, .31f),
        P(RocksRoot + "rock_cluster_01.png", 151, 266, .29f),
        P(VegetationRoot + "dead_tree_01.png", 166, 260, .30f),
        P(FarmPropsRoot + "fence_overgrown_02.png", 154, 196, .40f),
        P(Vegetation02Root + "hedge_01.png", 158, 184, .45f),
        P(Vegetation02Root + "bush_berries_01.png", 181, 197, .34f),
        P(LoggingPropsRoot + "stump_02.png", 149, 251, .36f),
        P(LoggingPropsRoot + "rotted_log_01.png", 139, 256, .38f),
        P(RoadsidePropsRoot + "mossy_boulder_02.png", 145, 264, .31f),
        P(RoadsidePropsRoot + "rock_formation_02.png", 161, 263, .34f)
    ];

    private Vector2I _coordinate;
    private Rect2 _gridBounds;
    private Vector2 _canvasOrigin;

    public bool DrawLocationLabels { get; init; } = true;

    public void Initialize(Vector2I coordinate)
    {
        _coordinate = coordinate;
        _gridBounds = CountyCoordinateSpace.ChunkGridBounds(coordinate);
        _canvasOrigin = IsometricGrid.GridToScreen(_gridBounds.Position);
        Position = _canvasOrigin;
        ZAsRelative = false;
        ZIndex = -100;
    }

    public override void _Ready() => QueueRedraw();

    public override void _Draw()
    {
        DrawTerrainFoundation();
        DrawFarmGround();
        DrawMillGround();
        DrawUrbanGround();
        DrawGroundTexturePatches();
        DrawWaterways();
        DrawRoadNetwork();
        DrawRoadArtStamps();
        DrawRailwayCorridor();
        DrawFarmComposition();
        DrawForestComposition();
        DrawAuthoredProps();
        DrawAshwoodTown();
        DrawLandmarks();
    }

    private void DrawTerrainFoundation()
    {
        const int block = 8;
        int startX = Mathf.FloorToInt(_gridBounds.Position.X);
        int startY = Mathf.FloorToInt(_gridBounds.Position.Y);
        int endX = Mathf.CeilToInt(_gridBounds.End.X);
        int endY = Mathf.CeilToInt(_gridBounds.End.Y);
        for (int y = startY; y < endY; y += block)
        {
            for (int x = startX; x < endX; x += block)
            {
                Rect2 cell = new(new Vector2(x, y), new Vector2(Mathf.Min(block, endX - x), Mathf.Min(block, endY - y)));
                Vector2 sample = cell.GetCenter();
                Color color = TerrainColor(sample);
                float shade = (Hash01(x, y, 3) - .5f) * .075f;
                color = shade >= 0 ? color.Lightened(shade) : color.Darkened(-shade);
                DrawColoredPolygon(ProjectRectangle(cell), color);

                // Cover the low-frequency biome color with a shared painterly
                // ground tile. Regional tint remains visible without reading as
                // a large flat blockout.
                string foundation = BiomeAt(sample) switch
                {
                    Biome.Farm or Biome.SouthFarm => Hash01(x, y, 5) > .48f ? "grass_dirt_01.png" : "grass_02.png",
                    Biome.Mill or Biome.Forest => Hash01(x, y, 5) > .52f ? "leaves_01.png" : "grass_01.png",
                    Biome.Urban => "gravel_scatter_01.png",
                    _ => Hash01(x, y, 5) > .45f ? "grass_01.png" : "grass_02.png"
                };
                DrawGroundTexture(TerrainRoot + foundation, sample, 1.45f, new Color(color.Lightened(.12f), .50f));
            }
        }
    }

    private void DrawFarmGround()
    {
        foreach (Field field in Fields)
        {
            if (!TryIntersect(field.Bounds, _gridBounds, out Rect2 visible))
                continue;

            DrawColoredPolygon(ProjectRectangle(visible), field.Soil);
        }

        DrawPatch(new Vector2(169, 201), new Vector2(13, 10), new Color(.43f, .34f, .20f, .62f), 18);
        DrawPatch(new Vector2(170, 231), new Vector2(23, 12), new Color(.27f, .33f, .16f, .32f), 22);
    }

    private void DrawMillGround()
    {
        DrawPatch(new Vector2(154, 250), new Vector2(39, 34), new Color(.10f, .21f, .13f, .38f), 28);
        DrawPatch(new Vector2(147, 247), new Vector2(14, 11), new Color(.30f, .23f, .14f, .55f), 20);
        DrawPatch(new Vector2(137, 255), new Vector2(12, 8), new Color(.25f, .20f, .13f, .48f), 18);
    }

    private void DrawUrbanGround()
    {
        DrawPatch(new Vector2(252, 145), new Vector2(47, 38), new Color(.27f, .28f, .25f, .67f), 36);
        DrawPatch(new Vector2(252, 144), new Vector2(24, 19), new Color(.34f, .33f, .28f, .42f), 28);
    }

    private void DrawGroundTexturePatches()
    {
        int startX = Mathf.FloorToInt(_gridBounds.Position.X) + 2;
        int startY = Mathf.FloorToInt(_gridBounds.Position.Y) + 2;
        int endX = Mathf.CeilToInt(_gridBounds.End.X);
        int endY = Mathf.CeilToInt(_gridBounds.End.Y);

        for (int y = startY; y < endY; y += 6)
        {
            for (int x = startX; x < endX; x += 6)
            {
                Vector2 point = new(
                    x + (Hash01(x, y, 11) - .5f) * 2.4f,
                    y + (Hash01(x, y, 13) - .5f) * 2.4f);
                if (IsInLake(point) || DistanceToPolyline(point, CountyMacroLayout.BlackwaterRiver) < 3.5f
                    || DistanceToPolyline(point, MillCreek) < 2.6f)
                    continue;

                Biome biome = BiomeAt(point);
                float choice = Hash01(x, y, 17);
                string file = biome switch
                {
                    Biome.Farm or Biome.SouthFarm => choice < .34f ? "grass_dirt_01.png" : choice < .66f ? "grass_02.png" : "dirt_scatter_01.png",
                    Biome.Mill or Biome.Forest => choice < .40f ? "leaves_01.png" : choice < .68f ? "grass_scatter_01.png" : "mud_scatter_01.png",
                    Biome.Urban => choice < .54f ? "gravel_scatter_01.png" : "dirt_scatter_01.png",
                    Biome.Scrub => choice < .50f ? "grass_dirt_01.png" : "dirt_scatter_01.png",
                    _ => choice < .26f ? "grass_01.png" : choice < .58f ? "grass_scatter_01.png" : choice < .80f ? "grass_02.png" : "dirt_scatter_01.png"
                };
                float scale = .52f + Hash01(x, y, 19) * .22f;
                float alpha = biome is Biome.Mill or Biome.Farm ? .58f : .47f;
                DrawGroundTexture(TerrainRoot + file, point, scale, new Color(1, 1, 1, alpha));
            }
        }
    }

    private void DrawWaterways()
    {
        // Lake bank is made from short, locally culled pieces; coarse interior
        // diamonds then give the broad water body a subtle color variation.
        DrawPolylineRibbon(CountyMacroLayout.BlackwaterLake.Append(CountyMacroLayout.BlackwaterLake[0]).ToArray(), 3.4f, new Color("#635a43"), 3f);
        DrawLakeInterior();

        DrawStreamBanks(CountyMacroLayout.BlackwaterRiver, 3.4f);
        DrawStreamBanks(MillCreek, 2.7f);
        DrawShorelineDressing();
    }

    private void DrawLakeInterior()
    {
        int startX = Mathf.FloorToInt(_gridBounds.Position.X / 4f) * 4;
        int startY = Mathf.FloorToInt(_gridBounds.Position.Y / 4f) * 4;
        for (int y = startY; y < _gridBounds.End.Y; y += 4)
        {
            for (int x = startX; x < _gridBounds.End.X; x += 4)
            {
                Vector2 center = new(x + 2, y + 2);
                if (!IsInLake(center))
                    continue;
                Color water = new Color("#315d61").Lerp(new Color("#47787a"), Hash01(x, y, 31) * .33f);
                DrawColoredPolygon(ProjectRectangle(new Rect2(x, y, 4.15f, 4.15f)), water);
            }
        }
    }

    private void DrawStreamBanks(Vector2[] points, float bankWidth)
    {
        DrawPolylineRibbon(points, bankWidth + .7f, new Color("#4b4734"), 2.5f);
        DrawPolylineRibbon(points, bankWidth, new Color("#6f5b3e"), 2.5f);
    }

    private void DrawShorelineDressing()
    {
        for (int y = Mathf.FloorToInt(_gridBounds.Position.Y); y < _gridBounds.End.Y; y += 5)
        {
            for (int x = Mathf.FloorToInt(_gridBounds.Position.X); x < _gridBounds.End.X; x += 5)
            {
                Vector2 point = new(x + Hash01(x, y, 101) * 3f, y + Hash01(x, y, 103) * 3f);
                float riverDistance = DistanceToPolyline(point, CountyMacroLayout.BlackwaterRiver);
                float creekDistance = DistanceToPolyline(point, MillCreek);
                if ((riverDistance < 3.8f && riverDistance > 2.1f) || (creekDistance < 3.2f && creekDistance > 1.2f))
                {
                    string texture = Hash01(x, y, 107) > .52f ? RocksRoot + "mossy_rock_01.png" : VegetationRoot + "fern_01.png";
                    DrawAnchoredTexture(texture, point, .19f + Hash01(x, y, 109) * .07f, new Color(1, 1, 1, .88f));
                }
            }
        }
    }

    private void DrawRoadNetwork()
    {
        foreach (CountyRoadDefinition road in CountyMacroLayout.Roads.Concat(FarmTracks))
            DrawRoad(road);

        // Ashwood streets establish readable blocks at county zoom.
        for (int x = 218; x <= 286; x += 11)
            DrawRoad(new CountyRoadDefinition($"street_x_{x}", "Ashwood Street", .72f, [new(x, 116), new(x, 175)]));
        for (int y = 120; y <= 175; y += 11)
            DrawRoad(new CountyRoadDefinition($"street_y_{y}", "Ashwood Street", .72f, [new(214, y), new(290, y)]));
    }

    private void DrawRoad(CountyRoadDefinition road)
    {
        List<(Vector2 Start, Vector2 End, int Phase)> pieces = LocalSegments(road.Points, 2.8f);
        if (pieces.Count == 0)
            return;

        Color outer = road.Major ? new Color("#5f5b50") : new Color("#594f38");
        Color shoulder = road.Major ? new Color("#8a806a") : new Color("#766442");
        Color surface = road.Major ? new Color("#565750") : new Color("#957a4f");
        foreach ((Vector2 start, Vector2 end, _) in pieces)
            DrawRibbonSegment(start, end, road.HalfWidth + .78f, outer);
        foreach ((Vector2 start, Vector2 end, _) in pieces)
            DrawRibbonSegment(start, end, road.HalfWidth + .38f, shoulder);
        foreach ((Vector2 start, Vector2 end, _) in pieces)
            DrawRibbonSegment(start, end, road.HalfWidth, surface);

        foreach ((Vector2 start, Vector2 end, int phase) in pieces)
        {
            Vector2 tangent = (end - start).Normalized();
            Vector2 normal = new(-tangent.Y, tangent.X);
            if (road.Major)
            {
                if ((phase & 1) == 0)
                    DrawRibbonSegment(start + tangent * .35f, end - tangent * .35f, .055f, new Color("#c4ad69"));
            }
            else if (road.HalfWidth > .8f)
            {
                DrawRibbonSegment(start + normal * road.HalfWidth * .45f, end + normal * road.HalfWidth * .45f, .045f, new Color(.27f, .22f, .14f, .38f));
                DrawRibbonSegment(start - normal * road.HalfWidth * .45f, end - normal * road.HalfWidth * .45f, .045f, new Color(.27f, .22f, .14f, .38f));
            }
        }
    }

    private void DrawRoadArtStamps()
    {
        foreach (CountyRoadDefinition road in CountyMacroLayout.Roads.Concat(FarmTracks))
        {
            float spacing = road.Major ? 12f : 15f;
            foreach ((Vector2 point, Vector2 tangent, int index) in SamplesAlong(road.Points, spacing))
            {
                if (!_gridBounds.HasPoint(point) || Hash01(index, road.Id.GetHashCode(), 113) < .38f)
                    continue;
                string path = road.Major ? RoadArtRoot + "asphalt_wear_01.png"
                    : road.Id.Contains("farm", StringComparison.Ordinal) ? RoadArtRoot + "dirt_track_01.png"
                    : road.Id.Contains("mill", StringComparison.Ordinal) || road.Id.Contains("logging", StringComparison.Ordinal)
                        ? RoadArtRoot + "forest_track_01.png"
                        : RoadArtRoot + "gravel_road_01.png";
                DrawGroundTexture(path, point, road.Major ? .34f : .28f, new Color(1, 1, 1, road.Major ? .44f : .34f));
            }
        }

        DrawGroundTexture(RoadArtRoot + "asphalt_cracked_01.png", new Vector2(227, 144), .36f, new Color(1, 1, 1, .56f));
    }

    private void DrawRailwayCorridor()
    {
        Vector2[] rail = [new(111, 282), new(131, 268), new(153, 251), new(170, 234), new(185, 216), new(198, 198)];
        foreach ((Vector2 start, Vector2 end, _) in LocalSegments(rail, 2.3f))
        {
            Vector2 tangent = (end - start).Normalized();
            Vector2 normal = new(-tangent.Y, tangent.X);
            DrawRibbonSegment(start, end, 1.15f, new Color("#4b4436"));
            DrawRibbonSegment(start + normal * .48f, end + normal * .48f, .08f, new Color("#807766"));
            DrawRibbonSegment(start - normal * .48f, end - normal * .48f, .08f, new Color("#807766"));
        }
        int tie = 0;
        foreach ((Vector2 point, Vector2 tangent, _) in SamplesAlong(rail, 1.4f))
        {
            if (!_gridBounds.HasPoint(point) || (tie++ & 1) != 0) continue;
            Vector2 normal = new(-tangent.Y, tangent.X);
            DrawLine(P(point - normal * .85f), P(point + normal * .85f), new Color("#3c3023"), 3f, true);
        }
    }

    private void DrawFarmComposition()
    {
        foreach (Field field in Fields)
        {
            if (!field.Bounds.Intersects(_gridBounds))
                continue;

            Color dark = field.Soil.Darkened(.20f);
            if (field.RowsAlongX)
            {
                for (float y = field.Bounds.Position.Y + 1.6f; y < field.Bounds.End.Y; y += 2.35f)
                    DrawGridLineClipped(new Vector2(field.Bounds.Position.X + .8f, y), new Vector2(field.Bounds.End.X - .8f, y), dark, 1.4f);
            }
            else
            {
                for (float x = field.Bounds.Position.X + 1.6f; x < field.Bounds.End.X; x += 2.35f)
                    DrawGridLineClipped(new Vector2(x, field.Bounds.Position.Y + .8f), new Vector2(x, field.Bounds.End.Y - .8f), dark, 1.4f);
            }

            DrawFenceLine(new Vector2(field.Bounds.Position.X, field.Bounds.Position.Y), new Vector2(field.Bounds.End.X, field.Bounds.Position.Y));
            DrawFenceLine(new Vector2(field.Bounds.Position.X, field.Bounds.End.Y), new Vector2(field.Bounds.End.X, field.Bounds.End.Y));
            DrawFenceLine(new Vector2(field.Bounds.Position.X, field.Bounds.Position.Y), new Vector2(field.Bounds.Position.X, field.Bounds.End.Y));
            DrawFenceLine(new Vector2(field.Bounds.End.X, field.Bounds.Position.Y), new Vector2(field.Bounds.End.X, field.Bounds.End.Y));

            Vector2 fieldArt = field.Bounds.GetCenter();
            if (_gridBounds.HasPoint(fieldArt))
                DrawGroundTexture(Ground02Root + "farm_rows_muddy_01.png", fieldArt, .48f, new Color(1, 1, 1, .72f));
        }

        // Barn, farm sheds and silos make the agricultural center readable.
        DrawBuildingIfLocal(new Vector2(165, 199), new Vector2(3.0f, 2.4f), new Color("#715039"), new Color("#844c35"), 36);
        DrawBuildingIfLocal(new Vector2(177, 202), new Vector2(2.1f, 1.7f), new Color("#655443"), new Color("#586354"), 25);
        DrawSiloIfLocal(new Vector2(169, 201), .72f, 34);
        DrawSiloIfLocal(new Vector2(171, 202), .62f, 30);
    }

    private void DrawForestComposition()
    {
        List<(Vector2 Point, Biome Biome, float Value)> trees = [];
        int startX = Mathf.FloorToInt(_gridBounds.Position.X) - 3;
        int startY = Mathf.FloorToInt(_gridBounds.Position.Y) - 3;
        int endX = Mathf.CeilToInt(_gridBounds.End.X) + 3;
        int endY = Mathf.CeilToInt(_gridBounds.End.Y) + 3;
        for (int y = startY; y < endY; y += 4)
        {
            for (int x = startX; x < endX; x += 4)
            {
                Vector2 point = new(x + (Hash01(x, y, 41) - .5f) * 3.0f, y + (Hash01(x, y, 43) - .5f) * 3.0f);
                if (!_gridBounds.HasPoint(point) || IsInLake(point))
                    continue;
                Biome biome = BiomeAt(point);
                float threshold = biome switch
                {
                    Biome.Mill => .32f,
                    Biome.Forest => .43f,
                    Biome.Outskirts => .68f,
                    Biome.Scrub => .72f,
                    Biome.Farm or Biome.SouthFarm => IsFarmTreeLine(point) ? .45f : .91f,
                    Biome.Urban => .94f,
                    _ => .78f
                };
                float value = Hash01(x, y, 47);
                if (value < threshold || DistanceToAnyRoad(point) < 3.4f || DistanceToPolyline(point, MillCreek) < 2.7f)
                    continue;
                trees.Add((point, biome, value));
            }
        }

        foreach ((Vector2 point, Biome biome, float value) in trees.OrderBy(tree => tree.Point.X + tree.Point.Y))
        {
            string texture = biome is Biome.Mill or Biome.Forest
                ? value > .78f ? "pine_01.png" : value > .57f ? "oak_01.png" : "young_tree_01.png"
                : value > .85f ? "oak_01.png" : "young_tree_01.png";
            float scale = texture.StartsWith("young", StringComparison.Ordinal) ? .27f : .31f + Hash01((int)point.X, (int)point.Y, 53) * .05f;
            Color tint = biome == Biome.Mill ? new Color(.82f, .90f, .82f, .96f) : Colors.White;
            DrawAnchoredTexture(VegetationRoot + texture, point, scale, tint);

            if (Hash01((int)point.X, (int)point.Y, 59) > .66f)
            {
                Vector2 undergrowth = point + new Vector2(1.2f, -.5f);
                string floor = biome == Biome.Mill ? Vegetation02Root + "fern_02.png" : Vegetation02Root + "bush_dense_02.png";
                DrawAnchoredTexture(floor, undergrowth, .24f, new Color(1, 1, 1, .88f));
            }
        }

        // Sparse ground-level composition provides density without hiding actors.
        for (int y = startY; y < endY; y += 7)
        {
            for (int x = startX; x < endX; x += 7)
            {
                Vector2 point = new(x + Hash01(x, y, 61) * 3f, y + Hash01(x, y, 67) * 3f);
                if (!_gridBounds.HasPoint(point) || DistanceToAnyRoad(point) < 2.3f)
                    continue;
                Biome biome = BiomeAt(point);
                if (biome is not (Biome.Mill or Biome.Forest or Biome.Outskirts))
                    continue;
                float choice = Hash01(x, y, 71);
                string path = choice < .34f ? VegetationRoot + "fern_01.png"
                    : choice < .68f ? VegetationRoot + "grass_clump_01.png"
                    : RocksRoot + "mossy_rock_01.png";
                DrawAnchoredTexture(path, point, .22f + Hash01(x, y, 73) * .06f, new Color(1, 1, 1, .84f));
            }
        }
    }

    private void DrawAuthoredProps()
    {
        foreach (Prop prop in AuthoredProps)
        {
            if (_gridBounds.HasPoint(prop.Position))
                DrawAnchoredTexture(prop.Texture, prop.Position, prop.Scale, prop.Tint);
        }
    }

    private void DrawAshwoodTown()
    {
        CountyLocationDefinition? ashwood = CountyMacroLayout.Find("ashwood");
        if (ashwood is null || !ashwood.Bounds.Intersects(_gridBounds))
            return;

        for (int y = 124; y <= 168; y += 11)
        {
            for (int x = 220; x <= 286; x += 11)
            {
                Vector2 point = new(x + 4.1f, y + 4.0f);
                if (!_gridBounds.Grow(2).HasPoint(point) || !CountyMacroLayout.Contains(ashwood, point))
                    continue;

                float variant = Hash01(x, y, 83);
                if (x is >= 242 and <= 264 && y is >= 135 and <= 157)
                    DrawBuildingIfLocal(point, new Vector2(2.8f, 2.3f), new Color("#5a554b"), variant > .5f ? new Color("#6d5c45") : new Color("#515c57"), 30);
                else
                    DrawAnchoredTexture("res://assets/art/buildings/survival_cabin.png", point, .105f + variant * .012f, new Color(.84f, .84f, .80f, .94f));
            }
        }

        // Civic anchors and a small town green break up the repeated blocks.
        DrawBuildingIfLocal(new Vector2(244, 151), new Vector2(4.2f, 3.1f), new Color("#59605c"), new Color("#728078"), 40);
        DrawBuildingIfLocal(new Vector2(272, 137), new Vector2(3.1f, 2.4f), new Color("#5c554a"), new Color("#75644d"), 34);
        DrawPatch(new Vector2(266, 160), new Vector2(8, 7), new Color(.22f, .40f, .19f, .78f), 18);
    }

    private void DrawLandmarks()
    {
        foreach (CountyLocationDefinition landmark in CountyMacroLayout.Locations.Where(location => location.Kind == CountyLocationKind.Landmark))
        {
            if (!_gridBounds.HasPoint(landmark.Center))
                continue;

            switch (landmark.Id)
            {
                case "old_mill":
                    DrawBuilding(landmark.Center, new Vector2(3.2f, 2.6f), new Color("#554838"), new Color("#644c34"), 38);
                    DrawAnchoredTexture(ResourcesRoot + "wood_stack_01.png", landmark.Center + new Vector2(5, 1), .30f, Colors.White);
                    break;
                case "farm_silos":
                case "hospital":
                case "sheriffs_office":
                    // Their authored visual is drawn by the district pass.
                    break;
                case "starting_camp":
                    // Existing full-detail camp content owns this footprint.
                    break;
                default:
                    DrawBuilding(landmark.Center, new Vector2(2.3f, 2.0f), new Color("#5e5543"), new Color("#756147"), 28);
                    break;
            }

            if (DrawLocationLabels)
            {
                Vector2 at = P(landmark.Center) + new Vector2(12, -18);
                DrawString(ThemeDB.FallbackFont, at, landmark.Name, HorizontalAlignment.Left, -1, 13, new Color("#ddcca0"));
            }
        }
    }

    private void DrawFenceLine(Vector2 start, Vector2 end)
    {
        float length = start.DistanceTo(end);
        int pieces = Mathf.Max(1, Mathf.CeilToInt(length / 3.5f));
        for (int index = 0; index <= pieces; index++)
        {
            Vector2 point = start.Lerp(end, index / (float)pieces);
            if (_gridBounds.HasPoint(point) && Hash01((int)(point.X * 3), (int)(point.Y * 3), 91) > .12f)
                DrawAnchoredTexture(PropsRoot + "fence_01.png", point, .28f, new Color(.91f, .86f, .74f, .91f));
        }
    }

    private void DrawBuildingIfLocal(Vector2 center, Vector2 size, Color wall, Color roof, float height)
    {
        if (_gridBounds.HasPoint(center))
            DrawBuilding(center, size, wall, roof, height);
    }

    private void DrawBuilding(Vector2 center, Vector2 size, Color wall, Color roof, float height)
    {
        Vector2 position = center - size * .5f;
        Vector2[] footprint = ProjectRectangle(new Rect2(position, size));
        Vector2 lift = new(0, -height);
        DrawColoredPolygon([footprint[1], footprint[2], footprint[2] + lift, footprint[1] + lift], wall.Darkened(.18f));
        DrawColoredPolygon([footprint[2], footprint[3], footprint[3] + lift, footprint[2] + lift], wall);
        Vector2 ridge = (footprint[0] + footprint[2]) * .5f + lift - new Vector2(0, height * .34f);
        DrawColoredPolygon([footprint[0] + lift, footprint[1] + lift, ridge, footprint[3] + lift], roof);
        DrawPolyline([footprint[0] + lift, footprint[1] + lift, ridge, footprint[3] + lift, footprint[0] + lift], roof.Lightened(.16f), 1.3f, true);
    }

    private void DrawSiloIfLocal(Vector2 center, float radius, float height)
    {
        if (!_gridBounds.HasPoint(center))
            return;
        Vector2 basePoint = P(center);
        Vector2 radii = new(radius * IsometricGrid.TileWidth * .45f, radius * IsometricGrid.TileHeight * .35f);
        Vector2[] bottom = CanvasEllipse(basePoint, radii, 18);
        Vector2[] top = bottom.Select(point => point - new Vector2(0, height)).ToArray();
        DrawColoredPolygon([bottom[4], bottom[13], top[13], top[4]], new Color("#676a60"));
        DrawColoredPolygon(top, new Color("#8b8a76"));
        DrawPolyline(top.Append(top[0]).ToArray(), new Color("#bab49a"), 1.2f, true);
    }

    private void DrawPatch(Vector2 center, Vector2 radius, Color color, int segments)
    {
        // Tessellate broad patches into chunk-local diamonds. Drawing one huge
        // ellipse from several chunks would defeat CanvasItem culling and stack
        // translucent copies along boundaries.
        const int block = 4;
        int startX = Mathf.FloorToInt(_gridBounds.Position.X / block) * block;
        int startY = Mathf.FloorToInt(_gridBounds.Position.Y / block) * block;
        for (int y = startY; y < _gridBounds.End.Y; y += block)
        {
            for (int x = startX; x < _gridBounds.End.X; x += block)
            {
                Vector2 sample = new(x + block * .5f, y + block * .5f);
                Vector2 normalized = new((sample.X - center.X) / radius.X, (sample.Y - center.Y) / radius.Y);
                float distance = normalized.Length();
                if (distance >= 1f)
                    continue;
                float feather = Mathf.Clamp((1f - distance) * 2.5f, 0f, 1f);
                Color localColor = new(color, color.A * feather);
                DrawColoredPolygon(ProjectRectangle(new Rect2(x, y, block + .08f, block + .08f)), localColor);
            }
        }
    }

    private void DrawGroundTexture(string path, Vector2 point, float scale, Color tint)
    {
        Texture2D texture = TextureRegistry.Get(path);
        Vector2 size = texture.GetSize() * scale;
        DrawTextureRect(texture, new Rect2(P(point) - size * .5f, size), false, tint);
    }

    private void DrawAnchoredTexture(string path, Vector2 point, float scale, Color tint)
    {
        Texture2D texture = TextureRegistry.Get(path);
        Vector2 size = texture.GetSize() * scale;
        DrawTextureRect(texture, new Rect2(P(point) - new Vector2(size.X * .5f, size.Y), size), false, tint);
    }

    private void DrawGridLineClipped(Vector2 start, Vector2 end, Color color, float width)
    {
        foreach ((Vector2 a, Vector2 b, _) in LocalSegments([start, end], 3f))
            DrawLine(P(a), P(b), color, width, true);
    }

    private void DrawPolylineRibbon(Vector2[] points, float halfWidth, Color color, float maxPieceLength)
    {
        foreach ((Vector2 start, Vector2 end, _) in LocalSegments(points, maxPieceLength))
            DrawRibbonSegment(start, end, halfWidth, color);
    }

    private void DrawRibbonSegment(Vector2 start, Vector2 end, float halfWidth, Color color)
    {
        if (start.IsEqualApprox(end))
            return;

        // Canvas-space strokes avoid oversized skewed quads where a road crosses
        // an isometric chunk seam, while retaining the one shared projection.
        float width = Mathf.Max(1f, halfWidth * IsometricGrid.TileHeight * 1.25f);
        DrawLine(P(start), P(end), color, width, true);
    }

    private List<(Vector2 Start, Vector2 End, int Phase)> LocalSegments(Vector2[] points, float maxLength)
    {
        List<(Vector2, Vector2, int)> result = [];
        int phase = 0;
        for (int index = 0; index < points.Length - 1; index++)
        {
            Vector2 a = points[index];
            Vector2 b = points[index + 1];
            int count = Mathf.Max(1, Mathf.CeilToInt(a.DistanceTo(b) / maxLength));
            for (int piece = 0; piece < count; piece++, phase++)
            {
                Vector2 start = a.Lerp(b, piece / (float)count);
                Vector2 end = a.Lerp(b, (piece + 1f) / count);
                if (_gridBounds.HasPoint((start + end) * .5f))
                    result.Add((start, end, phase));
            }
        }
        return result;
    }

    private static IEnumerable<(Vector2 Point, Vector2 Tangent, int Index)> SamplesAlong(Vector2[] points, float spacing)
    {
        int sampleIndex = 0;
        for (int index = 0; index < points.Length - 1; index++)
        {
            Vector2 start = points[index];
            Vector2 end = points[index + 1];
            Vector2 tangent = (end - start).Normalized();
            int count = Mathf.Max(1, Mathf.CeilToInt(start.DistanceTo(end) / spacing));
            for (int sample = 0; sample < count; sample++)
                yield return (start.Lerp(end, (sample + .5f) / count), tangent, sampleIndex++);
        }
    }

    private Vector2[] ProjectRectangle(Rect2 rectangle) =>
        IsometricGrid.ProjectRectangle(rectangle.Position, rectangle.Size).Select(point => point - _canvasOrigin).ToArray();

    private Vector2 P(Vector2 gridPoint) => IsometricGrid.GridToScreen(gridPoint) - _canvasOrigin;

    private static Vector2[] CanvasEllipse(Vector2 center, Vector2 radius, int segments)
    {
        Vector2[] points = new Vector2[segments];
        for (int index = 0; index < segments; index++)
        {
            float angle = Mathf.Tau * index / segments;
            points[index] = center + new Vector2(Mathf.Cos(angle) * radius.X, Mathf.Sin(angle) * radius.Y);
        }
        return points;
    }

    private static Biome BiomeAt(Vector2 point)
    {
        if (IsInLake(point)) return Biome.Water;
        float mill = Influence(point, new Vector2(154, 250), new Vector2(49, 43));
        float farm = Influence(point, new Vector2(170, 204), new Vector2(55, 45));
        float outskirts = Influence(point, new Vector2(197, 157), new Vector2(48, 40));
        float urban = Influence(point, new Vector2(252, 145), new Vector2(54, 45));
        float south = Influence(point, new Vector2(164, 263), new Vector2(91, 59));
        if (mill > .34f) return Biome.Mill;
        if (farm > .37f) return Biome.Farm;
        if (outskirts > .38f) return Biome.Outskirts;
        if (urban > .38f) return Biome.Urban;
        if (south > .35f) return Biome.SouthFarm;
        if (point.Y < 118 || point.X < 115) return Biome.Forest;
        if (point.X > 290) return Biome.Scrub;
        return Biome.Meadow;
    }

    private static Color TerrainColor(Vector2 point)
    {
        Color color = new("#49613b");
        color = color.Lerp(new Color("#304735"), Influence(point, new Vector2(145, 54), new Vector2(170, 76)) * .83f);
        color = color.Lerp(new Color("#607848"), Influence(point, new Vector2(197, 157), new Vector2(51, 43)) * .78f);
        color = color.Lerp(new Color("#737747"), Influence(point, new Vector2(170, 204), new Vector2(62, 51)) * .82f);
        color = color.Lerp(new Color("#304e3b"), Influence(point, new Vector2(154, 250), new Vector2(53, 48)) * .88f);
        color = color.Lerp(new Color("#777849"), Influence(point, new Vector2(164, 268), new Vector2(100, 66)) * .72f);
        color = color.Lerp(new Color("#565a50"), Influence(point, new Vector2(252, 145), new Vector2(57, 48)) * .84f);
        color = color.Lerp(new Color("#46593b"), Influence(point, new Vector2(322, 193), new Vector2(77, 105)) * .58f);
        return color;
    }

    private static float Influence(Vector2 point, Vector2 center, Vector2 radius)
    {
        Vector2 offset = point - center;
        float distance = Mathf.Sqrt(Mathf.Pow(offset.X / radius.X, 2) + Mathf.Pow(offset.Y / radius.Y, 2));
        float value = Mathf.Clamp(1f - distance, 0f, 1f);
        return value * value * (3f - 2f * value);
    }

    private static bool IsFarmTreeLine(Vector2 point)
    {
        foreach (Field field in Fields)
        {
            float distance = Mathf.Min(
                Mathf.Min(Mathf.Abs(point.X - field.Bounds.Position.X), Mathf.Abs(point.X - field.Bounds.End.X)),
                Mathf.Min(Mathf.Abs(point.Y - field.Bounds.Position.Y), Mathf.Abs(point.Y - field.Bounds.End.Y)));
            if (field.Bounds.Grow(3).HasPoint(point) && distance < 2.6f)
                return true;
        }
        return false;
    }

    private static float DistanceToAnyRoad(Vector2 point)
    {
        float distance = float.PositiveInfinity;
        foreach (CountyRoadDefinition road in CountyMacroLayout.Roads)
            distance = Mathf.Min(distance, DistanceToPolyline(point, road.Points));
        foreach (CountyRoadDefinition road in FarmTracks)
            distance = Mathf.Min(distance, DistanceToPolyline(point, road.Points));
        return distance;
    }

    private static float DistanceToPolyline(Vector2 point, Vector2[] line)
    {
        float best = float.PositiveInfinity;
        for (int index = 0; index < line.Length - 1; index++)
            best = Mathf.Min(best, DistanceToSegment(point, line[index], line[index + 1]));
        return best;
    }

    private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 segment = end - start;
        float lengthSquared = segment.LengthSquared();
        if (lengthSquared <= .0001f)
            return point.DistanceTo(start);
        float t = Mathf.Clamp((point - start).Dot(segment) / lengthSquared, 0f, 1f);
        return point.DistanceTo(start + segment * t);
    }

    private static bool IsInLake(Vector2 point) => Geometry2D.IsPointInPolygon(point, CountyMacroLayout.BlackwaterLake);

    private static float Hash01(int x, int y, int salt)
    {
        unchecked
        {
            uint value = (uint)(x * 374761393 + y * 668265263 + salt * 69069);
            value = (value ^ (value >> 13)) * 1274126177u;
            return (value ^ (value >> 16)) / (float)uint.MaxValue;
        }
    }

    private static bool TryIntersect(Rect2 a, Rect2 b, out Rect2 intersection)
    {
        Vector2 start = new(Mathf.Max(a.Position.X, b.Position.X), Mathf.Max(a.Position.Y, b.Position.Y));
        Vector2 end = new(Mathf.Min(a.End.X, b.End.X), Mathf.Min(a.End.Y, b.End.Y));
        if (end.X <= start.X || end.Y <= start.Y)
        {
            intersection = default;
            return false;
        }
        intersection = new Rect2(start, end - start);
        return true;
    }

    private static Prop P(string texture, float x, float y, float scale) =>
        new(texture, new Vector2(x, y), scale, Colors.White);
}
