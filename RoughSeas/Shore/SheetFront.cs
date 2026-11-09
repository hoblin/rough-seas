using System;

namespace RoughSeas.Shore;

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
