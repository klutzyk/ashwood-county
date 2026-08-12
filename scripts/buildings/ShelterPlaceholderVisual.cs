using Godot;
using AshwoodCounty.World;

namespace AshwoodCounty.Buildings;

[Tool]
public partial class ShelterPlaceholderVisual : Node2D
{
    private const string TexturePath = "res://assets/art/buildings/survival_cabin.png";
    private const float ArtScale = 0.30f;
    private static readonly Vector2 GroundAnchor = new(565, 1110);

    public override void _Draw()
    {
        Texture2D texture = TextureRegistry.Get(TexturePath);
        Vector2 size = texture.GetSize() * ArtScale;
        DrawTextureRect(texture, new Rect2(-GroundAnchor * ArtScale, size), false);
    }
}
