using System;
using RoughSeas.Vessels;

namespace RoughSeas.Storms;

/// <summary>
/// What a hull shows the water below her waterline, and how she answers the water moving past her: the drag of her
/// underwater body on the water's velocity relative to her own, over her mass plus the water she carries with her.
/// Along her length the drag is the friction on her wetted surface and the pressure on her ends; across it, the
/// pressure on her side. One answer for every force that moves a hull through the water: the current, a rower's
/// stroke, the wind's push.
/// </summary>
/// <example><code>
/// var body = UnderwaterBody.Of(length, beam, afloat.DraftMetres, aboard.Total, config.Underwater, storms.Config.Drift);
/// var (vx, vz) = body.Carry(vx, vz, waterX, waterZ, yaw, rowX, rowZ, seconds);
/// </code></example>
public sealed class UnderwaterBody
{
    private const double MaxStepSeconds = 0.05;
    private const int MaxSteps = 40;
    private const int Strips = 16;
    private const double MinDraftMetres = 0.005;

    private readonly double density;
    private readonly double viscosity;
    private readonly double formFactor;
    private readonly double wettedSquareMetres;
    private readonly double endPressure;
    private readonly double sidePressure;
    private readonly double appendageSide;
    private readonly double appendageEnds;

    private UnderwaterBody(double lengthMetres, double beamMetres, double draftMetres, double massKg, UnderwaterConfig hull, DriftConfig config)
    {
        LengthMetres = lengthMetres;
        BeamMetres = beamMetres;
        DraftMetres = draftMetres;
        MassKg = massKg;
        density = config.WaterDensity;
        viscosity = config.KinematicViscosity;
        formFactor = config.FormFactor;
        double displaced = massKg / density;
        // Mumford's wetted surface, from length, draft and displacement
        wettedSquareMetres = 1.7 * lengthMetres * draftMetres + displaced / draftMetres;
        FrontalSquareMetres = beamMetres * draftMetres + hull.Appendage.FrontalSquareMetres;
        LateralSquareMetres = lengthMetres * draftMetres + hull.Appendage.LateralSquareMetres;
        endPressure = hull.BowOnDrag * beamMetres * draftMetres;
        sidePressure = hull.BroadsideDrag * lengthMetres * draftMetres;
        appendageEnds = hull.Appendage.BowOnDrag * hull.Appendage.FrontalSquareMetres;
        appendageSide = hull.Appendage.BroadsideDrag * hull.Appendage.LateralSquareMetres;
        SurgeKg = massKg * (1 + SpheroidSurgeAddedMass(lengthMetres, beamMetres));
        SwayKg = massKg + density * Math.PI * draftMetres * draftMetres / 2 * lengthMetres;
    }

    public double LengthMetres { get; }

    public double BeamMetres { get; }

    public double DraftMetres { get; }

    public double MassKg { get; }

    /// <summary>The area she shows bow-on under water, m².</summary>
    public double FrontalSquareMetres { get; }

    /// <summary>The area she shows broadside under water, m².</summary>
    public double LateralSquareMetres { get; }

    /// <summary>Her mass with the water she drags along when she moves ahead or astern, kg.</summary>
    public double SurgeKg { get; }

    /// <summary>Her mass with the water she drags along when she moves sideways, kg.</summary>
    public double SwayKg { get; }

    /// <summary>
    /// The body of a hull of this length and beam floating at this draft with this mass aboard, or null where she
    /// has no draft to show the water.
    /// </summary>
    public static UnderwaterBody? Of(double lengthMetres, double beamMetres, double draftMetres, double massKg, UnderwaterConfig hull, DriftConfig config) =>
        lengthMetres > 0 && beamMetres > 0 && draftMetres > MinDraftMetres && massKg > 0
            ? new UnderwaterBody(lengthMetres, beamMetres, draftMetres, massKg, hull, config)
            : null;

    /// <summary>
    /// Newtons on the hull, in her own frame, from water running past her at <paramref name="along"/> m/s toward her
    /// stern and <paramref name="across"/> m/s across her: each pushes her the way the water runs.
    /// </summary>
    public (double Along, double Across) Drag(double along, double across) =>
        (AlongCoefficient(along) * along * Math.Abs(along), SideCoefficient * across * Math.Abs(across));

    /// <summary>
    /// The force in world axes, newtons, that water at (<paramref name="waterX"/>, <paramref name="waterZ"/>) m/s puts
    /// on a hull at heading <paramref name="yaw"/> moving at (<paramref name="vx"/>, <paramref name="vz"/>).
    /// </summary>
    public (double X, double Z) Pull(double vx, double vz, double waterX, double waterZ, double yaw)
    {
        var (along, across) = HullShape.ToHull(waterX - vx, waterZ - vz, yaw);
        var (fAlong, fAcross) = Drag(along, across);
        return HullShape.ToWorld(fAlong, fAcross, yaw);
    }

    /// <summary>
    /// Her velocity over the ground, m/s, <paramref name="seconds"/> later: carried by the water at
    /// (<paramref name="waterX"/>, <paramref name="waterZ"/>) and driven by a stroke that would hold her at
    /// (<paramref name="rowX"/>, <paramref name="rowZ"/>) through still water, as much of it as lies along her length.
    /// </summary>
    public (double X, double Z) Carry(double vx, double vz, double waterX, double waterZ, double yaw, double rowX, double rowZ, double seconds)
    {
        var (along, across) = HullShape.ToHull(vx - waterX, vz - waterZ, yaw);
        if (!(seconds > 0)) return (vx, vz);

        // the stroke's thrust is the drag it holds her against in still water
        double row = HullShape.ToHull(rowX, rowZ, yaw).Along;
        double thrust = AlongCoefficient(row) * row * Math.Abs(row);
        int steps = Math.Clamp((int)Math.Ceiling(seconds / MaxStepSeconds), 1, MaxSteps);
        double step = seconds / steps;
        for (int i = 0; i < steps; i++)
        {
            // implicit in the drag, so that a short light hull settles instead of overshooting
            along = (along + thrust / SurgeKg * step) / (1 + AlongCoefficient(along) * Math.Abs(along) / SurgeKg * step);
            across /= 1 + SideCoefficient * Math.Abs(across) / SwayKg * step;
        }
        var (x, z) = HullShape.ToWorld(along, across, yaw);
        return (waterX + x, waterZ + z);
    }

    /// <summary>
    /// How she swings about her bow, held there by a rode, with water running past her at <paramref name="across"/> m/s
    /// across her frame (the water along her turns her nowhere): her yaw rate, radians a second, <paramref name="seconds"/>
    /// later, and the angle she turned through, both counterclockwise in her own frame. Each length of her side takes the
    /// water's pressure on its velocity across her less its own as she turns, so the drag that turns her also damps her.
    /// </summary>
    public (double Rate, double Turned) Swing(double rate, double across, double seconds)
    {
        if (!(seconds > 0)) return (rate, 0);

        // about the bow: a rod's own moment of inertia and its centre's, half its length away
        double inertia = SwayKg * LengthMetres * LengthMetres / 3;
        int steps = Math.Clamp((int)Math.Ceiling(seconds / MaxStepSeconds), 1, MaxSteps);
        double step = seconds / steps;
        double turned = 0;
        for (int i = 0; i < steps; i++)
        {
            rate += Torque(rate, across) / inertia * step;
            turned += rate * step;
        }
        return (rate, turned);
    }

    /// <summary>
    /// Newton metres about the bow: each strip of her side, r metres aft of it, meets the water across her less its own
    /// swing, ω r.
    /// </summary>
    public double Torque(double rate, double across)
    {
        double strip = LengthMetres / Strips;
        double perMetre = SideCoefficient / LengthMetres;
        double torque = 0;
        for (int i = 0; i < Strips; i++)
        {
            double aft = (i + 0.5) * strip;
            double water = across - rate * aft;
            torque += perMetre * water * Math.Abs(water) * strip * aft;
        }
        return torque;
    }

    /// <summary>½ ρ C_D A along her length, kg/m: friction on her wetted surface by the ITTC 1957 line, pressure on her ends.</summary>
    private double AlongCoefficient(double speed)
    {
        double reynolds = Math.Max(Math.Abs(speed), 0.01) * LengthMetres / viscosity;
        double friction = 0.075 / Math.Pow(Math.Log10(reynolds) - 2, 2);
        return 0.5 * density * (formFactor * friction * wettedSquareMetres + endPressure + appendageEnds);
    }

    private double SideCoefficient => 0.5 * density * (sidePressure + appendageSide);

    /// <summary>
    /// The share of her own displaced mass a hull drags along moving ahead, as a prolate spheroid of her length and beam
    /// does (Lamb 1932, Hydrodynamics; Newman 1977, Marine Hydrodynamics).
    /// </summary>
    public static double SpheroidSurgeAddedMass(double lengthMetres, double beamMetres)
    {
        double ratio = Math.Min(beamMetres, lengthMetres) / Math.Max(beamMetres, lengthMetres);
        double e = Math.Sqrt(1 - ratio * ratio);
        if (e < 1e-3) return 0.5;

        double alpha = 2 * (1 - e * e) / (e * e * e) * (0.5 * Math.Log((1 + e) / (1 - e)) - e);
        return alpha / (2 - alpha);
    }
}

/// <summary>A hull's underwater drag, from its <c>roughseas:seaforces</c> <c>underwater</c> attributes.</summary>
public class UnderwaterConfig
{
    /// <summary>Pressure drag of her ends on her beam times her draft; her friction comes on top.</summary>
    public double BowOnDrag;

    /// <summary>Pressure drag of her side on her length times her draft.</summary>
    public double BroadsideDrag = 1;

    /// <summary>A keel or an outrigger's float, which adds its own fixed area to each side.</summary>
    public AppendageConfig Appendage = new();
}

public class AppendageConfig
{
    public double FrontalSquareMetres;
    public double LateralSquareMetres;
    public double BowOnDrag;
    public double BroadsideDrag;
}

public class DriftConfig
{
    public double WaterDensity;
    public double KinematicViscosity;
    public double FormFactor;
    public double LogSeconds;
}
