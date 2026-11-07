using System;
using HarmonyLib;
using RoughSeas.Storms;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace AgentBridge;

/// <summary>
/// Skips every way into Rough Seas' <see cref="EntityBehaviorCurrentDrift"/> and the waves' carry on a hull nobody steers while
/// <see cref="Drifting"/> is off, so vessels stay where a test put them: its tick, its keeping the set through a move and a
/// rower's overwrite, its pull on a mooring and the swing about the bow. With <see cref="WavesOnly"/> it skips the current
/// alone, to see what the waves do by themselves.
/// </summary>
public sealed class DriftSwitch : IDisposable
{
    private static bool drifting;
    private static bool wavesOnly;
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
        harmony.Patch(AccessTools.Method(typeof(WaveRide), nameof(WaveRide.Carry)),
            prefix: new HarmonyMethod(typeof(DriftSwitch), nameof(Unridden)));
    }

    public bool Drifting
    {
        get => drifting;
        set => drifting = value;
    }

    public bool WavesOnly
    {
        get => wavesOnly;
        set => wavesOnly = value;
    }

    public void Dispose()
    {
        harmony.UnpatchAll(harmony.Id);
        drifting = false;
        wavesOnly = false;
    }

    private static bool Current => drifting && !wavesOnly;

    private static bool Prefix() => Current;

    // a driving hull's motion meets no water drag, so the set left in it would carry her on for good
    private static bool BeforeMove(EntityBehaviorCurrentDrift __instance, Vec3d motion)
    {
        if (!Current) __instance.Stopped(motion);
        return Current;
    }

    // a steered vessel is her rider's to move, as vanilla leaves her
    private static bool Unridden(EntityBoat boat) => drifting || boat.GetInterface<IMountable>()?.IsBeingControlled() == true;
}
