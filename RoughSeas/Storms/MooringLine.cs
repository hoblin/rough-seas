using System;

namespace RoughSeas.Storms;

public readonly record struct MooringLine(double RestMetres, double BreakingNewtons, double StretchAtBreak)
{
    private const double FullTurn = 2 * Math.PI;

    public double NewtonsPerMetre => BreakingNewtons / StretchAtBreak / RestMetres;

    /// <summary>
    /// One step of the line on a hull, solved exactly as a spring on her mass: her bow <paramref name="stretchMetres"/>
    /// past its reach and moving away at <paramref name="outwardMetresPerSecond"/>, with the water's steady pull on her
    /// along it, all across the water; <paramref name="level"/> is the cosine of the line's slope. Slack, it does nothing.
    /// </summary>
    public LineStep Over(double stretchMetres, double outwardMetresPerSecond, double steadyNewtons, double kilograms, double seconds, double level)
    {
        if (stretchMetres <= 0 || kilograms <= 0 || level <= 0 || seconds <= 0) return default;

        var stiffness = NewtonsPerMetre * level * level;
        var turning = Math.Sqrt(stiffness / kilograms);
        var held = steadyNewtons / stiffness;
        var (offset, lead) = (stretchMetres - held, outwardMetresPerSecond / turning);
        var swing = Math.Sqrt(offset * offset + lead * lead);
        var phase = Math.Atan2(lead, offset);
        var span = turning * seconds;
        if (swing >= Math.Abs(held))
        {
            var reach = Math.Acos(-held / swing);
            span = Math.Min(span, Math.Min(Positive(phase + reach), Positive(phase - reach)));
        }

        var stretch = held + swing * Math.Cos(span - phase);
        var speed = turning * swing * -Math.Sin(span - phase);
        var furthest = Positive(phase) < span ? held + swing : Math.Max(stretchMetres, stretch);
        return new LineStep(speed - outwardMetresPerSecond - steadyNewtons * span / turning / kilograms, Math.Max(0, stiffness * furthest / level));
    }

    public bool Parts(double newtons) => newtons > BreakingNewtons;

    private static double Positive(double angle)
    {
        var turned = angle % FullTurn;
        return turned <= 0 ? turned + FullTurn : turned;
    }
}

/// <param name="Kick">What the line's tension changes her velocity by along it over the step, m/s, toward the mooring when negative.</param>
/// <param name="PeakNewtons">The highest tension along the line within the step.</param>
public readonly record struct LineStep(double Kick, double PeakNewtons);
