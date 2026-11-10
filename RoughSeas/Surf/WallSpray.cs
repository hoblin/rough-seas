using System;
using RoughSeas.Shore;

namespace RoughSeas.Surf;

/// <summary>What a strike on a face throws: its litres per metre of face, and how fast each drop leaves it, by that drop's place in the strike's measured spread of heights.</summary>
public abstract class Impact(double litres, double topSpeed)
{
    protected const double Gravity = RunUp.Gravity;

    public double Litres { get; } = litres;

    public double TopSpeed { get; } = topSpeed;

    public double Speed(double exceedance) => Math.Min(TopSpeed, Math.Sqrt(2 * Gravity * Math.Max(Climb(exceedance), 0)));

    protected abstract double Climb(double exceedance);
}

public sealed class WaveImpact(double litres, double topSpeed, double height, double crest, FacesConfig config) : Impact(litres, topSpeed)
{
    protected override double Climb(double exceedance) =>
        height * Inverse(Math.Max(exceedance, 1e-6) * Exceedance(crest / height)) - crest;

    private double Exceedance(double relative) =>
        relative < config.ImpulsiveBreak ? Impulse(relative) : config.ImpulsivePowerFactor / config.ImpulsiveFactor * Math.Pow(relative, -config.ImpulsivePower);

    private double Inverse(double share) => share >= Impulse(config.ImpulsiveBreak)
        ? -Math.Log(share) / config.ImpulsiveFreeboard
        : Math.Pow(share * config.ImpulsiveFactor / config.ImpulsivePowerFactor, -1 / config.ImpulsivePower);

    private double Impulse(double relative) => Math.Exp(-config.ImpulsiveFreeboard * relative);
}

public sealed class SheetImpact(double litres, double head, FacesConfig config) : Impact(litres, Math.Sqrt(2 * Gravity * head))
{
    protected override double Climb(double exceedance) => Math.Min(head, -Math.Log(Math.Max(exceedance, 1e-6)) * head / config.ImpulsiveFreeboard);
}

public sealed class WallSpray(FacesConfig config, double spectralPeriodRatio)
{
    private const double Gravity = RunUp.Gravity;

    public WaveImpact Wave(double height, double period, double toeDepth, double crest, double celerity)
    {
        var cubicMetres = Discharge(height, period, toeDepth, crest / height) * Math.Sqrt(Gravity * height * height * height) * period;
        return new WaveImpact(cubicMetres * 1000, config.JetSpeedRatio * celerity, height, crest, config);
    }

    public SheetImpact Sheet(double lens, double head) => new(lens * 1000, head, config);

    public (int Count, double Side) Parcels(double litres)
    {
        var smallest = config.SmallestParcelSide;
        var count = (int)Math.Min(config.ParcelsPerMetre, litres / (smallest * smallest * smallest * 1000));
        if (count <= 0) return (0, smallest);
        return (count, Math.Clamp(Math.Cbrt(litres / count / 1000), smallest, config.LargestParcelSide));
    }

    private double Discharge(double height, double period, double toeDepth, double relative)
    {
        var steepness = height / RunUp.DeepLength(period / spectralPeriodRatio);
        var breaking = Math.Sqrt(height / (toeDepth * steepness));
        return relative < config.ImpulsiveBreak
            ? config.ImpulsiveFactor * breaking * Math.Exp(-config.ImpulsiveFreeboard * relative)
            : config.ImpulsivePowerFactor * breaking * Math.Pow(relative, -config.ImpulsivePower);
    }
}
