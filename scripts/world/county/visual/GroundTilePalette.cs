#nullable enable

using System.Collections.Generic;
using Godot;

namespace AshwoodCounty.World.County.Visual;

/// <summary>
/// Maps county ground surfaces onto the project's authored isometric ground
/// diamonds. Every entry is real artwork from assets/art/terrain; nothing here
/// generates a substitute shape.
///
/// Two rules give the ground its hierarchy.
///
/// Weights are lopsided: each surface has one clearly dominant variant carrying
/// most of its area, with the rest as occasional relief. Even weights make every
/// patch equally interesting, which reads as noise.
///
/// Selection is spatially correlated: the variant comes from a smooth noise
/// field rather than a per-tile hash, so the same diamond repeats across a run
/// of neighbours and the ground resolves into masses. An independent hash per
/// tile is what produced the visible salt-and-pepper lattice.
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
            new(Terrain + "grass_02.png", 7.0f),
            new(Ground + "mixed_grass_03.png", 2.6f),
            new(Terrain + "grass_01.png", 1.2f),
            new(Ground + "meadow_flowers_03.png", .4f)
        ],
        [GroundSurface.RichMeadow] =
        [
            new(Ground + "lush_grass_flowers_01.png", 5.5f),
            new(Terrain + "grass_01.png", 3.0f),
            new(Ground + "wildflower_grass_02.png", 1.6f),
            new(Ground + "meadow_flowers_03.png", .6f)
        ],
        // Dominant variants are chosen to sit close in value to one another.
        // sparse_grass_01 is a dry tan; leading with it turned the outskirts
        // arid, so the greener mixed grass carries the mass and the dry tufts
        // appear as relief.
        [GroundSurface.Pasture] =
        [
            new(Terrain + "grass_dirt_01.png", 6.0f),
            new(Ground + "sparse_grass_01.png", 3.0f),
            new(Terrain + "grass_02.png", 1.8f),
            new(Ground + "mixed_grass_03.png", .8f)
        ],
        [GroundSurface.DryGrass] =
        [
            new(Ground + "sparse_grass_01.png", 5.5f),
            new(Ground + "dry_grass_rock_01.png", 2.6f),
            new(Ground + "sparse_ground_02.png", 1.6f)
        ],
        // leaf_litter_02 and leaves_01 are deliberately absent here: they are
        // partial scatters rather than full diamonds, so as a base layer they
        // punch holes through to the macro ground. They belong in LeafDetail.
        [GroundSurface.ForestFloor] =
        [
            new(Ground + "forest_floor_02.png", 7.0f),
            new(Ground + "mushroom_meadow_01.png", 1.6f),
            new(Ground + "sparse_ground_02.png", 1.2f),
            new(Ground + "mixed_grass_03.png", 1.0f)
        ],
        [GroundSurface.PineFloor] =
        [
            new(Ground + "forest_floor_02.png", 6.5f),
            new(Ground + "sparse_ground_02.png", 2.0f),
            new(Ground + "rocky_ground_03.png", 1.6f),
            new(Ground + "stone_outcrop_ground_01.png", .5f)
        ],
        [GroundSurface.Scrub] =
        [
            new(Ground + "dry_grass_rock_01.png", 5.5f),
            new(Ground + "rocky_dirt_01.png", 2.0f),
            new(Ground + "sparse_ground_02.png", 1.6f)
        ],
        // A worked field commits to one treatment across its whole area. Mixing
        // plough patterns inside a single field is what made the agricultural
        // belt read as corduroy noise rather than as fields.
        [GroundSurface.Farmland] =
        [
            new(Ground + "ploughed_rows_02.png", 9.0f),
            new(Ground + "farm_rows_muddy_01.png", 1.0f)
        ],
        [GroundSurface.Ploughed] =
        [
            new(Ground + "farm_rows_muddy_01.png", 8.0f),
            new(Ground + "bare_dirt_01.png", 1.2f)
        ],
        [GroundSurface.BareEarth] =
        [
            new(Ground + "bare_dirt_01.png", 6.0f),
            new(Ground + "sparse_dirt_01.png", 2.6f),
            new(Terrain + "dirt_01.png", 1.2f)
        ],
        [GroundSurface.Gravel] =
        [
            new(Ground + "gravel_ground_01.png", 6.5f),
            new(Ground + "sparse_dirt_01.png", 1.6f),
            new(Ground + "rocky_dirt_01.png", 1.2f)
        ],
        [GroundSurface.Mud] =
        [
            new(Ground + "muddy_ground_02.png", 6.5f),
            new(Ground + "bare_dirt_01.png", 1.6f),
            new(Ground + "sparse_dirt_01.png", 1.0f)
        ],
        [GroundSurface.Wetland] =
        [
            new(Ground + "muddy_ground_02.png", 4.0f),
            new(Ground + "mixed_grass_03.png", 2.6f),
            new(Ground + "sparse_grass_01.png", 1.6f)
        ],
        [GroundSurface.TownGround] =
        [
            new(Ground + "gravel_ground_01.png", 6.0f),
            new(Ground + "sparse_ground_02.png", 1.8f),
            new(Ground + "rocky_dirt_01.png", 1.0f)
        ],
        [GroundSurface.Trodden] =
        [
            new(Terrain + "grass_dirt_01.png", 4.5f),
            new(Ground + "sparse_dirt_01.png", 3.0f),
            new(Ground + "grass_dirt_edge_01.png", 1.8f),
            new(Terrain + "dirt_path_01.png", .5f)
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

    /// <summary>
    /// Wavelength of the variant field, in county cells. Around twelve cells
    /// gives patches roughly six across: big enough to read as a mass at
    /// gameplay zoom, small enough to keep a surface from looking uniform.
    /// </summary>
    private const float VariantFrequency = 1f / 12f;

    /// <summary>
    /// Deterministically choose the diamond for a surface at a block, correlated
    /// with its neighbours so runs of the same variant form patches.
    /// </summary>
    public static string? Select(GroundSurface surface, Vector2 point, int blockX, int blockY)
    {
        if (surface == GroundSurface.None || !Palette.TryGetValue(surface, out Variant[]? variants))
            return null;
        if (variants.Length == 1)
            return variants[0].Path;

        float total = 0f;
        foreach (Variant variant in variants)
            total += variant.Weight;

        // The smooth field picks the patch; a small hash term ragged-edges it so
        // patch boundaries do not read as clean noise contours.
        float field = CountyTerrain.Fbm(point, VariantFrequency, 1013 + (int)surface * 37);
        float jitter = (CountyTerrain.Hash01(blockX, blockY, 1451 + (int)surface) - .5f) * .10f;
        float roll = Mathf.Clamp(field + jitter, 0f, .9999f) * total;

        foreach (Variant variant in variants)
        {
            roll -= variant.Weight;
            if (roll <= 0f)
                return variant.Path;
        }
        return variants[^1].Path;
    }

    /// <summary>
    /// Which detail family, if any, belongs on top of this surface.
    ///
    /// Cultivated and gravelled ground is deliberately left bare: a worked field
    /// or a settlement yard reads as worked precisely because it is calm.
    /// </summary>
    public static string[]? DetailFor(GroundSurface surface) => surface switch
    {
        GroundSurface.Farmland or GroundSurface.Ploughed => null,
        GroundSurface.Gravel or GroundSurface.TownGround => null,
        GroundSurface.ForestFloor or GroundSurface.PineFloor => LeafDetail,
        GroundSurface.Meadow or GroundSurface.RichMeadow or GroundSurface.Pasture => GrassDetail,
        GroundSurface.Mud or GroundSurface.Wetland => WetDetail,
        GroundSurface.BareEarth or GroundSurface.Trodden => EarthDetail,
        GroundSurface.Scrub => StoneDetail,
        GroundSurface.DryGrass => GrassDetail,
        _ => null
    };
}
