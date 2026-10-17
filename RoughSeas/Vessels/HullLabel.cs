using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace RoughSeas.Vessels;

/// <summary>
/// Leaves out the label vanilla's selection boxes write for a vessel's hull box: the hull's own line already names
/// it, with its condition.
/// </summary>
public static class HullLabel
{
    private const string HarmonyId = "roughseas.vessels.hulllabel";
    private const string HullBox = "HullAP";

    private static Harmony? harmony;

    public static void Attach()
    {
        if (harmony != null) return;
        harmony = new Harmony(HarmonyId);
        harmony.Patch(AccessTools.Method(typeof(EntityBehaviorSelectionBoxes), nameof(EntityBehaviorSelectionBoxes.GetInfoText)),
            prefix: new HarmonyMethod(typeof(HullLabel), nameof(Omit)));
    }

    public static void Detach()
    {
        harmony?.UnpatchAll(HarmonyId);
        harmony = null;
    }

    private static readonly System.Func<EntityBehaviorSelectionBoxes, int> HitIndex =
        AccessTools.MethodDelegate<System.Func<EntityBehaviorSelectionBoxes, int>>(AccessTools.Method(typeof(EntityBehaviorSelectionBoxes), "getHitIndex"));

    private static bool Omit(EntityBehaviorSelectionBoxes __instance)
    {
        if (__instance.entity.GetBehavior<EntityBehaviorVesselIntegrity>() == null) return true;

        int hit = HitIndex(__instance);
        return hit < 0 || __instance.selectionBoxes[hit].AttachPoint.Code != HullBox;
    }
}
