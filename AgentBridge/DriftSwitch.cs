using System;
using HarmonyLib;
using RoughSeas.Storms;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace AgentBridge;

/// <summary>
/// Holds every vessel nobody steers where a test put it while <see cref="Drifting"/> is off: Rough Seas'
/// <see cref="EntityBehaviorCurrentDrift"/>, which moves a hull by the current, the waves and her lines, skips its tick,
/// its keeping the set through a move and a rower's overwrite, its pull on a mooring, the swing about the bow and the
/// lines' pull. A steered vessel keeps the waves, as her rider's client draws them. With <see cref="WavesOnly"/>, or for a
/// steered vessel while drifting is off, the current is left out, to see what the waves do by themselves.
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
                     nameof(EntityBehaviorCurrentDrift.OnGameTick), "Rowed", nameof(EntityBehaviorCurrentDrift.Pull),
                     nameof(EntityBehaviorCurrentDrift.HeldAtBow), nameof(EntityBehaviorCurrentDrift.Impulse)
                 })
        {
            harmony.Patch(AccessTools.Method(typeof(EntityBehaviorCurrentDrift), method), prefix: prefix);
        }
        harmony.Patch(AccessTools.Method(typeof(EntityBehaviorCurrentDrift), "BeforeMove"),
            prefix: new HarmonyMethod(typeof(DriftSwitch), nameof(BeforeMove)));
        harmony.Patch(AccessTools.Method(typeof(EntityBehaviorCurrentDrift), "Current"),
            prefix: new HarmonyMethod(typeof(DriftSwitch), nameof(Current)));
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

    private static bool Moves(Entity entity) => drifting || entity.GetInterface<IMountable>()?.IsBeingControlled() == true;

    private static bool Prefix(EntityBehaviorCurrentDrift __instance) => Moves(__instance.entity);

    // a driving hull's motion meets no water drag, so the set left in it would carry her on for good
    private static bool BeforeMove(EntityBehaviorCurrentDrift __instance, Vec3d motion)
    {
        var moves = Moves(__instance.entity);
        if (!moves) __instance.Stopped(motion);
        return moves;
    }

    private static bool Current(ref (double X, double Z) __result)
    {
        if (drifting && !wavesOnly) return true;
        __result = default;
        return false;
    }
}
