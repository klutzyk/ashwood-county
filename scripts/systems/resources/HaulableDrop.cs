#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using AshwoodCounty.Items;
using AshwoodCounty.World;
using Godot;

namespace AshwoodCounty.Resources;

/// <summary>
/// A loose ground pile of physical items awaiting transport to settlement
/// storage. It reuses the existing item catalog, survivor inventory and
/// settlement item storage; it is deliberately not a parallel resource
/// economy. One survivor claims a pile at a time.
/// </summary>
[Tool]
public partial class HaulableDrop : Node2D, IGridOccupant
{
    public const string GroupName = "haulable_drops";

    private readonly List<ItemStack> _stacks = [];
    private Vector2 _gridPosition;
    private ulong _claimingWorker;
    private bool _hovered;
    private bool _workHighlighted;
    private bool _designated;

    [Export]
    public Vector2 GridPosition
    {
        get => _gridPosition;
        set
        {
            _gridPosition = value;
            UpdateRenderedPosition();
        }
    }

    [Export] public string DisplayName { get; set; } = "Loose Supplies";
    [Export] public float InteractionRadius { get; set; } = 0.75f;

    /// <summary>Authoring helper: "itemId:quantity;itemId:quantity".</summary>
    [Export(PropertyHint.MultilineText)]
    public string Contents
    {
        get => string.Join(";", _stacks.Select(stack => $"{stack.ItemId}:{stack.Quantity}"));
        set => ParseContents(value);
    }

    public bool IsClaimed => _claimingWorker != 0;
    public bool IsHovered => _hovered;
    public bool IsWorkHighlighted => _workHighlighted;
    public bool IsDesignatedForHauling => _designated;
    public bool HasItems => _stacks.Any(stack => stack.Quantity > 0);
    public IReadOnlyList<ItemStack> Stacks => _stacks;
    public Vector2 WorldPosition => GridPosition + new Vector2(0.5f, 0.5f);
    public WorldFootprint OccupancyFootprint => new(WorldPosition - Vector2.One * 0.35f, Vector2.One * 0.7f);

    public override void _Ready()
    {
        UpdateRenderedPosition();
        ParseContents(Contents);
        if (Engine.IsEditorHint())
        {
            SetProcess(false);
            return;
        }

        AddToGroup(GroupName);
        AddToGroup(GridOccupancy.OccupantGroup);
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (IsHovered || IsWorkHighlighted || IsClaimed) QueueRedraw();
    }

    public void AddStack(string itemId, int quantity)
    {
        if (quantity <= 0 || !ItemCatalog.TryGet(itemId, out _)) return;
        _stacks.Add(new ItemStack(itemId, quantity));
        QueueRedraw();
    }

    public bool TryClaim(ulong workerId)
    {
        if (Engine.IsEditorHint() || !HasItems || (_claimingWorker != 0 && _claimingWorker != workerId)) return false;
        _claimingWorker = workerId;
        QueueRedraw();
        return true;
    }

    public void ReleaseClaim(ulong workerId)
    {
        if (_claimingWorker != workerId) return;
        _claimingWorker = 0;
        QueueRedraw();
    }

    /// <summary>Moves as many stacks as fit into the survivor's inventory. Returns the number of items taken.</summary>
    public int TakeAvailable(SurvivorInventory into)
    {
        int taken = 0;
        foreach (ItemStack stack in _stacks.ToArray())
        {
            if (stack.Quantity <= 0) continue;
            int added = into.TryAdd(stack.ItemId, stack.Quantity);
            if (added <= 0) continue;
            taken += added;
            int left = stack.Quantity - added;
            int index = _stacks.IndexOf(stack);
            if (left <= 0) _stacks.RemoveAt(index);
            else _stacks[index] = stack with { Quantity = left };
        }

        if (taken > 0) QueueRedraw();
        return taken;
    }

    public void SetHovered(bool hovered)
    {
        if (_hovered == hovered) return;
        _hovered = hovered;
        QueueRedraw();
    }

    public void SetWorkHighlighted(bool highlighted)
    {
        if (_workHighlighted == highlighted) return;
        _workHighlighted = highlighted;
        QueueRedraw();
    }

    public void SetDesignated(bool designated)
    {
        _designated = designated && HasItems;
        QueueRedraw();
    }

    public Vector2 GetInteractionPosition()
    {
        return WorldPosition + new Vector2(0, InteractionRadius);
    }

    public bool ContainsScreenPoint(Vector2 screenPoint)
    {
        Vector2 local = GetGlobalTransformWithCanvas().AffineInverse() * screenPoint;
        return new Rect2(-34, -34, 68, 44).HasPoint(local);
    }

    public override void _Draw()
    {
        if (IsHovered || IsWorkHighlighted)
        {
            float pulse = IsWorkHighlighted ? 0.86f + 0.14f * Mathf.Sin((float)Time.GetTicksMsec() / 520.0f) : 1f;
            float alpha = IsWorkHighlighted ? 0.34f : 0.20f;
            DrawCrate(new Vector2(1.18f, 1.18f), new Color(1f, 1f, 1f, alpha * pulse), new Color(1f, 1f, 1f, alpha * 0.8f * pulse), new Color(1f, 1f, 1f, alpha * 0.65f * pulse));
            DrawCrate(new Vector2(1.07f, 1.07f), new Color(1f, 1f, 1f, alpha * 0.45f * pulse), new Color(1f, 1f, 1f, alpha * 0.35f * pulse), new Color(1f, 1f, 1f, alpha * 0.3f * pulse));
        }

        DrawCrate(Vector2.One, new Color("#6b6759"), new Color("#57534a"), new Color("#4a473f"));
        DrawPolyline([new(-28, -6), new(0, -16), new(28, -6)], new Color("#8a7a4c"), 2, true);

        if (HasItems)
        {
            Texture2D? icon = FirstIcon();
            if (icon is not null)
            {
                DrawTextureRect(icon, new Rect2(new Vector2(-11, -12), new Vector2(22, 22)), false);
            }
        }

        if (IsClaimed)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin((float)Time.GetTicksMsec() / 220.0f);
            DrawPolyline(Ellipse(30, 11), new Color(1f, 1f, 1f, 0.16f + 0.12f * pulse), 2, true);
        }

        if (IsDesignatedForHauling)
        {
            DrawCircle(new Vector2(0, -28), 8, new Color(0.95f, 0.68f, 0.22f, 0.92f));
        }
    }

    private void DrawCrate(Vector2 scale, Color top, Color left, Color right)
    {
        DrawSetTransform(Vector2.Zero, 0, scale * 1.12f);
        DrawPolygon([new(-28, -6), new(0, -16), new(28, -6), new(0, 8)], [top]);
        DrawPolygon([new(-28, -6), new(0, 8), new(0, 17), new(-28, 3)], [left]);
        DrawPolygon([new(0, 8), new(28, -6), new(28, 3), new(0, 17)], [right]);
        DrawSetTransform(Vector2.Zero);
    }

    private Texture2D? FirstIcon()
    {
        foreach (ItemStack stack in _stacks)
        {
            if (stack.Quantity <= 0 || !ItemCatalog.TryGet(stack.ItemId, out ItemDefinition definition)) continue;
            string iconPath = definition.IconPath;
            if (!string.IsNullOrWhiteSpace(iconPath) && ResourceLoader.Exists(iconPath)) return TextureRegistry.Get(iconPath);
        }

        return null;
    }

    private static Vector2[] Ellipse(float radiusX, float radiusY)
    {
        Vector2[] points = new Vector2[33];
        for (int i = 0; i < points.Length; i++)
        {
            float angle = Mathf.Tau * i / 32;
            points[i] = new(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
        }

        return points;
    }

    private void ParseContents(string value)
    {
        _stacks.Clear();
        if (string.IsNullOrWhiteSpace(value)) return;
        foreach (string entry in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] parts = entry.Split(':', 2);
            if (parts.Length != 2 || !int.TryParse(parts[1], out int quantity) || quantity <= 0) continue;
            AddStack(parts[0].Trim(), quantity);
        }
    }

    private void UpdateRenderedPosition()
    {
        Vector2 projected = IsometricGrid.GridToScreen(WorldPosition);
        if (!Position.IsEqualApprox(projected)) Position = projected;
    }
}
