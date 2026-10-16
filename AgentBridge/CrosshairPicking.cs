using System;
using HarmonyLib;

namespace AgentBridge;

/// <summary>
/// An unfocused client picks what lies under the desktop's mouse cursor; this makes it pick what lies under the
/// crosshair, as a focused one does, so the bench's aim decides what the player looks at.
/// </summary>
public sealed class CrosshairPicking : IDisposable
{
    private readonly Harmony harmony = new("agentbridge.crosshair");

    public CrosshairPicking()
    {
        harmony.Patch(AccessTools.Method("Vintagestory.Client.NoObf.PickingRayUtil:GetPickingRayByMouseCoordinates"),
            prefix: new HarmonyMethod(typeof(CrosshairPicking), nameof(Centre)));
    }

    public void Dispose() => harmony.UnpatchAll(harmony.Id);

    private static void Centre(object game)
    {
        var client = Traverse.Create(game);
        client.Field("MouseCurrentX").SetValue(client.Property("Width").GetValue<int>() / 2);
        client.Field("MouseCurrentY").SetValue(client.Property("Height").GetValue<int>() / 2);
    }
}
