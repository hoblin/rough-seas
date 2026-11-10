using System;
using System.Collections.Generic;
using System.Linq;

namespace RoughSeas.Water;

/// <summary>
/// How far shoaling water holds a wave back: the eikonal |∇S| = k(depth) solved by fast sweeping, less the plane wave's phase. The wave starts where it enters the water of a grid: at
/// the grid's edge, or from land upwind. There it takes the lag of <c>anchor</c>, the coarser solution over a wider grid that all readers of a place share (<see cref="CoarseBed"/>),
/// so grids read around different points agree. The gradient turns the crests by Snell's law. Land holds no phase.
/// </summary>
public static class PhaseField
{
    private const int Rounds = 2;

    /// <param name="anchor">The lag where the wave enters this grid, by column and row; none is 0.</param>
    /// <param name="spacing">How many blocks a cell is across.</param>
    public static float[] Lag(int size, float[] depth, double numberX, double numberZ, Func<int, int, double>? anchor = null, double spacing = 1, int rounds = Rounds)
    {
        var deepNumber = Math.Sqrt(numberX * numberX + numberZ * numberZ);
        var phase = new double[size * size];
        var number = new double[size * size];
        Array.Fill(phase, double.PositiveInfinity);
        for (var i = 0; i < phase.Length; i++) number[i] = depth[i] > 0 ? WaveTheory.Wavenumber(deepNumber, depth[i]) * spacing : double.NaN;

        double Plain(int x, int z) => (numberX * (x + 0.5) + numberZ * (z + 0.5)) * spacing;
        if (deepNumber > 0) Seed(size, depth, phase, numberX / deepNumber, numberZ / deepNumber, (x, z) => Plain(x, z) + (anchor?.Invoke(x, z) ?? 0));

        for (var round = 0; round < rounds; round++)
        {
            Sweep(size, phase, number, 0, size, 1, 0, size, 1);
            Sweep(size, phase, number, size - 1, -1, -1, 0, size, 1);
            Sweep(size, phase, number, 0, size, 1, size - 1, -1, -1);
            Sweep(size, phase, number, size - 1, -1, -1, size - 1, -1, -1);
        }

        var lag = new float[size * size];
        for (var i = 0; i < lag.Length; i++)
        {
            var (x, z) = (i % size, i / size);
            lag[i] = double.IsFinite(phase[i]) ? (float)(phase[i] - Plain(x, z)) : 0;
        }
        return lag;
    }

    /// <summary>The lag's slope at a cell along one axis, over the water alone.</summary>
    public static double Slope(float[] lag, float[] depth, int size, int x, int z, int dx, int dz)
    {
        var back = (x - dx, z - dz);
        var ahead = (x + dx, z + dz);
        if (back.Item1 < 0 || back.Item2 < 0 || !(depth[back.Item2 * size + back.Item1] > 0)) back = (x, z);
        if (ahead.Item1 >= size || ahead.Item2 >= size || !(depth[ahead.Item2 * size + ahead.Item1] > 0)) ahead = (x, z);
        var span = ahead.Item1 - back.Item1 + ahead.Item2 - back.Item2;
        return span > 0 ? (lag[ahead.Item2 * size + ahead.Item1] - lag[back.Item2 * size + back.Item1]) / span : 0;
    }

    private static void Seed(int size, float[] depth, double[] phase, double towardX, double towardZ, Func<int, int, double> entering)
    {
        for (var i = 0; i < phase.Length; i++)
        {
            if (!(depth[i] > 0)) continue;
            var (x, z) = (i % size, i / size);
            var (backX, backZ) = ((int)Math.Round(x - towardX), (int)Math.Round(z - towardZ));
            if (backX < 0 || backZ < 0 || backX >= size || backZ >= size || !(depth[backZ * size + backX] > 0)) phase[i] = entering(x, z);
        }
    }

    private static void Sweep(int size, double[] phase, double[] number, int fromX, int toX, int stepX, int fromZ, int toZ, int stepZ)
    {
        for (var z = fromZ; z != toZ; z += stepZ)
        for (var x = fromX; x != toX; x += stepX)
        {
            var i = z * size + x;
            if (double.IsNaN(number[i])) continue;
            var a = Math.Min(x > 0 ? phase[i - 1] : double.PositiveInfinity, x < size - 1 ? phase[i + 1] : double.PositiveInfinity);
            var b = Math.Min(z > 0 ? phase[i - size] : double.PositiveInfinity, z < size - 1 ? phase[i + size] : double.PositiveInfinity);
            if (double.IsPositiveInfinity(a) && double.IsPositiveInfinity(b)) continue;
            var f = number[i];
            var reached = Math.Abs(a - b) >= f ? Math.Min(a, b) + f : (a + b + Math.Sqrt(2 * f * f - (a - b) * (a - b))) / 2;
            if (reached < phase[i]) phase[i] = reached;
        }
    }
}
