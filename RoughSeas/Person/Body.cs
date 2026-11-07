using System;
using System.Linq;

namespace RoughSeas.Person;

/// <summary>
/// A person's body as the water holds it up: its mass, and its volume by height from the feet, built from the segments of
/// <see cref="BodyConfig"/>. Each segment's tissue stands evenly between its landmarks; the lungs' air stands in the chest.
/// The tissue's volume is the mass over the whole body's density by its fat, and the segments' own densities only share
/// that volume out among them.
/// </summary>
public sealed class Body
{
    public const double LitresPerCubicMetre = 1000;

    private readonly BodyConfig config;
    private readonly Slab[] tissue;
    private readonly Slab lungs;

    private Body(BodyConfig config, double breathingLitres, Slab[] tissue, Slab lungs)
    {
        (this.config, BreathingLitres, this.tissue, this.lungs) = (config, breathingLitres, tissue, lungs);
        TissueLitres = tissue.Sum(slab => slab.Litres);
    }

    public double MassKg => config.MassKg;

    public double HeightMetres => config.HeightMetres;

    public double TissueLitres { get; }

    /// <summary>The air in the lungs while breathing quietly at the surface, at the surface's pressure: the end of a breath out, and half a breath more.</summary>
    public double BreathingLitres { get; }

    /// <summary>How far above the feet the lungs' air stands, in metres.</summary>
    public double ChestMetres => (lungs.From + lungs.To) / 2;

    public static Body Of(BodyConfig config)
    {
        var height = config.HeightMetres;
        // the two-compartment body: fat and lean tissue at their own densities (Siri 1961)
        var density = 1 / (config.FatShare / config.FatDensity + (1 - config.FatShare) / config.LeanDensity);
        var litres = config.MassKg / density;
        var shares = config.Segments.Select(segment => segment.MassShare / segment.Density).ToArray();
        var scale = litres / shares.Sum();
        var slabs = config.Segments.Select((segment, i) => new Slab(segment.From * height, segment.To * height, shares[i] * scale)).ToArray();
        var chest = config.Segments.First(segment => segment.Lungs);
        var functional = config.Frc[0] * height + config.Frc[1] * config.AgeYears + config.Frc[2];
        return new Body(config, functional + config.TidalLitres / 2, slabs, new Slab(chest.From * height, chest.To * height, 0));
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
    /// Air the lungs hold at the surface's pressure, by Boyle's law, with the chest <paramref name="chestDepth"/> metres
    /// under a surface of water of <paramref name="waterDensity"/>: the water's pressure squeezes it.
    /// </summary>
    public double Squeezed(double lungLitres, double chestDepth, double waterDensity, double gravity) =>
        lungLitres * config.AtmospherePascals / (config.AtmospherePascals + waterDensity * gravity * Math.Max(0, chestDepth));

    /// <summary>
    /// The water the body drags along up and down: a disc of the body's section, (8/3) ρ a³ with a its radius, at the
    /// waterline while the surface cuts the body, at its widest once it is under.
    /// </summary>
    public double AddedKg(double waterline, double lungLitres, double waterDensity)
    {
        var section = waterline < HeightMetres ? SectionAt(waterline, lungLitres) : Widest(lungLitres);
        return config.AddedMassPerRadiusCubed * waterDensity * Math.Pow(section / Math.PI, 1.5);
    }

    /// <summary>
    /// The upward acceleration, in metres a second squared, of <paramref name="massKg"/> displacing
    /// <paramref name="litresUnder"/> of water of <paramref name="waterDensity"/> that accelerates up at
    /// <paramref name="waterAcceleration"/>, dragging <paramref name="addedKg"/> along: the water's weight it displaces
    /// against its own, and the pressure that accelerates the water around it and the water it drags. With the displaced
    /// volume taken under the drawn surface this is the linear wave force on a body small against the wave: ρ g A η from
    /// the waterline's rise and (ρ V + m_a) a from the water's acceleration (Journée and Massie 2001, Offshore
    /// Hydromechanics, 6.3.3), so a body as dense as the water moves with it.
    /// </summary>
    public static double Lift(double litresUnder, double waterDensity, double massKg, double addedKg, double gravity, double waterAcceleration = 0)
    {
        var displaced = waterDensity * litresUnder / LitresPerCubicMetre;
        return ((displaced - massKg) * gravity + (displaced + addedKg) * waterAcceleration) / (massKg + addedKg);
    }

    /// <summary>
    /// A speed through the water, <paramref name="metresPerSecond"/> now, after <paramref name="seconds"/> of its drag
    /// (½ ρ Cd A v²) on <paramref name="inertiaKg"/>. Solved implicitly, so a step never turns the body back.
    /// </summary>
    public static double Dragged(double metresPerSecond, double waterDensity, double dragArea, double inertiaKg, double seconds)
    {
        var rate = 0.5 * waterDensity * dragArea / inertiaKg;
        return metresPerSecond / (1 + rate * Math.Abs(metresPerSecond) * seconds);
    }

    // square metres of the body in plan at a height above the feet
    private double SectionAt(double height, double lungLitres)
    {
        var section = lungs.Across(height, lungLitres);
        foreach (var slab in tissue) section += slab.Across(height, slab.Litres);
        return section;
    }

    private double Widest(double lungLitres) => tissue.Max(slab => SectionAt((slab.From + slab.To) / 2, lungLitres));

    private readonly record struct Slab(double From, double To, double Litres)
    {
        public double Under(double waterline) => Math.Clamp((waterline - From) / (To - From), 0, 1);

        public double Across(double height, double litres) =>
            height >= From && height < To ? litres / LitresPerCubicMetre / (To - From) : 0;
    }
}
