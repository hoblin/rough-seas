using HarmonyLib;
using Vintagestory.GameContent;

namespace RoughSeas.Storms;

/// <summary>
/// Vanilla's <c>EntityBoat.updateBoatAngleAndMotion</c> sets a boat's motion from her forward speed, on the server for
/// a boat nobody steers and on the helm's client for a steered one. Right after it, on both sides, her
/// <see cref="EntityBehaviorCurrentDrift"/> moves her through the water from the motion she had before.
/// </summary>
public static class HullDrift
{
    private const string HarmonyId = "roughseas.storms.hulldrift";

    private static readonly object patchLock = new();
    private static int attached;
    private static Harmony? harmony;

    public static void Attach()
    {
        lock (patchLock)
        {
            if (attached++ > 0) return;
            harmony = new Harmony(HarmonyId);
            harmony.Patch(AccessTools.Method(typeof(EntityBoat), "updateBoatAngleAndMotion"),
                prefix: new HarmonyMethod(typeof(HullDrift), nameof(Before)),
                postfix: new HarmonyMethod(typeof(HullDrift), nameof(After)));
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

    private static void Before(EntityBoat __instance, out (double X, double Z) __state) =>
        __state = (__instance.Pos.Motion.X, __instance.Pos.Motion.Z);

    private static void After(EntityBoat __instance, float dt, (double X, double Z) __state) =>
        __instance.GetBehavior<EntityBehaviorCurrentDrift>()?.Step(__instance, dt, __state);
}
