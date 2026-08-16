#nullable enable

using System.Collections.Generic;
using Godot;

namespace AshwoodCounty.World.County.Visual;

/// <summary>
/// Developer overlay for tracing world artwork back to its source asset.
///
/// The landscape is drawn as retained canvas commands rather than scene nodes,
/// so there is nothing to click on and no inspector to open. Instead, the two
/// draw funnels record what they placed while this is enabled, and the overlay
/// reports whichever sprite is under the cursor: its resource path, its native
/// pixel size, the size it is actually being drawn at, and the resulting scale.
///
/// The scale is the number that matters. Every source image in the library
/// carries genuine detail at its own resolution, so artwork only looks soft
/// when it is being enlarged past that. Anything reported above 1.00 is being
/// stretched and is a candidate for a smaller slot or for removal from the
/// palette.
///
/// Toggle with F9. Recording is off by default and costs nothing when off.
/// </summary>
public partial class AssetInspector : CanvasLayer
{
    public readonly record struct Sample(
        string Texture, Vector2 GridPosition, Vector2 Native, Vector2 Drawn, bool Ground);

    /// <summary>Guards the record calls in the draw funnels.</summary>
    public static bool Capturing { get; private set; }

    private static readonly List<Sample> Samples = [];

    /// <summary>Bound so a long session cannot grow this without limit.</summary>
    private const int SampleLimit = 60000;

    public static void Record(string texture, Vector2 gridPosition, Vector2 native, Vector2 drawn, bool ground)
    {
        if (!Capturing || Samples.Count >= SampleLimit)
            return;
        Samples.Add(new Sample(texture, gridPosition, native, drawn, ground));
    }

    /// <summary>Turn recording on without the overlay, for automated auditing.</summary>
    public static void BeginAudit()
    {
        Capturing = true;
        Samples.Clear();
    }

    /// <summary>
    /// Print every distinct texture on screen with the largest scale it is
    /// drawn at. Anything above 1.0 is being enlarged past its source pixels,
    /// which is the only thing that makes this library's artwork look soft.
    /// </summary>
    public static void ReportScaleAudit()
    {
        Dictionary<string, (float Max, Vector2 Native, int Count)> worst = [];
        foreach (Sample sample in Samples)
        {
            float scale = sample.Native.Y > 0 ? sample.Drawn.Y / sample.Native.Y : 0f;
            if (worst.TryGetValue(sample.Texture, out (float Max, Vector2 Native, int Count) entry))
                worst[sample.Texture] = (Mathf.Max(entry.Max, scale), sample.Native, entry.Count + 1);
            else
                worst[sample.Texture] = (scale, sample.Native, 1);
        }

        int enlarged = 0;
        foreach ((string texture, (float max, Vector2 native, int count)) in worst)
        {
            if (max <= 1.02f)
                continue;
            enlarged++;
            GD.Print($"ASSET_SCALE_AUDIT: ENLARGED x{max:0.00} {texture} native={native.X:0}x{native.Y:0} count={count}");
        }
        GD.Print($"ASSET_SCALE_AUDIT: {(enlarged == 0 ? "PASS" : "FAIL")} (textures={worst.Count}, enlarged={enlarged}, samples={Samples.Count})");
        Capturing = false;
        Samples.Clear();
    }

    private Label _readout = null!;
    private PanelContainer _panel = null!;

    public override void _Ready()
    {
        Layer = 40;
        ProcessMode = ProcessModeEnum.Always;

        _panel = new PanelContainer { Visible = false };
        _panel.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        _panel.Position = new Vector2(16, -170);
        _panel.MouseFilter = Control.MouseFilterEnum.Ignore;
        StyleBoxFlat style = new()
        {
            BgColor = new Color("0b0f0bf2"),
            BorderColor = new Color("c2a35fcc"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 8,
            ContentMarginBottom = 8
        };
        _panel.AddThemeStyleboxOverride("panel", style);

        _readout = new Label { Text = "", MouseFilter = Control.MouseFilterEnum.Ignore };
        _readout.AddThemeColorOverride("font_color", new Color("e5dcc4"));
        _readout.AddThemeFontSizeOverride("font_size", 12);
        _panel.AddChild(_readout);
        AddChild(_panel);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.F9 })
            return;
        SetEnabled(!Capturing);
        GetViewport().SetInputAsHandled();
    }

    private void SetEnabled(bool enabled)
    {
        Capturing = enabled;
        Samples.Clear();
        _panel.Visible = enabled;
        SetProcess(enabled);
        _readout.Text = "ASSET INSPECTOR\nrebuilding terrain...";

        // Chunks only draw once, so the recording has to be replayed by forcing
        // everything currently on screen to redraw.
        foreach (Node node in GetTree().GetNodesInGroup(CountyVisualChunk.RedrawGroup))
            if (node is CanvasItem item)
                item.QueueRedraw();

        GD.Print($"ASSET_INSPECTOR: {(enabled ? "enabled" : "disabled")}");
    }

    public override void _Process(double delta)
    {
        if (!Capturing)
            return;

        Vector2 mouse = GetViewport().GetMousePosition();
        Transform2D canvas = GetViewport().GetCanvasTransform();
        Vector2 world = canvas.AffineInverse() * mouse;

        // Later samples are drawn on top, so the last containing sprite wins.
        // Standing art is preferred over the ground it stands on.
        Sample? best = null;
        for (int index = Samples.Count - 1; index >= 0; index--)
        {
            Sample sample = Samples[index];
            Vector2 anchor = IsometricGrid.GridToScreen(sample.GridPosition);
            Rect2 rect = sample.Ground
                ? new Rect2(anchor - sample.Drawn * .5f, sample.Drawn)
                : new Rect2(anchor - new Vector2(sample.Drawn.X * .5f, sample.Drawn.Y), sample.Drawn);
            if (!rect.HasPoint(world))
                continue;
            if (!sample.Ground) { best = sample; break; }
            best ??= sample;
        }

        if (best is not { } hit)
        {
            _readout.Text = $"ASSET INSPECTOR  (F9)\nno artwork under cursor\nsamples: {Samples.Count}";
            return;
        }

        float scale = hit.Native.Y > 0 ? hit.Drawn.Y / hit.Native.Y : 0f;
        string verdict = scale > 1.02f ? $"ENLARGED x{scale:0.00} - soft"
            : scale > .98f ? "native size"
            : $"reduced x{scale:0.00} - crisp";
        _readout.Text =
            $"ASSET INSPECTOR  (F9)\n" +
            $"{hit.Texture}\n" +
            $"kind    : {(hit.Ground ? "ground diamond" : "standing art")}\n" +
            $"native  : {hit.Native.X:0} x {hit.Native.Y:0}\n" +
            $"drawn   : {hit.Drawn.X:0} x {hit.Drawn.Y:0}\n" +
            $"scale   : {scale:0.000}  ({verdict})\n" +
            $"at      : {hit.GridPosition.X:0.0}, {hit.GridPosition.Y:0.0}";
    }
}
