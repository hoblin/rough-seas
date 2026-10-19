using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace RoughSeas.Vessels;

public class ImpactMass
{
    public double PlayerGearKg;
    public double ArmorKgPerWalkSpeed;
    public Dictionary<string, double> ContainerKg = [];

    public double Of(Entity vessel)
    {
        var seats = vessel.GetBehavior<EntityBehaviorSeatable>()?.Seats ?? [];
        var accessories = vessel.GetBehavior<EntityBehaviorRideableAccessories>()?.Inventory ?? Enumerable.Empty<ItemSlot>();
        return vessel.Properties.Weight
            + seats.Sum(seat => seat.Passenger is { } passenger ? Passenger(passenger) : 0)
            + accessories.Sum(slot => slot.Itemstack is { } stack ? Container(stack) : 0)
            + vessel.SidedProperties.Behaviors.OfType<IVesselLoad>().Sum(load => load.ImpactKilograms);
    }

    private double Passenger(Entity passenger)
    {
        if (passenger is not EntityPlayer { Player: { } player }) return passenger.Properties.Weight;

        var worn = player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName) ?? Enumerable.Empty<ItemSlot>();
        double slowedBy = worn
            .Select(slot => slot.Itemstack?.ItemAttributes?["statModifiers"]["walkSpeed"].AsFloat() ?? 0)
            .Sum(walkSpeed => Math.Max(0, -walkSpeed));
        return passenger.Properties.Weight + PlayerGearKg + slowedBy * ArmorKgPerWalkSpeed;
    }

    private double Container(ItemStack stack) =>
        IAttachableToEntity.FromCollectible(stack.Collectible)?.GetCategoryCode(stack) is { } category
            ? ContainerKg.GetValueOrDefault(category)
            : 0;
}
