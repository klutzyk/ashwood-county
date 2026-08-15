#nullable enable

using AshwoodCounty.UI;
using Godot;

namespace AshwoodCounty.Systems;

public enum TimeOfDay { Dawn, Day, Dusk, Night }

/// <summary>
/// Drives the first daily survival rhythm from the existing GameClock. It owns
/// the time-of-day phase, the ambient darkness value used by the night lighting
/// system, the zombie threat scale, and restrained phase warnings through the
/// HUD.
/// </summary>
public partial class SurvivalCycle : Node
{
    public const string GroupName = "survival_cycle";

    public static SurvivalCycle? Active => Current is not null && GodotObject.IsInstanceValid(Current) ? Current : null;
    public static float GetThreatScale() => Active?.ThreatScale ?? 1f;
    public static bool IsNightActive() => Active?.IsNight ?? false;

    private static SurvivalCycle? Current;

    // Day structure in in-game minutes since midnight.
    [Export] public int DawnStartMinute { get; set; } = 360;
    [Export] public int DayStartMinute { get; set; } = 420;
    [Export] public int DuskStartMinute { get; set; } = 1020;
    [Export] public int NightStartMinute { get; set; } = 1140;

    // Night-pressure tuning.
    [Export] public float DuskThreatScale { get; set; } = 1.35f;
    [Export] public float NightThreatScale { get; set; } = 1.85f;

    private GameClock _clock = null!;
    private TimeOfDay _lastNotifiedPhase;
    private float _threatScale = 1f;
    private float _darkness;

    public TimeOfDay Phase { get; private set; } = TimeOfDay.Day;
    public bool IsNight => Phase == TimeOfDay.Night;
    public float ThreatScale => _threatScale;
    public float Darkness => _darkness;

    public override void _Ready()
    {
        Current = this;
        AddToGroup(GroupName);
        _clock = GetNode<GameClock>("../GameClock");

        Phase = Classify(_clock.TotalMinutes % 1440.0);
        _lastNotifiedPhase = Phase;
        _threatScale = ThreatScaleFor(_clock.TotalMinutes % 1440.0);
        _darkness = DarknessFor(_clock.TotalMinutes % 1440.0);
    }

    public override void _Process(double delta)
    {
        double minute = _clock.TotalMinutes % 1440.0;
        Phase = Classify(minute);
        _threatScale = ThreatScaleFor(minute);
        _darkness = DarknessFor(minute);
        NotifyTransitions(Phase);
    }

    private TimeOfDay Classify(double minute)
    {
        if (minute >= NightStartMinute || minute < DawnStartMinute) return TimeOfDay.Night;
        if (minute < DayStartMinute) return TimeOfDay.Dawn;
        if (minute < DuskStartMinute) return TimeOfDay.Day;
        if (minute < NightStartMinute) return TimeOfDay.Dusk;
        return TimeOfDay.Night;
    }

    private float ThreatScaleFor(double minute)
    {
        if (minute >= NightStartMinute || minute < DawnStartMinute) return NightThreatScale;
        if (minute < DayStartMinute)
            return Mathf.Lerp(NightThreatScale, 1f, (float)((minute - DawnStartMinute) / (DayStartMinute - DawnStartMinute)));
        if (minute < DuskStartMinute) return 1f;

        float duskT = Mathf.Clamp((float)((minute - DuskStartMinute) / (NightStartMinute - DuskStartMinute)), 0f, 1f);
        return duskT < 0.5f
            ? Mathf.Lerp(1f, DuskThreatScale, duskT * 2f)
            : Mathf.Lerp(DuskThreatScale, NightThreatScale, (duskT - 0.5f) * 2f);
    }

    private float DarknessFor(double minute)
    {
        if (minute >= NightStartMinute || minute < DawnStartMinute) return 1f;
        if (minute < DayStartMinute)
            return 1f - Smoothstep((float)((minute - DawnStartMinute) / (DayStartMinute - DawnStartMinute)));
        if (minute < DuskStartMinute) return 0f;
        return Smoothstep((float)((minute - DuskStartMinute) / (NightStartMinute - DuskStartMinute)));
    }

    private static float Smoothstep(float t) { float x = Mathf.Clamp(t, 0f, 1f); return x * x * (3f - 2f * x); }

    private void NotifyTransitions(TimeOfDay phase)
    {
        if (phase == _lastNotifiedPhase) return;
        _lastNotifiedPhase = phase;

        string message = phase switch
        {
            TimeOfDay.Dusk => "DUSK APPROACHING\nFinish nearby work and head for shelter.",
            TimeOfDay.Night => "NIGHTFALL\nZombies are more dangerous in the dark.",
            TimeOfDay.Dawn => "DAWN\nThe night has passed.",
            _ => "MORNING\nDaylight offers a fresh chance to search."
        };
        (GetTree().GetFirstNodeInGroup(GameHud.GroupName) as GameHud)?.Notify(message);
    }
}
