using System;

namespace RoughSeas.Storms;

public readonly record struct Anchor(double X, double Y, double Z, double RodeMetres, string Rope, double HoldingNewtons)
{
    public double Reach(double depth) => Math.Sqrt(Math.Max(0, RodeMetres * RodeMetres - depth * depth));

    public double Holding(double depth, double fullScope) =>
        depth <= 0 ? HoldingNewtons : HoldingNewtons * Math.Clamp((RodeMetres / depth - 1) / (fullScope - 1), 0, 1);

    public Anchor DraggedBy(double x, double z) => this with { X = X + x, Z = Z + z };
}
