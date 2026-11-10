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
    private static readonly (int X, int Z)[] Sides = [(1, 0), (-1, 0), (0, 1), (0, -1)];

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
        if (deepNumber > 0)
        {
            var along = anchor == null ? AlongEdges(size, number, numberX, numberZ, spacing) : null;
            Seed(size, depth, phase, numberX, numberZ, deepNumber, (x, z) => Plain(x, z) + (anchor?.Invoke(x, z) ?? along![z * size + x]));
        }

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

    /// <summary>For each cell the index of the nearest water, itself over water; -1 where a grid holds none.</summary>
    public static int[] NearestWater(float[] depth, int size)
    {
        var nearest = new int[depth.Length];
        Array.Fill(nearest, -1);
        var waiting = new Queue<int>();
        for (var i = 0; i < depth.Length; i++)
        {
            if (!(depth[i] > 0)) continue;
            nearest[i] = i;
            waiting.Enqueue(i);
        }
        while (waiting.TryDequeue(out var i))
        {
            var (x, z) = (i % size, i / size);
            foreach (var (dx, dz) in Sides)
            {
                var (nextX, nextZ) = (x + dx, z + dz);
                if (nextX < 0 || nextZ < 0 || nextX >= size || nextZ >= size || nearest[nextZ * size + nextX] >= 0) continue;
                nearest[nextZ * size + nextX] = nearest[i];
                waiting.Enqueue(nextZ * size + nextX);
            }
        }
        return nearest;
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

    private static void Seed(int size, float[] depth, double[] phase, double numberX, double numberZ, double deepNumber, Func<int, int, double> entering)
    {
        var (upwindX, upwindZ) = (numberX > 0 ? 0 : size - 1, numberZ > 0 ? 0 : size - 1);
        for (var i = 0; i < phase.Length; i++)
        {
            if (!(depth[i] > 0)) continue;
            var (x, z) = (i % size, i / size);
            var (backX, backZ) = ((int)Math.Round(x - numberX / deepNumber), (int)Math.Round(z - numberZ / deepNumber));
            var offLand = backX >= 0 && backZ >= 0 && backX < size && backZ < size && !(depth[backZ * size + backX] > 0);
            if ((numberX != 0 && x == upwindX) || (numberZ != 0 && z == upwindZ) || offLand) phase[i] = entering(x, z);
        }
    }

    /// <summary>The lag along the grid's upwind edges, each marched from its own upwind end: a wave running along an edge falls behind over the depths on it, in the share of its path that lies along that edge.</summary>
    private static double[] AlongEdges(int size, double[] number, double numberX, double numberZ, double spacing)
    {
        var lag = new double[size * size];
        var deepNumber = Math.Sqrt(numberX * numberX + numberZ * numberZ);
        void March(Func<int, int> index, double along)
        {
            for (var step = 1; step < size; step++)
            {
                var (here, before) = (index(step), index(step - 1));
                if (double.IsNaN(number[here]) || double.IsNaN(number[before])) continue;
                lag[here] = lag[before] + Math.Abs(along) / deepNumber * (number[here] - deepNumber * spacing);
            }
        }
        var (upwindX, upwindZ) = (numberX > 0 ? 0 : size - 1, numberZ > 0 ? 0 : size - 1);
        if (numberZ != 0) March(step => upwindZ * size + (numberX >= 0 ? step : size - 1 - step), numberX);
        if (numberX != 0) March(step => (numberZ >= 0 ? step : size - 1 - step) * size + upwindX, numberZ);
        return lag;
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
