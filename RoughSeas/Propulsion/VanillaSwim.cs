using System;
using RoughSeas.SeaAndWeather;
using RoughSeas.Storms;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace RoughSeas.Propulsion;

/// <summary>
/// The speed to ask of a boat's <c>ForwardSpeed</c> for her to really make it: vanilla's swimming physics divides her
/// motion by up to 1.25 when it is fast (<c>EntityBehaviorPassivePhysics.MotionAndCollision</c>), the inverse of
/// <see cref="VesselDraught.Realized"/>. Its water drag is left out: while ours acts it is undone along the water.
/// </summary>
[ReliesOn("Vintagestory.API.Common.EntityBehaviorPassivePhysics", "MotionAndCollision", "the swim drag is clamp(10·|motion|·dtFactor − 0.02, 1, 1.25) and this class undoes it along the water")]
public static class VanillaSwim
{
    private const double TicksPerSecond = 60;

    private static double StepFactor => GlobalConstants.PhysicsFrameTime * TicksPerSecond;

    public static void LeaveWaterDragOnHeave(Vec3d motion, double waterDrag, float seconds)
    {
        var kept = WaterDrag.Kept(waterDrag, seconds);
        motion.X /= kept;
        motion.Z /= kept;
    }

    public static double Commanded(double metresPerSecond)
    {
        var free = metresPerSecond / TicksPerSecond;
        return Math.Abs(free) > VesselDraught.SubSteppedMotion || SwimDrag(free) <= 1 ? free : free * VesselDraught.MostSwimDrag;
    }

    private static double SwimDrag(double motion) => VesselDraught.SwimDrag(motion, StepFactor);
}
