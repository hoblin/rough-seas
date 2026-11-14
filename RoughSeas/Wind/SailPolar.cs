using System;
using System.Linq;
using Vintagestory.API.MathTools;

namespace RoughSeas.Wind;

/// <summary>
/// A sail's force over the dynamic pressure on its area, as drive along the hull and side force across it, at the
/// apparent wind's angle off her bow and the sail's incidence to it.
/// </summary>
public abstract class SailPolar
{
    public static SailPolar Of(SailConfig config) =>
        config.LiftAndDrag.Length > 0 ? new IncidencePolar(config.LiftAndDrag, config.FrictionDrag) : new HeadingPolar(config.Headings);

    public abstract bool Luffs(double fromBow);

    public abstract (double Drive, double Side) At(double fromBow, double incidence);
}

/// <summary>Measured with the sail trimmed to each heading: its force follows the heading alone.</summary>
public sealed class HeadingPolar(double[][] headings) : SailPolar
{
    private readonly (double Radians, double Drive, double Side)[] headings =
        headings.Select(h => (h[0] * GameMath.DEG2RAD, h[1], h[2])).OrderBy(h => h.Item1).ToArray();

    public override bool Luffs(double fromBow) => fromBow < headings[0].Radians;

    public override (double Drive, double Side) At(double fromBow, double incidence)
    {
        var after = Array.FindIndex(headings, h => h.Radians >= fromBow);
        if (after < 0) return (headings[^1].Drive, headings[^1].Side);
        if (after == 0) return (headings[0].Drive, headings[0].Side);

        var (a, b) = (headings[after - 1], headings[after]);
        var t = (fromBow - a.Radians) / (b.Radians - a.Radians);
        return (a.Drive + (b.Drive - a.Drive) * t, a.Side + (b.Side - a.Side) * t);
    }
}

/// <summary>
/// Measured lift and drag over the sail's trims, each at the incidence where the cloth's pressure, which stands normal
/// to it, and its friction along the wind make them; edge-on to the wind it keeps its friction alone.
/// </summary>
public sealed class IncidencePolar : SailPolar
{
    private readonly (double Radians, double Lift, double Drag)[] trims;

    public IncidencePolar(double[][] liftAndDrag, double friction) =>
        trims = liftAndDrag.Select(pair => (Math.Atan2(pair[1] - friction, pair[0]), pair[0], pair[1]))
            .Prepend((0, 0, friction)).OrderBy(trim => trim.Item1).ToArray();

    public override bool Luffs(double fromBow) => false;

    public override (double Drive, double Side) At(double fromBow, double incidence)
    {
        var (lift, drag) = LiftAndDrag(Math.Clamp(incidence, 0, Math.PI / 2));
        var (sin, cos) = (Math.Sin(fromBow), Math.Cos(fromBow));
        return (lift * sin - drag * cos, lift * cos + drag * sin);
    }

    public double Friction => trims[0].Drag;

    public double Pressing(double incidence)
    {
        var (lift, drag) = LiftAndDrag(Math.Clamp(Math.Abs(incidence), 0, Math.PI / 2));
        return Math.Sqrt(lift * lift + Math.Pow(drag - trims[0].Drag, 2));
    }

    private (double Lift, double Drag) LiftAndDrag(double incidence)
    {
        var after = Array.FindIndex(trims, trim => trim.Radians >= incidence);
        if (after < 0) return (trims[^1].Lift, trims[^1].Drag);
        if (after == 0) return (trims[0].Lift, trims[0].Drag);

        var (a, b) = (trims[after - 1], trims[after]);
        var t = (incidence - a.Radians) / (b.Radians - a.Radians);
        return (a.Lift + (b.Lift - a.Lift) * t, a.Drag + (b.Drag - a.Drag) * t);
    }
}
