using System;
using HarmonyLib;
using RoughSeas.SeaAndWeather;

namespace AgentBridge;

/// <summary>Sets one current everywhere in place of the sea's, so a test can carry a vessel as hard as a river would.</summary>
public sealed class CurrentOverride : IDisposable
{
    private static (double X, double Z)? current;
    private readonly Harmony harmony = new("agentbridge.current");

    public CurrentOverride()
    {
        harmony.Patch(AccessTools.Method(typeof(SeaWeatherSystem), nameof(SeaWeatherSystem.CurrentAt)),
            postfix: new HarmonyMethod(typeof(CurrentOverride), nameof(Postfix)));
    }

    public (double X, double Z)? Current => current;

    public void Set(double metresPerSecond, double headingDegrees)
    {
        double heading = headingDegrees * Math.PI / 180;
        current = (metresPerSecond * Math.Sin(heading), metresPerSecond * Math.Cos(heading));
    }

    public void Set(double x, double z, bool on) => current = on ? (x, z) : null;

    public void Clear() => current = null;

    public void Dispose()
    {
        harmony.UnpatchAll(harmony.Id);
        current = null;
    }

    private static void Postfix(ref SeaCurrent __result)
    {
        if (current is var (x, z)) __result = __result with { X = x, Z = z };
    }
}
