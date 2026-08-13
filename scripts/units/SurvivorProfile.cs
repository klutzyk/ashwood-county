using System;
using System.Collections.Generic;

namespace AshwoodCounty.Units;

public enum SurvivorSkill { Labor, Scavenging, Combat, Medical }
public enum WorkCategory { Construction, Woodcutting, Foraging, Scavenging, Hauling }
public enum WorkPriority { Disabled, Allowed, Preferred }

public sealed class SurvivorProfile
{
    public string DisplayName { get; init; } = "Survivor";
    public string Occupation { get; init; } = "County Resident";
    public string HomeRegion { get; init; } = "Ashwood Outskirts";
    public string ImportantLocation { get; init; } = "Old Road Junction";
    public string Trait { get; init; } = "Resourceful";
    public Dictionary<SurvivorSkill, int> Skills { get; } = [];
    public Dictionary<SurvivorSkill, float> Experience { get; } = [];
    public Dictionary<WorkCategory, WorkPriority> WorkPriorities { get; } = [];
    public HashSet<string> KnownRegions { get; } = ["outskirts"];

    public int Skill(SurvivorSkill skill) => Skills.GetValueOrDefault(skill, 1);
    public WorkPriority Priority(WorkCategory category) => WorkPriorities.GetValueOrDefault(category, WorkPriority.Allowed);
    public void AddExperience(SurvivorSkill skill, float amount)
    {
        float xp = Experience.GetValueOrDefault(skill) + Math.Max(0, amount);
        Experience[skill] = xp;
        Skills[skill] = Math.Clamp(1 + (int)(xp / 100), 1, 10);
    }

    public static SurvivorProfile ForIndex(int index)
    {
        (string name, string job, string home, string place, string trait, SurvivorSkill specialty)[] people =
        [
            ("Maya Torres", "Former Nurse", "Ashwood", "County Hospital", "Observant", SurvivorSkill.Medical),
            ("Ben Carter", "Mechanic", "Mill Creek", "Service Station", "Resourceful", SurvivorSkill.Scavenging),
            ("Eli Brooks", "Carpenter", "Farm District", "Old Farmhouse", "Hard Worker", SurvivorSkill.Labor),
            ("June Mercer", "Hunter", "Pine Ridge", "Fire Lookout", "Tough", SurvivorSkill.Combat),
            ("Nora Bell", "Farmer", "South Farmland", "Bell Homestead", "Steady", SurvivorSkill.Labor)
        ];
        var p = people[Math.Abs(index) % people.Length];
        SurvivorProfile profile = new() { DisplayName=p.name, Occupation=p.job, HomeRegion=p.home, ImportantLocation=p.place, Trait=p.trait };
        foreach (SurvivorSkill skill in Enum.GetValues<SurvivorSkill>()) profile.Skills[skill] = skill == p.specialty ? 3 : 1;
        foreach (WorkCategory work in Enum.GetValues<WorkCategory>()) profile.WorkPriorities[work] = WorkPriority.Allowed;
        profile.WorkPriorities[p.specialty switch { SurvivorSkill.Labor => WorkCategory.Construction, SurvivorSkill.Scavenging => WorkCategory.Scavenging, _ => WorkCategory.Hauling }] = WorkPriority.Preferred;
        if (p.home == "Farm District") profile.KnownRegions.Add("farm_district");
        if (p.home == "Mill Creek") profile.KnownRegions.Add("mill_creek");
        return profile;
    }
}
