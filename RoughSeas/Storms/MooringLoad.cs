using System;
using System.Collections.Generic;
using System.Linq;

namespace RoughSeas.Storms;

public static class MooringLoad
{
    private const double Parallel = 1e-6;

    /// <summary>
    /// What each taut line carries of a steady pull, newtons, along the line from the mooring out to her: one line the
    /// pull's part along it, two lines tensions that sum to the pull. A line the balance would push goes slack.
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

                var (alongA, alongB) = (Along(pullX, pullZ, a), Along(pullX, pullZ, b));
                return alongA >= alongB ? [alongA, 0] : [0, alongB];
            }
        }

        var alongEach = outward.Select(line => Along(pullX, pullZ, line)).ToArray();
        var pulling = alongEach.Count(newtons => newtons > 0);
        return alongEach.Select(newtons => pulling > 0 ? newtons / pulling : 0).ToArray();
    }

    /// <summary>The shares while one line slides at a fixed pull: the rest of the pull shared by the other lines.</summary>
    public static double[] Beside(double pullX, double pullZ, IReadOnlyList<(double X, double Z)> outward, int sliding, double slidingNewtons)
    {
        var (slideX, slideZ) = outward[sliding];
        var others = outward.Where((_, i) => i != sliding).ToArray();
        var shares = Share(pullX - slidingNewtons * slideX, pullZ - slidingNewtons * slideZ, others).ToList();
        shares.Insert(sliding, slidingNewtons);
        return shares.ToArray();
    }

    /// <summary>
    /// The share of her mass each line stops as she runs out at this velocity: lines stretched together share her as
    /// springs side by side do, by their stiffness along the way she moves, so a stiff short line takes most of the jerk
    /// and one she runs across takes none. Lying still, they share by their stiffness alone; a line too stiff to stretch
    /// takes her whole.
    /// </summary>
    public static double[] Masses(double velocityX, double velocityZ, IReadOnlyList<(double X, double Z)> outward, IReadOnlyList<double> newtonsPerMetre)
    {
        var rigid = newtonsPerMetre.Select(double.IsInfinity).ToArray();
        double[] Shared(Func<int, double> weight)
        {
            var weights = Enumerable.Range(0, outward.Count).Select(weight).ToArray();
            var total = weights.Sum();
            return total > 0 ? weights.Select(w => w / total).ToArray() : weights;
        }

        if (rigid.Any(r => r)) return Shared(i => rigid[i] ? 1 : 0);
        var moving = Shared(i => newtonsPerMetre[i] * Square(Along(velocityX, velocityZ, outward[i])));
        return moving.Sum() > 0 ? moving : Shared(i => newtonsPerMetre[i]);
    }

    private static double Along(double pullX, double pullZ, (double X, double Z) line) => Math.Max(0, pullX * line.X + pullZ * line.Z);

    private static double Square(double value) => value * value;
}
