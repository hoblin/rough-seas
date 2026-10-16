using System;
using System.Linq;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace AgentBridge;

/// <summary>Makes the client's own picking report <see cref="Target"/> or <see cref="Block"/> as what the player looks at.</summary>
public sealed class HoverOverride : IDisposable
{
    private static Entity? target;
    private static BlockPos? block;
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

    public BlockPos? Block
    {
        get => block;
        set => block = value;
    }

    public void Dispose()
    {
        harmony.UnpatchAll(harmony.Id);
        target = null;
        block = null;
    }

    private static void Postfix(object __instance, ref BlockSelection? blockSelection, ref EntitySelection? entitySelection)
    {
        if (!ReferenceEquals(__instance, clientWorld)) return;

        if (block != null)
        {
            blockSelection = new BlockSelection(block, BlockFacing.UP, ((IWorldAccessor)clientWorld).BlockAccessor.GetBlock(block));
            entitySelection = null;
            return;
        }
        if (target == null) return;

        blockSelection = null;
        entitySelection = new EntitySelection
        {
            Entity = target,
            Position = target.Pos.XYZ,
            HitPosition = new Vec3d(0, target.SelectionBox.Y2 / 2, 0),
            Face = BlockFacing.UP
        };
    }
}
