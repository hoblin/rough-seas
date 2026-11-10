using System;
using System.Linq;
using RoughSeas.Surf;

namespace RoughSeas.Water;

/// <param name="Felt">The depth under still water that the longest wave feels, cell by cell.</param>
/// <param name="Lags">For each slot, the phase the bed holds the wave back by.</param>
/// <param name="Gains">For each slot, its height over its height in deep water.</param>
/// <param name="Breaking">The share of waves breaking.</param>
/// <param name="Gain">The whole sea's height over its height in deep water.</param>
/// <param name="SlopeX">The depth's slope along X, at the scale the longest wave feels it.</param>
/// <param name="SlopeZ">The same along Z.</param>
/// <param name="CelerityDepth">The depth that sets the waves' speed: the water under a wave and, where it has broken, the bore's own height over it, never less than a terminal bore's. Over land, the terminal bore's.</param>
public sealed record TransformedWaves(float[] Felt, float[][] Lags, float[][] Gains, float[] Breaking, float[] Gain, float[] SlopeX, float[] SlopeZ, float[] CelerityDepth)
{
    /// <summary>The sea's height, against the deep sea's, where it enters the last stretch of breaking water upwind of this cell along <paramref name="ray"/>: the sea that comes to the shore, whatever the shallows then leave of it.</summary>
    public double ArrivingAt(float[] depth, int size, int column, int row, (double X, double Z) ray)
    {
        var length = Math.Sqrt(ray.X * ray.X + ray.Z * ray.Z);
        if (length <= 0) return 1;
        var last = -1;
        for (var step = 0; step < 2 * size; step++)
        {
            var (back, up) = ((int)Math.Round(column - ray.X / length * step), (int)Math.Round(row - ray.Z / length * step));
            if (back < 0 || up < 0 || back >= size || up >= size) return 1;
            var i = up * size + back;
            if (!(depth[i] > 0)) return last >= 0 ? Math.Min(1, Gain[last]) : 1;
            if (Breaking[i] <= 0) return Math.Min(1, Gain[i]);
            last = i;
        }
        return 1;
    }
}

/// <summary>What a bed does to waves arriving over it: how each slot is held back and turned (<see cref="PhaseField"/>), and what energy it keeps (<see cref="WaveEnergy"/>), each at the depth that wave feels.</summary>
public sealed class WaveTransformation(double breakingDepthRatio, Func<double, int> smoothingBlocks, double breakingDissipation, double terminalBoreShare)
{
    private const float DeepestFelt = 200;
    private const float ShallowestFelt = 0.05f;
    private const int Banks = 2;

    /// <param name="seaHeights">The significant height of the sea standing on each cell.</param>
    public TransformedWaves Solve(int size, float[] depth, (double X, double Z)[] numbers, double[]? amplitudeShares, double[] seaHeights)
    {
        var smoothing = smoothingBlocks(numbers.Select(WaveLength).DefaultIfEmpty(0).Max());
        var felt = Felt(depth, size, smoothing);
        var felts = numbers.Take(SurfField.Slots).Select(number => Felt(depth, size, smoothingBlocks(WaveLength(number)))).ToArray();
        var boreDepth = (float)(terminalBoreShare * seaHeights.Max());
        float[][] LagsOver(Func<int, float, int, float> celerityDepth) => felts.Select((each, slot) =>
            PhaseField.Lag(size, each.Select((d, i) => d > 0 ? Math.Max(celerityDepth(slot, d, i), boreDepth) : d).ToArray(), numbers[slot].X, numbers[slot].Z)).ToArray();
        var lags = LagsOver((_, d, _) => d);

        var gains = numbers.Take(SurfField.Slots).Select(_ => Enumerable.Repeat(1f, size * size).ToArray()).ToArray();
        var breaking = new float[size * size];
        var gain = new float[size * size];
        var seaHeight = seaHeights.Max();
        var seaShare = seaHeights.Select(height => seaHeight > 0 ? (float)(height / seaHeight) : 1f).ToArray();
        var generation = amplitudeShares == null ? numbers.Length : numbers.Length / Banks;
        for (var bank = 0; bank * generation < Math.Min(numbers.Length, SurfField.Slots); bank++)
        {
            var slots = Enumerable.Range(bank * generation, generation).Where(slot => slot < lags.Length).ToArray();
            var trains = slots.Select(slot => new WaveEnergy.Train(numbers[slot].X, numbers[slot].Z, (amplitudeShares?[slot] ?? 1 / (2 * Math.Sqrt(2) * Math.Sqrt(generation))) * seaHeight, lags[slot], felts[slot])).ToArray();
            var (solved, broken) = WaveEnergy.Solve(size, felt, trains, breakingDepthRatio, breakingDissipation, depth, seaShare);
            var deep = trains.Sum(train => train.Amplitude * train.Amplitude);
            for (var i = 0; i < broken.Length; i++)
            {
                breaking[i] = Math.Max(breaking[i], broken[i]);
                if (deep > 0 && slots.Any(slot => numbers[slot] != default)) gain[i] = Math.Max(gain[i], (float)Math.Sqrt(trains.Select((train, index) => train.Amplitude * train.Amplitude * solved[index][i] * solved[index][i]).Sum() / deep));
                for (var train = 0; train < slots.Length; train++) gains[slots[train]][i] = numbers[slots[train]] == default ? 1 : solved[train][i];
            }
        }
        var boreHeight = Enumerable.Range(0, depth.Length).Select(i => (float)(gain[i] * seaHeights[i] / Math.Sqrt(2) * breaking[i])).ToArray();
        lags = LagsOver((_, d, i) => d + boreHeight[i]);
        var celerityDepth = felt.Select((d, i) => d > 0 ? Math.Max(d + boreHeight[i], boreDepth) : boreDepth).ToArray();

        var slopeX = BoxAverage.Over(Enumerable.Range(0, depth.Length).Select(i => Gradient(depth, size, i, 1)).ToArray(), size, smoothing);
        var slopeZ = BoxAverage.Over(Enumerable.Range(0, depth.Length).Select(i => Gradient(depth, size, i, size)).ToArray(), size, smoothing);
        return new TransformedWaves(felt, lags, gains, breaking, gain, slopeX, slopeZ, celerityDepth);
    }

    private static float Gradient(float[] depth, int size, int i, int step)
    {
        var (column, row) = (i % size, i / size);
        var (back, ahead) = step == 1 ? (column > 0 ? i - 1 : i, column < size - 1 ? i + 1 : i) : (row > 0 ? i - size : i, row < size - 1 ? i + size : i);
        if (back == ahead || float.IsNaN(depth[back]) || float.IsNaN(depth[ahead])) return 0;
        return (depth[ahead] - depth[back]) / ((ahead - back) / step);
    }

    private static double WaveLength((double X, double Z) number)
    {
        var size = Math.Sqrt(number.X * number.X + number.Z * number.Z);
        return size > 0 ? 2 * Math.PI / size : 0;
    }

    private static float[] Felt(float[] depth, int size, int radius)
    {
        var averaged = BoxAverage.Over(depth.Select(d => float.IsNaN(d) ? 0 : Math.Clamp(d, 0, DeepestFelt)).ToArray(), size, radius);
        return depth.Select((d, i) => d > 0 ? Math.Max(averaged[i], ShallowestFelt) : d).ToArray();
    }
}
