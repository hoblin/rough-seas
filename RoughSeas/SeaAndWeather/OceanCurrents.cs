using System;
using Vintagestory.API.MathTools;

namespace RoughSeas.SeaAndWeather;

/// <summary>
/// Wind-driven gyres after Stommel: across a basin the flow follows (1 − e^(−s/δ))(1 − s), s from the west shore, and
/// across latitude −cos(π·|latitude|/gyreDegrees), so subtropical gyres carry warm water poleward along western
/// shores and subpolar gyres carry cold water equatorward there. Wider basins run stronger gyres, up to full strength
/// where no shore is in reach.
/// </summary>
public class OceanCurrents
{
    private readonly CurrentsConfig config;

    public OceanCurrents(CurrentsConfig config)
    {
        this.config = config;
    }

    public SeaCurrent At(double latitudeDegrees, int polewardZ, Basin basin, double airCelsius)
    {
        if (!basin.IsSea) return new SeaCurrent(0, 0, Math.Max(config.SeawaterFreezingCelsius, airCelsius), 0);

        var s = basin.FromWest;
        var delta = config.BoundaryFraction;
        var strength = Math.Min(1, basin.Width / (2.0 * config.BasinReachBlocks));
        var gyre = Math.Abs(latitudeDegrees) * Math.PI / config.GyreDegrees;
        var meridional = -Math.Cos(gyre);
        var fromWestCoast = Math.Exp(-s / delta);
        var fromEastCoast = Math.Exp(-(1 - s) / delta);

        var shape = (1 - fromWestCoast) * (1 - s);
        var slope = fromWestCoast * (1 - s) / delta - (1 - fromWestCoast);
        var east = strength * config.InteriorMps * shape * -Math.Sin(gyre);
        var poleward = strength * config.WesternBoundaryMps * delta * slope * meridional;

        var gradient = Math.Sin(2 * Math.Abs(latitudeDegrees) * GameMath.DEG2RAD_DOUBLE);
        var warmth = strength * meridional * gradient * (config.WesternBoundaryCelsius * fromWestCoast - config.EasternBoundaryCelsius * fromEastCoast);
        return new SeaCurrent(east, poleward * polewardZ, Math.Max(config.SeawaterFreezingCelsius, airCelsius + warmth), warmth);
    }
}
