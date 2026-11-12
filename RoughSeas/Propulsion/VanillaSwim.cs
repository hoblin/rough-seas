using System;
using RoughSeas.SeaAndWeather;
using RoughSeas.Storms;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace RoughSeas.Propulsion;

/// <summary>
/// The speed to ask of a boat's <c>ForwardSpeed</c> for her to really make it, the inverse of
/// <see cref="VesselDraught.Realized"/>: vanilla's swimming physics divides her motion by up to 1.25 when it is fast in
/// one move a tick, and not at all in the ten moves it splits a faster tick into, so between 4.8 and 6 m/s nothing is
/// made and a speed asked there makes 4.8. Its water drag is left out: while ours acts it is undone along the water.
/// </summary>
[ReliesOn("Vintagestory.API.Common.EntityBehaviorPassivePhysics", "MotionAndCollision", "the swim drag is clamp(10·|motion|·dtFactor − 0.02, 1, 1.25) and this class undoes it along the water")]
[ReliesOn("Vintagestory.API.Common.EntityBehaviorPassivePhysics", "OnPhysicsTick", "splits a tick into 10 moves while the motion is longer than 0.1 blocks a tick")]
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
        if (Math.Abs(free) > VesselDraught.SubSteppedMotion || SwimDrag(free) <= 1) return free;
        return Math.Clamp(free * VesselDraught.MostSwimDrag, -VesselDraught.SubSteppedMotion, VesselDraught.SubSteppedMotion);
    }

    private static double SwimDrag(double motion) => VesselDraught.SwimDrag(motion, StepFactor);
}
