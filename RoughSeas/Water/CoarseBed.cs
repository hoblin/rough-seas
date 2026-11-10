using System;
using System.Linq;

namespace RoughSeas.Water;

/// <summary>The water and the sea over the regions around a place, a cell of the sea map at a time: what every reader of the place shares, so that a wave's lag has one reference there.</summary>
public sealed record CoarseBed(int OriginX, int OriginZ, int CellBlocks, int Across, float[] Depth, double[] SeaHeights)
{
    /// <summary>For each slot, the lag the same <paramref name="transformation"/> gives its wave over this bed, by column and row of a grid whose first cell is at <paramref name="gridX"/>, <paramref name="gridZ"/>.</summary>
    public Func<int, int, double>[] Anchors(WaveTransformation transformation, (double X, double Z)[] numbers, double[]? amplitudeShares, int dominant, int gridX, int gridZ)
    {
        var nearest = PhaseField.NearestWater(Depth, Across);
        return transformation.Solve(Across, Depth, numbers, amplitudeShares, SeaHeights, dominant, spacing: CellBlocks).Lags
            .Select(lag => Reader(lag.Select((_, i) => nearest[i] >= 0 ? lag[nearest[i]] : 0).ToArray(), gridX, gridZ)).ToArray();
    }

    private Func<int, int, double> Reader(float[] lag, int gridX, int gridZ) => (column, row) =>
    {
        var u = Math.Clamp((gridX + column + 0.5 - OriginX) / CellBlocks - 0.5, 0, Across - 1);
        var v = Math.Clamp((gridZ + row + 0.5 - OriginZ) / CellBlocks - 0.5, 0, Across - 1);
        var (left, top) = ((int)Math.Floor(u), (int)Math.Floor(v));
        var (right, bottom) = (Math.Min(left + 1, Across - 1), Math.Min(top + 1, Across - 1));
        var (tu, tv) = (u - left, v - top);
        double At(int x, int z) => lag[z * Across + x];
        var upper = At(left, top) + (At(right, top) - At(left, top)) * tu;
        var lower = At(left, bottom) + (At(right, bottom) - At(left, bottom)) * tu;
        return upper + (lower - upper) * tv;
    };
}
