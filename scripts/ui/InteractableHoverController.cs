#nullable enable

using System.Linq;
using AshwoodCounty.Buildings.Interiors;
using AshwoodCounty.Resources;
using Godot;

namespace AshwoodCounty.UI;

/// <summary>
/// Restrained contextual hover presentation for searchable objects. While the
/// pointer is over a searchable container or scavenge source it strengthens the
/// object's own glow and shows a hand cursor. When the player is not
/// interacting, nothing extra is shown.
/// </summary>
public partial class InteractableHoverController : CanvasLayer
{
    private const double RefreshInterval = 0.08;
    private Node2D? _hovered;
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
        Node2D? hit = pointerOverUi ? null : HitTestSearchable(GetViewport().GetMousePosition());
        SetHovered(hit);
        SetCursor(hit is not null);
    }

    private Node2D? HitTestSearchable(Vector2 screenPosition)
    {
        Node2D? container = GetTree().GetNodesInGroup(InteriorContainerRuntime.GroupName)
            .OfType<InteriorContainerRuntime>()
            .Where(item => item.Visible && item.ContainsScreenPoint(screenPosition))
            .OrderBy(item => item.Position.Y)
            .LastOrDefault();
        Node2D? source = GetTree().GetNodesInGroup(ScavengeSource.GroupName)
            .OfType<ScavengeSource>()
            .Where(item => item.Visible && item.ContainsScreenPoint(screenPosition))
            .OrderBy(item => item.Position.Y)
            .LastOrDefault();

        float containerDepth = container is null ? float.MinValue : container.Position.Y;
        float sourceDepth = source is null ? float.MinValue : source.Position.Y;
        return containerDepth >= sourceDepth ? container : source;
    }

    private void SetHovered(Node2D? target)
    {
        if (_hovered == target) return;
        if (_hovered is not null && GodotObject.IsInstanceValid(_hovered))
        {
            switch (_hovered)
            {
                case InteriorContainerRuntime container: container.IsHovered = false; break;
                case ScavengeSource source: source.SetHovered(false); break;
            }
        }
        _hovered = target;
        if (target is not null)
        {
            switch (target)
            {
                case InteriorContainerRuntime container: container.IsHovered = true; break;
                case ScavengeSource source: source.SetHovered(true); break;
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
