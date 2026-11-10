using System;
using System.Collections.Generic;

namespace RoughSeas.Shore;

/// <summary>The run-up exceeded by 2% of waves: Stockdon's on a sandy slope up to about 1:7, EurOtop's on a steeper or harder one, and a standing wave's crest at a wall.</summary>
public sealed class RunUp(RunUpConfig config)
{
    public const double Gravity = 9.81;

    public double WallSlope => config.WallSlope;

    public static double DeepLength(double period) => Gravity * period * period / (2 * Math.PI);

    public static double Iribarren(double height, double period, double slope) =>
        height > 0 && period > 0 ? slope / Math.Sqrt(height / DeepLength(period)) : double.PositiveInfinity;

    public double Metres(double height, double period, double slope, BedConfig bed)
    {
        if (height <= 0 || period <= 0) return 0;
        if (slope >= config.WallSlope) return height;
        return bed.Sandy && slope <= config.SandyMaxSlope ? Stockdon(height, period, slope) : Eurotop(height, period, slope, bed.Roughness);
    }

    /// <summary>The run-up over a shore whose land, inland from the water, stands <paramref name="profile"/> metres over still water block by block, its slope read up to where the run-up reaches.</summary>
    public double Over(double height, double period, IReadOnlyList<double> profile, BedConfig bed)
    {
        if (height <= 0 || period <= 0 || profile.Count == 0) return 0;
        var reach = config.DissipativeFactor * Math.Sqrt(height * DeepLength(period));
        for (var i = 0; i < 3; i++) reach = Metres(height, period, Slope(profile, reach), bed);
        return reach;
    }

    /// <summary>The steps of a block shore read as a ramp from the waterline through the landward corner of the first block whose top the run-up reaches.</summary>
    public static double Slope(IReadOnlyList<double> profile, double reach)
    {
        for (var i = 0; i < profile.Count; i++)
        {
            if (profile[i] >= reach) return profile[i] / (i + 1);
        }
        return Math.Max(profile[^1], 0) / profile.Count;
    }

    private double Stockdon(double height, double period, double slope)
    {
        var scale = Math.Sqrt(height * DeepLength(period));
        if (Iribarren(height, period, slope) < config.DissipativeIribarren) return config.DissipativeFactor * scale;
        var setup = config.SetupSlopeFactor * slope * scale;
        var swash = Math.Sqrt(height * DeepLength(period) * (config.IncidentSwashFactor * slope * slope + config.InfragravitySwash));
        return config.StockdonFactor * (setup + swash / 2);
    }

    private double Eurotop(double height, double period, double slope, double roughness)
    {
        var breaker = Iribarren(height, period / config.SpectralPeriodRatio, slope);
        var reach = config.EurotopFactor * breaker;
        var ceiling = config.EurotopCeiling - config.EurotopCeilingRoot / Math.Sqrt(breaker);
        if (ceiling > 0) reach = Math.Min(reach, ceiling);
        return roughness * reach * height;
    }
}
