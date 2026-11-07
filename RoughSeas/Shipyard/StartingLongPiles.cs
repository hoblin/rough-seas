using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace RoughSeas.Shipyard;

/// <summary>
/// Every new ground pile, from a held item or from a click on another pile, starts in
/// <see cref="BlockGroundStorage.CreateStorage"/>, which asks the held item's behaviours nothing: a pile of a
/// <see cref="CollectibleBehaviorLongPile"/> starts only where its length has room, and gets its fillers once it has.
/// </summary>
public static class StartingLongPiles
{
    private const string HarmonyId = "roughseas.shipyard.startinglongpiles";

    private static Harmony? harmony;

    public static void Attach()
    {
        if (harmony != null) return;
        harmony = new Harmony(HarmonyId);
        harmony.Patch(AccessTools.Method(typeof(BlockGroundStorage), nameof(BlockGroundStorage.CreateStorage)),
            prefix: new HarmonyMethod(typeof(StartingLongPiles), nameof(Before)),
            postfix: new HarmonyMethod(typeof(StartingLongPiles), nameof(After)));
    }

    public static void Detach()
    {
        harmony?.UnpatchAll(HarmonyId);
        harmony = null;
    }

    private static bool Before(BlockSelection blockSel, IPlayer player, ref bool __result)
    {
        if (Pile(player) is not { } pile || pile.MayStart(player.Entity, blockSel)) return true;

        __result = false;
        return false;
    }

    private static void After(IWorldAccessor world, BlockSelection blockSel, IPlayer player, bool __result)
    {
        if (!__result || world.Side != EnumAppSide.Server || Pile(player) is not { } pile) return;

        var at = blockSel.Face == null ? blockSel.Position : blockSel.Position.AddCopy(blockSel.Face);
        if (world.BlockAccessor.GetBlockEntity(at) is BlockEntityGroundStorage storage) pile.Fill(world, storage);
    }

    private static CollectibleBehaviorLongPile? Pile(IPlayer player) =>
        player.InventoryManager.ActiveHotbarSlot.Itemstack?.Collectible.GetCollectibleBehavior<CollectibleBehaviorLongPile>(true);
}
