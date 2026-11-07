using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace RoughSeas.Shipyard;

/// <summary>
/// A firestarter held on a vessel's laid fire lights it: vanilla's <c>ItemFirestarter</c> handles the held step and
/// its release itself and never asks the firestarter's behaviours, though it lets them take the click.
/// </summary>
public static class LightingFirestarter
{
    private const string HarmonyId = "roughseas.shipyard.lightingfirestarter";

    private static Harmony? harmony;

    public static void Attach()
    {
        if (harmony != null) return;
        harmony = new Harmony(HarmonyId);
        harmony.Patch(AccessTools.Method(typeof(ItemFirestarter), nameof(ItemFirestarter.OnHeldInteractStep)),
            prefix: new HarmonyMethod(typeof(LightingFirestarter), nameof(Step)));
        harmony.Patch(AccessTools.Method(typeof(ItemFirestarter), nameof(ItemFirestarter.OnHeldInteractStop)),
            prefix: new HarmonyMethod(typeof(LightingFirestarter), nameof(Stop)));
    }

    public static void Detach()
    {
        harmony?.UnpatchAll(HarmonyId);
        harmony = null;
    }

    private static bool Step(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref bool __result)
    {
        if (Lighting(slot, entitySel) is not { } lighting) return true;

        var handled = EnumHandling.PassThrough;
        __result = lighting.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel, ref handled);
        return false;
    }

    private static bool Stop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        if (Lighting(slot, entitySel) is not { } lighting) return true;

        var handled = EnumHandling.PassThrough;
        lighting.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel, ref handled);
        return false;
    }

    private static CollectibleBehaviorLight? Lighting(ItemSlot slot, EntitySelection entitySel) =>
        CollectibleBehaviorLight.CanLight(entitySel) ? slot.Itemstack?.Collectible.GetCollectibleBehavior<CollectibleBehaviorLight>(true) : null;
}
