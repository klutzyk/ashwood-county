#nullable enable

using Godot;

namespace AshwoodCounty.World.County.Visual;

/// <summary>
/// The hand-authored dressing for Ashwood's starting slice: the survivor camp
/// on the outskirts lane, the abandoned family home to its east, the cabin
/// terrace to its north, and the woodland edge that closes the pocket in.
///
/// This is the quality benchmark the rest of the county is meant to grow
/// towards, so it is deliberately explicit placement rather than another
/// procedural rule. Everything below is existing project artwork.
/// </summary>
public static class StartingAreaComposition
{
    public readonly record struct Piece(string Texture, Vector2 Position, float Scale, Color Tint, bool Flat = false);

    private const string Tree = "res://assets/art/trees/";
    private const string Growth = "res://assets/art/undergrowth/";
    private const string OldVeg = "res://assets/art/environment/vegetation/";
    private const string Rocks = "res://assets/art/environment/rocks/";
    private const string Props = "res://assets/art/environment/props/";
    private const string Resources = "res://assets/art/resources/";
    private const string Rural = "res://assets/art/props/rural/";
    private const string Roadside = "res://assets/art/props/roadside/";
    private const string Logging = "res://assets/art/props/logging/";
    private const string Farm = "res://assets/art/props/farm/";
    private const string Industrial = "res://assets/art/props/industrial/";
    private const string Landmarks = "res://assets/art/props/landmarks/";

    /// <summary>
    /// Bounds worth testing before iterating the table. The starting slice is
    /// small, so chunks outside it skip the whole pass.
    /// </summary>
    public static readonly Rect2 Bounds = new(178, 128, 58, 50);

    public static readonly Piece[] Pieces =
    [
        // ---------------------------------------------------------- camp core
        // A lived-in yard: hearth, seating, work surfaces and stored materials
        // arranged around the open ground the survivors actually stand on.
        Prop(Rural + "campfire_01.png", 201.4f, 156.2f, .30f),
        Prop(Logging + "fallen_log_02.png", 200.2f, 157.4f, .30f),
        Prop(Logging + "log_pile_03.png", 202.6f, 155.0f, .28f),
        Prop(Rural + "picnic_table_01.png", 204.6f, 154.4f, .24f),
        Prop(Resources + "wood_stack_01.png", 205.8f, 160.0f, .30f),
        Prop(Resources + "wood_stack_02.png", 204.6f, 160.8f, .28f),
        Prop(Industrial + "crate_01.png", 207.2f, 160.2f, .26f),
        Prop(Industrial + "barrels_01.png", 208.0f, 159.2f, .26f),
        Prop(Rural + "laundry_yard_01.png", 199.6f, 153.4f, .26f),
        Prop(Rural + "garden_plot_01.png", 198.0f, 159.6f, .24f),
        Prop(Logging + "stump_02.png", 202.0f, 158.6f, .22f),
        Prop(Roadside + "tire_pile_01.png", 207.6f, 154.2f, .22f),

        // A partial perimeter: the camp is fenced where it faces the road and
        // open where it faces the woodland it forages.
        Fence(206.6f, 161.2f), Fence(205.4f, 161.8f), Fence(204.2f, 162.4f),
        Fence(203.0f, 162.6f), Fence(201.8f, 162.4f),
        Prop(Farm + "wood_gate_02.png", 204.0f, 161.6f, .26f),
        Prop(Farm + "palisade_fence_01.png", 208.4f, 158.0f, .26f),
        Prop(Farm + "palisade_fence_01.png", 208.8f, 156.6f, .26f),

        // Camp-edge softening so the cleared yard does not end on a hard line.
        Plant(Growth + "grass_tuft_01.png", 199.0f, 161.6f, .20f),
        Plant(Growth + "flowers_yellow_01.png", 200.4f, 162.2f, .20f),
        Plant(Growth + "bush_green_01.png", 197.6f, 156.0f, .24f),
        Plant(Growth + "bush_green_02.png", 198.4f, 151.8f, .24f),
        Plant(Growth + "fern_01.png", 209.4f, 161.0f, .22f),
        Plant(OldVeg + "grass_clump_01.png", 206.2f, 152.8f, .20f),

        // ------------------------------------------------- family home garden
        // The house already reads as authored; the ground around it did not.
        Prop(Rural + "mailbox_tree_01.png", 215.0f, 158.8f, .26f),
        Prop(Rural + "garden_plot_01.png", 223.4f, 158.4f, .24f),
        Prop(Growth + "bush_green_01.png", 217.0f, 159.6f, .30f),
        Prop(Growth + "bush_green_01.png", 219.4f, 159.8f, .30f),
        Prop(Growth + "bush_green_01.png", 221.8f, 159.6f, .30f),
        Prop(Industrial + "abandoned_pickup_01.png", 214.4f, 152.6f, .26f),
        Prop(Roadside + "rusty_barrel_01.png", 224.6f, 152.0f, .20f),
        Plant(Growth + "bush_white_flower_01.png", 215.6f, 150.2f, .24f),
        Plant(Growth + "bush_berry_red_01.png", 224.4f, 156.4f, .24f),
        Plant(Tree + "birch_medium_01.png", 226.0f, 150.6f, .30f),
        Plant(Tree + "maple_large_01.png", 213.6f, 148.6f, .30f),

        // ----------------------------------------------------- cabin terrace
        Prop(Resources + "wood_stack_01.png", 197.4f, 141.6f, .28f),
        Prop(Logging + "timber_stack_03.png", 199.0f, 142.4f, .26f),
        Prop(Logging + "stump_02.png", 205.6f, 141.8f, .22f),
        Prop(Rural + "picnic_table_01.png", 210.2f, 139.6f, .22f),
        Prop(Rural + "campfire_01.png", 196.0f, 144.4f, .24f),
        Fence(211.6f, 141.0f), Fence(212.4f, 142.2f), Fence(212.8f, 143.4f),
        Plant(Tree + "spruce_small_01.png", 194.0f, 137.0f, .26f),
        Plant(Growth + "bush_autumn_01.png", 213.4f, 138.2f, .24f),

        // ------------------------------------------------------- lane frontage
        // Roadside furniture makes the lane read as a real county road rather
        // than a coloured strip laid over grass.
        Prop(Roadside + "utility_pole_01.png", 199.2f, 167.4f, .28f),
        Prop(Roadside + "utility_pole_01.png", 210.6f, 163.6f, .28f),
        Prop(Roadside + "utility_pole_01.png", 221.4f, 157.8f, .28f),
        Prop(Roadside + "speed_sign_55_01.png", 205.4f, 165.2f, .22f),
        Prop(Roadside + "stone_wall_01.png", 213.2f, 163.0f, .26f),
        Prop(Roadside + "stone_wall_01.png", 215.4f, 162.0f, .26f),
        Prop(Roadside + "mossy_boulder_02.png", 193.4f, 169.8f, .24f),
        Prop(Roadside + "boulder_cluster_03.png", 189.0f, 171.4f, .24f),
        Prop(Industrial + "abandoned_pickup_01.png", 196.6f, 167.0f, .26f),
        Prop(Roadside + "curve_sign_01.png", 186.4f, 171.8f, .22f),

        // ------------------------------------------------- woodland pocket
        // A deliberate tree line north-west of the camp: dense trunks, an
        // understory band, and deadfall the settlement has started working.
        Plant(Tree + "spruce_large_01.png", 191.0f, 152.0f, .30f),
        Plant(Tree + "spruce_large_01.png", 189.4f, 155.4f, .32f),
        Plant(Tree + "pine_medium_01.png", 192.6f, 157.6f, .26f),
        Plant(OldVeg + "oak_01.png", 187.6f, 150.4f, .26f),
        Plant(Tree + "maple_large_01.png", 190.2f, 148.2f, .30f),
        Plant(Tree + "birch_medium_01.png", 193.8f, 146.4f, .28f),
        Plant(Tree + "maple_autumn_small_01.png", 194.6f, 150.2f, .24f),
        Plant(Growth + "fern_large_01.png", 192.0f, 154.4f, .22f),
        Plant(Growth + "fern_01.png", 190.6f, 158.0f, .22f),
        Plant(Growth + "bush_green_01.png", 194.2f, 158.8f, .24f),
        Prop(Logging + "fallen_log_02.png", 193.0f, 160.8f, .30f),
        Prop(Logging + "rotted_log_01.png", 191.2f, 162.4f, .26f),
        Prop(Logging + "stump_02.png", 195.0f, 156.6f, .22f),
        Prop(Rocks + "mossy_rock_01.png", 188.8f, 160.0f, .24f),
        Prop(Rocks + "rock_cluster_01.png", 196.0f, 148.0f, .24f),

        // South-east woodland edge, closing the pocket without walling it off.
        Plant(Tree + "spruce_large_01.png", 212.4f, 168.6f, .30f),
        Plant(Tree + "maple_large_01.png", 216.6f, 167.0f, .30f),
        Plant(Tree + "spruce_small_01.png", 209.8f, 170.2f, .24f),
        Plant(Growth + "bush_green_02.png", 214.2f, 169.4f, .24f),
        Plant(OldVeg + "dead_tree_01.png", 219.6f, 166.0f, .26f),
        Prop(Roadside + "rock_formation_02.png", 207.0f, 171.0f, .24f),

        // North paddock: open, grazed, a few markers so it is not empty ground.
        Fence(206.0f, 146.6f), Fence(207.4f, 146.0f), Fence(208.8f, 145.6f),
        Prop(Farm + "hay_bale_round_01.png", 210.4f, 148.4f, .24f),
        Prop(Farm + "hay_bale_round_01.png", 211.4f, 149.2f, .22f),
        Plant(Growth + "flowers_daisy_01.png", 208.2f, 150.6f, .20f),
        Plant(Growth + "grass_tuft_01.png", 206.6f, 151.4f, .20f)
    ];

    private static Piece Prop(string texture, float x, float y, float scale) =>
        new(texture, new Vector2(x, y), scale, Colors.White);

    private static Piece Plant(string texture, float x, float y, float scale) =>
        new(texture, new Vector2(x, y), scale, Colors.White);

    private static Piece Fence(float x, float y) =>
        new(Props + "fence_01.png", new Vector2(x, y), .28f, new Color(.93f, .89f, .78f));
}
