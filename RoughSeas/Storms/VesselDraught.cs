using HarmonyLib;
using Vintagestory.GameContent;

namespace RoughSeas.Storms;

/// <summary>Vanilla floats a boat at a fixed line; here the water aboard settles her lower, on both sides.</summary>
public static class VesselDraught
{
    private const string HarmonyId = "roughseas.storms.draught";

    private static readonly object patchLock = new();
    private static int attached;
    private static Harmony? harmony;

    public static void Attach()
    {
        lock (patchLock)
        {
            if (attached++ > 0) return;
            harmony = new Harmony(HarmonyId);
            harmony.Patch(AccessTools.PropertyGetter(typeof(EntityBoat), nameof(EntityBoat.SwimmingOffsetY)),
                postfix: new HarmonyMethod(typeof(VesselDraught), nameof(Settle)));
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

    private static void Settle(EntityBoat __instance, ref double __result)
    {
        if (__instance.GetBehavior<EntityBehaviorSeaForces>() is { } forces) __result -= forces.SettlingMetres;
    }
}
