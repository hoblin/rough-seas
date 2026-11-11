using System;

namespace RoughSeas.Water;

/// <summary>
/// The water a body drags with it as it moves, by Lamb (Hydrodynamics, §373). A flat waterplane heaving at the surface is
/// Lamb's elliptic disc, (4/3)πρab²/E(e) in open water, of which the free surface carries half at high frequency (Newman,
/// Marine Hydrodynamics): for a long plate that is strip theory's ½ρπ(b/2)² a metre, for a round one (4/3)ρa³, the
/// high-frequency limit and an upper bound on what a hull carries at her own heave period. A body wholly under water is
/// Lamb's prolate spheroid of its own length and volume.
/// </summary>
public static class AddedMass
{
    private const int MostMeanSteps = 30;

    public static double OfPlate(double halfLengthMetres, double halfBeamMetres, double kgPerCubicMetre)
    {
        double major = Math.Max(halfLengthMetres, halfBeamMetres);
        double minor = Math.Min(halfLengthMetres, halfBeamMetres);
        if (minor <= 0) return 0;

        double eccentricity = Math.Sqrt(1 - minor * minor / (major * major));
        return 2.0 / 3 * Math.PI * kgPerCubicMetre * major * minor * minor / SecondKind(eccentricity);
    }

    /// <summary>An elliptic waterplane of this area and half-beam.</summary>
    public static double OfWaterplane(double squareMetres, double halfBeamMetres, double kgPerCubicMetre) =>
        halfBeamMetres > 0 ? OfPlate(squareMetres / (Math.PI * halfBeamMetres), halfBeamMetres, kgPerCubicMetre) : 0;

    /// <summary>Along its length and across it, by Lamb's coefficients α₀/(2 − α₀) and β₀/(2 − β₀) of its displaced water.</summary>
    public static (double Along, double Across) OfSpheroid(double lengthMetres, double cubicMetres, double kgPerCubicMetre)
    {
        var major = lengthMetres / 2;
        var minor = Math.Sqrt(3 * cubicMetres / (4 * Math.PI * major));
        var (along, across) = SpheroidShares(major / minor);
        var displaced = kgPerCubicMetre * cubicMetres;
        return (along * displaced, across * displaced);
    }

    /// <summary>The shares of its displaced water a prolate spheroid this many times as long as it is wide carries along its length and across it.</summary>
    public static (double Along, double Across) SpheroidShares(double slenderness)
    {
        if (slenderness <= 1.01) return (0.5, 0.5);
        var e = Math.Sqrt(1 - 1 / (slenderness * slenderness));
        var log = Math.Log((1 + e) / (1 - e));
        var along = 2 * (1 - e * e) / (e * e * e) * (log / 2 - e);
        var across = 1 / (e * e) - (1 - e * e) / (2 * e * e * e) * log;
        return (along / (2 - along), across / (2 - across));
    }

    /// <summary>Legendre's complete elliptic integral of the second kind, by the arithmetic-geometric mean.</summary>
    public static double SecondKind(double eccentricity)
    {
        if (eccentricity >= 1) return 1;

        double arithmetic = 1, geometric = Math.Sqrt(1 - eccentricity * eccentricity);
        double weight = 0.5, lost = weight * eccentricity * eccentricity;
        for (int step = 0; step < MostMeanSteps && arithmetic - geometric > 1e-15; step++)
        {
            double half = (arithmetic - geometric) / 2;
            (arithmetic, geometric) = ((arithmetic + geometric) / 2, Math.Sqrt(arithmetic * geometric));
            weight *= 2;
            lost += weight * half * half;
        }
        return Math.PI / (2 * arithmetic) * (1 - lost);
    }
}
