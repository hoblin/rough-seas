using System;

namespace RoughSeas.Vessels;

public sealed class Flotation(double depthMetres, double waterplaneSquareMetres, double materialLitres)
{
    private const double LitresPerCubicMetre = 1000;

    public double DepthMetres => depthMetres;

    public bool HasHold => waterplaneSquareMetres > 0;

    public double BuoyantKg => materialLitres * VesselMass.SeawaterKgPerLitre;

    public Afloat Float(Aboard aboard, bool flooded)
    {
        if ((flooded || !HasHold) && aboard.Sinking > BuoyantKg) return new Afloat(depthMetres, 0, true);

        double draft = HasHold
            ? flooded ? depthMetres : aboard.Total / (VesselMass.SeawaterKgPerLitre * LitresPerCubicMetre * waterplaneSquareMetres)
            : Math.Min(depthMetres, depthMetres * aboard.Total / BuoyantKg);
        return new Afloat(draft, depthMetres - draft, false);
    }
}

public readonly record struct Afloat(double DraftMetres, double FreeboardMetres, bool Sunk);
