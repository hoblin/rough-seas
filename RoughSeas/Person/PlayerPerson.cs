using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;

namespace RoughSeas.Person;

/// <summary>
/// The player's mass. The inventory is a game convention, so what is weighed stops where the game draws the line: armour,
/// and in the hotbar what the item's own type calls a tool or weapon. The backpack and materials aren't weighed. It is
/// worked out when a slot changes and held until the next change.
/// </summary>
public sealed class PlayerPerson(Body body, CarriedConfig config, Action<string>? log = null) : IPerson
{
    private readonly ConcurrentDictionary<string, Kit> kits = new();

    public Body Body { get; } = body;

    public Carried CarriedBy(Entity entity)
    {
        if (entity is not EntityPlayer { Player: { } player }) return default;
        var kit = kits.GetOrAdd(player.PlayerUID, _ => new Kit());
        kit.Watch(player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName),
            player.InventoryManager.GetOwnInventory(GlobalConstants.hotBarInvClassName));
        if (!kit.Reweigh()) return kit.Held;

        kit.Held = Weigh(kit.Worn.Select(WalkSpeedPenalty), kit.Hotbar.Select(slot => slot.Itemstack?.Collectible.GetTool(slot)));
        log?.Invoke($"{player.PlayerName} carries {kit.Held.Kg:F1} kg, displacing {kit.Held.Litres:F2} L");
        return kit.Held;
    }

    /// <summary>What armour slowing a body by these shares of its walking speed, and hotbar slots holding these types of tool, come to.</summary>
    public Carried Weigh(IEnumerable<double> walkSpeedPenalties, IEnumerable<EnumTool?> hotbar)
    {
        var kg = walkSpeedPenalties.Sum() * config.ArmourKgPerWalkSpeed + hotbar.Sum(ToolKg);
        return new Carried(kg, kg / config.DensityKgPerLitre);
    }

    private double ToolKg(EnumTool? tool) =>
        tool is { } type && config.ToolKg.TryGetValue(type.ToString().ToLowerInvariant(), out var kg) ? kg : 0;

    private static double WalkSpeedPenalty(ItemSlot slot) =>
        Math.Max(0, -(slot.Itemstack?.ItemAttributes?["statModifiers"]["walkSpeed"].AsFloat() ?? 0));

    private sealed class Kit
    {
        private IInventory? worn, hotbar;
        private volatile bool changed = true;

        public Carried Held { get; set; }

        public IEnumerable<ItemSlot> Worn => worn ?? Enumerable.Empty<ItemSlot>();

        public IEnumerable<ItemSlot> Hotbar => hotbar ?? Enumerable.Empty<ItemSlot>();

        public void Watch(IInventory? wornNow, IInventory? hotbarNow)
        {
            if (wornNow != worn) Listen(worn = wornNow);
            if (hotbarNow != hotbar) Listen(hotbar = hotbarNow);
        }

        public bool Reweigh()
        {
            if (!changed) return false;
            changed = false;
            return true;
        }

        private void Listen(IInventory? inventory)
        {
            changed = true;
            if (inventory is InventoryBase slots) slots.SlotModified += _ => changed = true;
        }
    }
}
