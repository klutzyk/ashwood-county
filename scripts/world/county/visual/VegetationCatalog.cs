#nullable enable

using Godot;

namespace AshwoodCounty.World.County.Visual;

/// <summary>How large a plant is meant to read next to a survivor.</summary>
public enum VegetationTier
{
    /// <summary>Full-grown tree, roughly two and a half times a figure.</summary>
    Mature,

    /// <summary>An established but smaller tree, about head-and-shoulders above the canopy floor.</summary>
    Mid,

    /// <summary>Sapling or scrub tree, around a figure's height.</summary>
    Sapling
}

/// <summary>
/// Which tree art is allowed at which size.
///
/// The project carries two vegetation families at very different resolutions.
/// <c>assets/art/environment/vegetation</c> holds large sprites (oak 393x516,
/// pine 288x491, young tree 224x403), while <c>assets/art/vegetation</c> holds a
/// much smaller set (pine_02 94x175, pine_03 72x104, birch_01 64x116). Both were
/// previously drawn at the same 252px canopy height, so half the forest was
/// being enlarged between 1.4x and 2.4x past its native size. With canvas
/// filtering on and mipmaps off, that upscale is exactly the soft, smeared look
/// that stood out beside the sharp sprites next to it.
///
/// The fix is to let resolution decide the role: the large sprites carry the
/// mature canopy, the small ones fill the mid and sapling layers where their
/// native size is already the right size. That removes the blur and, as a
/// bonus, produces the layered canopy a real wood has.
///
/// <see cref="SpriteScaling"/> still enforces the cap, so nothing here can
/// silently regress by being asked for a size its art cannot hold.
/// </summary>
public static class VegetationCatalog
{
    private const string Large = "res://assets/art/environment/vegetation/";
    private const string Small = "res://assets/art/vegetation/";

    /// <summary>Canvas heights at zoom 1. A survivor sprite is about 100px.</summary>
    public static float HeightFor(VegetationTier tier) => tier switch
    {
        VegetationTier.Mature => 248f,
        VegetationTier.Mid => 166f,
        _ => 104f
    };

    // Mature canopy. Only the high-resolution family qualifies: every entry
    // here is being reduced, never enlarged, at 248px.
    private static readonly string[] ConiferMature = [Large + "pine_01.png"];
    private static readonly string[] BroadleafMature = [Large + "oak_01.png", Large + "young_tree_01.png"];
    private static readonly string[] DeadMature = [Large + "dead_tree_01.png"];

    // Mid storey. These sit at or just under their native height at 166px.
    private static readonly string[] ConiferMid = [Small + "pine_02.png"];
    private static readonly string[] BroadleafMid =
        [Small + "deciduous_02.png", Small + "deciduous_autumn_01.png"];
    private static readonly string[] DeadMid = [Small + "dead_tree_young_01.png", Small + "dead_tree_02.png"];

    // Saplings, drawn at or below native size.
    // young_pine_02 (73x80, 64x71 opaque) and young_deciduous_02 (72x83,
    // 61x71 opaque) are excluded. They are the only vegetation sprites whose
    // content is too small to fill even the 104px sapling slot without being
    // stretched, and a stretched sprite standing next to a reduced one is
    // exactly the inconsistency that reads as poor quality. The remaining
    // saplings all draw at or below their native size.
    private static readonly string[] ConiferSapling = [Small + "pine_03.png"];
    private static readonly string[] BroadleafSapling =
        [Small + "birch_01.png", Small + "birch_young_01.png"];

    /// <summary>Pick the tree art for a biome, tier and deterministic roll.</summary>
    public static string Select(CountyBiome biome, VegetationTier tier, float roll)
    {
        string[] options = biome switch
        {
            // A conifer ridge is a conifer ridge. Occasional standing deadwood
            // is the only thing breaking it up.
            CountyBiome.PineRidge => tier switch
            {
                VegetationTier.Mature => roll > .90f ? DeadMature : ConiferMature,
                VegetationTier.Mid => ConiferMid,
                _ => ConiferSapling
            },

            // Worked woodland: thinned conifer with a lot of standing dead.
            CountyBiome.Logging => tier switch
            {
                VegetationTier.Mature => roll > .55f ? DeadMature : ConiferMature,
                VegetationTier.Mid => roll > .45f ? DeadMid : ConiferMid,
                _ => ConiferSapling
            },

            // Mixed temperate wood, which is most of Ashwood's forest.
            CountyBiome.Mill or CountyBiome.Forest => tier switch
            {
                VegetationTier.Mature => roll > .74f ? ConiferMature : roll > .06f ? BroadleafMature : DeadMature,
                VegetationTier.Mid => roll > .70f ? ConiferMid : roll > .10f ? BroadleafMid : DeadMid,
                _ => roll > .68f ? ConiferSapling : BroadleafSapling
            },

            // Open country: hedgerow and field-corner broadleaf.
            _ => tier switch
            {
                VegetationTier.Mature => roll > .08f ? BroadleafMature : DeadMature,
                VegetationTier.Mid => roll > .12f ? BroadleafMid : DeadMid,
                _ => BroadleafSapling
            }
        };

        return options[Mathf.Min((int)(roll * options.Length), options.Length - 1)];
    }
}

/// <summary>
/// One rule for how big a sprite may be drawn: never much larger than the pixels
/// it actually has.
///
/// Godot's canvas textures here are imported with mipmaps off and the project's
/// default linear filter, so reducing a sprite is clean but enlarging one is
/// visibly soft. Mixed-resolution artwork drawn to a shared target size is
/// therefore guaranteed to produce blurry sprites sitting beside sharp ones,
/// which is precisely the inconsistency that reads as low quality.
///
/// Capping is deliberately preferred over compensating with a sharpen filter or
/// turning filtering off globally: the former fights the symptom, and the latter
/// would make every correctly downscaled sprite alias instead.
/// </summary>
public static class SpriteScaling
{
    /// <summary>
    /// The most a sprite may be enlarged. A little over 1 is imperceptible and
    /// avoids pointlessly rejecting art that is a few pixels short of its slot.
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
