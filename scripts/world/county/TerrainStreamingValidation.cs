#nullable enable

using System.Linq;
using AshwoodCounty.Camera;
using AshwoodCounty.World.County.Visual;
using Godot;

namespace AshwoodCounty.World.County;

/// <summary>
/// Opt-in check that terrain keeps streaming while the simulation is paused,
/// and that what gets built actually covers the visible rectangle.
///
/// Set ASHWOOD_VALIDATE_TERRAIN_STREAMING=1; inert in normal play.
///
/// This exists because pausing is implemented as <c>GetTree().Paused</c>, and
/// the terrain layers drive their streaming from <c>_Process</c>. Any node in
/// that path that loses <c>ProcessMode.Always</c> silently stops building
/// ground the moment the player pauses and pans, which is easy to introduce and
/// easy to miss in a screenshot taken while running.
/// </summary>
public partial class TerrainStreamingValidation : Node
{
    private enum Step { Idle, PausedPan, SecondPan, Done }

    /// <summary>Somewhere far from the camp, across several chunk boundaries.</summary>
    private static readonly Vector2 FirstTarget = new(154, 250);
    private static readonly Vector2 SecondTarget = new(208, 160);

    private StrategyCamera _camera = null!;
    private CountyGroundSurface _ground = null!;
    private CountyVisualLayer _landscape = null!;
    private Step _step = Step.Idle;
    private int _wait;

    public override void _Ready()
    {
        if (System.Environment.GetEnvironmentVariable("ASHWOOD_VALIDATE_TERRAIN_STREAMING") != "1")
        {
            SetProcess(false);
            return;
        }
        ProcessMode = ProcessModeEnum.Always;
        Callable.From(Begin).CallDeferred();
    }

    private void Begin()
    {
        _camera = GetNode<StrategyCamera>("../World/StrategyCamera");
        Node world = GetNode("../World");
        _ground = world.GetNode<CountyWorld>("CountyWorld")
            .GetNode<CountyVisualLayer>("CountyVisuals")
            .GetNode<CountyGroundSurface>("CountyGround");
        _landscape = world.GetNode<CountyWorld>("CountyWorld").GetNode<CountyVisualLayer>("CountyVisuals");

        // Pause first, then move. This is the exact order a player produces
        // when they hit space and drag the map.
        GetTree().Paused = true;
        _camera.SnapTo(FirstTarget, 1f);
        _step = Step.PausedPan;
        _wait = 45;
        GD.Print("TERRAIN_STREAMING_VALIDATION: paused pan started");
    }

    public override void _Process(double delta)
    {
        if (_step is Step.Idle or Step.Done)
            return;
        if (_wait-- > 0)
            return;

        if (_step == Step.PausedPan)
        {
            if (!Covers(FirstTarget, out string detail))
            {
                Finish(false, $"paused pan did not build terrain ({detail})");
                return;
            }
            _camera.SnapTo(SecondTarget, 1f);
            _step = Step.SecondPan;
            _wait = 45;
            return;
        }

        // Second target sits on a chunk corner, so it exercises the case where
        // the visible rectangle spans four chunks at once.
        if (!Covers(SecondTarget, out string seamDetail))
        {
            Finish(false, $"chunk-seam framing not covered ({seamDetail})");
            return;
        }
        Finish(true, $"ground={_ground.DetailChunks.Count}, landscape={_landscape.LandscapeChunks.Count}");
    }

    /// <summary>Both terrain layers hold the chunk under a grid position.</summary>
    private bool Covers(Vector2 gridPosition, out string detail)
    {
        Vector2I chunk = CountyCoordinateSpace.GridToChunk(gridPosition);
        bool ground = _ground.DetailChunks.Contains(chunk);
        bool landscape = _landscape.LandscapeChunks.Contains(chunk);
        detail = $"chunk={chunk}, ground={ground}, landscape={landscape}, paused={GetTree().Paused}";
        return ground && landscape;
    }

    private void Finish(bool passed, string detail)
    {
        _step = Step.Done;
        GetTree().Paused = false;
        SetProcess(false);
        GD.Print($"TERRAIN_STREAMING_VALIDATION: {(passed ? "PASS" : "FAIL")} ({detail})");
    }
}
