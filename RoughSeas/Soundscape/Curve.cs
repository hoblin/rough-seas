using System;
using System.Linq;

namespace RoughSeas.Soundscape;

public static class Curve
{
    public static double At(double[][] points, double x)
    {
        if (points.Length == 0) return 0;
        if (x <= points[0][0]) return points[0][1];

        for (var i = 1; i < points.Length; i++)
        {
            var (from, to) = (points[i - 1], points[i]);
            if (x <= to[0]) return from[1] + (to[1] - from[1]) * (x - from[0]) / (to[0] - from[0]);
        }
        return points[^1][1];
    }

    public static double[] Mix(BedsConfig beds, double windMps)
    {
        var shares = beds.Loops.Select(loop => Math.Max(0, At(loop.Share, windMps))).ToArray();
        var power = Math.Sqrt(shares.Sum(share => share * share));
        var level = At(beds.Lufs, windMps);
        var presence = Math.Min(1, shares.Sum());
        return beds.Loops.Select((loop, i) => power > 0 ? presence * shares[i] / power * Math.Pow(10, (level - loop.Lufs) / 20) : 0).ToArray();
    }
}
