#nullable enable

using System;
using System.Linq;
using AshwoodCounty.Camera;
using AshwoodCounty.Items;
using AshwoodCounty.Resources;
using AshwoodCounty.Systems;
using AshwoodCounty.Threats;
using AshwoodCounty.UI;
using AshwoodCounty.Units;
using AshwoodCounty.Jobs;
using Godot;

namespace AshwoodCounty.Buildings.Interiors;

/// <summary>
/// Opt-in itemized loot / survivor inventory / equipment vertical-slice test;
/// inert in normal play. Set ASHWOOD_VALIDATE_ITEMS=1. Optionally set
/// ASHWOOD_CAPTURE_LOOT_PNG / ASHWOOD_CAPTURE_INVENTORY_PNG to file paths to
/// also save screenshots of the loot popup and the survivor inventory tab.
/// </summary>
public partial class ItemVerticalSliceValidation : Node
{
    private enum Phase { Waiting, Approach, Search, EquipGear, DepositMove, DepositArrive, Exit, FarAway, Return, Complete }
    private Phase _phase;
    private InteriorBuildingRuntime _building = null!;
    private Survivor _first = null!;
    private Survivor _second = null!;
    private SettlementInventory _settlementInventory = null!;
    private SettlementItemStorage _itemStorage = null!;
    private Stockpile _stockpile = null!;
    private InteriorContainerRuntime _fridge = null!;
    private InteriorContainerRuntime _bathroom = null!;
    private ItemStack[] _fridgeRevealed = [];
    private ItemStack[] _bathroomRevealed = [];
    private string? _depositedFoodItemId;
    private Vector2 _lastFirst;
    private Vector2 _lastSecond;
    private double _elapsed;
    private int _startingFood, _startingMaterials, _startingMedicine;

    public override void _Ready()
    {
        if (System.Environment.GetEnvironmentVariable("ASHWOOD_VALIDATE_ITEMS") != "1") { SetProcess(false); return; }
        _phase = Phase.Waiting;
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        if (_elapsed > 150) { Fail("timeout"); return; }
        if (_phase == Phase.Waiting) { TryBegin(); return; }

        switch (_phase)
        {
            case Phase.Approach:
                if (!At(_first, new Vector2(218.7f, 156.55f)) || !At(_second, new Vector2(218.95f, 152.95f))) return;
                if (_building.State.Doors["front_door"].State != InteriorDoorState.Open) { Fail("entrance did not auto-open"); return; }
                if (_building.ExteriorAlpha > .15f) return;
                RefreshInteractables();
                _first.IssueSearchContainerOrder(_fridge);
                _second.IssueSearchContainerOrder(_bathroom);
                if (_bathroom.TryClaim(_first.GetInstanceId())) { _bathroom.ReleaseClaim(_first.GetInstanceId()); Fail("container reservation allowed a second survivor mid-search"); return; }
                Next(Phase.Search);
                break;

            case Phase.Search:
                if (!_fridge.IsSearched || !_bathroom.IsSearched) return;
                if (_settlementInventory.GetAmount(ResourceType.Food) != _startingFood || _settlementInventory.GetAmount(ResourceType.Materials) != _startingMaterials || _settlementInventory.GetAmount(ResourceType.Medicine) != _startingMedicine)
                { Fail("searching alone changed settlement resources; loot should only transfer via explicit take/deposit"); return; }

                _fridgeRevealed = _fridge.RemainingLoot.ToArray();
                _bathroomRevealed = _bathroom.RemainingLoot.ToArray();
                if (_fridgeRevealed.Length > 0)
                {
                    string itemId = _fridgeRevealed[0].ItemId;
                    int took = _fridge.TakeItem(itemId, 1, _first.Inventory);
                    if (took != 1) { Fail($"partial take of {itemId} returned {took}"); return; }
                    if (_first.Inventory.GetQuantity(itemId) != 1) { Fail("taken item did not appear in survivor inventory"); return; }
                    int expectedRemaining = _fridgeRevealed[0].Quantity - 1;
                    int actualRemaining = _fridge.RemainingLoot.Where(s => s.ItemId == itemId).Sum(s => s.Quantity);
                    if (actualRemaining != expectedRemaining) { Fail($"container kept {actualRemaining} of {itemId}, expected {expectedRemaining}"); return; }
                }
                if (_bathroomRevealed.Length > 0)
                {
                    int totalBathroom = _bathroomRevealed.Sum(s => s.Quantity);
                    int takenAll = _bathroom.TakeAll(_second.Inventory);
                    if (takenAll != totalBathroom) { Fail($"TakeAll moved {takenAll}, expected {totalBathroom}"); return; }
                    if (_bathroom.RemainingLoot.Count != 0) { Fail("bathroom cabinet still has loot after TakeAll"); return; }
                }
                CaptureIfRequested("ASHWOOD_CAPTURE_LOOT_PNG", "LOOT_PNG");
                GD.Print($"ITEM_VALIDATION: fridge revealed [{string.Join(", ", _fridgeRevealed.Select(s => $"{s.ItemId}x{s.Quantity}"))}], bathroom revealed [{string.Join(", ", _bathroomRevealed.Select(s => $"{s.ItemId}x{s.Quantity}"))}]");
                Next(Phase.EquipGear);
                break;

            case Phase.EquipGear:
                if (_first.Inventory.TryAdd("hiking_backpack", 1) != 1) { Fail("could not grant hiking_backpack"); return; }
                if (_first.Inventory.TryAdd("baseball_bat", 1) != 1) { Fail("could not grant baseball_bat"); return; }
                float capacityBefore = _first.Inventory.TotalCapacityKg;
                if (!_first.EquipItem("hiking_backpack")) { Fail("EquipItem(hiking_backpack) failed"); return; }
                if (_first.Inventory.EquippedBackpackId != "hiking_backpack") { Fail("backpack slot not set after equip"); return; }
                float expectedCapacity = capacityBefore + ItemCatalog.Get("hiking_backpack").CapacityBonusKg;
                if (Mathf.Abs(_first.Inventory.TotalCapacityKg - expectedCapacity) > .01f) { Fail($"capacity after backpack equip is {_first.Inventory.TotalCapacityKg}, expected {expectedCapacity}"); return; }
                if (!_first.EquipItem("baseball_bat")) { Fail("EquipItem(baseball_bat) failed"); return; }
                if (_first.Inventory.EquippedWeaponId != "baseball_bat") { Fail("weapon slot not set after equip"); return; }
                if (_first.Inventory.Items.Any(s => s.ItemId is "hiking_backpack" or "baseball_bat")) { Fail("equipped item duplicated in loose inventory"); return; }
                if (_second.Inventory.TryAdd("hammer", 1) != 1) { Fail("could not grant hammer to second survivor"); return; }
                CaptureInventoryTab();
                _first.IssueMoveOrder(_stockpile.WorldPosition + new Vector2(-.3f, 0));
                _second.IssueMoveOrder(_stockpile.WorldPosition + new Vector2(.3f, 0));
                Next(Phase.DepositArrive);
                break;

            case Phase.DepositArrive:
                if (_first.SimulationPosition.DistanceTo(_stockpile.WorldPosition) > 3f || _second.SimulationPosition.DistanceTo(_stockpile.WorldPosition) > 3f) return;
                if (_fridgeRevealed.Length > 0)
                {
                    _depositedFoodItemId = _fridgeRevealed[0].ItemId;
                    int qty = _first.Inventory.GetQuantity(_depositedFoodItemId);
                    if (qty > 0)
                    {
                        ItemDefinition definition = ItemCatalog.Get(_depositedFoodItemId);
                        int foodBefore = _settlementInventory.GetAmount(ResourceType.Food);
                        if (!_first.Inventory.TryRemove(_depositedFoodItemId, qty)) { Fail("could not remove deposited item from survivor inventory"); return; }
                        _itemStorage.Deposit(_depositedFoodItemId, qty);
                        if (definition.ResourceRelationship is ItemResourceRelationship relationship)
                        {
                            int expectedFood = foodBefore + relationship.Amount * qty;
                            if (_settlementInventory.GetAmount(ResourceType.Food) != expectedFood) { Fail($"deposit did not convert into Food resource as expected ({_settlementInventory.GetAmount(ResourceType.Food)} != {expectedFood})"); return; }
                        }
                    }
                }
                int hammerBefore = _itemStorage.GetQuantity("hammer");
                int materialsBefore = _settlementInventory.GetAmount(ResourceType.Materials);
                if (!_second.Inventory.TryRemove("hammer", 1)) { Fail("second survivor lost the hammer before depositing"); return; }
                _itemStorage.Deposit("hammer", 1);
                if (_itemStorage.GetQuantity("hammer") != hammerBefore + 1) { Fail("hammer did not appear in settlement item storage (should stay a real item, not convert)"); return; }
                if (_settlementInventory.GetAmount(ResourceType.Materials) != materialsBefore) { Fail("depositing a tool with no resource relationship unexpectedly changed Materials"); return; }
                Vector2 outsideLeft = new(_building.Definition.Footprint.Position.X - .8f, _building.Definition.Footprint.End.Y + 1f);
                Vector2 outsideRight = new(_building.Definition.Footprint.End.X + .8f, _building.Definition.Footprint.End.Y + 1f);
                _first.IssueMoveOrder(outsideLeft);
                _second.IssueMoveOrder(outsideRight);
                Next(Phase.Exit);
                break;

            case Phase.Exit:
                Vector2 exitLeft = new(_building.Definition.Footprint.Position.X - .8f, _building.Definition.Footprint.End.Y + 1f);
                Vector2 exitRight = new(_building.Definition.Footprint.End.X + .8f, _building.Definition.Footprint.End.Y + 1f);
                if (!At(_first, exitLeft) || !At(_second, exitRight)) return;
                if (_building.HasSurvivorInside || _building.ExteriorAlpha < .95f) return;
                _first.IssueMoveOrder(new Vector2(180, 155));
                _second.IssueMoveOrder(new Vector2(180, 156));
                Next(Phase.FarAway);
                break;

            case Phase.FarAway:
                if (!At(_first, new Vector2(180, 155)) || !At(_second, new Vector2(180, 156))) return;
                if (_building.IsInteriorActive) return;
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                _first.IssueMoveOrder(new Vector2(218.7f, 156.55f));
                _second.IssueMoveOrder(new Vector2(218.95f, 152.95f));
                Next(Phase.Return);
                break;

            case Phase.Return:
                if (!At(_first, new Vector2(218.7f, 156.55f)) || !At(_second, new Vector2(218.95f, 152.95f))) return;
                if (!_building.IsInteriorActive) return;
                RefreshInteractables();
                if (!_fridge.IsSearched) { Fail("fridge lost its Searched flag across unload/reload"); return; }
                ItemStack[] expectedFridgeLoot = _fridgeRevealed.Length > 0
                    ? [.. _fridgeRevealed.Select((s, i) => i == 0 ? s with { Quantity = s.Quantity - 1 } : s).Where(s => s.Quantity > 0)]
                    : [];
                if (!LootMatches(_fridge.RemainingLoot, expectedFridgeLoot)) { Fail($"fridge loot changed across unload/reload: now [{string.Join(", ", _fridge.RemainingLoot.Select(s => $"{s.ItemId}x{s.Quantity}"))}], expected [{string.Join(", ", expectedFridgeLoot.Select(s => $"{s.ItemId}x{s.Quantity}"))}]"); return; }
                if (_bathroom.RemainingLoot.Count != 0) { Fail("bathroom cabinet reacquired loot across unload/reload"); return; }
                if (_first.Inventory.EquippedBackpackId != "hiking_backpack" || _first.Inventory.EquippedWeaponId != "baseball_bat") { Fail("equipped gear did not persist"); return; }
                if (_depositedFoodItemId is not null && _first.Inventory.GetQuantity(_depositedFoodItemId) != 0) { Fail("deposited item reappeared in survivor inventory"); return; }
                if (_itemStorage.GetQuantity("hammer") != 1) { Fail("settlement-stored hammer did not persist"); return; }
                Pass();
                break;
        }
    }

    private void TryBegin()
    {
        _building = GetTree().GetNodesInGroup(InteriorBuildingRuntime.GroupName).OfType<InteriorBuildingRuntime>().FirstOrDefault(b => b.Definition.Containers.Count > 0)!;
        Survivor[] survivors = GetTree().GetNodesInGroup(Survivor.GroupName).OfType<Survivor>().Where(s => s.IsAlive).Take(2).ToArray();
        if (_building is null || survivors.Length < 2) return;
        foreach (Zombie zombie in GetTree().GetNodesInGroup(Zombie.GroupName).OfType<Zombie>()) { zombie.SetPhysicsProcess(false); zombie.RemoveFromGroup(Zombie.GroupName); }
        (GetTree().GetFirstNodeInGroup(SettlementJobSystem.GroupName) as SettlementJobSystem)?.SetProcess(false);

        _first = survivors[0]; _second = survivors[1];
        _first.MovementSpeed = 8; _second.MovementSpeed = 8;
        foreach (Survivor other in GetTree().GetNodesInGroup(Survivor.GroupName).OfType<Survivor>().Where(s => s.IsAlive && s != _first && s != _second))
        { other.MovementSpeed = 8; other.IssueMoveOrder(new Vector2(180, 158)); }

        _settlementInventory = GetTree().GetFirstNodeInGroup(SettlementInventory.GroupName) as SettlementInventory ?? throw new InvalidOperationException("SettlementInventory missing");
        _itemStorage = GetTree().GetFirstNodeInGroup(SettlementItemStorage.GroupName) as SettlementItemStorage ?? throw new InvalidOperationException("SettlementItemStorage missing");
        _startingFood = _settlementInventory.GetAmount(ResourceType.Food);
        _startingMaterials = _settlementInventory.GetAmount(ResourceType.Materials);
        _startingMedicine = _settlementInventory.GetAmount(ResourceType.Medicine);

        PackedScene stockpileScene = GD.Load<PackedScene>("res://scenes/buildings/Stockpile.tscn");
        _stockpile = stockpileScene.Instantiate<Stockpile>();
        _stockpile.Name = "ItemValidationStockpile";
        _stockpile.GridPosition = new Vector2(_building.Definition.Footprint.Position.X - 2.5f, _building.Definition.Footprint.End.Y + 2.5f);
        GetNode<Node2D>("../World/Objects").AddChild(_stockpile);

        _lastFirst = _first.SimulationPosition; _lastSecond = _second.SimulationPosition;
        _first.IssueMoveOrder(new Vector2(218.7f, 156.55f));
        _second.IssueMoveOrder(new Vector2(218.95f, 152.95f));
        GD.Print("ITEM_VALIDATION: physical approach started");
        Next(Phase.Approach);
    }

    private void RefreshInteractables()
    {
        _fridge = GetTree().GetNodesInGroup(InteriorContainerRuntime.GroupName).OfType<InteriorContainerRuntime>().First(c => c.Id == "fridge");
        _bathroom = GetTree().GetNodesInGroup(InteriorContainerRuntime.GroupName).OfType<InteriorContainerRuntime>().First(c => c.Id == "bathroom_cabinet");
    }

    private static bool LootMatches(System.Collections.Generic.IReadOnlyList<ItemStack> actual, ItemStack[] expected)
    {
        if (actual.Count != expected.Length) return false;
        var actualSorted = actual.OrderBy(s => s.ItemId).ToArray();
        var expectedSorted = expected.OrderBy(s => s.ItemId).ToArray();
        for (int i = 0; i < actualSorted.Length; i++)
            if (actualSorted[i].ItemId != expectedSorted[i].ItemId || actualSorted[i].Quantity != expectedSorted[i].Quantity) return false;
        return true;
    }

    private void CaptureIfRequested(string envVar, string label)
    {
        string? path = System.Environment.GetEnvironmentVariable(envVar);
        if (string.IsNullOrWhiteSpace(path)) return;
        StrategyCamera camera = GetNode<StrategyCamera>("../World/StrategyCamera");
        camera.CenterOnGridPosition(new Vector2(220, 155));
        camera.SetZoom(.92f);
        CapturePngAfterFrames(path, label);
    }

    private async void CapturePngAfterFrames(string path, string label)
    {
        for (int i = 0; i < 6; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Error error = GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print($"ITEM_VALIDATION_{label}: {error} {path}");
    }

    private void CaptureInventoryTab()
    {
        string? path = System.Environment.GetEnvironmentVariable("ASHWOOD_CAPTURE_INVENTORY_PNG");
        if (string.IsNullOrWhiteSpace(path)) return;
        GetNode<SurvivorSelectionController>("../SelectionController")?.DebugSelectOnly(_first);
        (GetTree().GetFirstNodeInGroup(GameHud.GroupName) as GameHud)?.DebugShowInventoryTab();
        StrategyCamera camera = GetNode<StrategyCamera>("../World/StrategyCamera");
        camera.CenterOnGridPosition(new Vector2(220, 155));
        camera.SetZoom(.92f);
        CapturePngAfterFrames(path, "INVENTORY_PNG");
    }

    private static bool At(Survivor survivor, Vector2 target) => survivor.SimulationPosition.DistanceTo(target) < .12f;
    private void Next(Phase phase) { _phase = phase; GD.Print($"ITEM_VALIDATION: {phase}"); }
    private void Fail(string reason) { GD.PrintErr($"ITEM_VALIDATION: FAIL ({reason}, phase={_phase})"); _phase = Phase.Complete; SetProcess(false); }
    private void Pass()
    {
        GD.Print("ITEM_VALIDATION: PASS (search=True, partial_take=True, take_all=True, no_duplicate_on_search=True, equip_no_duplicate=True, capacity_bonus=True, resource_relationship=True, tool_stays_item=True, persistence=True, reservation=True)");
        _phase = Phase.Complete; SetProcess(false);
    }
}
