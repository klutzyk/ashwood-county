#nullable enable

using System.Linq;
using AshwoodCounty.Buildings.Interiors;
using AshwoodCounty.Resources;
using Godot;

namespace AshwoodCounty.UI;

/// <summary>
/// Dedicated overlay layer for in-progress searches. Because it renders on its
/// own CanvasLayer it always appears above the environment, no matter how the
/// searched object is Y-sorted against furniture or walls. Bars are positioned
/// directly above the object being searched so the association stays clear.
/// </summary>
public partial class SearchProgressOverlay : CanvasLayer
{
    public const string GroupName = "search_progress_overlay";

    private ProgressDrawer _drawer = null!;

    public override void _Ready()
    {
        Layer = 13;
        AddToGroup(GroupName);
        _drawer = new ProgressDrawer();
        AddChild(_drawer);
    }

    public override void _Process(double delta) => _drawer.QueueRedraw();

    private partial class ProgressDrawer : Node2D
    {
        private static readonly Color Backing = new(0.03f, 0.04f, 0.03f, 0.78f);
        private static readonly Color Track = new(0.14f, 0.15f, 0.12f, 0.95f);
        private static readonly Color Fill = new("#e8bd5f");

        public override void _Draw()
        {
            foreach (InteriorContainerRuntime container in GetTree()
                         .GetNodesInGroup(InteriorContainerRuntime.GroupName).OfType<InteriorContainerRuntime>())
            {
                if (!GodotObject.IsInstanceValid(container) || !container.Visible || !container.IsClaimed || container.IsSearched) continue;
                float height = container.ScreenDrawnHeight;
                DrawBar(container.GetGlobalTransformWithCanvas().Origin, container.SearchProgress, height);
            }

            foreach (ScavengeSource source in GetTree()
                         .GetNodesInGroup(ScavengeSource.GroupName).OfType<ScavengeSource>())
            {
                if (!GodotObject.IsInstanceValid(source) || !source.Visible || !source.IsClaimed || source.IsDepleted) continue;
                float zoom = Mathf.Abs(source.GetGlobalTransformWithCanvas().Scale.Y);
                DrawBar(source.GetGlobalTransformWithCanvas().Origin, source.DisplayedSearchProgress, 46f * zoom);
            }
        }

        private void DrawBar(Vector2 anchor, float progress, float objectHeight)
        {
            const float width = 74f;
            float clamped = Mathf.Clamp(progress, 0f, 1f);
            Vector2 topLeft = new(anchor.X - width * 0.5f, anchor.Y - objectHeight - 22f);

            DrawRect(new Rect2(topLeft - new Vector2(3f, 3f), new Vector2(width + 6f, 12f)), Backing);
            DrawRect(new Rect2(topLeft, new Vector2(width, 6f)), Track);
            if (clamped > 0f)
                DrawRect(new Rect2(topLeft, new Vector2(width * clamped, 6f)), Fill);
        }
    }
}
