using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.GameContent;

namespace RoughSeas.Storms;

public static class VesselMotion
{
    private const string HarmonyId = "roughseas.storms.motion";

    private static Harmony? harmony;

    public static void Attach()
    {
        if (harmony != null) return;
        harmony = new Harmony(HarmonyId);
        harmony.Patch(AccessTools.Method(typeof(EntityBoat), nameof(EntityBoat.OnRenderFrame)),
            postfix: new HarmonyMethod(typeof(VesselMotion), nameof(Respond)));
    }

    public static void Detach()
    {
        harmony?.UnpatchAll(HarmonyId);
        harmony = null;
    }

    private static void Respond(EntityBoat __instance)
    {
        if (__instance.Api is ICoreClientAPI { IsGamePaused: true }) return;
        var forces = __instance.GetBehavior<EntityBehaviorSeaForces>();
        if (forces == null || __instance.Properties.Client.Renderer is not EntityShapeRenderer renderer) return;

        var seaworthiness = forces.Seaworthiness;
        renderer.xangle += __instance.mountAngle.X * (float)(seaworthiness.RollResponse - 1);
        renderer.zangle += __instance.mountAngle.Z * (float)(seaworthiness.PitchResponse - 1);
    }
}
