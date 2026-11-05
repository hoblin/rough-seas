using System;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace RoughSeas.Storms;

/// <summary>
/// How a vessel with a draft floats in vanilla's physics: its swim line at its keel, so vanilla's <c>Swimming</c> means the
/// keel is wet, and <see cref="Heave"/> in place of vanilla's buoyancy, whose <c>BoyancyMul</c> scales nothing but that.
/// </summary>
public static class VesselDraught
{
    private const string HarmonyId = "roughseas.storms.draught";
    private const double TicksPerSecond = 60;
    private const double WaterDragTicksPerSecond = 33;
    private const double SwimDragPerMotion = 10;
    private const double SwimDragOffset = 0.02;
    public const double MostSwimDrag = 1.25;
    private const double VanillaLiftPerSubmergence = 60;
    private const double VanillaWaterDensity = 1000;
    private const double FounderedMetres = 100;
    private const double LiquidLevels = 8;
    private const double LiquidAboveMetres = 9 / LiquidLevels;

    private static readonly object patchLock = new();
    private static int attached;
    private static Harmony? harmony;

    public static void Attach()
    {
        lock (patchLock)
        {
            if (attached++ > 0) return;
            harmony = new Harmony(HarmonyId);
            harmony.Patch(AccessTools.PropertyGetter(typeof(EntityBoat), nameof(EntityBoat.SwimmingOffsetY)),
                postfix: new HarmonyMethod(typeof(VesselDraught), nameof(Settle)));
            harmony.Patch(AccessTools.Method(typeof(EntityBehaviorPassivePhysics), nameof(EntityBehaviorPassivePhysics.MotionAndCollision)),
                prefix: new HarmonyMethod(typeof(VesselDraught), nameof(Heave)) { priority = Priority.Last });
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

    public static double WaterDragKept(double waterDrag, double seconds) => Math.Pow(waterDrag, seconds * WaterDragTicksPerSecond);

    public static double SwimDrag(double motionLength, double stepFactor) =>
        Math.Clamp(SwimDragPerMotion * Math.Abs(motionLength * stepFactor) - SwimDragOffset, 1, MostSwimDrag);

    private static void Settle(EntityBoat __instance, ref double __result)
    {
        if (__instance.GetBehavior<EntityBehaviorSeaForces>() is not { } forces) return;

        __result += forces.Buoyant is { HasDraft: true } buoyant
            ? buoyant.Afloat.Sunk ? -FounderedMetres : buoyant.ReferenceDraftMetres - VanillaRestMetres(__instance)
            : -forces.SettlingMetres;
    }

    private static double VanillaRestMetres(Entity boat) => 1 / (VanillaLiftPerSubmergence * (1 - boat.MaterialDensity / VanillaWaterDensity));

    private static void Heave(EntityBehaviorPassivePhysics __instance, EntityPos pos, float dt, bool ___feetInLiquidBefore, bool ___swimmingBefore)
    {
        if (__instance.Entity is not EntityBoat boat || boat.GetBehavior<EntityBehaviorSeaForces>()?.Heave is not { } heave) return;

        __instance.BoyancyMul = 0;
        double kept = ___feetInLiquidBefore || ___swimmingBefore ? WaterDragKept(__instance.WaterDragValue, dt) : 1;
        if (!boat.Swimming)
        {
            pos.Motion.Y /= kept;
            return;
        }

        var motion = pos.Motion;
        double heaving = motion.Y + heave.Acceleration(Immersion(boat, pos), motion.Y * TicksPerSecond) * dt / TicksPerSecond;
        double along = Math.Sqrt(Square(motion.X * kept) + Square(motion.Z * kept));
        motion.Y = BeforeSwimDrag(heaving, along, dt * TicksPerSecond) / kept;
    }

    public static double BeforeSwimDrag(double heaving, double along, double stepFactor)
    {
        double rate = SwimDragPerMotion * stepFactor;
        double At(double drag) => SwimDrag(Math.Sqrt(Square(along) + Square(heaving * drag)), stepFactor);
        if (At(1) <= 1) return heaving;
        if (At(MostSwimDrag) >= MostSwimDrag) return heaving * MostSwimDrag;

        double a = 1 - Square(rate * heaving), b = 2 * SwimDragOffset, c = Square(SwimDragOffset) - Square(rate * along);
        double drag = Math.Abs(a) < 1e-12 ? -c / b : (-b + Math.Sqrt(b * b - 4 * a * c)) / (2 * a);
        return heaving * drag;
    }

    private static double Square(double value) => value * value;

    private static double Immersion(EntityBoat boat, EntityPos pos)
    {
        var accessor = boat.World.BlockAccessor;
        var fluid = accessor.GetBlockRaw((int)pos.X, (int)pos.InternalY, (int)pos.Z, BlockLayersAccess.Fluid);
        var above = accessor.GetBlockRaw((int)pos.X, (int)pos.InternalY + 1, (int)pos.Z, BlockLayersAccess.Fluid);
        double surface = (int)pos.Y + fluid.LiquidLevel / LiquidLevels + (above.IsLiquid() ? LiquidAboveMetres : 0);
        return surface - pos.Y - (boat.SelectionBox.Y2 - boat.SwimmingOffsetY);
    }
}
