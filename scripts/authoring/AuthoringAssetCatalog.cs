#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AshwoodCounty.World;
using Godot;

namespace AshwoodCounty.Authoring;

public sealed record AuthoringAssetEntry(string Path,string Name,string Category,string Subcategory,float DefaultScale,bool DefaultCollision,string SourceSheet,string AssetKind,Vector2 SuggestedAnchor,string SearchTags);

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
               ||System.IO.Path.GetFileNameWithoutExtension(normalized).Contains("sheet",StringComparison.OrdinalIgnoreCase)
               ||normalized.EndsWith("/county_ground.png",StringComparison.OrdinalIgnoreCase)
               ||normalized.EndsWith("/ashwood_outskirts_ground.png",StringComparison.OrdinalIgnoreCase)
               ||normalized.Contains("/ui/",StringComparison.OrdinalIgnoreCase)
               ||normalized.Contains("/characters/",StringComparison.OrdinalIgnoreCase)
               ||normalized.Contains("/zombies/",StringComparison.OrdinalIgnoreCase))continue;
            int marker=normalized.IndexOf("/assets/art/",StringComparison.OrdinalIgnoreCase);
            if(marker<0)continue;
            string path="res://"+normalized[(marker+1)..];
            if(IsRejected(path))continue;
            string category=CategoryFor(path);
            string subcategory=SubcategoryFor(path);
            string name=DisplayName(path);
            float scale=path.Contains("/interiors/")?1f:path.Contains("/buildings/")?.42f:path.Contains("/vehicles/")?.48f:.55f;
            bool collision=category=="Buildings"||path.Contains("/trees/")||path.Contains("/rocks/")||IsLargeInterior(path);
            string kind=IsComposite(path)?"Composite":"Standalone";Vector2 anchor=path.Contains("/terrain/")?new Vector2(.5f,.5f):new Vector2(.5f,1f);
            result.Add(new AuthoringAssetEntry(path,name,category,subcategory,scale,collision,SourceFor(path),kind,anchor,$"{name} {category} {subcategory} {kind}"));
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
    private static string DisplayName(string path)
    {
        if(path.EndsWith("/abandoned_pickup_01.png")||path.EndsWith("/scrap_pile_01.png"))return "Abandoned Pickup Scrap Scene";
        if(path.EndsWith("/dock_rowboat_01.png")||path.EndsWith("/dock_01.png")||path.EndsWith("/rowboat_01.png"))return "Dock and Rowboat Scene";
        if(path.EndsWith("/rusty_barrel_01.png"))return "Road Barrier and Barrel Scene";
        if(path.EndsWith("/cones_barrier_01.png"))return "Traffic Cones and Barrier Scene";
        if(path.EndsWith("/corrugated_shed_01.png"))return "Corrugated Shed Work Area";
        if(path.EndsWith("/ruined_shed_01.png"))return "Ruined Shed Work Area";
        return Title(System.IO.Path.GetFileNameWithoutExtension(path));
    }
    private static bool IsComposite(string path)=>new[]{"abandoned_pickup_01","scrap_pile_01","corrugated_shed_01","ruined_shed_01","dock_rowboat_01","dock_01.png","rowboat_01","garden_plot_01","laundry_yard_01","mailbox_tree_01","ridge_viewpoint_01","campfire_01","rusty_barrel_01","cones_barrier_01","stockpile_01"}.Any(path.Contains);
    private static string SourceFor(string path)
    {
        if(path.Contains("/interiors/residential/"))return "residential_interior_kit_01.png";
        if(path.Contains("/buildings/residential/"))return path.Contains("abandoned")?"houses (abandoned).png":"houses.png";
        if(path.Contains("/buildings/rural/")||path.Contains("/props/rural/"))return "rural_structures.png";
        if(path.Contains("/props/urban/"))return "urban props.png";
        if(path.Contains("/props/landmarks/"))return "landmarks.png";
        if(path.Contains("/props/vehicles/"))return "vehicles (abandoned).png";
        if(path.Contains("/terrain/")||path.Contains("/props/farm/")||path.Contains("/props/industrial/")||path.Contains("/props/logging/")||path.Contains("/props/roadside/")||path.Contains("/vegetation/"))return path.Contains("_02")||path.Contains("_03")||new[]{"crate_01","barrels_01","watchtower_01","scrap_pile_01","corrugated_shed_01","ruined_shed_01","abandoned_pickup_01","concrete_pipes_01","road_barrier_01"}.Any(path.Contains)?"terrain_asset_sheet_02.png":"terrain_asset_sheet.png";
        return "isometric_asset_sheet.png / project source";
    }
    private static bool IsLargeInterior(string path)=>new[]{"sofa","bed_","counter","fridge","refrigerator","stove","bathtub","shelf","bookcase"}.Any(path.Contains);
    private static bool IsRejected(string path)=>new[]{"/props/farm/barbed_fence_01.png","/props/roadside/street_light_01.png","/vegetation/flowers_blue_01.png","/vegetation/shrub_yellow_01.png"}.Any(path.EndsWith);
}

/// <summary>Keeps small editor thumbnails instead of rooting every full-resolution source texture.</summary>
public static class AuthoringThumbnailCache
{
    private sealed record CachedThumbnail(Texture2D Texture,DateTime Modified);
    private static readonly Dictionary<string,CachedThumbnail> Cache=[];
    public static Texture2D? Get(string resourcePath)
    {
        string file=ProjectSettings.GlobalizePath(resourcePath);DateTime modified=File.GetLastWriteTimeUtc(file);
        if(Cache.TryGetValue(resourcePath,out CachedThumbnail? cached)&&cached.Modified==modified)return cached.Texture;
        Image image=new();if(image.Load(file)!=Error.Ok)return null;
        const int maximum=64;float scale=Mathf.Min(1f,maximum/(float)Mathf.Max(image.GetWidth(),image.GetHeight()));
        if(scale<1)image.Resize(Mathf.Max(1,Mathf.RoundToInt(image.GetWidth()*scale)),Mathf.Max(1,Mathf.RoundToInt(image.GetHeight()*scale)),Image.Interpolation.Lanczos);
        Texture2D texture=ImageTexture.CreateFromImage(image);Cache[resourcePath]=new(texture,modified);return texture;
    }
    public static void Clear()=>Cache.Clear();
}

public partial class AssetInspectionPreview:Control
{
    private Texture2D? _texture;
    public void ShowAsset(string path){_texture=ResourceLoader.Exists(path)?TextureRegistry.Get(path):null;QueueRedraw();}
    public override void _Draw()
    {
        const float cell=14;for(float y=0;y<Size.Y;y+=cell)for(float x=0;x<Size.X;x+=cell)DrawRect(new Rect2(x,y,cell,cell),(((int)(x/cell)+(int)(y/cell))&1)==0?new Color("5a5d57"):new Color("3e413d"));
        if(_texture is null)return;Vector2 available=Size-new Vector2(18,18);float scale=Mathf.Min(available.X/_texture.GetWidth(),available.Y/_texture.GetHeight());Vector2 drawSize=_texture.GetSize()*Mathf.Min(1.8f,scale);DrawTextureRect(_texture,new Rect2((Size-drawSize)*.5f,drawSize),false);
    }
}
