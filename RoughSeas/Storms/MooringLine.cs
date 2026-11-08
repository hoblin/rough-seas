using System;

namespace RoughSeas.Storms;

public readonly record struct MooringLine(double RestMetres, double BreakingNewtons, double StretchAtBreak)
{
    public double NewtonsPerMetre => BreakingNewtons / StretchAtBreak / RestMetres;

    public double Tension(double steadyNewtons, double arrivingMetresPerSecond, double kilograms, bool wasTaut)
    {
        var steady = Math.Max(0, steadyNewtons);
        if (wasTaut) return steady;

        var speed = Math.Max(0, arrivingMetresPerSecond);
        return steady + Math.Sqrt(steady * steady + NewtonsPerMetre * kilograms * speed * speed);
    }

    public bool Parts(double newtons) => newtons > BreakingNewtons;
}
