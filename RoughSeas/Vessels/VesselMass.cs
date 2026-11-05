using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace RoughSeas.Vessels;

public class VesselMass
{
    public const double SeawaterKgPerLitre = 1.025;

    public double PlayerGearKg;
    public double ArmorKgPerWalkSpeed;
    public double CrewKg;
    public Dictionary<string, double> ContainerKg = [];

    public Aboard Of(Entity vessel, double hullKg, int awaitedRiders = 0)
    {
        var accessories = vessel.GetBehavior<EntityBehaviorRideableAccessories>()?.Inventory ?? Enumerable.Empty<ItemSlot>();
        var hold = vessel.SidedProperties.Behaviors.OfType<IVesselHold>().FirstOrDefault();
        double water = (hold?.Litres ?? 0) * SeawaterKgPerLitre;
        double fill = hold is { CapacityLitres: > 0 } ? hold.Litres / hold.CapacityLitres : 0;
        var passengers = (vessel.GetBehavior<EntityBehaviorSeatable>()?.Seats ?? []).Select(seat => seat.Passenger).OfType<Entity>().ToList();
        return new Aboard(
            hullKg,
            passengers.Sum(passenger => passenger.Properties.Weight) + awaitedRiders * (CrewKg - PlayerGearKg),
            passengers.Sum(Kit) + awaitedRiders * PlayerGearKg,
            accessories.Sum(slot => slot.Itemstack is { } stack ? Container(stack) : 0),
            water,
            water * fill);
    }

    /// <summary>
    /// The riders her saved seats name. Read as she initialises: vanilla rewrites the record from the seats as each saved
    /// rider is seated again, dropping those whose entities haven't loaded yet.
    /// </summary>
    public static IEnumerable<long> Recorded(Entity vessel) =>
        vessel.WatchedAttributes["seatdata"] is TreeAttribute seats
            ? seats.Values.OfType<TreeAttribute>().Select(seat => seat.GetLong("passenger")).Where(id => id != 0)
            : [];

    private double Kit(Entity passenger)
    {
        if (passenger is not EntityPlayer { Player: { } player }) return 0;

        var worn = player.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName) ?? Enumerable.Empty<ItemSlot>();
        double slowedBy = worn
            .Select(slot => slot.Itemstack?.ItemAttributes?["statModifiers"]["walkSpeed"].AsFloat() ?? 0)
            .Sum(walkSpeed => Math.Max(0, -walkSpeed));
        return PlayerGearKg + slowedBy * ArmorKgPerWalkSpeed;
    }

    private double Container(ItemStack stack) =>
        IAttachableToEntity.FromCollectible(stack.Collectible)?.GetCategoryCode(stack) is { } category
            ? ContainerKg.GetValueOrDefault(category)
            : 0;
}

public readonly record struct Aboard(double Hull, double Bodies, double Kit, double Containers, double Water, double CoupledWater)
{
    public double Total => Hull + Bodies + Kit + Containers + Water;

    public double Impact => Hull + Bodies + Kit + Containers + CoupledWater;

    /// <summary>What the water does not hold up once she is awash: a rider's body is about as dense as seawater.</summary>
    public double Sinking => Hull + Kit + Containers;
}
