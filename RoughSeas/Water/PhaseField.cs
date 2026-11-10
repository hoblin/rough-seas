using System;
using System.Collections.Generic;
using System.Linq;

namespace RoughSeas.Water;

/// <summary>
/// How far shoaling water holds a wave back: the eikonal |∇S| = k(depth) solved by fast sweeping, less the plane wave's phase. It is anchored to what the bed itself holds, so
/// maps read around different points agree wherever they share it: to deep water open to the sea upwind or, on a map with none, to the shoreline, whose tangent keeps
/// the wave's along-shore number as Snell's law does. A map with neither starts from its upwind edge, where the lag fades out anyway. Waves running out to sea take the same solution run
/// the other way: minus the arrival of the wave reversed. The gradient turns the crests. Land holds no phase.
/// </summary>
public static class PhaseField
{
    private const int Rounds = 2;
    private static readonly (int X, int Z)[] Sides = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    public static float[] Lag(int size, float[] depth, double numberX, double numberZ, int rounds = Rounds)
    {
        var arriving = Arrival(size, depth, numberX, numberZ, rounds);
        var leaving = Arrival(size, depth, -numberX, -numberZ, rounds);
        var known = arriving.Select(value => float.IsNaN(value) ? 0 : value).ToArray();
        var lag = new float[size * size];
        for (var i = 0; i < lag.Length; i++)
        {
            var (x, z) = (i % size, i / size);
            if (float.IsNaN(arriving[i]))
            {
                lag[i] = float.IsNaN(leaving[i]) ? 0 : -leaving[i];
                continue;
            }
            if (float.IsNaN(leaving[i]))
            {
                lag[i] = arriving[i];
                continue;
            }
            var along = numberX * Slope(known, depth, size, x, z, 1, 0) + numberZ * Slope(known, depth, size, x, z, 0, 1);
            lag[i] = along + numberX * numberX + numberZ * numberZ >= 0 ? arriving[i] : -leaving[i];
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

    private static float[] Arrival(int size, float[] depth, double numberX, double numberZ, int rounds)
    {
        var deepNumber = Math.Sqrt(numberX * numberX + numberZ * numberZ);
        var phase = new double[size * size];
        var number = new double[size * size];
        Array.Fill(phase, double.PositiveInfinity);
        for (var i = 0; i < phase.Length; i++) number[i] = depth[i] > 0 ? WaveTheory.Wavenumber(deepNumber, depth[i]) : double.NaN;

        SeedOpenDeepWater(size, depth, phase, Math.PI / deepNumber, numberX, numberZ);
        var bodies = WaterBodies(size, depth, out var count);
        SeedShoreline(size, depth, phase, numberX, numberZ, Unanchored(phase, bodies, count), bodies);
        SeedUpwindEdge(size, phase, number, numberX, numberZ, Unanchored(phase, bodies, count), bodies);

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
            var plain = numberX * (i % size + 0.5) + numberZ * (i / size + 0.5);
            lag[i] = double.IsFinite(phase[i]) ? (float)(phase[i] - plain) : float.NaN;
        }
        return lag;
    }

    private static void SeedOpenDeepWater(int size, float[] depth, double[] phase, double deep, double numberX, double numberZ)
    {
        var open = new bool[size * size];
        var (stepX, stepZ) = (numberX >= 0 ? 1 : -1, numberZ >= 0 ? 1 : -1);
        for (var rowIndex = 0; rowIndex < size; rowIndex++)
        for (var columnIndex = 0; columnIndex < size; columnIndex++)
        {
            var z = stepZ > 0 ? rowIndex : size - 1 - rowIndex;
            var x = stepX > 0 ? columnIndex : size - 1 - columnIndex;
            var i = z * size + x;
            if (!(depth[i] >= deep)) continue;
            var (backX, backZ) = (x - stepX, z - stepZ);
            var openAlongX = numberX != 0 && (backX < 0 || backX >= size || open[z * size + backX]);
            var openAlongZ = numberZ != 0 && (backZ < 0 || backZ >= size || open[backZ * size + x]);
            open[i] = openAlongX || openAlongZ;
            if (open[i]) phase[i] = numberX * (x + 0.5) + numberZ * (z + 0.5);
        }
    }

    private static int[] WaterBodies(int size, float[] depth, out int count)
    {
        var bodies = new int[size * size];
        Array.Fill(bodies, -1);
        count = 0;
        var waiting = new Stack<int>();
        for (var start = 0; start < bodies.Length; start++)
        {
            if (!(depth[start] > 0) || bodies[start] >= 0) continue;
            bodies[start] = count;
            waiting.Push(start);
            while (waiting.TryPop(out var i))
            {
                foreach (var (dx, dz) in Sides)
                {
                    var (x, z) = (i % size + dx, i / size + dz);
                    if (x < 0 || z < 0 || x >= size || z >= size || !(depth[z * size + x] > 0) || bodies[z * size + x] >= 0) continue;
                    bodies[z * size + x] = count;
                    waiting.Push(z * size + x);
                }
            }
            count++;
        }
        return bodies;
    }

    private static bool[] Unanchored(double[] phase, int[] bodies, int count)
    {
        var unanchored = Enumerable.Repeat(true, count).ToArray();
        for (var i = 0; i < phase.Length; i++)
        {
            if (bodies[i] >= 0 && double.IsFinite(phase[i])) unanchored[bodies[i]] = false;
        }
        return unanchored;
    }

    private static void SeedShoreline(int size, float[] depth, double[] phase, double numberX, double numberZ, bool[] unanchored, int[] bodies)
    {
        for (var i = 0; i < phase.Length; i++)
        {
            if (bodies[i] < 0 || !unanchored[bodies[i]]) continue;
            var (x, z) = (i % size, i / size);
            var shore = Sides.Any(side => x + side.X >= 0 && z + side.Z >= 0 && x + side.X < size && z + side.Z < size && !(depth[(z + side.Z) * size + x + side.X] > 0));
            if (shore) phase[i] = numberX * (x + 0.5) + numberZ * (z + 0.5);
        }
    }

    private static void SeedUpwindEdge(int size, double[] phase, double[] number, double numberX, double numberZ, bool[] unanchored, int[] bodies)
    {
        for (var i = 0; i < phase.Length; i++)
        {
            if (bodies[i] < 0 || !unanchored[bodies[i]]) continue;
            var (x, z) = (i % size, i / size);
            var upwind = (x == 0 && numberX > 0) || (x == size - 1 && numberX < 0) || (z == 0 && numberZ > 0) || (z == size - 1 && numberZ < 0);
            if (upwind && !double.IsNaN(number[i])) phase[i] = numberX * (x + 0.5) + numberZ * (z + 0.5);
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
