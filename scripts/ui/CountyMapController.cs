using System.Collections.Generic;
using System.Linq;
using AshwoodCounty.Units;
using AshwoodCounty.World;
using AshwoodCounty.World.County;
using AshwoodCounty.World.Fog;
using AshwoodCounty.World.Regions;
using Godot;

namespace AshwoodCounty.UI;

public partial class CountyMapController : CanvasLayer
{
    private static readonly (string NodeName, int RegionIndex)[] MarkerDefinitions =
    [
        ("Outskirts", 0),
        ("Farm", 1),
        ("Mill", 2)
    ];

    private readonly Dictionary<int, Button> _markers = [];
    private Control _panel = null!;
    private Label _detail = null!;
    private CountyProgress _progress = null!;
    private CountyWorld _county = null!;
    private CountyFogOfWar _fog = null!;
    private CountyMapFogOverlay _fogOverlay = null!;
    private int _selectedRegionIndex;

    public bool IsOpen => _panel.Visible;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _progress = GetNode<CountyProgress>("../CountyProgress");
        _county = GetNode<CountyWorld>("../World/CountyWorld");
        _fog = GetNode<CountyFogOfWar>("../World/CountyFog");
        _panel = GetNode<Control>("Panel");
        _panel.MouseFilter = Control.MouseFilterEnum.Stop;

        Control frame = GetNode<Control>("Panel/MapFrame");
        frame.Theme = AshwoodTheme.Create();
        BuildMapHeader(frame);
        BuildDetailCard(frame);

        _detail = GetNode<Label>("Panel/MapFrame/Detail");
        _detail.ThemeTypeVariation = "HudMapDetail";
        _detail.Position = new Vector2(758, 96);
        _detail.Size = new Vector2(246, 378);

        Button close = GetNode<Button>("Panel/MapFrame/Close");
        close.ThemeTypeVariation = "HudMapCloseButton";
        close.TooltipText = "Return to the county view [M or Esc]";
        close.Text = "CLOSE  [M]";
        close.Position = new Vector2(800, 526);
        close.Size = new Vector2(190, 40);
        close.Pressed += Toggle;

        foreach ((string nodeName, int regionIndex) in MarkerDefinitions)
        {
            int capturedIndex = regionIndex;
            Button marker = GetNode<Button>($"Panel/MapFrame/{nodeName}");
            RegionDefinition region = RegionCatalog.All[regionIndex];
            marker.ThemeTypeVariation = "HudMapMarker";
            marker.ToggleMode = true;
            marker.TooltipText = $"{region.Name}\n{region.Description}";
            marker.Size = new Vector2(150, 36);
            marker.Pressed += () => ShowRegion(capturedIndex);
            _markers[regionIndex] = marker;
        }

        _fogOverlay = new CountyMapFogOverlay
        {
            Fog = _fog,
            Position = new Vector2(20, 20),
            Size = new Vector2(710, 580),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        frame.AddChild(_fogOverlay);
        frame.MoveChild(_fogOverlay, 1);

        _panel.Visible = false;
        _fogOverlay.SetMapOpen(false);
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (inputEvent is not InputEventKey { Pressed: true, Echo: false } key)
            return;

        if (key.Keycode != Key.M && (!IsOpen || key.Keycode != Key.Escape))
            return;

        Toggle();
        GetViewport().SetInputAsHandled();
    }

    public void Toggle()
    {
        bool opening = !_panel.Visible;
        _panel.Visible = opening;
        _fogOverlay.SetMapOpen(opening);
        if (opening)
            ShowRegion(FindCurrentMappedRegionIndex() ?? _selectedRegionIndex);
    }

    private void ShowRegion(int index)
    {
        if (index < 0 || index >= RegionCatalog.All.Length)
            return;

        _selectedRegionIndex = index;
        RegionDefinition region = RegionCatalog.All[index];
        RegionState state = _progress.GetState(region.Id);
        CountyLocationDefinition location = CountyMacroLayout.Find(region.Id);
        bool physicallyExplored = location is not null && _fog.IsExplored(location.Center);
        bool known = state.Discovered || region.Availability != RegionAvailability.Unknown;
        string currentRegionId = GetCurrentRegionId();

        UpdateMarkerPresentation(index, currentRegionId);
        if (!known)
        {
            _detail.Text =
                $"{region.Name.ToUpperInvariant()}\n" +
                "LOCATION UNKNOWN\n\n" +
                "Explore connecting roads or learn of this area through survivor knowledge.";
            return;
        }

        string current = currentRegionId == region.Id ? "  •  CURRENT" : string.Empty;
        string landmarks = state.DiscoveredLandmarks.Count > 0
            ? string.Join("\n", state.DiscoveredLandmarks)
            : "None discovered";

        _detail.Text =
            $"{region.Name.ToUpperInvariant()}\n" +
            $"{region.Environment.ToUpperInvariant()}  •  DANGER {region.Danger}/5\n\n" +
            $"{region.Description}\n\n" +
            "STATUS\n" +
            $"Control     {state.Control}{current}\n" +
            $"Terrain     {(physicallyExplored ? "Explored" : "Not explored")}\n" +
            $"County      {_fog.ExploredRatio:P1} explored\n\n" +
            "KNOWN RESOURCES\n" +
            $"{string.Join("  •  ", region.Resources)}\n\n" +
            "DISCOVERED LANDMARKS\n" +
            $"{landmarks}\n\n" +
            "Strategic overview only — survivors travel physically.";
    }

    private void UpdateMarkerPresentation(int selectedIndex, string currentRegionId)
    {
        foreach ((int index, Button marker) in _markers)
        {
            RegionDefinition region = RegionCatalog.All[index];
            marker.ButtonPressed = index == selectedIndex;
            string prefix = index == selectedIndex ? "◆" : currentRegionId == region.Id ? "●" : "•";
            marker.Text = $"{prefix}  {region.Name.ToUpperInvariant()}";
        }
    }

    private int? FindCurrentMappedRegionIndex()
    {
        string currentRegionId = GetCurrentRegionId();
        foreach ((int index, _) in _markers)
        {
            if (RegionCatalog.All[index].Id == currentRegionId)
                return index;
        }

        return null;
    }

    private string GetCurrentRegionId()
    {
        Survivor lead = GetTree().GetNodesInGroup(Survivor.GroupName).OfType<Survivor>().FirstOrDefault();
        return lead is null ? string.Empty : _county.GetRegionAt(lead.SimulationPosition).Id;
    }

    private static void BuildMapHeader(Control frame)
    {
        Label title = new()
        {
            Text = "ASHWOOD COUNTY",
            ThemeTypeVariation = "HudMapTitle",
            Position = new Vector2(750, 22),
            Size = new Vector2(265, 24),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        frame.AddChild(title);

        Label subtitle = new()
        {
            Text = "STRATEGIC OVERVIEW  •  REGIONAL INTELLIGENCE",
            ThemeTypeVariation = "HudMapSubtitle",
            Position = new Vector2(750, 48),
            Size = new Vector2(265, 18),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        frame.AddChild(subtitle);

        HSeparator rule = new()
        {
            Position = new Vector2(750, 70),
            Size = new Vector2(265, 1),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        frame.AddChild(rule);
    }

    private static void BuildDetailCard(Control frame)
    {
        PanelContainer detailCard = new()
        {
            ThemeTypeVariation = "HudMapDetailPanel",
            Position = new Vector2(742, 80),
            Size = new Vector2(278, 410),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        frame.AddChild(detailCard);

        Label detail = frame.GetNode<Label>("Detail");
        frame.MoveChild(detailCard, detail.GetIndex());
    }
}
