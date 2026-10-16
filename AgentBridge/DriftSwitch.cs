using System;
using HarmonyLib;
using RoughSeas.SeaAndWeather;

namespace AgentBridge;

/// <summary>Skips Rough Seas' <see cref="CurrentDrift"/> while <see cref="Drifting"/> is off, so unridden vessels stay where a test put them.</summary>
public sealed class DriftSwitch : IDisposable
{
    private static bool drifting;
    private readonly Harmony harmony = new("agentbridge.drift");

    public DriftSwitch()
    {
        harmony.Patch(AccessTools.Method(typeof(CurrentDrift), nameof(CurrentDrift.OnGameTick)),
            prefix: new HarmonyMethod(typeof(DriftSwitch), nameof(Prefix)));
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
}
