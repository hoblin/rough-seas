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

public sealed class SailForm
{
    private const int CentroidCells = 16;

    private readonly HullPoint luffFoot;
    private readonly HullPoint luffHead;
    private readonly HullPoint leechHead;
    private readonly HullPoint leechFoot;
    private readonly HullPoint pivot;
    private readonly double camber;
    private readonly (double Along, double Across) normal;
    private readonly (double Reach, double Span) centroid;

    public SailForm(HullPoint luffFoot, HullPoint luffHead, HullPoint leechHead, HullPoint leechFoot, HullPoint pivot, double camber)
    {
        (this.luffFoot, this.luffHead, this.leechHead, this.leechFoot, this.pivot, this.camber) = (luffFoot, luffHead, leechHead, leechFoot, pivot, camber);
        var chord = (leechFoot + leechHead) - (luffFoot + luffHead);
        RestChord = Math.Atan2(Math.Abs(chord.Across), Math.Abs(chord.Along));
        var length = Math.Sqrt(chord.Along * chord.Along + chord.Across * chord.Across);
        normal = (-chord.Across / length, chord.Along / length);
        centroid = Centroid();
    }

    public double RestChord { get; }

    public static readonly (double Reach, double Span) Whole = (1, 1);

    public HullPoint At(double reach, double span, double turn, double depth) => At(reach, span, turn, depth, Whole);

    public HullPoint At(double reach, double span, double turn, double depth, (double Reach, double Span) set)
    {
        reach *= set.Reach;
        var foot = (1 - reach) * luffFoot + reach * leechFoot;
        var head = (1 - reach) * luffHead + reach * leechHead;
        var flat = foot + (1 - set.Span + span * set.Span) * (head - foot);
        var belly = depth * 4 * camber * set.Span * (head - foot).Length * span * (1 - span);
        var (along, across) = (flat.Along - pivot.Along + belly * normal.Along, flat.Across - pivot.Across + belly * normal.Across);
        var (cos, sin) = (Math.Cos(turn), Math.Sin(turn));
        return new HullPoint(pivot.Along + cos * along - sin * across, flat.Height, pivot.Across + sin * along + cos * across);
    }

    public (double Along, double Across) Normal(double turn)
    {
        var (cos, sin) = (Math.Cos(turn), Math.Sin(turn));
        return (cos * normal.Along - sin * normal.Across, sin * normal.Along + cos * normal.Across);
    }

    public HullPoint Centre((double Reach, double Span) set, double turn, double depth) => At(centroid.Reach, centroid.Span, turn, depth, set);

    private (double Reach, double Span) Centroid()
    {
        double area = 0, reach = 0, span = 0;
        for (var i = 0; i < CentroidCells; i++)
        for (var j = 0; j < CentroidCells; j++)
        {
            var (s, t) = ((i + 0.5) / CentroidCells, (j + 0.5) / CentroidCells);
            var (a, b) = (At(s + 0.5 / CentroidCells, t, 0, 0) - At(s - 0.5 / CentroidCells, t, 0, 0), At(s, t + 0.5 / CentroidCells, 0, 0) - At(s, t - 0.5 / CentroidCells, 0, 0));
            var cell = new HullPoint(a.Height * b.Across - a.Across * b.Height, a.Across * b.Along - a.Along * b.Across, a.Along * b.Height - a.Height * b.Along).Length;
            (area, reach, span) = (area + cell, reach + cell * s, span + cell * t);
        }
        return (reach / area, span / area);
    }
}
