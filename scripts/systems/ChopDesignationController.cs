using System.Linq;
using AshwoodCounty.Jobs;
using AshwoodCounty.Resources;
using AshwoodCounty.Systems;
using AshwoodCounty.Units;
using Godot;

namespace AshwoodCounty.Systems;

public enum WorkMode
{
    None,
    Chop,
    Forage,
    Scavenge,
    Haul
}

/// <summary>
/// The WORK command bar state. Activating a work category immediately
/// highlights every currently valid target in the world, starts automatic
/// work for selected survivors who allow the category, and keeps manual
/// designation clicks working as before (explicit designations take
/// priority). Cancelling the mode clears highlights and work mandates.
/// </summary>
public partial class ChopDesignationController : CanvasLayer
{
    private Button _chopButton = null!;
    private Button _forageButton = null!;
    private Label _status = null!;
    private SettlementJobSystem _jobs = null!;
    private SurvivorSelectionController _selection = null!;
    private double _highlightRefresh;
    private bool _scavengeMode;

    public WorkMode ActiveMode { get; private set; } = WorkMode.None;
    public bool IsDesignationActive => ActiveMode != WorkMode.None;
    public ResourceType DesignatedResourceType { get; private set; } = ResourceType.Wood;
    public string InstructionText { get; private set; } = "Choose a work action, then mark targets in the world.";

    public override void _Ready()
    {
        // Pausing stops the simulation, not the player. GetTree().Paused halts
        // _Process and input for every node that is not ProcessMode.Always, so
        // without this the pause key froze the camera, selection and orders as
        // well as the clock, and the map became completely inert.
        ProcessMode = ProcessModeEnum.Always;
        _chopButton = GetNode<Button>("Panel/Margin/Rows/ChopButton");
        _forageButton = GetNode<Button>("Panel/Margin/Rows/ForageButton");
        _status = GetNode<Label>("Panel/Margin/Rows/Status");
        _chopButton.Pressed += () => ToggleMode(WorkMode.Chop);
        _forageButton.Pressed += () => ToggleMode(WorkMode.Forage);
        _jobs = GetTree().GetFirstNodeInGroup(SettlementJobSystem.GroupName) as SettlementJobSystem
            ?? GetNode<SettlementJobSystem>("../SettlementJobSystem");
        _selection = GetNode<SurvivorSelectionController>("../SelectionController");
    }

    public override void _Process(double delta)
    {
        if (!IsDesignationActive) return;
        _highlightRefresh -= delta;
        if (_highlightRefresh > 0) return;
        _highlightRefresh = 0.25;
        RefreshHighlights();
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (!IsDesignationActive) return;
        if (inputEvent is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
        { EndDesignation(); GetViewport().SetInputAsHandled(); }
        else if (inputEvent is InputEventMouseButton mouse && mouse.Pressed)
        {
            if (mouse.ButtonIndex == MouseButton.Right) EndDesignation();
            else if (mouse.ButtonIndex == MouseButton.Left) ToggleResourceAt(mouse.Position);
            GetViewport().SetInputAsHandled();
        }
    }

    public void ToggleDesignation() => ToggleMode(WorkMode.Chop);
    public void ToggleForageDesignation() => ToggleMode(WorkMode.Forage);
    public void ToggleScavengeDesignation() => ToggleMode(WorkMode.Scavenge);
    public void ToggleHaulDesignation() => ToggleMode(WorkMode.Haul);

    private void ToggleMode(WorkMode mode)
    {
        if (IsDesignationActive && ActiveMode == mode)
        {
            EndDesignation();
            return;
        }

        ClearModeState();
        ActiveMode = mode;
        DesignatedResourceType = mode switch
        {
            WorkMode.Chop => ResourceType.Wood,
            WorkMode.Forage => ResourceType.Food,
            _ => ResourceType.Materials
        };
        UpdateInstruction();
        RefreshLegacyButtons();
        RefreshHighlights();
        StartAutomaticWork();
    }

    public void EndDesignation()
    {
        ClearModeState();
        ActiveMode = WorkMode.None;
        _scavengeMode = false;
        UpdateInstruction();
        RefreshLegacyButtons();
    }

    public bool ToggleTreeAt(Vector2 screenPosition) => ToggleResourceAt(screenPosition);

    public bool ToggleResourceAt(Vector2 screenPosition)
    {
        if (ActiveMode == WorkMode.Haul)
        {
            HaulableDrop drop = GetTree().GetNodesInGroup(HaulableDrop.GroupName).OfType<HaulableDrop>()
                .Where(item => item.HasItems && item.ContainsScreenPoint(screenPosition))
                .OrderBy(item => item.Position.Y)
                .LastOrDefault();
            if (drop is null) return false;
            drop.SetDesignated(!drop.IsDesignatedForHauling);
            _status.Text = drop.IsDesignatedForHauling ? "Drop designated for hauling" : "Haul designation removed";
            PrioritizeSelected(drop);
            return true;
        }

        if (ActiveMode == WorkMode.Scavenge || _scavengeMode)
        {
            ScavengeSource source = GetTree().GetNodesInGroup(ScavengeSource.GroupName).OfType<ScavengeSource>()
                .Where(item => item.ContainsScreenPoint(screenPosition) && !item.IsDepleted)
                .OrderBy(item => item.Position.Y)
                .LastOrDefault();
            if (source is null) return false;
            source.SetScavengeDesignated(!source.IsDesignatedForScavenging);
            _status.Text = source.IsDesignatedForScavenging ? "Location designated for scavenging" : "Scavenge designation removed";
            PrioritizeSelected(source);
            return true;
        }

        HarvestableResource resource = GetTree().GetNodesInGroup(HarvestableResource.GroupName).OfType<HarvestableResource>()
            .Where(item => item.ResourceType == DesignatedResourceType && item.IsHarvestable && item.ContainsScreenPoint(screenPosition))
            .OrderBy(item => item.Position.Y)
            .LastOrDefault();
        if (resource is null) return false;
        resource.SetHarvestDesignated(!resource.IsDesignatedForHarvest);
        string action = DesignatedResourceType == ResourceType.Wood ? "chopping" : "foraging";
        _status.Text = resource.IsDesignatedForHarvest ? $"Resource designated for {action}" : "Resource designation removed";
        PrioritizeSelected(resource);
        return true;
    }

    private void PrioritizeSelected(GodotObject target)
    {
        if (_selection.SelectedCount == 0 || _jobs is null) return;
        foreach (Survivor survivor in _selection.SelectedSurvivors.Where(s => s.IsAlive))
        {
            _jobs.PrioritizeDesignatedTarget(survivor, target, WorkCategoryFor(ActiveMode));
        }
    }

    private void StartAutomaticWork()
    {
        if (_jobs is null || _selection.SelectedCount == 0) return;
        WorkCategory category = WorkCategoryFor(ActiveMode);
        foreach (Survivor survivor in _selection.SelectedSurvivors.Where(s => s.IsAlive && s.AllowsWork(category)))
        {
            _jobs.SetWorkMandate(survivor, category);
        }
    }

    private void RefreshHighlights()
    {
        switch (ActiveMode)
        {
            case WorkMode.Chop:
                foreach (HarvestableResource resource in GetTree().GetNodesInGroup(HarvestableResource.GroupName).OfType<HarvestableResource>())
                    resource.SetWorkHighlighted(resource.ResourceType == ResourceType.Wood && resource.IsHarvestable);
                break;
            case WorkMode.Forage:
                foreach (HarvestableResource resource in GetTree().GetNodesInGroup(HarvestableResource.GroupName).OfType<HarvestableResource>())
                    resource.SetWorkHighlighted(resource.ResourceType == ResourceType.Food && resource.IsHarvestable);
                break;
            case WorkMode.Scavenge:
                foreach (ScavengeSource source in GetTree().GetNodesInGroup(ScavengeSource.GroupName).OfType<ScavengeSource>())
                    source.SetWorkHighlighted(!source.IsDepleted);
                break;
            case WorkMode.Haul:
                foreach (HaulableDrop drop in GetTree().GetNodesInGroup(HaulableDrop.GroupName).OfType<HaulableDrop>())
                    drop.SetWorkHighlighted(drop.HasItems && !drop.IsClaimed);
                break;
        }
    }

    private void ClearModeState()
    {
        ClearHighlights();
        _jobs?.ClearWorkMandates(WorkCategoryFor(ActiveMode));
    }

    private void ClearHighlights()
    {
        if (GetTree() is null) return;
        foreach (HarvestableResource resource in GetTree().GetNodesInGroup(HarvestableResource.GroupName).OfType<HarvestableResource>())
            resource.SetWorkHighlighted(false);
        foreach (ScavengeSource source in GetTree().GetNodesInGroup(ScavengeSource.GroupName).OfType<ScavengeSource>())
            source.SetWorkHighlighted(false);
        foreach (HaulableDrop drop in GetTree().GetNodesInGroup(HaulableDrop.GroupName).OfType<HaulableDrop>())
            drop.SetWorkHighlighted(false);
    }

    private void UpdateInstruction()
    {
        InstructionText = ActiveMode switch
        {
            WorkMode.Chop => "Valid trees highlighted • Click a tree to designate • Selected survivors chop automatically",
            WorkMode.Forage => "Valid forage highlighted • Click to designate • Selected survivors forage automatically",
            WorkMode.Scavenge => "Valid salvage highlighted • Click to designate • Selected survivors scavenge automatically",
            WorkMode.Haul => "Loose salvage highlighted • Click to designate • Selected survivors haul automatically",
            _ => "Choose a work action, then mark targets in the world."
        };
        _status.Text = IsDesignationActive
            ? InstructionText
            : "Designate settlement work";
    }

    private void RefreshLegacyButtons()
    {
        _chopButton.ButtonPressed = IsDesignationActive && ActiveMode == WorkMode.Chop;
        _forageButton.ButtonPressed = IsDesignationActive && ActiveMode == WorkMode.Forage;
    }

    private static WorkCategory WorkCategoryFor(WorkMode mode) => mode switch
    {
        WorkMode.Chop => WorkCategory.Woodcutting,
        WorkMode.Forage => WorkCategory.Foraging,
        WorkMode.Scavenge => WorkCategory.Scavenging,
        WorkMode.Haul => WorkCategory.Hauling,
        _ => WorkCategory.Woodcutting
    };
}
