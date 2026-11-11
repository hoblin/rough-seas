using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace RoughSeas.Shipyard;

/// <summary>
/// A ground pile takes the click before the held item (<c>PlacedPriorityInteract</c>), and rope is ground-storable, so
/// rope on a pile of laths would take a lath: a pile that the held rope binds into a vessel leaves the click to the rope.
/// </summary>
public static class BindingPiles
{
    private const string HarmonyId = "roughseas.shipyard.bindingpiles";

    private static Harmony? harmony;

    public static void Attach()
    {
        if (harmony != null) return;
        harmony = new Harmony(HarmonyId);
        harmony.Patch(AccessTools.Method(typeof(BlockEntityGroundStorage), nameof(BlockEntityGroundStorage.OnPlayerInteractStart)),
            prefix: new HarmonyMethod(typeof(BindingPiles), nameof(Before)));
    }

    public static void Detach()
    {
        harmony?.UnpatchAll(HarmonyId);
        harmony = null;
    }

    [ReliesOn("Vintagestory.GameContent.BlockEntityGroundStorage", "OnPlayerInteractStart", "a ground pile takes a click before the held item, with a ground-storable item in hand")]
    private static bool Before(BlockEntityGroundStorage __instance, IPlayer player, ref bool __result)
    {
        if (LathPile.At(__instance.Api.World, __instance.Pos)?.WorkWith(player.InventoryManager.ActiveHotbarSlot) == null) return true;

        __result = false;
        return false;
    }
}
