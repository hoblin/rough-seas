using System;

namespace RoughSeas.Storms;

public class Waves(WavesConfig config)
{
    private const double Gravity = 9.81;

    /// <summary>A wave's phase speed in water this deep, by Eckart's approximation to linear dispersion.</summary>
    public static double Celerity(double period, double depth)
    {
        var deep = Gravity * period / (2 * Math.PI);
        return deep * Math.Sqrt(Math.Tanh(2 * Math.PI / period / deep * depth));
    }

    /// <summary>The speed of a breaker this high: as long as its breaking steepness makes it, and no faster than the sea's own waves.</summary>
    public double BreakerCelerity(double height, double period, double depth) =>
        Math.Min(Celerity(period, depth), Math.Sqrt(Gravity * height / config.BreakingSteepness / (2 * Math.PI)));

    public double BreakingChance(double windMps) =>
        Math.Min(1, config.WhitecapCoefficient * Math.Pow(windMps, config.WhitecapExponent));

    public bool BreaksAt(double depth, double height) => depth < BreakingDepth(height);

    public double BreakingDepth(double height) => config.BreakingDepthRatio * height;

    public bool Plunges(double height, double period, double bedSlope, double depth)
    {
        var iribarren = bedSlope / Math.Sqrt(height / (Gravity * period * period / (2 * Math.PI)));
        if (iribarren < config.PlungingIribarren[0] || iribarren > config.PlungingIribarren[1]) return false;

        var pastBreaking = (BreakingDepth(height) - depth) / bedSlope;
        return pastBreaking <= height * (config.PlungeDistance[0] - config.PlungeDistance[1] * bedSlope);
    }

    public bool PlungesAtSea(double draw) => draw < config.PlungingShareAtSea;

    /// <summary>The pressure a breaker presses on a flat side: a plunging crest's slam, a spilling roller's stagnation.</summary>
    public double SlamPressure(double celerity, bool plunging)
    {
        var speed = plunging ? config.PlungingSpeedRatio * celerity : celerity;
        return 0.5 * config.WaterDensity * (plunging ? config.SlamCoefficient : 1) * speed * speed;
    }

    /// <summary>The height of one wave in a sea of <paramref name="significantHeight"/>, which <paramref name="draw"/> of all waves fall below: wave heights follow the Rayleigh distribution.</summary>
    public static double WaveHeight(double significantHeight, double draw) =>
        significantHeight * Math.Sqrt(-Math.Log(1 - draw) / 2);

    public double Crest(double height, bool breaking) => height * (breaking ? config.BreakingCrestRatio : config.CrestRatio);

    public double ShippedLitres(double height, double beam) =>
        config.ShippedWaterFraction * CrestMass(height, beam) / config.WaterDensity * 1000;

    public double Force(double windMps, double height, double beam, double windage) =>
        0.5 * config.AirDensity * config.WindageDragCoefficient * windage * windMps * windMps
        + config.WaveDriftCoefficient * config.WaterDensity * Gravity * height * height * beam;

    /// <summary>
    /// The chance one wave pulls past <paramref name="limit"/> when the significant wave pulls <paramref name="force"/>:
    /// wave heights follow the Rayleigh distribution, and the pull grows with height squared.
    /// </summary>
    public static double ChanceToExceed(double force, double limit) => force <= 0 ? 0 : Math.Exp(-2 * limit / force);

    private double CrestMass(double height, double beam) => config.WaterDensity * beam * height * height / 4;
}
