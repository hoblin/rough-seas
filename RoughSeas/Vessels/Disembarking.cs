using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace RoughSeas.Vessels;

/// <summary>
/// Keeps a steered vessel's interpolated position where her steersman's client moves her: while it steers, the server sends
/// it none of hers, and the frames after it lets go are drawn from that position.
/// </summary>
[ReliesOn("EntityBehaviorInterpolatePosition", "OnRenderFrame", "replays its last snapshot on the first frame without a controller")]
public static class Disembarking
{
    private const string HarmonyId = "roughseas.vessels.disembarking";

    private static Harmony? harmony;

    public static void Attach()
    {
        if (harmony != null) return;
        harmony = new Harmony(HarmonyId);
        harmony.Patch(AccessTools.Method(typeof(EntityBehaviorInterpolatePosition), nameof(EntityBehaviorInterpolatePosition.OnRenderFrame)),
            prefix: new HarmonyMethod(typeof(Disembarking), nameof(KeepWhereSteered)));
    }

    public static void Detach()
    {
        harmony?.UnpatchAll(HarmonyId);
        harmony = null;
    }

    private static void KeepWhereSteered(EntityBehaviorInterpolatePosition __instance)
    {
        if (__instance.entity is not EntityBoat { Api: ICoreClientAPI capi } boat) return;
        if (boat.GetInterface<IMountable>()?.Controller != capi.World.Player.Entity) return;

        var handling = EnumHandling.Handled;
        __instance.OnReceivedServerPos(true, ref handling);
    }
}
