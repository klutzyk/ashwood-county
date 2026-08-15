#nullable enable

using System.Collections.Generic;
using Godot;

namespace AshwoodCounty.UI;

/// <summary>
/// Brief world-space reveal that makes the discovered items themselves the
/// reward. Item artwork rises and fades above the searched object in a short
/// stagger; an empty search shows a single muted result line instead.
/// </summary>
public partial class ItemRevealFeedback : Node2D
{
    private const float TotalDuration = 1.2f;

    private readonly List<(Texture2D Texture, string Label)> _entries = [];
    private string _emptyText = "";
    private float _elapsed;

    public void Initialize(
        Vector2 position,
        IReadOnlyList<(Texture2D Texture, string Label)> entries,
        string emptyText = "")
    {
        Position = position;
        _entries.Clear();
        _entries.AddRange(entries);
        _emptyText = emptyText;
        SetProcess(true);
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        _elapsed += (float)delta;
        if (_elapsed >= TotalDuration) QueueFree();
        else QueueRedraw();
    }

    public override void _Draw()
    {
        if (_entries.Count == 0)
        {
            if (string.IsNullOrEmpty(_emptyText)) return;
            float alpha = FadeAlpha(_elapsed);
            DrawString(
                ThemeDB.FallbackFont,
                new Vector2(-80f, 8f),
                _emptyText.ToUpperInvariant(),
                HorizontalAlignment.Center,
                160f,
                12,
                new Color(0.9f, 0.87f, 0.78f, 0.9f * alpha));
            return;
        }

        int count = Mathf.Min(_entries.Count, 5);
        const float slot = 40f;
        float startX = -(count - 1) * slot * 0.5f;

        for (int i = 0; i < count; i++)
        {
            float t = _elapsed - i * 0.09f;
            if (t < 0f) continue;

            float appear = Mathf.Clamp(t / 0.12f, 0f, 1f);
            float fade = 1f - Mathf.Clamp((t - 0.55f) / 0.5f, 0f, 1f);
            float alpha = appear * fade;
            float rise = Mathf.Min(t * 24f, 18f);
            float pop = 0.84f + 0.16f * appear;

            Texture2D texture = _entries[i].Texture;
            float scale = 36f / Mathf.Max(1f, texture.GetHeight());
            Vector2 size = texture.GetSize() * scale;
            Vector2 center = new(startX + i * slot, -rise);
            Rect2 rect = new(
                center.X - size.X * pop * 0.5f,
                center.Y - size.Y * pop,
                size.X * pop,
                size.Y * pop);
            DrawTextureRect(texture, rect, false, new Color(1f, 1f, 1f, alpha));

            if (_entries[i].Label.Length > 0)
            {
                DrawString(
                    ThemeDB.FallbackFont,
                    new Vector2(center.X - 24f, center.Y + 4f),
                    _entries[i].Label,
                    HorizontalAlignment.Center,
                    48f,
                    11,
                    new Color(0.96f, 0.92f, 0.8f, alpha));
            }
        }
    }

    private float FadeAlpha(float localTime)
    {
        float appear = Mathf.Clamp(localTime / 0.12f, 0f, 1f);
        float fade = 1f - Mathf.Clamp((localTime - 0.55f) / 0.5f, 0f, 1f);
        return appear * fade;
    }
}
