using System;
using RoughSeas.Vessels;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Storms;

public readonly record struct HullForm(double LengthMetres, double BeamMetres, double BowDrag, double SideDrag, double FinSquareMetres)
{
    public static HullForm From(JsonObject attributes, DriftConfig defaults, double lengthMetres, double beamMetres) => new(
        attributes["lengthMetres"].AsDouble(lengthMetres),
        attributes["beamMetres"].AsDouble(beamMetres),
        attributes["bowDrag"].AsDouble(defaults.BowDrag),
        attributes["sideDrag"].AsDouble(defaults.SideDrag),
        attributes["finSquareMetres"].AsDouble());
}

/// <summary>
/// The water's drag on a hull and how she answers it, along her and across her apart (Hoerner's cross-flow principle).
/// Along: ITTC-1957 friction on Mumford's wetted surface by the form factor, and a blunt bow's own drag on beam by draft.
/// Across: cross-flow drag on her length by draft and any keel or float. She answers with her mass and the water she
/// carries with her: Lamb's prolate spheroid of her length and beam along, a flat plate of her draft across (Newman 1977).
/// Velocities are the water's relative to her, in m/s east and south; forces are newtons on her.
/// </summary>
public sealed class HullDrag
{
    private const int Strips = 8;
    private const int SwingSteps = 4;
    private const double FrictionLine = 0.075;
    private const double FrictionLineOffset = 2;
    private const double MumfordSides = 1.7;

    private readonly HullForm form;
    private readonly DriftConfig config;
    private readonly double halfDensity;
    private readonly double draft;
    private readonly double wettedSquareMetres;
    private readonly double alongKg;
    private readonly double acrossKg;
    private readonly double sideSquareMetres;

    public HullDrag(HullForm form, double draftMetres, double kilograms, double waterDensity, DriftConfig config)
    {
        this.form = form;
        this.config = config;
        halfDensity = waterDensity / 2;
        draft = draftMetres;
        Kilograms = kilograms;
        wettedSquareMetres = MumfordSides * form.LengthMetres * draft + kilograms / waterDensity / draft;
        alongKg = kilograms * (1 + SurgeAddedShare(form.LengthMetres / form.BeamMetres));
        acrossKg = kilograms + halfDensity * Math.PI * draft * draft * form.LengthMetres;
        sideSquareMetres = form.LengthMetres * draft + form.FinSquareMetres;
    }

    public double Kilograms { get; }

    public (double X, double Z) Pull(double waterX, double waterZ, double yaw)
    {
        var (along, across) = HullShape.ToHull(waterX, waterZ, yaw);
        return HullShape.ToWorld(AlongDrag(along) * along * Math.Abs(along), SideDrag * across * Math.Abs(across), yaw);
    }

    /// <summary>The water's velocity relative to her after <paramref name="seconds"/> of its drag alone.</summary>
    public (double X, double Z) Settle(double waterX, double waterZ, double yaw, double seconds)
    {
        var (along, across) = HullShape.ToHull(waterX, waterZ, yaw);
        return HullShape.ToWorld(Settle(along, AlongDrag(along) / alongKg, seconds), Settle(across, SideDrag / acrossKg, seconds), yaw);
    }

    /// <summary>Her turn in radians of yaw a second after <paramref name="seconds"/>, held at her bow while the water runs past.</summary>
    public double Swing(double waterX, double waterZ, double yaw, double turnPerSecond, double seconds)
    {
        var across = HullShape.ToHull(waterX, waterZ, yaw).Across;
        var length = form.LengthMetres;
        var perMetre = SideDrag / length;
        var inertia = acrossKg * length * length / 3;
        var step = seconds / SwingSteps;
        var strip = length / Strips;
        for (var i = 0; i < SwingSteps; i++)
        {
            var torque = 0.0;
            for (var s = 0; s < Strips; s++)
            {
                var aft = (s + 0.5) * strip;
                var flow = across + turnPerSecond * aft;
                torque -= aft * perMetre * flow * Math.Abs(flow) * strip;
            }
            turnPerSecond += torque / inertia * step;
        }
        return turnPerSecond;
    }

    private double SideDrag => halfDensity * form.SideDrag * sideSquareMetres;

    private double AlongDrag(double speed)
    {
        var reynolds = Math.Max(Math.Abs(speed) * form.LengthMetres / config.KinematicViscosity, config.TransitionReynolds);
        var friction = FrictionLine / Math.Pow(Math.Log10(reynolds) - FrictionLineOffset, 2);
        return halfDensity * (friction * config.FormFactor * wettedSquareMetres + form.BowDrag * form.BeamMetres * draft);
    }

    private static double Settle(double speed, double dragPerKg, double seconds) => speed / (1 + dragPerKg * Math.Abs(speed) * seconds);

    public static double SurgeAddedShare(double slenderness)
    {
        if (slenderness <= 1.01) return 0.5;
        var e = Math.Sqrt(1 - 1 / (slenderness * slenderness));
        var alpha = 2 * (1 - e * e) / (e * e * e) * (0.5 * Math.Log((1 + e) / (1 - e)) - e);
        return alpha / (2 - alpha);
    }
}
