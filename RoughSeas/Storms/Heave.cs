using System;

namespace RoughSeas.Storms;

/// <summary>
/// A hull's heave by Newton: Archimedes' lift on her immersion against her weight, on her mass and the water she drags
/// with her, damped by the waves she radiates at <c>heaveDamping</c>, which <c>storms.json</c> sources.
/// </summary>
public readonly record struct Heave(double Kg, double AddedKg, double KgPerMetre, double DraftMetres, double DepthMetres, ResponseConfig Config)
{
    private double VirtualKg => Kg + AddedKg;

    private double Stiffness => HullResponse.Gravity * KgPerMetre;

    /// <summary>Her speed once her keel meets the water: the water she starts to carry takes its share of her momentum.</summary>
    public double Entering(double speed) => speed * Kg / VirtualKg;

    public double Acceleration(double immersionMetres, double metresPerSecond)
    {
        double damping = 2 * Config.HeaveDamping * Math.Sqrt(Stiffness * VirtualKg);
        double displaced = DraftMetres < DepthMetres ? Math.Min(immersionMetres, DepthMetres) : immersionMetres;
        double lifted = Stiffness * (displaced - DraftMetres);
        return (lifted - damping * metresPerSecond) / VirtualKg;
    }
}
