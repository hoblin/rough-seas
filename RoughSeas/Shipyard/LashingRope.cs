using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace RoughSeas.Shipyard;

/// <summary>
/// Rope held on a canoe that wants lashing lashes her, where vanilla's rope would tie itself to her: vanilla's
/// <c>ItemRope</c> handles the click itself and never asks the rope's behaviours. A hold kept on past her last lashing
/// ties nothing: the game repeats a held use while the button stays down, and only a fresh click ties her.
/// </summary>
public static class LashingRope
{
    private const string HarmonyId = "roughseas.shipyard.lashingrope";

    private static Harmony? harmony;

    public static void Attach()
    {
        if (harmony != null) return;
        harmony = new Harmony(HarmonyId);
        harmony.Patch(AccessTools.Method(typeof(ItemRope), nameof(ItemRope.OnHeldInteractStart)),
            prefix: new HarmonyMethod(typeof(LashingRope), nameof(Lash)));
    }

    public static void Detach()
    {
        harmony?.UnpatchAll(HarmonyId);
        harmony = null;
    }

    private static bool Lash(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        if (!CollectibleBehaviorFraming.WorksOn(slot, byEntity, blockSel, entitySel)) return firstEvent || !CollectibleBehaviorFraming.Builds(entitySel);
        if (slot.Itemstack?.Collectible.GetCollectibleBehavior<CollectibleBehaviorFraming>(true) is not { } lashing) return true;

        var handled = EnumHandling.PassThrough;
        lashing.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling, ref handled);
        return false;
    }
}
