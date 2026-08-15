#nullable enable

using System.Collections.Generic;
using System.Linq;
using AshwoodCounty.Units;
using AshwoodCounty.World;
using Godot;

namespace AshwoodCounty.Systems;

/// <summary>
/// Renders night darkness with Godot's 2D canvas lighting. A CanvasModulate
/// multiplies the whole world toward a dark, cool ambient color, and pooled
/// Light2D pools follow each survivor so the world art itself is revealed in a
/// soft local radius. An equipped flashlight adds a larger, forward-weighted
/// pool. HUD and overlay canvas layers are untouched.
/// </summary>
public partial class NightLightingSystem : Node2D
{
    private const float TextureRadius = 128f; // radial light texture is 256x256

    [ExportGroup("Ambient Darkness")]
    [Export] public Color DayModulate { get; set; } = new(1f, 1f, 1f);
    [Export] public Color NightModulate { get; set; } = new(0.13f, 0.15f, 0.22f);

    [ExportGroup("Personal Visibility")]
    [Export] public float PersonalLightRadius { get; set; } = 108f;
    [Export] public float PersonalLightEnergy { get; set; } = 1.15f;
    [Export] public Color PersonalLightColor { get; set; } = new("#ffe6bd");
    [Export] public float PersonalDensityRadius { get; set; } = 150f;

    [ExportGroup("Flashlight")]
    [Export] public float FlashlightRadius { get; set; } = 300f;
    [Export] public float FlashlightEnergy { get; set; } = 1.45f;
    [Export] public float FlashlightElongation { get; set; } = 1.35f;
    [Export] public Color FlashlightColor { get; set; } = new("#fff3d0");
    [Export] public float FlashlightDensityRadius { get; set; } = 420f;

    private CanvasModulate _modulate = null!;
    private Node2D _lightsRoot = null!;
    private Texture2D _lightTexture = null!;
    private readonly List<PointLight2D> _personalLights = [];
    private readonly List<PointLight2D> _flashlights = [];

    public float CurrentDarkness { get; private set; }
    public Color CanvasModulateColor => _modulate?.Color ?? Colors.White;
    public int ActiveLightCount => _personalLights.Count(light => light.Visible);
    public int ActiveFlashlightCount => _flashlights.Count(light => light.Visible);
    public float PersonalLightEnergyUsed => _personalLights.Where(light => light.Visible).Select(light => light.Energy).DefaultIfEmpty(0f).Max();
    public float FlashlightEnergyUsed => _flashlights.Where(light => light.Visible).Select(light => light.Energy).DefaultIfEmpty(0f).Max();

    public override void _Ready()
    {
        _modulate = new CanvasModulate { Name = "DayNightModulate", Color = DayModulate };
        AddChild(_modulate);
        _lightsRoot = new Node2D { Name = "VisibilityLights" };
        AddChild(_lightsRoot);
        _lightTexture = CreateRadialLightTexture();
    }

    public override void _Process(double delta)
    {
        float darkness = SurvivalCycle.Active?.Darkness ?? 0f;
        CurrentDarkness = darkness;
        _modulate.Color = DayModulate.Lerp(NightModulate, darkness);
        UpdateSurvivorLights(darkness);
    }

    private void UpdateSurvivorLights(float darkness)
    {
        Survivor[] survivors = GetTree().GetNodesInGroup(Survivor.GroupName)
            .OfType<Survivor>()
            .Where(survivor => survivor.IsAlive)
            .ToArray();

        EnsureLightCount(_personalLights, survivors.Length);
        EnsureLightCount(_flashlights, survivors.Length);
        bool lightsVisible = darkness > 0.02f;

        for (int index = 0; index < survivors.Length; index++)
        {
            Survivor survivor = survivors[index];
            Vector2 lightPosition = IsometricGrid.GridToScreen(survivor.SimulationPosition);
            bool hasFlashlight = survivor.Inventory.EquippedLightId is not null;

            // Additive lights sum linearly where pools overlap. Scale each
            // source's energy by the local survivor density so a cluster
            // illuminates a larger area without making shared pixels blow out.
            float personalDensity = DensityAt(survivors, lightPosition, PersonalDensityRadius);
            float personalScale = 1f / Mathf.Max(1f, personalDensity);

            PointLight2D personal = _personalLights[index];
            // A flashlight replaces the small baseline pool instead of stacking
            // on top of it, so an equipped survivor is not overexposed at their
            // own position.
            personal.Visible = lightsVisible && !hasFlashlight;
            personal.Position = lightPosition;
            personal.Texture = _lightTexture;
            personal.Color = PersonalLightColor;
            personal.Energy = PersonalLightEnergy * darkness * personalScale;
            personal.TextureScale = PersonalLightRadius / TextureRadius;
            personal.Scale = Vector2.One;
            personal.Rotation = 0f;

            PointLight2D torch = _flashlights[index];
            float flashlightDensity = DensityAt(survivors, lightPosition, FlashlightDensityRadius);
            float flashlightScale = 1f / Mathf.Max(1f, flashlightDensity);
            torch.Visible = lightsVisible && hasFlashlight;
            torch.Energy = hasFlashlight ? FlashlightEnergy * darkness * flashlightScale : 0f;
            if (!torch.Visible) continue;
            torch.Position = lightPosition;
            torch.Texture = _lightTexture;
            torch.Color = FlashlightColor;
            torch.TextureScale = FlashlightRadius / TextureRadius;
            torch.Scale = new Vector2(FlashlightElongation, 1f);
            torch.Rotation = AngleForDirection(survivor.FacingDirection);
        }

        for (int index = survivors.Length; index < _personalLights.Count; index++)
        {
            _personalLights[index].Visible = false;
            _flashlights[index].Visible = false;
        }
    }

    /// <summary>
    /// Soft local density of survivors around a position. Each survivor within
    /// the radius contributes a triangular falloff weight, and the survivor at
    /// the center contributes a full 1.0, so isolated survivors normalize to 1.
    /// </summary>
    private static float DensityAt(Survivor[] survivors, Vector2 position, float radius)
    {
        if (radius <= 0f) return 1f;
        float density = 0f;
        foreach (Survivor survivor in survivors)
        {
            float distance = IsometricGrid.GridToScreen(survivor.SimulationPosition).DistanceTo(position);
            if (distance >= radius) continue;
            density += 1f - distance / radius;
        }
        return Mathf.Max(1f, density);
    }

    private void EnsureLightCount(List<PointLight2D> lights, int count)
    {
        while (lights.Count < count)
        {
            PointLight2D light = new()
            {
                Name = "SurvivorLight" + lights.Count,
                BlendMode = Light2D.BlendModeEnum.Add,
                Texture = _lightTexture
            };
            _lightsRoot.AddChild(light);
            lights.Add(light);
        }
    }

    private static Texture2D CreateRadialLightTexture()
    {
        Gradient gradient = new();
        gradient.SetOffset(0, 0f);
        gradient.SetColor(0, new Color(1f, 1f, 1f, 1f));
        gradient.SetOffset(1, 0.55f);
        gradient.SetColor(1, new Color(1f, 1f, 1f, 0.72f));
        gradient.AddPoint(1f, new Color(1f, 1f, 1f, 0f));
        return new GradientTexture2D
        {
            Gradient = gradient,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(1f, 0.5f),
            Width = 256,
            Height = 256
        };
    }

    private static float AngleForDirection(SurvivorDirection direction) => direction switch
    {
        SurvivorDirection.E => 0f,
        SurvivorDirection.SE => Mathf.Pi / 4f,
        SurvivorDirection.S => Mathf.Pi / 2f,
        SurvivorDirection.SW => 3f * Mathf.Pi / 4f,
        SurvivorDirection.W => Mathf.Pi,
        SurvivorDirection.NW => 5f * Mathf.Pi / 4f,
        SurvivorDirection.N => 3f * Mathf.Pi / 2f,
        _ => 7f * Mathf.Pi / 4f
    };
}
