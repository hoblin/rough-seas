using System;

namespace RoughSeas.Vessels;

/// <summary>
/// Draft and freeboard by Archimedes. A hull with a hold floats on its waterplane until its side is under; a solid
/// one (a raft, a reed boat), given no waterplane, is pressed down through its own depth and holds its load awash.
/// Flooded or solid, it sinks when what the water does not hold up outweighs what its material keeps afloat.
/// </summary>
public sealed class Flotation(double depthMetres, double waterplaneSquareMetres, double materialLitres)
{
    private const double LitresPerCubicMetre = 1000;

    public double DepthMetres => depthMetres;

    public bool HasHold => waterplaneSquareMetres > 0;

    public double BuoyantKg => materialLitres * VesselMass.SeawaterKgPerLitre;

    public Afloat Float(Aboard aboard, bool flooded)
    {
        if (depthMetres <= 0) return default;
        if ((flooded || !HasHold) && aboard.Sinking > BuoyantKg) return new Afloat(depthMetres, 0, true);

        double draft = HasHold
            ? flooded ? depthMetres : aboard.Total / (VesselMass.SeawaterKgPerLitre * LitresPerCubicMetre * waterplaneSquareMetres)
            : Math.Min(depthMetres, depthMetres * aboard.Total / BuoyantKg);
        double inside = HasHold ? aboard.Water / (VesselMass.SeawaterKgPerLitre * LitresPerCubicMetre * waterplaneSquareMetres) : 0;
        return new Afloat(draft, depthMetres - draft, false, Math.Min(inside, depthMetres));
    }
}

/// <param name="InsideMetres">How high the water in her hold stands above her keel.</param>
public readonly record struct Afloat(double DraftMetres, double FreeboardMetres, bool Sunk, double InsideMetres = 0);
