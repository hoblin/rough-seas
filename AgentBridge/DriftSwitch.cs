using System;
using HarmonyLib;
using RoughSeas.Storms;
using Vintagestory.API.MathTools;

namespace AgentBridge;

/// <summary>
/// Skips every way into Rough Seas' <see cref="EntityBehaviorCurrentDrift"/> while <see cref="Drifting"/> is off, so
/// vessels stay where a test put them: its tick, its keeping the set through a move and a rower's overwrite, its pull on
/// a mooring and the swing about the bow.
/// </summary>
public sealed class DriftSwitch : IDisposable
{
    private static bool drifting;
    private readonly Harmony harmony = new("agentbridge.drift");

    public DriftSwitch()
    {
        var prefix = new HarmonyMethod(typeof(DriftSwitch), nameof(Prefix));
        foreach (var method in new[]
                 {
                     nameof(EntityBehaviorCurrentDrift.OnGameTick), "Rowed",
                     nameof(EntityBehaviorCurrentDrift.Pull), nameof(EntityBehaviorCurrentDrift.HeldAtBow)
                 })
        {
            harmony.Patch(AccessTools.Method(typeof(EntityBehaviorCurrentDrift), method), prefix: prefix);
        }
        harmony.Patch(AccessTools.Method(typeof(EntityBehaviorCurrentDrift), "BeforeMove"),
            prefix: new HarmonyMethod(typeof(DriftSwitch), nameof(BeforeMove)));
    }

    public bool Drifting
    {
        get => drifting;
        set => drifting = value;
    }

    public void Dispose()
    {
        harmony.UnpatchAll(harmony.Id);
        drifting = false;
    }

    private static bool Prefix() => drifting;

    // a driving hull's motion meets no water drag, so the set left in it would carry her on for good
    private static bool BeforeMove(EntityBehaviorCurrentDrift __instance, Vec3d motion)
    {
        if (!drifting) __instance.Stopped(motion);
        return drifting;
    }
}
