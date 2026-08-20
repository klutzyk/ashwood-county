using System.Collections.Generic;
using System.Linq;
using AshwoodCounty.Buildings;
using AshwoodCounty.Resources;
using AshwoodCounty.Units;
using AshwoodCounty.World;
using AshwoodCounty.Threats;
using AshwoodCounty.Buildings.Interiors;
using AshwoodCounty.UI;
using Godot;

namespace AshwoodCounty.Systems;

public partial class SurvivorSelectionController : CanvasLayer
{
    private const float DragThreshold = 6.0f;
    private const float FormationSpacing = 0.9f;

    private readonly List<Survivor> _selectedSurvivors = [];
    private IsometricWorld _world = null!;
    private Control _selectionMarquee = null!;
    private Node2D _effects = null!;
    private Stockpile _stockpile = null!;
    private BuildingPlacementController _buildingPlacement = null!;
    private ChopDesignationController _chopDesignation = null!;
    private Vector2 _dragStart;
    private bool _leftPressed;
    private bool _isBoxSelecting;

    public int SelectedCount => _selectedSurvivors.Count;
    public IReadOnlyList<Survivor> SelectedSurvivors => _selectedSurvivors;

    /// <summary>Test-only selection hook for the ASHWOOD_VALIDATE_* automated validation scripts; not used by normal input handling.</summary>
    internal void DebugSelectOnly(Survivor survivor)
    {
        foreach (Survivor previous in _selectedSurvivors) previous.SetSelected(false);
        _selectedSurvivors.Clear();
        SetSurvivorSelected(survivor, true);
    }

    /// <summary>Test-only additive selection hook for the ASHWOOD_VALIDATE_* automated validation scripts; not used by normal input handling.</summary>
    internal void DebugSelect(Survivor survivor)
    {
        SetSurvivorSelected(survivor, true);
    }

    public override void _Ready()
    {
        // Pausing stops the simulation, not the player. GetTree().Paused halts
        // _Process and input for every node that is not ProcessMode.Always, so
        // without this the pause key froze the camera, selection and orders as
        // well as the clock, and the map became completely inert.
        ProcessMode = ProcessModeEnum.Always;
        _world = GetNode<IsometricWorld>("../World");
        _effects = GetNode<Node2D>("../World/Effects");
        _stockpile = GetNode<Stockpile>("../World/Objects/Stockpile");
        _buildingPlacement = GetNode<BuildingPlacementController>("../BuildingPlacementController");
        _chopDesignation = GetNode<ChopDesignationController>("../ChopDesignationController");
        _selectionMarquee = GetNode<Control>("SelectionMarquee");
        _selectionMarquee.Visible = false;
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (_buildingPlacement.IsPlacementActive || _chopDesignation.IsDesignationActive)
        {
            return;
        }

        if (inputEvent is InputEventMouseButton mouseButton)
        {
            HandleMouseButton(mouseButton);
            return;
        }

        if (inputEvent is InputEventMouseMotion mouseMotion && _leftPressed)
        {
            UpdateBoxSelection(mouseMotion.Position);
        }
    }

    private void HandleMouseButton(InputEventMouseButton mouseButton)
    {
        if (mouseButton.ButtonIndex == MouseButton.Left)
        {
            if (mouseButton.Pressed)
            {
                _leftPressed = true;
                _isBoxSelecting = false;
                _dragStart = mouseButton.Position;
            }
            else if (_leftPressed)
            {
                _leftPressed = false;
                if (_isBoxSelecting)
                {
                    ApplyBoxSelection(MakeScreenRect(_dragStart, mouseButton.Position), mouseButton.ShiftPressed);
                }
                else
                {
                    ApplyClickSelection(mouseButton.Position, mouseButton.ShiftPressed);
                }

                _isBoxSelecting = false;
                _selectionMarquee.Visible = false;
            }

            GetViewport().SetInputAsHandled();
        }
        else if (mouseButton.ButtonIndex == MouseButton.Right && mouseButton.Pressed)
        {
            IssueContextOrder(mouseButton.Position, mouseButton.ShiftPressed);
            GetViewport().SetInputAsHandled();
        }
    }

    private void UpdateBoxSelection(Vector2 currentPosition)
    {
        if (!_isBoxSelecting && _dragStart.DistanceSquaredTo(currentPosition) < DragThreshold * DragThreshold)
        {
            return;
        }

        _isBoxSelecting = true;
        Rect2 rectangle = MakeScreenRect(_dragStart, currentPosition);
        _selectionMarquee.Position = rectangle.Position;
        _selectionMarquee.Size = rectangle.Size;
        _selectionMarquee.Visible = true;
    }

    private void ApplyClickSelection(Vector2 screenPosition, bool shiftPressed)
    {
        Survivor hit = GetSurvivors()
            .Where(survivor => survivor.ContainsScreenPoint(screenPosition))
            .OrderBy(survivor => survivor.Position.Y)
            .LastOrDefault();

        if (hit is null)
        {
            if (!shiftPressed)
            {
                ClearSelection();
            }

            return;
        }

        if (shiftPressed)
        {
            SetSurvivorSelected(hit, !hit.IsSelected);
        }
        else
        {
            ClearSelection();
            SetSurvivorSelected(hit, true);
        }
    }

    private void ApplyBoxSelection(Rect2 screenRectangle, bool shiftPressed)
    {
        List<Survivor> inside = GetSurvivors()
            .Where(survivor => screenRectangle.Intersects(survivor.GetScreenSelectionBounds(), true))
            .ToList();

        if (!shiftPressed)
        {
            ClearSelection();
        }

        foreach (Survivor survivor in inside)
        {
            if (shiftPressed && survivor.IsSelected)
            {
                continue;
            }

            SetSurvivorSelected(survivor, true);
        }
    }

    private void IssueContextOrder(Vector2 screenPosition, bool shiftPressed)
    {
        Zombie zombie=GetTree().GetNodesInGroup(Zombie.GroupName).OfType<Zombie>().Where(z=>z.IsAlive&&z.ContainsScreenPoint(screenPosition)).OrderBy(z=>z.Position.Y).LastOrDefault();
        if(zombie is not null&&_selectedSurvivors.Count>0){foreach(Survivor survivor in _selectedSurvivors.Where(s=>s.IsAlive))survivor.IssueAttackOrder(zombie);return;}
        ConstructionSite constructionSite = GetConstructionSites()
            .Where(site => site.IsAvailableForBuilding && site.ContainsScreenPoint(screenPosition))
            .OrderBy(site => site.Position.Y)
            .LastOrDefault();
        if (constructionSite is not null)
        {
            if (shiftPressed)
            {
                if (constructionSite.CancelConstruction())
                {
                    _buildingPlacement.ShowStatus("Construction cancelled • 30 Wood refunded");
                }
            }
            else if (_selectedSurvivors.Count > 0)
            {
                IssueBuildOrder(constructionSite);
            }

            return;
        }

        InteriorContainerRuntime container = GetTree().GetNodesInGroup(InteriorContainerRuntime.GroupName)
            .OfType<InteriorContainerRuntime>().Where(item => item.Visible && item.ContainsScreenPoint(screenPosition))
            .OrderBy(item => item.Position.Y).LastOrDefault();
        if (container is not null)
        {
            if (container.IsSearched)
            {
                Notify($"{container.DisplayName.ToUpperInvariant()}\nAlready searched");
            }
            else if (container.IsClaimed)
            {
                Notify($"{container.DisplayName.ToUpperInvariant()}\nAlready being searched");
            }
            else
            {
                Survivor searcher = _selectedSurvivors.Where(s => s.IsAlive)
                    .MinBy(s => s.SimulationPosition.DistanceSquaredTo(container.InteractionPosition))!;
                if (searcher is null) Notify("SELECT A SURVIVOR\nRight-click a container to search it");
                else if (!searcher.IsInsideInterior(container.Building))
                    Notify($"{container.Building.Definition.DisplayName.ToUpperInvariant()}\nEnter the building first");
                else searcher.IssueSearchContainerOrder(container);
            }
            return;
        }

        ScavengeSource scavengeTarget = GetScavengeSources()
            .Where(source => source.Visible && source.ContainsScreenPoint(screenPosition))
            .OrderBy(source => source.Position.Y)
            .LastOrDefault();
        if (scavengeTarget is not null)
        {
            if (scavengeTarget.IsDepleted)
            {
                Notify($"{HumanizeName(scavengeTarget.ResolvedDisplayName).ToUpperInvariant()}\nAlready cleared");
            }
            else if (_selectedSurvivors.Count == 0)
            {
                Notify("SELECT A SURVIVOR\nRight-click salvage to search it");
            }
            else if (scavengeTarget.IsClaimed)
            {
                Notify($"{HumanizeName(scavengeTarget.ResolvedDisplayName).ToUpperInvariant()}\nAlready being searched");
            }
            else
            {
                if (!scavengeTarget.IsDesignatedForScavenging) scavengeTarget.SetScavengeDesignated(true);
                Survivor searcher = _selectedSurvivors.Where(s => s.IsAlive)
                    .MinBy(s => s.SimulationPosition.DistanceSquaredTo(scavengeTarget.WorldPosition))!;
                if (searcher is not null)
                    searcher.IssueScavengeOrder(scavengeTarget, _stockpile, scavengeTarget.GetInteractionPosition(), _stockpile.GetInteractionPosition(0, 1));
            }
            return;
        }

        if (_selectedSurvivors.Count == 0)
        {
            return;
        }

        InteriorBedRuntime bed = GetTree().GetNodesInGroup(InteriorBedRuntime.GroupName)
            .OfType<InteriorBedRuntime>().Where(item => item.Visible && item.ContainsScreenPoint(screenPosition))
            .OrderBy(item => item.Position.Y).LastOrDefault();
        if (bed is not null)
        {
            Survivor sleeper = _selectedSurvivors.Where(s => s.IsAlive).MinBy(s => s.SimulationPosition.DistanceSquaredTo(bed.InteractionPosition))!;
            if (sleeper is null) Notify("SELECT A SURVIVOR\nRight-click a bed to rest in it");
            else if (!sleeper.IsInsideInterior(bed.Building))
                Notify($"{bed.Building.Definition.DisplayName.ToUpperInvariant()}\nEnter the building first");
            else sleeper.IssueBedRestOrder(bed);
            return;
        }

        InteriorDoorRuntime door = GetTree().GetNodesInGroup(InteriorDoorRuntime.GroupName)
            .OfType<InteriorDoorRuntime>().Where(item => item.Visible && item.ContainsScreenPoint(screenPosition))
            .OrderBy(item => item.Position.Y).LastOrDefault();
        if (door is not null)
        {
            Survivor user = _selectedSurvivors.Where(s => s.IsAlive).MinBy(s => s.SimulationPosition.DistanceSquaredTo(door.InteractionPosition))!;
            if (user is null) Notify("SELECT A SURVIVOR\nRight-click a door to use it");
            else if (!door.IsExterior && !user.IsInsideInterior(door.Building))
                Notify($"{door.Building.Definition.DisplayName.ToUpperInvariant()}\nEnter the building first");
            else user.IssueDoorOrder(door);
            return;
        }

        HarvestableResource harvestTarget = GetHarvestableResources()
            .Where(resource => resource.IsHarvestable && resource.ContainsScreenPoint(screenPosition))
            .OrderBy(resource => resource.Position.Y)
            .LastOrDefault();
        if (harvestTarget is not null)
        {
            IssueHarvestOrder(harvestTarget);
            return;
        }

        InteriorBuildingRuntime interiorBuilding = GetTree().GetNodesInGroup(InteriorBuildingRuntime.GroupName)
            .OfType<InteriorBuildingRuntime>().Where(item => item.ContainsScreenPoint(screenPosition))
            .OrderBy(item => item.ScreenSortDepth).LastOrDefault();
        if (interiorBuilding is not null)
        {
            if (_selectedSurvivors.Count == 0) Notify("SELECT A SURVIVOR\nRight-click a building to enter it");
            else
            {
                Survivor entrant = _selectedSurvivors.Where(s => s.IsAlive)
                    .MinBy(s => s.SimulationPosition.DistanceSquaredTo(interiorBuilding.Definition.Footprint.GetCenter()))!;
                if (entrant is not null) entrant.IssueEnterBuildingOrder(interiorBuilding);
            }
            return;
        }

        CompletedBuilding completedBuilding = GetTree().GetNodesInGroup(CompletedBuilding.GroupName)
            .OfType<CompletedBuilding>().Where(item => item.ContainsScreenPoint(screenPosition))
            .OrderBy(item => item.Position.Y).LastOrDefault();
        if (completedBuilding is not null && _selectedSurvivors.Count > 0)
        {
            Vector2 approach = new(completedBuilding.OccupancyFootprint.Center.X, completedBuilding.OccupancyFootprint.Bounds.End.Y + 1.1f);
            _selectedSurvivors.Where(s => s.IsAlive).MinBy(s => s.SimulationPosition.DistanceSquaredTo(approach))?.IssueMoveOrder(approach);
            return;
        }

        Vector2 target = _world.ScreenToGridPosition(screenPosition);
        if (!IsometricWorld.IsGridPositionInBounds(target))
        {
            return;
        }

        List<Vector2> destinations = CreateFormationDestinations(target, _selectedSurvivors.Count);
        List<Survivor> unassigned = [.. _selectedSurvivors];
        foreach (Vector2 destination in destinations)
        {
            Survivor nearest = unassigned.MinBy(survivor => survivor.SimulationPosition.DistanceSquaredTo(destination))!;
            nearest.IssueMoveOrder(destination);
            unassigned.Remove(nearest);
        }

        MoveCommandMarker marker = new();
        _effects.AddChild(marker);
        marker.Initialize(target);
    }

    private void IssueHarvestOrder(HarvestableResource target)
    {
        int workerCount = _selectedSurvivors.Count;
        for (int index = 0; index < workerCount; index++)
        {
            Survivor survivor = _selectedSurvivors[index];
            Vector2 interactionPosition = target.GetInteractionPosition(index, workerCount);
            Vector2 deliveryPosition = _stockpile.GetInteractionPosition(index, workerCount);
            survivor.IssueHarvestOrder(target, _stockpile, interactionPosition, deliveryPosition);
        }
    }

    private void IssueBuildOrder(ConstructionSite target)
    {
        int workerCount = _selectedSurvivors.Count;
        for (int index = 0; index < workerCount; index++)
        {
            Survivor survivor = _selectedSurvivors[index];
            survivor.IssueBuildOrder(target, target.GetInteractionPosition(index, workerCount));
        }
    }

    private static List<Vector2> CreateFormationDestinations(Vector2 center, int count)
    {
        int columns = Mathf.CeilToInt(Mathf.Sqrt(count));
        int rows = Mathf.CeilToInt((float)count / columns);
        float halfWidth = (Mathf.Min(columns, count) - 1) * FormationSpacing * 0.5f;
        float halfHeight = (rows - 1) * FormationSpacing * 0.5f;
        center.X = Mathf.Clamp(center.X, 0.25f + halfWidth, IsometricWorld.MapWidth - 0.25f - halfWidth);
        center.Y = Mathf.Clamp(center.Y, 0.25f + halfHeight, IsometricWorld.MapHeight - 0.25f - halfHeight);
        List<Vector2> destinations = new(count);

        for (int index = 0; index < count; index++)
        {
            int column = index % columns;
            int row = index / columns;
            int itemsInRow = Mathf.Min(columns, count - row * columns);
            float offsetX = (column - (itemsInRow - 1) * 0.5f) * FormationSpacing;
            float offsetY = (row - (rows - 1) * 0.5f) * FormationSpacing;
            destinations.Add(center + new Vector2(offsetX, offsetY));
        }

        return destinations;
    }

    private void SetSurvivorSelected(Survivor survivor, bool selected)
    {
        survivor.SetSelected(selected);
        if (selected)
        {
            if (!_selectedSurvivors.Contains(survivor))
            {
                _selectedSurvivors.Add(survivor);
            }
        }
        else
        {
            _selectedSurvivors.Remove(survivor);
        }
    }

    private void ClearSelection()
    {
        foreach (Survivor survivor in _selectedSurvivors)
        {
            survivor.SetSelected(false);
        }

        _selectedSurvivors.Clear();
    }

    private IEnumerable<Survivor> GetSurvivors()
    {
        foreach (Node node in GetTree().GetNodesInGroup(Survivor.GroupName))
        {
            if (node is Survivor survivor)
            {
                yield return survivor;
            }
        }
    }

    private IEnumerable<HarvestableResource> GetHarvestableResources()
    {
        foreach (Node node in GetTree().GetNodesInGroup(HarvestableResource.GroupName))
        {
            if (node is HarvestableResource resource)
            {
                yield return resource;
            }
        }
    }

    private IEnumerable<ConstructionSite> GetConstructionSites()
    {
        foreach (Node node in GetTree().GetNodesInGroup(ConstructionSite.GroupName))
        {
            if (node is ConstructionSite site)
            {
                yield return site;
            }
        }
    }

    private IEnumerable<ScavengeSource> GetScavengeSources()
    {
        foreach (Node node in GetTree().GetNodesInGroup(ScavengeSource.GroupName))
        {
            if (node is ScavengeSource source)
            {
                yield return source;
            }
        }
    }

    private void Notify(string message) => (GetTree().GetFirstNodeInGroup(GameHud.GroupName) as GameHud)?.Notify(message);

    private static string HumanizeName(string name) => System.Text.RegularExpressions.Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");

    private static Rect2 MakeScreenRect(Vector2 start, Vector2 end)
    {
        Vector2 position = new(Mathf.Min(start.X, end.X), Mathf.Min(start.Y, end.Y));
        Vector2 size = new(Mathf.Abs(end.X - start.X), Mathf.Abs(end.Y - start.Y));
        return new Rect2(position, size);
    }
}
