using System;
using Vintagestory.API.MathTools;

namespace RoughSeas.Lines;

public sealed class Chain
{
    private const int Passes = 4;
    private const double CylinderDrag = 1.2;

    private readonly Vec3d[] at;
    private readonly Vec3d[] moving;
    private readonly Vec3d[] next;
    private readonly Vec3d[] along;
    private readonly double[] pulled;
    private readonly double[] below;
    private readonly double[] across;
    private readonly double[] above;
    private readonly double[] right;
    private readonly double[] upper;
    private readonly double[] solved;
    private readonly bool[] taut;
    private readonly double kilogramsPerMetre;
    private readonly double dragPerMetre;
    private readonly double stretchingNewtons;
    private double rest;

    public Chain(Vec3d[] points, double restMetres, double diameterMetres, Fibre fibre, double airKilogramsPerCubicMetre)
    {
        at = points;
        moving = Array.ConvertAll(points, _ => new Vec3d());
        next = Array.ConvertAll(points, _ => new Vec3d());
        var lengths = points.Length - 1;
        along = new Vec3d[lengths];
        for (var k = 0; k < lengths; k++) along[k] = new Vec3d();
        (pulled, below, across, above, right, upper, solved, taut) =
            (new double[lengths], new double[lengths], new double[lengths], new double[lengths], new double[lengths], new double[lengths], new double[lengths], new bool[lengths]);
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
        next[0].Set(first);
        next[^1].Set(last);
        for (var k = 1; k < count - 1; k++)
        {
            var point = moving[k];
            point.Y -= gravity * seconds;
            var (x, y, z) = (point.X - air.X, point.Y - air.Y, point.Z - air.Z);
            var kept = 1 / (1 + slowed * Math.Sqrt(x * x + y * y + z * z) * seconds);
            point.Set(air.X + x * kept, air.Y + y * kept, air.Z + z * kept);
            next[k].Set(at[k].X + point.X * seconds, at[k].Y + point.Y * seconds, at[k].Z + point.Z * seconds);
        }

        Array.Clear(pulled);
        var yielding = rest / stretchingNewtons / (seconds * seconds);
        for (var pass = 0; pass < Passes; pass++) Drawn(yielding);

        for (var k = 0; k < count; k++)
        {
            moving[k].Set((next[k].X - at[k].X) / seconds, (next[k].Y - at[k].Y) / seconds, (next[k].Z - at[k].Z) / seconds);
            at[k].Set(next[k]);
        }
    }

    private void Drawn(double yielding)
    {
        var lengths = next.Length - 1;
        var light = 1 / (kilogramsPerMetre * rest);
        double Free(int point) => point == 0 || point == next.Length - 1 ? 0 : light;
        for (var k = 0; k < lengths; k++)
        {
            along[k].Set(next[k + 1].X - next[k].X, next[k + 1].Y - next[k].Y, next[k + 1].Z - next[k].Z);
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

        Tridiagonal();
        for (var k = 0; k < lengths; k++)
        {
            if (!taut[k]) continue;

            var pull = Math.Max(-pulled[k], solved[k]);
            pulled[k] += pull;
            var (x, y, z) = (along[k].X * pull, along[k].Y * pull, along[k].Z * pull);
            next[k].Add(x * Free(k), y * Free(k), z * Free(k));
            next[k + 1].Add(-x * Free(k + 1), -y * Free(k + 1), -z * Free(k + 1));
        }
    }

    private void Tridiagonal()
    {
        var size = across.Length;
        for (var k = 0; k < size; k++)
        {
            var pivot = across[k] - (k > 0 ? below[k] * upper[k - 1] : 0);
            upper[k] = above[k] / pivot;
            solved[k] = (right[k] - (k > 0 ? below[k] * solved[k - 1] : 0)) / pivot;
        }
        for (var k = size - 2; k >= 0; k--) solved[k] -= upper[k] * solved[k + 1];
    }
}
