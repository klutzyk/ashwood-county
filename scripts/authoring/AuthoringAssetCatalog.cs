#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;

namespace AshwoodCounty.Authoring;

public sealed record AuthoringAssetEntry(string Path,string Name,string Category,string Subcategory,float DefaultScale,bool DefaultCollision);

public static class AuthoringAssetCatalog
{
    private static IReadOnlyList<AuthoringAssetEntry>? _cached;
    public static IReadOnlyList<AuthoringAssetEntry> GetAssets()=>_cached??=Scan();

    private static IReadOnlyList<AuthoringAssetEntry> Scan()
    {
        string root=ProjectSettings.GlobalizePath("res://assets/art");
        if(!Directory.Exists(root))return [];
        List<AuthoringAssetEntry> result=[];
        foreach(string file in Directory.EnumerateFiles(root,"*.png",SearchOption.AllDirectories))
        {
            string normalized=file.Replace('\\','/');
            if(normalized.Contains("/sheets/",StringComparison.OrdinalIgnoreCase)
               ||normalized.Contains("/ui/",StringComparison.OrdinalIgnoreCase)
               ||normalized.Contains("/characters/",StringComparison.OrdinalIgnoreCase)
               ||normalized.Contains("/zombies/",StringComparison.OrdinalIgnoreCase))continue;
            int marker=normalized.IndexOf("/assets/art/",StringComparison.OrdinalIgnoreCase);
            if(marker<0)continue;
            string path="res://"+normalized[(marker+1)..];
            string category=CategoryFor(path);
            string subcategory=SubcategoryFor(path);
            string name=Title(System.IO.Path.GetFileNameWithoutExtension(path));
            float scale=path.Contains("/interiors/")?1f:path.Contains("/buildings/")?.42f:path.Contains("/vehicles/")?.48f:.55f;
            bool collision=category=="Buildings"||path.Contains("/trees/")||path.Contains("/rocks/")||IsLargeInterior(path);
            result.Add(new AuthoringAssetEntry(path,name,category,subcategory,scale,collision));
        }
        return result.OrderBy(item=>item.Category).ThenBy(item=>item.Subcategory).ThenBy(item=>item.Name).ToArray();
    }

    private static string CategoryFor(string path)
    {
        if(path.Contains("/interiors/"))return "Interiors";
        if(path.Contains("/buildings/"))return "Buildings";
        if(path.Contains("/environment/")||path.Contains("/vegetation/"))return "Environment";
        if(path.Contains("/terrain/"))return "Terrain";
        if(path.Contains("/resources/"))return "Resources";
        return "Props";
    }

    private static string SubcategoryFor(string path)
    {
        string[] parts=path.Split('/');
        if(path.Contains("/interiors/")&&parts.Length>2)return Title(parts[^2]);
        if(path.Contains("/buildings/")&&parts.Length>2)return Title(parts[^2]);
        if(path.Contains("/environment/")&&parts.Length>2)return Title(parts[^2]);
        return parts.Length>2?Title(parts[^2]):"General";
    }
    private static string Title(string value)=>string.Join(' ',value.Split('_').Select(word=>word.Length==0?word:char.ToUpperInvariant(word[0])+word[1..]));
    private static bool IsLargeInterior(string path)=>new[]{"sofa","bed_","counter","fridge","refrigerator","stove","bathtub","shelf","bookcase"}.Any(path.Contains);
}

/// <summary>Keeps small editor thumbnails instead of rooting every full-resolution source texture.</summary>
public static class AuthoringThumbnailCache
{
    private static readonly Dictionary<string,Texture2D> Cache=[];
    public static Texture2D? Get(string resourcePath)
    {
        if(Cache.TryGetValue(resourcePath,out Texture2D? texture))return texture;
        Image image=new();if(image.Load(ProjectSettings.GlobalizePath(resourcePath))!=Error.Ok)return null;
        const int maximum=64;float scale=Mathf.Min(1f,maximum/(float)Mathf.Max(image.GetWidth(),image.GetHeight()));
        if(scale<1)image.Resize(Mathf.Max(1,Mathf.RoundToInt(image.GetWidth()*scale)),Mathf.Max(1,Mathf.RoundToInt(image.GetHeight()*scale)),Image.Interpolation.Lanczos);
        texture=ImageTexture.CreateFromImage(image);Cache[resourcePath]=texture;return texture;
    }
}
