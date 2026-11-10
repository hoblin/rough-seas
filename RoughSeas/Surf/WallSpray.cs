using System;
using RoughSeas.Shore;

namespace RoughSeas.Surf;

/// <summary>What a strike on a face throws: its litres per metre of face, and how fast each drop leaves it, by that drop's place in the strike's measured spread of heights.</summary>
public abstract class Impact(double litres, double topSpeed)
{
    protected const double Gravity = RunUp.Gravity;

    public double Litres { get; } = litres;

    public double TopSpeed { get; } = topSpeed;

    public double Speed(double draw) => Math.Min(TopSpeed, Math.Sqrt(2 * Gravity * Math.Max(Climb(draw), 0)));

    protected abstract double Climb(double draw);
}

public sealed class WaveImpact(double litres, double topSpeed, double height, double from, bool impulsive, FacesConfig config) : Impact(litres, topSpeed)
{
    public bool Impulsive { get; } = impulsive;

    protected override double Climb(double draw)
    {
        var share = Math.Max(draw, 1e-6) * Exceedance(from / height);
        return height * Inverse(share) - from;
    }

    private double Exceedance(double relative) => Impulsive
        ? relative < config.ImpulsiveBreak ? Impulse(relative) : Power(relative)
        : Math.Exp(-config.NonImpulsiveFreeboard * relative);

    private double Inverse(double share)
    {
        if (!Impulsive) return -Math.Log(share) / config.NonImpulsiveFreeboard;
        return share >= Impulse(config.ImpulsiveBreak)
            ? -Math.Log(share) / config.ImpulsiveFreeboard
            : Math.Pow(share * config.ImpulsiveFactor / config.ImpulsivePowerFactor, -1 / config.ImpulsivePower);
    }

    private double Impulse(double relative) => Math.Exp(-config.ImpulsiveFreeboard * relative);

    private double Power(double relative) => config.ImpulsivePowerFactor / config.ImpulsiveFactor * Math.Pow(relative, -config.ImpulsivePower);
}

public sealed class SheetImpact(double litres, double head, FacesConfig config) : Impact(litres, Math.Sqrt(2 * Gravity * head))
{
    protected override double Climb(double draw) => Math.Min(head, -Math.Log(Math.Max(draw, 1e-6)) * head / config.NonImpulsiveFreeboard);
}

public sealed class WallSpray(FacesConfig config, double spectralPeriodRatio)
{
    private const double Gravity = RunUp.Gravity;

    public bool Impulsive(double height, double period, double toeDepth) =>
        toeDepth * toeDepth / (height * RunUp.DeepLength(period / spectralPeriodRatio)) <= config.ImpulsiveToe;

    public WaveImpact Wave(double height, double period, double toeDepth, double from, double celerity)
    {
        var impulsive = Impulsive(height, period, toeDepth);
        var relative = from / height;
        var overtopping = impulsive ? ImpulsiveDischarge(height, period, toeDepth, relative) : config.NonImpulsiveFactor * Math.Exp(-config.NonImpulsiveFreeboard * relative);
        var cubicMetres = overtopping * Math.Sqrt(Gravity * height * height * height) * period;
        return new WaveImpact(cubicMetres * 1000, config.JetSpeedRatio * celerity, height, from, impulsive, config);
    }

    public SheetImpact Sheet(double lens, double head) => new(lens * 1000, head, config);

    public (int Count, double Side) Parcels(double litres)
    {
        var smallest = config.SmallestParcelSide;
        var count = (int)Math.Min(config.ParcelsPerMetre, litres / (smallest * smallest * smallest * 1000));
        if (count <= 0) return (0, smallest);
        return (count, Math.Clamp(Math.Cbrt(litres / count / 1000), smallest, config.LargestParcelSide));
    }

    private double ImpulsiveDischarge(double height, double period, double toeDepth, double relative)
    {
        var steepness = height / RunUp.DeepLength(period / spectralPeriodRatio);
        var breaking = Math.Sqrt(height / (toeDepth * steepness));
        return relative < config.ImpulsiveBreak
            ? config.ImpulsiveFactor * breaking * Math.Exp(-config.ImpulsiveFreeboard * relative)
            : config.ImpulsivePowerFactor * breaking * Math.Pow(relative, -config.ImpulsivePower);
    }
}
