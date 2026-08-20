#nullable enable

using System.Linq;
using AshwoodCounty.Units;
using Godot;

namespace AshwoodCounty.Systems;

/// <summary>
/// Opt-in smoke test for the daily survival cycle and night lighting. Set
/// ASHWOOD_VALIDATE_SURVIVAL=1; inert in normal play. It jumps the clock through
/// day, dusk, night and dawn, verifies phase/darkness/threat, and checks that
/// the CanvasModulate darkness and pooled survivor lights are live, including a
/// flashlight-equipped survivor.
/// </summary>
public partial class SurvivalLoopValidation : Node
{
    private enum Phase { Waiting, DayCheck, DuskCheck, NightCheck, FlashlightCheck, ClusterCheck, DawnCheck, Complete }

    private Phase _phase;
    private GameClock _clock = null!;
    private NightLightingSystem _lighting = null!;
    private Survivor _first = null!;
    private int _frames;
    private double _elapsed;

    public override void _Ready()
    {
        if (System.Environment.GetEnvironmentVariable("ASHWOOD_VALIDATE_SURVIVAL") != "1") { SetProcess(false); return; }
        _phase = Phase.Waiting;
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        if (_elapsed > 30) { Fail("timeout"); return; }
        if (_phase == Phase.Waiting) { TryBegin(); return; }

        _frames++;
        switch (_phase)
        {
            case Phase.DayCheck:
                if (_frames < 3) return;
                if (SurvivalCycle.Active is not { } cycle) { Fail("cycle missing"); return; }
                if (cycle.Phase != TimeOfDay.Day) { Fail($"expected Day, got {cycle.Phase}"); return; }
                if (cycle.Darkness > 0.02f) { Fail($"day darkness too high: {cycle.Darkness:0.00}"); return; }
                if (Luminance(_lighting.CanvasModulateColor) < 0.95f) { Fail("day modulate is not bright"); return; }
                if (_lighting.ActiveLightCount != 0) { Fail("lights active during day"); return; }
                _clock.SetTotalMinutes(cycle.DuskStartMinute + 20);
                Next(Phase.DuskCheck);
                break;

            case Phase.DuskCheck:
                if (_frames < 3) return;
                if (SurvivalCycle.Active is not { } dusk) { Fail("cycle missing"); return; }
                if (dusk.Phase != TimeOfDay.Dusk) { Fail($"expected Dusk, got {dusk.Phase}"); return; }
                if (dusk.Darkness <= 0.05f || dusk.Darkness >= 0.95f) { Fail($"dusk darkness out of range: {dusk.Darkness:0.00}"); return; }
                if (dusk.ThreatScale <= 1.0f) { Fail("dusk threat did not rise"); return; }
                _clock.SetTotalMinutes(dusk.NightStartMinute + 20);
                Next(Phase.NightCheck);
                break;

            case Phase.NightCheck:
                if (_frames < 3) return;
                if (SurvivalCycle.Active is not { } night) { Fail("cycle missing"); return; }
                if (night.Phase != TimeOfDay.Night || !night.IsNight) { Fail("night phase not active"); return; }
                if (night.Darkness < 0.85f) { Fail($"night darkness too weak: {night.Darkness:0.00}"); return; }
                if (Luminance(_lighting.CanvasModulateColor) > 0.5f) { Fail("night modulate too bright"); return; }
                if (_lighting.ActiveLightCount < 1) { Fail("no survivor lights at night"); return; }
                if (_lighting.PersonalLightEnergyUsed <= 0f) { Fail("personal light energy not applied"); return; }
                if (night.ThreatScale < 1.5f) { Fail("night threat too weak"); return; }
                _first.Inventory.TryAdd("flashlight", 1);
                if (!_first.EquipItem("flashlight")) { Fail("could not equip flashlight"); return; }
                Next(Phase.FlashlightCheck);
                break;

            case Phase.FlashlightCheck:
                if (_frames < 3) return;
                if (_lighting.ActiveFlashlightCount < 1) { Fail("flashlight light not active"); return; }
                Vector2 clusterPosition = _first.SimulationPosition;
                foreach (Survivor survivor in GetTree().GetNodesInGroup(Survivor.GroupName).OfType<Survivor>())
                    survivor.SimulationPosition = clusterPosition;
                Next(Phase.ClusterCheck);
                break;

            case Phase.ClusterCheck:
                if (_frames < 3) return;
                if (_lighting.ActiveLightCount < 1) { Fail("cluster lost survivor lights"); return; }
                if (_lighting.PersonalLightEnergyUsed >= 1.0f) { Fail($"personal lights not normalized when clustered: {_lighting.PersonalLightEnergyUsed:0.00}"); return; }
                if (_lighting.ActiveFlashlightCount >= 1 && _lighting.FlashlightEnergyUsed >= 1.0f) { Fail($"flashlight not normalized when clustered: {_lighting.FlashlightEnergyUsed:0.00}"); return; }
                if (SurvivalCycle.Active is not { } current) { Fail("cycle missing"); return; }
                _clock.SetTotalMinutes(current.DawnStartMinute + 20);
                Next(Phase.DawnCheck);
                break;

            case Phase.DawnCheck:
                if (_frames < 3) return;
                if (SurvivalCycle.Active is not { } dawn) { Fail("cycle missing"); return; }
                if (dawn.Phase != TimeOfDay.Dawn) { Fail($"expected Dawn, got {dawn.Phase}"); return; }
                if (dawn.Darkness >= 0.9f) { Fail($"dawn darkness did not lift: {dawn.Darkness:0.00}"); return; }
                if (dawn.ThreatScale >= dawn.NightThreatScale) { Fail("dawn threat did not ease"); return; }
                Pass();
                break;
        }
    }

    private void TryBegin()
    {
        _clock = GetNode<GameClock>("../GameClock");
        _lighting = GetNode<NightLightingSystem>("../World/NightLightingSystem");
        _first = GetTree().GetNodesInGroup(Survivor.GroupName).OfType<Survivor>().FirstOrDefault()!;
        if (SurvivalCycle.Active is null || _first is null) return;
        Next(Phase.DayCheck);
    }

    private void Next(Phase phase)
    {
        _phase = phase;
        _frames = 0;
        GD.Print($"SURVIVAL_VALIDATION: {phase}");
    }

    private static float Luminance(Color color) => 0.2126f * color.R + 0.7152f * color.G + 0.0722f * color.B;

    private void Fail(string reason)
    {
        GD.PrintErr($"SURVIVAL_VALIDATION: FAIL ({reason}, phase={_phase})");
        _phase = Phase.Complete;
        SetProcess(false);
    }

    private void Pass()
    {
        GD.Print("SURVIVAL_VALIDATION: PASS (day=True, dusk=True, night=True, flashlight=True, dawn=True, ambient=True, threat=True, lights=True)");
        _phase = Phase.Complete;
        SetProcess(false);
    }
}
