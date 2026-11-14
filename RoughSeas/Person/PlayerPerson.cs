using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;

namespace RoughSeas.Person;

/// <summary>
/// The player's mass. The inventory is a game convention, so what is weighed stops where the game draws the line: armour,
/// and in the hotbar what the item's own type calls a tool or weapon. The backpack and materials aren't weighed. It is
/// worked out when a slot changes and held until the next change. Another player's client is sent only the hotbar slot in
/// hand, so there the server's weighing, kept on the player, is read.
/// </summary>
public sealed class PlayerPerson(Body body, CarriedConfig config, Action<string>? log = null) : IPerson
{
    private const string CarriedKgKey = "roughseas:carriedKg";
    private const string CarriedLitresKey = "roughseas:carriedLitres";
    private static readonly string[] MaterialGroups = ["material", "metal"];

    private readonly ConcurrentDictionary<string, Kit> kits = new();
    private readonly Materials materials = new(config);

    public Body Body { get; } = body;

    public Carried CarriedBy(Entity entity)
    {
        if (entity is not EntityPlayer { Player: { } player }) return default;
        if (entity.World is IClientWorldAccessor { Player: var local } && local?.Entity != entity)
            return new Carried(entity.WatchedAttributes.GetDouble(CarriedKgKey), entity.WatchedAttributes.GetDouble(CarriedLitresKey));
        var kit = kits.GetOrAdd(player.PlayerUID, _ => new Kit());
        kit.Watch(player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName),
            player.InventoryManager.GetOwnInventory(GlobalConstants.hotBarInvClassName));
        if (!kit.Reweigh()) return kit.Held;

        var held = Weigh(kit.Worn.Where(slot => !slot.Empty).Select(slot => new Worn(WalkSpeedPenalty(slot), MaterialOf(slot))),
            kit.Hotbar.Where(slot => !slot.Empty).Select(slot => new Held(slot.Itemstack.Collectible.GetTool(slot), MaterialOf(slot))));
        if (held != kit.Held) log?.Invoke($"{player.PlayerName} carries {held.Kg:F1} kg, displacing {held.Litres:F2} L");
        kit.Held = held;
        if (entity.World.Side == EnumAppSide.Server)
        {
            entity.WatchedAttributes.SetDouble(CarriedKgKey, held.Kg);
            entity.WatchedAttributes.SetDouble(CarriedLitresKey, held.Litres);
        }
        return held;
    }

    public Carried Weigh(IEnumerable<Worn> worn, IEnumerable<Held> hotbar) =>
        worn.Aggregate(default(Carried), (sum, armour) => sum + Weigh(armour)) + hotbar.Aggregate(default(Carried), (sum, item) => sum + Weigh(item));

    private Carried Weigh(Worn armour) =>
        armour.WalkSpeedPenalty > 0 ? materials.Worn(armour.WalkSpeedPenalty * config.ArmourKgPerWalkSpeed, armour.Material) : default;

    private Carried Weigh(Held item)
    {
        if (item.Tool is not { } tool || !config.Tools.TryGetValue(tool.ToString().ToLowerInvariant(), out var parts)) return default;
        var (head, handle) = materials.Split(item.Material);
        return materials.Part(parts.Head, head) + (parts.Haft is { } haft ? materials.Part(haft, handle) : default);
    }

    private static string? MaterialOf(ItemSlot slot) =>
        MaterialGroups.Select(group => slot.Itemstack.Collectible.Variant.TryGetValue(group, out var material) ? material : null)
            .FirstOrDefault(material => material != null);

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
