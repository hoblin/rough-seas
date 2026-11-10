using System;
using HarmonyLib;
using RoughSeas.SeaAndWeather;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace RoughSeas.Storms;

/// <summary>
/// How a vessel with a draft floats in vanilla's physics: its swim line at its keel, so vanilla's <c>Swimming</c> means the
/// keel is wet, and <see cref="Heave"/> in place of vanilla's buoyancy, whose <c>BoyancyMul</c> scales nothing but that.
/// Clear of the water she falls under the real g on her own mass, not vanilla's. Nothing else of vanilla's moves her up or
/// down: its water drag, swim drag, a flowing block's push and other entities' repulsion keep their effect along the water
/// and lose it on her heave.
/// </summary>
[ReliesOn("Vintagestory.API.Common.EntityBehaviorPassivePhysics", "MotionAndCollision", "buoyancy, the flow push, the swim drag and the immersion formula copied here as constants")]
[ReliesOn("Vintagestory.GameContent.EntityBehaviorRepulseAgents", "OnGameTick", "pushes Pos.Motion Y, which this class puts back")]
public static class VesselDraught
{
    private const string HarmonyId = "roughseas.storms.draught";
    private const double TicksPerSecond = 60;
    private const double SwimDragPerMotion = 10;
    private const double SwimDragOffset = 0.02;
    public const double MostSwimDrag = 1.25;
    private const double VanillaFlowPush = 0.3;
    private const double LeastPushedDensity = 750;
    private const double MostPushedDensity = 2500;
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
            harmony.Patch(AccessTools.Method(typeof(EntityBehaviorRepulseAgents), nameof(EntityBehaviorRepulseAgents.OnGameTick)),
                prefix: new HarmonyMethod(typeof(VesselDraught), nameof(BeforeRepulsion)),
                postfix: new HarmonyMethod(typeof(VesselDraught), nameof(AfterRepulsion)));
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
        if (__instance.Entity is not EntityBoat boat || boat.GetBehavior<EntityBehaviorSeaForces>() is not { Heave: { } heave } forces) return;

        __instance.BoyancyMul = 0;
        bool wetBefore = ___feetInLiquidBefore || ___swimmingBefore;
        double airDrag = WaterDrag.Kept(__instance.AirDragValue, dt);
        double drag = wetBefore ? WaterDrag.Kept(__instance.WaterDragValue, dt) : airDrag;
        double kept = wetBefore ? 1 : airDrag;
        bool entering = boat.Swimming && forces.KeelWet == false;
        forces.KeelWet = boat.Swimming;

        var motion = pos.Motion;
        var push = FlowPush(boat, pos, dt, ___feetInLiquidBefore);
        double speed = (entering ? heave.Entering(motion.Y) : motion.Y) * kept;
        if (!boat.Swimming)
        {
            double vanillaGravity = __instance.GravityPerSecond * dt;
            motion.Y = (speed - HullResponse.Gravity * dt / TicksPerSecond + vanillaGravity - push.Y) / drag;
            return;
        }

        double heaving = speed + heave.Acceleration(Immersion(boat, pos), speed * TicksPerSecond) * dt / TicksPerSecond;
        double along = Math.Sqrt(Square(motion.X * drag + push.X) + Square(motion.Z * drag + push.Z));
        motion.Y = (BeforeSwimDrag(heaving, along, dt * TicksPerSecond) - push.Y) / drag;
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

    private static void BeforeRepulsion(EntityBehaviorRepulseAgents __instance, out double __state) => __state = __instance.entity.Pos.Motion.Y;

    private static void AfterRepulsion(EntityBehaviorRepulseAgents __instance, double __state)
    {
        if (__instance.entity is EntityBoat boat && boat.GetBehavior<EntityBehaviorSeaForces>()?.Heave is not null) boat.Pos.Motion.Y = __state;
    }

    private static (double X, double Y, double Z) FlowPush(EntityBoat boat, EntityPos pos, float dt, bool feetInLiquidBefore)
    {
        if (!feetInLiquidBefore) return default;
        var block = pos.AsBlockPos;
        if (OpenWater.At(boat.World.BlockAccessor, block) is not IBlockFlowing { IsStill: false } flowing) return default;

        double strength = VanillaFlowPush * VanillaWaterDensity / Math.Clamp(boat.MaterialDensity, LeastPushedDensity, MostPushedDensity) * dt * TicksPerSecond;
        var push = flowing.GetPushVector(block);
        return (push.X * strength, push.Y * strength, push.Z * strength);
    }

    private static double Square(double value) => value * value;

    private static double Immersion(EntityBoat boat, EntityPos pos)
    {
        var accessor = boat.World.BlockAccessor;
        var fluid = OpenWater.At(accessor, (int)pos.X, (int)pos.InternalY, (int)pos.Z);
        var above = OpenWater.At(accessor, (int)pos.X, (int)pos.InternalY + 1, (int)pos.Z);
        double surface = (int)pos.Y + fluid.LiquidLevel / LiquidLevels + (above.IsLiquid() ? LiquidAboveMetres : 0);
        return surface - pos.Y - (boat.SelectionBox.Y2 - boat.SwimmingOffsetY);
    }
}
