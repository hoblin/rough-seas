using System;
using RoughSeas.Shore;

namespace RoughSeas.Surf;

public sealed class WallReflection(FacesConfig config, double spectralPeriodRatio)
{
    public double Coefficient(double height, double period, double slope, double roughness)
    {
        if (height <= 0 || period <= 0 || slope <= 0) return 0;
        var breaker = RunUp.Iribarren(height, period / spectralPeriodRatio, slope);
        var factor = config.ReflectionFactor * (1 - Math.Exp(-config.ReflectionRoughnessRate * roughness));
        var offset = roughness - config.ReflectionShapeRoughness;
        var shape = config.ReflectionShape * offset * offset + config.ReflectionShapeBase;
        return Math.Tanh(factor * Math.Pow(breaker, shape));
    }

    /// <summary>What is left of a reflection this far from a face of this length: the plane wave it sends out stays whole within the Fresnel distance, run² / wavelength, and thins as the square root of the distance beyond it.</summary>
    public static double Spreading(double wallRun, double wavelength, double distance) =>
        distance <= 0 || wavelength <= 0 ? 1 : Math.Min(1, wallRun / Math.Sqrt(wavelength * distance));

    public double Taper(double distance)
    {
        var from = config.TaperFrom * config.ReachBlocks;
        if (distance <= from) return 1;
        var share = Math.Clamp((config.ReachBlocks - distance) / (config.ReachBlocks - from), 0, 1);
        return share * share * (3 - 2 * share);
    }
}
