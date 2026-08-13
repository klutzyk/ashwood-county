using Godot;
using AshwoodCounty.World;

namespace AshwoodCounty.Camera;

public partial class StrategyCamera : Camera2D
{
    [Export] public float MoveSpeed { get; set; } = 700.0f;
    [Export] public float MoveSmoothing { get; set; } = 12.0f;
    [Export] public float ZoomSmoothing { get; set; } = 14.0f;
    [Export] public float MinZoom { get; set; } = 0.16f;
    [Export] public float MaxZoom { get; set; } = 1.75f;
    [Export] public float ZoomStep { get; set; } = 0.15f;
    [Export] public float BoundsPadding { get; set; } = 220.0f;

    private Vector2 _targetPosition;
    private float _targetZoom = 1.0f;
    private bool _dragging;
    private Rect2 _mapBounds;

    public override void _Ready()
    {
        Position = Vector2.Zero;
        _targetPosition = Position;
        _targetZoom = Zoom.X;
    }

    public void ConfigureBounds(Rect2 bounds)
    {
        _mapBounds = bounds;
        _targetPosition = IsometricGrid.GridToScreen(new Vector2(203, 157));
        Position = _targetPosition;
    }

    public void CenterOnGridPosition(Vector2 gridPosition)
    {
        _targetPosition = IsometricGrid.GridToScreen(gridPosition);
        ClampTargetPosition();
    }

    public void SetZoom(float zoom)
    {
        _targetZoom = Mathf.Clamp(zoom, MinZoom, MaxZoom);
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (inputEvent is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.Middle)
            {
                _dragging = mouseButton.Pressed;
                GetViewport().SetInputAsHandled();
            }
            else if (mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.WheelUp)
            {
                _targetZoom = Mathf.Clamp(_targetZoom + ZoomStep, MinZoom, MaxZoom);
                GetViewport().SetInputAsHandled();
            }
            else if (mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.WheelDown)
            {
                _targetZoom = Mathf.Clamp(_targetZoom - ZoomStep, MinZoom, MaxZoom);
                GetViewport().SetInputAsHandled();
            }
        }
        else if (_dragging && inputEvent is InputEventMouseMotion mouseMotion)
        {
            _targetPosition -= mouseMotion.Relative / Zoom.X;
            ClampTargetPosition();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        Vector2 input = Input.GetVector("camera_left", "camera_right", "camera_up", "camera_down");
        if (!input.IsZeroApprox())
        {
            float zoomAdjustedSpeed = MoveSpeed / Mathf.Max(_targetZoom, 0.01f);
            _targetPosition += input * zoomAdjustedSpeed * (float)delta;
            ClampTargetPosition();
        }

        float moveWeight = 1.0f - Mathf.Exp(-MoveSmoothing * (float)delta);
        float zoomWeight = 1.0f - Mathf.Exp(-ZoomSmoothing * (float)delta);
        Position = Position.Lerp(_targetPosition, moveWeight);
        float smoothedZoom = Mathf.Lerp(Zoom.X, _targetZoom, zoomWeight);
        Zoom = Vector2.One * smoothedZoom;
    }

    private void ClampTargetPosition()
    {
        if (_mapBounds.Size.IsZeroApprox())
        {
            return;
        }

        Vector2 viewportWorldSize = GetViewportRect().Size / Mathf.Max(_targetZoom, 0.01f);
        Vector2 halfViewport = viewportWorldSize * 0.5f;
        float minX = _mapBounds.Position.X - BoundsPadding + halfViewport.X;
        float maxX = _mapBounds.End.X + BoundsPadding - halfViewport.X;
        float minY = _mapBounds.Position.Y - BoundsPadding + halfViewport.Y;
        float maxY = _mapBounds.End.Y + BoundsPadding - halfViewport.Y;

        _targetPosition.X = minX <= maxX ? Mathf.Clamp(_targetPosition.X, minX, maxX) : _mapBounds.GetCenter().X;
        _targetPosition.Y = minY <= maxY ? Mathf.Clamp(_targetPosition.Y, minY, maxY) : _mapBounds.GetCenter().Y;
    }
}
