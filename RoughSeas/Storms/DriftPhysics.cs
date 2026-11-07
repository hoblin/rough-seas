using HarmonyLib;
using RoughSeas.SeaAndWeather;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace RoughSeas.Storms;

/// <summary>
/// Keeps a vessel's set in her motion through vanilla's physics: <see cref="HullSet"/> does the sums, and this patch only
/// tells it when. Before each of vanilla's moves, after it, and after <see cref="EntityBoat"/> writes her motion over
/// from her forward speed.
/// </summary>
public static class DriftPhysics
{
    private const string HarmonyId = "roughseas.storms.drift";

    private static readonly object patchLock = new();
    private static int attached;
    private static Harmony? harmony;

    public static void Attach()
    {
        lock (patchLock)
        {
            if (attached++ > 0) return;
            harmony = new Harmony(HarmonyId);
            harmony.Patch(AccessTools.Method(typeof(EntityBehaviorPassivePhysics), nameof(EntityBehaviorPassivePhysics.MotionAndCollision)),
                prefix: new HarmonyMethod(typeof(DriftPhysics), nameof(BeforeMove)),
                postfix: new HarmonyMethod(typeof(DriftPhysics), nameof(AfterMove)));
            harmony.Patch(AccessTools.Method(typeof(EntityBoat), "updateBoatAngleAndMotion"),
                postfix: new HarmonyMethod(typeof(DriftPhysics), nameof(Rowed)));
        }
    }

    public static void Detach()
    {
        lock (patchLock)
        {
            if (--attached > 0 || harmony == null) return;
            harmony.UnpatchAll(HarmonyId);
            harmony = null;
        }
    }

    private static void BeforeMove(EntityBehaviorPassivePhysics __instance, EntityPos pos, float dt, bool ___feetInLiquidBefore, bool ___swimmingBefore)
    {
        if (__instance.Entity is not EntityBoat boat || boat.GetBehavior<EntityBehaviorCurrentDrift>() is not { } drift) return;

        var wet = ___feetInLiquidBefore || ___swimmingBefore;
        var drag = wet ? __instance.WaterDragValue : __instance.AirDragValue;
        drift.BeforeMove(pos.Motion, dt, drag, wet, boat.Swimming && boat.ApplyGravity, dt * WaterDrag.TicksPerSecond);
    }

    private static void AfterMove(EntityBehaviorPassivePhysics __instance, EntityPos pos)
    {
        if (__instance.Entity is EntityBoat boat) boat.GetBehavior<EntityBehaviorCurrentDrift>()?.AfterMove(pos.Motion);
    }

    private static void Rowed(EntityBoat __instance)
    {
        if (__instance.ForwardSpeed != 0 && __instance.Swimming) __instance.GetBehavior<EntityBehaviorCurrentDrift>()?.Rowed(__instance.Pos.Motion);
    }
}
