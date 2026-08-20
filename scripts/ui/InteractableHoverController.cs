#nullable enable

using System.Linq;
using AshwoodCounty.Buildings;
using AshwoodCounty.Buildings.Interiors;
using AshwoodCounty.Resources;
using Godot;

namespace AshwoodCounty.UI;

/// <summary>
/// Restrained contextual hover presentation for actually interactable world
/// objects. While the pointer is over an eligible target it strengthens the
/// object's own highlight and shows a hand cursor. Interior containers, beds
/// and interior doors are only hoverable while a survivor is inside their
/// building; exterior doors and world objects are always eligible. Nothing
/// decorative ever glows.
/// </summary>
public partial class InteractableHoverController : CanvasLayer
{
    private const double RefreshInterval = 0.08;
    private GodotObject? _hovered;
    private bool _cursorHand;
    private double _refresh;

    public override void _Ready()
    {
        Layer = 12;
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Process(double delta)
    {
        _refresh -= delta;
        if (_refresh > 0) return;
        _refresh = RefreshInterval;
        RefreshHover();
    }

    private void RefreshHover()
    {
        bool pointerOverUi = IsPointerOverUi();
        GodotObject? hit = pointerOverUi ? null : HitTestInteractable(GetViewport().GetMousePosition());
        SetHovered(hit);
        SetCursor(hit is not null);
    }

    private GodotObject? HitTestInteractable(Vector2 screenPosition)
    {
        GodotObject? best = null;
        float bestDepth = float.MinValue;

        foreach (InteriorContainerRuntime container in GetTree().GetNodesInGroup(InteriorContainerRuntime.GroupName).OfType<InteriorContainerRuntime>())
        {
            if (container.Visible && container.Building.HasSurvivorInside && container.ContainsScreenPoint(screenPosition))
                Consider(container, container.Position.Y);
        }

        foreach (InteriorBedRuntime bed in GetTree().GetNodesInGroup(InteriorBedRuntime.GroupName).OfType<InteriorBedRuntime>())
        {
            if (bed.Visible && bed.Building.HasSurvivorInside && bed.ContainsScreenPoint(screenPosition))
                Consider(bed, bed.Position.Y);
        }

        foreach (InteriorDoorRuntime door in GetTree().GetNodesInGroup(InteriorDoorRuntime.GroupName).OfType<InteriorDoorRuntime>())
        {
            bool eligible = door.IsExterior || door.Building.HasSurvivorInside;
            if (door.Visible && eligible && door.ContainsScreenPoint(screenPosition))
                Consider(door, door.Position.Y);
        }

        foreach (InteriorBuildingRuntime building in GetTree().GetNodesInGroup(InteriorBuildingRuntime.GroupName).OfType<InteriorBuildingRuntime>())
        {
            if (building.ContainsScreenPoint(screenPosition))
                Consider(building, building.ScreenSortDepth);
        }

        foreach (CompletedBuilding building in GetTree().GetNodesInGroup(CompletedBuilding.GroupName).OfType<CompletedBuilding>())
        {
            if (building.ContainsScreenPoint(screenPosition))
                Consider(building, building.Position.Y);
        }

        foreach (HarvestableResource resource in GetTree().GetNodesInGroup(HarvestableResource.GroupName).OfType<HarvestableResource>())
        {
            if (resource.IsHarvestable && resource.ContainsScreenPoint(screenPosition))
                Consider(resource, resource.Position.Y);
        }

        foreach (ScavengeSource source in GetTree().GetNodesInGroup(ScavengeSource.GroupName).OfType<ScavengeSource>())
        {
            if (source.Visible && !source.IsDepleted && source.ContainsScreenPoint(screenPosition))
                Consider(source, source.Position.Y);
        }

        foreach (HaulableDrop drop in GetTree().GetNodesInGroup(HaulableDrop.GroupName).OfType<HaulableDrop>())
        {
            if (drop.HasItems && drop.ContainsScreenPoint(screenPosition))
                Consider(drop, drop.Position.Y);
        }

        return best;

        void Consider(GodotObject candidate, float depth)
        {
            if (depth <= bestDepth) return;
            best = candidate;
            bestDepth = depth;
        }
    }

    private void SetHovered(GodotObject? target)
    {
        if (_hovered == target) return;
        if (_hovered is not null && GodotObject.IsInstanceValid(_hovered))
        {
            switch (_hovered)
            {
                case InteriorContainerRuntime container: container.IsHovered = false; break;
                case ScavengeSource source: source.SetHovered(false); break;
                case HarvestableResource resource: resource.SetHovered(false); break;
                case HaulableDrop drop: drop.SetHovered(false); break;
                case InteriorBuildingRuntime building: building.SetHovered(false); break;
                case CompletedBuilding building: building.SetHovered(false); break;
                case InteriorBedRuntime bed: bed.SetHovered(false); break;
                case InteriorDoorRuntime door: door.SetHovered(false); break;
            }
        }

        _hovered = target;
        if (target is not null)
        {
            switch (target)
            {
                case InteriorContainerRuntime container: container.IsHovered = true; break;
                case ScavengeSource source: source.SetHovered(true); break;
                case HarvestableResource resource: resource.SetHovered(true); break;
                case HaulableDrop drop: drop.SetHovered(true); break;
                case InteriorBuildingRuntime building: building.SetHovered(true); break;
                case CompletedBuilding building: building.SetHovered(true); break;
                case InteriorBedRuntime bed: bed.SetHovered(true); break;
                case InteriorDoorRuntime door: door.SetHovered(true); break;
            }
        }
    }

    private void SetCursor(bool hand)
    {
        if (_cursorHand == hand) return;
        _cursorHand = hand;
        Input.SetDefaultCursorShape(hand ? Input.CursorShape.PointingHand : Input.CursorShape.Arrow);
    }

    private bool IsPointerOverUi()
    {
        return GetViewport().GuiGetHoveredControl() is Control control
            && control.MouseFilter != Control.MouseFilterEnum.Ignore;
    }
}
