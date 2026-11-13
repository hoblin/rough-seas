using System;

namespace RoughSeas.Wind;

public readonly record struct HullPoint(double Along, double Height, double Across)
{
    public static HullPoint Of(double[] metres) => new(metres[0], metres[1], metres[2]);

    public static HullPoint operator +(HullPoint a, HullPoint b) => new(a.Along + b.Along, a.Height + b.Height, a.Across + b.Across);

    public static HullPoint operator -(HullPoint a, HullPoint b) => new(a.Along - b.Along, a.Height - b.Height, a.Across - b.Across);

    public static HullPoint operator *(double k, HullPoint a) => new(k * a.Along, k * a.Height, k * a.Across);

    public double Length => Math.Sqrt(Along * Along + Height * Height + Across * Across);
}

public sealed class SailForm(HullPoint tack, HullPoint peak, HullPoint clew, double camber)
{
    private const double CentroidReach = 2.0 / 3;
    private const double MidSpan = 0.5;

    public HullPoint At(double reach, double span, double trim, double fill)
    {
        var edge = (1 - span) * clew + span * peak;
        var flat = tack + reach * (edge - tack);
        var depth = fill * 4 * camber * reach * (peak - clew).Length * span * (1 - span);
        var (along, across) = (flat.Along - tack.Along, flat.Across - tack.Across + depth);
        var (cos, sin) = (Math.Cos(trim), Math.Sin(trim));
        return new HullPoint(tack.Along + cos * along - sin * across, flat.Height, tack.Across + sin * along + cos * across);
    }

    public HullPoint Centre(double reef, double trim, double fill) => At(CentroidReach * reef, MidSpan, trim, fill);
}
