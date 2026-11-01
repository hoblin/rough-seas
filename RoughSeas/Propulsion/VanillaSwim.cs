using System;
using Vintagestory.API.Config;

namespace RoughSeas.Propulsion;

/// <summary>
/// Between a boat's <c>ForwardSpeed</c> and how far she really goes: vanilla's swimming physics divides her motion by
/// up to 1.25 when it is fast (<c>EntityBehaviorPassivePhysics.MotionAndCollision</c>). Its water drag is left out:
/// a rowed vessel's is set to none while ours acts.
/// </summary>
public static class VanillaSwim
{
    private const double TicksPerSecond = 60;
    private const double SwimDragPerMotion = 10;
    private const double SwimDragOffset = 0.02;
    private const double MostSwimDrag = 1.25;

    private static double StepFactor => GlobalConstants.PhysicsFrameTime * TicksPerSecond;

    public static double Realized(double forwardSpeed) => TicksPerSecond * forwardSpeed / SwimDrag(forwardSpeed);

    public static double Commanded(double metresPerSecond)
    {
        var free = metresPerSecond / TicksPerSecond;
        return SwimDrag(free) <= 1 ? free : free * MostSwimDrag;
    }

    private static double SwimDrag(double motion) =>
        Math.Clamp(SwimDragPerMotion * Math.Abs(motion) * StepFactor - SwimDragOffset, 1, MostSwimDrag);
}
