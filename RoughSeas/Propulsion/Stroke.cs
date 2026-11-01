using System;

namespace RoughSeas.Propulsion;

public abstract class Stroke(string code, StrokeConfig config)
{
    protected const double Gravity = 9.81;
    private const int Bisections = 60;

    public string Code => code;

    protected StrokeConfig Config => config;

    public double StrokesPerMinute => config.StrokesPerMinute;

    public double GrossEfficiency => config.GrossEfficiency;

    public abstract double Thrust(double metresPerSecond, double watts);

    protected static double Solve(Func<double, double> rising, double target)
    {
        double low = 0, high = 1;
        while (rising(high) < target) high *= 2;
        for (var i = 0; i < Bisections; i++)
        {
            var mid = (low + high) / 2;
            if (rising(mid) < target) low = mid;
            else high = mid;
        }
        return (low + high) / 2;
    }
}

public sealed class BladeStroke(string code, StrokeConfig config, double bladeSquareMetres, double waterDensity) : Stroke(code, config)
{
    private readonly double drag = waterDensity / 2 * config.DragCoefficient * bladeSquareMetres * config.Blades;

    public override double Thrust(double metresPerSecond, double watts)
    {
        if (watts <= 0) return 0;
        var drive = Config.DriveShare;
        var ahead = Math.Max(0, metresPerSecond);
        var slip = Solve(s => drive * drag * s * s * (ahead + s), watts);
        return drive * drag * slip * slip;
    }
}

public sealed class PoleStroke(string code, StrokeConfig config, double poleKg, double depthMetres, double handsAboveWaterMetres) : Stroke(code, config)
{
    public double LiftWatts => poleKg * Gravity * (depthMetres + handsAboveWaterMetres) / 2 * StrokesPerMinute / 60;

    public override double Thrust(double metresPerSecond, double watts)
    {
        var pushing = watts - LiftWatts;
        if (pushing <= 0) return 0;
        var most = Config.DriveShare * Config.PushNewtons;
        return metresPerSecond <= 0 ? most : Math.Min(most, pushing / metresPerSecond);
    }
}
