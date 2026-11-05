using System;

namespace RoughSeas.Vessels;

/// <summary>
/// The water a flat waterplane drags with it as it heaves: Lamb's elliptic disc, (4/3)πρab²/E(e) in open water
/// (Hydrodynamics, §373), of which the free surface carries half at high frequency (Newman, Marine Hydrodynamics). For a
/// long plate that is strip theory's ½ρπ(b/2)² a metre, for a round one (4/3)ρa³. It is the high-frequency limit, an
/// upper bound on what a hull carries at her own heave period.
/// </summary>
public static class HeaveAddedMass
{
    private const double KgPerCubicMetre = VesselMass.SeawaterKgPerLitre * 1000;
    private const int MostMeanSteps = 30;

    public static double OfPlate(double halfLengthMetres, double halfBeamMetres)
    {
        double major = Math.Max(halfLengthMetres, halfBeamMetres);
        double minor = Math.Min(halfLengthMetres, halfBeamMetres);
        if (minor <= 0) return 0;

        double eccentricity = Math.Sqrt(1 - minor * minor / (major * major));
        return 2.0 / 3 * Math.PI * KgPerCubicMetre * major * minor * minor / SecondKind(eccentricity);
    }

    /// <summary>An elliptic waterplane of this area and half-beam.</summary>
    public static double OfWaterplane(double squareMetres, double halfBeamMetres) =>
        halfBeamMetres > 0 ? OfPlate(squareMetres / (Math.PI * halfBeamMetres), halfBeamMetres) : 0;

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
