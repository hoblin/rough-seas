using System;
using RoughSeas.Water;

namespace RoughSeas.Propulsion;

public abstract class Stroke(string code, StrokeConfig config)
{
    public string Code => code;

    protected StrokeConfig Config => config;

    public double StrokesPerMinute => config.StrokesPerMinute;

    public double GrossEfficiency => config.GrossEfficiency;

    public virtual double LiftWatts(double metresPerSecond) => 0;

    public virtual double Pace(double metresPerSecond, double? drawnShare) => 1;

    public virtual double Shove(double meanNewtons, double metresPerSecond, double? drawnShare) => meanNewtons;

    public abstract double Thrust(double metresPerSecond, double watts, double? strokeMetresPerSecond = null);
}

public sealed class BladeStroke(string code, StrokeConfig config, double bladeSquareMetres, double waterDensity) : Stroke(code, config)
{
    private readonly double drag = waterDensity / 2 * config.DragCoefficient * bladeSquareMetres;

    public override double Thrust(double metresPerSecond, double watts, double? strokeMetresPerSecond = null) =>
        SlippingBlade.Thrust(Config.DriveShare * drag, metresPerSecond, watts);
}

public sealed class PoleStroke(string code, StrokeConfig config, Pole pole, double depthMetres, double handsAboveWaterMetres, double waterDensity)
    : Stroke(code, config)
{
    public override double LiftWatts(double metresPerSecond) => pole.LiftJoules(depthMetres, waterDensity) / StrokeSeconds(metresPerSecond);

    public override double Pace(double metresPerSecond, double? drawnShare) =>
        drawnShare is { } share && Config.Driving(share) ? Config.DriveShare * DrawnSeconds / ShoveSeconds(metresPerSecond) : 1;

    private double DrawnSeconds => 60 / StrokesPerMinute;

    private double RecoverySeconds => (1 - Config.DriveShare) * DrawnSeconds;

    private double PushMetres => Math.Min(Config.PushMetres, pole.Travel(depthMetres + handsAboveWaterMetres));

    private double ShoveSeconds(double metresPerSecond) =>
        PushMetres <= 0 || metresPerSecond <= 0
            ? Config.DriveShare * DrawnSeconds
            : Math.Min(Config.DriveShare * DrawnSeconds, PushMetres / metresPerSecond);

    private double StrokeSeconds(double metresPerSecond) => ShoveSeconds(metresPerSecond) + RecoverySeconds;

    public override double Shove(double meanNewtons, double metresPerSecond, double? drawnShare) =>
        drawnShare is not { } share ? meanNewtons
        : Config.Driving(share) ? meanNewtons * StrokeSeconds(metresPerSecond) / ShoveSeconds(metresPerSecond)
        : 0;

    public override double Thrust(double metresPerSecond, double watts, double? strokeMetresPerSecond = null)
    {
        if (watts <= 0 || PushMetres <= 0) return 0;
        var speed = Math.Max(0, metresPerSecond);
        var stroking = strokeMetresPerSecond ?? speed;
        var shoving = Config.PushNewtons * ShoveSeconds(stroking) / StrokeSeconds(stroking);
        return speed <= 0 ? shoving : Math.Min(shoving, watts / speed);
    }
}
