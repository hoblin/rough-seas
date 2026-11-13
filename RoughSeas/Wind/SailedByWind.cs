using HarmonyLib;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RoughSeas.Wind;

[ReliesOn("Vintagestory.GameContent.EntityBoat", "SeatsToMotion", "adds a set sail's thrust, sailPosition · 1.5, to the linear motion it returns, and nothing else while the sail is set")]
public static class SailedByWind
{
    private const string HarmonyId = "roughseas.wind.sailed";
    private const string SailKey = "sailPosition";

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
                postfix: new HarmonyMethod(typeof(SailedByWind), nameof(Sailed)));
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

    private static void Sailed(EntityBoat __instance, ref Vec2d __result)
    {
        if (__instance.WatchedAttributes.GetInt(SailKey) > 0 && __instance.GetBehavior<EntityBehaviorWindage>()?.Sail != null) __result = new Vec2d(0, __result.Y);
    }
}
