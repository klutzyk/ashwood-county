using System.Collections.Generic;
using Godot;

namespace AshwoodCounty.World;

/// <summary>
/// Owns the managed references for textures used by retained CanvasItem draw commands.
/// Godot stores texture RIDs in the draw list, but those RIDs do not root C# wrappers.
/// </summary>
public static class TextureRegistry
{
    private static readonly Dictionary<string, Texture2D> Textures = [];

    public static Texture2D Get(string path)
    {
        if (Textures.TryGetValue(path, out Texture2D texture) && GodotObject.IsInstanceValid(texture))
        {
            return texture;
        }

        texture = GD.Load<Texture2D>(path);
        if (texture is null)
        {
            GD.PushError($"Could not load required texture: {path}");
            return null!;
        }

        Textures[path] = texture;
        return texture;
    }
}
