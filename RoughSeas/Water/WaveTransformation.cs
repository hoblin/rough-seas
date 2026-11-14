using System;
using System.Linq;

namespace RoughSeas.Water;

/// <param name="Felt">The depth under still water that the longest wave feels, cell by cell.</param>
/// <param name="Lags">For each slot, the phase the bed holds the wave back by.</param>
/// <param name="Gains">For each slot, its height over its height in deep water.</param>
/// <param name="Breaking">The share of waves breaking.</param>
/// <param name="Gain">The whole sea's height over its height in deep water.</param>
/// <param name="SlopeX">The depth's slope along X, at the scale the longest wave feels it.</param>
/// <param name="SlopeZ">The same along Z.</param>
/// <param name="CelerityDepth">The depth that sets the waves' speed: the water under a wave and, where it has broken, the bore's own height over it, never less than that of the bore reaching the nearest shoreline. Over land, that bore's.</param>
/// <param name="Entering">Where the breaking water runs off the grid, the sea's height coming into it, over its height in deep water, by column and row; none is deep water's.</param>
public sealed record TransformedWaves(float[] Felt, float[][] Lags, float[][] Gains, float[] Breaking, float[] Gain, float[] SlopeX, float[] SlopeZ, float[] CelerityDepth, Func<int, int, double>? Entering = null)
{
    /// <summary>The sea's height, against the deep sea's, where it enters the last stretch of breaking water upwind of this cell along <paramref name="ray"/>: the sea that comes to the shore, whatever the shallows then leave of it.</summary>
    public double ArrivingAt(float[] depth, int size, int column, int row, (double X, double Z) ray) => Arriving(Gain, Breaking, Entering, depth, size, column, row, ray);

    internal static double Arriving(float[] gain, float[] breaking, Func<int, int, double>? entering, float[] depth, int size, int column, int row, (double X, double Z) ray)
    {
        var length = Math.Sqrt(ray.X * ray.X + ray.Z * ray.Z);
        if (length <= 0) return 1;
        var last = -1;
        for (var step = 0; step < 2 * size; step++)
        {
            var (back, up) = ((int)Math.Round(column - ray.X / length * step), (int)Math.Round(row - ray.Z / length * step));
            if (back < 0 || up < 0 || back >= size || up >= size) return Math.Min(1, entering?.Invoke(Math.Clamp(back, 0, size - 1), Math.Clamp(up, 0, size - 1)) ?? 1);
            var i = up * size + back;
            if (!(depth[i] > 0)) return last >= 0 ? Math.Min(1, gain[last]) : 1;
            if (breaking[i] <= 0) return Math.Min(1, gain[i]);
            last = i;
        }
        return 1;
    }
}

/// <summary>What a bed does to waves arriving over it: how each slot is held back and turned (<see cref="PhaseField"/>), and what energy it keeps (<see cref="WaveEnergy"/>), each at the depth that wave feels.</summary>
public sealed class WaveTransformation(double breakingDepthRatio, Func<double, int> smoothingBlocks, double breakingDissipation, double boreCelerityShare)
{
    private const float DeepestFelt = 200;
    private const float ShallowestFelt = 0.05f;
    private const int Banks = 2;

    /// <param name="seaHeights">The significant height of the sea standing on each cell.</param>
    /// <param name="dominant">The slot that dominates the sea now: its generation sets the whole sea's gain and breaking.</param>
    /// <param name="entering">The waves where they enter this grid; none enter as from deep water, with no lag.</param>
    /// <param name="spacing">How many blocks a cell is across.</param>
    /// <param name="shoreBore">The depth that sets the speed of the bore reaching the nearest shoreline, cell by cell: the swash it throws leaves at that speed, so no broken wave runs slower, in the share of waves broken; none is 0.</param>
    public TransformedWaves Solve(int size, float[] depth, (double X, double Z)[] numbers, double[]? amplitudeShares, double[] seaHeights, int dominant = 0, EnteringWaves? entering = null, int spacing = 1, float[]? shoreBore = null) =>
        Shoal(size, depth, numbers, amplitudeShares, seaHeights, dominant, entering, spacing).Broken(shoreBore);

    /// <summary>What <see cref="Solve"/> works out before the broken water's speed is known: the energy each slot keeps over the bed, and where the sea breaks. <see cref="Shoaling.Broken"/> finishes it for a shore bore.</summary>
    public Shoaling Shoal(int size, float[] depth, (double X, double Z)[] numbers, double[]? amplitudeShares, double[] seaHeights, int dominant = 0, EnteringWaves? entering = null, int spacing = 1)
    {
        var anchors = entering?.Lags;
        int Radius(double length) => smoothingBlocks(length) / spacing;
        var smoothing = Radius(numbers.Select(WaveLength).DefaultIfEmpty(0).Max());
        var felt = Felt(depth, size, smoothing);
        var felts = numbers.Take(SurfField.Slots).Select(number => Felt(depth, size, Radius(WaveLength(number)))).ToArray();
        var lags = Lags(size, felts, numbers, anchors, spacing, (d, _) => d);

        var gains = numbers.Take(SurfField.Slots).Select(_ => Enumerable.Repeat(1f, size * size).ToArray()).ToArray();
        var breaking = new float[size * size];
        var gain = new float[size * size];
        var seaHeight = seaHeights.Max();
        var seaShare = seaHeights.Select(height => seaHeight > 0 ? (float)(height / seaHeight) : 1f).ToArray();
        var generation = amplitudeShares == null ? numbers.Length : numbers.Length / Banks;
        for (var bank = 0; bank * generation < Math.Min(numbers.Length, SurfField.Slots); bank++)
        {
            var slots = Enumerable.Range(bank * generation, generation).Where(slot => slot < lags.Length).ToArray();
            var trains = slots.Select(slot => new WaveEnergy.Train(numbers[slot].X, numbers[slot].Z, (amplitudeShares?[slot] ?? 1 / (2 * Math.Sqrt(2) * Math.Sqrt(generation))) * seaHeight, lags[slot], felts[slot], Radius(WaveLength(numbers[slot])), entering?.Gains[slot])).ToArray();
            var (solved, broken) = WaveEnergy.Solve(size, felt, trains, breakingDepthRatio, breakingDissipation, depth, seaShare, spacing);
            var deep = trains.Sum(train => train.Amplitude * train.Amplitude);
            var live = bank == dominant / generation;
            var waving = live && deep > 0 && slots.Any(slot => numbers[slot] != default);
            for (var i = 0; i < broken.Length; i++)
            {
                if (live) breaking[i] = broken[i];
                if (waving)
                {
                    var energy = 0.0;
                    for (var index = 0; index < trains.Length; index++) energy += trains[index].Amplitude * trains[index].Amplitude * solved[index][i] * solved[index][i];
                    gain[i] = (float)Math.Sqrt(energy / deep);
                }
                for (var train = 0; train < slots.Length; train++) gains[slots[train]][i] = numbers[slots[train]] == default ? 1 : solved[train][i];
            }
        }
        return new Shoaling(boreCelerityShare, size, depth, numbers, anchors, spacing, smoothing, felt, felts, gains, breaking, gain, seaHeights, entering?.Arriving);
    }

    private static float[][] Lags(int size, float[][] felts, (double X, double Z)[] numbers, Func<int, int, double>[]? anchors, int spacing, Func<float, int, float> celerityDepth) =>
        felts.Select((each, slot) => PhaseField.Lag(size, each.Select((d, i) => d > 0 ? celerityDepth(d, i) : d).ToArray(), numbers[slot].X, numbers[slot].Z, anchors?[slot], spacing)).ToArray();

    public sealed record Shoaling(double BoreCelerityShare, int Size, float[] Depth, (double X, double Z)[] Numbers, Func<int, int, double>[]? Anchors, int Spacing, int Smoothing,
        float[] Felt, float[][] Felts, float[][] Gains, float[] Breaking, float[] Gain, double[] SeaHeights, Func<int, int, double>? Entering)
    {
        public double ArrivingAt(int column, int row, (double X, double Z) ray) => TransformedWaves.Arriving(Gain, Breaking, Entering, Depth, Size, column, row, ray);

        public TransformedWaves Broken(float[]? shoreBore = null)
        {
            var boreDepth = shoreBore ?? new float[Depth.Length];
            var boreHeight = Enumerable.Range(0, Depth.Length).Select(i => (float)(BoreCelerityShare * Gain[i] * SeaHeights[i] * Breaking[i])).ToArray();
            float Broken(float d, int i) => Math.Max(d + boreHeight[i], boreDepth[i] * Breaking[i]);
            var lags = Lags(Size, Felts, Numbers, Anchors, Spacing, Broken);
            var celerityDepth = Felt.Select((d, i) => d > 0 ? Broken(d, i) : boreDepth[i]).ToArray();

            var slopeX = BoxAverage.Over(Enumerable.Range(0, Depth.Length).Select(i => Gradient(Depth, Size, i, 1) / Spacing).ToArray(), Size, Smoothing);
            var slopeZ = BoxAverage.Over(Enumerable.Range(0, Depth.Length).Select(i => Gradient(Depth, Size, i, Size) / Spacing).ToArray(), Size, Smoothing);
            return new TransformedWaves(Felt, lags, Gains, Breaking, Gain, slopeX, slopeZ, celerityDepth, Entering);
        }
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
