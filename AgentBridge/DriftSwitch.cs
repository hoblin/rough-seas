using System;
using HarmonyLib;
using RoughSeas.Storms;

namespace AgentBridge;

/// <summary>Skips Rough Seas' <see cref="CurrentDrift"/> while <see cref="Drifting"/> is off, so vessels stay where a test put them.</summary>
public sealed class DriftSwitch : IDisposable
{
    private static bool drifting;
    private readonly Harmony harmony = new("agentbridge.drift");

    public DriftSwitch()
    {
        var prefix = new HarmonyMethod(typeof(DriftSwitch), nameof(Prefix));
        harmony.Patch(AccessTools.Method(typeof(CurrentDrift), nameof(CurrentDrift.OnGameTick)), prefix: prefix);
        harmony.Patch(AccessTools.Method(typeof(CurrentDrift), "BeforeMove"), prefix: prefix);
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
