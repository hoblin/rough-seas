using System;
using RoughSeas.Water;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Vessels;

public readonly record struct HullForm(double LengthMetres, double BeamMetres, double BowDrag, double SideDrag, double FinSquareMetres)
{
    public static HullForm From(JsonObject attributes, DragConfig defaults, HullShape shape) => new(
        2 * shape.HalfLengthMetres,
        2 * shape.HalfBeamMetres,
        attributes["bowDrag"].AsDouble(defaults.BowDrag),
        attributes["sideDrag"].AsDouble(defaults.SideDrag),
        attributes["finSquareMetres"].AsDouble());
}

/// <summary>
/// The water's drag on a hull and how she answers it, along her and across her apart (Hoerner's cross-flow principle).
/// Along: ITTC-1957 friction on Mumford's wetted surface by the form factor, and a blunt bow's own drag on beam by draft.
/// Across: cross-flow drag on her length by draft and any keel or float, strip by strip along her as she turns. She
/// answers with her mass and the water she carries with her: Lamb's prolate spheroid of her length and beam along, a
/// flat plate of her draft across (Newman 1977). Under way she also resists turning in proportion to her speed: the
/// linear yaw damping of Clarke, Gedling and Hine's regression (Trans. RINA 125, 1983), whose slender-body term alone is
/// the lift her underwater body makes against the turn. Speeds are m/s through the water, forces newtons, torques N·m.
/// Where a velocity is the water's relative to her, it is m/s east and south in world axes.
/// </summary>
public sealed class HullDrag
{
    private const double FrictionLine = 0.075;
    private const double FrictionLineOffset = 2;
    private const double MumfordSides = 1.7;
    private const double SpinMoment = 32;
    private const double RodMoment = 12;
    private const double SlenderYawDamping = 0.25;
    private const double BeamOnDraftYawDamping = 0.039;
    private const double BeamOnLengthYawDamping = 0.56;
    private const double EndMoment = 3;
    private const int SwingStrips = 8;
    private const int SwingSteps = 4;
    private const double SlopeNudge = 1e-4;
    private const double StiffestStep = 0.1;
    private const int MostSteps = 50;

    private readonly HullForm form;
    private readonly DragConfig config;
    private readonly double halfDensity;
    private readonly double draft;
    private readonly double wettedSquareMetres;
    private readonly double sideSquareMetres;
    private readonly WaveMaking waves;
    private readonly double yawDamping;

    public HullDrag(HullForm form, double draftMetres, double kilograms, double waterDensity, DragConfig config)
    {
        this.form = form;
        this.config = config;
        halfDensity = waterDensity / 2;
        draft = draftMetres;
        wettedSquareMetres = MumfordSides * form.LengthMetres * draft + kilograms / waterDensity / draft;
        sideSquareMetres = form.LengthMetres * draft + form.FinSquareMetres;
        waves = new WaveMaking(form.LengthMetres, form.BeamMetres, draft, waterDensity);
        Kilograms = kilograms;
        AlongKg = kilograms * (1 + AddedMass.SpheroidShares(form.LengthMetres / form.BeamMetres).Along);
        AcrossKg = kilograms + halfDensity * Math.PI * draft * draft * form.LengthMetres;
        TurningKgSquareMetres = AcrossKg * form.LengthMetres * form.LengthMetres / RodMoment;
        var clarke = SlenderYawDamping + BeamOnDraftYawDamping * form.BeamMetres / draft - BeamOnLengthYawDamping * form.BeamMetres / form.LengthMetres;
        yawDamping = Math.PI * halfDensity * draft * draft * form.LengthMetres * form.LengthMetres * Math.Max(0, clarke);
    }

    public double Kilograms { get; }

    public double AlongKg { get; }

    public double AcrossKg { get; }

    public double TurningKgSquareMetres { get; }

    public double Along(double metresPerSecond) =>
        -AlongDrag(metresPerSecond) * metresPerSecond * Math.Abs(metresPerSecond) - Math.Sign(metresPerSecond) * waves.Newtons(metresPerSecond);

    public double Turning(double radiansPerSecond, double metresPerSecond)
    {
        var length = form.LengthMetres;
        return -SideDrag / length * radiansPerSecond * Math.Abs(radiansPerSecond) * Math.Pow(length, 4) / SpinMoment
               - yawDamping * Math.Abs(metresPerSecond) * radiansPerSecond;
    }

    public double Across(double metresPerSecond) => -SideDrag * metresPerSecond * Math.Abs(metresPerSecond);

    /// <summary>The water's drag on her, newtons in world axes, less what her own way <paramref name="wayAlong"/> meets alone, which her rower pays.</summary>
    public (double X, double Z) Pull(double waterX, double waterZ, double yaw, double wayAlong = 0)
    {
        var (along, across) = HullShape.ToHull(waterX, waterZ, yaw);
        return HullShape.ToWorld(Along(wayAlong - along) - Along(wayAlong), Across(-across), yaw);
    }

    /// <summary>
    /// The water's velocity relative to her after <paramref name="seconds"/> of its drag, as <see cref="Pull"/> takes
    /// it, and of a push of <paramref name="pushX"/>, <paramref name="pushZ"/> m/s². Solved implicitly, in steps short
    /// against the drag's own time, so a long step can't carry her past the water and a push held by the drag holds
    /// her exactly where they balance.
    /// </summary>
    public (double X, double Z) Settle(double waterX, double waterZ, double yaw, double seconds, double wayAlong = 0, double pushX = 0, double pushZ = 0)
    {
        var (along, across) = HullShape.ToHull(waterX, waterZ, yaw);
        var (pushAlong, pushAcross) = HullShape.ToHull(pushX, pushZ, yaw);
        return HullShape.ToWorld(Settled(true, along, pushAlong, wayAlong, seconds), Settled(false, across, pushAcross, 0, seconds), yaw);
    }

    public (double X, double Z) Newtons(double accelerationX, double accelerationZ, double yaw)
    {
        var (along, across) = HullShape.ToHull(accelerationX, accelerationZ, yaw);
        return HullShape.ToWorld(along * AlongKg, across * AcrossKg, yaw);
    }

    public (double X, double Z) Acceleration(double newtonsX, double newtonsZ, double yaw)
    {
        var (along, across) = HullShape.ToHull(newtonsX, newtonsZ, yaw);
        return HullShape.ToWorld(along / AlongKg, across / AcrossKg, yaw);
    }

    /// <summary>Her mass and the water she drags as a pull along this unit vector meets them.</summary>
    public double KilogramsToward(double unitX, double unitZ, double yaw)
    {
        var (along, across) = HullShape.ToHull(unitX, unitZ, yaw);
        return 1 / (along * along / AlongKg + across * across / AcrossKg);
    }

    /// <summary>
    /// Her turn in radians of yaw a second after <paramref name="seconds"/>, held at her bow with the water running past:
    /// each strip of her side meets the water across it less its own swing, about the moment of a rod at its end.
    /// </summary>
    public double Swing(double waterX, double waterZ, double yaw, double turnPerSecond, double seconds)
    {
        var across = HullShape.ToHull(waterX, waterZ, yaw).Across;
        var length = form.LengthMetres;
        var perMetre = SideDrag / length;
        var moment = AcrossKg * length * length / EndMoment;
        var step = seconds / SwingSteps;
        var strip = length / SwingStrips;
        for (var i = 0; i < SwingSteps; i++)
        {
            var torque = 0.0;
            for (var s = 0; s < SwingStrips; s++)
            {
                var aft = (s + 0.5) * strip;
                var flow = across + turnPerSecond * aft;
                torque -= aft * perMetre * flow * Math.Abs(flow) * strip;
            }
            turnPerSecond += torque / moment * step;
        }
        return turnPerSecond;
    }

    private double Settled(bool along, double relative, double push, double way, double seconds)
    {
        var steps = Math.Clamp((int)Math.Ceiling(seconds * Slope(along, relative, way) / StiffestStep), 1, MostSteps);
        var step = seconds / steps;
        for (var i = 0; i < steps; i++) relative -= step * (push + Pulled(along, relative, way)) / (1 + step * Slope(along, relative, way) / 2);
        return relative;
    }

    private double Slope(bool along, double relative, double way)
    {
        var nudge = Math.Max(SlopeNudge, Math.Abs(relative) * SlopeNudge);
        return Math.Max(0, (Pulled(along, relative + nudge, way) - Pulled(along, relative - nudge, way)) / (2 * nudge));
    }

    private double Pulled(bool along, double relative, double way) =>
        along ? (Along(way - relative) - Along(way)) / AlongKg : Across(-relative) / AcrossKg;

    private double SideDrag => halfDensity * form.SideDrag * sideSquareMetres;

    private double AlongDrag(double speed)
    {
        var reynolds = Math.Max(Math.Abs(speed) * form.LengthMetres / config.KinematicViscosity, config.TransitionReynolds);
        var friction = FrictionLine / Math.Pow(Math.Log10(reynolds) - FrictionLineOffset, 2);
        return halfDensity * (friction * config.FormFactor * wettedSquareMetres + form.BowDrag * form.BeamMetres * draft);
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
