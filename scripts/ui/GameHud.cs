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
        safe.AddThemeConstantOverride("margin_top", 12);
        safe.AddThemeConstantOverride("margin_right", 16);
        safe.AddThemeConstantOverride("margin_bottom", 14);

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
        PanelContainer panel = Panel("HudTopPanel", new Vector2(840, 0));
        center.AddChild(panel);

        HBoxContainer bar = Layout<HBoxContainer>();
        panel.AddChild(bar);

        VBoxContainer brand = Layout<VBoxContainer>();
        brand.CustomMinimumSize = new Vector2(150, 0);
        brand.AddChild(Text("ASHWOOD COUNTY", "HudTitle"));
        brand.AddChild(Text("COUNTY SETTLEMENT", "HudTiny"));
        bar.AddChild(brand);
        bar.AddChild(Separator(true));

        AddResourceReadout(bar, ResourceType.Wood, "WOOD");
        AddResourceReadout(bar, ResourceType.Food, "FOOD");
        AddResourceReadout(bar, ResourceType.Materials, "MATERIALS");
        AddResourceReadout(bar, ResourceType.Medicine, "MEDICINE");

        Control stretch = new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
        bar.AddChild(stretch);
        bar.AddChild(Separator(true));

        VBoxContainer clock = Layout<VBoxContainer>();
        _time = Text("DAY 1  09:00", "HudHeading");
        _time.HorizontalAlignment = HorizontalAlignment.Right;
        _simulationState = Text("1x", "HudTiny");
        _simulationState.HorizontalAlignment = HorizontalAlignment.Right;
        clock.AddChild(_time);
        clock.AddChild(_simulationState);
        bar.AddChild(clock);

        AddSpeedButton(bar, "||", 0, "Pause or resume [Space]");
        AddSpeedButton(bar, ">", 1, "Normal speed [1]");
        AddSpeedButton(bar, ">>", 2, "Fast speed [2]");
        AddSpeedButton(bar, ">>>", 3, "Very fast speed [3]");
        return center;
    }

    private Control BuildLowerHud()
    {
        HBoxContainer row = Layout<HBoxContainer>();
        row.Alignment = BoxContainer.AlignmentMode.End;

        VBoxContainer notificationColumn = Layout<VBoxContainer>();
        notificationColumn.CustomMinimumSize = new Vector2(225, 0);
        notificationColumn.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        _toastPanel = Panel("HudToastPanel");
        _toastPanel.Visible = false;
        _toast = Text(string.Empty, "HudMuted");
        _toast.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _toastPanel.AddChild(_toast);
        notificationColumn.AddChild(_toastPanel);
        row.AddChild(notificationColumn);

        Control leftStretch = new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddChild(leftStretch);
        row.AddChild(BuildActionArea());
        Control rightStretch = new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddChild(rightStretch);
        row.AddChild(BuildSurvivorPanel());
        return row;
    }

    private Control BuildActionArea()
    {
        VBoxContainer column = Layout<VBoxContainer>();
        column.CustomMinimumSize = new Vector2(480, 0);

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
        AddCategory(toolbar, HudCategory.Build, "BUILD", "Construction and settlement structures");
        AddCategory(toolbar, HudCategory.Work, "WORK", "Harvesting and scavenging designations");
        AddCategory(toolbar, HudCategory.People, "PEOPLE", "Selected survivor information");
        AddCategory(toolbar, HudCategory.County, "COUNTY", "County map, discoveries and regional control");
        column.AddChild(toolbarPanel);
        return column;
    }

    private Control BuildBuildPalette()
    {
        PanelContainer panel = Panel("HudPalettePanel");
        HBoxContainer row = Layout<HBoxContainer>();
        row.Alignment = BoxContainer.AlignmentMode.Center;
        panel.AddChild(row);
        AddAction(row, "SHELTER\n30 WOOD", () => _placement.BeginPlacement(BuildingCatalog.Shelter), "Provides survivor resting capacity.");
        AddAction(row, "STORAGE\n20 WOOD", () => _placement.BeginPlacement(BuildingCatalog.ProvisionsShed), "Stores settlement provisions.");
        AddAction(row, "OUTPOST\n12 MATERIALS", () => _placement.BeginPlacement(BuildingCatalog.Outpost), "Extends settlement control.");
        return panel;
    }

    private Control BuildWorkPalette()
    {
        PanelContainer panel = Panel("HudPalettePanel");
        HBoxContainer row = Layout<HBoxContainer>();
        row.Alignment = BoxContainer.AlignmentMode.Center;
        panel.AddChild(row);
        AddAction(row, "CHOP", _designation.ToggleDesignation, "Designate trees for timber harvesting.");
        AddAction(row, "FORAGE", _designation.ToggleForageDesignation, "Designate food-bearing plants.");
        AddAction(row, "SCAVENGE", _designation.ToggleScavengeDesignation, "Search abandoned locations for salvage.");
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
        AddAction(row, "OPEN COUNTY MAP  [M]", OpenCountyMap, "Open the strategic county overview.");
        return panel;
    }

    private Control BuildSurvivorPanel()
    {
        _survivorPanel = Panel("HudSurvivorPanel", new Vector2(395, 0));
        _survivorPanel.Visible = false;
        VBoxContainer rows = Layout<VBoxContainer>();
        _survivorPanel.AddChild(rows);

        _survivorName = Text("SURVIVOR", "HudSurvivorName");
        _survivorMeta = Text(string.Empty, "HudMuted");
        _survivorMeta.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        rows.AddChild(_survivorName);
        rows.AddChild(_survivorMeta);
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
        _overviewText.CustomMinimumSize = new Vector2(0, 92);
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
            name.CustomMinimumSize = new Vector2(92, 0);
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
            name.CustomMinimumSize = new Vector2(88, 0);
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
                button.CustomMinimumSize = new Vector2(84, 26);
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

    private void AddResourceReadout(Container parent, ResourceType resource, string name)
    {
        VBoxContainer readout = Layout<VBoxContainer>();
        readout.CustomMinimumSize = new Vector2(resource == ResourceType.Materials ? 76 : 58, 0);
        Label value = Text("0", "HudResourceValue");
        value.HorizontalAlignment = HorizontalAlignment.Center;
        Label caption = Text(name, "HudResourceName");
        caption.HorizontalAlignment = HorizontalAlignment.Center;
        readout.AddChild(value);
        readout.AddChild(caption);
        parent.AddChild(readout);
        _resourceValues[resource] = value;
    }

    private void AddVital(Container parent, string name, Color color)
    {
        HBoxContainer row = Layout<HBoxContainer>();
        Label title = Text(name, "HudMuted");
        title.CustomMinimumSize = new Vector2(62, 0);
        row.AddChild(title);
        ProgressBar bar = CreateBar(color, 100);
        bar.CustomMinimumSize = new Vector2(220, 12);
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

    private void AddCategory(Container parent, HudCategory category, string text, string tooltip)
    {
        Button button = HudButton(text, "HudCategoryButton", tooltip);
        button.ToggleMode = true;
        button.CustomMinimumSize = new Vector2(105, 42);
        button.Pressed += () => SelectCategory(category);
        parent.AddChild(button);
        _categoryButtons[category] = button;
    }

    private void AddTab(Container parent, SurvivorTab tab)
    {
        Button button = HudButton(tab.ToString().ToUpperInvariant(), "HudTabButton");
        button.ToggleMode = true;
        button.CustomMinimumSize = new Vector2(104, 29);
        button.Pressed += () => SelectSurvivorTab(tab);
        parent.AddChild(button);
        _tabButtons[tab] = button;
    }

    private static void AddAction(Container parent, string text, Action action, string tooltip)
    {
        Button button = HudButton(text, "HudActionButton", tooltip);
        button.CustomMinimumSize = new Vector2(118, 42);
        button.Pressed += action;
        parent.AddChild(button);
    }

    private void AddSpeedButton(Container parent, string text, int speed, string tooltip)
    {
        Button button = HudButton(text, "HudSpeedButton", tooltip);
        button.CustomMinimumSize = new Vector2(speed >= 2 ? 43 : 34, 32);
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
        _paletteHint.Text = category switch
        {
            HudCategory.Build => "Choose a structure, then place it in the world.",
            HudCategory.Work => "Choose a designation, then mark targets in the world.",
            HudCategory.People => "Survivor details appear when one or more people are selected.",
            _ => "Review county control, discoveries and known routes."
        };
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
        if (_placement.IsPlacementActive){_paletteHint.Visible=true;_paletteHint.Text = _placement.CurrentFeedback;}
        else if (_designation.IsDesignationActive){_paletteHint.Visible=true;_paletteHint.Text = "Designation active - click targets; right-click or Esc to finish.";}
    }

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
        _survivorMeta.Text = $"{profile.Occupation}\n{profile.HomeRegion} - {profile.ImportantLocation}";
        _overviewText.Text = $"ACTIVITY\n{survivor.Activity}\n\nTRAIT\n{profile.Trait}";

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

    private static Button HudButton(string text, string variation, string tooltip = "")
    {
        return new Button
        {
            Text = text,
            ThemeTypeVariation = variation,
            TooltipText = tooltip,
            MouseFilter = Control.MouseFilterEnum.Stop
        };
    }
}
