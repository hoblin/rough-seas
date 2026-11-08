using System;
using System.Collections.Generic;
using System.Linq;

namespace RoughSeas.Storms;

public static class MooringLoad
{
    private const double Parallel = 1e-6;

    /// <summary>
    /// What each taut line carries of a steady pull, newtons: their tensions along them, from the mooring out to her,
    /// sum to the pull. A line the balance would push rather than pull goes slack and carries nothing.
    /// </summary>
    public static double[] Share(double pullX, double pullZ, IReadOnlyList<(double X, double Z)> outward)
    {
        if (outward.Count == 2)
        {
            var (a, b) = (outward[0], outward[1]);
            var determinant = a.X * b.Z - a.Z * b.X;
            if (Math.Abs(determinant) > Parallel)
            {
                var first = (pullX * b.Z - pullZ * b.X) / determinant;
                var second = (a.X * pullZ - a.Z * pullX) / determinant;
                if (first >= 0 && second >= 0) return [first, second];
                return first < 0 ? [0, Along(pullX, pullZ, b)] : [Along(pullX, pullZ, a), 0];
            }
        }

        var alongEach = outward.Select(line => Along(pullX, pullZ, line)).ToArray();
        var pulling = alongEach.Count(newtons => newtons > 0);
        return alongEach.Select(newtons => pulling > 0 ? newtons / pulling : 0).ToArray();
    }

    private static double Along(double pullX, double pullZ, (double X, double Z) line) => Math.Max(0, pullX * line.X + pullZ * line.Z);
}
