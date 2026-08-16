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
///
/// A third rule governs which art is eligible at all. A base diamond is drawn
/// about 246px wide, so only the roughly 280-340px source tiles can fill that
/// slot without being enlarged. The library also contains a set of ~141px
/// diamonds; mixing the two families meant a third of the ground was being
/// upscaled about 1.7x, which with mipmaps off produced soft patches sitting
/// directly beside sharp ones. Those small tiles are excluded from base terrain
/// and their colour is recovered through <see cref="SurfaceTint"/> instead,
/// which costs nothing and keeps every base draw sharp.
///
/// Restricting the base set also shrinks the number of distinct textures on
/// screen, so the per-texture batching in the ground layer has less to do.
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
            new(Terrain + "grass_01.png", 2.5f),
            new(Terrain + "grass_dirt_01.png", .8f)
        ],
        [GroundSurface.RichMeadow] =
        [
            new(Ground + "lush_grass_flowers_01.png", 6.0f),
            new(Terrain + "grass_01.png", 3.0f),
            new(Terrain + "grass_02.png", 1.2f)
        ],
        [GroundSurface.Pasture] =
        [
            new(Terrain + "grass_dirt_01.png", 6.0f),
            new(Ground + "sparse_grass_01.png", 3.0f),
            new(Terrain + "grass_02.png", 1.6f)
        ],
        [GroundSurface.DryGrass] =
        [
            new(Ground + "sparse_grass_01.png", 6.0f),
            new(Ground + "grass_dirt_edge_01.png", 2.2f),
            new(Ground + "rocky_dirt_01.png", 1.0f)
        ],
        // leaf_litter_02 and leaves_01 are deliberately absent here: they are
        // partial scatters rather than full diamonds, so as a base layer they
        // punch holes through to the macro ground. They belong in LeafDetail.
        //
        // forest_floor_02 was the dominant tile here and is only 141px, so the
        // whole woodland floor was the softest surface in the county. The brown
        // it provided is now a tint over a full-resolution earth diamond.
        [GroundSurface.ForestFloor] =
        [
            new(Terrain + "grass_dirt_01.png", 5.0f),
            new(Terrain + "dirt_01.png", 3.0f),
            new(Ground + "sparse_grass_01.png", 1.6f)
        ],
        [GroundSurface.PineFloor] =
        [
            new(Terrain + "dirt_01.png", 4.2f),
            new(Terrain + "grass_dirt_01.png", 3.0f),
            new(Ground + "rocky_dirt_01.png", 2.2f)
        ],
        [GroundSurface.Scrub] =
        [
            new(Ground + "rocky_dirt_01.png", 5.0f),
            new(Ground + "sparse_grass_01.png", 2.6f),
            new(Ground + "grass_dirt_edge_01.png", 1.4f)
        ],
        // A worked field commits to one treatment across its whole area. Mixing
        // plough patterns inside a single field is what made the agricultural
        // belt read as corduroy noise rather than as fields.
        [GroundSurface.Farmland] =
        [
            new(Ground + "farm_rows_muddy_01.png", 9.0f),
            new(Terrain + "grass_dirt_01.png", 1.0f)
        ],
        [GroundSurface.Ploughed] =
        [
            new(Terrain + "dirt_01.png", 6.0f),
            new(Ground + "bare_dirt_01.png", 3.0f),
            new(Ground + "farm_rows_muddy_01.png", 1.4f)
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
            new(Ground + "bare_dirt_01.png", 6.0f),
            new(Ground + "sparse_dirt_01.png", 2.0f),
            new(Terrain + "dirt_01.png", 1.2f)
        ],
        [GroundSurface.Wetland] =
        [
            new(Terrain + "grass_dirt_01.png", 4.5f),
            new(Ground + "sparse_grass_01.png", 2.6f),
            new(Ground + "bare_dirt_01.png", 1.4f)
        ],
        [GroundSurface.TownGround] =
        [
            new(Ground + "gravel_ground_01.png", 6.0f),
            new(Ground + "sparse_dirt_01.png", 1.8f),
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

    /// <summary>
    /// Per-surface colour multiplier.
    ///
    /// Restricting base terrain to the high-resolution diamonds costs some of
    /// the library's colour range, because several of the distinctly coloured
    /// tiles are the low-resolution ones. Recovering that range as a tint is
    /// both sharper and cheaper than drawing the soft art: a woodland floor is a
    /// brown-olive earth diamond, wet ground is the same diamond taken down and
    /// desaturated, and a standing crop is a green cast over plough rows.
    /// </summary>
    public static Color SurfaceTint(GroundSurface surface) => surface switch
    {
        // Woodland floors are in shade and mostly needle and leaf litter, so
        // they are taken down in value and pulled away from the orange that the
        // bare earth diamonds carry on their own.
        GroundSurface.ForestFloor => new Color(.74f, .74f, .60f),
        GroundSurface.PineFloor => new Color(.72f, .70f, .56f),
        GroundSurface.Wetland => new Color(.82f, .89f, .78f),
        GroundSurface.Mud => new Color(.70f, .68f, .60f),
        GroundSurface.Farmland => new Color(.92f, 1.00f, .82f),
        GroundSurface.DryGrass => new Color(1.00f, .97f, .82f),
        GroundSurface.Scrub => new Color(1.00f, .96f, .80f),
        GroundSurface.Ploughed => new Color(.94f, .90f, .82f),
        _ => Colors.White
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
