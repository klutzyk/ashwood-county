#nullable enable

using System.Collections.Generic;
using Godot;

namespace AshwoodCounty.World.County.Visual.Authoring;

/// <summary>
/// Resolves newly extracted art by semantic role and retains a safe project
/// fallback. This keeps placements stable while individual artwork remains
/// replaceable. TextureRegistry owns every texture wrapper used by draw lists.
/// </summary>
internal static class CountyAuthoredAssetCatalog
{
    private static readonly Dictionary<string, string> ResolvedPaths = [];

    public static Texture2D Texture(string role, params string[] candidates) =>
        TextureRegistry.Get(Path(role, candidates));

    public static string Path(string role, params string[] candidates)
    {
        if (ResolvedPaths.TryGetValue(role, out string? resolved))
            return resolved;

        foreach (string candidate in candidates)
        {
            if (ResourceLoader.Exists(candidate))
            {
                ResolvedPaths[role] = candidate;
                return candidate;
            }
        }

        const string fallback = "res://assets/art/buildings/survival_cabin.png";
        ResolvedPaths[role] = fallback;
        return fallback;
    }

    public static bool TryTexture(string role, out Texture2D texture, params string[] candidates)
    {
        if (ResolvedPaths.TryGetValue(role, out string? resolved))
        {
            if (string.IsNullOrEmpty(resolved))
            {
                texture = null!;
                return false;
            }

            texture = TextureRegistry.Get(resolved);
            return true;
        }

        foreach (string candidate in candidates)
        {
            if (!ResourceLoader.Exists(candidate))
                continue;
            ResolvedPaths[role] = candidate;
            texture = TextureRegistry.Get(candidate);
            return true;
        }

        ResolvedPaths[role] = string.Empty;
        texture = null!;
        return false;
    }
}
