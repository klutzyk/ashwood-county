#nullable enable

using System.Linq;
using Godot;
using AshwoodCounty.World;

namespace AshwoodCounty.Buildings.Interiors;

internal partial class InteriorSpriteVisual : Node2D
{
    private Texture2D _texture = null!;
    private float _targetHeight;
    private Color _tint = Colors.White;

    public void Initialize(string path, Vector2 gridPosition, float targetHeight, Color? tint = null, bool flip = false)
    {
        _texture = TextureRegistry.Get(path);
        _targetHeight = targetHeight;
        _tint = tint ?? Colors.White;
        Position = IsometricGrid.GridToScreen(gridPosition);
        Scale = new Vector2(flip ? -1 : 1, 1);
        ZIndex = 0;
        ZAsRelative = true;
    }

    public override void _Ready() => QueueRedraw();

    public override void _Draw()
    {
        float scale = _targetHeight / Mathf.Max(1, _texture.GetHeight());
        Vector2 size = _texture.GetSize() * scale;
        DrawTextureRect(_texture, new Rect2(-size.X * .5f, -size.Y, size.X, size.Y), false, _tint);
    }
}

internal partial class InteriorWallVisual : Node2D
{
    private WallDefinition _wall = null!;
    private Texture2D _texture = null!;
    private Vector2 _origin;

    public void Initialize(WallDefinition wall)
    {
        _wall = wall;
        _texture = TextureRegistry.Get(wall.TexturePath);
        Vector2 midpoint = (wall.Start + wall.End) * .5f;
        _origin = IsometricGrid.GridToScreen(midpoint);
        Position = _origin;
        ZIndex = 0;
    }

    public override void _Ready() => QueueRedraw();

    public override void _Draw()
    {
        Vector2 a = IsometricGrid.GridToScreen(_wall.Start) - _origin;
        Vector2 b = IsometricGrid.GridToScreen(_wall.End) - _origin;
        Vector2 lift = new(0,-44);
        Color wallColor = _wall.FlipVisual ? new Color("66716f") : new Color("aaa28f");
        DrawColoredPolygon([a,b,b+lift,a+lift],wallColor);
        DrawLine(a+lift,b+lift,new Color("514335"),5f,true);
        DrawLine(a,b,new Color(.20f,.18f,.15f,.82f),2f,true);
        float scale=38f/Mathf.Max(1,_texture.GetHeight());
        Vector2 size=_texture.GetSize()*scale;
        DrawTextureRect(_texture,new Rect2(-size.X*.5f,-size.Y+3,size.X,size.Y),false,new Color(1,1,1,.46f));
    }
}

internal partial class InteriorFloorVisual : Node2D
{
    private RoomDefinition _room = null!;
    private Texture2D _texture = null!;
    private Vector2 _origin;

    public void Initialize(RoomDefinition room)
    {
        _room = room;
        _texture = TextureRegistry.Get(room.FloorTexturePath);
        _origin = IsometricGrid.GridToScreen(room.Bounds.Position);
        Position = _origin;
        // Match the world-object layer so the legacy starting-area terrain
        // cannot cover the interior. The room's top-corner Y keeps it sorted
        // before its furniture and occupants.
        ZAsRelative = true;
        ZIndex = 0;
    }

    public override void _Ready() => QueueRedraw();

    public override void _Draw()
    {
        Vector2[] polygon = IsometricGrid.ProjectRectangle(_room.Bounds.Position, _room.Bounds.Size)
            .Select(point => point - _origin).ToArray();
        DrawColoredPolygon(polygon, _room.FloorTint.Darkened(.24f));
        const float span = 1.0f;
        float textureScale = 1.08f;
        Vector2 size = _texture.GetSize() * textureScale;
        for (float y = _room.Bounds.Position.Y + span * .5f; y < _room.Bounds.End.Y; y += span)
        for (float x = _room.Bounds.Position.X + span * .5f; x < _room.Bounds.End.X; x += span)
        {
            Vector2 center = IsometricGrid.GridToScreen(new Vector2(x, y)) - _origin;
            DrawTextureRect(_texture, new Rect2(center - size * .5f, size), false, new Color(1, 1, 1, .72f));
        }
        DrawPolyline([polygon[0], polygon[1], polygon[2], polygon[3], polygon[0]], new Color(.18f,.16f,.13f,.78f), 2f, true);
    }
}

internal partial class InteriorRoomMaskVisual : Node2D
{
    private Rect2 _bounds;
    private Vector2 _origin;

    public void Initialize(Rect2 bounds)
    {
        _bounds = bounds;
        _origin = IsometricGrid.GridToScreen(bounds.Position);
        Position = _origin;
        ZAsRelative = false;
        ZIndex = 3;
    }

    public override void _Ready() => QueueRedraw();

    public override void _Draw()
    {
        Vector2[] polygon = IsometricGrid.ProjectRectangle(_bounds.Position, _bounds.Size)
            .Select(point => point - _origin).ToArray();
        DrawColoredPolygon(polygon, new Color(.025f,.031f,.027f,.74f));
        DrawPolyline([polygon[0],polygon[1],polygon[2],polygon[3],polygon[0]],new Color(.15f,.17f,.14f,.64f),2f,true);
    }
}

internal partial class InteriorExteriorVisual : Node2D
{
    private Texture2D _texture = null!;
    private float _targetHeight;
    private float _targetWidth;

    public void Initialize(InteriorBuildingDefinition definition)
    {
        _texture = TextureRegistry.Get(definition.ExteriorTexturePath);
        _targetHeight = definition.ExteriorTargetHeight;
        _targetWidth = definition.ExteriorTargetWidth;
        Position = IsometricGrid.GridToScreen(definition.ExteriorAnchor);
        RotationDegrees = definition.ExteriorRotationDegrees;
        // Share the Objects y-sort layer with survivors: actors north of the
        // house render behind it, while actors south of its base render in front.
        ZAsRelative = true;
        ZIndex = 0;
    }

    public override void _Ready() => QueueRedraw();

    public override void _Draw()
    {
        float scaleY = _targetHeight / Mathf.Max(1, _texture.GetHeight());
        float scaleX = _targetWidth > 0 ? _targetWidth / Mathf.Max(1, _texture.GetWidth()) : scaleY;
        Vector2 size = _texture.GetSize() * new Vector2(scaleX,scaleY);
        DrawTextureRect(_texture, new Rect2(-size.X * .5f, -size.Y, size.X, size.Y), false);
    }
}
