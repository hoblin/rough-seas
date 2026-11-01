using System;
using System.Linq;

namespace RoughSeas.Storms;

/// <summary>
/// A swimmer's body as the water holds it up: its mass, and its volume by height from the feet, built from the
/// segments of <see cref="BodyBuildConfig"/>. Each segment's tissue stands evenly between its landmarks; the lungs'
/// air stands in the chest. The tissue's volume is the mass over the whole body's density by its fat, and the
/// segments' own densities only share that volume out among them.
/// </summary>
public sealed class Body
{
    private const double LitresPerCubicMetre = 1000;

    private readonly Slab[] tissue;
    private readonly Slab lungs;

    private Body(double massKg, double heightMetres, double breathingLitres, Slab[] tissue, Slab lungs)
    {
        (MassKg, HeightMetres, BreathingLitres, this.tissue, this.lungs) = (massKg, heightMetres, breathingLitres, tissue, lungs);
        TissueLitres = tissue.Sum(slab => slab.Litres);
    }

    public double MassKg { get; }

    public double HeightMetres { get; }

    public double TissueLitres { get; }

    /// <summary>The air in the lungs while breathing quietly at the surface: the end of a breath out, and half a breath more.</summary>
    public double BreathingLitres { get; }

    public static Body Of(BodyConfig config) => Of(config, config.BuildOf);

    public static Body Of(BodyConfig config, BodyBuildConfig build)
    {
        var height = build.HeightMetres;
        // the two-compartment body: fat and lean tissue at their own densities (Siri 1961)
        var density = 1 / (build.FatShare / config.FatDensity + (1 - build.FatShare) / config.LeanDensity);
        var litres = build.MassKg / density;
        var shares = build.Segments.Select(segment => segment.MassShare / segment.Density).ToArray();
        var scale = litres / shares.Sum();
        var slabs = build.Segments.Select((segment, i) => new Slab(segment.From * height, segment.To * height, shares[i] * scale)).ToArray();
        var chest = build.Segments.First(segment => segment.Lungs);
        var functional = build.Frc[0] * height + build.Frc[1] * build.AgeYears + build.Frc[2];
        return new Body(build.MassKg, height, functional + config.TidalLitres / 2, slabs,
            new Slab(chest.From * height, chest.To * height, 0));
    }

    public double Litres(double lungLitres) => TissueLitres + lungLitres;

    /// <summary>The litres of the body under water with the surface <paramref name="waterline"/> metres above the feet.</summary>
    public double LitresUnder(double waterline, double lungLitres)
    {
        var under = lungLitres * lungs.Under(waterline);
        foreach (var slab in tissue) under += slab.Litres * slab.Under(waterline);
        return under;
    }

    /// <summary>
    /// The upward acceleration, in metres a second squared, of a body of <paramref name="massKg"/> displacing
    /// <paramref name="litresUnder"/> of water of <paramref name="waterDensity"/> that accelerates up at
    /// <paramref name="waterAcceleration"/>: the water's weight it displaces against its own, and the pressure that
    /// accelerates the water around it and the water it drags along, a share <paramref name="addedMass"/> of what it
    /// displaces, over its mass and that water's. With the displaced volume taken under the drawn surface this is the
    /// linear wave force on a body small against the wave: ρ g A η from the waterline's rise and (ρ V + m_a) a from
    /// the water's acceleration (Newman 1977, Marine Hydrodynamics, 6.17), so a body as dense as the water moves with it.
    /// </summary>
    public static double Lift(double litresUnder, double waterDensity, double massKg, double addedMass, double gravity, double waterAcceleration = 0)
    {
        var displaced = waterDensity * litresUnder / LitresPerCubicMetre;
        return ((displaced - massKg) * gravity + (1 + addedMass) * displaced * waterAcceleration) / (massKg + addedMass * displaced);
    }

    /// <summary>
    /// The body's speed through the water up or down, <paramref name="metresPerSecond"/> now, after
    /// <paramref name="seconds"/> of the water's drag along its length (½ ρ Cd A v²), over its mass and the water it
    /// drags along. Solved implicitly, so a step never turns the body back.
    /// </summary>
    public static double Dragged(double metresPerSecond, double litresUnder, double waterDensity, double massKg, BodyConfig config, double seconds)
    {
        var inertia = massKg + config.AddedMass * waterDensity * litresUnder / LitresPerCubicMetre;
        var rate = 0.5 * waterDensity * config.DragArea / inertia;
        return metresPerSecond / (1 + rate * Math.Abs(metresPerSecond) * seconds);
    }

    private readonly record struct Slab(double From, double To, double Litres)
    {
        public double Under(double waterline) => Math.Clamp((waterline - From) / (To - From), 0, 1);
    }
}
