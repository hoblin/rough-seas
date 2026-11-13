using System;
using System.Linq;
using Vintagestory.API.MathTools;

namespace RoughSeas.Wind;

public sealed class SailPolar
{
    private readonly (double Radians, double Drive, double Side)[] headings;

    public SailPolar(SailConfig config) =>
        headings = config.Headings.Select(h => (h[0] * GameMath.DEG2RAD, h[1], h[2])).OrderBy(h => h.Item1).ToArray();

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
}
