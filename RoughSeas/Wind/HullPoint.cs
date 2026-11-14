using System;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Wind;

public readonly record struct HullPoint(double Along, double Height, double Across)
{
    public static HullPoint Of(double[] metres) => new(metres[0], metres[1], metres[2]);

    public static HullPoint Of(JsonObject point) => Of(point.AsArray<double>() ?? throw new InvalidOperationException($"no point in {point}"));

    public static HullPoint operator +(HullPoint a, HullPoint b) => new(a.Along + b.Along, a.Height + b.Height, a.Across + b.Across);

    public static HullPoint operator -(HullPoint a, HullPoint b) => new(a.Along - b.Along, a.Height - b.Height, a.Across - b.Across);

    public static HullPoint operator -(HullPoint a) => new(-a.Along, -a.Height, -a.Across);

    public static HullPoint operator *(double k, HullPoint a) => new(k * a.Along, k * a.Height, k * a.Across);

    public double Length => Math.Sqrt(Dot(this));

    public HullPoint Unit => 1 / Length * this;

    public double Dot(HullPoint b) => Along * b.Along + Height * b.Height + Across * b.Across;

    public HullPoint Cross(HullPoint b) =>
        new(Height * b.Across - Across * b.Height, Across * b.Along - Along * b.Across, Along * b.Height - Height * b.Along);

    public HullPoint Turned(HullPoint origin, HullPoint axis, double radians)
    {
        var v = this - origin;
        var (cos, sin) = (Math.Cos(radians), Math.Sin(radians));
        return origin + cos * v + sin * axis.Cross(v) + (1 - cos) * axis.Dot(v) * axis;
    }
}
