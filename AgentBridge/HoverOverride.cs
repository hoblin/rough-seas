using System;
using System.Linq;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace AgentBridge;

/// <summary>Makes the client's own picking report <see cref="Target"/> as the entity the player looks at.</summary>
public sealed class HoverOverride : IDisposable
{
    private static Entity? target;
    private static object? clientWorld;
    private readonly Harmony harmony = new("agentbridge.hover");

    public HoverOverride(ICoreClientAPI capi)
    {
        clientWorld = capi.World;
        var postfix = new HarmonyMethod(typeof(HoverOverride), nameof(Postfix));
        foreach (var method in AccessTools.TypeByName("Vintagestory.Common.GameMain").GetMethods()
                     .Where(method => method.Name == "RayTraceForSelection" && method.GetParameters().Any(parameter => parameter.Name == "entitySelection")))
        {
            harmony.Patch(method, postfix: postfix);
        }
    }

    public Entity? Target
    {
        get => target;
        set => target = value;
    }

    public void Dispose()
    {
        harmony.UnpatchAll(harmony.Id);
        target = null;
    }

    private static void Postfix(object __instance, ref EntitySelection entitySelection)
    {
        if (target == null || !ReferenceEquals(__instance, clientWorld)) return;

        entitySelection = new EntitySelection
        {
            Entity = target,
            Position = target.Pos.XYZ,
            HitPosition = new Vec3d(0, target.SelectionBox.Y2 / 2, 0),
            Face = BlockFacing.UP
        };
    }
}
