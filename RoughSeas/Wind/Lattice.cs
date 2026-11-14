using System;
using System.Collections.Generic;
using System.Linq;

namespace RoughSeas.Wind;

public readonly record struct Lattice(double Potential, double Vortex, double Aspect, double LeadingSweep)
{
    private const int Strips = 20;
    private const int Rows = 6;
    private const double Wake = 1000;

    private const double ShapeResolution = 100;
    private const int MostShapes = 20000;

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Lattice> Shapes = new();

    private readonly record struct Horseshoe(double Ax, double Ay, double Bx, double By, double TrailA, double TrailB);

    public static Lattice Seen(IReadOnlyList<(double X, double Y)> outline)
    {
        var (left, low) = (outline.Min(p => p.X), outline.Min(p => p.Y));
        var span = outline.Max(p => p.Y) - low;
        if (span <= 0) return default;

        var shape = outline.Select(p => ((int)Math.Round((p.X - left) / span * ShapeResolution), (int)Math.Round((p.Y - low) / span * ShapeResolution))).ToArray();
        if (Shapes.Count > MostShapes) Shapes.Clear();
        return Shapes.GetOrAdd(string.Join(";", shape), _ => Of(shape.Select(p => (p.Item1 / ShapeResolution, p.Item2 / ShapeResolution)).ToArray()));
    }

    public static Lattice Of(IReadOnlyList<(double X, double Y)> outline)
    {
        var (low, high) = (outline.Min(p => p.Y), outline.Max(p => p.Y));
        var span = high - low;
        var area = Math.Abs(Enumerable.Range(0, outline.Count).Sum(k => outline[k].X * outline[(k + 1) % outline.Count].Y - outline[(k + 1) % outline.Count].X * outline[k].Y)) / 2;
        if (span <= 0 || area <= 0) return default;

        var shoes = new List<Horseshoe>();
        var controls = new List<(double X, double Y)>();
        var strips = new List<(int First, double Width)>();
        for (var j = 0; j < Strips; j++)
        {
            var (y0, y1) = (low + span * j / Strips, low + span * (j + 1) / Strips);
            var (front0, back0) = Chord(outline, y0);
            var (front1, back1) = Chord(outline, y1);
            var (frontMid, backMid) = Chord(outline, (y0 + y1) / 2);
            strips.Add((shoes.Count, y1 - y0));
            for (var i = 0; i < Rows; i++)
            {
                var bound = (i + 0.25) / Rows;
                shoes.Add(new Horseshoe(front0 + bound * (back0 - front0), y0, front1 + bound * (back1 - front1), y1, back0, back1));
                var control = (i + 0.75) / Rows;
                controls.Add((frontMid + control * (backMid - frontMid), (y0 + y1) / 2));
            }
        }

        var count = shoes.Count;
        var influence = new double[count, count];
        for (var c = 0; c < count; c++)
        for (var k = 0; k < count; k++) influence[c, k] = Downwash(shoes[k], controls[c].X, controls[c].Y);
        var circulation = Solved(influence, Enumerable.Repeat(-1.0, count).ToArray());

        var lift = Enumerable.Range(0, count).Sum(k => circulation[k] * (shoes[k].By - shoes[k].Ay));
        double Upwash(double x, double y, int skip) =>
            1 + Enumerable.Range(0, count).Where(k => k != skip).Sum(k => circulation[k] * Downwash(shoes[k], x, y));

        var held = strips.Select(strip => Enumerable.Range(strip.First, Rows).Sum(k => circulation[k])).ToArray();
        var edges = Enumerable.Range(0, Strips + 1).Select(e => (Y: low + span * e / Strips, Shed: (e > 0 ? held[e - 1] : 0) - (e < Strips ? held[e] : 0))).ToArray();
        var drag = Enumerable.Range(0, Strips).Sum(s =>
        {
            var y = low + span * (s + 0.5) / Strips;
            var down = edges.Sum(edge => edge.Shed / (2 * Math.PI * (y - edge.Y)));
            return held[s] * down * strips[s].Width;
        });
        var potential = 2 * lift / area;
        var induced = -drag / area / (potential * potential);
        var thrust = potential - potential * potential * induced;
        double Side(int strip, bool atA)
        {
            var side = 0.0;
            for (var i = 0; i < Rows; i++)
            {
                var k = strips[strip].First + i;
                var (x0, y, x1) = atA ? (shoes[k].Ax, shoes[k].Ay, shoes[k].TrailA) : (shoes[k].Bx, shoes[k].By, shoes[k].TrailB);
                var flow = Upwash((x0 + x1) / 2, y, -1) * circulation[k] * (atA ? -1 : 1);
                side += flow * (x1 - x0);
            }
            return Math.Abs(side);
        }
        var vortex = thrust * Secant(outline) + 2 * (Side(0, true) + Side(Strips - 1, false)) / area;
        return new Lattice(potential, vortex, span * span / area, SweepOf(outline));
    }

    private static double Secant(IReadOnlyList<(double X, double Y)> outline)
    {
        var turning = Math.Sign(Enumerable.Range(0, outline.Count).Sum(k => outline[k].X * outline[(k + 1) % outline.Count].Y - outline[(k + 1) % outline.Count].X * outline[k].Y));
        var (spanned, weighted) = (0.0, 0.0);
        for (var k = 0; k < outline.Count; k++)
        {
            var (a, b) = (outline[k], outline[(k + 1) % outline.Count]);
            var (dx, dy) = (b.X - a.X, b.Y - a.Y);
            if (turning * dy >= 0 || Math.Abs(dy) < 1e-12) continue;

            spanned += Math.Abs(dy);
            weighted += Math.Sqrt(dx * dx + dy * dy);
        }
        return spanned > 0 ? weighted / spanned : 1;
    }

    private static (double Front, double Back) Chord(IReadOnlyList<(double X, double Y)> outline, double y)
    {
        var (front, back) = (double.MaxValue, double.MinValue);
        for (var k = 0; k < outline.Count; k++)
        {
            var (a, b) = (outline[k], outline[(k + 1) % outline.Count]);
            if (y < Math.Min(a.Y, b.Y) - 1e-12 || y > Math.Max(a.Y, b.Y) + 1e-12) continue;

            var xs = Math.Abs(b.Y - a.Y) < 1e-12 ? new[] { a.X, b.X } : [a.X + (y - a.Y) / (b.Y - a.Y) * (b.X - a.X)];
            foreach (var x in xs) (front, back) = (Math.Min(front, x), Math.Max(back, x));
        }
        return front <= back ? (front, back) : (0, 0);
    }

    private static double SweepOf(IReadOnlyList<(double X, double Y)> outline)
    {
        var edges = Enumerable.Range(0, outline.Count).Select(k => (A: outline[k], B: outline[(k + 1) % outline.Count]))
            .Where(edge => Math.Abs(edge.B.Y - edge.A.Y) > 1e-9).ToArray();
        var lead = edges.MinBy(edge => (edge.A.X + edge.B.X) / 2);
        return Math.Atan2(Math.Abs(lead.B.X - lead.A.X), Math.Abs(lead.B.Y - lead.A.Y));
    }

    private static double Downwash(Horseshoe shoe, double x, double y) =>
        Segment(shoe.Ax + Wake, shoe.Ay, shoe.Ax, shoe.Ay, x, y) + Segment(shoe.Ax, shoe.Ay, shoe.Bx, shoe.By, x, y)
        + Segment(shoe.Bx, shoe.By, shoe.Bx + Wake, shoe.By, x, y);

    private static double Segment(double ax, double ay, double bx, double by, double x, double y)
    {
        var (r1x, r1y, r2x, r2y) = (x - ax, y - ay, x - bx, y - by);
        var across = r1x * r2y - r1y * r2x;
        if (Math.Abs(across) < 1e-10) return 0;

        var (r0x, r0y) = (bx - ax, by - ay);
        var (l1, l2) = (Math.Sqrt(r1x * r1x + r1y * r1y), Math.Sqrt(r2x * r2x + r2y * r2y));
        return (r0x * (r1x / l1 - r2x / l2) + r0y * (r1y / l1 - r2y / l2)) / (4 * Math.PI * across);
    }

    private static double[] Solved(double[,] matrix, double[] right)
    {
        var size = right.Length;
        var (a, b) = ((double[,])matrix.Clone(), (double[])right.Clone());
        for (var col = 0; col < size; col++)
        {
            var pivot = Enumerable.Range(col, size - col).MaxBy(row => Math.Abs(a[row, col]));
            if (pivot != col)
            {
                for (var k = 0; k < size; k++) (a[col, k], a[pivot, k]) = (a[pivot, k], a[col, k]);
                (b[col], b[pivot]) = (b[pivot], b[col]);
            }
            for (var row = col + 1; row < size; row++)
            {
                var factor = a[row, col] / a[col, col];
                for (var k = col; k < size; k++) a[row, k] -= factor * a[col, k];
                b[row] -= factor * b[col];
            }
        }
        var x = new double[size];
        for (var row = size - 1; row >= 0; row--)
        {
            var sum = b[row];
            for (var k = row + 1; k < size; k++) sum -= a[row, k] * x[k];
            x[row] = sum / a[row, row];
        }
        return x;
    }
}
