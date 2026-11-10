using System;

namespace RoughSeas.Water;

/// <summary>The depth of the water over the regions around a place, a cell of the sea map at a time: what every reader of the place shares, so that a wave's lag has one reference there.</summary>
public sealed record CoarseBed(int OriginX, int OriginZ, int CellBlocks, int Across, float[] Depth)
{
    /// <summary>The lag this bed gives a wave vector, by column and row of a grid whose first cell is at <paramref name="gridX"/>, <paramref name="gridZ"/>.</summary>
    public Func<int, int, double> Anchor(double numberX, double numberZ, int gridX, int gridZ)
    {
        var lag = PhaseField.Lag(Across, Depth, numberX, numberZ, spacing: CellBlocks);
        return (column, row) =>
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
}
