using System;
using Vintagestory.API.Config;

namespace RoughSeas.Propulsion;

public static class VanillaSwim
{
    private const double TicksPerSecond = 60;
    private const double DragExponentPerSecond = 33;
    private const double SwimDragPerMotion = 10;
    private const double SwimDragOffset = 0.02;
    private const double MostSwimDrag = 1.25;

    private static double StepFactor => GlobalConstants.PhysicsFrameTime * TicksPerSecond;

    private static double Kept => Math.Pow(GlobalConstants.WaterDrag, GlobalConstants.PhysicsFrameTime * DragExponentPerSecond);

    public static double Realized(double forwardSpeed)
    {
        var kept = forwardSpeed * Kept;
        return TicksPerSecond * kept / SwimDrag(kept);
    }

    public static double Commanded(double metresPerSecond)
    {
        var sign = Math.Sign(metresPerSecond);
        var speed = Math.Abs(metresPerSecond);
        var free = speed / TicksPerSecond;
        if (SwimDrag(free) <= 1) return sign * free / Kept;

        var held = speed * MostSwimDrag / TicksPerSecond;
        return sign * Math.Max(held, (1 + SwimDragOffset) / (SwimDragPerMotion * StepFactor)) / Kept;
    }

    private static double SwimDrag(double motion) =>
        Math.Clamp(SwimDragPerMotion * Math.Abs(motion) * StepFactor - SwimDragOffset, 1, MostSwimDrag);
}
