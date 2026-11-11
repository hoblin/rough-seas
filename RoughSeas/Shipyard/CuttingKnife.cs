using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace RoughSeas.Shipyard;

/// <summary>
/// A knife held on a vessel that wants a cut cuts her, as a held axe hews: vanilla's <c>ItemKnife</c> handles the
/// held step and its release itself and never asks the knife's behaviours, though it lets them take the click.
/// </summary>
[ReliesOn("Vintagestory.GameContent.ItemKnife", "OnHeldInteractStep", "handles the step itself and never asks the knife's collectible behaviours")]
[ReliesOn("Vintagestory.GameContent.ItemKnife", "OnHeldInteractStop", "handles the stop itself and never asks the knife's collectible behaviours")]
public static class CuttingKnife
{
    private const string HarmonyId = "roughseas.shipyard.cuttingknife";

    private static Harmony? harmony;

    public static void Attach()
    {
        if (harmony != null) return;
        harmony = new Harmony(HarmonyId);
        harmony.Patch(AccessTools.Method(typeof(ItemKnife), nameof(ItemKnife.OnHeldInteractStep)),
            prefix: new HarmonyMethod(typeof(CuttingKnife), nameof(Step)));
        harmony.Patch(AccessTools.Method(typeof(ItemKnife), nameof(ItemKnife.OnHeldInteractStop)),
            prefix: new HarmonyMethod(typeof(CuttingKnife), nameof(Stop)));
    }

    public static void Detach()
    {
        harmony?.UnpatchAll(HarmonyId);
        harmony = null;
    }

    private static bool Step(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref bool __result)
    {
        if (Cutting(slot, byEntity, blockSel, entitySel) is not { } cutting) return true;

        var handled = EnumHandling.PassThrough;
        __result = cutting.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel, ref handled);
        return false;
    }

    private static bool Stop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        if (slot.Itemstack?.Collectible.GetCollectibleBehavior<CollectibleBehaviorFraming>(true) is not { } cutting) return true;

        var handled = EnumHandling.PassThrough;
        cutting.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel, ref handled);
        return handled == EnumHandling.PassThrough;
    }

    private static CollectibleBehaviorFraming? Cutting(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel) =>
        CollectibleBehaviorFraming.WorksOn(slot, byEntity, blockSel, entitySel) ? slot.Itemstack?.Collectible.GetCollectibleBehavior<CollectibleBehaviorFraming>(true) : null;
}
