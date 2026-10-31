using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RoughSeas.Shipyard;

/// <summary>
/// Dry grass laid with Shift on the middle log of a dugout's row, where vanilla would place a campfire's base, lays
/// the blank in place of the logs, with that grass as its fire's base.
/// </summary>
public static class FireOnLogs
{
    private const string HarmonyId = "roughseas.shipyard.fireonlogs";

    private static Harmony? harmony;

    public static void Attach()
    {
        if (harmony != null) return;
        harmony = new Harmony(HarmonyId);
        harmony.Patch(AccessTools.Method(typeof(ItemDryGrass), nameof(ItemDryGrass.OnHeldInteractStart)),
            prefix: new HarmonyMethod(typeof(FireOnLogs), nameof(Lay)));
    }

    public static void Detach()
    {
        harmony?.UnpatchAll(HarmonyId);
        harmony = null;
    }

    private static bool Lay(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, ref EnumHandHandling handHandling)
    {
        if (blockSel is not { Face: var face } || face != BlockFacing.UP || byEntity?.Controls.ShiftKey != true) return true;
        if (byEntity.World.BlockAccessor.GetBlock(blockSel.Position).GetBehavior<BlockBehaviorBlankLog>() is not { } log) return true;
        if (!log.TryLay(byEntity, blockSel.Position)) return true;

        if (byEntity.World.Side == EnumAppSide.Server)
        {
            itemslot.TakeOut(1);
            itemslot.MarkDirty();
        }
        handHandling = EnumHandHandling.PreventDefault;
        return false;
    }
}
