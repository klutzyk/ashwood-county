#nullable enable

using System.Collections.Generic;
using System.Linq;
using AshwoodCounty.Units;
using Godot;

namespace AshwoodCounty.World;

/// <summary>
/// Exterior occlusion transparency. Large authored objects that obscure a
/// survivor from the camera (for example a house the survivor walks behind)
/// fade smoothly while covered and restore when the survivor is readable.
/// It is deliberately separate from interior activation: being behind an
/// exterior object is not the same as being inside a building.
/// </summary>
public interface IOccludable
{
    bool IsValidOccludable();
    float OcclusionAlpha { get; set; }
    float ComputeOcclusionAlpha(Vector2 survivorScreenPosition);
}

public partial class OcclusionController : Node
{
    public const string GroupName = "occlusion_controller";
    private const double RefreshInterval = 0.12;
    private const float FadeSpeed = 3.6f;
    private readonly List<IOccludable> _occludables = [];
    private double _refresh;

    public override void _Ready()
    {
        AddToGroup(GroupName);
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Process(double delta)
    {
        _refresh -= delta;
        if (_refresh > 0) return;
        _refresh = RefreshInterval;
        UpdateAll((float)delta);
    }

    public void Register(IOccludable occludable)
    {
        if (occludable is null || _occludables.Contains(occludable)) return;
        _occludables.Add(occludable);
    }

    public void Unregister(IOccludable occludable)
    {
        _occludables.Remove(occludable);
    }

    public IReadOnlyList<IOccludable> Occludables => _occludables;

    private void UpdateAll(float delta)
    {
        List<Survivor> survivors = GetTree().GetNodesInGroup(Survivor.GroupName).OfType<Survivor>().ToList();
        foreach (IOccludable occludable in _occludables)
        {
            if (!occludable.IsValidOccludable()) continue;
            float target = 1f;
            foreach (Survivor survivor in survivors)
            {
                if (!survivor.IsAlive) continue;
                target = Mathf.Min(target, occludable.ComputeOcclusionAlpha(survivor.GetGlobalTransformWithCanvas().Origin));
            }

            occludable.OcclusionAlpha = Mathf.MoveToward(occludable.OcclusionAlpha, target, FadeSpeed * delta);
        }
    }
}
