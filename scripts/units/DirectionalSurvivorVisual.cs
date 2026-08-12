using System.Collections.Generic;
using Godot;
using AshwoodCounty.World;

namespace AshwoodCounty.Units;

[Tool]
public partial class DirectionalSurvivorVisual : Node2D
{
    private const string AssetRoot = "res://assets/art/characters/survivor_01/runtime";
    private readonly Dictionary<SurvivorDirection, Texture2D> _idleTextures = [];
    private readonly Dictionary<SurvivorDirection, Texture2D[]> _walkTextures = [];
    private Survivor _survivor = null!;
    private SurvivorDirection _direction = SurvivorDirection.S;
    private double _animationTime;

    [Export] public float RenderScale { get; set; } = 0.23f;
    [Export] public float WalkFramesPerSecond { get; set; } = 8.0f;
    public SurvivorDirection DisplayedDirection => _direction;
    public int DisplayedFrame { get; private set; }

    public override void _Ready()
    {
        LoadTextures();
        _survivor = GetParentOrNull<Survivor>();
        SetProcess(!Engine.IsEditorHint());
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (_survivor is null)
        {
            return;
        }

        SurvivorDirection previousDirection = _direction;
        if (_survivor.IsMoving)
        {
            _direction = QuantizeDirection(_survivor.MovementVector, _direction);
            _animationTime += delta;
        }
        else
        {
            _animationTime = 0;
        }

        int previousFrame = DisplayedFrame;
        DisplayedFrame = GetFrameIndex();
        if (previousDirection != _direction || previousFrame != DisplayedFrame)
        {
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        Texture2D texture = GetCurrentTexture();
        if (texture is null)
        {
            return;
        }

        Vector2 size = texture.GetSize() * RenderScale;
        DrawTextureRect(texture, new Rect2(new Vector2(-size.X * 0.5f, -size.Y), size), false);

        if (!Engine.IsEditorHint() && _survivor is not null && _survivor.CarriedAmount > 0)
        {
            DrawCarriedResource(_survivor.CarriedAmount, _survivor.CarriedResourceType);
        }
    }

    public static SurvivorDirection QuantizeDirection(Vector2 movement, SurvivorDirection fallback)
    {
        if (movement.LengthSquared() < 0.000001f)
        {
            return fallback;
        }

        // Direction names describe what the player sees on screen, while survivor
        // movement is stored in logical grid space. Project the vector before
        // quantizing so right=E, down-right=SE, and straight down=S.
        Vector2 screenMovement = IsometricGrid.GridToScreen(movement);
        float angle = Mathf.Atan2(screenMovement.Y, screenMovement.X);
        int octant = Mathf.PosMod(Mathf.RoundToInt(angle / (Mathf.Pi / 4.0f)), 8);
        return octant switch
        {
            0 => SurvivorDirection.E,
            1 => SurvivorDirection.SE,
            2 => SurvivorDirection.S,
            3 => SurvivorDirection.SW,
            4 => SurvivorDirection.W,
            5 => SurvivorDirection.NW,
            6 => SurvivorDirection.N,
            _ => SurvivorDirection.NE
        };
    }

    private int GetFrameIndex()
    {
        if (_survivor is null || !_survivor.IsMoving || !_walkTextures.TryGetValue(_direction, out Texture2D[] frames))
        {
            return 0;
        }

        return Mathf.PosMod(Mathf.FloorToInt(_animationTime * WalkFramesPerSecond), frames.Length);
    }

    private Texture2D GetCurrentTexture()
    {
        if (!Engine.IsEditorHint() && _survivor is not null && _survivor.IsMoving
            && _walkTextures.TryGetValue(_direction, out Texture2D[] frames))
        {
            return frames[DisplayedFrame];
        }

        return _idleTextures.GetValueOrDefault(_direction);
    }

    private void LoadTextures()
    {
        foreach (SurvivorDirection direction in System.Enum.GetValues<SurvivorDirection>())
        {
            string name = direction.ToString().ToLowerInvariant();
            _idleTextures[direction] = TextureRegistry.Get($"{AssetRoot}/idle_{name}.png");
        }

        foreach (SurvivorDirection direction in new[] { SurvivorDirection.NE, SurvivorDirection.E, SurvivorDirection.SE })
        {
            string name = direction.ToString().ToLowerInvariant();
            int frameCount = direction is SurvivorDirection.E or SurvivorDirection.SE ? 8 : 6;
            Texture2D[] frames = new Texture2D[frameCount];
            for (int index = 0; index < frames.Length; index++)
            {
                frames[index] = TextureRegistry.Get($"{AssetRoot}/walk_{name}_{index:00}.png");
            }

            _walkTextures[direction] = frames;
        }
    }

    private void DrawCarriedResource(int amount, Resources.ResourceType resourceType)
    {
        if (resourceType == Resources.ResourceType.Food)
        {
            DrawCircle(new Vector2(20, -38), 11, new Color("#704c35"));
            DrawCircle(new Vector2(17, -42), 3, new Color("#c83f45"));
            DrawCircle(new Vector2(23, -39), 3, new Color("#aa2639"));
            DrawString(ThemeDB.FallbackFont, new Vector2(25, -27), amount.ToString(), HorizontalAlignment.Left, -1, 11, Colors.White);
            return;
        }
        DrawCircle(new Vector2(20, -38), 10, new Color(0.12f, 0.1f, 0.07f, 0.62f));
        Color wood = new("#a66d35");
        for (int index = 0; index < 3; index++)
        {
            Vector2 start = new(12 + index * 5, -43 + index * 2);
            DrawLine(start, start + new Vector2(12, -5), wood, 4);
        }

        DrawString(ThemeDB.FallbackFont, new Vector2(25, -27), amount.ToString(), HorizontalAlignment.Left, -1, 11, Colors.White);
    }
}
