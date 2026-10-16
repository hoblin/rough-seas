using System;

namespace RoughSeas.Storms;

public class Waves(WavesConfig config)
{
    private const double Gravity = 9.81;

    public double Period(double height) => config.PeriodSecondsPerRootMetre * Math.Sqrt(height);

    public double BreakingChance(double windMps) =>
        Math.Min(1, config.WhitecapCoefficient * Math.Pow(windMps, config.WhitecapExponent));

    public bool BreaksAt(double depth, double height) => depth < config.BreakingDepthRatio * height;

    public double BreakerEnergy(double height, double beam)
    {
        var crestSpeed = Gravity * Period(height) / (2 * Math.PI);
        return config.BreakerTransfer * 0.5 * CrestMass(height, beam) * crestSpeed * crestSpeed;
    }

    /// <summary>The height of one wave in a sea of <paramref name="significantHeight"/>: the Rayleigh distribution at <paramref name="chance"/>.</summary>
    public static double OneWave(double significantHeight, double chance) =>
        significantHeight * Math.Sqrt(-Math.Log(1 - chance) / 2);

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
