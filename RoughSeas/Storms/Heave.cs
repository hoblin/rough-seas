using System;

namespace RoughSeas.Storms;

/// <summary>
/// A hull's heave by Newton: Archimedes' lift on her immersion against her weight, on her mass and the water she drags
/// with her, damped by the water.
/// </summary>
public readonly record struct Heave(double Kg, double KgPerMetre, double DraftMetres, double DepthMetres, ResponseConfig Config)
{
    private double VirtualKg => Kg * (1 + Config.HeaveAddedMass);

    private double Stiffness => HullResponse.Gravity * KgPerMetre;

    public double Acceleration(double immersionMetres, double metresPerSecond)
    {
        double damping = 2 * Config.HeaveDamping * Math.Sqrt(Stiffness * VirtualKg);
        double displaced = DraftMetres < DepthMetres ? Math.Min(immersionMetres, DepthMetres) : immersionMetres;
        double lifted = Stiffness * (displaced - DraftMetres);
        return (lifted - damping * metresPerSecond) / VirtualKg;
    }
}
