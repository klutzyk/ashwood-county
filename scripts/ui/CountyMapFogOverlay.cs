using AshwoodCounty.World.Fog;
using Godot;

namespace AshwoodCounty.UI;

/// <summary>
/// A single filtered strategic fog texture. It refreshes only while the county
/// map is open and only after the gameplay fog reports a visibility change.
/// </summary>
public partial class CountyMapFogOverlay : Control
{
    private const int MaskWidth = 96;
    private const int MaskHeight = 80;
    private const double RefreshIntervalSeconds = 0.25;

    public CountyFogOfWar Fog { get; set; } = null!;

    private Image _mask = null!;
    private ImageTexture _texture = null!;
    private bool _dirty = true;
    private double _untilRefresh;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        TextureFilter = TextureFilterEnum.Linear;
        _mask = Image.CreateEmpty(MaskWidth, MaskHeight, false, Image.Format.Rgba8);
        Fog.FogChanged += OnFogChanged;
        RefreshMask();
        SetProcess(false);
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(Fog))
            Fog.FogChanged -= OnFogChanged;
    }

    public override void _Process(double delta)
    {
        if (!_dirty)
            return;

        _untilRefresh -= delta;
        if (_untilRefresh > 0)
            return;

        RefreshMask();
    }

    public override void _Draw()
    {
        if (_texture is not null)
            DrawTextureRect(_texture, new Rect2(Vector2.Zero, Size), false);
    }

    public void SetMapOpen(bool open)
    {
        SetProcess(open);
        if (!open)
            return;

        _dirty = true;
        _untilRefresh = 0;
        RefreshMask();
    }

    private void OnFogChanged(int exploredCellCount, int visibleCellCount)
    {
        _dirty = true;
    }

    private void RefreshMask()
    {
        if (Fog is null || _mask is null)
            return;

        Vector2 origin = Fog.CountyOrigin;
        Vector2 countySize = Fog.CountySize;
        for (int y = 0; y < MaskHeight; y++)
        {
            for (int x = 0; x < MaskWidth; x++)
            {
                Vector2 countyPosition = origin + new Vector2(
                    (x + 0.5f) / MaskWidth * countySize.X,
                    (y + 0.5f) / MaskHeight * countySize.Y);
                FogCellVisibility state = Fog.GetVisibilityAt(countyPosition);
                float alpha = state switch
                {
                    FogCellVisibility.Visible => 0.01f,
                    FogCellVisibility.Explored => 0.18f,
                    _ => 0.54f
                };
                _mask.SetPixel(x, y, new Color(0.025f, 0.032f, 0.024f, alpha));
            }
        }

        if (_texture is null)
            _texture = ImageTexture.CreateFromImage(_mask);
        else
            _texture.Update(_mask);

        _dirty = false;
        _untilRefresh = RefreshIntervalSeconds;
        QueueRedraw();
    }
}
