#nullable enable

using System.Collections.Generic;
using Godot;

namespace AshwoodCounty.World.County.Visual;

/// <summary>
/// Maps county ground surfaces onto the project's authored isometric ground
/// diamonds. Every entry is real artwork from assets/art/terrain; nothing here
/// generates a substitute shape.
///
/// Variants are weighted rather than uniform so each surface has a dominant
/// look with occasional relief, which is what stops large areas reading as a
/// randomly shuffled tile grid.
/// </summary>
public static class GroundTilePalette
{
    private const string Terrain = "res://assets/art/terrain/";
    private const string Ground = "res://assets/art/terrain/ground/";

    private readonly record struct Variant(string Path, float Weight);

    private static readonly Dictionary<GroundSurface, Variant[]> Palette = new()
    {
        [GroundSurface.Meadow] =
        [
            new(Terrain + "grass_02.png", 3.2f),
            new(Ground + "mixed_grass_03.png", 2.4f),
            new(Terrain + "grass_01.png", 1.6f),
            new(Ground + "sparse_grass_01.png", 1.0f),
            new(Ground + "meadow_flowers_03.png", .5f)
        ],
        [GroundSurface.RichMeadow] =
        [
            new(Ground + "lush_grass_flowers_01.png", 2.6f),
            new(Ground + "wildflower_grass_02.png", 2.2f),
            new(Terrain + "grass_01.png", 2.0f),
            new(Ground + "meadow_flowers_03.png", 1.4f),
            new(Ground + "mushroom_meadow_01.png", .5f)
        ],
        [GroundSurface.Pasture] =
        [
            new(Ground + "sparse_grass_01.png", 3.0f),
            new(Ground + "mixed_grass_03.png", 2.2f),
            new(Terrain + "grass_02.png", 1.8f),
            new(Terrain + "grass_dirt_01.png", 1.1f),
            new(Ground + "dry_grass_rock_01.png", .7f)
        ],
        [GroundSurface.DryGrass] =
        [
            new(Ground + "dry_grass_rock_01.png", 3.0f),
            new(Ground + "sparse_ground_02.png", 2.2f),
            new(Ground + "sparse_grass_01.png", 1.8f),
            new(Ground + "mixed_grass_03.png", 1.0f)
        ],
        // leaf_litter_02 and leaves_01 are deliberately absent here: they are
        // partial scatters rather than full diamonds, so as a base layer they
        // punch holes through to the macro ground. They belong in LeafDetail.
        [GroundSurface.ForestFloor] =
        [
            new(Ground + "forest_floor_02.png", 3.4f),
            new(Ground + "mushroom_meadow_01.png", 1.5f),
            new(Ground + "mixed_grass_03.png", 1.5f),
            new(Ground + "sparse_ground_02.png", 1.1f),
            new(Ground + "muddy_ground_02.png", .6f)
        ],
        [GroundSurface.PineFloor] =
        [
            new(Ground + "forest_floor_02.png", 2.8f),
            new(Ground + "rocky_ground_03.png", 2.0f),
            new(Ground + "sparse_ground_02.png", 1.4f),
            new(Ground + "stone_outcrop_ground_01.png", 1.0f),
            new(Ground + "mixed_grass_03.png", .9f)
        ],
        [GroundSurface.Scrub] =
        [
            new(Ground + "dry_grass_rock_01.png", 2.6f),
            new(Ground + "rocky_dirt_01.png", 2.0f),
            new(Ground + "sparse_ground_02.png", 1.6f),
            new(Ground + "stone_outcrop_ground_01.png", .7f)
        ],
        [GroundSurface.Farmland] =
        [
            new(Ground + "ploughed_rows_02.png", 3.0f),
            new(Ground + "farm_rows_muddy_01.png", 1.4f),
            new(Ground + "mixed_grass_03.png", .6f)
        ],
        [GroundSurface.Ploughed] =
        [
            new(Ground + "farm_rows_muddy_01.png", 2.8f),
            new(Ground + "ploughed_rows_02.png", 2.4f),
            new(Ground + "bare_dirt_01.png", .8f)
        ],
        [GroundSurface.BareEarth] =
        [
            new(Ground + "bare_dirt_01.png", 2.8f),
            new(Ground + "sparse_dirt_01.png", 2.2f),
            new(Terrain + "dirt_01.png", 1.6f),
            new(Ground + "rocky_dirt_01.png", 1.0f)
        ],
        [GroundSurface.Gravel] =
        [
            new(Ground + "gravel_ground_01.png", 3.0f),
            new(Ground + "rocky_dirt_01.png", 1.6f),
            new(Ground + "sparse_ground_02.png", 1.2f),
            new(Ground + "sparse_dirt_01.png", .9f)
        ],
        [GroundSurface.Mud] =
        [
            new(Ground + "muddy_ground_02.png", 3.0f),
            new(Ground + "farm_rows_muddy_01.png", 1.2f),
            new(Ground + "bare_dirt_01.png", 1.2f),
            new(Ground + "sparse_dirt_01.png", .8f)
        ],
        [GroundSurface.Wetland] =
        [
            new(Ground + "muddy_ground_02.png", 2.2f),
            new(Ground + "forest_floor_02.png", 1.8f),
            new(Ground + "mixed_grass_03.png", 1.6f),
            new(Ground + "sparse_grass_01.png", 1.0f)
        ],
        [GroundSurface.TownGround] =
        [
            new(Ground + "gravel_ground_01.png", 2.8f),
            new(Ground + "sparse_ground_02.png", 1.8f),
            new(Ground + "rocky_dirt_01.png", 1.2f)
        ],
        [GroundSurface.Trodden] =
        [
            new(Ground + "sparse_dirt_01.png", 2.6f),
            new(Terrain + "grass_dirt_01.png", 2.2f),
            new(Ground + "grass_dirt_edge_01.png", 1.8f),
            new(Ground + "sparse_grass_01.png", 1.4f),
            new(Terrain + "dirt_path_01.png", .9f)
        ]
    };

    /// <summary>Detail overlays stamped sparsely on top of the base diamonds.</summary>
    public static readonly string[] LeafDetail =
    [
        Terrain + "leaves_01.png",
        Ground + "leaf_litter_02.png"
    ];

    public static readonly string[] GrassDetail =
    [
        Terrain + "grass_scatter_01.png",
        Ground + "grass_scatter_02.png"
    ];

    public static readonly string[] EarthDetail =
    [
        Terrain + "dirt_scatter_01.png",
        Ground + "dirt_scatter_02.png"
    ];

    /// <summary>Standing water belongs on wet ground, not on every worn verge.</summary>
    public static readonly string[] WetDetail =
    [
        Terrain + "mud_scatter_01.png",
        Ground + "dirt_scatter_02.png"
    ];

    public static readonly string[] StoneDetail =
    [
        Terrain + "gravel_scatter_01.png",
        Ground + "gravel_scatter_02.png"
    ];

    /// <summary>Every texture the palette can request, for warm-up and batching order.</summary>
    public static IEnumerable<string> AllTextures()
    {
        HashSet<string> seen = [];
        foreach (Variant[] variants in Palette.Values)
            foreach (Variant variant in variants)
                if (seen.Add(variant.Path))
                    yield return variant.Path;
    }

    /// <summary>Deterministically choose the diamond for a surface at a block.</summary>
    public static string? Select(GroundSurface surface, int blockX, int blockY)
    {
        if (surface == GroundSurface.None || !Palette.TryGetValue(surface, out Variant[]? variants))
            return null;

        float total = 0f;
        foreach (Variant variant in variants)
            total += variant.Weight;

        float roll = CountyTerrain.Hash01(blockX, blockY, 1013 + (int)surface * 37) * total;
        foreach (Variant variant in variants)
        {
            roll -= variant.Weight;
            if (roll <= 0f)
                return variant.Path;
        }
        return variants[^1].Path;
    }

    /// <summary>Which detail family, if any, belongs on top of this surface.</summary>
    public static string[]? DetailFor(GroundSurface surface) => surface switch
    {
        GroundSurface.ForestFloor or GroundSurface.PineFloor => LeafDetail,
        GroundSurface.Meadow or GroundSurface.RichMeadow or GroundSurface.Pasture => GrassDetail,
        GroundSurface.Mud or GroundSurface.Wetland => WetDetail,
        GroundSurface.BareEarth or GroundSurface.Trodden => EarthDetail,
        GroundSurface.Gravel or GroundSurface.TownGround or GroundSurface.Scrub => StoneDetail,
        GroundSurface.DryGrass => GrassDetail,
        _ => null
    };
}
