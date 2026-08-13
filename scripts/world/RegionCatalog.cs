using System;
using System.Linq;
using Godot;
namespace AshwoodCounty.World;
public enum RegionAvailability { Current, Known, Unknown }
public enum RegionControl { Unknown, Infested, Contested, Secured, Settled }
public sealed record RegionDefinition(string Id,string Name,string Description,Vector2 CountyPosition,Vector2I Bounds,string Environment,int Danger,string[] Resources,string[] Landmarks,string[] Neighbors,RegionAvailability Availability);
public static class RegionCatalog
{
    public static readonly RegionDefinition[] All=
    [
        R("outskirts","Ashwood Outskirts","Rural foothold and the settlement's first clearing.",.48f,.48f,"Woodland",2,["Wood","Food"],["Road Junction","Abandoned Farmhouse"],["farm_district","ashwood"],RegionAvailability.Current),
        R("farm_district","Farm District","Open fields, farm tracks, windbreaks and abandoned holdings.",.27f,.48f,"Farmland",3,["Food","Materials"],["Bell Homestead","Grain Silos"],["outskirts","mill_creek","south_farmland"],RegionAvailability.Known),
        R("mill_creek","Mill Creek","Dense timber, creek corridor and broken logging infrastructure.",.30f,.68f,"Creek Woodland",4,["Wood","Materials"],["Old Mill","Rail Spur"],["farm_district","old_mill_bridge","logging_camp"],RegionAvailability.Known),
        R("ashwood","Ashwood","The county seat: dense streets, valuable supplies, and heavy infection.",.60f,.42f,"Town",5,["Materials","Medicine"],["County Hospital","Sheriff's Office"],["outskirts","service_station","county_fairgrounds"],RegionAvailability.Unknown),
        R("south_farmland","South Farmland","Broad fields and isolated rural homes south of the district.",.34f,.58f,"Farmland",3,["Food","Wood"],["South Barn"],["farm_district","trailer_park"],RegionAvailability.Unknown),
        R("trailer_park","Trailer Park","A compact residential pocket beside the southern road.",.48f,.67f,"Residential",4,["Materials","Medicine"],["Community Hall"],["south_farmland","service_station"],RegionAvailability.Unknown),
        R("service_station","Service Station","A strategic road stop on Ashwood's southern approach.",.58f,.57f,"Roadside",4,["Materials","Medicine"],["Ashwood Service Station"],["ashwood","trailer_park"],RegionAvailability.Unknown),
        R("county_fairgrounds","County Fairgrounds","Wide open grounds and temporary structures east of town.",.72f,.44f,"Fairgrounds",4,["Food","Materials"],["Grandstand"],["ashwood","blackwater_dam"],RegionAvailability.Unknown),
        R("old_mill_bridge","Old Mill Bridge","A damaged crossing controlling access to western timber country.",.20f,.72f,"River Crossing",5,["Wood","Materials"],["Old Mill Bridge"],["mill_creek","blackwater_lake"],RegionAvailability.Unknown),
        R("logging_camp","Logging Camp","Remote timber camp beneath Pine Ridge.",.22f,.86f,"Forest",5,["Wood","Materials"],["Camp Office"],["mill_creek","pine_ridge"],RegionAvailability.Unknown),
        R("blackwater_dam","Blackwater Dam","Critical infrastructure on the county's eastern watershed.",.84f,.50f,"Industrial",5,["Materials"],["Dam Control House"],["county_fairgrounds","blackwater_lake"],RegionAvailability.Unknown),
        R("blackwater_lake","Blackwater Lake","Forest shoreline and scattered recreation sites.",.76f,.72f,"Lakeside",4,["Food","Wood"],["Lakeside Camp"],["blackwater_dam","old_mill_bridge","fire_lookout"],RegionAvailability.Unknown),
        R("pine_ridge","Pine Ridge","Steep forest and exposed high ground.",.18f,.92f,"Highland Forest",5,["Wood"],["Ridge Trail"],["logging_camp","fire_lookout"],RegionAvailability.Unknown),
        R("fire_lookout","Fire Lookout","A remote tower with a commanding view of the county.",.46f,.91f,"Highland",4,["Medicine"],["Lookout Tower"],["pine_ridge","blackwater_lake"],RegionAvailability.Unknown)
    ];
    public static RegionDefinition Find(string id)=>All.FirstOrDefault(r=>r.Id==id);
    private static RegionDefinition R(string id,string name,string description,float x,float y,string environment,int danger,string[] resources,string[] landmarks,string[] neighbors,RegionAvailability availability)=>new(id,name,description,new Vector2(x,y),new Vector2I(42,38),environment,danger,resources,landmarks,neighbors,availability);
}
