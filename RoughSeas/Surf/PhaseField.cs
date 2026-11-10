using System;
using System.Linq;
using RoughSeas.Water;

namespace RoughSeas.Surf;

/// <summary>
/// How far shoaling water holds a wave back: the eikonal |∇S| = k(depth) solved by fast sweeping from the plane wave
/// where the water is deep, less the plane wave's phase: anchored to deep water alone, so maps read around different
/// points agree wherever they share it. A map with no water deep enough for this wave starts it from its upwind edge, where the lag fades out anyway. Waves running out to sea take the same
/// solution run the other way: minus the arrival of the wave reversed. The gradient turns the crests by Snell's law. Land holds no phase.
/// </summary>
public static class PhaseField
{
    private const int Rounds = 2;

    public static float[] Lag(int size, float[] depth, double numberX, double numberZ, int rounds = Rounds)
    {
        var arriving = Arrival(size, depth, numberX, numberZ, rounds);
        var leaving = Arrival(size, depth, -numberX, -numberZ, rounds);
        var lag = new float[size * size];
        for (var i = 0; i < lag.Length; i++)
        {
            var (x, z) = (i % size, i / size);
            var along = numberX * Gradient(arriving, depth, size, x, z, 1, 0) + numberZ * Gradient(arriving, depth, size, x, z, 0, 1);
            lag[i] = along + numberX * numberX + numberZ * numberZ >= 0 ? arriving[i] : -leaving[i];
        }
        return lag;
    }

    private static double Gradient(float[] lag, float[] depth, int size, int x, int z, int dx, int dz)
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
        var deep = Math.PI / deepNumber;
        for (var i = 0; i < phase.Length; i++)
        {
            var (x, z) = (i % size, i / size);
            if (!(depth[i] > 0))
            {
                phase[i] = double.PositiveInfinity;
                number[i] = double.NaN;
                continue;
            }
            number[i] = WaveTheory.Wavenumber(deepNumber, depth[i]);
            phase[i] = depth[i] >= deep ? numberX * (x + 0.5) + numberZ * (z + 0.5) : double.PositiveInfinity;
        }
        if (!phase.Any(double.IsFinite)) SeedUpwindEdge(size, phase, number, numberX, numberZ);

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
            lag[i] = double.IsFinite(phase[i]) ? (float)(phase[i] - plain) : 0;
        }
        return lag;
    }

    private static void SeedUpwindEdge(int size, double[] phase, double[] number, double numberX, double numberZ)
    {
        for (var i = 0; i < phase.Length; i++)
        {
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
