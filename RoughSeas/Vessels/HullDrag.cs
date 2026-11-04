using System;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Vessels;

public readonly record struct HullForm(double LengthMetres, double BeamMetres, double BowDrag, double SideDrag, double FinSquareMetres)
{
    public static HullForm From(JsonObject attributes, DragConfig defaults, HullShape shape) => new(
        2 * shape.HalfLength,
        2 * shape.HalfBeam,
        attributes["bowDrag"].AsDouble(defaults.BowDrag),
        attributes["sideDrag"].AsDouble(defaults.SideDrag),
        attributes["finSquareMetres"].AsDouble());
}

/// <summary>
/// The water's drag on a hull and how she answers it, along her and across her apart (Hoerner's cross-flow principle).
/// Along: ITTC-1957 friction on Mumford's wetted surface by the form factor, and a blunt bow's own drag on beam by draft.
/// Across: cross-flow drag on her length by draft and any keel or float, strip by strip along her as she turns. She
/// answers with her mass and the water she carries with her: Lamb's prolate spheroid of her length and beam along, a
/// flat plate of her draft across (Newman 1977). Speeds are m/s through the water, forces newtons, torques N·m.
/// </summary>
public sealed class HullDrag
{
    private const double FrictionLine = 0.075;
    private const double FrictionLineOffset = 2;
    private const double MumfordSides = 1.7;
    private const double SpinMoment = 32;
    private const double RodMoment = 12;

    private readonly HullForm form;
    private readonly DragConfig config;
    private readonly double halfDensity;
    private readonly double draft;
    private readonly double wettedSquareMetres;
    private readonly double sideSquareMetres;
    private readonly WaveMaking waves;

    public HullDrag(HullForm form, double draftMetres, double kilograms, double waterDensity, DragConfig config)
    {
        this.form = form;
        this.config = config;
        halfDensity = waterDensity / 2;
        draft = draftMetres;
        wettedSquareMetres = MumfordSides * form.LengthMetres * draft + kilograms / waterDensity / draft;
        sideSquareMetres = form.LengthMetres * draft + form.FinSquareMetres;
        waves = new WaveMaking(form.LengthMetres, form.BeamMetres, draft, waterDensity);
        AlongKg = kilograms * (1 + SurgeAddedShare(form.LengthMetres / form.BeamMetres));
        var acrossKg = kilograms + halfDensity * Math.PI * draft * draft * form.LengthMetres;
        TurningKgSquareMetres = acrossKg * form.LengthMetres * form.LengthMetres / RodMoment;
    }

    public double AlongKg { get; }

    public double TurningKgSquareMetres { get; }

    public double Along(double metresPerSecond) =>
        -AlongDrag(metresPerSecond) * metresPerSecond * Math.Abs(metresPerSecond) - Math.Sign(metresPerSecond) * waves.Newtons(metresPerSecond);

    public double Turning(double radiansPerSecond)
    {
        var length = form.LengthMetres;
        return -SideDrag / length * radiansPerSecond * Math.Abs(radiansPerSecond) * Math.Pow(length, 4) / SpinMoment;
    }

    private double SideDrag => halfDensity * form.SideDrag * sideSquareMetres;

    private double AlongDrag(double speed)
    {
        var reynolds = Math.Max(Math.Abs(speed) * form.LengthMetres / config.KinematicViscosity, config.TransitionReynolds);
        var friction = FrictionLine / Math.Pow(Math.Log10(reynolds) - FrictionLineOffset, 2);
        return halfDensity * (friction * config.FormFactor * wettedSquareMetres + form.BowDrag * form.BeamMetres * draft);
    }

    public static double SurgeAddedShare(double slenderness)
    {
        if (slenderness <= 1.01) return 0.5;
        var e = Math.Sqrt(1 - 1 / (slenderness * slenderness));
        var alpha = 2 * (1 - e * e) / (e * e * e) * (0.5 * Math.Log((1 + e) / (1 - e)) - e);
        return alpha / (2 - alpha);
    }
}

public class DragConfig
{
    public double KinematicViscosity;
    public double TransitionReynolds;
    public double FormFactor;
    public double BowDrag;
    public double SideDrag;
}
