#nullable enable

using Godot;

namespace AshwoodCounty.World.County.Visual;

/// <summary>Shared lightweight water shader variants. Geometry and pathing remain separate.</summary>
internal static class WaterMaterialLibrary
{
    private const string ShaderPath = "res://assets/shaders/water_flow.gdshader";
    private static Shader? _shader;
    private static ShaderMaterial? _lake;
    private static ShaderMaterial? _creek;
    private static ShaderMaterial? _river;
    private static ShaderMaterial? _pond;

    public static ShaderMaterial Lake => _lake ??= Create(
        new Color("193e43"), new Color("417076"), new Color("88a8a4"),
        new Vector2(.25f, .08f), .008f, 5.0f, .010f, .035f, .91f);

    public static ShaderMaterial Creek => _creek ??= Create(
        new Color("173f42"), new Color("3b7273"), new Color("9ab8ad"),
        new Vector2(.82f, .30f), .026f, 7.0f, .016f, .070f, .94f);

    public static ShaderMaterial River => _river ??= Create(
        new Color("17383e"), new Color("397079"), new Color("b1c6b7"),
        new Vector2(.75f, .38f), .045f, 8.5f, .022f, .105f, .95f);

    public static ShaderMaterial Pond => _pond ??= Create(
        new Color("203f3c"), new Color("486e65"), new Color("8ba69a"),
        new Vector2(.18f, .04f), .004f, 4.0f, .007f, .025f, .88f);

    private static ShaderMaterial Create(Color deep, Color shallow, Color highlight, Vector2 direction,
        float speed, float scale, float ripple, float highlightStrength, float opacity)
    {
        _shader ??= GD.Load<Shader>(ShaderPath);
        ShaderMaterial material = new() { Shader = _shader };
        material.SetShaderParameter("deep_color", deep);
        material.SetShaderParameter("shallow_color", shallow);
        material.SetShaderParameter("highlight_color", highlight);
        material.SetShaderParameter("flow_direction", direction);
        material.SetShaderParameter("flow_speed", speed);
        material.SetShaderParameter("ripple_scale", scale);
        material.SetShaderParameter("ripple_strength", ripple);
        material.SetShaderParameter("highlight_strength", highlightStrength);
        material.SetShaderParameter("opacity", opacity);
        return material;
    }
}
