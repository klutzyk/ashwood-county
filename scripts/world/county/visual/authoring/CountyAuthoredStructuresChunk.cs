#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace AshwoodCounty.World.County.Visual.Authoring;

/// <summary>
/// Fixed, geographically meaningful structure/prop composition for one county
/// chunk. This layer is visual only: it neither changes navigation nor creates
/// gameplay collision. All repeated artwork is retained as draw commands.
/// </summary>
public partial class CountyAuthoredStructuresChunk : Node2D
{
    private readonly record struct Art(string Role, Vector2 At, float Scale, string[] Candidates, Color Tint);
    private readonly record struct House(Vector2 At, int Variant, bool Overgrown = false);

    private static readonly string[] House01 =
    [
        "res://assets/art/buildings/residential/house_01.png",
        "res://assets/art/buildings/residential/suburban_house_01.png",
        "res://assets/art/buildings/survival_cabin.png"
    ];
    private static readonly string[] House02 =
    [
        "res://assets/art/buildings/residential/house_02.png",
        "res://assets/art/buildings/residential/suburban_house_02.png",
        "res://assets/art/buildings/survival_cabin.png"
    ];
    private static readonly string[] House03 =
    [
        "res://assets/art/buildings/residential/house_03.png",
        "res://assets/art/buildings/residential/ranch_house_01.png",
        "res://assets/art/buildings/survival_cabin.png"
    ];
    private static readonly string[] House04 =
    [
        "res://assets/art/buildings/residential/house_04.png",
        "res://assets/art/buildings/residential/two_storey_house_01.png",
        "res://assets/art/buildings/survival_cabin.png"
    ];
    private static readonly string[] House05 =
    [
        "res://assets/art/buildings/residential/house_05.png",
        "res://assets/art/buildings/survival_cabin.png"
    ];
    private static readonly string[] House06 =
    [
        "res://assets/art/buildings/residential/house_06.png",
        "res://assets/art/buildings/survival_cabin.png"
    ];
    private static readonly string[] House07 =
    [
        "res://assets/art/buildings/residential/house_07.png",
        "res://assets/art/buildings/survival_cabin.png"
    ];
    private static readonly string[] House08 =
    [
        "res://assets/art/buildings/residential/house_08.png",
        "res://assets/art/buildings/survival_cabin.png"
    ];
    private static readonly string[] AbandonedHouse =
    [
        "res://assets/art/buildings/residential/abandoned_house_01.png",
        "res://assets/art/buildings/residential/house_abandoned_01.png",
        "res://assets/art/buildings/survival_cabin.png"
    ];
    private static readonly string[] AbandonedHouse02 =
    [
        "res://assets/art/buildings/residential/abandoned_house_02.png",
        "res://assets/art/buildings/residential/house_abandoned_02.png",
        "res://assets/art/buildings/survival_cabin.png"
    ];
    private static readonly string[] AbandonedHouse03 =
    [
        "res://assets/art/buildings/residential/abandoned_house_03.png",
        "res://assets/art/buildings/residential/house_abandoned_03.png",
        "res://assets/art/buildings/survival_cabin.png"
    ];
    private static readonly string[] AbandonedHouse04 =
    [
        "res://assets/art/buildings/residential/abandoned_house_04.png",
        "res://assets/art/buildings/residential/house_abandoned_04.png",
        "res://assets/art/buildings/survival_cabin.png"
    ];
    private static readonly string[] AbandonedHouse05 =
    [
        "res://assets/art/buildings/residential/abandoned_house_05.png",
        "res://assets/art/buildings/survival_cabin.png"
    ];
    private static readonly string[] AbandonedHouse06 =
    [
        "res://assets/art/buildings/residential/abandoned_house_06.png",
        "res://assets/art/buildings/survival_cabin.png"
    ];
    private static readonly string[] AbandonedHouse07 =
    [
        "res://assets/art/buildings/residential/abandoned_house_07.png",
        "res://assets/art/buildings/survival_cabin.png"
    ];
    private static readonly string[] AbandonedHouse08 =
    [
        "res://assets/art/buildings/residential/abandoned_house_08.png",
        "res://assets/art/buildings/survival_cabin.png"
    ];
    private static readonly string[] Shed =
    [
        "res://assets/art/buildings/rural/garden_shed_01.png",
        "res://assets/art/buildings/rural/shed_01.png",
        "res://assets/art/props/industrial/corrugated_shed_01.png"
    ];
    private static readonly string[] RuralCabin =
    [
        "res://assets/art/buildings/rural/cabin_01.png",
        "res://assets/art/buildings/rural/small_cabin_01.png",
        "res://assets/art/buildings/survival_cabin.png"
    ];
    private static readonly string[] Trailer =
    [
        "res://assets/art/buildings/rural/trailer_01.png",
        "res://assets/art/buildings/rural/mobile_home_01.png",
        "res://assets/art/props/vehicles/trailer_01.png",
        "res://assets/art/props/industrial/corrugated_shed_01.png"
    ];

    private static readonly House[] AshwoodHomes =
    [
        new(new(221, 122), 0), new(new(232, 122), 1), new(new(276, 122), 2), new(new(286, 122), 3, true),
        new(new(221, 133), 4), new(new(232, 133), 5, true), new(new(286, 133), 6),
        // (220,155) is the streamed reference interior and owns its exterior.
        new(new(231, 155), 0), new(new(276, 155), 1), new(new(286, 155), 2, true),
        new(new(220, 166), 3), new(new(231, 166), 4), new(new(242, 166), 5, true), new(new(276, 166), 6), new(new(286, 166), 7),
        new(new(264, 121), 1, true), new(new(264, 166), 4),
        // Second homes within selected larger blocks prevent the town from
        // reading as one isolated house per road intersection.
        new(new(225, 127), 6), new(new(236, 127), 7), new(new(280, 127), 4),
        new(new(225, 159), 5), new(new(235, 159), 6, true), new(new(280, 159), 7),
        new(new(225, 170), 0), new(new(247, 170), 2), new(new(281, 170), 3, true)
    ];

    private static readonly Art[] FixedArt =
    [
        // Highway 16 storytelling clusters, deliberately separated.
        A("sedan", 71, 149, .34f, "res://assets/art/props/vehicles/abandoned_sedan_01.png", "res://assets/art/props/vehicles/sedan_01.png", "res://assets/art/props/industrial/abandoned_pickup_01.png"),
        A("overturned", 77, 153, .33f, "res://assets/art/props/vehicles/overturned_vehicle_01.png", "res://assets/art/props/vehicles/wreck_01.png", "res://assets/art/props/industrial/scrap_pile_01.png"),
        A("school_bus", 119, 153, .31f, "res://assets/art/props/vehicles/school_bus_01.png", "res://assets/art/props/vehicles/bus_01.png", "res://assets/art/props/industrial/abandoned_pickup_01.png"),
        A("highway_pickup", 187, 146, .33f, "res://assets/art/props/vehicles/pickup_01.png", "res://assets/art/props/industrial/abandoned_pickup_01.png"),
        A("highway_van", 318, 135, .32f, "res://assets/art/props/vehicles/van_01.png", "res://assets/art/props/industrial/abandoned_pickup_01.png"),
        A("highway_truck", 329, 134, .33f, "res://assets/art/props/vehicles/box_truck_01.png", "res://assets/art/props/vehicles/truck_01.png", "res://assets/art/props/industrial/abandoned_pickup_01.png"),
        A("welcome_sign", 38, 148, .34f, "res://assets/art/props/landmarks/ashwood_welcome_sign_01.png", "res://assets/art/props/landmarks/welcome_sign_01.png", "res://assets/art/props/roadside/warning_sign_01.png"),

        // Hospital, Sheriff and town-service vehicles.
        A("ambulance", 239, 154, .34f, "res://assets/art/props/vehicles/ambulance_01.png", "res://assets/art/props/vehicles/abandoned_ambulance_01.png", "res://assets/art/props/industrial/abandoned_pickup_01.png"),
        A("police_car", 269, 140, .34f, "res://assets/art/props/vehicles/police_car_01.png", "res://assets/art/props/vehicles/sheriff_car_01.png", "res://assets/art/props/industrial/abandoned_pickup_01.png"),
        A("mainstreet_sedan", 253, 142, .31f, "res://assets/art/props/vehicles/abandoned_sedan_02.png", "res://assets/art/props/vehicles/sedan_02.png", "res://assets/art/props/industrial/abandoned_pickup_01.png"),
        A("mainstreet_suv", 263, 146, .31f, "res://assets/art/props/vehicles/suv_01.png", "res://assets/art/props/vehicles/jeep_01.png", "res://assets/art/props/industrial/abandoned_pickup_01.png"),

        // Service station and trailer park.
        A("station_pickup", 222, 193, .34f, "res://assets/art/props/vehicles/pickup_02.png", "res://assets/art/props/industrial/abandoned_pickup_01.png"),
        A("station_van", 230, 188, .32f, "res://assets/art/props/vehicles/van_02.png", "res://assets/art/props/industrial/abandoned_pickup_01.png"),
        A("trailer_van", 272, 213, .31f, "res://assets/art/props/vehicles/old_van_01.png", "res://assets/art/props/vehicles/van_01.png", "res://assets/art/props/industrial/abandoned_pickup_01.png"),
        A("trailer_car", 288, 208, .30f, "res://assets/art/props/vehicles/abandoned_sedan_03.png", "res://assets/art/props/vehicles/sedan_01.png", "res://assets/art/props/industrial/abandoned_pickup_01.png"),

        // South farmland and logging work yards.
        A("farm_pickup", 111, 259, .32f, "res://assets/art/props/vehicles/farm_pickup_01.png", "res://assets/art/props/industrial/abandoned_pickup_01.png"),
        A("logging_truck", 101, 77, .34f, "res://assets/art/props/vehicles/logging_truck_01.png", "res://assets/art/props/vehicles/box_truck_01.png", "res://assets/art/props/industrial/abandoned_pickup_01.png"),
        A("logging_pickup", 112, 70, .31f, "res://assets/art/props/vehicles/work_pickup_01.png", "res://assets/art/props/industrial/abandoned_pickup_01.png"),

        // Landmark art.
        A("lake_dock", 222, 84, .34f, "res://assets/art/props/landmarks/dock_01.png", "res://assets/art/props/landmarks/wooden_dock_01.png", "res://assets/art/props/logging/timber_stack_03.png"),
        A("picnic_table", 207, 80, .28f, "res://assets/art/props/landmarks/picnic_table_01.png", "res://assets/art/props/rural/picnic_table_01.png", "res://assets/art/props/industrial/crate_01.png"),
        A("trail_board", 84, 48, .30f, "res://assets/art/props/landmarks/trail_information_board_01.png", "res://assets/art/props/landmarks/trail_board_01.png", "res://assets/art/props/roadside/warning_sign_01.png"),
        A("ridge_campfire", 73, 39, .27f, "res://assets/art/props/landmarks/campfire_01.png", "res://assets/art/props/rural/campfire_01.png", "res://assets/art/props/industrial/barrels_01.png"),
        A("fire_tower", 311, 54, .38f, "res://assets/art/props/landmarks/fire_lookout_tower_01.png", "res://assets/art/props/landmarks/watchtower_01.png", "res://assets/art/props/industrial/watchtower_01.png"),
        A("communications", 316, 51, .30f, "res://assets/art/props/landmarks/communications_tower_01.png", "res://assets/art/props/urban/communications_tower_01.png", "res://assets/art/props/roadside/utility_pole_01.png")
    ];

    private Vector2I _coordinate;
    private Rect2 _gridBounds;
    private Vector2 _canvasOrigin;

    public void Initialize(Vector2I coordinate)
    {
        _coordinate = coordinate;
        _gridBounds = CountyCoordinateSpace.ChunkGridBounds(coordinate);
        _canvasOrigin = IsometricGrid.GridToScreen(_gridBounds.Position);
        Position = _canvasOrigin;
        ZAsRelative = false;
        ZIndex = -72;
    }

    public override void _Ready() => QueueRedraw();

    public override void _Draw()
    {
        DrawAshwoodNeighborhoods();
        DrawMainStreet();
        DrawHospital();
        DrawSheriffsOffice();
        DrawServiceStation();
        DrawTrailerPark();
        DrawFairgrounds();
        DrawRuralSheetStructures();
        DrawSouthFarmland();
        DrawLoggingCamp();
        DrawPineRidge();
        DrawFireLookout();
        DrawBlackwater();
        DrawDam();
        DrawOldMillBridge();
        DrawHighwayInfrastructure();
        DrawAdditionalUrbanProps();
        DrawFixedArt();
    }

    private void DrawAshwoodNeighborhoods()
    {
        foreach (House house in AshwoodHomes)
        {
            if (!Owns(house.At)) continue;
            DrawLot(house.At, house.Overgrown);
            string[] candidates = HouseCandidates(house.Variant, house.Overgrown);
            DrawAssetAtHeight($"ashwood_house_{house.Variant}_{house.Overgrown}", house.At, 132f, Colors.White, candidates);

            Vector2 shed = house.At + new Vector2(2.8f, 2.1f);
            if (((int)house.At.X + (int)house.At.Y) % 3 == 0)
                DrawAsset("garden_shed", shed, .25f, new Color(1, 1, 1, .94f), Shed);
            DrawUrbanProp("mailbox", house.At + new Vector2(-2.5f, 2.5f), .25f,
                "res://assets/art/props/urban/mailbox_01.png", "res://assets/art/props/roadside/utility_pole_01.png");
        }

        // Street trees, lights and utility rhythm make the neighborhood legible.
        for (int y = 121; y <= 169; y += 11)
        {
            for (int x = 217; x <= 289; x += 11)
            {
                Vector2 at = new(x, y + 2.2f);
                if (!Owns(at)) continue;
                DrawAsset("street_tree", at, .24f, Colors.White,
                    "res://assets/art/trees/maple_autumn_small_01.png");
                if (((x + y) / 11 & 1) == 0)
                    DrawUrbanProp("street_light", at + new Vector2(2.5f, .4f), .28f,
                        "res://assets/art/props/urban/street_light_01.png", "res://assets/art/props/roadside/street_light_01.png");
            }
        }
    }

    private void DrawMainStreet()
    {
        Vector2[] north = [new(238, 137), new(245, 137), new(252, 137), new(259, 137), new(266, 137)];
        string[] names = ["DINER", "HARDWARE", "PHARMACY", "MARKET", "OFFICES"];
        Color[] roofs = [new("#744a35"), new("#4f615d"), new("#5b664b"), new("#6a5440"), new("#555c62")];
        for (int i = 0; i < north.Length; i++)
        {
            Vector2 at = north[i];
            if (!Owns(at)) continue;
            DrawParking(new Rect2(at - new Vector2(2.5f, 3.5f), new Vector2(5f, 3f)), 1);
            DrawStorefront(at, new Vector2(4.4f, 3.0f), new Color("#777167"), roofs[i], 38);
            DrawCleanLabel(at + new Vector2(-1.8f, .2f), names[i], new Color("#e1d2a7"), 11);
            DrawUrbanProp("dumpster", at + new Vector2(2.7f, -1.7f), .25f,
                "res://assets/art/props/urban/dumpster_01.png", "res://assets/art/props/industrial/scrap_pile_01.png");
        }

        foreach (Vector2 at in new[] { new Vector2(239, 143), new Vector2(250, 143), new Vector2(261, 143), new Vector2(272, 143) })
        {
            if (!Owns(at)) continue;
            DrawUrbanProp("bench", at, .25f, "res://assets/art/props/urban/bench_01.png", "res://assets/art/props/industrial/crate_01.png");
            DrawUrbanProp("hydrant", at + new Vector2(2, .6f), .23f, "res://assets/art/props/urban/fire_hydrant_01.png", "res://assets/art/props/roadside/warning_sign_01.png");
        }

        foreach (Vector2 at in new[] { new Vector2(235, 143), new Vector2(268, 143), new Vector2(268, 132), new Vector2(235, 132) })
            DrawUrbanPropIfOwned("traffic_light", at, .29f, "res://assets/art/props/urban/traffic_light_01.png", "res://assets/art/props/roadside/stop_sign_01.png");

        // Town green and civic seating.
        if (Owns(new Vector2(266, 160)))
        {
            DrawAsset("town_green_tree", new Vector2(266, 161), .31f, Colors.White, "res://assets/art/environment/vegetation/oak_01.png");
            DrawUrbanProp("bench", new Vector2(262, 159), .25f, "res://assets/art/props/urban/bench_01.png", "res://assets/art/props/industrial/crate_01.png");
        }
    }

    private void DrawHospital()
    {
        Vector2 at = new(244, 151);
        if (!Owns(at)) return;
        DrawParking(new Rect2(238.5f, 147, 11f, 8f), 5);
        DrawBuilding(at, new Vector2(5.4f, 4.2f), new Color("#c0c3b8"), new Color("#6f7977"), 49);
        DrawBuilding(at + new Vector2(3.2f, -1.1f), new Vector2(2.5f, 2.4f), new Color("#aeb3aa"), new Color("#65706f"), 34);
        DrawCleanLabel(at + new Vector2(-2.7f, .7f), "HOSPITAL", new Color("#f2eee0"), 13);
        DrawCross(P(at) + new Vector2(0, -66), 8f, new Color("#b9433f"));
        DrawUrbanProp("hospital_light", at + new Vector2(-6, 4), .30f,
            "res://assets/art/props/urban/street_light_01.png", "res://assets/art/props/roadside/street_light_01.png");
        DrawUrbanProp("hospital_barrier", at + new Vector2(5.5f, 4.2f), .30f,
            "res://assets/art/props/urban/road_barrier_01.png", "res://assets/art/props/industrial/road_barrier_01.png");
    }

    private void DrawSheriffsOffice()
    {
        Vector2 at = new(272, 137);
        if (!Owns(at)) return;
        DrawParking(new Rect2(267, 133.5f, 9.5f, 7f), 4);
        DrawBuilding(at, new Vector2(4.3f, 3.3f), new Color("#9c9584"), new Color("#544f49"), 39);
        DrawCleanLabel(at + new Vector2(-2.5f, .8f), "SHERIFF", new Color("#e9d6a3"), 12);
        for (int i = 0; i < 4; i++)
            DrawUrbanProp("sheriff_barrier", at + new Vector2(-5 + i * 2.2f, 4.1f), .26f,
                "res://assets/art/props/urban/road_barrier_01.png", "res://assets/art/props/industrial/road_barrier_01.png");
    }

    private void DrawServiceStation()
    {
        Vector2 at = new(226, 190);
        if (!Owns(at)) return;
        DrawParking(new Rect2(219.5f, 186, 13f, 8.5f), 4);
        DrawBuilding(new Vector2(230, 188), new Vector2(4.1f, 3.2f), new Color("#77746a"), new Color("#58635b"), 34);
        DrawCanopy(new Vector2(224, 189), new Vector2(4.8f, 2.5f));
        DrawCleanLabel(new Vector2(228, 188), "SERVICE", new Color("#e6d39c"), 11);
        for (int i = 0; i < 3; i++)
            DrawPump(new Vector2(222 + i * 2.1f, 190.3f));
        DrawUrbanProp("station_sign", new Vector2(217.5f, 187), .32f,
            "res://assets/art/props/urban/service_station_sign_01.png", "res://assets/art/props/roadside/warning_sign_01.png");
        DrawAsset("station_barrels", new Vector2(234, 190), .27f, Colors.White, "res://assets/art/props/industrial/barrels_01.png");
    }

    private void DrawTrailerPark()
    {
        Vector2 center = new(279, 211);
        if (!Owns(center)) return;
        DrawDiamondRect(new Rect2(269, 203, 20, 14), new Color(.32f, .31f, .25f, .20f));
        Vector2[] trailers = [new(270, 205), new(278, 204), new(287, 205), new(271, 214), new(280, 212), new(288, 216)];
        for (int i = 0; i < trailers.Length; i++)
        {
            Vector2 at = trailers[i];
            DrawAsset($"trailer_{i % 2}", at, .30f, i == 4 ? new Color(.82f, .80f, .74f) : Colors.White, Trailer);
            DrawUrbanProp("trash_bin", at + new Vector2(3.1f, 1.8f), .24f,
                "res://assets/art/props/urban/trash_bin_01.png", "res://assets/art/props/industrial/barrels_01.png");
            if ((i & 1) == 0)
                DrawAsset("trailer_shed", at + new Vector2(-2.6f, 2.5f), .23f, Colors.White, Shed);
        }
        for (int i = 0; i < 5; i++)
            DrawUrbanProp("trailer_pole", new Vector2(265 + i * 6.2f, 218), .28f,
                "res://assets/art/props/urban/utility_pole_01.png", "res://assets/art/props/roadside/utility_pole_01.png");
    }

    private void DrawFairgrounds()
    {
        Vector2 center = new(246, 234);
        if (!Owns(center)) return;
        DrawParking(new Rect2(249, 230, 7, 7), 4);
        DrawFenceRectangle(new Rect2(233, 225, 28, 18), .25f);
        DrawAssetAtHeight("fair_shelter", new Vector2(240, 232), 84f, Colors.White,
            "res://assets/art/buildings/rural/farm_shelter_01.png", "res://assets/art/props/industrial/corrugated_shed_01.png");
        DrawAssetAtHeight("fair_tool_shed", new Vector2(248, 237), 66f, Colors.White,
            "res://assets/art/buildings/rural/tool_shed_01.png", "res://assets/art/props/industrial/corrugated_shed_01.png");
        DrawCleanLabel(new Vector2(239, 230), "FAIRGROUNDS", new Color("#dfca92"), 12);
        foreach (Vector2 at in new[] { new Vector2(238, 228), new Vector2(246, 228), new Vector2(254, 237) })
            DrawAsset("fair_picnic", at, .26f, Colors.White,
                "res://assets/art/props/landmarks/picnic_table_01.png", "res://assets/art/props/industrial/crate_01.png");
        DrawAsset("fair_trailer", new Vector2(258, 232), .28f, new Color(.88f, .87f, .82f),
            "res://assets/art/props/vehicles/utility_trailer_01.png", "res://assets/art/props/industrial/crate_01.png");
        DrawAsset("fair_barriers", new Vector2(253, 241), .28f, Colors.White,
            "res://assets/art/props/roadside/cones_barrier_01.png", "res://assets/art/props/industrial/road_barrier_01.png");
        for (int x = 236; x <= 248; x += 4)
            DrawAsset("fair_crate", new Vector2(x, 240), .24f, Colors.White, "res://assets/art/props/industrial/crate_01.png");
    }

    private void DrawRuralSheetStructures()
    {
        // Farm District: productive outbuildings break up the large fields.
        DrawFittedIfOwned("farm_greenhouse", new Vector2(176, 198), 78f,
            "res://assets/art/buildings/rural/greenhouse_01.png");
        DrawFittedIfOwned("farm_shelter", new Vector2(181, 204), 82f,
            "res://assets/art/buildings/rural/farm_shelter_01.png", "res://assets/art/props/industrial/corrugated_shed_01.png");
        DrawFittedIfOwned("farm_tool_shed", new Vector2(159, 212), 66f,
            "res://assets/art/buildings/rural/tool_shed_01.png", "res://assets/art/props/industrial/corrugated_shed_01.png");
        DrawFittedIfOwned("farm_garden", new Vector2(183, 211), 62f,
            "res://assets/art/props/rural/garden_plot_01.png", "res://assets/art/props/farm/crop_rows_mixed_01.png");
        DrawFittedIfOwned("farm_wood_shelter", new Vector2(151, 197), 67f,
            "res://assets/art/buildings/rural/wood_shelter_01.png", "res://assets/art/props/logging/timber_stack_03.png");

        // Outskirts rural properties, away from the active starting camp.
        DrawFittedIfOwned("outskirts_cabin", new Vector2(184, 166), 116f,
            "res://assets/art/buildings/rural/work_cabin_01.png", "res://assets/art/buildings/survival_cabin.png");
        DrawFittedIfOwned("outskirts_greenhouse", new Vector2(187, 170), 72f,
            "res://assets/art/buildings/rural/greenhouse_01.png");
        DrawFittedIfOwned("outskirts_shed", new Vector2(216, 170), 62f,
            "res://assets/art/buildings/rural/garden_shed_01.png", "res://assets/art/props/industrial/corrugated_shed_01.png");

        // Mill Creek and Logging Camp work structures.
        DrawFittedIfOwned("mill_work_cabin", new Vector2(136, 247), 112f,
            "res://assets/art/buildings/rural/work_cabin_01.png", "res://assets/art/buildings/survival_cabin.png");
        DrawFittedIfOwned("mill_tool_shed", new Vector2(143, 259), 64f,
            "res://assets/art/buildings/rural/tool_shed_01.png", "res://assets/art/props/industrial/ruined_shed_01.png");
        DrawFittedIfOwned("logging_work_cabin", new Vector2(106, 67), 108f,
            "res://assets/art/buildings/rural/work_cabin_01.png", "res://assets/art/buildings/survival_cabin.png");
        DrawFittedIfOwned("logging_wood_shelter", new Vector2(116, 77), 70f,
            "res://assets/art/buildings/rural/wood_shelter_01.png", "res://assets/art/props/logging/timber_stack_03.png");

        // South Farmland homestead detail.
        DrawFittedIfOwned("south_greenhouse", new Vector2(130, 279), 76f,
            "res://assets/art/buildings/rural/greenhouse_01.png");
        DrawFittedIfOwned("south_garden", new Vector2(179, 280), 62f,
            "res://assets/art/props/rural/garden_plot_01.png", "res://assets/art/props/farm/crop_rows_green_01.png");
        DrawFittedIfOwned("south_farm_shelter", new Vector2(198, 265), 80f,
            "res://assets/art/buildings/rural/farm_shelter_01.png", "res://assets/art/props/industrial/corrugated_shed_01.png");
        DrawFittedIfOwned("south_center_greenhouse", new Vector2(179, 261), 74f,
            "res://assets/art/buildings/rural/greenhouse_01.png");
        DrawFittedIfOwned("south_center_hay", new Vector2(170, 263), 42f,
            "res://assets/art/props/farm/hay_bale_round_01.png");
    }

    private void DrawSouthFarmland()
    {
        Vector2[] farmhouses = [new(104, 247), new(125, 283), new(174, 260), new(174, 281), new(203, 262)];
        for (int i = 0; i < farmhouses.Length; i++)
        {
            Vector2 at = farmhouses[i];
            if (!Owns(at)) continue;
            DrawAssetAtHeight($"farmhouse_{i % 3}", at, 138f, new Color(.94f, .91f, .83f), (i % 3) switch { 0 => House01, 1 => House02, _ => RuralCabin });
            DrawAsset("farm_shed", at + new Vector2(5, 1), .28f, Colors.White, Shed);
            DrawAsset("hay_bales", at + new Vector2(-4, 3), .31f, Colors.White,
                i % 2 == 0 ? "res://assets/art/props/farm/hay_bale_round_01.png" : "res://assets/art/props/farm/hay_bale_square_01.png");
            DrawFenceRectangle(new Rect2(at - new Vector2(5, 4), new Vector2(10, 8)), .23f);
        }
    }

    private void DrawLoggingCamp()
    {
        Vector2 center = new(105, 74);
        if (!Owns(center)) return;
        DrawDiamondRect(new Rect2(94, 65, 23, 17), new Color(.32f, .27f, .19f, .22f));
        DrawAsset("logging_shed", new Vector2(98, 69), .34f, Colors.White, Shed);
        DrawAsset("logging_shed", new Vector2(111, 67), .31f, new Color(.87f, .86f, .80f), Shed);
        for (int y = 72; y <= 81; y += 4)
        {
            for (int x = 92; x <= 116; x += 6)
                DrawAsset("timber_stack", new Vector2(x, y), .31f, Colors.White, "res://assets/art/props/logging/timber_stack_03.png");
        }
        foreach (Vector2 at in new[] { new Vector2(101, 72), new Vector2(107, 76), new Vector2(112, 81) })
            DrawAsset("timber_stack_large", at, .44f, Colors.White, "res://assets/art/props/logging/timber_stack_03.png");
        foreach (Vector2 at in new[] { new Vector2(94, 65), new Vector2(119, 70), new Vector2(114, 83), new Vector2(89, 78) })
            DrawAsset("logging_stump", at, .29f, Colors.White, "res://assets/art/props/logging/stump_02.png");
        foreach (Vector2 at in new[] { new Vector2(88, 63), new Vector2(91, 86), new Vector2(120, 61), new Vector2(124, 82), new Vector2(84, 76) })
            DrawAsset("logging_edge_pine", at, .28f, new Color(.88f, .92f, .84f),
                "res://assets/art/vegetation/pine_03.png", "res://assets/art/environment/vegetation/pine_01.png");
        DrawCleanLabel(new Vector2(100, 65), "LOGGING CAMP", new Color("#dfc68c"), 12);
    }

    private void DrawPineRidge()
    {
        Vector2[] cabins = [new(46, 37), new(68, 34), new(70, 54), new(91, 31)];
        for (int i = 0; i < cabins.Length; i++)
        {
            Vector2 at = cabins[i];
            if (!Owns(at)) continue;
            DrawAssetAtHeight($"ridge_cabin_{i}", at, 118f, i == 2 ? new Color(.77f, .75f, .69f) : Colors.White, RuralCabin);
            DrawAsset("ridge_rock", at + new Vector2(4, 1), .30f, Colors.White,
                i % 2 == 0 ? "res://assets/art/props/roadside/cliff_rock_03.png" : "res://assets/art/props/roadside/boulder_cluster_03.png");
        }
        foreach (Vector2 at in new[] { new Vector2(37, 25), new Vector2(53, 20), new Vector2(69, 27), new Vector2(82, 18), new Vector2(96, 42) })
            DrawAsset("ridge_outcrop", at, .34f, Colors.White, "res://assets/art/props/roadside/rock_formation_02.png");
        foreach (Vector2 at in new[] { new Vector2(58, 31), new Vector2(63, 42), new Vector2(75, 31), new Vector2(80, 43), new Vector2(88, 36), new Vector2(54, 47) })
            DrawAssetAtHeight("ridge_pine_cluster", at, 150f, new Color(.84f, .91f, .85f),
                "res://assets/art/trees/spruce_medium_01.png");
    }

    private void DrawFireLookout()
    {
        Vector2 center = new(311, 54);
        if (!Owns(center)) return;
        DrawDiamondRect(new Rect2(309, 52, 4.5f, 3.5f), new Color(.38f, .35f, .25f, .30f));
        DrawAsset("lookout_shed", new Vector2(306, 57), .25f, Colors.White, Shed);
        DrawAsset("lookout_crates", new Vector2(316, 57), .25f, Colors.White, "res://assets/art/props/industrial/crate_01.png");
        DrawAsset("lookout_barrels", new Vector2(317, 54), .24f, Colors.White, "res://assets/art/props/industrial/barrels_01.png");
        DrawCleanLabel(new Vector2(307, 49), "FIRE LOOKOUT", new Color("#e3cc91"), 12);
    }

    private void DrawBlackwater()
    {
        // Shore access and a restrained picnic/boating destination.
        Vector2 center = new(211, 81);
        if (!Owns(center)) return;
        DrawDiamondRect(new Rect2(206, 78, 10, 6), new Color(.34f, .31f, .22f, .50f));
        DrawAsset("lake_bin", new Vector2(213, 80), .23f, Colors.White,
            "res://assets/art/props/urban/trash_bin_01.png", "res://assets/art/props/industrial/barrels_01.png");
        DrawAsset("lake_reeds", new Vector2(217, 84), .31f, Colors.White, "res://assets/art/undergrowth/grass_pampas_01.png");
        DrawAsset("lake_rocks", new Vector2(203, 80), .31f, Colors.White, "res://assets/art/props/roadside/mossy_boulder_02.png");
    }

    private void DrawDam()
    {
        Vector2 center = new(301, 103);
        if (!Owns(center)) return;
        DrawDiamondRect(new Rect2(297.5f, 99.5f, 7.5f, 5.5f), new Color(.30f, .31f, .29f, .54f));
        DrawDamWall(new Vector2(303, 102.5f), new Vector2(298.2f, 101.5f));
        DrawBuilding(new Vector2(304, 100), new Vector2(3.4f, 2.7f), new Color("#878983"), new Color("#565d5a"), 34);
        DrawCleanLabel(new Vector2(301, 99), "DAM CONTROL", new Color("#e7e0c7"), 11);
        for (int i = 0; i < 4; i++)
            DrawAsset("dam_barrier", new Vector2(297.5f + i * 2f, 106), .30f, Colors.White,
                "res://assets/art/props/roadside/concrete_barrier_01.png");
        DrawAsset("dam_pipes", new Vector2(307, 104), .31f, Colors.White, "res://assets/art/props/industrial/concrete_pipes_01.png");
        DrawAsset("dam_pole", new Vector2(309, 99), .30f, Colors.White, "res://assets/art/props/roadside/utility_pole_01.png");
    }

    private void DrawOldMillBridge()
    {
        Vector2 center = new(166, 121);
        if (!Owns(center)) return;
        Vector2 a = P(new Vector2(163.3f, 123.7f));
        Vector2 b = P(new Vector2(168.7f, 118.3f));
        DrawLine(a, b, new Color("#3f3326"), 28f, true);
        DrawLine(a, b, new Color("#8a7655"), 18f, true);
        Vector2 tangent = (b - a).Normalized();
        Vector2 normal = new(-tangent.Y, tangent.X);
        for (int i = 0; i <= 6; i++)
        {
            Vector2 p = a.Lerp(b, i / 6f);
            DrawLine(p - normal * 12f, p + normal * 12f, new Color("#4e3a28"), 2f, true);
        }
        DrawCleanLabel(new Vector2(164, 122), "OLD MILL BRIDGE", new Color("#ddc891"), 11);
    }

    private void DrawHighwayInfrastructure()
    {
        foreach (Vector2 at in new[] { new Vector2(46, 148), new Vector2(91, 151), new Vector2(140, 152), new Vector2(286, 140), new Vector2(344, 132) })
            DrawUrbanPropIfOwned("utility_pole", at, .29f, "res://assets/art/props/urban/utility_pole_01.png", "res://assets/art/props/roadside/utility_pole_01.png");
        foreach (Vector2 at in new[] { new Vector2(82, 151), new Vector2(86, 151), new Vector2(312, 136), new Vector2(316, 135) })
            DrawUrbanPropIfOwned("road_barrier", at, .29f, "res://assets/art/props/urban/road_barrier_01.png", "res://assets/art/props/industrial/road_barrier_01.png");
        foreach (Vector2 at in new[] { new Vector2(25, 150), new Vector2(151, 150), new Vector2(305, 138), new Vector2(357, 130) })
            DrawUrbanPropIfOwned("highway_sign", at, .29f, "res://assets/art/props/urban/road_sign_01.png", "res://assets/art/props/roadside/speed_sign_55_01.png");
    }

    private void DrawAdditionalUrbanProps()
    {
        // Transit and public-space props remain tied to streets and civic uses.
        DrawFittedIfOwned("bus_shelter", new Vector2(259, 144), 58f,
            "res://assets/art/props/urban/bus_shelter_01.png");
        DrawFittedIfOwned("bike_rack", new Vector2(248, 155), 38f,
            "res://assets/art/props/urban/bicycle_rack_01.png");
        DrawFittedIfOwned("newspaper_box", new Vector2(251, 143), 34f,
            "res://assets/art/props/urban/newspaper_box_01.png");
        DrawFittedIfOwned("town_planter", new Vector2(264, 158), 37f,
            "res://assets/art/props/urban/county_planter_01.png",
            "res://assets/art/props/urban/planter_01.png");
        DrawFittedIfOwned("commercial_hvac", new Vector2(260, 134), 39f,
            "res://assets/art/props/urban/hvac_unit_01.png");
        DrawFittedIfOwned("commercial_cabinet", new Vector2(267, 134), 40f,
            "res://assets/art/props/urban/electrical_cabinet_01.png");
        DrawFittedIfOwned("hospital_cones", new Vector2(238, 156), 34f,
            "res://assets/art/props/urban/traffic_cone_01.png",
            "res://assets/art/props/urban/traffic_cones_01.png");

        // Singular wilderness story beats; no repeated landmark scattering.
        DrawFittedIfOwned("ruined_wilderness_cabin", new Vector2(124, 91), 105f,
            "res://assets/art/props/landmarks/ruined_cabin_01.png",
            "res://assets/art/buildings/residential/abandoned_house_01.png");
        DrawFittedIfOwned("ridge_viewpoint", new Vector2(76, 33), 70f,
            "res://assets/art/props/landmarks/ridge_viewpoint_01.png",
            "res://assets/art/props/landmarks/lookout_viewpoint_01.png",
            "res://assets/art/props/roadside/rock_slab_01.png");
    }

    private void DrawFixedArt()
    {
        foreach (Art art in FixedArt)
        {
            if (Owns(art.At))
                DrawAsset(art.Role, art.At, art.Scale, art.Tint, art.Candidates);
        }
    }

    private void DrawLot(Vector2 center, bool overgrown)
    {
        DrawDiamondRect(new Rect2(center + new Vector2(-.4f, 1.0f), new Vector2(.8f, 1.65f)), new Color(.35f, .34f, .31f, .52f));
        if (overgrown)
            DrawAsset("yard_shrub", center + new Vector2(-3, -1), .24f, new Color(1, 1, 1, .9f), "res://assets/art/undergrowth/bush_green_01.png");
    }

    private void DrawParking(Rect2 gridRect, int stripeCount)
    {
        DrawDiamondRect(gridRect, new Color(.25f, .26f, .25f, .92f));
        for (int i = 1; i <= stripeCount; i++)
        {
            float x = gridRect.Position.X + gridRect.Size.X * i / (stripeCount + 1f);
            DrawLine(P(new Vector2(x, gridRect.Position.Y + .7f)), P(new Vector2(x, gridRect.End.Y - .7f)), new Color(.75f, .70f, .54f, .48f), 1.6f, true);
        }
    }

    private void DrawFenceRectangle(Rect2 rect, float scale)
    {
        List<Vector2> points = [];
        for (float x = rect.Position.X; x <= rect.End.X; x += 3.2f)
        {
            points.Add(new Vector2(x, rect.Position.Y));
            points.Add(new Vector2(x, rect.End.Y));
        }
        for (float y = rect.Position.Y + 3.2f; y < rect.End.Y; y += 3.2f)
        {
            points.Add(new Vector2(rect.Position.X, y));
            points.Add(new Vector2(rect.End.X, y));
        }
        foreach (Vector2 point in points)
            DrawAsset("fence", point, scale, new Color(1, 1, 1, .9f), "res://assets/art/environment/props/fence_01.png");
    }

    private void DrawCanopy(Vector2 center, Vector2 size)
    {
        Vector2[] baseShape = ProjectRect(center, size);
        Vector2 lift = new(0, -30);
        DrawColoredPolygon(baseShape.Select(p => p + lift).ToArray(), new Color("#755b42"));
        DrawPolyline(baseShape.Select(p => p + lift).Append(baseShape[0] + lift).ToArray(), new Color("#b59b6b"), 2f, true);
        foreach (Vector2 p in baseShape)
            DrawLine(p, p + lift, new Color("#6a675d"), 3f, true);
    }

    private void DrawPump(Vector2 at)
    {
        Vector2 p = P(at);
        DrawRect(new Rect2(p - new Vector2(4, 12), new Vector2(8, 12)), new Color("#8c4f37"));
        DrawRect(new Rect2(p - new Vector2(2, 10), new Vector2(4, 3)), new Color("#d5c9a4"));
    }

    private void DrawDamWall(Vector2 start, Vector2 end)
    {
        Vector2 a = P(start);
        Vector2 b = P(end);
        Vector2 down = new(0, 34);
        DrawColoredPolygon([a, b, b + down, a + down], new Color("#777b77"));
        DrawLine(a, b, new Color("#686d6a"), 12f, true);
        DrawLine(a, b, new Color(.74f, .75f, .70f, .70f), 2f, true);
        for (int i = 1; i < 6; i++)
        {
            Vector2 p = a.Lerp(b, i / 6f);
            DrawLine(p, p + down, new Color(.25f, .27f, .27f, .48f), 2f, true);
        }
    }

    private void DrawBuilding(Vector2 center, Vector2 size, Color wall, Color roof, float height)
    {
        Vector2[] footprint = ProjectRect(center, size);
        Vector2 lift = new(0, -height);
        DrawColoredPolygon([footprint[1], footprint[2], footprint[2] + lift, footprint[1] + lift], wall.Darkened(.18f));
        DrawColoredPolygon([footprint[2], footprint[3], footprint[3] + lift, footprint[2] + lift], wall);
        Vector2[] top = footprint.Select(point => point + lift).ToArray();
        DrawColoredPolygon(top, roof);
        DrawPolyline(top.Append(top[0]).ToArray(), roof.Lightened(.16f), 1.4f, true);
        DrawLine(top[0], top[2], new Color(roof.Lightened(.08f), .42f), 1.1f, true);
    }

    private void DrawStorefront(Vector2 center, Vector2 size, Color wall, Color roof, float height)
    {
        DrawBuilding(center, size, wall, roof, height);
        Vector2[] footprint = ProjectRect(center, size);
        Vector2 fasciaLift = new(0, -height * .58f);
        Color awning = roof.Darkened(.22f);
        DrawLine(footprint[2] + fasciaLift, footprint[3] + fasciaLift, awning, 8f, true);
        DrawLine(footprint[1] + fasciaLift, footprint[2] + fasciaLift, awning.Darkened(.10f), 7f, true);
        DrawLine(footprint[2] + fasciaLift + new Vector2(0, 5), footprint[3] + fasciaLift + new Vector2(0, 5), new Color(.82f, .77f, .63f, .48f), 1.5f, true);
    }

    private void DrawCleanLabel(Vector2 at, string label, Color color, int size)
    {
        Vector2 p = P(at) + new Vector2(0, -27);
        Vector2 textSize = ThemeDB.FallbackFont.GetStringSize(label, HorizontalAlignment.Left, -1, size);
        DrawRect(new Rect2(p - new Vector2(3, textSize.Y), textSize + new Vector2(6, 4)), new Color(.06f, .07f, .06f, .72f), true);
        DrawString(ThemeDB.FallbackFont, p, label, HorizontalAlignment.Left, -1, size, color);
    }

    private void DrawCross(Vector2 center, float radius, Color color)
    {
        DrawRect(new Rect2(center - new Vector2(radius * .32f, radius), new Vector2(radius * .64f, radius * 2)), color);
        DrawRect(new Rect2(center - new Vector2(radius, radius * .32f), new Vector2(radius * 2, radius * .64f)), color);
    }

    private void DrawUrbanPropIfOwned(string role, Vector2 at, float scale, params string[] candidates)
    {
        if (Owns(at)) DrawUrbanProp(role, at, scale, candidates);
    }

    private void DrawUrbanProp(string role, Vector2 at, float scale, params string[] candidates) =>
        DrawAsset(role, at, scale, Colors.White, candidates);

    private void DrawAsset(string role, Vector2 at, float scale, Color tint, params string[] candidates)
    {
        if (!CountyAuthoredAssetCatalog.TryTexture(role, out Texture2D texture, candidates))
            return;
        Vector2 size = texture.GetSize() * scale;
        DrawTextureRect(texture, new Rect2(P(at) - new Vector2(size.X * .5f, size.Y), size), false, tint);
    }

    private void DrawAssetAtHeight(string role, Vector2 at, float targetHeight, Color tint, params string[] candidates)
    {
        if (!CountyAuthoredAssetCatalog.TryTexture(role, out Texture2D texture, candidates))
            return;
        float scale = targetHeight / Mathf.Max(1f, texture.GetHeight());
        Vector2 size = texture.GetSize() * scale;
        DrawTextureRect(texture, new Rect2(P(at) - new Vector2(size.X * .5f, size.Y), size), false, tint);
    }

    private void DrawFittedIfOwned(string role, Vector2 at, float targetHeight, params string[] candidates)
    {
        if (Owns(at))
            DrawAssetAtHeight(role, at, targetHeight, Colors.White, candidates);
    }

    private void DrawDiamondRect(Rect2 gridRect, Color color) =>
        DrawColoredPolygon(IsometricGrid.ProjectRectangle(gridRect.Position, gridRect.Size).Select(point => point - _canvasOrigin).ToArray(), color);

    private Vector2[] ProjectRect(Vector2 center, Vector2 size) =>
        IsometricGrid.ProjectRectangle(center - size * .5f, size).Select(point => point - _canvasOrigin).ToArray();

    private Vector2 P(Vector2 point) => IsometricGrid.GridToScreen(point) - _canvasOrigin;

    private bool Owns(Vector2 point) => _gridBounds.HasPoint(point);

    private static string[] HouseCandidates(int variant, bool abandoned)
    {
        int index = Mathf.PosMod(variant, 8);
        if (abandoned)
        {
            return index switch
            {
                0 => AbandonedHouse,
                1 => AbandonedHouse02,
                2 => AbandonedHouse03,
                3 => AbandonedHouse04,
                4 => AbandonedHouse05,
                5 => AbandonedHouse06,
                6 => AbandonedHouse07,
                _ => AbandonedHouse08
            };
        }

        return index switch
        {
            0 => House01,
            1 => House02,
            2 => House03,
            3 => House04,
            4 => House05,
            5 => House06,
            6 => House07,
            _ => House08
        };
    }

    private static Art A(string role, float x, float y, float scale, params string[] candidates) =>
        new(role, new Vector2(x, y), scale, candidates, Colors.White);
}
