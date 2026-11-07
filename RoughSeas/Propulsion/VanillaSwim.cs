using System;
using RoughSeas.SeaAndWeather;
using RoughSeas.Storms;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace RoughSeas.Propulsion;

/// <summary>
/// Between a boat's <c>ForwardSpeed</c> and how far she really goes: vanilla's swimming physics divides her motion by
/// up to 1.25 when it is fast (<c>EntityBehaviorPassivePhysics.MotionAndCollision</c>). Its water drag is left out:
/// while ours acts it is undone along the water.
/// </summary>
public static class VanillaSwim
{
    private const double TicksPerSecond = 60;

    private static double StepFactor => GlobalConstants.PhysicsFrameTime * TicksPerSecond;

    public static double Realized(double forwardSpeed) => TicksPerSecond * forwardSpeed / SwimDrag(forwardSpeed);

    public static void LeaveWaterDragOnHeave(Vec3d motion, double waterDrag, float seconds)
    {
        var kept = WaterDrag.Kept(waterDrag, seconds);
        motion.X /= kept;
        motion.Z /= kept;
    }

    public static double Commanded(double metresPerSecond)
    {
        var free = metresPerSecond / TicksPerSecond;
        return SwimDrag(free) <= 1 ? free : free * VesselDraught.MostSwimDrag;
    }

    private static double SwimDrag(double motion) => VesselDraught.SwimDrag(motion, StepFactor);
}
