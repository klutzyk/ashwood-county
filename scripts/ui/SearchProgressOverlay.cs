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
        private static readonly Color Track = new(0.12f, 0.13f, 0.10f, 0.96f);
        private static readonly Color Fill = new("#e8bd5f");
        private static readonly Color SearchCaption = new(0.96f, 0.86f, 0.60f, 0.95f);
        private static readonly Color ApproachCaption = new(0.88f, 0.86f, 0.80f, 0.85f);
        private static readonly Color TravelMarker = new(0.97f, 0.95f, 0.87f, 0.9f);
        private const float BarWidth = 104f;
        private const float BarHeight = 10f;

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
                DrawBar(source.GetGlobalTransformWithCanvas().Origin, source.DisplayedSearchProgress, 50f * zoom);
            }
        }

        private void DrawBar(Vector2 anchor, float progress, float objectHeight)
        {
            float clamped = Mathf.Clamp(progress, 0f, 1f);
            bool approaching = clamped <= 0f;
            Vector2 topLeft = new(anchor.X - BarWidth * 0.5f, anchor.Y - objectHeight - 34f);

            DrawString(
                ThemeDB.FallbackFont,
                new Vector2(anchor.X - 90f, topLeft.Y - 5f),
                approaching ? "APPROACHING" : "SEARCHING",
                HorizontalAlignment.Center,
                180f,
                11,
                approaching ? ApproachCaption : SearchCaption);

            DrawRect(new Rect2(topLeft - new Vector2(4f, 4f), new Vector2(BarWidth + 8f, BarHeight + 8f)), Backing);
            DrawRect(new Rect2(topLeft, new Vector2(BarWidth, BarHeight)), Track);

            if (approaching)
            {
                float travel = 0.5f + 0.5f * Mathf.Sin((float)Time.GetTicksMsec() / 520.0f);
                float markerX = topLeft.X + 5f + (BarWidth - 10f) * travel;
                DrawCircle(new Vector2(markerX, topLeft.Y + BarHeight * 0.5f), 4f, TravelMarker);
            }
            else
            {
                float breathe = 0.94f + 0.06f * Mathf.Sin((float)Time.GetTicksMsec() / 260.0f);
                DrawRect(new Rect2(topLeft, new Vector2(BarWidth * clamped, BarHeight)), new Color(Fill.R * breathe, Fill.G * breathe, Fill.B * breathe, 1f));
            }
        }
    }
}
