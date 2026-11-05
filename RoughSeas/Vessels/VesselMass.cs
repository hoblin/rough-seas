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
    public double SitDownSeconds;
    public double StandUpSeconds;

    /// <summary>Built as the vessel initialises, while vanilla's saved <c>seatdata</c> still names who was aboard when she was loaded.</summary>
    public Seating Seating(Entity vessel) => new(SitDownSeconds, StandUpSeconds, Recorded(vessel));

    private static IEnumerable<long> Recorded(Entity vessel) =>
        vessel.WatchedAttributes["seatdata"] is TreeAttribute seats
            ? seats.Values.OfType<TreeAttribute>().Select(seat => seat.GetLong("passenger")).Where(id => id != 0).ToList()
            : [];

    public static IEnumerable<Entity> Passengers(Entity vessel) =>
        (vessel.GetBehavior<EntityBehaviorSeatable>()?.Seats ?? []).Select(seat => seat.Passenger).OfType<Entity>();

    public Aboard Of(Entity vessel, double hullKg, Seating? seating = null)
    {
        var accessories = vessel.GetBehavior<EntityBehaviorRideableAccessories>()?.Inventory ?? Enumerable.Empty<ItemSlot>();
        var hold = vessel.SidedProperties.Behaviors.OfType<IVesselHold>().FirstOrDefault();
        double water = (hold?.Litres ?? 0) * SeawaterKgPerLitre;
        double fill = hold is { CapacityLitres: > 0 } ? hold.Litres / hold.CapacityLitres : 0;
        var seated = Passengers(vessel).Select(passenger => new Person(passenger.EntityId, passenger.Properties.Weight, Kit(passenger)));
        var people = seating?.Bear(seated, vessel.World.ElapsedMilliseconds / 1000.0) ?? seated.ToList();
        return new Aboard(
            hullKg,
            people.Sum(person => person.BodyKg),
            people.Sum(person => person.KitKg),
            accessories.Sum(slot => slot.Itemstack is { } stack ? Container(stack) : 0),
            water,
            water * fill);
    }

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
