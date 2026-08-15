#nullable enable

using System.Collections.Generic;
using System.Linq;
using AshwoodCounty.Buildings.Interiors;
using AshwoodCounty.Items;
using AshwoodCounty.UI;
using AshwoodCounty.World;
using Godot;

namespace AshwoodCounty.Units.Orders;

public sealed class SearchInteriorContainerOrder(InteriorContainerRuntime container) : ISurvivorOrder
{
    private enum Phase { Moving, Searching }
    private readonly InteriorPathFollower _path = new();
    private ulong _survivorId;
    private Phase _phase;
    private float _elapsed;
    private bool _claimed;

    public SurvivorOrderType Type => SurvivorOrderType.SearchContainer;
    public bool IsComplete { get; private set; }

    public void Start(Survivor survivor)
    {
        _survivorId = survivor.GetInstanceId();
        if (!GodotObject.IsInstanceValid(container))
        {
            Notify(survivor, "Unavailable");
            IsComplete = true;
            return;
        }
        if (container.IsSearched)
        {
            Notify(survivor, "Already searched");
            IsComplete = true;
            return;
        }
        if (!container.TryClaim(_survivorId))
        {
            Notify(survivor, "Already being searched");
            IsComplete = true;
            return;
        }
        _claimed = true;
        _elapsed = container.SearchProgress * container.SearchDuration;
        _path.Plan(survivor, container.InteractionPosition);
        if (_path.Unreachable)
        {
            Notify(survivor, "Can't reach");
            Complete();
        }
    }

    public void Tick(Survivor survivor, double delta)
    {
        if (IsComplete || !GodotObject.IsInstanceValid(container)) { Complete(); return; }
        if (_phase == Phase.Moving)
        {
            if (_path.Blocked)
            {
                Notify(survivor, "Can't reach");
                Complete();
                return;
            }
            if (_path.Tick(survivor, delta)) _phase = Phase.Searching;
            return;
        }
        survivor.StopMovement();
        _elapsed += (float)delta * survivor.WorkSpeedMultiplier * survivor.SkillMultiplier(SurvivorSkill.Scavenging);
        container.ReportProgress(_survivorId, _elapsed / Mathf.Max(.1f, container.SearchDuration));
        if (_elapsed < container.SearchDuration) return;
        IReadOnlyList<ItemStack> found = container.CompleteSearch(_survivorId);
        survivor.GainSkillExperience(SurvivorSkill.Scavenging, 3f + found.Sum(stack => stack.Quantity));
        if (found.Count == 0)
        {
            SpawnReveal(container, [], "Nothing useful");
        }
        else
        {
            List<(Texture2D Texture, string Label)> entries = found
                .Select(stack => (TextureRegistry.Get(ItemCatalog.Get(stack.ItemId).IconPath), $"x{stack.Quantity}"))
                .ToList();
            SpawnReveal(container, entries, "");
            (survivor.GetTree().GetFirstNodeInGroup(GameHud.GroupName) as GameHud)?.ShowContainerLoot(container, survivor);
        }
        _claimed = false;
        IsComplete = true;
    }

    public void Cancel(Survivor survivor) => Complete();

    private void Complete()
    {
        if (_claimed && GodotObject.IsInstanceValid(container)) container.ReleaseClaim(_survivorId);
        _claimed = false; IsComplete = true;
    }

    private void Notify(Survivor survivor, string message)
    {
        if (!GodotObject.IsInstanceValid(container)) return;
        (survivor.GetTree().GetFirstNodeInGroup(GameHud.GroupName) as GameHud)?.Notify($"{container.DisplayName.ToUpperInvariant()}\n{message}");
    }

    private static void SpawnReveal(InteriorContainerRuntime container, IReadOnlyList<(Texture2D Texture, string Label)> entries, string emptyText)
    {
        Node? parent = container.GetParent();
        if (parent is null) return;
        ItemRevealFeedback reveal = new();
        parent.AddChild(reveal);
        reveal.Initialize(container.Position + new Vector2(0f, -54f), entries, emptyText);
    }
}
