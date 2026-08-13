using System;
using System.Collections.Generic;
using AshwoodCounty.Buildings;
using AshwoodCounty.Resources;
using AshwoodCounty.Systems;
using AshwoodCounty.Units;
using Godot;

namespace AshwoodCounty.UI;

public partial class GameHud : CanvasLayer
{
    public const string GroupName = "game_hud";

    private enum HudCategory { Build, Work, People, County }
    private enum SurvivorTab { Overview, Skills, Work }

    private SettlementInventory _inventory = null!;
    private GameClock _clock = null!;
    private SimulationController _simulation = null!;
    private SurvivorSelectionController _selection = null!;
    private BuildingPlacementController _placement = null!;
    private ChopDesignationController _designation = null!;

    private readonly Dictionary<ResourceType, Label> _resourceValues = [];
    private readonly Dictionary<HudCategory, Button> _categoryButtons = [];
    private readonly Dictionary<HudCategory, Control> _palettes = [];
    private readonly Dictionary<SurvivorTab, Button> _tabButtons = [];
    private readonly Dictionary<SurvivorTab, Control> _tabContents = [];
    private readonly Dictionary<string, ProgressBar> _vitalBars = [];
    private readonly Dictionary<string, Label> _vitalValues = [];
    private readonly Dictionary<SurvivorSkill, ProgressBar> _skillBars = [];
    private readonly Dictionary<SurvivorSkill, Label> _skillValues = [];
    private readonly Dictionary<WorkCategory, Dictionary<WorkPriority, Button>> _priorityButtons = [];

    private Label _time = null!;
    private Label _simulationState = null!;
    private Label _survivorName = null!;
    private Label _survivorMeta = null!;
    private Label _overviewText = null!;
    private Label _groupText = null!;
    private Label _paletteHint = null!;
    private PanelContainer _survivorPanel = null!;
    private PanelContainer _toastPanel = null!;
    private Label _toast = null!;
    private HBoxContainer _vitals = null!;
    private HBoxContainer _tabs = null!;
    private double _toastRemaining;
    private double _refresh;
    private HudCategory? _activeCategory;
    private SurvivorTab _activeSurvivorTab = SurvivorTab.Overview;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        AddToGroup(GroupName);
        _inventory = GetNode<SettlementInventory>("../SettlementInventory");
        _clock = GetNode<GameClock>("../GameClock");
        _simulation = GetNode<SimulationController>("../SimulationController");
        _selection = GetNode<SurvivorSelectionController>("../SelectionController");
        _placement = GetNode<BuildingPlacementController>("../BuildingPlacementController");
        _designation = GetNode<ChopDesignationController>("../ChopDesignationController");

        MarginContainer safe = new()
        {
            AnchorsPreset = (int)Control.LayoutPreset.FullRect,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Theme = AshwoodTheme.Create()
        };
        AddChild(safe);
        safe.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        safe.AddThemeConstantOverride("margin_left", 16);
        safe.AddThemeConstantOverride("margin_top", 10);
        safe.AddThemeConstantOverride("margin_right", 16);
        safe.AddThemeConstantOverride("margin_bottom", 12);

        VBoxContainer screen = Layout<VBoxContainer>();
        safe.AddChild(screen);
        screen.AddChild(BuildTopBar());
        screen.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore });
        screen.AddChild(BuildLowerHud());

        CollapsePalettes();
        SelectSurvivorTab(SurvivorTab.Overview);
    }

    private Control BuildTopBar()
    {
        CenterContainer center = Layout<CenterContainer>();
        PanelContainer panel = Panel("HudTopPanel", new Vector2(760, 0));
        center.AddChild(panel);

        HBoxContainer bar = Layout<HBoxContainer>();
        panel.AddChild(bar);

        HBoxContainer identity = Layout<HBoxContainer>();
        identity.CustomMinimumSize = new Vector2(140, 0);
        identity.AddChild(Icon("county", 28));
        VBoxContainer brand = Layout<VBoxContainer>();
        brand.AddChild(Text("ASHWOOD", "HudTitle"));
        brand.AddChild(Text("COUNTY", "HudTiny"));
        identity.AddChild(brand);
        bar.AddChild(identity);
        bar.AddChild(Separator(true));

        AddResourceReadout(bar, ResourceType.Wood, "WOOD", "wood");
        AddResourceReadout(bar, ResourceType.Food, "FOOD", "food");
        AddResourceReadout(bar, ResourceType.Materials, "MATERIALS", "materials");
        AddResourceReadout(bar, ResourceType.Medicine, "MEDICINE", "medicine");

        Control stretch = new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
        bar.AddChild(stretch);
        bar.AddChild(Separator(true));

        VBoxContainer clock = Layout<VBoxContainer>();
        clock.CustomMinimumSize = new Vector2(82, 0);
        clock.MouseFilter = Control.MouseFilterEnum.Stop;
        clock.TooltipText = "County day, local time, and current simulation speed.";
        _time = Text("DAY 1  09:00", "HudHeading");
        _time.HorizontalAlignment = HorizontalAlignment.Right;
        _simulationState = Text("1x", "HudTiny");
        _simulationState.HorizontalAlignment = HorizontalAlignment.Right;
        clock.AddChild(_time);
        clock.AddChild(_simulationState);
        bar.AddChild(clock);

        AddSpeedButton(bar, string.Empty, 0, "Pause or resume [Space]", "pause");
        AddSpeedButton(bar, "1×", 1, "Normal speed [1]");
        AddSpeedButton(bar, "2×", 2, "Fast speed [2]");
        AddSpeedButton(bar, "3×", 3, "Very fast speed [3]");
        return center;
    }

    private Control BuildLowerHud()
    {
        HBoxContainer row = Layout<HBoxContainer>();
        row.Alignment = BoxContainer.AlignmentMode.End;

        VBoxContainer notificationColumn = Layout<VBoxContainer>();
        notificationColumn.CustomMinimumSize = new Vector2(326, 0);
        notificationColumn.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        _toastPanel = Panel("HudToastPanel");
        _toastPanel.CustomMinimumSize = new Vector2(205, 0);
        _toastPanel.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        _toastPanel.Visible = false;
        HBoxContainer toastRow = Layout<HBoxContainer>();
        toastRow.AddChild(Icon("county", 20));
        _toast = Text(string.Empty, "HudMuted");
        _toast.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        toastRow.AddChild(_toast);
        _toastPanel.AddChild(toastRow);
        notificationColumn.AddChild(_toastPanel);
        row.AddChild(notificationColumn);

        Control leftStretch = new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddChild(leftStretch);
        row.AddChild(BuildActionArea());
        Control rightStretch = new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddChild(rightStretch);
        VBoxContainer survivorColumn = Layout<VBoxContainer>();
        survivorColumn.CustomMinimumSize = new Vector2(326, 0);
        survivorColumn.AddChild(BuildSurvivorPanel());
        row.AddChild(survivorColumn);
        return row;
    }

    private Control BuildActionArea()
    {
        VBoxContainer column = Layout<VBoxContainer>();
        column.CustomMinimumSize = new Vector2(435, 0);

        _paletteHint = Text("Designate work in the world.", "HudMuted");
        _paletteHint.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(_paletteHint);

        _palettes[HudCategory.Build] = BuildBuildPalette();
        _palettes[HudCategory.Work] = BuildWorkPalette();
        _palettes[HudCategory.People] = BuildPeoplePalette();
        _palettes[HudCategory.County] = BuildCountyPalette();
        foreach (Control palette in _palettes.Values) column.AddChild(palette);

        PanelContainer toolbarPanel = Panel("HudToolbarPanel");
        HBoxContainer toolbar = Layout<HBoxContainer>();
        toolbar.Alignment = BoxContainer.AlignmentMode.Center;
        toolbarPanel.AddChild(toolbar);
        AddCategory(toolbar, HudCategory.Build, "BUILD", "build", "Construction and settlement structures");
        AddCategory(toolbar, HudCategory.Work, "WORK", "work", "Harvesting and scavenging designations");
        AddCategory(toolbar, HudCategory.People, "PEOPLE", "people", "Selected survivor information");
        AddCategory(toolbar, HudCategory.County, "COUNTY", "county", "County map, discoveries and regional control");
        column.AddChild(toolbarPanel);
        return column;
    }

    private Control BuildBuildPalette()
    {
        PanelContainer panel = Panel("HudPalettePanel");
        HBoxContainer row = Layout<HBoxContainer>();
        row.Alignment = BoxContainer.AlignmentMode.Center;
        panel.AddChild(row);
        AddAction(row, "SHELTER  30", "shelter", () => _placement.BeginPlacement(BuildingCatalog.Shelter), "Provides survivor resting capacity. Costs 30 Wood.");
        AddAction(row, "STORAGE  20", "storage", () => _placement.BeginPlacement(BuildingCatalog.ProvisionsShed), "Stores settlement provisions. Costs 20 Wood.");
        AddAction(row, "OUTPOST  12", "outpost", () => _placement.BeginPlacement(BuildingCatalog.Outpost), "Extends settlement control. Costs 12 Materials.");
        return panel;
    }

    private Control BuildWorkPalette()
    {
        PanelContainer panel = Panel("HudPalettePanel");
        HBoxContainer row = Layout<HBoxContainer>();
        row.Alignment = BoxContainer.AlignmentMode.Center;
        panel.AddChild(row);
        AddAction(row, "CHOP", "chop", _designation.ToggleDesignation, "Designate trees for timber harvesting.");
        AddAction(row, "FORAGE", "forage", _designation.ToggleForageDesignation, "Designate food-bearing plants.");
        AddAction(row, "SCAVENGE", "scavenge", _designation.ToggleScavengeDesignation, "Search abandoned locations for salvage.");
        AddAction(row, "HAUL", "haul", () => Notify("Hauling is assigned automatically by work priority."), "Hauling uses survivor work priorities.");
        AddAction(row, "CANCEL", "cancel", _designation.EndDesignation, "Cancel the active work designation.");
        return panel;
    }

    private Control BuildPeoplePalette()
    {
        PanelContainer panel = Panel("HudPalettePanel");
        Label message = Text("Select a survivor to inspect needs, skills and work priorities.", "HudMuted");
        message.HorizontalAlignment = HorizontalAlignment.Center;
        panel.AddChild(message);
        return panel;
    }

    private Control BuildCountyPalette()
    {
        PanelContainer panel = Panel("HudPalettePanel");
        HBoxContainer row = Layout<HBoxContainer>();
        row.Alignment = BoxContainer.AlignmentMode.Center;
        panel.AddChild(row);
        AddAction(row, "COUNTY MAP  [M]", "county", OpenCountyMap, "Open the strategic county overview.");
        return panel;
    }

    private Control BuildSurvivorPanel()
    {
        _survivorPanel = Panel("HudSurvivorPanel", new Vector2(326, 0));
        _survivorPanel.Visible = false;
        VBoxContainer rows = Layout<VBoxContainer>();
        _survivorPanel.AddChild(rows);

        HBoxContainer identity = Layout<HBoxContainer>();
        TextureRect portrait = Icon("survivor_portrait", 62);
        portrait.CustomMinimumSize = new Vector2(62, 62);
        identity.AddChild(portrait);
        VBoxContainer identityText = Layout<VBoxContainer>();
        _survivorName = Text("SURVIVOR", "HudSurvivorName");
        _survivorMeta = Text(string.Empty, "HudMuted");
        _survivorMeta.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        identityText.AddChild(_survivorName);
        identityText.AddChild(_survivorMeta);
        identity.AddChild(identityText);
        rows.AddChild(identity);
        rows.AddChild(Separator(false));

        _vitals = Layout<HBoxContainer>();
        VBoxContainer vitalRows = Layout<VBoxContainer>();
        _vitals.AddChild(vitalRows);
        AddVital(vitalRows, "Health", new Color("709557ff"));
        AddVital(vitalRows, "Hunger", new Color("c49a3eff"));
        AddVital(vitalRows, "Energy", new Color("4d8fb8ff"));
        AddVital(vitalRows, "Morale", new Color("806ab7ff"));
        rows.AddChild(_vitals);
        rows.AddChild(Separator(false));

        _tabs = Layout<HBoxContainer>();
        _tabs.Alignment = BoxContainer.AlignmentMode.Center;
        AddTab(_tabs, SurvivorTab.Overview);
        AddTab(_tabs, SurvivorTab.Skills);
        AddTab(_tabs, SurvivorTab.Work);
        rows.AddChild(_tabs);

        _tabContents[SurvivorTab.Overview] = BuildOverviewTab();
        _tabContents[SurvivorTab.Skills] = BuildSkillsTab();
        _tabContents[SurvivorTab.Work] = BuildWorkTab();
        foreach (Control content in _tabContents.Values) rows.AddChild(content);

        _groupText = Text(string.Empty, "HudMuted");
        _groupText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _groupText.Visible = false;
        rows.AddChild(_groupText);
        return _survivorPanel;
    }

    private Control BuildOverviewTab()
    {
        VBoxContainer content = Layout<VBoxContainer>();
        _overviewText = Text(string.Empty, "HudMuted");
        _overviewText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _overviewText.CustomMinimumSize = new Vector2(0, 60);
        content.AddChild(_overviewText);
        return content;
    }

    private Control BuildSkillsTab()
    {
        VBoxContainer content = Layout<VBoxContainer>();
        foreach (SurvivorSkill skill in Enum.GetValues<SurvivorSkill>())
        {
            HBoxContainer row = Layout<HBoxContainer>();
            Label name = Text(skill.ToString(), "HudMuted");
            name.CustomMinimumSize = new Vector2(74, 0);
            row.AddChild(name);
            ProgressBar bar = CreateBar(new Color("a8935eff"), 10);
            bar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(bar);
            Label value = Text("1", "HudHeading");
            value.CustomMinimumSize = new Vector2(18, 0);
            value.HorizontalAlignment = HorizontalAlignment.Right;
            row.AddChild(value);
            _skillBars[skill] = bar;
            _skillValues[skill] = value;
            content.AddChild(row);
        }
        return content;
    }

    private Control BuildWorkTab()
    {
        VBoxContainer content = Layout<VBoxContainer>();
        foreach (WorkCategory category in Enum.GetValues<WorkCategory>())
        {
            HBoxContainer row = Layout<HBoxContainer>();
            Label name = Text(category.ToString(), "HudTiny");
            name.CustomMinimumSize = new Vector2(68, 0);
            row.AddChild(name);
            Dictionary<WorkPriority, Button> choices = [];
            foreach ((WorkPriority priority, string label) in new[]
            {
                (WorkPriority.Allowed, "ALLOWED"),
                (WorkPriority.Preferred, "PREFERRED"),
                (WorkPriority.Disabled, "DISABLED")
            })
            {
                Button button = HudButton(label, "HudPriorityButton");
                button.ToggleMode = true;
                button.CustomMinimumSize = new Vector2(70, 23);
                button.TooltipText = $"Set {category} work to {priority.ToString().ToLowerInvariant()}.";
                WorkCategory capturedCategory = category;
                WorkPriority capturedPriority = priority;
                button.Pressed += () => SetPriority(capturedCategory, capturedPriority);
                row.AddChild(button);
                choices[priority] = button;
            }
            _priorityButtons[category] = choices;
            content.AddChild(row);
        }
        return content;
    }

    private void AddResourceReadout(Container parent, ResourceType resource, string name, string iconName)
    {
        HBoxContainer readout = Layout<HBoxContainer>();
        readout.CustomMinimumSize = new Vector2(resource == ResourceType.Materials ? 82 : 67, 0);
        readout.MouseFilter = Control.MouseFilterEnum.Stop;
        readout.TooltipText = $"{name[..1]}{name[1..].ToLowerInvariant()} available to the settlement.";
        readout.AddChild(Icon(iconName, 20));
        VBoxContainer copy = Layout<VBoxContainer>();
        Label value = Text("0", "HudResourceValue");
        Label caption = Text(name, "HudResourceName");
        copy.AddChild(value);
        copy.AddChild(caption);
        readout.AddChild(copy);
        parent.AddChild(readout);
        _resourceValues[resource] = value;
    }

    private void AddVital(Container parent, string name, Color color)
    {
        HBoxContainer row = Layout<HBoxContainer>();
        row.AddChild(Icon(name.ToLowerInvariant(), 15));
        Label title = Text(name, "HudMuted");
        title.CustomMinimumSize = new Vector2(49, 0);
        row.AddChild(title);
        ProgressBar bar = CreateBar(color, 100);
        bar.CustomMinimumSize = new Vector2(150, 9);
        bar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(bar);
        Label value = Text("100%", "HudTiny");
        value.CustomMinimumSize = new Vector2(40, 0);
        value.HorizontalAlignment = HorizontalAlignment.Right;
        row.AddChild(value);
        parent.AddChild(row);
        _vitalBars[name] = bar;
        _vitalValues[name] = value;
    }

    private static ProgressBar CreateBar(Color color, double max)
    {
        ProgressBar bar = new()
        {
            MaxValue = max,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(120, 10),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        StyleBoxFlat fill = new()
        {
            BgColor = color,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3
        };
        bar.AddThemeStyleboxOverride("fill", fill);
        return bar;
    }

    private void AddCategory(Container parent, HudCategory category, string text, string icon, string tooltip)
    {
        Button button = HudButton(text, "HudCategoryButton", tooltip, icon);
        button.ToggleMode = true;
        button.CustomMinimumSize = new Vector2(92, 40);
        button.Pressed += () => SelectCategory(category);
        parent.AddChild(button);
        _categoryButtons[category] = button;
    }

    private void AddTab(Container parent, SurvivorTab tab)
    {
        string tooltip = tab switch
        {
            SurvivorTab.Overview => "Background, current activity, and defining trait.",
            SurvivorTab.Skills => "Current survivor skill levels.",
            _ => "Allowed, preferred, and disabled work priorities."
        };
        Button button = HudButton(tab.ToString().ToUpperInvariant(), "HudTabButton", tooltip);
        button.ToggleMode = true;
        button.CustomMinimumSize = new Vector2(88, 27);
        button.Pressed += () => SelectSurvivorTab(tab);
        parent.AddChild(button);
        _tabButtons[tab] = button;
    }

    private static void AddAction(Container parent, string text, string icon, Action action, string tooltip)
    {
        Button button = HudButton(text, "HudActionButton", tooltip, icon);
        button.CustomMinimumSize = new Vector2(82, 38);
        button.Pressed += action;
        parent.AddChild(button);
    }

    private void AddSpeedButton(Container parent, string text, int speed, string tooltip, string icon = "")
    {
        Button button = HudButton(text, "HudSpeedButton", tooltip, icon);
        button.CustomMinimumSize = new Vector2(29, 27);
        button.Pressed += () =>
        {
            if (speed == 0) _simulation.TogglePause();
            else _simulation.SetSpeed(speed);
        };
        parent.AddChild(button);
    }

    private void SelectCategory(HudCategory category)
    {
        if (_activeCategory == category)
        {
            CollapsePalettes();
            return;
        }

        _activeCategory = category;
        _paletteHint.Visible = true;
        foreach ((HudCategory item, Control palette) in _palettes) palette.Visible = item == category;
        foreach ((HudCategory item, Button button) in _categoryButtons) button.ButtonPressed = item == category;
        _paletteHint.Text = CategoryHint(category);
    }

    private void CollapsePalettes()
    {
        _activeCategory = null;
        foreach (Control palette in _palettes.Values) palette.Visible = false;
        foreach (Button button in _categoryButtons.Values) button.ButtonPressed = false;
        _paletteHint.Text = string.Empty;
        _paletteHint.Visible = false;
    }

    private void SelectSurvivorTab(SurvivorTab tab)
    {
        _activeSurvivorTab = tab;
        foreach ((SurvivorTab item, Control content) in _tabContents) content.Visible = item == tab;
        foreach ((SurvivorTab item, Button button) in _tabButtons) button.ButtonPressed = item == tab;
    }

    private void SetPriority(WorkCategory category, WorkPriority priority)
    {
        if (_selection.SelectedCount != 1) return;
        _selection.SelectedSurvivors[0].Profile.WorkPriorities[category] = priority;
        RefreshPriorities(_selection.SelectedSurvivors[0].Profile);
    }

    private void RefreshPriorities(SurvivorProfile profile)
    {
        foreach ((WorkCategory category, Dictionary<WorkPriority, Button> choices) in _priorityButtons)
        {
            WorkPriority active = profile.Priority(category);
            foreach ((WorkPriority priority, Button button) in choices)
            {
                button.Disabled = false;
                button.ButtonPressed = priority == active;
            }
        }
    }

    private void OpenCountyMap()
    {
        CountyMapController countyMap = GetNode<CountyMapController>("../CountyMap");
        countyMap._UnhandledInput(new InputEventKey { Keycode = Key.M, Pressed = true });
    }

    public void Notify(string message)
    {
        _toast.Text = message;
        _toastPanel.Visible = true;
        _toastRemaining = 6;
    }

    public override void _Process(double delta)
    {
        if (_toastRemaining > 0)
        {
            _toastRemaining -= delta;
            if (_toastRemaining <= 0) _toastPanel.Visible = false;
        }

        _refresh -= delta;
        if (_refresh > 0) return;
        _refresh = .15;

        foreach ((ResourceType type, Label value) in _resourceValues)
        {
            value.Text = _inventory.DevUnlimitedResources ? "∞" : _inventory.GetAmount(type).ToString();
        }

        _time.Text = _clock.DisplayTime.ToUpperInvariant();
        _simulationState.Text = _simulation.IsPaused ? "PAUSED" : $"{_simulation.Speed}x SPEED";
        RefreshActionHint();
        RefreshSelection();
    }

    private void RefreshActionHint()
    {
        if (_placement.IsPlacementActive)
        {
            _paletteHint.Visible = true;
            _paletteHint.Text = _placement.CurrentFeedback;
        }
        else if (_designation.IsDesignationActive)
        {
            _paletteHint.Visible = true;
            _paletteHint.Text = "Designation active — click targets; right-click or Esc to finish.";
        }
        else if (_activeCategory is HudCategory category)
        {
            _paletteHint.Visible = true;
            _paletteHint.Text = CategoryHint(category);
        }
        else
        {
            _paletteHint.Visible = false;
            _paletteHint.Text = string.Empty;
        }
    }

    private static string CategoryHint(HudCategory category) => category switch
    {
        HudCategory.Build => "Choose a structure, then place it in the world.",
        HudCategory.Work => "Choose a designation, then mark targets in the world.",
        HudCategory.People => "Survivor details appear when one or more people are selected.",
        _ => "Review county control, discoveries and known routes."
    };

    private void RefreshSelection()
    {
        _survivorPanel.Visible = _selection.SelectedCount > 0;
        if (_selection.SelectedCount == 0) return;

        bool single = _selection.SelectedCount == 1;
        _vitals.Visible = single;
        _tabs.Visible = single;
        _groupText.Visible = !single;
        foreach ((SurvivorTab tab, Control content) in _tabContents) content.Visible = single && tab == _activeSurvivorTab;

        if (!single)
        {
            _survivorName.Text = $"{_selection.SelectedCount} SURVIVORS";
            _survivorMeta.Text = "SELECTED GROUP";
            _groupText.Text = "Issue movement, combat, or designation orders to the selected group.";
            return;
        }

        Survivor survivor = _selection.SelectedSurvivors[0];
        SurvivorProfile profile = survivor.Profile;
        _survivorName.Text = profile.DisplayName.ToUpperInvariant();
        _survivorMeta.Text = $"{profile.Occupation}\n{survivor.Activity}";
        _overviewText.Text = $"FROM  {profile.HomeRegion}\n{profile.ImportantLocation}\n\nTRAIT  {profile.Trait}";

        SetVital("Health", survivor.Health / Mathf.Max(1, survivor.MaxHealth) * 100f);
        SetVital("Hunger", survivor.Hunger);
        SetVital("Energy", survivor.Energy);
        SetVital("Morale", survivor.Morale);
        foreach (SurvivorSkill skill in Enum.GetValues<SurvivorSkill>())
        {
            int level = profile.Skill(skill);
            _skillBars[skill].Value = level;
            _skillValues[skill].Text = level.ToString();
        }
        RefreshPriorities(profile);
    }

    private void SetVital(string name, float value)
    {
        float clamped = Mathf.Clamp(value, 0, 100);
        _vitalBars[name].Value = clamped;
        _vitalValues[name].Text = $"{clamped:0}%";
    }

    private static T Layout<T>() where T : Control, new()
    {
        return new T { MouseFilter = Control.MouseFilterEnum.Ignore };
    }

    private static PanelContainer Panel(string variation, Vector2 minimum = default)
    {
        return new PanelContainer
        {
            ThemeTypeVariation = variation,
            CustomMinimumSize = minimum,
            MouseFilter = Control.MouseFilterEnum.Stop
        };
    }

    private static Label Text(string text, string variation = "")
    {
        return new Label
        {
            Text = text,
            ThemeTypeVariation = variation,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
    }

    private static Control Separator(bool vertical)
    {
        return vertical
            ? new VSeparator { MouseFilter = Control.MouseFilterEnum.Ignore }
            : new HSeparator { MouseFilter = Control.MouseFilterEnum.Ignore };
    }

    private static TextureRect Icon(string name, int size)
    {
        return new TextureRect
        {
            Texture = GD.Load<Texture2D>($"res://assets/ui/icons/{name}.svg"),
            CustomMinimumSize = new Vector2(size, size),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
    }

    private static Button HudButton(string text, string variation, string tooltip = "", string icon = "")
    {
        Button button = new()
        {
            Text = text,
            ThemeTypeVariation = variation,
            TooltipText = tooltip,
            MouseFilter = Control.MouseFilterEnum.Stop,
            ExpandIcon = true,
            Alignment = HorizontalAlignment.Center,
            IconAlignment = HorizontalAlignment.Left,
            ClipText = true
        };
        button.AddThemeConstantOverride("icon_max_width", 18);
        if (!string.IsNullOrEmpty(icon)) button.Icon = GD.Load<Texture2D>($"res://assets/ui/icons/{icon}.svg");
        return button;
    }
}
