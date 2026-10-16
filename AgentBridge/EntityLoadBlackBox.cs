using System;
using System.Linq;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace AgentBridge;

/// <summary>Logs which entity failed whenever loading one from bytes throws, then lets the exception go on.</summary>
public sealed class EntityLoadBlackBox : IDisposable
{
    private static ILogger? logger;
    private readonly Harmony harmony = new("agentbridge.blackbox");

    public EntityLoadBlackBox(ILogger log)
    {
        logger = log;
        harmony.Patch(
            AccessTools.Method(typeof(Entity), nameof(Entity.FromBytes), [typeof(System.IO.BinaryReader), typeof(bool)]),
            finalizer: new HarmonyMethod(typeof(EntityLoadBlackBox), nameof(Finalizer)));
    }

    public void Dispose()
    {
        harmony.UnpatchAll(harmony.Id);
        logger = null;
    }

    private static Exception? Finalizer(Exception? __exception, Entity __instance)
    {
        if (__exception != null)
        {
            logger?.Error(
                "Entity failed to load: {0} id {1} ({2}) on {3} at {4}; watched attributes: {5}",
                __instance.Code, __instance.EntityId, __instance.GetType().Name, __instance.World?.Side,
                __instance.Pos?.XYZ, string.Join(", ", __instance.WatchedAttributes.Select(pair => pair.Key)));
        }
        return __exception;
    }
}
