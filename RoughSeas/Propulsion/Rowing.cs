using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RoughSeas.Propulsion;

[ReliesOn("Vintagestory.GameContent.EntityBoat", "SeatsToMotion", "returns the motion target that updateBoatAngleAndMotion eases ForwardSpeed toward")]
public static class Rowing
{
    private const string HarmonyId = "roughseas.propulsion.rowing";

    private static readonly object patchLock = new();
    private static int attached;
    private static Harmony? harmony;

    public static void Attach()
    {
        lock (patchLock)
        {
            if (attached++ > 0) return;
            harmony = new Harmony(HarmonyId);
            harmony.Patch(AccessTools.Method(typeof(EntityBoat), nameof(EntityBoat.SeatsToMotion)),
                postfix: new HarmonyMethod(typeof(Rowing), nameof(Rowed)));
            harmony.Patch(AccessTools.Method(typeof(EntityBehaviorPassivePhysics), nameof(EntityBehaviorPassivePhysics.MotionAndCollision)),
                prefix: new HarmonyMethod(typeof(Rowing), nameof(Swum)));
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

    private static void Rowed(EntityBoat __instance, ref Vec2d __result)
    {
        if (__instance.GetBehavior<EntityBehaviorPropulsion>() is { } propulsion) __result = propulsion.Drive(__instance, __result);
    }

    private static void Swum(EntityBehaviorPassivePhysics __instance, EntityPos pos, float dt, bool ___feetInLiquidBefore, bool ___swimmingBefore)
    {
        if ((___feetInLiquidBefore || ___swimmingBefore) && __instance.Entity is EntityBoat boat &&
            boat.GetBehavior<EntityBehaviorPropulsion>() is { Driving: true })
        {
            VanillaSwim.LeaveWaterDragOnHeave(pos.Motion, __instance.WaterDragValue, dt);
        }
    }
}
