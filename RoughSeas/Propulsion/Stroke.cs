using System;

namespace RoughSeas.Propulsion;

public abstract class Stroke(string code, StrokeConfig config)
{
    private const int Bisections = 60;

    public string Code => code;

    protected StrokeConfig Config => config;

    public double StrokesPerMinute => config.StrokesPerMinute;

    public double GrossEfficiency => config.GrossEfficiency;

    public virtual double LiftWatts(double metresPerSecond) => 0;

    public virtual double Pace(double metresPerSecond) => 1;

    public virtual double Shove(double meanNewtons, double? drawnShare) => meanNewtons;

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
    private readonly double drag = waterDensity / 2 * config.DragCoefficient * bladeSquareMetres;

    public override double Thrust(double metresPerSecond, double watts)
    {
        if (watts <= 0 || drag <= 0) return 0;
        var drive = Config.DriveShare;
        var ahead = Math.Max(0, metresPerSecond);
        var slip = Solve(s => drive * drag * s * s * (ahead + s), watts);
        return drive * drag * slip * slip;
    }
}

public sealed class PoleStroke(string code, StrokeConfig config, Pole pole, double depthMetres, double handsAboveWaterMetres, double waterDensity)
    : Stroke(code, config)
{
    public override double LiftWatts(double metresPerSecond) => pole.LiftJoules(depthMetres, waterDensity) / StrokeSeconds(metresPerSecond);

    public override double Pace(double metresPerSecond) => DrawnSeconds / StrokeSeconds(metresPerSecond);

    private double DrawnSeconds => 60 / StrokesPerMinute;

    private double RecoverySeconds => (1 - Config.DriveShare) * DrawnSeconds;

    private double PushMetres => Math.Min(Config.PushMetres, pole.Travel(depthMetres + handsAboveWaterMetres));

    private double ShoveSeconds(double metresPerSecond) =>
        Math.Min(Config.DriveShare * DrawnSeconds, PushMetres / Math.Max(double.Epsilon, Math.Abs(metresPerSecond)));

    private double StrokeSeconds(double metresPerSecond) => ShoveSeconds(metresPerSecond) + RecoverySeconds;

    public override double Shove(double meanNewtons, double? drawnShare)
    {
        if (drawnShare is not { } share) return meanNewtons;
        var drive = Config.DriveShare;
        // The animation starts halfway through the recovery, as bin/lib/hulls/held.py's PolePath draws it.
        var phase = (share + drive + (1 - drive) / 2) % 1;
        return phase < drive ? meanNewtons / drive : 0;
    }

    public override double Thrust(double metresPerSecond, double watts)
    {
        if (watts <= 0 || PushMetres <= 0) return 0;
        var speed = Math.Max(0, metresPerSecond);
        var shoving = Config.PushNewtons * ShoveSeconds(speed) / StrokeSeconds(speed);
        return speed <= 0 ? shoving : Math.Min(shoving, watts / speed);
    }
}
