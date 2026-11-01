using System;
using Vintagestory.API.Config;

namespace RoughSeas.Propulsion;

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
