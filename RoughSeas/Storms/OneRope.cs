using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace RoughSeas.Storms;

/// <summary>A vessel is tied by one rope: vanilla's rope-tieable entity takes another only while she has none.</summary>
public static class OneRope
{
    private const string HarmonyId = "roughseas.storms.onerope";
    private const string ErrorCode = "vessel-one-rope";

    private static readonly object patchLock = new();
    private static int attached;
    private static Harmony? harmony;

    public static void Attach()
    {
        lock (patchLock)
        {
            if (attached++ > 0) return;
            harmony = new Harmony(HarmonyId);
            harmony.Patch(AccessTools.Method(typeof(EntityBehaviorRopeTieable), nameof(EntityBehaviorRopeTieable.CanAttach)),
                postfix: new HarmonyMethod(typeof(OneRope), nameof(Tied)));
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

    private static void Tied(EntityBehaviorRopeTieable __instance, ref bool __result)
    {
        if (!__result || __instance.entity.GetBehavior<EntityBehaviorMooring>() is not { TakesRope: false }) return;

        __result = false;
        (__instance.entity.Api as ICoreClientAPI)?.TriggerIngameError(__instance, ErrorCode, Lang.Get("roughseas:" + ErrorCode));
    }
}
