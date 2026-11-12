using System;

namespace RoughSeas.Storms;

public readonly record struct MooringLine(double RestMetres, double BreakingNewtons, double StretchAtBreak)
{
    private const double FullTurn = 2 * Math.PI;
    private const int MostBounces = 16;

    public double NewtonsPerMetre => BreakingNewtons / StretchAtBreak / RestMetres;

    /// <summary>
    /// One step of the line on a hull, solved exactly as a spring on her mass: her bow <paramref name="stretchMetres"/>
    /// past its reach and moving away at <paramref name="outwardMetresPerSecond"/>, with the water's steady pull on her
    /// along it, all across the water; <paramref name="level"/> is the cosine of the line's slope. It takes her up where
    /// she reaches it within the step, and lets her off and takes her up again as often as the step lasts for; one she
    /// doesn't reach does nothing. A line too stiff to stretch stops her at its end.
    /// </summary>
    public LineStep Over(double stretchMetres, double outwardMetresPerSecond, double steadyNewtons, double kilograms, double seconds, double level)
    {
        if (kilograms <= 0 || level <= 0 || seconds <= 0) return default;

        var pull = steadyNewtons / kilograms;
        if (Reaching(stretchMetres, outwardMetresPerSecond, pull) is not { } reached || reached > seconds) return default;

        var stiffness = NewtonsPerMetre * level * level;
        if (double.IsInfinity(stiffness)) return Stopped(outwardMetresPerSecond, steadyNewtons, kilograms, seconds, level);

        var turning = Math.Sqrt(stiffness / kilograms);
        var held = steadyNewtons / stiffness;
        var (stretch, speed, left, furthest) = (stretchMetres, outwardMetresPerSecond, seconds, 0.0);
        for (var bounce = 0; left > 0; bounce++)
        {
            var free = bounce < MostBounces ? Math.Min(left, Reaching(stretch, speed, pull) ?? left) : left;
            (stretch, speed, left) = (stretch + speed * free + pull * free * free / 2, speed + pull * free, left - free);
            if (left <= 0) break;

            var (offset, lead) = (Math.Max(0, stretch) - held, speed / turning);
            var swing = Math.Sqrt(offset * offset + lead * lead);
            var phase = Math.Atan2(lead, offset);
            var span = turning * left;
            var slackens = swing > held && span >= Positive(phase + Math.Acos(-held / swing));
            if (slackens) span = Positive(phase + Math.Acos(-held / swing));
            furthest = Math.Max(furthest, Positive(phase) < span ? held + swing : Math.Max(offset + held, held + swing * Math.Cos(span - phase)));
            (stretch, speed, left) = (slackens ? 0 : held + swing * Math.Cos(span - phase), turning * swing * -Math.Sin(span - phase), left - span / turning);
        }

        return new LineStep(
            speed - outwardMetresPerSecond - pull * seconds,
            (stretch - stretchMetres) / seconds - outwardMetresPerSecond - pull * seconds,
            Math.Max(0, stiffness * furthest / level));
    }

    /// <summary>How long until her bow, moving freely, takes the line up: none if it never will.</summary>
    private static double? Reaching(double stretchMetres, double outwardMetresPerSecond, double pull)
    {
        if (stretchMetres > 0 || (stretchMetres == 0 && (outwardMetresPerSecond > 0 || (outwardMetresPerSecond == 0 && pull > 0)))) return 0;
        if (stretchMetres == 0) return pull > 0 ? -2 * outwardMetresPerSecond / pull : null;

        var discriminant = outwardMetresPerSecond * outwardMetresPerSecond - 2 * pull * stretchMetres;
        if (discriminant < 0) return null;

        var closing = outwardMetresPerSecond + Math.Sqrt(discriminant);
        return closing > 0 ? -2 * stretchMetres / closing : null;
    }

    /// <summary>
    /// One step of an anchor dragging along the bed at the end of its rode: it holds her back by
    /// <paramref name="holdingNewtons"/> along the rode while she pulls it out, and never draws her back.
    /// </summary>
    public static LineStep Dragging(double outwardMetresPerSecond, double steadyNewtons, double holdingNewtons, double kilograms, double seconds, double level)
    {
        var kick = -Math.Min(holdingNewtons * level * seconds / kilograms, Math.Max(0, outwardMetresPerSecond + steadyNewtons * seconds / kilograms));
        return new LineStep(kick, kick, holdingNewtons);
    }

    public bool Parts(double newtons) => newtons > BreakingNewtons;

    private static LineStep Stopped(double outwardMetresPerSecond, double steadyNewtons, double kilograms, double seconds, double level)
    {
        var stopped = Math.Max(0, outwardMetresPerSecond + steadyNewtons * seconds / kilograms);
        return new LineStep(-stopped, -stopped, stopped * kilograms / seconds / level);
    }

    private static double Positive(double angle)
    {
        var turned = angle % FullTurn;
        return turned <= 0 ? turned + FullTurn : turned;
    }
}

/// <param name="Kick">What the line's tension changes her velocity by along it over the step, m/s, toward the mooring when negative.</param>
/// <param name="Moved">What to add to her velocity, with the step's steady pull already in it, for her to move over the step as far as she really does: the rest of the kick is hers at the step's end.</param>
/// <param name="PeakNewtons">The highest tension along the line within the step.</param>
public readonly record struct LineStep(double Kick, double Moved, double PeakNewtons);
