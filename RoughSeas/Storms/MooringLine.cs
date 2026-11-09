using System;

namespace RoughSeas.Storms;

public readonly record struct MooringLine(double RestMetres, double BreakingNewtons, double StretchAtBreak)
{
    public double NewtonsPerMetre => BreakingNewtons / StretchAtBreak / RestMetres;

    /// <param name="snatches">Whether she has just come to the line's end at <paramref name="arrivingMetresPerSecond"/>; held at it, the line carries the steady pull alone.</param>
    public double Tension(double steadyNewtons, double arrivingMetresPerSecond, double kilograms, bool snatches)
    {
        var steady = Math.Max(0, steadyNewtons);
        if (!snatches) return steady;

        var speed = Math.Max(0, arrivingMetresPerSecond);
        var energy = speed > 0 ? NewtonsPerMetre * kilograms * speed * speed : 0;
        return steady + Math.Sqrt(steady * steady + energy);
    }

    public bool Parts(double newtons) => newtons > BreakingNewtons;
}
