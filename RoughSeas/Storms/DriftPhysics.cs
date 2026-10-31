using System;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace RoughSeas.Storms;

/// <summary>
/// Keeps a vessel's set in her motion through vanilla's physics: each move restores what vanilla's water drag takes
/// from it, its fixed damping and the swimming drag it adds at speed alike (both mirror <c>MotionAndCollision</c>), a collision that stops an axis stops the set on it, and the rower's motion that <see cref="EntityBoat"/>
/// writes over hers gets it back.
/// </summary>
public static class DriftPhysics
{
    private const string HarmonyId = "roughseas.storms.drift";
    private const double DragTicksPerSecond = 33;
    private const double TicksPerSecond = 60;
    private const double SwimDragPerBlock = 10;
    private const double SwimDragOffset = 0.02;
    private const double MaxSwimDrag = 1.25;

    private static readonly object patchLock = new();
    private static int attached;
    private static Harmony? harmony;

    public static void Attach()
    {
        lock (patchLock)
        {
            if (attached++ > 0) return;
            harmony = new Harmony(HarmonyId);
            var move = AccessTools.Method(typeof(EntityBehaviorPassivePhysics), nameof(EntityBehaviorPassivePhysics.MotionAndCollision));
            harmony.Patch(move, prefix: new HarmonyMethod(typeof(DriftPhysics), nameof(BeforeMove)), postfix: new HarmonyMethod(typeof(DriftPhysics), nameof(AfterMove)));
            harmony.Patch(AccessTools.Method(typeof(EntityBoat), "updateBoatAngleAndMotion"), postfix: new HarmonyMethod(typeof(DriftPhysics), nameof(Rowed)));
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
        if (__instance.entity is not EntityBoat boat || boat.GetBehavior<CurrentDrift>() is not { } drift) return;

        var damping = ___feetInLiquidBefore || ___swimmingBefore ? Math.Pow(__instance.WaterDragValue, dt * DragTicksPerSecond) : 1;
        var swimDrag = boat.Swimming && boat.ApplyGravity
            ? Math.Clamp(SwimDragPerBlock * pos.Motion.Length() * damping * TicksPerSecond * dt - SwimDragOffset, 1, MaxSwimDrag)
            : 1;
        drift.BeforeMove(pos.Motion, dt, damping / swimDrag);
    }

    private static void AfterMove(EntityBehaviorPassivePhysics __instance, EntityPos pos)
    {
        if (__instance.entity is EntityBoat boat) boat.GetBehavior<CurrentDrift>()?.AfterMove(pos.Motion);
    }

    private static void Rowed(EntityBoat __instance)
    {
        if (__instance.ForwardSpeed != 0 && __instance.Swimming) __instance.GetBehavior<CurrentDrift>()?.Rowed(__instance.Pos.Motion);
    }
}
