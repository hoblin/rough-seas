using System;
using HarmonyLib;
using RoughSeas.SeaAndWeather;
using RoughSeas.Storms;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace AgentBridge;

/// <summary>
/// Skips Rough Seas' <see cref="CurrentDrift"/> and the waves' carry while <see cref="Drifting"/> is off, so unridden vessels stay
/// where a test put them; with <see cref="WavesOnly"/> it skips the current alone, to see what the waves do by themselves.
/// </summary>
public sealed class DriftSwitch : IDisposable
{
    private static bool drifting;
    private static bool wavesOnly;
    private readonly Harmony harmony = new("agentbridge.drift");

    public DriftSwitch()
    {
        harmony.Patch(AccessTools.Method(typeof(CurrentDrift), nameof(CurrentDrift.OnGameTick)),
            prefix: new HarmonyMethod(typeof(DriftSwitch), nameof(Prefix)));
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

    private static bool Prefix() => drifting && !wavesOnly;

    // a steered vessel is her rider's to move, as vanilla leaves her
    private static bool Unridden(EntityBoat boat) => drifting || boat.GetInterface<IMountable>()?.IsBeingControlled() == true;
}
