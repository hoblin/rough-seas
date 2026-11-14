using System;
using Vintagestory.API.MathTools;

namespace RoughSeas.Lines;

public sealed class Chain
{
    private const int Passes = 4;
    private const double CylinderDrag = 1.2;

    private readonly Vec3d[] at;
    private readonly Vec3d[] moving;
    private readonly double kilogramsPerMetre;
    private readonly double dragPerMetre;
    private readonly double stretchingNewtons;
    private double rest;

    public Chain(Vec3d[] points, double restMetres, double diameterMetres, Fibre fibre, double airKilogramsPerCubicMetre)
    {
        at = points;
        moving = Array.ConvertAll(points, _ => new Vec3d());
        kilogramsPerMetre = fibre.KilogramsPerCubicMetre * Math.PI * diameterMetres * diameterMetres / 4;
        dragPerMetre = airKilogramsPerCubicMetre * CylinderDrag * diameterMetres / 2;
        stretchingNewtons = fibre.BreakingAt(diameterMetres) / fibre.StretchAtBreak;
        Rest(restMetres);
    }

    public Vec3d[] Points => at;

    public Vec3d[] Moving => moving;

    public double RestMetres => rest * (at.Length - 1);

    public void Rest(double metres) => rest = metres / (at.Length - 1);

    public void Carry(Vec3d by)
    {
        foreach (var point in at) point.Add(by);
    }

    public void Step(Vec3d first, Vec3d last, double gravity, Vec3d air, double seconds)
    {
        if (seconds <= 0) return;

        var count = at.Length;
        var slowed = dragPerMetre / kilogramsPerMetre;
        var next = new Vec3d[count];
        next[0] = first.Clone();
        next[^1] = last.Clone();
        for (var k = 1; k < count - 1; k++)
        {
            moving[k].Y -= gravity * seconds;
            var through = moving[k].SubCopy(air);
            through.Mul(1 / (1 + slowed * through.Length() * seconds));
            moving[k] = through.Add(air);
            next[k] = at[k].AddCopy(moving[k].X * seconds, moving[k].Y * seconds, moving[k].Z * seconds);
        }

        var pulled = new double[count - 1];
        var yielding = rest / stretchingNewtons / (seconds * seconds);
        for (var pass = 0; pass < Passes; pass++) Drawn(next, pulled, yielding);

        for (var k = 0; k < count; k++)
        {
            moving[k] = next[k].SubCopy(at[k]).Mul(1 / seconds);
            at[k].Set(next[k]);
        }
    }

    private void Drawn(Vec3d[] points, double[] pulled, double yielding)
    {
        var lengths = points.Length - 1;
        var light = 1 / (kilogramsPerMetre * rest);
        double Free(int point) => point == 0 || point == points.Length - 1 ? 0 : light;
        var along = new Vec3d[lengths];
        var (below, across, above, right) = (new double[lengths], new double[lengths], new double[lengths], new double[lengths]);
        var taut = new bool[lengths];
        for (var k = 0; k < lengths; k++)
        {
            along[k] = points[k + 1].SubCopy(points[k]);
            var length = along[k].Length();
            taut[k] = length > rest && Free(k) + Free(k + 1) > 0;
            if (length > 0) along[k].Mul(1 / length);
            right[k] = taut[k] ? length - rest - yielding * pulled[k] : 0;
        }
        for (var k = 0; k < lengths; k++)
        {
            across[k] = taut[k] ? Free(k) + Free(k + 1) + yielding : 1;
            below[k] = taut[k] && k > 0 && taut[k - 1] ? -Free(k) * along[k - 1].Dot(along[k]) : 0;
            above[k] = taut[k] && k + 1 < lengths && taut[k + 1] ? -Free(k + 1) * along[k].Dot(along[k + 1]) : 0;
        }

        var more = Tridiagonal(below, across, above, right);
        for (var k = 0; k < lengths; k++)
        {
            if (!taut[k]) continue;

            var pull = Math.Max(-pulled[k], more[k]);
            pulled[k] += pull;
            points[k].Add(along[k].X * pull * Free(k), along[k].Y * pull * Free(k), along[k].Z * pull * Free(k));
            points[k + 1].Add(-along[k].X * pull * Free(k + 1), -along[k].Y * pull * Free(k + 1), -along[k].Z * pull * Free(k + 1));
        }
    }

    private static double[] Tridiagonal(double[] below, double[] across, double[] above, double[] right)
    {
        var size = across.Length;
        var (upper, solved) = (new double[size], new double[size]);
        for (var k = 0; k < size; k++)
        {
            var pivot = across[k] - (k > 0 ? below[k] * upper[k - 1] : 0);
            upper[k] = above[k] / pivot;
            solved[k] = (right[k] - (k > 0 ? below[k] * solved[k - 1] : 0)) / pivot;
        }
        for (var k = size - 2; k >= 0; k--) solved[k] -= upper[k] * solved[k + 1];
        return solved;
    }
}
