#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace AshwoodCounty.World.Regions;

/// <summary>In-memory county persistence with an optional small JSON save file.</summary>
public sealed class RegionStateStore
{
    private readonly Dictionary<string, RegionState> _regions = new();

    public string CurrentRegionId { get; set; } = RegionIds.Outskirts;
    public IReadOnlyDictionary<string, RegionState> Regions => _regions;

    public RegionState GetOrCreate(string regionId)
    {
        if (_regions.TryGetValue(regionId, out RegionState? state))
            return state;

        state = new RegionState { RegionId = regionId };
        _regions.Add(regionId, state);
        return state;
    }

    public bool Save(string path = "user://county_regions.json")
    {
        try
        {
            CountyRegionSave save = new() { CurrentRegionId = CurrentRegionId, Regions = _regions };
            using FileAccess? file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            if (file is null)
                return false;
            file.StoreString(JsonSerializer.Serialize(save, JsonOptions));
            return true;
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Could not save county region state: {exception.Message}");
            return false;
        }
    }

    public bool Load(string path = "user://county_regions.json")
    {
        if (!FileAccess.FileExists(path))
            return false;

        try
        {
            using FileAccess? file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            CountyRegionSave? save = file is null
                ? null
                : JsonSerializer.Deserialize<CountyRegionSave>(file.GetAsText(), JsonOptions);
            if (save is null || save.Version != 1)
                return false;

            _regions.Clear();
            foreach ((string id, RegionState state) in save.Regions)
                _regions[id] = state;
            CurrentRegionId = save.CurrentRegionId;
            return true;
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Could not load county region state: {exception.Message}");
            return false;
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
}
