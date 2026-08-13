namespace AshwoodCounty.World.Fog;

/// <summary>The three player-facing knowledge states of a county cell.</summary>
public enum FogCellVisibility
{
    Unexplored,
    Explored,
    Visible
}

/// <summary>Optional runtime visualization modes for fog diagnostics.</summary>
public enum FogDebugMode
{
    Disabled,
    RevealAll,
    StateColors
}
