#nullable enable

using Godot;

namespace AshwoodCounty.World.County.Visual;

/// <summary>How large a plant reads next to a survivor, who is about 100px tall.</summary>
public enum VegetationTier
{
    /// <summary>Full canopy tree, around three times a figure.</summary>
    Large,

    /// <summary>Established but smaller tree, about twice a figure.</summary>
    Medium,

    /// <summary>Sapling or scrub tree, a little above head height.</summary>
    Sapling
}

/// <summary>The band of ground cover a piece of undergrowth belongs to.</summary>
public enum UndergrowthLayer
{
    Shrub,
    Fern,
    Grass,
    Flower,
    Plant,
    FloorDetail,
    Deadfall,
    Stone
}

/// <summary>
/// The vegetation library and how it is distributed across the county.
///
/// Everything here comes from three sources, all of which hold detail at the
/// size they are drawn: the trees sheet, the undergrowth sheet, and the large
/// original isometric trees. Nothing from terrain_asset_sheet_02 is referenced.
/// That sheet's vegetation was authored at roughly a third the resolution of the
/// rest of the library, so drawing it at a believable tree size meant enlarging
/// it past its own pixels, and it was the visible soft artwork in the world.
/// Its rocks, roads, rails and props are unaffected and still in use elsewhere.
///
/// Distribution is per biome rather than one shared pool. A region reads as
/// itself because of what grows there and in what proportion, so Pine Ridge is
/// almost purely conifer with a fern and moss floor, the farm belt is hedgerow
/// broadleaf over grass and flowers, and the starting outskirts are open meadow
/// with occasional full-grown trees.
/// </summary>
public static class VegetationCatalog
{
    private const string Trees = "res://assets/art/trees/";
    private const string Under = "res://assets/art/undergrowth/";

    /// <summary>The original isometric sheet's trees, still among the best art here.</summary>
    private const string Legacy = "res://assets/art/environment/vegetation/";

    /// <summary>
    /// Canvas heights at zoom 1. Chosen so that every sprite in the matching
    /// pool is reduced rather than enlarged; the largest native tree is 516px
    /// and the smallest in the large pool is 403px, so 300 is comfortably under
    /// all of them.
    /// </summary>
    public static float HeightFor(VegetationTier tier) => tier switch
    {
        VegetationTier.Large => 300f,
        VegetationTier.Medium => 200f,
        _ => 130f
    };

    public static float HeightFor(UndergrowthLayer layer) => layer switch
    {
        UndergrowthLayer.Shrub => 86f,
        UndergrowthLayer.Fern => 80f,
        UndergrowthLayer.Grass => 72f,
        UndergrowthLayer.Flower => 56f,
        UndergrowthLayer.Plant => 50f,
        // Mushrooms and litter are ankle-level things. At the shrub band they
        // read as knee-high fungus, which is the one scale mistake that makes a
        // forest floor look like a prop shelf.
        UndergrowthLayer.FloorDetail => 32f,
        UndergrowthLayer.Deadfall => 44f,
        _ => 54f
    };

    // ------------------------------------------------------------------ trees

    private static readonly string[] ConiferLarge =
        [Trees + "spruce_large_01.png", Trees + "pine_large_01.png", Legacy + "pine_01.png"];
    private static readonly string[] ConiferMedium =
        [Trees + "spruce_medium_01.png", Trees + "spruce_medium_02.png", Trees + "pine_medium_01.png"];
    private static readonly string[] ConiferSapling =
        [Trees + "spruce_small_01.png"];

    private static readonly string[] BroadleafLarge =
        [Trees + "oak_large_01.png", Trees + "maple_large_01.png", Legacy + "oak_01.png", Legacy + "young_tree_01.png"];
    private static readonly string[] BroadleafMedium =
        [Trees + "maple_medium_01.png", Trees + "blossom_medium_01.png"];
    // Young broadleaf is the medium art drawn small. The sheet's only small
    // broadleaf sprites are autumn-coloured, and scattering those through every
    // meadow would put the county in two seasons at once.
    private static readonly string[] BroadleafSapling =
        [Trees + "birch_medium_01.png", Trees + "maple_medium_01.png"];

    private static readonly string[] BirchLarge = [Trees + "birch_large_01.png"];
    private static readonly string[] BirchMedium = [Trees + "birch_medium_01.png"];

    /// <summary>Autumn colour, used sparingly so the county stays one season.</summary>
    private static readonly string[] AutumnLarge = [Trees + "oak_autumn_large_01.png"];
    private static readonly string[] AutumnMedium = [Trees + "birch_autumn_medium_01.png"];

    private static readonly string[] DeadLarge =
        [Trees + "dead_oak_large_01.png", Legacy + "dead_tree_01.png"];
    private static readonly string[] DeadMedium =
        [Trees + "dead_tree_medium_01.png", Trees + "snag_large_01.png", Trees + "snag_medium_01.png"];
    private static readonly string[] DeadSapling = [Trees + "snag_medium_01.png"];

    /// <summary>Pick tree art for a biome, tier and deterministic roll.</summary>
    public static string SelectTree(CountyBiome biome, VegetationTier tier, float roll)
    {
        string[] options = biome switch
        {
            // A conifer ridge, with standing deadwood as the only relief.
            CountyBiome.PineRidge => tier switch
            {
                VegetationTier.Large => roll > .93f ? DeadLarge : ConiferLarge,
                VegetationTier.Medium => roll > .90f ? DeadMedium : ConiferMedium,
                _ => ConiferSapling
            },

            // Worked woodland: thinned conifer and a lot of snags.
            CountyBiome.Logging => tier switch
            {
                VegetationTier.Large => roll > .48f ? DeadLarge : ConiferLarge,
                VegetationTier.Medium => roll > .42f ? DeadMedium : ConiferMedium,
                _ => roll > .5f ? DeadSapling : ConiferSapling
            },

            // Mixed temperate wood: broadleaf led, conifer and birch through it.
            CountyBiome.Forest => tier switch
            {
                VegetationTier.Large => roll > .74f ? ConiferLarge
                    : roll > .62f ? BirchLarge : roll > .05f ? BroadleafLarge : DeadLarge,
                VegetationTier.Medium => roll > .70f ? ConiferMedium
                    : roll > .56f ? BirchMedium : roll > .08f ? BroadleafMedium : DeadMedium,
                _ => roll > .62f ? ConiferSapling : BroadleafSapling
            },

            // Mill Creek is the damp wood: more birch, more deadfall, less pine.
            CountyBiome.Mill => tier switch
            {
                VegetationTier.Large => roll > .82f ? ConiferLarge
                    : roll > .58f ? BirchLarge : roll > .10f ? BroadleafLarge : DeadLarge,
                VegetationTier.Medium => roll > .78f ? ConiferMedium
                    : roll > .50f ? BirchMedium : roll > .14f ? BroadleafMedium : DeadMedium,
                _ => roll > .55f ? BroadleafSapling : ConiferSapling
            },

            // Dry eastern scrub: stunted and sparse, never full canopy.
            CountyBiome.Scrub => tier switch
            {
                VegetationTier.Large => roll > .6f ? DeadLarge : BroadleafLarge,
                VegetationTier.Medium => roll > .55f ? DeadMedium : AutumnMedium,
                _ => BroadleafSapling
            },

            // Open country: hedgerow and field-corner broadleaf, with the
            // occasional autumn tree as a landmark.
            _ => tier switch
            {
                VegetationTier.Large => roll > .90f ? AutumnLarge
                    : roll > .82f ? BirchLarge : roll > .06f ? BroadleafLarge : DeadLarge,
                VegetationTier.Medium => roll > .88f ? AutumnMedium
                    : roll > .74f ? BirchMedium : roll > .10f ? BroadleafMedium : DeadMedium,
                _ => BroadleafSapling
            }
        };

        return options[Mathf.Min((int)(roll * options.Length), options.Length - 1)];
    }

    // ------------------------------------------------------------ undergrowth

    private static readonly string[] ShrubGreen =
        [Under + "bush_green_01.png", Under + "bush_green_02.png"];
    private static readonly string[] ShrubBerry =
        [Under + "bush_berry_red_01.png", Under + "bush_berry_red_02.png",
         Under + "bush_berry_blue_01.png", Under + "bush_berry_tall_01.png"];
    private static readonly string[] ShrubFlowering =
        [Under + "bush_white_flower_01.png", Under + "bush_white_flower_02.png",
         Under + "bush_white_flower_tall_01.png", Under + "bush_blue_flower_01.png"];
    private static readonly string[] ShrubDry =
        [Under + "shrub_bare_01.png", Under + "shrub_bare_red_01.png",
         Under + "bush_autumn_01.png", Under + "bush_autumn_02.png"];

    private static readonly string[] Ferns =
        [Under + "fern_01.png", Under + "fern_02.png",
         Under + "fern_large_01.png", Under + "fern_large_02.png"];

    private static readonly string[] Grasses =
        [Under + "grass_tuft_01.png", Under + "grass_seedheads_01.png"];
    private static readonly string[] MarshGrass =
        [Under + "grass_pampas_01.png", Under + "grass_pampas_02.png",
         Under + "grass_seedheads_01.png", Under + "plant_broadleaf_01.png"];

    private static readonly string[] Flowers =
        [Under + "flowers_yellow_01.png", Under + "flowers_yellow_02.png", Under + "flowers_yellow_03.png",
         Under + "flowers_daisy_01.png", Under + "flowers_daisy_02.png", Under + "flowers_white_01.png",
         Under + "flowers_blue_01.png", Under + "flowers_pink_01.png", Under + "flowers_mixed_01.png",
         Under + "flowers_lavender_01.png", Under + "flowers_lavender_02.png", Under + "flowers_orange_01.png"];

    private static readonly string[] Plants =
        [Under + "plant_leafy_01.png", Under + "plant_leafy_02.png", Under + "plant_leafy_03.png",
         Under + "plant_leafy_04.png", Under + "plant_leafy_05.png", Under + "plant_leafy_06.png",
         Under + "plant_small_01.png", Under + "plant_small_02.png", Under + "plant_small_03.png",
         Under + "plant_small_04.png", Under + "plant_creeper_01.png"];

    /// <summary>Autumn colour is kept to woodland floors so the county reads as one season.</summary>
    private static readonly string[] AutumnGroundCover =
        [Under + "plant_autumn_01.png", Under + "leaf_litter_01.png", Under + "leaf_litter_02.png"];

    private static readonly string[] WoodlandFloor =
        [Under + "mushroom_cluster_01.png", Under + "mushroom_cluster_02.png", Under + "mushroom_cluster_03.png",
         Under + "mushroom_red_01.png", Under + "mushroom_red_02.png", Under + "mushroom_brown_01.png",
         Under + "mushroom_brown_02.png", Under + "leaf_litter_01.png", Under + "leaf_litter_02.png",
         Under + "leaf_litter_03.png", Under + "pinecones_01.png"];

    private static readonly string[] WoodlandFloorRich =
        [.. WoodlandFloor, .. AutumnGroundCover];

    private static readonly string[] Deadfall =
        [Under + "branch_01.png", Under + "branch_02.png", Under + "branch_03.png",
         Under + "branch_04.png", Under + "branch_05.png",
         Under + "branch_bundle_01.png", Under + "branch_bundle_02.png", Under + "branch_pile_01.png"];

    private static readonly string[] Stones =
        [Under + "rock_mossy_01.png", Under + "rock_mossy_02.png", Under + "rock_mossy_03.png",
         Under + "rock_mossy_04.png", Under + "rock_mossy_05.png", Under + "rock_mossy_06.png",
         Under + "rock_small_01.png"];

    /// <summary>Large woodland pieces, placed rarely as focal detail.</summary>
    public static readonly string[] WoodlandFeatures =
        [Under + "stump_mossy_01.png", Under + "log_mossy_01.png", Under + "log_mushroom_01.png"];

    /// <summary>
    /// Which cover layer a biome grows, and how often. The weights are what make
    /// a meadow read as a meadow: open country is flowers and grass with the
    /// occasional shrub, while a closed wood is fern, moss and litter.
    /// </summary>
    private static UndergrowthLayer LayerFor(CountyBiome biome, float roll) => biome switch
    {
        CountyBiome.PineRidge => roll switch
        {
            < .34f => UndergrowthLayer.Fern,
            < .54f => UndergrowthLayer.Stone,
            < .72f => UndergrowthLayer.FloorDetail,
            < .86f => UndergrowthLayer.Deadfall,
            _ => UndergrowthLayer.Shrub
        },
        CountyBiome.Forest or CountyBiome.Mill => roll switch
        {
            < .28f => UndergrowthLayer.Fern,
            < .46f => UndergrowthLayer.FloorDetail,
            < .60f => UndergrowthLayer.Shrub,
            < .72f => UndergrowthLayer.Plant,
            < .84f => UndergrowthLayer.Deadfall,
            _ => UndergrowthLayer.Stone
        },
        CountyBiome.Logging => roll switch
        {
            < .34f => UndergrowthLayer.Deadfall,
            < .54f => UndergrowthLayer.Plant,
            < .70f => UndergrowthLayer.Fern,
            < .86f => UndergrowthLayer.Stone,
            _ => UndergrowthLayer.FloorDetail
        },
        CountyBiome.Scrub => roll switch
        {
            < .42f => UndergrowthLayer.Shrub,
            < .70f => UndergrowthLayer.Grass,
            < .88f => UndergrowthLayer.Stone,
            _ => UndergrowthLayer.Plant
        },
        CountyBiome.Farm or CountyBiome.SouthFarm => roll switch
        {
            < .40f => UndergrowthLayer.Grass,
            < .70f => UndergrowthLayer.Flower,
            < .88f => UndergrowthLayer.Shrub,
            _ => UndergrowthLayer.Plant
        },
        // Meadow, outskirts, town fringes: open and flowering.
        _ => roll switch
        {
            < .38f => UndergrowthLayer.Flower,
            < .64f => UndergrowthLayer.Grass,
            < .82f => UndergrowthLayer.Plant,
            _ => UndergrowthLayer.Shrub
        }
    };

    private static string[] PoolFor(CountyBiome biome, UndergrowthLayer layer) => layer switch
    {
        UndergrowthLayer.Fern => Ferns,
        UndergrowthLayer.Grass => Grasses,
        UndergrowthLayer.Flower => Flowers,
        UndergrowthLayer.Plant => Plants,
        UndergrowthLayer.FloorDetail => biome is CountyBiome.Forest or CountyBiome.Mill
            ? WoodlandFloorRich : WoodlandFloor,
        UndergrowthLayer.Deadfall => Deadfall,
        UndergrowthLayer.Stone => Stones,
        _ => biome switch
        {
            CountyBiome.Scrub => ShrubDry,
            CountyBiome.PineRidge or CountyBiome.Logging => ShrubGreen,
            CountyBiome.Forest or CountyBiome.Mill => ShrubBerry,
            _ => ShrubFlowering
        }
    };

    /// <summary>Pick undergrowth art and the size band it should be drawn at.</summary>
    public static (string Texture, UndergrowthLayer Layer) SelectUndergrowth(
        CountyBiome biome, float layerRoll, float pickRoll)
    {
        UndergrowthLayer layer = LayerFor(biome, layerRoll);
        string[] pool = PoolFor(biome, layer);
        return (pool[Mathf.Min((int)(pickRoll * pool.Length), pool.Length - 1)], layer);
    }

    /// <summary>Bankside cover, used where the ground is wet.</summary>
    public static string SelectWaterside(float roll) =>
        MarshGrass[Mathf.Min((int)(roll * MarshGrass.Length), MarshGrass.Length - 1)];
}

/// <summary>
/// One rule for how big a sprite may be drawn: never much larger than the pixels
/// it actually has.
///
/// Canvas textures here import with mipmaps off and the project's default linear
/// filter, so reducing a sprite is clean but enlarging one is visibly soft.
/// Mixed-resolution artwork drawn to a shared target size therefore guarantees
/// blurry sprites beside sharp ones, which is what reads as low quality.
///
/// Capping is deliberately preferred over a sharpen filter or over disabling
/// filtering globally: the first fights the symptom, and the second would make
/// every correctly reduced sprite alias instead.
/// </summary>
public static class SpriteScaling
{
    /// <summary>
    /// The most a sprite may be enlarged. Slightly above 1 is imperceptible and
    /// avoids rejecting art that is a few pixels short of its slot.
    /// </summary>
    public const float MaxUpscale = 1.12f;

    /// <summary>Scale factor to draw <paramref name="path"/> at a canvas height, capped.</summary>
    public static float ForHeight(string path, float canvasHeight)
    {
        Texture2D texture = TextureRegistry.Get(path);
        float source = texture is null ? 0f : texture.GetSize().Y;
        if (source <= 1f)
            return .3f;
        return Mathf.Min(canvasHeight / source, MaxUpscale);
    }

    /// <summary>Scale factor to draw <paramref name="path"/> at a canvas width, capped.</summary>
    public static float ForWidth(string path, float canvasWidth)
    {
        Texture2D texture = TextureRegistry.Get(path);
        float source = texture is null ? 0f : texture.GetSize().X;
        if (source <= 1f)
            return .3f;
        return Mathf.Min(canvasWidth / source, MaxUpscale);
    }
}
