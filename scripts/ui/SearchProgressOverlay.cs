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
        // Same ink, brass and parchment as AshwoodTheme, so a world-space
        // readout reads as part of the same interface as the HUD bars.
        private static readonly Color Backing = new("0d110fdb");
        private static readonly Color Edge = new("8a7a4c8c");
        private static readonly Color Track = new("1b211ce8");
        private static readonly Color Fill = new("#c2a35f");
        private static readonly Color SearchCaption = new("e5dcc4f0");
        private static readonly Color ApproachCaption = new("9c9683dd");
        private static readonly Color TravelMarker = new("dfc481e6");
        private const float BarWidth = 92f;
        private const float BarHeight = 6f;

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
                10,
                approaching ? ApproachCaption : SearchCaption);

            Rect2 frame = new(topLeft - new Vector2(3f, 3f), new Vector2(BarWidth + 6f, BarHeight + 6f));
            DrawRect(frame, Backing);
            DrawRect(frame, Edge, false, 1f);
            DrawRect(new Rect2(topLeft, new Vector2(BarWidth, BarHeight)), Track);

            if (approaching)
            {
                float travel = 0.5f + 0.5f * Mathf.Sin((float)Time.GetTicksMsec() / 520.0f);
                float markerX = topLeft.X + (BarWidth - 16f) * travel;
                DrawRect(new Rect2(markerX, topLeft.Y, 16f, BarHeight), TravelMarker);
            }
            else
            {
                float breathe = 0.94f + 0.06f * Mathf.Sin((float)Time.GetTicksMsec() / 260.0f);
                DrawRect(new Rect2(topLeft, new Vector2(BarWidth * clamped, BarHeight)), new Color(Fill.R * breathe, Fill.G * breathe, Fill.B * breathe, 1f));
            }
        }
    }
}
