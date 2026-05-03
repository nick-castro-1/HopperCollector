using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Objects;
using SObject = StardewValley.Object;

namespace HopperCollector;

public sealed class ModEntry : Mod
{
    private const int TickInterval = 60;
    private int ticksSinceLastRun;
    private ModConfig Config = null!;

    public override void Entry(IModHelper helper)
    {
        Config = helper.ReadConfig<ModConfig>();
        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        var gmcm = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (gmcm is null)
        {
            Monitor.Log("Could not get GMCM API — mod entry will not appear in config menu.", LogLevel.Warn);
            return;
        }
        Monitor.Log("GMCM found, registering config.", LogLevel.Debug);

        gmcm.Register(
            mod: ModManifest,
            reset: () => Config = new ModConfig(),
            save: () => Helper.WriteConfig(Config));

        gmcm.AddBoolOption(
            mod: ModManifest,
            getValue: () => Config.IsEnabled,
            setValue: v => Config.IsEnabled = v,
            name: () => "Enable Hopper Collector",
            tooltip: () => "Hoppers automatically collect finished machine output into an adjacent chest.");
    }

    // -------------------------------------------------------------------------
    // Event handler
    // -------------------------------------------------------------------------

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (!Config.IsEnabled || !Context.IsWorldReady || !Context.IsMainPlayer) return;
        if (++ticksSinceLastRun < TickInterval) return;
        ticksSinceLastRun = 0;

        foreach (var location in GetAllLocations())
            ProcessLocation(location);
    }

    // -------------------------------------------------------------------------
    // Core logic
    // -------------------------------------------------------------------------

    private void ProcessLocation(GameLocation location)
    {
        var hoppers = location.objects.Pairs
            .Where(p => IsHopper(p.Value))
            .ToList();

        foreach (var (tile, hopper) in hoppers)
        {
            if (hopper is Chest chest)
                CollectFromAdjacentMachines(location, tile, chest);
        }
    }


    private void CollectFromAdjacentMachines(GameLocation location, Vector2 hopperTile, Chest hopper)
    {
        foreach (var machineTile in CardinalTiles(hopperTile))
        {
            if (!location.objects.TryGetValue(machineTile, out var machine)) continue;
            if (!IsMachine(machine)) continue;
            if (!machine.readyForHarvest.Value || machine.heldObject.Value is null) continue;

            var output = machine.heldObject.Value;

            if (!TryDepositIntoAdjacentChest(location, hopperTile, output)) continue;

            machine.heldObject.Value = null;
            machine.readyForHarvest.Value = false;
            machine.showNextIndex.Value = false;

            Monitor.Log(
                $"Hopper at {hopperTile} collected {output.Name} (x{output.Stack}) " +
                $"from {machine.Name} at {machineTile}.",
                LogLevel.Trace);

            TryRefeedMachine(location, hopperTile, hopper, machine);
        }
    }

    private static bool IsCoal(SObject obj)
    {
        return obj.QualifiedItemId == "(O)382" || obj.ParentSheetIndex == 382 || obj.Name == "Coal";
    }
    
    private static SObject CopyStack(SObject item)
    {
        var copy = (SObject)item.getOne();
        copy.Stack = item.Stack;
        return copy;
    }

    private void TryRefeedMachine(GameLocation location, Vector2 hopperTile, Chest hopper, SObject machine)
    {
        if (machine.heldObject.Value is not null || machine.minutesUntilReady.Value > 0)
            return;

        var farmer = new Farmer { currentLocation = location };

        for (int i = 0; i < hopper.Items.Count; i++)
        {
            if (hopper.Items[i] is not SObject stock)
                continue;

            if (IsCoal(stock))
                continue;

            if (stock.getOne() is not SObject input)
                continue;

            var probeInput = CopyStack(stock);

            SObject.autoLoadFrom = hopper.Items;

            try
            {
                if (!machine.performObjectDropInAction(probeInput, probe: true, who: farmer))
                    continue;

                machine.heldObject.Value = null;

                input = CopyStack(stock);
                int before = input.Stack;

                if (!machine.performObjectDropInAction(input, probe: false, who: farmer))
                    continue;

                int consumed = before - input.Stack;

                if (consumed <= 0)
                    consumed = 1;

                stock.Stack -= consumed;

                if (stock.Stack <= 0)
                    hopper.Items[i] = null;

                hopper.clearNulls();

                Monitor.Log(
                    $"Hopper at {hopperTile} re-fed {machine.Name} with {consumed}x {stock.Name}.",
                    LogLevel.Trace);

                return;
            }
            finally
            {
                SObject.autoLoadFrom = null;
            }
        }
    }


    private static bool TryDepositIntoAdjacentChest(GameLocation location, Vector2 hopperTile, SObject item)
    {
        foreach (var tile in SurroundingTiles(hopperTile))
        {
            if (!location.objects.TryGetValue(tile, out var obj)) continue;
            if (obj is not Chest chest) continue;

            // addItem returns null when the item was fully accepted.
            var leftover = chest.addItem(item);
            if (leftover is null) return true;
        }
        return false;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static bool IsHopper(SObject obj)
    {
        // Check both name and qualified item ID to cover SV 1.5 and 1.6.
        return obj.Name == "Hopper" || obj.QualifiedItemId == "(BC)Hopper";
    }

    private static bool IsMachine(SObject obj)
    {
        // Chests and hoppers are big craftables but not machines.
        if (obj is Chest) return false;
        if (obj.Name is "Hopper" or "Chest" or "Stone Chest" or "Mini-Fridge"
                       or "Mini Shipping Bin" or "Junimo Chest") return false;

        // All machines are big craftables; decorative big craftables don't set
        // readyForHarvest, so the caller's check handles that distinction.
        return obj.bigCraftable.Value;
    }

    // 4-directional — used for machine scanning (matches vanilla hopper behaviour).
    private static IEnumerable<Vector2> CardinalTiles(Vector2 tile)
    {
        yield return new Vector2(tile.X + 1, tile.Y);
        yield return new Vector2(tile.X - 1, tile.Y);
        yield return new Vector2(tile.X, tile.Y + 1);
        yield return new Vector2(tile.X, tile.Y - 1);
    }

    // 8-directional — used for chest scanning.
    private static IEnumerable<Vector2> SurroundingTiles(Vector2 tile)
    {
        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        {
            if (dx == 0 && dy == 0) continue;
            yield return new Vector2(tile.X + dx, tile.Y + dy);
        }
    }

    /// <summary>
    /// Iterates every loaded location, including building interiors, without
    /// visiting the same location twice.
    /// </summary>
    private static IEnumerable<GameLocation> GetAllLocations()
    {
        var seen = new HashSet<GameLocation>(ReferenceEqualityComparer.Instance);
        var queue = new Queue<GameLocation>(Game1.locations);

        while (queue.Count > 0)
        {
            var loc = queue.Dequeue();
            if (!seen.Add(loc)) continue;
            yield return loc;

            foreach (var building in loc.buildings)
            {
                if (building.indoors.Value is { } indoors)
                    queue.Enqueue(indoors);
            }
        }
    }
}
