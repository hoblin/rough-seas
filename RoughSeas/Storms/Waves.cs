using System;
using RoughSeas.Water;

namespace RoughSeas.Storms;

public class Waves(WavesConfig config)
{
    /// <summary>The speed of a breaker this high: as long as its breaking steepness makes it, and no faster than the sea's own waves.</summary>
    public double BreakerCelerity(double height, double period, double depth)
    {
        var ownPeriod = Math.Sqrt(2 * Math.PI * height / config.BreakingSteepness / WaveTheory.Gravity);
        return WaveTheory.Celerity(Math.Min(period, ownPeriod), depth);
    }

    public double BreakingChance(double windMps) =>
        Math.Min(1, config.WhitecapCoefficient * Math.Pow(windMps, config.WhitecapExponent));

    public double BreakingDepthRatio => config.BreakingDepthRatio;

    public double CrestRatio => config.CrestRatio;

    public (double From, double To) PlungingIribarren => (config.PlungingIribarren[0], config.PlungingIribarren[1]);

    /// <summary>Whether a breaker of this Iribarren number plunges rather than spills.</summary>
    public bool PlungingBreaker(double iribarren) => iribarren >= config.PlungingIribarren[0] && iribarren < config.PlungingIribarren[1];

    public double BreakingDepth(double height) => config.BreakingDepthRatio * height;

    public bool Plunges(double height, double period, double bedSlope, double depth)
    {
        if (depth >= BreakingDepth(height) || !PlungingBreaker(WaveTheory.Iribarren(bedSlope, height, WaveTheory.DeepLength(period)))) return false;

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

    public double Crest(double height, bool breaking) => height * (breaking ? config.BreakingCrestRatio : config.CrestRatio);

    public double ShippedLitres(double height, double beam) =>
        config.ShippedWaterFraction * CrestMass(height, beam) / config.WaterDensity * 1000;

    private double CrestMass(double height, double beam) => config.WaterDensity * beam * height * height / 4;
}
