using System;

namespace RoughSeas.Water;

/// <summary>
/// A blade driven back through the water on a power: it slips back at s while the body it drives moves on at v, so its
/// thrust is k s² and the power it spends k s² (v + s), the useful part T v and the rest lost to the water pushed back.
/// k is ½ ρ Cd A, times the share of the time the blade drives.
/// </summary>
public static class SlippingBlade
{
    private const int Bisections = 60;

    public static double Thrust(double newtonsPerSlipSquared, double metresPerSecond, double watts)
    {
        if (watts <= 0 || newtonsPerSlipSquared <= 0) return 0;
        var ahead = Math.Max(0, metresPerSecond);
        var slip = Solve(s => newtonsPerSlipSquared * s * s * (ahead + s), watts);
        return newtonsPerSlipSquared * slip * slip;
    }

    private static double Solve(Func<double, double> rising, double target)
    {
        double low = 0, high = 1;
        while (rising(high) < target) high *= 2;
        for (var i = 0; i < Bisections; i++)
        {
            var mid = (low + high) / 2;
            if (rising(mid) < target) low = mid;
            else high = mid;
        }
        return (low + high) / 2;
    }
}
