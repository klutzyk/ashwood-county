using Godot;
using AshwoodCounty.World;

namespace AshwoodCounty.Resources;

[Tool]
public partial class StockpilePlaceholderVisual : Node2D
{
    public override void _Draw()
    {
        Texture2D texture = TextureRegistry.Get("res://assets/art/environment/props/stockpile_01.png");
        const float scale = 0.36f;
        Vector2 size = texture.GetSize() * scale;
        DrawTextureRect(texture, new Rect2(new Vector2(-size.X * 0.5f, -size.Y), size), false);
    }
}
