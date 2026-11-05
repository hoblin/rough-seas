using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace RoughSeas.Shipyard;

/// <summary>
/// A firestarter held on a vessel's laid fire kindles it, as a torch does: vanilla's <c>ItemFirestarter</c> handles
/// the held step and its release itself and never asks the firestarter's behaviours, though it lets them take the click.
/// </summary>
public static class KindlingFirestarter
{
    private const string HarmonyId = "roughseas.shipyard.kindlingfirestarter";

    private static Harmony? harmony;

    public static void Attach()
    {
        if (harmony != null) return;
        harmony = new Harmony(HarmonyId);
        harmony.Patch(AccessTools.Method(typeof(ItemFirestarter), nameof(ItemFirestarter.OnHeldInteractStep)),
            prefix: new HarmonyMethod(typeof(KindlingFirestarter), nameof(Step)));
        harmony.Patch(AccessTools.Method(typeof(ItemFirestarter), nameof(ItemFirestarter.OnHeldInteractStop)),
            prefix: new HarmonyMethod(typeof(KindlingFirestarter), nameof(Stop)));
    }

    public static void Detach()
    {
        harmony?.UnpatchAll(HarmonyId);
        harmony = null;
    }

    private static bool Step(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref bool __result)
    {
        if (Kindling(slot, entitySel) is not { } kindling) return true;

        var handled = EnumHandling.PassThrough;
        __result = kindling.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel, ref handled);
        return false;
    }

    private static bool Stop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        if (Kindling(slot, entitySel) is not { } kindling) return true;

        var handled = EnumHandling.PassThrough;
        kindling.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel, ref handled);
        return false;
    }

    private static CollectibleBehaviorKindle? Kindling(ItemSlot slot, EntitySelection entitySel) =>
        CollectibleBehaviorKindle.Lights(entitySel) ? slot.Itemstack?.Collectible.GetCollectibleBehavior<CollectibleBehaviorKindle>(true) : null;
}
