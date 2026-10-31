using System.Collections.Generic;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RoughSeas.Shipyard;

/// <summary>
/// Lighting a charcoal pit whose firewood stands on the middle log of a dugout's row lays the blank, its fire lit,
/// in place of the pit and the logs.
/// </summary>
public static class CharcoalPitOnLogs
{
    private const string HarmonyId = "roughseas.shipyard.charcoalpitonlogs";

    private static Harmony? harmony;

    public static void Attach()
    {
        if (harmony != null) return;
        harmony = new Harmony(HarmonyId);
        harmony.Patch(AccessTools.Method(typeof(BlockCharcoalPit), nameof(BlockCharcoalPit.OnTryIgniteBlockOver)),
            prefix: new HarmonyMethod(typeof(CharcoalPitOnLogs), nameof(Lay)));
    }

    public static void Detach()
    {
        harmony?.UnpatchAll(HarmonyId);
        harmony = null;
    }

    private static bool Lay(EntityAgent byEntity, BlockPos pos, ref EnumHandling handling)
    {
        var world = byEntity.World;
        var fire = new List<BlockPos> { pos.Copy() };
        var below = pos.DownCopy();
        while (BlockFirepit.IsFirewoodPile(world, below))
        {
            fire.Add(below.Copy());
            below.Down();
        }
        if (fire.Count < 2 || world.BlockAccessor.GetBlock(below).GetBehavior<BlockBehaviorBlankLog>() is not { } log) return true;
        if (!log.TryLay(byEntity, below, fire)) return true;

        handling = EnumHandling.PreventDefault;
        return false;
    }
}
