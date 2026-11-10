using System;

namespace RoughSeas.Surf;

/// <summary>
/// How far shoaling water holds a wave back: the eikonal |∇S| = k(depth) solved by fast sweeping from the plane wave
/// where the water is deep, less the plane wave's phase: anchored to deep water alone, so maps read around different
/// points agree wherever they share it. Water no deep water reaches within the map keeps no lag. Its gradient turns the crests
/// toward the shore by Snell's law. Land holds no phase.
/// </summary>
public static class PhaseField
{
    private const int Rounds = 2;

    public static float[] Lag(int size, float[] depth, double numberX, double numberZ, int rounds = Rounds)
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
            number[i] = Shoaling.Wavenumber(deepNumber, depth[i]);
            phase[i] = depth[i] >= deep ? numberX * (x + 0.5) + numberZ * (z + 0.5) : double.PositiveInfinity;
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
            var plain = numberX * (i % size + 0.5) + numberZ * (i / size + 0.5);
            lag[i] = double.IsFinite(phase[i]) ? (float)(phase[i] - plain) : 0;
        }
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
