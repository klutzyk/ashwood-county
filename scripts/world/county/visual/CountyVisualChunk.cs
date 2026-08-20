#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace AshwoodCounty.World.County.Visual;

/// <summary>
/// Everything that sits on the county floor for one chunk: waterways, roads,
/// field structure, vegetation masses, clutter and authored set dressing.
///
/// It is one CanvasItem per chunk so Godot can cull it, and every placement is
/// a pure function of county coordinates so the same chunk always draws the
/// same landscape. Sprites are draw commands rather than scene nodes.
/// </summary>
public partial class CountyVisualChunk : Node2D
{
    /// <summary>Chunks that can be asked to redraw, for the asset inspector.</summary>
    public const string RedrawGroup = "county_landscape_chunk";

    private readonly record struct Field(Rect2 Bounds, Color Soil, bool RowsAlongX);
    private readonly record struct Prop(string Texture, Vector2 Position, float Scale, Color Tint);

    /// <summary>
    /// A sprite queued for the standing-art pass.
    ///
    /// <c>Tall</c> separates the two things that need different treatment:
    /// trees, buildings and set dressing genuinely occlude one another and must
    /// be depth sorted, whereas ferns, flowers, grass and rubble sit flat on the
    /// ground and effectively never overlap. Drawing the low layer grouped by
    /// texture instead of by depth collapses hundreds of individual draws into a
    /// handful of batches for no visible difference.
    /// </summary>
    private readonly record struct Placement(string Texture, Vector2 Position, float Scale, Color Tint, bool Tall);

    private const string TerrainRoot = "res://assets/art/terrain/";
    private const string VegetationRoot = "res://assets/art/environment/vegetation/";
    private const string PropsRoot = "res://assets/art/environment/props/";
    private const string RocksRoot = "res://assets/art/environment/rocks/";
    private const string ResourcesRoot = "res://assets/art/resources/";
    private const string Ground02Root = "res://assets/art/terrain/ground/";
    private const string RoadArtRoot = "res://assets/art/terrain/roads/";
    private const string TreesRoot = "res://assets/art/trees/";
    private const string UndergrowthRoot = "res://assets/art/undergrowth/";
    private const string FarmPropsRoot = "res://assets/art/props/farm/";
    private const string LoggingPropsRoot = "res://assets/art/props/logging/";
    private const string RoadsidePropsRoot = "res://assets/art/props/roadside/";
    private const string RailArtRoot = "res://assets/art/terrain/rail/";
    private const string IndustrialPropsRoot = "res://assets/art/props/industrial/";
    private const string RuralBuildingsRoot = "res://assets/art/buildings/rural/";
    private const string WaterPropsRoot = "res://assets/art/water/props/";

    private static readonly Vector2[] LakeOutline =
        [.. CountyMacroLayout.BlackwaterLakeOutline, CountyMacroLayout.BlackwaterLakeOutline[0]];

    private static readonly Field[] Fields =
    [
        new(new Rect2(134, 174, 19, 27), new Color("#766b3d"), true),
        new(new Rect2(156, 176, 28, 18), new Color("#8a7945"), true),
        new(new Rect2(135, 205, 23, 25), new Color("#6f7542"), false),
        new(new Rect2(161, 211, 29, 19), new Color("#857541"), true),
        new(new Rect2(174, 195, 18, 13), new Color("#8f8050"), false),
        new(new Rect2(104, 238, 30, 31), new Color("#85814e"), true),
        new(new Rect2(177, 241, 31, 30), new Color("#8d8650"), false),

        // Broad southern agricultural mosaic. The intentional gaps are
        // hedgerows, drainage lanes and farm access rather than one flat fill.
        new(new Rect2(75, 248, 24, 29), new Color("#817a43"), false),
        new(new Rect2(79, 282, 31, 24), new Color("#716b3c"), true),
        new(new Rect2(115, 274, 27, 32), new Color("#91814a"), false),
        new(new Rect2(147, 280, 25, 29), new Color("#777441"), true),
        new(new Rect2(177, 277, 34, 27), new Color("#92874e"), false),
        new(new Rect2(214, 254, 27, 31), new Color("#77713e"), true)
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
        P(UndergrowthRoot + "bush_green_01.png", 158, 184, .40f),
        P(UndergrowthRoot + "bush_berry_red_01.png", 181, 197, .34f),
        P(LoggingPropsRoot + "stump_02.png", 149, 251, .36f),
        P(LoggingPropsRoot + "rotted_log_01.png", 139, 256, .38f),
        P(RoadsidePropsRoot + "mossy_boulder_02.png", 145, 264, .31f),
        P(RoadsidePropsRoot + "rock_formation_02.png", 161, 263, .34f),
        P(TreesRoot + "fir_tall_01.png", 132, 246, .30f),
        P(TreesRoot + "pine_scots_tall_01.png", 143, 269, .26f),
        P(TreesRoot + "oak_spreading_01.png", 170, 239, .30f),
        P(TreesRoot + "dead_hollow_01.png", 126, 258, .26f),
        P(UndergrowthRoot + "grass_pampas_01.png", 157, 248, .36f),
        P(FarmPropsRoot + "corn_rows_01.png", 145, 190, .43f),
        P(FarmPropsRoot + "crop_rows_green_01.png", 177, 186, .46f),
        P(FarmPropsRoot + "hay_bale_round_01.png", 181, 205, .31f),
        P(RoadsidePropsRoot + "stop_sign_01.png", 229, 146, .27f),
        P(RoadsidePropsRoot + "utility_pole_01.png", 216, 148, .28f),
        P(IndustrialPropsRoot + "abandoned_pickup_01.png", 260, 157, .32f),
        P(IndustrialPropsRoot + "scrap_pile_01.png", 151, 253, .27f),
        P(IndustrialPropsRoot + "corrugated_shed_01.png", 145, 249, .30f),
        P(IndustrialPropsRoot + "road_barrier_01.png", 272, 144, .30f)
    ];

    /// <summary>
    /// Canvas heights in pixels at zoom 1, for procedurally placed art.
    ///
    /// A survivor sprite is about 100px tall, which is the human reference.
    /// Mature trees were previously 172px, barely taller than a person, which
    /// is why woodland read as scrub. The concept art puts a mature tree at
    /// roughly two and a half times a figure.
    ///
    /// Fewer, larger trees also cost less: canopy density is reduced to
    /// compensate, so the same coverage arrives in fewer draws.
    /// </summary>
    private const float UnderstoryHeight = 62f;
    private const float ClutterHeight = 56f;

    /// <summary>Cell pitch of the vegetation sample lattice.</summary>
    private const int VegetationStep = 3;

    /// <summary>Cell pitch of the ground-clutter sample lattice.</summary>
    private const int ClutterStep = 7;

    private Vector2I _coordinate;
    private Rect2 _gridBounds;
    private Rect2 _grownBounds;
    private Vector2 _canvasOrigin;

    public bool DrawLocationLabels { get; init; } = true;

    public void Initialize(Vector2I coordinate)
    {
        _coordinate = coordinate;
        _gridBounds = CountyCoordinateSpace.ChunkGridBounds(coordinate);
        _grownBounds = _gridBounds.Grow(6);
        _canvasOrigin = IsometricGrid.GridToScreen(_gridBounds.Position);
        Position = _canvasOrigin;
        ZAsRelative = false;
        ZIndex = -100;
        AddToGroup(RedrawGroup);
    }

    public override void _Ready()
    {
        // Shore dressing has to sit above the animated water surfaces, which
        // render between this chunk and the actors. A thin child CanvasItem at
        // the right depth is cheaper than promoting the whole landscape.
        CountyShorelineChunk shoreline = new() { Name = "Shoreline" };
        shoreline.Initialize(_coordinate);
        AddChild(shoreline);
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawWaterways();
        DrawRoadNetwork();
        DrawRoadJunctions();
        DrawRoadDressing();
        DrawRailwayCorridor();
        DrawFieldStructure();

        // Everything with height shares one depth-sorted pass so trunks, bushes,
        // rocks and set dressing overlap each other correctly rather than
        // layering by which rule happened to produce them.
        List<Placement> standing = [];
        CollectVegetation(standing);
        CollectGroundClutter(standing);
        CollectAuthoredProps(standing);
        CollectStartingArea(standing);
        DrawStanding(standing);

        DrawLandmarks();
    }

    // ---------------------------------------------------------------- water

    private void DrawWaterways()
    {
        // The animated lake polygon owns its continuous water fill. A narrow,
        // subdued earth ribbon softens the edge without forming the broad
        // angular tan wedges produced by the old macro shoreline.
        DrawPolylineRibbon(LakeOutline, .30f, new Color(.22f, .29f, .20f, .58f), 1.8f);

        // A narrow north-country tributary gives Old Mill Bridge its actual
        // geographic crossing. It is visual only and does not affect pathing.
        DrawStreamBanks(CountyTerrain.OldMillTributary, .48f);
        DrawStreamBanks(CountyMacroLayout.BlackwaterRiverCourse, .95f);
        DrawStreamBanks(CountyTerrain.MillCreek, .60f);
        DrawWaterArtDetails();
    }

    /// <summary>
    /// A damp margin along a watercourse.
    ///
    /// This used to be two opaque strokes, which gave every creek a hard olive
    /// border and made the rivers read as lined canals. The ground layer already
    /// surfaces the banks as mud and wetland, so all that is wanted here is a
    /// soft darkening right at the waterline.
    /// </summary>
    private void DrawStreamBanks(Vector2[] points, float bankWidth)
    {
        DrawPolylineRibbon(points, bankWidth + .55f, new Color(.29f, .28f, .20f, .34f), 2.5f);
        DrawPolylineRibbon(points, bankWidth + .18f, new Color(.34f, .29f, .19f, .40f), 2.5f);
    }

    private void DrawWaterArtDetails()
    {
        if (_gridBounds.HasPoint(new Vector2(151, 257)))
            DrawGroundTexture("res://assets/art/terrain/water/creek_rapids_01.png", new Vector2(151, 257), .40f, new Color(1, 1, 1, .72f));
        if (_gridBounds.HasPoint(new Vector2(283, 123)))
            DrawGroundTexture("res://assets/art/terrain/water/river_rapids_rocks_01.png", new Vector2(283, 123), .45f, new Color(1, 1, 1, .78f));
        if (_gridBounds.HasPoint(new Vector2(296, 101)))
            DrawGroundTexture("res://assets/art/terrain/water/river_rapids_straight_01.png", new Vector2(296, 101), .43f, new Color(1, 1, 1, .82f));
        if (_gridBounds.HasPoint(new Vector2(146, 242)))
            DrawGroundTexture("res://assets/art/terrain/water/pond_reeds_01.png", new Vector2(146, 242), .38f, new Color(1, 1, 1, .78f));
    }

    // ---------------------------------------------------------------- roads

    /// <summary>
    /// True for routes built from the authored dirt kit. Highway 16 and the
    /// town grid keep the ribbon renderer: they are asphalt and genuinely
    /// engineered, and the dirt kit would be the wrong material for them.
    /// </summary>
    private static bool UsesDirtKit(CountyRoadDefinition road) =>
        CountyRoadClasses.UsesDirtKit(road);

    /// <summary>
    /// Draw the composed dirt roads that fall inside this chunk.
    ///
    /// Composition is cached for the whole county, so this is a lookup and a
    /// draw. Surface slices go down first and the curve and junction pieces
    /// over them, which is what lets an intersection read as one road feature
    /// rather than several strips crossing.
    /// </summary>
    private void DrawDirtRoadPieces()
    {
        List<RoadPiecePlacement> placements = [];
        DirtRoadComposer.CollectFor(_grownBounds, placements);

        foreach (RoadPiecePlacement placement in placements)
        {
            if (placement.Kind != RoadPieceKind.Slice)
                continue;
            Texture2D texture = TextureRegistry.Get(placement.Texture);
            if (texture is null)
                continue;
            Vector2[] points = new Vector2[4];
            for (int index = 0; index < 4; index++)
                points[index] = P(placement.GridCorners[index]);
            Color[] colors = [placement.Tint, placement.Tint, placement.Tint, placement.Tint];
            DrawPolygon(points, colors, placement.Uvs, texture);
        }

        foreach (RoadPiecePlacement placement in placements)
        {
            if (placement.Kind != RoadPieceKind.Sprite || !_gridBounds.HasPoint(placement.Anchor))
                continue;
            Texture2D texture = TextureRegistry.Get(placement.Texture);
            if (texture is null)
                continue;
            Vector2 native = texture.GetSize();
            Vector2 size = native * placement.SpriteScale;
            Rect2 destination = new(P(placement.Anchor) - size * .5f, size);
            // Untransformed: a junction's or curve's arms already point along
            // the ground axes in the artwork. Fitting them to grid cells rotates
            // those arms a quarter turn off the roads they join.
            DrawTextureRect(texture, destination, false, placement.Tint);
            if (AssetInspector.Capturing)
                AssetInspector.Record(placement.Texture, placement.Anchor, native, size, true);
        }
    }

    private void DrawRoadNetwork()
    {
        // Shoulders for the whole network first, then carriageways. Drawing in
        // that order means an intersection's surfaces meet cleanly instead of
        // one road's shoulder cutting across another's asphalt.
        foreach (CountyRoadDefinition road in CountyTerrain.AllRoads)
            if (!UsesDirtKit(road))
                DrawRoadShoulder(road);
        DrawDirtRoadPieces();
        foreach (CountyRoadDefinition road in CountyTerrain.AllRoads)
            if (!UsesDirtKit(road))
                DrawRoadSurface(road);
        foreach (CountyRoadDefinition road in CountyTerrain.AllRoads)
            DrawRoadMarkings(road);
    }

    private void DrawRoadShoulder(CountyRoadDefinition road)
    {
        RoadSurfaceProfile profile = RoadSurfacePalette.For(road);
        float half = road.HalfWidth * RoadSurfacePalette.GridWidthScale;
        float outer = half + profile.ShoulderWidth * RoadSurfacePalette.GridWidthScale + .18f;

        // Both verges are drawn as one band under the carriageway, faded to
        // nothing at the outer edge so the road dissolves into the terrain art.
        DrawTexturedRibbon(road.Points, outer, profile.Shoulder,
            profile.ShoulderVLow, profile.ShoulderVHigh, profile.ShoulderStretch,
            new Color(profile.ShoulderTint, 0f), profile.ShoulderTint, profile.Wander);
    }

    private void DrawRoadSurface(CountyRoadDefinition road)
    {
        RoadSurfaceProfile profile = RoadSurfacePalette.For(road);
        float half = road.HalfWidth * RoadSurfacePalette.GridWidthScale;
        DrawTexturedRibbon(road.Points, half, profile.Surface,
            profile.SurfaceVLow, profile.SurfaceVHigh, profile.SurfaceStretch,
            profile.SurfaceTint, profile.SurfaceTint, profile.Wander * .45f);
    }

    private void DrawRoadMarkings(CountyRoadDefinition road)
    {
        if (!RoadSurfacePalette.For(road).CentreLine)
            return;
        float half = road.HalfWidth * RoadSurfacePalette.GridWidthScale;
        foreach ((Vector2 start, Vector2 end, int phase) in LocalSegments(road.Points, 2.2f))
        {
            if ((phase & 1) != 0)
                continue;
            Vector2 tangent = (end - start).Normalized();
            Vector2 normal = new(-tangent.Y, tangent.X);
            DrawRibbonQuad(start + tangent * .3f, end - tangent * .3f, normal * half * .055f, new Color("#c9b06a"));
        }
    }

    /// <summary>
    /// Authored junction tiles laid over the places roads actually cross.
    ///
    /// Two ribbons crossing just overlap, which looks like exactly what it is.
    /// The library carries proper isometric junction art, so a crossing gets a
    /// real one stamped over it and reads as built rather than incidental.
    /// </summary>
    private void DrawRoadJunctions()
    {
        foreach (CountyTerrain.RoadJunction junction in CountyTerrain.Junctions)
        {
            if (!_gridBounds.HasPoint(junction.Position))
                continue;
            int salt = Mathf.RoundToInt(junction.Position.X * 13 + junction.Position.Y * 7);
            if (!junction.Paved)
                continue;
            string texture = junction.Paved
                ? RoadArtRoot + "asphalt_intersection_01.png"
                : CountyTerrain.Hash01(salt, salt >> 2, 331) > .5f
                    ? RoadArtRoot + "dirt_junction_01.png"
                    : RoadArtRoot + "dirt_crossroads_01.png";
            // Requested at native width: these tiles are around 150px, and
            // asking for a road-width stamp would only enlarge them.
            DrawGroundTexture(texture, junction.Position, 1f,
                new Color(1, 1, 1, junction.Paved ? .62f : .48f));
        }
    }

    /// <summary>
    /// Roadside character: surface wear stamps on the carriageway and verge
    /// dressing just off it. Both are keyed to the road so they follow bends.
    /// </summary>
    private void DrawRoadDressing()
    {
        foreach (CountyRoadDefinition road in CountyTerrain.AllRoads)
        {
            // Authored pieces already carry ruts, stones and worn edges. Adding
            // procedural wear on top of them just reintroduces the repeating
            // streaks the kit was brought in to remove.
            if (UsesDirtKit(road))
                continue;
            RoadSurfaceProfile profile = RoadSurfacePalette.For(road);
            int salt = road.Id.GetHashCode();

            foreach ((Vector2 point, Vector2 tangent, int index) in SamplesAlong(road.Points, profile.WearSpacing))
            {
                if (!_gridBounds.HasPoint(point))
                    continue;
                if (CountyTerrain.Hash01(index, salt, 113) < .42f)
                    continue;
                float scale = (road.Major ? .38f : .28f) + CountyTerrain.Hash01(index, salt, 117) * .09f;
                // Minor roads get a whisper of wear. At full strength every
                // track grew its own extra set of parallel ruts on top of the
                // ones already in the surface material.
                DrawGroundTexture(profile.WearTexture, point, scale, new Color(1, 1, 1, road.Major ? .38f : .18f));
            }
        }
    }

    // ------------------------------------------------------------- railway

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

        foreach ((Vector2 point, _, int index) in SamplesAlong(rail, 10f))
        {
            if (_gridBounds.HasPoint(point) && (index & 1) == 0)
                DrawGroundTexture(RailArtRoot + "rail_straight_01.png", point, .38f, new Color(1, 1, 1, .72f));
        }
    }

    // --------------------------------------------------------------- fields

    private void DrawFieldStructure()
    {
        // The ground layer paints the soil itself now, so this pass only adds
        // the structure a field needs to read as worked land: plough direction,
        // boundary fences and standing crop.
        foreach (Field field in Fields)
        {
            if (!field.Bounds.Intersects(_grownBounds))
                continue;

            Color dark = new(field.Soil.Darkened(.28f), .42f);
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

            // Only fields whose season is "standing" carry visible crop, which
            // is what makes the plough/crop/fallow mosaic read at a glance.
            int index = System.Array.IndexOf(Fields, field);
            int fieldIndex = CountyTerrain.FieldIndex(field.Bounds.GetCenter());
            if (fieldIndex < 0 || CountyTerrain.StateOfField(fieldIndex) != FieldState.Standing)
                continue;
            string crop = CountyTerrain.Hash01(fieldIndex, index, 911) switch
            {
                < .34f => FarmPropsRoot + "corn_rows_02.png",
                < .62f => FarmPropsRoot + "crop_rows_green_01.png",
                < .84f => FarmPropsRoot + "wheat_patch_01.png",
                _ => FarmPropsRoot + "crop_rows_mixed_01.png"
            };
            for (float y = field.Bounds.Position.Y + 1.6f; y < field.Bounds.End.Y - 1f; y += 3.2f)
            {
                for (float x = field.Bounds.Position.X + 1.6f; x < field.Bounds.End.X - 1f; x += 3.2f)
                {
                    Vector2 point = new(x, y);
                    if (!_gridBounds.HasPoint(point))
                        continue;
                    DrawAnchoredTexture(crop, point, .38f, new Color(1, 1, 1, .94f));
                }
            }
        }

        // The agricultural centre uses the project's rural building artwork
        // rather than coloured blockout volumes.
        DrawFarmstead();
    }

    /// <summary>Farm District's working yard, built from real rural building art.</summary>
    private void DrawFarmstead()
    {
        (string Texture, Vector2 At, float Scale)[] farmstead =
        [
            (RuralBuildingsRoot + "farm_shelter_01.png", new Vector2(165.0f, 199.0f), .62f),
            (RuralBuildingsRoot + "wood_shelter_01.png", new Vector2(169.4f, 200.4f), .42f),
            (RuralBuildingsRoot + "shed_01.png", new Vector2(172.2f, 201.6f), .34f),
            (RuralBuildingsRoot + "tool_shed_01.png", new Vector2(177.0f, 202.2f), .34f),
            (RuralBuildingsRoot + "greenhouse_01.png", new Vector2(174.0f, 204.0f), .34f),
            (RuralBuildingsRoot + "garden_shed_01.png", new Vector2(162.0f, 202.4f), .30f),
            (FarmPropsRoot + "hay_bale_round_01.png", new Vector2(167.4f, 203.0f), .30f),
            (FarmPropsRoot + "hay_bale_square_01.png", new Vector2(168.4f, 203.6f), .30f),
            (FarmPropsRoot + "gate_01.png", new Vector2(171.0f, 197.6f), .32f)
        ];

        foreach ((string texture, Vector2 at, float scale) in farmstead)
        {
            if (_gridBounds.HasPoint(at))
                DrawAnchoredTexture(texture, at, scale, Colors.White);
        }
    }

    // ----------------------------------------------------------- vegetation

    /// <summary>
    /// Vegetation is placed from a clustered canopy field rather than a flat
    /// per-cell probability. That gives closed woodland, thinning edges and
    /// genuine clearings out of the same deterministic sample grid.
    /// </summary>
    private void CollectVegetation(List<Placement> output)
    {
        int startX = CountyTerrain.LatticeStart(_gridBounds.Position.X, VegetationStep);
        int startY = CountyTerrain.LatticeStart(_gridBounds.Position.Y, VegetationStep);
        int endX = Mathf.CeilToInt(_gridBounds.End.X) + VegetationStep;
        int endY = Mathf.CeilToInt(_gridBounds.End.Y) + VegetationStep;

        for (int y = startY; y < endY; y += VegetationStep)
        {
            for (int x = startX; x < endX; x += VegetationStep)
            {
                Vector2 point = new(
                    x + VegetationStep * .5f + (CountyTerrain.Hash01(x, y, 41) - .5f) * 3.0f,
                    y + VegetationStep * .5f + (CountyTerrain.Hash01(x, y, 43) - .5f) * 3.0f);
                if (!_gridBounds.HasPoint(point) || CountyTerrain.IsInLake(point))
                    continue;

                CountyBiome biome = CountyTerrain.BiomeAt(point);
                float baseDensity = BaseCanopyDensity(biome, point);
                if (baseDensity <= 0f)
                    continue;

                // Large soft masses decide where woodland actually sits; the
                // clearing term carves believable holes in it.
                float mass = CountyTerrain.Fbm(point, .055f, 1201);
                float canopy = Mathf.Clamp(baseDensity * (.45f + mass * 1.25f), 0f, 1f);
                canopy *= 1f - CountyTerrain.VegetationSuppression(point);
                if (canopy <= .02f)
                    continue;

                float roll = CountyTerrain.Hash01(x, y, 47);
                if (roll < 1f - canopy)
                {
                    // Not a trunk. Low growth belongs at a woodland edge, where
                    // it stops the canopy ending on a hard line. Sprinkling it
                    // across open meadow instead just adds noise, so open
                    // country is left to its grass.
                    bool edge = canopy is > .16f and < .58f;
                    if (edge && CountyTerrain.Hash01(x, y, 149) > .70f)
                        output.Add(Undergrowth(biome, point, x, y, 151, .90f));
                    continue;
                }

                // Three storeys. Closed canopy is mostly full-grown trees;
                // thinner cover is mid storey and saplings. That is what a wood
                // actually looks like, and it also lets every sprite be drawn at
                // a size its own resolution can carry.
                float tierRoll = CountyTerrain.Hash01(x, y, 157);
                VegetationTier tier = canopy > .50f && tierRoll > .34f ? VegetationTier.Large
                    : canopy > .24f || tierRoll > .58f ? VegetationTier.Medium
                    : VegetationTier.Sapling;
                float variant = CountyTerrain.Hash01(x, y, 53);
                string texture = VegetationCatalog.SelectTree(biome, tier, variant);
                // The upward half of this variation must not push the shortest
                // sprite in a tier past its own native height, or the scale cap
                // silently starts clamping and that one species reads soft.
                float height = VegetationCatalog.HeightFor(tier)
                    * (.86f + CountyTerrain.Hash01(x, y, 59) * .22f);
                output.Add(new Placement(texture, point, SpriteScaling.ForHeight(texture, height), CanopyTint(biome), true));

                // Understory beneath closed canopy only.
                if (canopy > .55f && CountyTerrain.Hash01(x, y, 61) > .72f)
                    output.Add(Undergrowth(biome, point + new Vector2(1.1f, -.4f), x, y, 63, .88f));
            }
        }
    }

    /// <summary>
    /// How wooded each region is. These are the numbers that make a region
    /// recognisable without a label: Pine Ridge is closed forest, the farm belt
    /// is open but for its hedgerow trees, the outskirts are patchy in between.
    ///
    /// Values are lower than a raw canopy fraction because trees are now
    /// two and a half times taller, so each one covers far more ground.
    /// </summary>
    private static float BaseCanopyDensity(CountyBiome biome, Vector2 point) => biome switch
    {
        CountyBiome.PineRidge => .62f,
        CountyBiome.Forest => .54f,
        CountyBiome.Mill => .48f,
        CountyBiome.Logging => IsLoggingClearing(point) ? .07f : .40f,
        CountyBiome.Outskirts => .22f,
        CountyBiome.Scrub => .13f,
        CountyBiome.Meadow => .10f,
        CountyBiome.Farm or CountyBiome.SouthFarm => IsFarmTreeLine(point) ? .34f : .02f,
        CountyBiome.TrailerPark => .06f,
        CountyBiome.Fairgrounds => .03f,
        CountyBiome.Urban => .04f,
        _ => 0f
    };


    /// <summary>
    /// One piece of ground cover, chosen from the biome's own pools and drawn at
    /// the size band its layer belongs to. A fern is not a flower is not a
    /// branch, so each gets its own height rather than a shared understory size.
    /// </summary>
    private static Placement Undergrowth(CountyBiome biome, Vector2 point, int x, int y, int salt, float alpha)
    {
        float layerRoll = CountyTerrain.Hash01(x, y, salt);
        float pickRoll = CountyTerrain.Hash01(x, y, salt + 7);
        (string texture, UndergrowthLayer layer) = VegetationCatalog.SelectUndergrowth(biome, layerRoll, pickRoll);
        float height = VegetationCatalog.HeightFor(layer) * (.84f + CountyTerrain.Hash01(x, y, salt + 13) * .32f);
        return new Placement(texture, point, SpriteScaling.ForHeight(texture, height), new Color(1, 1, 1, alpha), false);
    }

    private static Color CanopyTint(CountyBiome biome) => biome switch
    {
        CountyBiome.Mill => new Color(.84f, .91f, .83f),
        CountyBiome.PineRidge => new Color(.80f, .89f, .83f),
        CountyBiome.Logging => new Color(.89f, .87f, .78f),
        CountyBiome.Scrub => new Color(.94f, .92f, .78f),
        _ => Colors.White
    };

    /// <summary>
    /// Sparse ground furniture (rocks, deadfall, reeds), spread far more
    /// thinly than vegetation so it reads as incident rather than texture.
    /// </summary>
    private void CollectGroundClutter(List<Placement> output)
    {
        int startX = CountyTerrain.LatticeStart(_gridBounds.Position.X, ClutterStep);
        int startY = CountyTerrain.LatticeStart(_gridBounds.Position.Y, ClutterStep);
        int endX = Mathf.CeilToInt(_gridBounds.End.X);
        int endY = Mathf.CeilToInt(_gridBounds.End.Y);

        for (int y = startY; y < endY; y += ClutterStep)
        {
            for (int x = startX; x < endX; x += ClutterStep)
            {
                Vector2 point = new(
                    x + CountyTerrain.Hash01(x, y, 601) * (ClutterStep - .6f),
                    y + CountyTerrain.Hash01(x, y, 607) * (ClutterStep - .6f));
                if (!_gridBounds.HasPoint(point) || CountyTerrain.IsInLake(point))
                    continue;
                if (CountyTerrain.VegetationSuppression(point) > .55f)
                    continue;

                // A ploughed field has been cleared of its boulders and deadfall
                // by the people who work it. Leaving loose rock in the furrows
                // read as an obvious mistake.
                if (CountyTerrain.IsInField(point))
                    continue;

                float roll = CountyTerrain.Hash01(x, y, 613);
                if (roll > .30f)
                    continue;

                CountyBiome biome = CountyTerrain.BiomeAt(point);
                float water = CountyTerrain.DistanceToWater(point);
                string texture;
                if (water < 3.2f)
                    texture = VegetationCatalog.SelectWaterside(CountyTerrain.Hash01(x, y, 619));
                else
                    texture = biome switch
                    {
                        CountyBiome.PineRidge or CountyBiome.Scrub => CountyTerrain.Hash01(x, y, 623) > .5f
                            ? RoadsidePropsRoot + "cliff_rock_02.png" : RoadsidePropsRoot + "rock_slab_01.png",
                        // Focal woodland pieces: a mossy stump or a fallen log
                        // reads as somewhere, which uniform scatter never does.
                        CountyBiome.Forest or CountyBiome.Mill => VegetationCatalog.WoodlandFeatures[
                            (int)(CountyTerrain.Hash01(x, y, 623) * VegetationCatalog.WoodlandFeatures.Length)
                            % VegetationCatalog.WoodlandFeatures.Length],
                        CountyBiome.Logging => CountyTerrain.Hash01(x, y, 623) > .5f
                            ? LoggingPropsRoot + "log_pile_03.png" : LoggingPropsRoot + "stump_02.png",
                        _ => CountyTerrain.Hash01(x, y, 623) > .62f
                            ? RoadsidePropsRoot + "mossy_boulder_02.png" : RocksRoot + "rock_cluster_01.png"
                    };

                output.Add(new Placement(texture, point,
                    SpriteScaling.ForHeight(texture, ClutterHeight * (.8f + CountyTerrain.Hash01(x, y, 629) * .55f)),
                    new Color(1, 1, 1, .92f), false));
            }
        }
    }

    private void CollectAuthoredProps(List<Placement> output)
    {
        foreach (Prop prop in AuthoredProps)
        {
            if (_gridBounds.HasPoint(prop.Position))
                output.Add(new Placement(prop.Texture, prop.Position, prop.Scale, prop.Tint, true));
        }
    }

    private void CollectStartingArea(List<Placement> output)
    {
        if (!StartingAreaComposition.Bounds.Intersects(_gridBounds))
            return;
        foreach (StartingAreaComposition.Piece piece in StartingAreaComposition.Pieces)
        {
            if (_gridBounds.HasPoint(piece.Position))
                output.Add(new Placement(piece.Texture, piece.Position, piece.Scale, piece.Tint, true));
        }
    }

    /// <summary>
    /// Emit the standing art in two passes: low growth grouped purely by
    /// texture, then tall art in strict back-to-front order. See
    /// <see cref="Placement"/> for why the split is safe.
    /// </summary>
    private void DrawStanding(List<Placement> placements)
    {
        placements.Sort(static (left, right) =>
        {
            if (left.Tall != right.Tall)
                return left.Tall ? 1 : -1;
            if (!left.Tall)
                return string.CompareOrdinal(left.Texture, right.Texture);
            return (left.Position.X + left.Position.Y).CompareTo(right.Position.X + right.Position.Y);
        });

        // Resolving the texture once per run avoids a dictionary lookup per
        // sprite in what is the chunk's largest draw list.
        string current = string.Empty;
        Texture2D? texture = null;
        foreach (Placement placement in placements)
        {
            if (current != placement.Texture)
            {
                current = placement.Texture;
                texture = TextureRegistry.Get(current);
            }
            if (texture is null)
                continue;
            Vector2 native = texture.GetSize();
            Vector2 size = native * placement.Scale;
            DrawTextureRect(texture, new Rect2(P(placement.Position) - new Vector2(size.X * .5f, size.Y), size), false, placement.Tint);

            // This is the path every tree and every piece of undergrowth takes.
            // It draws directly rather than through DrawAnchoredTexture for
            // batching, which meant the asset inspector never saw a single
            // plant and always fell through to the ground beneath it.
            if (AssetInspector.Capturing)
                AssetInspector.Record(placement.Texture, placement.Position, native, size, false);
        }
    }

    // ------------------------------------------------------------ landmarks

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
                    // StartingAreaComposition owns this footprint in full.
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

    // ------------------------------------------------------- drawing helpers

    /// <summary>
    /// A mitred, UV-mapped ribbon along a county polyline. Widths are in grid
    /// cells so the strip foreshortens with the isometric projection, and each
    /// quad is emitted by exactly the chunk that contains its midpoint.
    /// </summary>
    /// <summary>
    /// A textured ribbon along a county polyline.
    ///
    /// The two sides are widened independently from a smooth noise field keyed
    /// to world position, so the strip breathes instead of holding one exact
    /// width for its whole length. A constant-width ribbon with perfectly
    /// parallel edges is the clearest sign of a road that was computed rather
    /// than built; letting the verge wander a little, and wander differently on
    /// each side, is most of what makes it read as worn ground.
    ///
    /// The wander is strongest on the shoulder pass, where the outer edge is
    /// already fading to nothing, and slight on the carriageway, which should
    /// still look like a maintained running surface.
    /// </summary>
    private void DrawTexturedRibbon(Vector2[] points, float gridHalfWidth, string texturePath,
        float vLow, float vHigh, float stretch, Color outerTint, Color innerTint, float wander)
    {
        Texture2D texture = TextureRegistry.Get(texturePath);
        if (texture is null || points.Length < 2)
            return;

        Vector2[] line = Resample(points, 2.4f);
        float band = Mathf.Max(.02f, vHigh - vLow);
        float repeat = Mathf.Max(1.2f, 2f * gridHalfWidth / band * stretch);

        float travelled = 0f;
        for (int index = 0; index < line.Length - 1; index++)
        {
            Vector2 a = line[index];
            Vector2 b = line[index + 1];
            float length = a.DistanceTo(b);
            if (length <= .0001f)
                continue;

            float uA = travelled / repeat;
            travelled += length;
            float uB = travelled / repeat;

            if (!_grownBounds.HasPoint((a + b) * .5f))
                continue;

            Vector2 unitA = MitreNormal(line, index);
            Vector2 unitB = MitreNormal(line, index + 1);
            Vector2 leftA = unitA * gridHalfWidth * EdgeWidth(a, wander, 0);
            Vector2 leftB = unitB * gridHalfWidth * EdgeWidth(b, wander, 0);
            Vector2 rightA = unitA * gridHalfWidth * EdgeWidth(a, wander, 977);
            Vector2 rightB = unitB * gridHalfWidth * EdgeWidth(b, wander, 977);

            if (outerTint == innerTint)
            {
                // Uniform strip: one quad spanning the full width.
                EmitBand(texture, a + leftA, b + leftB, Vector2.Zero, -(leftA + rightA), -(leftB + rightB),
                    uA, uB, vLow, vHigh, innerTint, innerTint);
                continue;
            }

            // Two half-bands: the outer edge of each fades out, so the shoulder
            // pass blends into the painted ground on both sides.
            EmitBand(texture, a, b, Vector2.Zero, leftA, leftB, uA, uB, vLow + band * .5f, vLow, innerTint, outerTint);
            EmitBand(texture, a, b, Vector2.Zero, -rightA, -rightB, uA, uB, vLow + band * .5f, vHigh, innerTint, outerTint);
        }
    }

    /// <summary>Width multiplier for one edge of a ribbon at a point.</summary>
    private static float EdgeWidth(Vector2 point, float wander, int salt) =>
        wander <= 0f ? 1f : 1f + (CountyTerrain.Fbm(point, 1f / 6.5f, 1877 + salt) - .5f) * 2f * wander;

    private void EmitBand(Texture2D texture, Vector2 a, Vector2 b, Vector2 innerOffset,
        Vector2 outerA, Vector2 outerB, float uA, float uB, float vInner, float vOuter,
        Color innerTint, Color outerTint)
    {
        Vector2[] points =
        [
            P(a + innerOffset), P(b + innerOffset), P(b + outerB), P(a + outerA)
        ];
        Vector2[] uvs =
        [
            new(uA, vInner), new(uB, vInner), new(uB, vOuter), new(uA, vOuter)
        ];
        Color[] colors = [innerTint, innerTint, outerTint, outerTint];
        DrawPolygon(points, colors, uvs, texture);
    }

    /// <summary>Averaged perpendicular at a polyline vertex, so bends do not notch.</summary>
    private static Vector2 MitreNormal(Vector2[] line, int index)
    {
        Vector2 previous = index > 0 ? (line[index] - line[index - 1]).Normalized() : Vector2.Zero;
        Vector2 next = index < line.Length - 1 ? (line[index + 1] - line[index]).Normalized() : Vector2.Zero;
        Vector2 tangent = (previous + next);
        if (tangent.LengthSquared() < .0001f)
            tangent = next.LengthSquared() > 0 ? next : previous;
        tangent = tangent.Normalized();
        return new Vector2(-tangent.Y, tangent.X);
    }

    private static Vector2[] Resample(Vector2[] points, float spacing)
    {
        List<Vector2> result = [points[0]];
        for (int index = 0; index < points.Length - 1; index++)
        {
            Vector2 a = points[index];
            Vector2 b = points[index + 1];
            int steps = Mathf.Max(1, Mathf.CeilToInt(a.DistanceTo(b) / spacing));
            for (int step = 1; step <= steps; step++)
                result.Add(a.Lerp(b, step / (float)steps));
        }
        return [.. result];
    }

    private void DrawRibbonQuad(Vector2 start, Vector2 end, Vector2 offset, Color color)
    {
        Vector2[] points = [P(start + offset), P(end + offset), P(end - offset), P(start - offset)];
        DrawColoredPolygon(points, color);
    }

    /// <summary>
    /// A field boundary, drawn as a continuous run rather than scattered posts.
    ///
    /// At three and a half cells apart the pieces read as unrelated litter
    /// dropped around the fields. Closing the spacing turns them into the
    /// hedgerow lines that give the farm belt its structure, and occasional
    /// gaps keep the run from looking machine-placed.
    /// </summary>
    private void DrawFenceLine(Vector2 start, Vector2 end)
    {
        float length = start.DistanceTo(end);
        int pieces = Mathf.Max(1, Mathf.CeilToInt(length / 1.7f));
        for (int index = 0; index <= pieces; index++)
        {
            Vector2 point = start.Lerp(end, index / (float)pieces);
            if (!_gridBounds.HasPoint(point))
                continue;
            float roll = CountyTerrain.Hash01((int)(point.X * 3), (int)(point.Y * 3), 91);
            if (roll < .14f)
                continue;
            string texture = roll > .82f
                ? FarmPropsRoot + "fence_overgrown_02.png"
                : PropsRoot + "fence_01.png";
            DrawAnchoredTexture(texture, point, roll > .82f ? .24f : .28f, new Color(.91f, .86f, .74f, .91f));
        }
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


    private void DrawGroundTexture(string path, Vector2 point, float scale, Color tint)
    {
        Texture2D texture = TextureRegistry.Get(path);
        if (texture is null) return;
        Vector2 size = texture.GetSize() * scale;
        DrawTextureRect(texture, new Rect2(P(point) - size * .5f, size), false, tint);
    }

    /// <summary>
    /// Scale needed to draw a sprite at a given canvas height.
    ///
    /// The art library mixes resolutions badly: pine_01 is 288x491 while
    /// pine_02 is 94x175. Choosing sizes by raw scale therefore produces trees
    /// that differ by a factor of three. Procedural placement asks for a height
    /// instead, so a mature tree is a mature tree whichever asset is picked.
    /// </summary>

    private void DrawAnchoredTexture(string path, Vector2 point, float scale, Color tint)
    {
        Texture2D texture = TextureRegistry.Get(path);
        if (texture is null) return;
        Vector2 native = texture.GetSize();
        Vector2 size = native * scale;
        DrawTextureRect(texture, new Rect2(P(point) - new Vector2(size.X * .5f, size.Y), size), false, tint);
        if (AssetInspector.Capturing)
            AssetInspector.Record(path, point, native, size, false);
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

        // Canvas-space strokes avoid oversized skewed quads where a bank crosses
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

    private static bool IsLoggingClearing(Vector2 point) =>
        CountyTerrain.Influence(point, new Vector2(105, 74), new Vector2(21, 15)) > .18f;

    private static Prop P(string texture, float x, float y, float scale) =>
        new(texture, new Vector2(x, y), scale, Colors.White);
}
