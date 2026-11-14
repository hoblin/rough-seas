using System;
using System.Linq;

namespace RoughSeas.Wind;

public sealed class Wing(FlowConfig flow, double kinematicViscosity)
{
    private const double Degree = Math.PI / 180;

    private const double TransitionReynolds = 5e5;

    public static Wing Of(WindConfig config) => new(config.Flow, config.AirMetresSquaredPerSecond);

    public double Friction(double metresPerSecond, double chordMetres)
    {
        var reynolds = Math.Max(1, metresPerSecond * chordMetres / kinematicViscosity);
        return 2 * (reynolds < TransitionReynolds ? 1.328 / Math.Sqrt(reynolds) : 0.074 * Math.Pow(reynolds, -0.2) - 1700 / reynolds);
    }

    public double Pressing(Lattice seen, double incidence)
    {
        var angle = Math.Min(Math.Abs(incidence), Math.PI / 2);
        var (sin, cos) = (Math.Sin(angle), Math.Cos(angle));
        var attached = seen.Potential * sin * cos + seen.Vortex * sin * sin;
        var separated = Across(seen.Aspect) * sin * (4 + Math.PI) / (4 + Math.PI * sin);
        var (breaks, gone) = Breaks(seen);
        var held = angle <= breaks ? 1 : angle >= gone ? 0 : (gone - angle) / (gone - breaks);
        return separated + held * (attached - separated);
    }

    private double Across(double aspect) => aspect > 0 ? flow.NormalPlate.Across + flow.NormalPlate.Aspect * (aspect + 1 / aspect) : flow.NormalPlate.Across;

    private (double Breaks, double Gone) Breaks(Lattice seen)
    {
        var (table, at) = seen.LeadingSweep >= flow.VortexSweepDegrees * Degree
            ? (flow.Deltas, seen.LeadingSweep / Degree)
            : (flow.Plates, seen.Aspect > 0 ? 1 / seen.Aspect : flow.Plates.Max(row => row[0]));
        var rows = table.OrderBy(row => row[0]).ToArray();
        var after = Array.FindIndex(rows, row => row[0] >= at);
        var (a, b) = after switch
        {
            < 0 => (rows[^1], rows[^1]),
            0 => (rows[0], rows[0]),
            _ => (rows[after - 1], rows[after])
        };
        var t = b[0] > a[0] ? (at - a[0]) / (b[0] - a[0]) : 0;
        return ((a[1] + t * (b[1] - a[1])) * Degree, (a[2] + t * (b[2] - a[2])) * Degree);
    }
}
