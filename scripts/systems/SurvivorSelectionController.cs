using System.Collections.Generic;
using System.Linq;
using AshwoodCounty.Units;
using AshwoodCounty.World;
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
    private Vector2 _dragStart;
    private bool _leftPressed;
    private bool _isBoxSelecting;

    public int SelectedCount => _selectedSurvivors.Count;
    public IReadOnlyList<Survivor> SelectedSurvivors => _selectedSurvivors;

    public override void _Ready()
    {
        _world = GetNode<IsometricWorld>("../World");
        _effects = GetNode<Node2D>("../World/Effects");
        _selectionMarquee = GetNode<Control>("SelectionMarquee");
        _selectionMarquee.Visible = false;
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
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
            IssueMoveOrder(mouseButton.Position);
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

    private void IssueMoveOrder(Vector2 screenPosition)
    {
        if (_selectedSurvivors.Count == 0)
        {
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

    private static Rect2 MakeScreenRect(Vector2 start, Vector2 end)
    {
        Vector2 position = new(Mathf.Min(start.X, end.X), Mathf.Min(start.Y, end.Y));
        Vector2 size = new(Mathf.Abs(end.X - start.X), Mathf.Abs(end.Y - start.Y));
        return new Rect2(position, size);
    }
}
