using System;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace RoughSeas.Propulsion;

/// <summary>
/// Between a boat's <c>ForwardSpeed</c> and how far she really goes: vanilla's swimming physics divides her motion by
/// up to 1.25 when it is fast (<c>EntityBehaviorPassivePhysics.MotionAndCollision</c>). Its water drag is left out:
/// while ours acts it is undone along the water and kept on her heave, which nothing of ours damps.
/// </summary>
public static class VanillaSwim
{
    private const double TicksPerSecond = 60;
    private const double SwimDragPerMotion = 10;
    private const double SwimDragOffset = 0.02;
    private const double MostSwimDrag = 1.25;
    private const double WaterDragTicksPerSecond = 33;

    private static double StepFactor => GlobalConstants.PhysicsFrameTime * TicksPerSecond;

    public static double Realized(double forwardSpeed) => TicksPerSecond * forwardSpeed / SwimDrag(forwardSpeed);

    public static void LeaveWaterDragOnHeave(Vec3d motion, double waterDrag, float seconds)
    {
        var kept = Math.Pow(waterDrag, seconds * WaterDragTicksPerSecond);
        motion.X /= kept;
        motion.Z /= kept;
    }

    public static double Commanded(double metresPerSecond)
    {
        var free = metresPerSecond / TicksPerSecond;
        return SwimDrag(free) <= 1 ? free : free * MostSwimDrag;
    }

    private static double SwimDrag(double motion) =>
        Math.Clamp(SwimDragPerMotion * Math.Abs(motion) * StepFactor - SwimDragOffset, 1, MostSwimDrag);
}
