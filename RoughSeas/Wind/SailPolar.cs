using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.MathTools;

namespace RoughSeas.Wind;

public sealed class SailPolar
{
    private const int StepsAlongPolar = 20;
    private const int Degrees = 180;

    private readonly (double Radians, double Drive, double Side)[] headings;

    public SailPolar(SailConfig config) => headings = config.LiftAndDrag.Length > 0
        ? Trimmed(config.LiftAndDrag).ToArray()
        : config.Headings.Select(h => (h[0] * GameMath.DEG2RAD, h[1], h[2])).OrderBy(h => h.Item1).ToArray();

    public double LuffsBelow => headings[0].Radians;

    public (double Drive, double Side)? At(double radians)
    {
        if (radians < LuffsBelow) return null;

        var after = Array.FindIndex(headings, h => h.Radians >= radians);
        if (after < 0) return (headings[^1].Drive, headings[^1].Side);
        if (after == 0) return (headings[0].Drive, headings[0].Side);

        var (a, b) = (headings[after - 1], headings[after]);
        var t = (radians - a.Radians) / (b.Radians - a.Radians);
        return (a.Drive + (b.Drive - a.Drive) * t, a.Side + (b.Side - a.Side) * t);
    }

    private static IEnumerable<(double, double, double)> Trimmed(double[][] liftAndDrag)
    {
        var trims = liftAndDrag.Zip(liftAndDrag.Skip(1))
            .SelectMany(pair => Enumerable.Range(0, StepsAlongPolar).Select(k => (double)k / StepsAlongPolar)
                .Select(t => (Lift: pair.First[0] + (pair.Second[0] - pair.First[0]) * t, Drag: pair.First[1] + (pair.Second[1] - pair.First[1]) * t)))
            .Append((Lift: liftAndDrag[^1][0], Drag: liftAndDrag[^1][1]))
            .ToArray();
        for (var degrees = 1; degrees <= Degrees; degrees++)
        {
            var radians = degrees * GameMath.DEG2RAD;
            var (sin, cos) = (Math.Sin(radians), Math.Cos(radians));
            var best = trims.OrderByDescending(trim => trim.Lift * sin - trim.Drag * cos).ThenBy(trim => Math.Abs(trim.Lift * cos + trim.Drag * sin)).First();
            var drive = best.Lift * sin - best.Drag * cos;
            if (drive > 0) yield return (radians, drive, best.Lift * cos + best.Drag * sin);
        }
    }
}
