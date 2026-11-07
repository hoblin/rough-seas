using HarmonyLib;
using RoughSeas.SeaAndWeather;
using Vintagestory.GameContent;

namespace RoughSeas.Storms;

/// <summary>
/// Carries a vessel to and fro with the water the drawn waves move, after vanilla's steering has set her motion for the
/// step: on the client that steers her, and on the server while nobody does, each through her own
/// <see cref="WaveRide"/>.
/// </summary>
public static class WaveCarry
{
    private const string HarmonyId = "roughseas.storms.carry";

    private static readonly object patchLock = new();
    private static int attached;
    private static Harmony? harmony;

    public static void Attach()
    {
        lock (patchLock)
        {
            if (attached++ > 0) return;
            harmony = new Harmony(HarmonyId);
            harmony.Patch(AccessTools.Method(typeof(EntityBoat), "updateBoatAngleAndMotion"),
                postfix: new HarmonyMethod(typeof(WaveCarry), nameof(Carry)) { priority = Priority.Low });
        }
    }

    public static void Detach()
    {
        lock (patchLock)
        {
            if (--attached > 0 || harmony == null) return;
            harmony.UnpatchAll(HarmonyId);
            harmony = null;
        }
    }

    /// <summary>
    /// What a hull's motion takes this step to move at <paramref name="metresPerSecond"/> on top of its own way, in
    /// blocks per physics tick, when it moved at <paramref name="beforeMetresPerSecond"/> on top of it the step
    /// before. Under way (<paramref name="kept"/> null) vanilla has just set the motion from her speed through the
    /// water, and the waves' velocity goes on top. Lying still, vanilla's drag has kept <paramref name="kept"/> of
    /// last step's motion, the waves' part with it, and the waves' part is brought up to this step's.
    /// </summary>
    internal static double Carried(double metresPerSecond, double beforeMetresPerSecond, double? kept) =>
        (metresPerSecond - (kept ?? 0) * beforeMetresPerSecond) / WaterDrag.TicksPerSecond;

    /// <summary>
    /// A velocity in metres a second of the waves' clock as metres a second of the tick's, when the waves' clock runs
    /// <paramref name="wavesPerTickSecond"/> of its seconds to the tick's. One wave then moves a hull as far at any speed of time.
    /// </summary>
    internal static (double X, double Z) OnTheClock((double X, double Z) velocity, double wavesPerTickSecond) =>
        (velocity.X * wavesPerTickSecond, velocity.Z * wavesPerTickSecond);

    private static void Carry(EntityBoat __instance, float dt) => __instance.GetBehavior<EntityBehaviorSeaForces>()?.Ride.Carry(__instance, dt);
}
