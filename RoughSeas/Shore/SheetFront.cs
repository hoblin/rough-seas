using System;

namespace RoughSeas.Shore;

/// <summary>
/// The front of the sheet a broken wave throws up the shore, thrown at U = √(2gR) so that it climbs the run-up R, as
/// Shen &amp; Meyer's swash meets the measured run-up. Crossing a block, it climbs the step to the block's top only while its
/// velocity head reaches over it, loses speed to bed friction over the lens's mean depth, and soaks into the bed by
/// Darcy; it stops when it can't climb, when the bed has drunk it, or when its wave's period is out and the next bore
/// runs over it.
/// </summary>
public readonly record struct SheetFront(double SpeedSquared, double Depth, double Seconds, double Period, double Friction)
{
    private const double Gravity = RunUp.Gravity;

    public static SheetFront Thrown(double runUpMetres, double periodSeconds, SheetConfig config)
    {
        var reach = Math.Max(runUpMetres, 0);
        return new SheetFront(2 * Gravity * reach, 2 * reach / config.LensDepthRatio, 0, periodSeconds, config.UprushFriction);
    }

    public double Head => SpeedSquared / (2 * Gravity);

    public SheetFront? Cross(double rise, BedConfig bed)
    {
        if (rise >= Head || Depth <= 0) return null;
        var speedSquared = SpeedSquared - 2 * Gravity * rise;
        var loss = Friction / Depth;
        var seconds = Seconds + (Math.Exp(loss) - 1) / (loss * Math.Sqrt(speedSquared));
        var depth = Depth - bed.Conductivity * (seconds - Seconds);
        if (depth <= 0 || seconds > Period) return null;
        return this with { SpeedSquared = speedSquared * Math.Exp(-2 * loss), Depth = depth, Seconds = seconds };
    }
}
