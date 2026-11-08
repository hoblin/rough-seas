using System;

namespace RoughSeas.Storms;

public readonly record struct Anchor(double X, double Y, double Z, double RodeMetres, string Rope, double Kilograms, double BedHold)
{
    private const double Gravity = 9.81;

    public static Anchor? LetGo(double x, double seabedY, double z, double depth, double rodeAboard, double fullScope, string rope, double kilograms, double bedHold) =>
        depth > rodeAboard ? null : new Anchor(x, seabedY, z, Math.Min(rodeAboard, depth * fullScope), rope, kilograms, bedHold);

    public double Reach(double depth) => Math.Sqrt(Math.Max(0, RodeMetres * RodeMetres - depth * depth));

    public double Holding(double depth, double stoneDensity, double waterDensity)
    {
        var weight = Kilograms * Gravity;
        var submerged = weight * (1 - waterDensity / stoneDensity);
        var friction = BedHold * weight / submerged;
        var rise = Math.Clamp(depth / RodeMetres, 0, 1);
        return friction * submerged / (Math.Sqrt(1 - rise * rise) + friction * rise);
    }

    public Anchor DraggedBy(double x, double z) => this with { X = X + x, Z = Z + z };
}
