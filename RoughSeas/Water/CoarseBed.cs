using System;
using System.Linq;

namespace RoughSeas.Water;

/// <param name="Lags">For each slot, the lag where the wave enters a grid, by its column and row.</param>
/// <param name="Gains">For each slot, its height there over its height in deep water.</param>
/// <param name="Gain">The whole sea's height there over its height in deep water.</param>
public sealed record EnteringWaves(Func<int, int, double>[] Lags, Func<int, int, double>[] Gains, Func<int, int, double> Gain);

/// <summary>The water and the sea over the regions around a place, a cell of the sea map at a time: what every reader of the place shares, so that the waves entering a finer grid have one reference there.</summary>
public sealed record CoarseBed(int OriginX, int OriginZ, int CellBlocks, int Across, float[] Depth, double[] SeaHeights)
{
    /// <summary>The waves the same <paramref name="transformation"/> brings over this bed, by column and row of a grid whose first cell is at <paramref name="gridX"/>, <paramref name="gridZ"/>. Land takes the nearest water's.</summary>
    public EnteringWaves Entering(WaveTransformation transformation, (double X, double Z)[] numbers, double[]? amplitudeShares, int dominant, int gridX, int gridZ)
    {
        var nearest = PhaseField.NearestWater(Depth, Across);
        var solved = transformation.Solve(Across, Depth, numbers, amplitudeShares, SeaHeights, dominant, spacing: CellBlocks);
        Func<int, int, double> Read(float[] values, float none) => Reader(values.Select((_, i) => nearest[i] >= 0 ? values[nearest[i]] : none).ToArray(), gridX, gridZ);
        return new EnteringWaves(solved.Lags.Select(lag => Read(lag, 0)).ToArray(), solved.Gains.Select(gain => Read(gain, 1)).ToArray(), Read(solved.Gain, 1));
    }

    private Func<int, int, double> Reader(float[] values, int gridX, int gridZ) => (column, row) =>
    {
        var u = Math.Clamp((gridX + column + 0.5 - OriginX) / CellBlocks - 0.5, 0, Across - 1);
        var v = Math.Clamp((gridZ + row + 0.5 - OriginZ) / CellBlocks - 0.5, 0, Across - 1);
        var (left, top) = ((int)Math.Floor(u), (int)Math.Floor(v));
        var (right, bottom) = (Math.Min(left + 1, Across - 1), Math.Min(top + 1, Across - 1));
        var (tu, tv) = (u - left, v - top);
        double At(int x, int z) => values[z * Across + x];
        var upper = At(left, top) + (At(right, top) - At(left, top)) * tu;
        var lower = At(left, bottom) + (At(right, bottom) - At(left, bottom)) * tu;
        return upper + (lower - upper) * tv;
    };
}
