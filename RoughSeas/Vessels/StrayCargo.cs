using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace RoughSeas.Vessels;

/// <summary>
/// Cargo left in a slot that no longer takes it, such as a basket on a rower's seat from 1.0: unseen and out of
/// reach. It moves to a free slot that takes it, or goes over the side.
/// </summary>
public static class StrayCargo
{
    public static void Restow(Entity vessel)
    {
        if (vessel.GetBehavior<EntityBehaviorRideableAccessories>() is not { } hold) return;
        var stray = hold.Inventory.Where(slot => !slot.Empty && !slot.CanHold(slot)).ToList();
        if (stray.Count == 0) return;

        var log = vessel.Api.ModLoader.GetModSystem<VesselsModSystem>();
        foreach (var slot in stray)
        {
            var code = slot.Itemstack.Collectible.Code;
            var berth = hold.Inventory.FirstOrDefault(other => other.Empty && other.CanHold(slot));
            if (berth != null)
            {
                slot.TryPutInto(vessel.World, berth);
                log.Log(vessel, "{0} restowed from a slot that no longer takes it", code);
            }
            else
            {
                vessel.World.SpawnItemEntity(slot.TakeOutWhole(), vessel.Pos.XYZ);
                log.Log(vessel, "{0} put over the side: no slot takes it", code);
            }
        }
        hold.storeInv();
    }
}
